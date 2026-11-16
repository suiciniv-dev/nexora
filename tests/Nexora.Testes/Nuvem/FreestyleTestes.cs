using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Nexora.Core.Nuvem;
using Nexora.Core.Segredos;

namespace Nexora.Testes.Nuvem;

public sealed class FreestyleTestes : IDisposable
{
    readonly string _pasta = Path.Combine(Path.GetTempPath(), "nexora-freestyle-" + Guid.NewGuid().ToString("N")[..8]);
    readonly FreestyleFalsa _api = new();
    readonly ArmazemNaMemoria _armazem = new();

    public FreestyleTestes() => ClienteFreestyle.IntervaloDaEspera = TimeSpan.FromMilliseconds(5);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_pasta, true);
        }
        catch (IOException)
        {
        }
    }

    ServicoDaNuvem Servico() => new(_pasta, _armazem, null, null, null, _api);

    [Fact]
    public async Task Sem_chave_avisa_onde_colar_sem_chamar_a_api()
    {
        var nuvem = Servico();
        Assert.False(nuvem.TemChaveFreestyle);
        var erro = await Assert.ThrowsAsync<ErroDaFreestyle>(() => nuvem.Freestyle.Cliente.ContarAsync());
        Assert.Contains("Máquinas na nuvem", erro.Message);
        Assert.Empty(_api.Pedidos);
    }

    [Fact]
    public async Task Criar_libera_a_saida_para_a_internet_e_espera_ligar()
    {
        var nuvem = Servico();
        nuvem.GuardarChaveFreestyle(" fs_chave ");
        Assert.Equal("fs_chave", _armazem.Ler("nuvem-freestyle-chave"));
        var maquina = nuvem.Salvar(await nuvem.Freestyle.CriarAsync("Minha VM"));
        Assert.Equal(TiposDeMaquina.Freestyle, maquina.Tipo);
        Assert.Equal("vm-1", maquina.IdNoProvedor);
        var criar = _api.Pedidos.First(p => p.Metodo == "POST" && p.Caminho == "/v5/vms");
        Assert.Equal("Bearer fs_chave", criar.Autorizacao);
        var corpo = JsonNode.Parse(criar.Corpo)!;
        Assert.Equal("Minha VM", corpo["displayName"]!.ToString());
        Assert.True(corpo["firewall"]!["rules"]![0]!["destination"]!["public"]!.GetValue<bool>());
        Assert.Contains(_api.Pedidos, p => p.Metodo == "GET" && p.Caminho == "/v5/vms/vm-1");
        Assert.Equal("running", _api.Estado);
    }

    [Fact]
    public async Task Pausar_e_retomar_esperam_o_estado_e_o_estado_vira_o_comum()
    {
        var nuvem = Servico();
        nuvem.GuardarChaveFreestyle("fs");
        var maquina = nuvem.Salvar(await nuvem.Freestyle.CriarAsync("vm"));
        await nuvem.PausarAsync(maquina);
        Assert.Equal("suspended", await nuvem.Freestyle.EstadoAsync(maquina));
        await nuvem.RetomarAsync(maquina);
        Assert.Equal("running", await nuvem.Freestyle.EstadoAsync(maquina));
        Assert.Contains(_api.Pedidos, p => p.Caminho == "/v5/vms/vm-1/start");
    }

    [Fact]
    public async Task Conexao_cria_identidade_uma_vez_e_o_ssh_entra_com_o_token_como_senha()
    {
        var nuvem = Servico();
        nuvem.GuardarChaveFreestyle("fs");
        var maquina = nuvem.Salvar(await nuvem.Freestyle.CriarAsync("vm"));
        TransporteSsh.ProgramaDeSenha = @"C:\NexoraApp\Nexora.exe";
        var transporte = new TransporteFreestyle(maquina, nuvem.Freestyle);
        await transporte.AntesAsync(CancellationToken.None);
        var info = transporte.Comando("echo oi");
        Assert.Equal("vm-1@" + ClienteFreestyle.HostSsh, info.ArgumentList[^2]);
        Assert.Contains("PubkeyAuthentication=no", info.ArgumentList);
        Assert.DoesNotContain("-i", info.ArgumentList);
        Assert.DoesNotContain(info.ArgumentList, a => a.Contains("tok-segredo-1", StringComparison.Ordinal));
        Assert.Equal("tok-segredo-1", info.Environment[TransporteSsh.VariavelDaSenha]);
        Assert.Equal("force", info.Environment["SSH_ASKPASS_REQUIRE"]);
        Assert.Equal(@"C:\NexoraApp\Nexora.exe", info.Environment["SSH_ASKPASS"]);
        transporte.Caiu();
        await new TransporteFreestyle(maquina, nuvem.Freestyle).AntesAsync(CancellationToken.None);
        Assert.Single(_api.Pedidos, p => p.Caminho == "/v5/identities");
        Assert.Single(_api.Pedidos, p => p.Caminho == "/v5/identities/idn-1/permissions/vm" && p.Corpo.Contains("vm-1"));
        Assert.Equal(2, _api.Pedidos.Count(p => p.Caminho == "/v5/identities/idn-1/tokens"));
        Assert.DoesNotContain(Directory.EnumerateFiles(_pasta, "*", SearchOption.AllDirectories), f => File.ReadAllText(f).Contains("tok-segredo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Apagar_tira_a_vm_e_a_identidade()
    {
        var nuvem = Servico();
        nuvem.GuardarChaveFreestyle("fs");
        var maquina = nuvem.Salvar(await nuvem.Freestyle.CriarAsync("vm"));
        await new TransporteFreestyle(maquina, nuvem.Freestyle).AntesAsync(CancellationToken.None);
        await nuvem.ApagarComAMaquinaAsync(maquina);
        Assert.Contains(_api.Pedidos, p => p.Metodo == "DELETE" && p.Caminho == "/v5/vms/vm-1");
        Assert.Contains(_api.Pedidos, p => p.Metodo == "DELETE" && p.Caminho == "/v5/identities/idn-1");
        Assert.Empty(nuvem.Maquinas);
    }

    [Fact]
    public async Task Erro_da_api_vira_explicacao()
    {
        var nuvem = Servico();
        nuvem.GuardarChaveFreestyle("fs");
        _api.Recusar = true;
        var erro = await Assert.ThrowsAsync<ErroDaFreestyle>(() => nuvem.Freestyle.Cliente.ContarAsync());
        Assert.Equal(401, erro.Status);
        Assert.Contains("recusou a chave", erro.Message);
    }

    sealed record Pedido(string Metodo, string Caminho, string? Autorizacao, string Corpo);

    sealed class FreestyleFalsa : HttpMessageHandler
    {
        public List<Pedido> Pedidos { get; } = [];
        public string Estado { get; private set; } = "starting";
        public bool Recusar { get; set; }
        int _tokens;
        int _passos;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var corpo = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            var caminho = request.RequestUri!.AbsolutePath;
            Pedidos.Add(new(request.Method.Method, caminho, request.Headers.Authorization?.ToString(), corpo));
            if (Recusar) return Json(HttpStatusCode.Unauthorized, new JsonObject { ["code"] = "UNAUTHORIZED_ERROR", ["message"] = "no" });
            switch (request.Method.Method, caminho)
            {
                case ("GET", "/v5/vms"):
                    return Json(HttpStatusCode.OK, new JsonObject { ["vms"] = new JsonArray(), ["totalCount"] = 0 });
                case ("POST", "/v5/vms"):
                    Estado = "starting";
                    return Json(HttpStatusCode.Created, new JsonObject { ["id"] = "vm-1", ["state"] = Estado });
                case ("GET", "/v5/vms/vm-1"):
                    if (Estado is "starting" or "pausing" && ++_passos % 2 == 0) Estado = Estado == "starting" ? "running" : "paused";
                    return Json(HttpStatusCode.OK, new JsonObject { ["id"] = "vm-1", ["state"] = Estado });
                case ("POST", "/v5/vms/vm-1/start"):
                    Estado = "starting";
                    return Json(HttpStatusCode.OK, new JsonObject { ["id"] = "vm-1", ["state"] = Estado });
                case ("POST", "/v5/vms/vm-1/pause"):
                    if (Estado != "running") return Json(HttpStatusCode.Conflict, new JsonObject { ["code"] = "CONFLICT" });
                    Estado = "pausing";
                    return Json(HttpStatusCode.OK, new JsonObject { ["id"] = "vm-1", ["state"] = Estado });
                case ("DELETE", "/v5/vms/vm-1"):
                case ("DELETE", "/v5/identities/idn-1"):
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                case ("POST", "/v5/identities"):
                    return Json(HttpStatusCode.OK, new JsonObject { ["id"] = "idn-1", ["managed"] = true });
                case ("POST", "/v5/identities/idn-1/permissions/vm"):
                    return Json(HttpStatusCode.OK, new JsonObject());
                case ("POST", "/v5/identities/idn-1/tokens"):
                    var expira = JsonNode.Parse(corpo)!["expiresAt"]!.ToString();
                    return Json(HttpStatusCode.OK, new JsonObject { ["id"] = "tok-" + ++_tokens, ["token"] = "tok-segredo-" + _tokens, ["expiresAt"] = expira });
                default:
                    return Json(HttpStatusCode.NotFound, new JsonObject { ["code"] = "NOT_FOUND" });
            }
        }

        static HttpResponseMessage Json(HttpStatusCode status, JsonObject corpo) =>
            new(status) { Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json") };
    }
}
