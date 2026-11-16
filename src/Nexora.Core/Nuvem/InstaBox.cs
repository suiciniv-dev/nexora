using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nexora.Core.Nuvem;

public sealed record ModeloInstaBox(string Id, string? Descricao, bool Criavel, bool Padrao, int Vcpus, int MemoriaMiB);

public sealed record EstadoInstaBox(string Id, string Nome, string Estado, string? Modelo, DateTimeOffset? DescansaAte = null, string? MotivoDoDescanso = null);

public sealed record CredencialInstaBox(string Host, int Porta, string Usuario, string Certificado, string ChaveDaAutoridade, DateTimeOffset Expira);

public sealed class ErroDoInstaBox(int status, string codigo, string mensagem, TimeSpan? tentarDepois = null) : InvalidOperationException(mensagem)
{
    public int Status { get; } = status;
    public string Codigo { get; } = codigo;
    public TimeSpan? TentarDepois { get; } = tentarDepois;
}

public sealed class ClienteInstaBox
{
    public static readonly Uri EnderecoPadrao = new("https://api.instabox.sh/");
    public static TimeSpan LimiteDaOperacao { get; set; } = TimeSpan.FromMinutes(10);

    readonly Func<string?> _chave;
    readonly HttpClient _http;

    public ClienteInstaBox(Func<string?> chave, HttpMessageHandler? manipulador = null, Uri? endereco = null)
    {
        _chave = chave;
        _http = manipulador is null ? new HttpClient() : new HttpClient(manipulador, disposeHandler: false);
        _http.BaseAddress = endereco ?? EnderecoPadrao;
        _http.Timeout = TimeSpan.FromSeconds(60);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Nexora", "1.0"));
    }

    public bool TemChave => !string.IsNullOrWhiteSpace(_chave());

    public async Task<IReadOnlyList<ModeloInstaBox>> ModelosAsync(CancellationToken cancelamento = default)
    {
        var json = await EnviarAsync(HttpMethod.Get, "v1/templates", null, false, null, cancelamento).ConfigureAwait(false);
        List<ModeloInstaBox> modelos = [];
        foreach (var item in json?["templates"]?.AsArray() ?? [])
        {
            if (item is null) continue;
            var spec = item["defaultSpec"];
            modelos.Add(new ModeloInstaBox(
                Valor(item, "id") ?? "",
                Valor(item, "description"),
                item["creatable"]?.GetValue<bool>() ?? true,
                item["default"]?.GetValue<bool>() ?? false,
                spec?["vcpus"]?.GetValue<int>() ?? 0,
                spec?["memMiB"]?.GetValue<int>() ?? 0));
        }
        return modelos.Where(m => m.Id.Length > 0).ToList();
    }

    public async Task<EstadoInstaBox> MaquinaAsync(string id, CancellationToken cancelamento = default)
    {
        var json = await EnviarAsync(HttpMethod.Get, "v1/machines/" + Uri.EscapeDataString(id), null, false, null, cancelamento).ConfigureAwait(false);
        return LerMaquina(json);
    }

    public async Task<string> CriarAsync(string nome, string? modelo, CancellationToken cancelamento = default)
    {
        var corpo = new JsonObject { ["name"] = nome };
        if (!string.IsNullOrEmpty(modelo)) corpo["templateId"] = modelo;
        var operacao = await EnviarAsync(HttpMethod.Post, "v1/machines", corpo, true, null, cancelamento).ConfigureAwait(false);
        var id = Valor(operacao, "machineId") ?? throw new ErroDoInstaBox(0, "resposta", Texto.Idioma.T("O InstaBox não disse qual máquina criou."));
        await EsperarAsync(operacao, cancelamento).ConfigureAwait(false);
        return id;
    }

    public Task LigarAsync(string id, CancellationToken cancelamento = default) => AcaoAsync(id, "start", cancelamento);
    public Task PausarAsync(string id, CancellationToken cancelamento = default) => AcaoAsync(id, "suspend", cancelamento);
    public Task RetomarAsync(string id, CancellationToken cancelamento = default) => AcaoAsync(id, "resume", cancelamento);

    public async Task ApagarAsync(string id, CancellationToken cancelamento = default)
    {
        try
        {
            var operacao = await EnviarAsync(HttpMethod.Delete, "v1/machines/" + Uri.EscapeDataString(id), null, true, id, cancelamento).ConfigureAwait(false);
            await EsperarAsync(operacao, cancelamento).ConfigureAwait(false);
        }
        catch (ErroDoInstaBox ex) when (ex.Status == 404)
        {
        }
    }

    public async Task<CredencialInstaBox> ConectarAsync(string id, string chavePublica, int validadeEmSegundos = 3600, CancellationToken cancelamento = default)
    {
        var corpo = new JsonObject { ["publicKey"] = chavePublica.Trim(), ["ttlSeconds"] = validadeEmSegundos };
        var json = await EnviarAsync(HttpMethod.Post, "v1/machines/" + Uri.EscapeDataString(id) + "/connections", corpo, true, null, cancelamento).ConfigureAwait(false);
        return new CredencialInstaBox(
            Valor(json, "host") ?? throw new ErroDoInstaBox(0, "resposta", Texto.Idioma.T("O InstaBox não mandou o endereço do SSH.")),
            json?["port"]?.GetValue<int>() ?? 22,
            Valor(json, "username") ?? id,
            Valor(json, "certificate") ?? throw new ErroDoInstaBox(0, "resposta", Texto.Idioma.T("O InstaBox não mandou o certificado do SSH.")),
            Valor(json?["hostTrust"], "publicKey") ?? throw new ErroDoInstaBox(0, "resposta", Texto.Idioma.T("O InstaBox não mandou a chave da máquina.")),
            DateTimeOffset.TryParse(Valor(json, "expiresAt"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expira) ? expira : DateTimeOffset.UtcNow.AddSeconds(validadeEmSegundos));
    }

    public async Task ManterAcordadaAsync(string id, string tarefa, int validadeEmSegundos, CancellationToken cancelamento = default) =>
        await EnviarAsync(HttpMethod.Put, "v1/machines/" + Uri.EscapeDataString(id) + "/activity-leases/" + Uri.EscapeDataString(tarefa),
            new JsonObject { ["ttlSeconds"] = validadeEmSegundos }, false, null, cancelamento).ConfigureAwait(false);

    public async Task SoltarAsync(string id, string tarefa, CancellationToken cancelamento = default)
    {
        try
        {
            await EnviarAsync(HttpMethod.Delete, "v1/machines/" + Uri.EscapeDataString(id) + "/activity-leases/" + Uri.EscapeDataString(tarefa), null, false, null, cancelamento).ConfigureAwait(false);
        }
        catch (ErroDoInstaBox ex) when (ex.Status == 404)
        {
        }
    }

    async Task AcaoAsync(string id, string acao, CancellationToken cancelamento)
    {
        var operacao = await EnviarAsync(HttpMethod.Post, "v1/machines/" + Uri.EscapeDataString(id) + "/" + acao, null, true, null, cancelamento).ConfigureAwait(false);
        await EsperarAsync(operacao, cancelamento).ConfigureAwait(false);
    }

    async Task EsperarAsync(JsonNode? operacao, CancellationToken cancelamento)
    {
        var limite = Stopwatch.StartNew();
        while (true)
        {
            switch (Valor(operacao, "state"))
            {
                case "succeeded":
                    return;
                case "failed":
                    var erro = operacao?["error"];
                    throw new ErroDoInstaBox(0, Valor(erro, "code") ?? "failed", Explicar(Valor(erro, "code"), Valor(erro, "message")));
            }
            if (limite.Elapsed > LimiteDaOperacao) throw new TimeoutException(Texto.Idioma.T("O InstaBox demorou demais para terminar."));
            var id = Valor(operacao, "id") ?? throw new ErroDoInstaBox(0, "resposta", Texto.Idioma.T("O InstaBox não mandou o número da operação."));
            operacao = await EnviarAsync(HttpMethod.Get, "v1/operations/" + Uri.EscapeDataString(id) + "?wait=25s", null, false, null, cancelamento).ConfigureAwait(false);
        }
    }

    async Task<JsonNode?> EnviarAsync(HttpMethod metodo, string caminho, JsonObject? corpo, bool idempotente, string? confirmarApagar, CancellationToken cancelamento)
    {
        var chave = _chave()?.Trim();
        if (string.IsNullOrEmpty(chave)) throw new ErroDoInstaBox(401, "missing_key", Texto.Idioma.T("Falta a chave de API do InstaBox em Configurações › Máquinas na nuvem."));
        using var pedido = new HttpRequestMessage(metodo, caminho);
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chave);
        if (idempotente) pedido.Headers.Add("Idempotency-Key", "nexora-" + Guid.NewGuid().ToString("N"));
        if (confirmarApagar is not null) pedido.Headers.Add("X-Confirm-Delete", confirmarApagar);
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
        var erro = json?["error"];
        var codigo = Valor(erro, "code") ?? ((int)resposta.StatusCode).ToString(CultureInfo.InvariantCulture);
        TimeSpan? depois = erro?["retryAfterSeconds"] is JsonValue segundos && segundos.TryGetValue<double>(out var s) ? TimeSpan.FromSeconds(s)
            : resposta.Headers.RetryAfter?.Delta;
        throw new ErroDoInstaBox((int)resposta.StatusCode, codigo, Explicar(codigo, Valor(erro, "message") ?? resposta.ReasonPhrase), depois);
    }

    public static string Explicar(string? codigo, string? mensagem) => codigo switch
    {
        "missing_key" or "malformed_key" or "invalid_key" => Texto.Idioma.T("O InstaBox não aceitou a chave de API. Confira se colou a chave inteira."),
        "expired_key" or "revoked_key" => Texto.Idioma.T("A chave de API do InstaBox venceu ou foi revogada. Crie outra no painel do InstaBox."),
        "missing_scope" => Texto.Idioma.T("A chave de API do InstaBox não tem permissão para isso. Crie uma com machines:read, machines:write, machines:delete e images:read."),
        "template_not_allowed" => Texto.Idioma.T("O seu plano do InstaBox não cria máquinas desse modelo. Escolha outro."),
        "quota_exceeded" => Texto.Idioma.T("O InstaBox chegou ao limite de máquinas do seu plano. Pause ou apague uma."),
        "no_capacity" => Texto.Idioma.T("O InstaBox está sem máquinas livres agora. Tente de novo em alguns minutos."),
        "credit_exhausted" or "spend_limit_reached" or "subscription_required" or "payment_failed" => Texto.Idioma.T("O InstaBox pediu pagamento: os créditos ou o limite de gasto acabaram."),
        "machine_cooldown" or "max_run_disabled" => Texto.Idioma.T("A máquina atingiu o tempo máximo seguido do plano grátis e está descansando. Tente mais tarde."),
        "not_found" => Texto.Idioma.T("Essa máquina não existe mais no InstaBox."),
        _ => string.IsNullOrWhiteSpace(mensagem) ? Texto.Idioma.F("O InstaBox respondeu {0}.", codigo ?? "?") : Texto.Idioma.F("InstaBox: {0}", mensagem.Trim()),
    };

    static EstadoInstaBox LerMaquina(JsonNode? json) => new(
        Valor(json, "id") ?? "",
        Valor(json, "name") ?? "",
        Valor(json, "state") ?? "",
        Valor(json?["template"], "id"),
        DateTimeOffset.TryParse(Valor(json, "cooldownUntil"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var ate) ? ate : null,
        Valor(json, "cooldownReason"));

    static string? Valor(JsonNode? no, string nome)
    {
        try
        {
            return no?[nome] is JsonValue valor && valor.TryGetValue<string>(out var texto) ? texto : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

}

public sealed class TransporteInstaBox(MaquinaNaNuvem dados, ProvedorInstaBox provedor) : ITransporte
{
    readonly SemaphoreSlim _trava = new(1, 1);
    CredencialInstaBox? _credencial;

    public static TimeSpan Folga { get; set; } = TimeSpan.FromMinutes(5);

    public CredencialInstaBox? Credencial => _credencial;

    public async Task AntesAsync(CancellationToken cancelamento)
    {
        if (Valida(_credencial)) return;
        await _trava.WaitAsync(cancelamento).ConfigureAwait(false);
        try
        {
            if (Valida(_credencial)) return;
            var id = dados.IdNoProvedor ?? throw new InvalidOperationException(Texto.Idioma.T("Esta máquina não tem o número dela no InstaBox."));
            var publica = await ChavesSsh.GarantirAsync(provedor.Chave, null, cancelamento).ConfigureAwait(false);
            CredencialInstaBox credencial;
            try
            {
                credencial = await provedor.Cliente.ConectarAsync(id, publica, 3600, cancelamento).ConfigureAwait(false);
            }
            catch (ErroDoInstaBox ex) when (ex.Codigo == "machine_not_running")
            {
                await provedor.RetomarAsync(dados, cancelamento).ConfigureAwait(false);
                credencial = await provedor.Cliente.ConectarAsync(id, publica, 3600, cancelamento).ConfigureAwait(false);
            }
            Directory.CreateDirectory(provedor.Pasta);
            await File.WriteAllTextAsync(provedor.Certificado(dados), credencial.Certificado.Trim() + "\n", cancelamento).ConfigureAwait(false);
            await File.WriteAllTextAsync(provedor.Conhecidos(dados), "@cert-authority " + Padrao(credencial) + " " + credencial.ChaveDaAutoridade.Trim() + "\n", cancelamento).ConfigureAwait(false);
            _credencial = credencial;
        }
        catch (ErroDoInstaBox ex) when (ex.Codigo == "machine_cooldown")
        {
            throw new ConexaoCaiuException(await ExplicarDescansoAsync(ex).ConfigureAwait(false));
        }
        catch (HttpRequestException ex)
        {
            throw new ConexaoCaiuException(Texto.Idioma.F("Não deu para falar com o InstaBox: {0}", ex.Message));
        }
        catch (TaskCanceledException ex) when (!cancelamento.IsCancellationRequested)
        {
            throw new ConexaoCaiuException(Texto.Idioma.F("O InstaBox não respondeu: {0}", ex.Message));
        }
        finally
        {
            _trava.Release();
        }
    }

    public void Caiu() => _credencial = null;

    async Task<string> ExplicarDescansoAsync(ErroDoInstaBox erro)
    {
        DateTimeOffset? ate = erro.TentarDepois is { } depois ? DateTimeOffset.Now + depois : null;
        string? motivo = null;
        try
        {
            var estado = await provedor.Cliente.MaquinaAsync(dados.IdNoProvedor!, CancellationToken.None).ConfigureAwait(false);
            ate = estado.DescansaAte ?? ate;
            motivo = estado.MotivoDoDescanso;
        }
        catch (Exception ex) when (ex is ErroDoInstaBox or HttpRequestException or TaskCanceledException)
        {
        }
        var hora = ate?.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture) ?? "?";
        return motivo is { Length: > 0 }
            ? Texto.Idioma.F("O InstaBox travou a máquina por suspeita de abuso ({0}) até {1}. Veja o motivo no painel do InstaBox.", motivo, hora)
            : Texto.Idioma.F("A máquina chegou ao tempo máximo seguido do plano do InstaBox (2 h no grátis) e está descansando até {0}. O agente fica parado lá e o Nexora reconecta sozinho quando ela voltar.", hora);
    }

    static bool Valida(CredencialInstaBox? credencial) => credencial is not null && credencial.Expira - DateTimeOffset.UtcNow > Folga;

    public static string Padrao(CredencialInstaBox credencial) =>
        credencial.Porta is 22 or 0 ? credencial.Host : "[" + credencial.Host + "]:" + credencial.Porta.ToString(CultureInfo.InvariantCulture);

    public ProcessStartInfo Comando(string comandoRemoto) => Ssh().Comando(comandoRemoto);

    public ProcessStartInfo? Tunel(int portaLocal, int portaRemota) => Ssh().Tunel(portaLocal, portaRemota);

    TransporteSsh Ssh()
    {
        var credencial = _credencial ?? throw new InvalidOperationException(Texto.Idioma.T("A conexão com o InstaBox ainda não foi aberta."));
        var destino = dados with { Host = credencial.Host, Usuario = credencial.Usuario, Porta = credencial.Porta };
        return new TransporteSsh(destino, provedor.Chave, provedor.Conhecidos(dados), null, provedor.Certificado(dados));
    }
}

public sealed class AvisoDeTrabalho : IAsyncDisposable
{
    public static TimeSpan Validade { get; set; } = TimeSpan.FromMinutes(10);
    public static TimeSpan Intervalo { get; set; } = TimeSpan.FromMinutes(4);

    readonly ClienteInstaBox _cliente;
    readonly string _maquina;
    readonly string _tarefa;
    readonly CancellationTokenSource _fim = new();
    readonly Task _laco;

    public AvisoDeTrabalho(ClienteInstaBox cliente, string maquina, string tarefa)
    {
        _cliente = cliente;
        _maquina = maquina;
        _tarefa = tarefa;
        _laco = Task.Run(RenovarAsync);
    }

    public int Renovacoes { get; private set; }

    async Task RenovarAsync()
    {
        while (!_fim.IsCancellationRequested)
        {
            try
            {
                await _cliente.ManterAcordadaAsync(_maquina, _tarefa, (int)Validade.TotalSeconds, _fim.Token).ConfigureAwait(false);
                Renovacoes++;
            }
            catch (Exception ex) when (ex is ErroDoInstaBox or HttpRequestException or TaskCanceledException)
            {
            }
            try
            {
                await Task.Delay(Intervalo, _fim.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_fim.IsCancellationRequested) return;
        await _fim.CancelAsync().ConfigureAwait(false);
        await _laco.ConfigureAwait(false);
        try
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await _cliente.SoltarAsync(_maquina, _tarefa, limite.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ErroDoInstaBox or HttpRequestException or TaskCanceledException)
        {
        }
        _fim.Dispose();
    }
}

public sealed class ProvedorInstaBox(string pasta, string chave, ClienteInstaBox cliente) : IProvedorDeMaquinas
{
    public string Tipo => TiposDeMaquina.InstaBox;
    public bool CriaMaquinas => true;
    public bool Pausa => true;
    public bool PublicaPortas => false;

    public string Pasta { get; } = Path.Combine(pasta, "instabox");
    public string Chave { get; } = chave;
    public ClienteInstaBox Cliente { get; } = cliente;

    public string Certificado(MaquinaNaNuvem dados) => Path.Combine(Pasta, dados.Id + "-cert.pub");
    public string Conhecidos(MaquinaNaNuvem dados) => Path.Combine(Pasta, dados.Id + ".known_hosts");

    public IMaquinaRemota Abrir(MaquinaNaNuvem dados) => new MaquinaRemota(dados, new TransporteInstaBox(dados, this));

    public static string NomeRemoto(string nome)
    {
        var texto = new StringBuilder();
        foreach (var c in nome.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var minuscula = char.ToLowerInvariant(c);
            if (minuscula is >= 'a' and <= 'z' or >= '0' and <= '9') texto.Append(minuscula);
            else if (texto.Length > 0 && texto[^1] != '-') texto.Append('-');
        }
        var limpo = texto.ToString().Trim('-');
        if (limpo.Length > 50) limpo = limpo[..50].TrimEnd('-');
        return (limpo.Length > 0 ? limpo : "nexora") + "-" + Guid.NewGuid().ToString("N")[..6];
    }

    public Task<MaquinaNaNuvem> CriarAsync(string nome, CancellationToken cancelamento = default) => CriarAsync(nome, null, cancelamento);

    public async Task<MaquinaNaNuvem> CriarAsync(string nome, string? modelo, CancellationToken cancelamento = default)
    {
        var id = await Cliente.CriarAsync(NomeRemoto(nome), modelo, cancelamento).ConfigureAwait(false);
        return new MaquinaNaNuvem(MaquinaNaNuvem.NovoId(), nome, TiposDeMaquina.InstaBox, IdNoProvedor: id, Modelo: modelo);
    }

    public async Task RetomarAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default)
    {
        var id = maquina.IdNoProvedor ?? throw new InvalidOperationException();
        var estado = await Cliente.MaquinaAsync(id, cancelamento).ConfigureAwait(false);
        switch (estado.Estado)
        {
            case "running" or "starting" or "resuming" or "creating":
                return;
            case "suspended" or "suspending":
                await Cliente.RetomarAsync(id, cancelamento).ConfigureAwait(false);
                return;
            default:
                await Cliente.LigarAsync(id, cancelamento).ConfigureAwait(false);
                return;
        }
    }

    public Task PausarAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default) =>
        Cliente.PausarAsync(maquina.IdNoProvedor ?? throw new InvalidOperationException(), cancelamento);

    public async Task ApagarAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default)
    {
        if (maquina.IdNoProvedor is { } id) await Cliente.ApagarAsync(id, cancelamento).ConfigureAwait(false);
        foreach (var arquivo in new[] { Certificado(maquina), Conhecidos(maquina) })
            try
            {
                File.Delete(arquivo);
            }
            catch (IOException)
            {
            }
    }

    public Task<EstadoInstaBox> EstadoAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default) =>
        Cliente.MaquinaAsync(maquina.IdNoProvedor ?? throw new InvalidOperationException(), cancelamento);

    public Task<string> PublicarPortaAsync(MaquinaNaNuvem maquina, int porta, CancellationToken cancelamento = default) => throw new NotSupportedException();
    public Task<CotaDaMaquina?> CotaAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default) => Task.FromResult<CotaDaMaquina?>(null);
}
