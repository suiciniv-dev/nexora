using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Nexora.Core.Nuvem;
using Nexora.Core.Segredos;

namespace Nexora.Testes.Nuvem;

public sealed class InstaBoxTestes : IDisposable
{
    readonly string _pasta = Path.Combine(Path.GetTempPath(), "nexora-instabox-" + Guid.NewGuid().ToString("N")[..8]);
    readonly InstaBoxFalso _api = new();
    readonly ArmazemNaMemoria _armazem = new();

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

    ServicoDaNuvem Servico() => new(_pasta, _armazem, null, _api);

    [Fact]
    public async Task Sem_chave_avisa_onde_colar_sem_chamar_a_api()
    {
        var nuvem = Servico();
        Assert.False(nuvem.TemChaveInstaBox);
        var erro = await Assert.ThrowsAsync<ErroDoInstaBox>(() => nuvem.InstaBox.Cliente.ModelosAsync());
        Assert.Contains("Máquinas na nuvem", erro.Message);
        Assert.Empty(_api.Pedidos);
    }

    [Fact]
    public async Task Chave_fica_no_cofre_e_vai_no_cabecalho_bearer()
    {
        var nuvem = Servico();
        nuvem.GuardarChaveInstaBox("  ibk_teste123  ");
        Assert.True(nuvem.TemChaveInstaBox);
        Assert.Equal("ibk_teste123", _armazem.Ler("nuvem-instabox-chave"));
        Assert.DoesNotContain("ibk_teste123", File.Exists(Path.Combine(_pasta, "nuvem.json")) ? File.ReadAllText(Path.Combine(_pasta, "nuvem.json")) : "");
        var modelos = await nuvem.InstaBox.Cliente.ModelosAsync();
        Assert.Equal(["agent-browser", "ci"], modelos.Where(m => m.Criavel).Select(m => m.Id));
        Assert.True(modelos[0].Padrao);
        Assert.Equal("Bearer ibk_teste123", _api.Pedidos[0].Autorizacao);
        nuvem.ApagarChaveInstaBox();
        Assert.False(nuvem.TemChaveInstaBox);
    }

    [Fact]
    public async Task Criar_espera_a_operacao_e_guarda_o_id_da_maquina()
    {
        var nuvem = Servico();
        nuvem.GuardarChaveInstaBox("ibk_x");
        var maquina = nuvem.Salvar(await nuvem.InstaBox.CriarAsync("Minha Máquina!", "ci"));
        Assert.Equal(TiposDeMaquina.InstaBox, maquina.Tipo);
        Assert.Equal("m_novo", maquina.IdNoProvedor);
        Assert.Equal("InstaBox · ci · m_novo", maquina.Endereco);
        var criar = _api.Pedidos.Single(p => p.Metodo == "POST" && p.Caminho == "/v1/machines");
        Assert.StartsWith("nexora-", criar.Idempotencia);
        var corpo = JsonNode.Parse(criar.Corpo)!;
        Assert.Matches("^minha-maquina-[0-9a-f]{6}$", corpo["name"]!.GetValue<string>());
        Assert.Equal("ci", corpo["templateId"]!.GetValue<string>());
        Assert.Contains(_api.Pedidos, p => p.Caminho == "/v1/operations/op_criar" && p.Consulta.Contains("wait=25s"));
        Assert.Equal("m_novo", new ServicoDaNuvem(_pasta, _armazem, null, _api).Maquinas.Single().IdNoProvedor);
    }

    [Fact]
    public async Task Operacao_que_falha_vira_mensagem_em_portugues()
    {
        var nuvem = Servico();
        nuvem.GuardarChaveInstaBox("ibk_x");
        _api.FalharCriacao = "template_not_allowed";
        var erro = await Assert.ThrowsAsync<ErroDoInstaBox>(() => nuvem.InstaBox.CriarAsync("x", "universal"));
        Assert.Equal("template_not_allowed", erro.Codigo);
        Assert.Contains("plano", erro.Message);
    }

    [Fact]
    public async Task Erro_http_da_api_vira_mensagem_em_portugues()
    {
        var nuvem = Servico();
        nuvem.GuardarChaveInstaBox("ibk_revogada");
        _api.Recusar = (HttpStatusCode.Unauthorized, "revoked_key");
        var erro = await Assert.ThrowsAsync<ErroDoInstaBox>(() => nuvem.InstaBox.Cliente.ModelosAsync());
        Assert.Equal(401, erro.Status);
        Assert.Contains("revogada", erro.Message);
    }

    [Fact]
    public async Task Conectar_grava_certificado_e_autoridade_e_monta_o_ssh_com_eles()
    {
        var nuvem = Servico();
        nuvem.GuardarChaveInstaBox("ibk_x");
        var maquina = nuvem.Salvar(new MaquinaNaNuvem("abc", "Caixa", TiposDeMaquina.InstaBox, IdNoProvedor: "m_caixa"));
        var remota = (MaquinaRemota)nuvem.Abrir(maquina);
        var info = await remota.PrepararAsync("echo oi");
        var args = info.ArgumentList.ToList();
        Assert.Contains("m_caixa@ssh.instabox.test", args);
        Assert.Contains("2222", args);
        Assert.Contains("StrictHostKeyChecking=yes", args);
        Assert.Contains("CertificateFile=" + nuvem.InstaBox.Certificado(maquina), args);
        Assert.Contains("UserKnownHostsFile=" + nuvem.InstaBox.Conhecidos(maquina), args);
        Assert.Equal("ssh-ed25519-cert-v01@openssh.com AAAACERT", File.ReadAllText(nuvem.InstaBox.Certificado(maquina)).Trim());
        Assert.Equal("@cert-authority [ssh.instabox.test]:2222 ssh-ed25519 AAAACA", File.ReadAllText(nuvem.InstaBox.Conhecidos(maquina)).Trim());
        var conexao = _api.Pedidos.Single(p => p.Caminho == "/v1/machines/m_caixa/connections");
        Assert.StartsWith("ssh-ed25519 ", JsonNode.Parse(conexao.Corpo)!["publicKey"]!.GetValue<string>());
        await remota.PrepararAsync("echo de novo");
        Assert.Single(_api.Pedidos, p => p.Caminho.EndsWith("/connections"));
    }

    [Fact]
    public async Task Maquina_pausada_e_retomada_antes_de_conectar()
    {
        var nuvem = Servico();
        nuvem.GuardarChaveInstaBox("ibk_x");
        _api.Estado = "suspended";
        var maquina = nuvem.Salvar(new MaquinaNaNuvem("abc", "Caixa", TiposDeMaquina.InstaBox, IdNoProvedor: "m_caixa"));
        await nuvem.Abrir(maquina).PrepararAsync("true");
        Assert.Contains(_api.Pedidos, p => p.Metodo == "POST" && p.Caminho == "/v1/machines/m_caixa/resume");
        Assert.Equal(2, _api.Pedidos.Count(p => p.Caminho.EndsWith("/connections")));
        Assert.Equal("running", _api.Estado);
    }

    [Fact]
    public async Task Pausar_e_apagar_mandam_a_confirmacao_e_tiram_da_lista()
    {
        var nuvem = Servico();
        nuvem.GuardarChaveInstaBox("ibk_x");
        var maquina = nuvem.Salvar(new MaquinaNaNuvem("abc", "Caixa", TiposDeMaquina.InstaBox, IdNoProvedor: "m_caixa"));
        await nuvem.PausarAsync(maquina);
        Assert.Equal("suspended", _api.Estado);
        await nuvem.RetomarAsync(maquina);
        Assert.Equal("running", _api.Estado);
        await nuvem.ApagarComAMaquinaAsync(maquina);
        var apagar = _api.Pedidos.Single(p => p.Metodo == "DELETE");
        Assert.Equal("m_caixa", apagar.ConfirmarApagar);
        Assert.Empty(nuvem.Maquinas);
    }

    [Fact]
    public async Task Aviso_de_trabalho_renova_enquanto_dura_e_solta_no_fim()
    {
        var nuvem = Servico();
        nuvem.GuardarChaveInstaBox("ibk_x");
        var maquina = nuvem.Salvar(new MaquinaNaNuvem("abc", "Caixa", TiposDeMaquina.InstaBox, IdNoProvedor: "m_caixa"));
        Assert.Null(nuvem.ManterAcordada(new MaquinaNaNuvem("s", "VPS", TiposDeMaquina.Ssh, "h"), "t"));
        var intervalo = AvisoDeTrabalho.Intervalo;
        AvisoDeTrabalho.Intervalo = TimeSpan.FromMilliseconds(40);
        try
        {
            var aviso = nuvem.ManterAcordada(maquina, "conversa1")!;
            var limite = DateTime.Now.AddSeconds(10);
            while (aviso.Renovacoes < 3 && DateTime.Now < limite) await Task.Delay(20);
            Assert.True(aviso.Renovacoes >= 3);
            await aviso.DisposeAsync();
        }
        finally
        {
            AvisoDeTrabalho.Intervalo = intervalo;
        }
        var renovar = _api.Pedidos.First(p => p.Metodo == "PUT");
        Assert.Equal("/v1/machines/m_caixa/activity-leases/nexora-conversa1", renovar.Caminho);
        Assert.Equal(600, JsonNode.Parse(renovar.Corpo)!["ttlSeconds"]!.GetValue<int>());
        Assert.Equal("DELETE", _api.Pedidos[^1].Metodo);
        Assert.Equal(renovar.Caminho, _api.Pedidos[^1].Caminho);
    }

    [Fact]
    public async Task Maquina_descansando_avisa_ate_que_horas_e_deixa_reconectar_depois()
    {
        var nuvem = Servico();
        nuvem.GuardarChaveInstaBox("ibk_x");
        _api.Estado = "suspended";
        _api.DescansaAte = DateTimeOffset.UtcNow.AddMinutes(90);
        var maquina = nuvem.Salvar(new MaquinaNaNuvem("abc", "Caixa", TiposDeMaquina.InstaBox, IdNoProvedor: "m_caixa"));
        var erro = await Assert.ThrowsAsync<ConexaoCaiuException>(() => nuvem.Abrir(maquina).PrepararAsync("true"));
        Assert.Contains("descansando até " + _api.DescansaAte.Value.ToLocalTime().ToString("HH:mm"), erro.Message);
        _api.DescansaAte = null;
        await nuvem.Abrir(maquina).PrepararAsync("true");
        Assert.Equal("running", _api.Estado);
    }

    [Fact]
    public async Task Queda_da_conexao_pede_certificado_novo()
    {
        var nuvem = Servico();
        nuvem.GuardarChaveInstaBox("ibk_x");
        var maquina = nuvem.Salvar(new MaquinaNaNuvem("abc", "Caixa", TiposDeMaquina.InstaBox, IdNoProvedor: "m_caixa"));
        var remota = nuvem.Abrir(maquina);
        await remota.PrepararAsync("true");
        remota.AvisarQueda();
        await remota.PrepararAsync("true");
        Assert.Equal(2, _api.Pedidos.Count(p => p.Caminho.EndsWith("/connections")));
    }

    [Fact]
    public void Estado_da_maquina_vira_texto_curto()
    {
        Assert.Equal("ligada", Nexora.ViewModels.MaquinaVM.TextoDoEstado("running"));
        Assert.Equal("pausada", Nexora.ViewModels.MaquinaVM.TextoDoEstado("suspended"));
        Assert.Equal("ligando…", Nexora.ViewModels.MaquinaVM.TextoDoEstado("resuming"));
        Assert.Null(Nexora.ViewModels.MaquinaVM.TextoDoEstado(null));
        Assert.Equal("estranho", Nexora.ViewModels.MaquinaVM.TextoDoEstado("estranho"));
    }

    [Fact]
    public void Nome_remoto_so_tem_letras_minusculas_numeros_e_hifen()
    {
        Assert.Matches("^acao-pao-123-[0-9a-f]{6}$", ProvedorInstaBox.NomeRemoto("  Ação PÃO__123 "));
        Assert.Matches("^nexora-[0-9a-f]{6}$", ProvedorInstaBox.NomeRemoto("!!!"));
        Assert.True(ProvedorInstaBox.NomeRemoto(new string('a', 100)).Length <= 63);
    }

    sealed record Pedido(string Metodo, string Caminho, string Consulta, string? Autorizacao, string? Idempotencia, string? ConfirmarApagar, string Corpo);

    sealed class InstaBoxFalso : HttpMessageHandler
    {
        public List<Pedido> Pedidos { get; } = [];
        public string Estado { get; set; } = "running";
        public string? FalharCriacao { get; set; }
        public (HttpStatusCode Status, string Codigo)? Recusar { get; set; }
        public DateTimeOffset? DescansaAte { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken cancelamento)
        {
            var corpo = pedido.Content is null ? "" : await pedido.Content.ReadAsStringAsync(cancelamento);
            string? Cabecalho(string nome) => pedido.Headers.TryGetValues(nome, out var v) ? v.First() : null;
            var caminho = pedido.RequestUri!.AbsolutePath;
            lock (Pedidos)
                Pedidos.Add(new Pedido(pedido.Method.Method, caminho, pedido.RequestUri.Query, pedido.Headers.Authorization?.ToString(), Cabecalho("Idempotency-Key"), Cabecalho("X-Confirm-Delete"), corpo));
            if (Recusar is { } recusa) return Json(recusa.Status, $$$"""{"error":{"code":"{{{recusa.Codigo}}}","message":"no"}}""");
            switch (pedido.Method.Method, caminho)
            {
                case ("GET", "/v1/templates"):
                    return Json(HttpStatusCode.OK, """{"templates":[{"id":"agent-browser","creatable":true,"default":true,"defaultSpec":{"vcpus":4,"memMiB":8192,"persistentDiskGiB":20}},{"id":"universal","creatable":false},{"id":"ci","creatable":true}]}""");
                case ("POST", "/v1/machines"):
                    return Json(HttpStatusCode.Accepted, """{"id":"op_criar","machineId":"m_novo","type":"create","state":"queued","error":null}""");
                case ("GET", "/v1/operations/op_criar"):
                    return FalharCriacao is { } codigo
                        ? Json(HttpStatusCode.OK, $$$"""{"id":"op_criar","machineId":"m_novo","state":"failed","error":{"code":"{{{codigo}}}","message":"no"}}""")
                        : Json(HttpStatusCode.OK, """{"id":"op_criar","machineId":"m_novo","state":"succeeded","error":null}""");
                case ("GET", "/v1/machines/m_caixa"):
                    var descanso = DescansaAte is { } ate ? ",\"cooldownUntil\":\"" + ate.ToString("O") + "\"" : "";
                    return Json(HttpStatusCode.OK, """{"id":"m_caixa","name":"caixa","template":{"id":"ci"},"state":""" + "\"" + Estado + "\"" + descanso + "}");
                case ("PUT", "/v1/machines/m_caixa/activity-leases/nexora-conversa1"):
                    return Json(HttpStatusCode.OK, """{"id":"nexora-conversa1","machineId":"m_caixa","expiresAt":"2099-01-01T00:00:00Z"}""");
                case ("DELETE", "/v1/machines/m_caixa/activity-leases/nexora-conversa1"):
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                case ("POST", "/v1/machines/m_caixa/connections"):
                    if (Estado != "running") return Json(HttpStatusCode.Conflict, """{"error":{"code":"machine_not_running","message":"not running"}}""");
                    return Json(HttpStatusCode.Created, """{"id":"cn_1","machineId":"m_caixa","transport":"ssh","host":"ssh.instabox.test","port":2222,"username":"m_caixa","certificate":"ssh-ed25519-cert-v01@openssh.com AAAACERT","expiresAt":"2099-01-01T00:00:00Z","hostTrust":{"type":"ca","publicKey":"ssh-ed25519 AAAACA","principals":["ssh.instabox.test"]}}""");
                case ("POST", "/v1/machines/m_caixa/resume" or "/v1/machines/m_caixa/start"):
                    if (DescansaAte is not null) return Json(HttpStatusCode.Conflict, """{"error":{"code":"machine_cooldown","message":"cooling down","retryAfterSeconds":5400}}""");
                    Estado = "running";
                    return Json(HttpStatusCode.Accepted, """{"id":"op_feito","machineId":"m_caixa","state":"succeeded","error":null}""");
                case ("POST", "/v1/machines/m_caixa/suspend"):
                    Estado = "suspended";
                    return Json(HttpStatusCode.Accepted, """{"id":"op_pausa","machineId":"m_caixa","state":"queued","error":null}""");
                case ("GET", "/v1/operations/op_pausa"):
                    return Json(HttpStatusCode.OK, """{"id":"op_pausa","machineId":"m_caixa","state":"succeeded","error":null}""");
                case ("DELETE", "/v1/machines/m_caixa"):
                    return Json(HttpStatusCode.Accepted, """{"id":"op_feito","machineId":"m_caixa","state":"succeeded","error":null}""");
                default:
                    return Json(HttpStatusCode.NotFound, """{"error":{"code":"not_found","message":"?"}}""");
            }
        }

        static HttpResponseMessage Json(HttpStatusCode status, string json) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
