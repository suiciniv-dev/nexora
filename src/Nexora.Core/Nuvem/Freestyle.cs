using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nexora.Core.Nuvem;

public sealed class ErroDaFreestyle(int status, string codigo, string mensagem) : InvalidOperationException(mensagem)
{
    public int Status { get; } = status;
    public string Codigo { get; } = codigo;
}

public sealed record TokenDaFreestyle(string Identidade, string Token, DateTimeOffset Expira);

public sealed class ClienteFreestyle
{
    public static readonly Uri EnderecoPadrao = new("https://api.freestyle.sh/");
    public const string HostSsh = "beta-ssh.freestyle.sh";
    public static TimeSpan LimiteDaOperacao { get; set; } = TimeSpan.FromMinutes(5);
    public static TimeSpan IntervaloDaEspera { get; set; } = TimeSpan.FromSeconds(2);

    readonly Func<string?> _chave;
    readonly HttpClient _http;

    public ClienteFreestyle(Func<string?> chave, HttpMessageHandler? manipulador = null, Uri? endereco = null)
    {
        _chave = chave;
        _http = manipulador is null ? new HttpClient() : new HttpClient(manipulador, disposeHandler: false);
        _http.BaseAddress = endereco ?? EnderecoPadrao;
        _http.Timeout = TimeSpan.FromSeconds(60);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Nexora", "1.0"));
    }

    public bool TemChave => !string.IsNullOrWhiteSpace(_chave());

    public async Task<int> ContarAsync(CancellationToken cancelamento = default)
    {
        var json = await EnviarAsync(HttpMethod.Get, "v5/vms?limit=1", null, cancelamento).ConfigureAwait(false);
        return json?["totalCount"] is JsonValue v && v.TryGetValue<int>(out var n) ? n : 0;
    }

    public async Task<string> CriarAsync(string nome, CancellationToken cancelamento = default)
    {
        var corpo = new JsonObject
        {
            ["displayName"] = nome,
            ["metadata"] = new JsonObject { ["criada-por"] = "nexora" },
            ["firewall"] = new JsonObject
            {
                ["rules"] = new JsonArray(new JsonObject
                {
                    ["action"] = "allow",
                    ["source"] = new JsonObject(),
                    ["destination"] = new JsonObject { ["public"] = true },
                }),
            },
        };
        var json = await EnviarAsync(HttpMethod.Post, "v5/vms", corpo, cancelamento).ConfigureAwait(false);
        var id = Valor(json, "id") ?? Valor(json, "vmId") ?? Valor(json?["vm"], "id")
            ?? throw new ErroDaFreestyle(0, "resposta", Texto.Idioma.T("A Freestyle não disse qual máquina criou."));
        await EsperarAsync(id, "running", cancelamento).ConfigureAwait(false);
        return id;
    }

    public async Task<string> EstadoAsync(string id, CancellationToken cancelamento = default)
    {
        var json = await EnviarAsync(HttpMethod.Get, "v5/vms/" + Uri.EscapeDataString(id), null, cancelamento).ConfigureAwait(false);
        return Valor(json, "state") ?? Valor(json?["vm"], "state") ?? "";
    }

    public static string EstadoComum(string estado) => estado switch
    {
        "paused" => "suspended",
        "pausing" => "suspending",
        _ => estado,
    };

    public async Task LigarAsync(string id, CancellationToken cancelamento = default)
    {
        var estado = await EstadoAsync(id, cancelamento).ConfigureAwait(false);
        if (estado is "running") return;
        if (estado is "pausing") await EsperarAsync(id, "paused", cancelamento).ConfigureAwait(false);
        if (estado is not "starting") await EnviarAsync(HttpMethod.Post, "v5/vms/" + Uri.EscapeDataString(id) + "/start", null, cancelamento).ConfigureAwait(false);
        await EsperarAsync(id, "running", cancelamento).ConfigureAwait(false);
    }

    public async Task PausarAsync(string id, CancellationToken cancelamento = default)
    {
        try
        {
            await EnviarAsync(HttpMethod.Post, "v5/vms/" + Uri.EscapeDataString(id) + "/pause", null, cancelamento).ConfigureAwait(false);
        }
        catch (ErroDaFreestyle ex) when (ex.Status == 409)
        {
            return;
        }
        await EsperarAsync(id, "paused", cancelamento).ConfigureAwait(false);
    }

    public async Task ApagarAsync(string id, CancellationToken cancelamento = default)
    {
        try
        {
            await EnviarAsync(HttpMethod.Delete, "v5/vms/" + Uri.EscapeDataString(id), null, cancelamento).ConfigureAwait(false);
        }
        catch (ErroDaFreestyle ex) when (ex.Status == 404)
        {
        }
    }

    public async Task<TokenDaFreestyle> TokenAsync(string vm, string? identidade, TimeSpan validade, CancellationToken cancelamento = default)
    {
        if (identidade is null)
        {
            var criada = await EnviarAsync(HttpMethod.Post, "v5/identities", new JsonObject(), cancelamento).ConfigureAwait(false);
            identidade = Valor(criada, "id") ?? throw new ErroDaFreestyle(0, "resposta", Texto.Idioma.T("A Freestyle não mandou a identidade do SSH."));
            await EnviarAsync(HttpMethod.Post, "v5/identities/" + Uri.EscapeDataString(identidade) + "/permissions/vm", new JsonObject { ["vmId"] = vm }, cancelamento).ConfigureAwait(false);
        }
        var expira = DateTimeOffset.UtcNow.Add(validade);
        var json = await EnviarAsync(HttpMethod.Post, "v5/identities/" + Uri.EscapeDataString(identidade) + "/tokens",
            new JsonObject { ["expiresAt"] = expira.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) }, cancelamento).ConfigureAwait(false);
        var token = Valor(json, "token") ?? throw new ErroDaFreestyle(0, "resposta", Texto.Idioma.T("A Freestyle não mandou o token do SSH."));
        return new TokenDaFreestyle(identidade, token,
            DateTimeOffset.TryParse(Valor(json, "expiresAt"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var data) ? data : expira);
    }

    public async Task ApagarIdentidadeAsync(string identidade, CancellationToken cancelamento = default)
    {
        try
        {
            await EnviarAsync(HttpMethod.Delete, "v5/identities/" + Uri.EscapeDataString(identidade), null, cancelamento).ConfigureAwait(false);
        }
        catch (ErroDaFreestyle ex) when (ex.Status == 404)
        {
        }
    }

    async Task EsperarAsync(string id, string desejado, CancellationToken cancelamento)
    {
        var relogio = Stopwatch.StartNew();
        while (true)
        {
            var estado = await EstadoAsync(id, cancelamento).ConfigureAwait(false);
            if (estado == desejado) return;
            if (estado is "stopped" && desejado == "running")
                await EnviarAsync(HttpMethod.Post, "v5/vms/" + Uri.EscapeDataString(id) + "/start", null, cancelamento).ConfigureAwait(false);
            if (relogio.Elapsed > LimiteDaOperacao) throw new TimeoutException(Texto.Idioma.T("A Freestyle demorou demais para terminar."));
            await Task.Delay(IntervaloDaEspera, cancelamento).ConfigureAwait(false);
        }
    }

    async Task<JsonNode?> EnviarAsync(HttpMethod metodo, string caminho, JsonObject? corpo, CancellationToken cancelamento)
    {
        var chave = _chave()?.Trim();
        if (string.IsNullOrEmpty(chave)) throw new ErroDaFreestyle(401, "missing_key", Texto.Idioma.T("Falta a chave de API da Freestyle em Configurações › Máquinas na nuvem."));
        using var pedido = new HttpRequestMessage(metodo, caminho);
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chave);
        if (corpo is not null) pedido.Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json");
        using var resposta = await _http.SendAsync(pedido, cancelamento).ConfigureAwait(false);
        var texto = await resposta.Content.ReadAsStringAsync(cancelamento).ConfigureAwait(false);
        JsonNode? json = null;
        try
        {
            if (texto.Length > 0) json = JsonNode.Parse(texto);
        }
        catch (JsonException)
        {
        }
        if (resposta.IsSuccessStatusCode) return json;
        var codigo = Valor(json, "code") ?? ((int)resposta.StatusCode).ToString(CultureInfo.InvariantCulture);
        throw new ErroDaFreestyle((int)resposta.StatusCode, codigo, Explicar(codigo, Valor(json, "message") ?? resposta.ReasonPhrase));
    }

    public static string Explicar(string? codigo, string? mensagem) => codigo switch
    {
        "UNAUTHORIZED_ERROR" or "401" => Texto.Idioma.T("A Freestyle recusou a chave de API. Crie outra no painel da Freestyle e cole de novo."),
        "LIMIT_EXCEEDED" or "429" => Texto.Idioma.T("O plano da Freestyle chegou ao limite (máquinas ou horas do mês). Apague uma máquina ou espere o mês virar."),
        "NOT_FOUND" or "404" => Texto.Idioma.T("A máquina não existe mais na Freestyle."),
        _ => Texto.Idioma.F("A Freestyle respondeu: {0}", mensagem ?? codigo ?? "?"),
    };

    static string? Valor(JsonNode? no, string nome) =>
        no is JsonObject o && o[nome] is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s : null;
}

public sealed class TransporteFreestyle(MaquinaNaNuvem dados, ProvedorFreestyle provedor) : ITransporte
{
    readonly SemaphoreSlim _trava = new(1, 1);
    TokenDaFreestyle? _token;

    public static TimeSpan Validade { get; set; } = TimeSpan.FromHours(1);
    public static TimeSpan Folga { get; set; } = TimeSpan.FromMinutes(5);

    public async Task AntesAsync(CancellationToken cancelamento)
    {
        if (Valido(_token)) return;
        await _trava.WaitAsync(cancelamento).ConfigureAwait(false);
        try
        {
            if (Valido(_token)) return;
            var id = dados.IdNoProvedor ?? throw new InvalidOperationException(Texto.Idioma.T("Esta máquina não tem o número dela na Freestyle."));
            await provedor.Cliente.LigarAsync(id, cancelamento).ConfigureAwait(false);
            var token = await provedor.Cliente.TokenAsync(id, provedor.Identidade(dados), Validade, cancelamento).ConfigureAwait(false);
            provedor.GuardarIdentidade(dados, token.Identidade);
            _token = token;
        }
        catch (HttpRequestException ex)
        {
            throw new ConexaoCaiuException(Texto.Idioma.F("Não deu para falar com a Freestyle: {0}", ex.Message));
        }
        catch (TaskCanceledException ex) when (!cancelamento.IsCancellationRequested)
        {
            throw new ConexaoCaiuException(Texto.Idioma.F("A Freestyle não respondeu: {0}", ex.Message));
        }
        finally
        {
            _trava.Release();
        }
    }

    public void Caiu() => _token = null;

    static bool Valido(TokenDaFreestyle? token) => token is not null && token.Expira - DateTimeOffset.UtcNow > Folga;

    public ProcessStartInfo Comando(string comandoRemoto) => Ssh().Comando(comandoRemoto);

    public ProcessStartInfo? Tunel(int portaLocal, int portaRemota) => Ssh().Tunel(portaLocal, portaRemota);

    TransporteSsh Ssh()
    {
        var token = _token ?? throw new InvalidOperationException(Texto.Idioma.T("A conexão com a Freestyle ainda não foi aberta."));
        var destino = dados with { Host = ClienteFreestyle.HostSsh, Usuario = dados.IdNoProvedor ?? "", Porta = 22 };
        return new TransporteSsh(destino, provedor.Chave, provedor.Conhecidos, senha: token.Token);
    }
}

public sealed class ProvedorFreestyle(string pasta, string chave, ClienteFreestyle cliente) : IProvedorDeMaquinas
{
    public string Tipo => TiposDeMaquina.Freestyle;
    public bool CriaMaquinas => true;
    public bool Pausa => true;
    public bool PublicaPortas => false;

    public string Pasta { get; } = Path.Combine(pasta, "freestyle");
    public string Chave { get; } = chave;
    public ClienteFreestyle Cliente { get; } = cliente;
    public string Conhecidos => Path.Combine(Pasta, "known_hosts");

    string ArquivoDaIdentidade(MaquinaNaNuvem dados) => Path.Combine(Pasta, dados.Id + ".identidade");

    public string? Identidade(MaquinaNaNuvem dados) =>
        File.Exists(ArquivoDaIdentidade(dados)) && File.ReadAllText(ArquivoDaIdentidade(dados)).Trim() is { Length: > 0 } id ? id : null;

    public void GuardarIdentidade(MaquinaNaNuvem dados, string identidade)
    {
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(ArquivoDaIdentidade(dados), identidade);
    }

    public IMaquinaRemota Abrir(MaquinaNaNuvem dados) => new MaquinaRemota(dados, new TransporteFreestyle(dados, this));

    public Task<MaquinaNaNuvem> CriarAsync(string nome, CancellationToken cancelamento = default) => CriarAsync(nome, null, cancelamento);

    public async Task<MaquinaNaNuvem> CriarAsync(string nome, string? modelo, CancellationToken cancelamento = default)
    {
        var id = await Cliente.CriarAsync(nome, cancelamento).ConfigureAwait(false);
        return new MaquinaNaNuvem(MaquinaNaNuvem.NovoId(), nome, TiposDeMaquina.Freestyle, IdNoProvedor: id, Home: null);
    }

    public Task RetomarAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default) =>
        Cliente.LigarAsync(maquina.IdNoProvedor ?? throw new InvalidOperationException(), cancelamento);

    public Task PausarAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default) =>
        Cliente.PausarAsync(maquina.IdNoProvedor ?? throw new InvalidOperationException(), cancelamento);

    public async Task<string> EstadoAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default) =>
        ClienteFreestyle.EstadoComum(await Cliente.EstadoAsync(maquina.IdNoProvedor ?? throw new InvalidOperationException(), cancelamento).ConfigureAwait(false));

    public async Task ApagarAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default)
    {
        if (maquina.IdNoProvedor is { } id) await Cliente.ApagarAsync(id, cancelamento).ConfigureAwait(false);
        if (Identidade(maquina) is { } identidade) await Cliente.ApagarIdentidadeAsync(identidade, cancelamento).ConfigureAwait(false);
        try
        {
            File.Delete(ArquivoDaIdentidade(maquina));
        }
        catch (IOException)
        {
        }
    }

    public Task<string> PublicarPortaAsync(MaquinaNaNuvem maquina, int porta, CancellationToken cancelamento = default) => throw new NotSupportedException();
    public Task<CotaDaMaquina?> CotaAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default) => Task.FromResult<CotaDaMaquina?>(null);
}
