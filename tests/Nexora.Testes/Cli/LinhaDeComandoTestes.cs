using System.Text.Json.Nodes;
using Nexora.Core.Cli;

namespace Nexora.Testes.Cli;

public sealed class LinhaDeComandoTestes
{
    readonly List<JsonObject> _pedidos = [];
    readonly StringWriter _saida = new();
    readonly StringWriter _erros = new();

    LinhaDeComando Linha(Func<JsonObject, JsonObject?> responder, string entrada = "", string? pasta = null) =>
        new((pedido, _) =>
        {
            _pedidos.Add(pedido);
            return Task.FromResult(responder(pedido));
        }, _saida, _erros, new StringReader(entrada), pasta ?? Path.GetTempPath());

    static JsonObject Ok(JsonObject dados)
    {
        dados["ok"] = true;
        return dados;
    }

    [Fact]
    public async Task Sem_o_nexora_aberto_avisa_e_sai_com_codigo_3()
    {
        var codigo = await Linha(_ => null).RodarAsync(["status"]);
        Assert.Equal(CodigosDaCli.NexoraFechado, codigo);
        Assert.Contains("não está aberto", _erros.ToString());
    }

    [Fact]
    public async Task Comando_desconhecido_e_falta_de_argumento_saem_com_codigo_de_uso()
    {
        Assert.Equal(CodigosDaCli.Uso, await Linha(_ => Ok([])).RodarAsync(["xyz"]));
        Assert.Equal(CodigosDaCli.Uso, await Linha(_ => Ok([])).RodarAsync(["enviar", "s1"]));
        Assert.Equal(CodigosDaCli.Uso, await Linha(_ => Ok([])).RodarAsync([]));
        Assert.Empty(_pedidos);
        Assert.Equal(CodigosDaCli.Ok, await Linha(_ => Ok([])).RodarAsync(["ajuda"]));
        Assert.Contains("nexora iniciar", _saida.ToString());
    }

    [Fact]
    public async Task Iniciar_com_ponto_usa_a_pasta_atual_e_le_a_mensagem_do_pipe()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "projeto-cli");
        var codigo = await Linha(p => Ok(new JsonObject { ["id"] = "s7", ["pasta"] = p["projeto"]!.ToString() }), "  arrume o teste \n", pasta)
            .RodarAsync(["iniciar", ".", "-", "--agente", "codex", "--modelo", "opus"]);
        Assert.Equal(0, codigo);
        var pedido = Assert.Single(_pedidos);
        Assert.Equal("iniciar", pedido["comando"]!.ToString());
        Assert.Equal(Path.GetFullPath(pasta), pedido["projeto"]!.ToString());
        Assert.Equal("arrume o teste", pedido["mensagem"]!.ToString());
        Assert.Equal("codex", pedido["agente"]!.ToString());
        Assert.Equal("opus", pedido["modelo"]!.ToString());
        Assert.Contains("s7", _saida.ToString());
    }

    [Fact]
    public async Task Nome_sem_barra_vai_como_nome_do_projeto()
    {
        await Linha(_ => Ok(new JsonObject { ["sessoes"] = new JsonArray() })).RodarAsync(["sessoes", "--projeto", "Junto-que-nao-existe-aqui"]);
        Assert.Equal("Junto-que-nao-existe-aqui", _pedidos[0]["projeto"]!.ToString());
    }

    [Fact]
    public async Task Espere_repete_ate_parar_de_trabalhar_e_imprime_a_resposta()
    {
        var vezes = 0;
        var codigo = await Linha(p => p["comando"]!.ToString() switch
        {
            "enviar" => Ok(new JsonObject { ["id"] = "s1", ["estado"] = "trabalhando" }),
            _ => ++vezes < 3
                ? Ok(new JsonObject { ["id"] = "s1", ["estado"] = "trabalhando" })
                : Ok(new JsonObject { ["id"] = "s1", ["estado"] = "pronta", ["resposta"] = "feito" }),
        }).RodarAsync(["enviar", "s1", "continue", "--espere"]);
        Assert.Equal(0, codigo);
        Assert.Equal(3, vezes);
        Assert.True(_pedidos.Skip(1).All(p => p["resposta"]!.GetValue<bool>()));
        Assert.Equal("feito", _saida.ToString().Trim());
    }

    [Fact]
    public async Task Espere_sai_com_codigo_2_quando_precisa_de_voce()
    {
        var codigo = await Linha(p => p["comando"]!.ToString() == "iniciar"
            ? Ok(new JsonObject { ["id"] = "s2", ["pasta"] = "x" })
            : Ok(new JsonObject { ["id"] = "s2", ["estado"] = "precisa de você", ["pendencia"] = "Permitir Bash" }))
            .RodarAsync(["iniciar", "Loja", "rode os testes", "--espere"]);
        Assert.Equal(CodigosDaCli.PrecisaDeVoce, codigo);
        Assert.Contains("Permitir Bash", _erros.ToString());
    }

    [Fact]
    public async Task Ao_vivo_sai_com_2_se_algo_precisa_e_0_se_nada()
    {
        var com = await Linha(_ => Ok(new JsonObject { ["sessoes"] = new JsonArray(new JsonObject { ["id"] = "s1", ["estado"] = "precisa de você", ["titulo"] = "t", ["projeto"] = "p" }) })).RodarAsync(["ao-vivo"]);
        Assert.Equal(CodigosDaCli.PrecisaDeVoce, com);
        Assert.True(_pedidos[0]["precisam"]!.GetValue<bool>());
        var sem = await Linha(_ => Ok(new JsonObject { ["sessoes"] = new JsonArray() })).RodarAsync(["ao-vivo"]);
        Assert.Equal(CodigosDaCli.Ok, sem);
    }

    [Fact]
    public async Task Erro_do_nexora_vira_mensagem_e_codigo()
    {
        var codigo = await Linha(_ => new JsonObject { ["ok"] = false, ["erro"] = "Sessão não encontrada: s9." }).RodarAsync(["ver", "s9"]);
        Assert.Equal(CodigosDaCli.Erro, codigo);
        Assert.Contains("s9", _erros.ToString());
    }

    [Fact]
    public async Task Canal_leva_o_pedido_e_traz_a_resposta_so_para_o_mesmo_usuario()
    {
        var nome = "nexora-teste-" + Guid.NewGuid().ToString("N")[..8];
        using var servidor = new ServidorDoCanal(nome, (pedido, _) => Task.FromResult(ServidorDoCanal.Ok(new JsonObject { ["eco"] = pedido["comando"]!.ToString() })));
        var resposta = await CanalLocal.PedirAsync(nome, new JsonObject { ["comando"] = "status" }, TimeSpan.FromSeconds(5));
        Assert.True(resposta!["ok"]!.GetValue<bool>());
        Assert.Equal("status", resposta["eco"]!.ToString());
        Assert.Null(await CanalLocal.PedirAsync(nome + "-outro", new JsonObject { ["comando"] = "status" }, TimeSpan.FromMilliseconds(300)));
    }

    [Fact]
    public void Nome_do_canal_depende_da_pasta_de_dados()
    {
        Assert.Equal(CanalLocal.Nome(@"C:\Dados\Nexora"), CanalLocal.Nome(@"C:\Dados\Nexora\"));
        Assert.NotEqual(CanalLocal.Nome(@"C:\Dados\Nexora"), CanalLocal.Nome(@"C:\Outra\Nexora"));
        Assert.StartsWith("nexora-", CanalLocal.Nome(@"C:\Dados\Nexora"));
    }

    [Fact]
    public void Comando_no_terminal_aponta_para_o_nexora_e_remove()
    {
        var casa = Path.Combine(Path.GetTempPath(), "casa-cli-" + Guid.NewGuid().ToString("N")[..6]);
        var comando = new ComandoNoTerminal(casa, @"C:\Program Files\dotnet\dotnet.exe", @"D:\NexoraApp\Nexora.dll");
        Assert.False(comando.Instalado);
        Directory.CreateDirectory(comando.Pasta);
        File.WriteAllText(comando.Arquivo, comando.Conteudo);
        Assert.True(comando.Instalado);
        if (OperatingSystem.IsWindows()) Assert.Contains(@"""D:\NexoraApp\Nexora.dll"" --cli %*", comando.Conteudo);
        comando.Remover();
        Assert.False(comando.Instalado);
        Directory.Delete(casa, true);
    }
}
