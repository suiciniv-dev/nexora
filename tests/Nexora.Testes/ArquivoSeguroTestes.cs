using System.Text;
using Nexora.Core.Ambientes;
using Nexora.Core.Arquivos;

namespace Nexora.Testes;

public sealed class ArquivoSeguroTestes : IDisposable
{
    readonly string _pasta = Path.Combine(Path.GetTempPath(), "nexora-arquivo-seguro-" + Guid.NewGuid().ToString("N")[..8]);

    public ArquivoSeguroTestes() => Directory.CreateDirectory(_pasta);

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    sealed class Dados
    {
        public string Nome { get; set; } = "";
        public List<int> Numeros { get; set; } = [];
    }

    string Caminho(string nome = "dados.json") => Path.Combine(_pasta, nome);

    string[] Sobras() => [.. Directory.EnumerateFiles(_pasta, "*.tmp")];

    [Fact]
    public void Gravar_cria_o_arquivo_sem_deixar_temporario()
    {
        ArquivoSeguro.Gravar(Caminho(), "{\"Nome\":\"a\"}");
        Assert.Equal("{\"Nome\":\"a\"}", File.ReadAllText(Caminho()));
        Assert.Empty(Sobras());
        Assert.False(File.Exists(ArquivoSeguro.Copia(Caminho())));
    }

    [Fact]
    public void Gravacao_na_fila_fica_so_com_a_ultima_e_a_leitura_ve_o_que_ainda_nao_foi_gravado()
    {
        for (var i = 0; i < 50; i++) ArquivoSeguro.Enfileirar(Caminho(), "{\"Nome\":\"" + i + "\"}", manterCopia: true);
        Assert.Equal("49", ArquivoSeguro.LerJson(Caminho(), () => new Dados()).Valor.Nome);

        ArquivoSeguro.Enfileirar(Caminho(), "{\"Nome\":\"fila\"}");
        ArquivoSeguro.Gravar(Caminho(), "{\"Nome\":\"na hora\"}");
        Assert.True(ArquivoSeguro.Esperar(TimeSpan.FromSeconds(5)));
        Assert.Equal("{\"Nome\":\"na hora\"}", File.ReadAllText(Caminho()));

        ArquivoSeguro.Enfileirar(Caminho("outro.json"), "{\"Nome\":\"b\"}");
        Assert.True(ArquivoSeguro.Esperar(TimeSpan.FromSeconds(5)));
        Assert.Equal("{\"Nome\":\"b\"}", File.ReadAllText(Caminho("outro.json")));
        Assert.Empty(Sobras());
    }

    [Fact]
    public void Gravar_com_copia_guarda_a_versao_anterior()
    {
        ArquivoSeguro.Gravar(Caminho(), "um", manterCopia: true);
        ArquivoSeguro.Gravar(Caminho(), "dois", manterCopia: true);
        Assert.Equal("dois", File.ReadAllText(Caminho()));
        Assert.Equal("um", File.ReadAllText(ArquivoSeguro.Copia(Caminho())));
    }

    [Fact]
    public void Gravar_respeita_o_bom_da_codificacao()
    {
        ArquivoSeguro.Gravar(Caminho("bom.txt"), "x", new UTF8Encoding(true));
        Assert.Equal([0xEF, 0xBB, 0xBF, (byte)'x'], File.ReadAllBytes(Caminho("bom.txt")));
        ArquivoSeguro.Gravar(Caminho("sem.txt"), "x");
        Assert.Equal([(byte)'x'], File.ReadAllBytes(Caminho("sem.txt")));
    }

    [Fact]
    public void Ler_sem_arquivo_devolve_novo()
    {
        var lida = ArquivoSeguro.LerJson(Caminho(), () => new Dados { Nome = "vazio" });
        Assert.Equal(OrigemDaLeitura.Novo, lida.Origem);
        Assert.Equal("vazio", lida.Valor.Nome);
        Assert.False(lida.Recuperado);
    }

    [Fact]
    public void Ler_arquivo_bom_devolve_o_principal()
    {
        ArquivoSeguro.Gravar(Caminho(), "{\"Nome\":\"ok\",\"Numeros\":[1,2]}");
        var lida = ArquivoSeguro.LerJson(Caminho(), () => new Dados());
        Assert.Equal(OrigemDaLeitura.Principal, lida.Origem);
        Assert.Equal([1, 2], lida.Valor.Numeros);
        Assert.Null(lida.Guardado);
    }

    [Theory]
    [InlineData("{\"Nome\":\"cort")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\0\0\0\0")]
    [InlineData("null")]
    public void Principal_danificado_volta_da_copia_e_guarda_o_danificado(string danificado)
    {
        ArquivoSeguro.Gravar(Caminho(), "{\"Nome\":\"antigo\"}", manterCopia: true);
        ArquivoSeguro.Gravar(Caminho(), "{\"Nome\":\"novo\"}", manterCopia: true);
        File.WriteAllText(Caminho(), danificado);

        var lida = ArquivoSeguro.LerJson(Caminho(), () => new Dados { Nome = "vazio" });

        Assert.Equal(OrigemDaLeitura.Copia, lida.Origem);
        Assert.Equal("antigo", lida.Valor.Nome);
        Assert.True(lida.Recuperado);
        Assert.NotNull(lida.Guardado);
        Assert.Equal(danificado, File.ReadAllText(lida.Guardado!));
    }

    [Fact]
    public void Principal_danificado_sem_copia_comeca_vazio_mas_guarda_o_danificado()
    {
        File.WriteAllText(Caminho(), "{ nada disso é json");
        var lida = ArquivoSeguro.LerJson(Caminho(), () => new Dados { Nome = "vazio" });
        Assert.Equal(OrigemDaLeitura.Perdido, lida.Origem);
        Assert.Equal("vazio", lida.Valor.Nome);
        Assert.Equal("{ nada disso é json", File.ReadAllText(lida.Guardado!));
    }

    [Fact]
    public void Arquivo_em_uso_fica_ilegivel_e_nao_e_tratado_como_danificado()
    {
        ArquivoSeguro.Gravar(Caminho(), "{\"Nome\":\"meu\"}");
        LeituraSegura<Dados> lida;
        using (new FileStream(Caminho(), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            lida = ArquivoSeguro.LerJson(Caminho(), () => new Dados());
        Assert.Equal(OrigemDaLeitura.Ilegivel, lida.Origem);
        Assert.False(lida.PodeGravar);
        Assert.Null(lida.Guardado);
        Assert.Empty(Directory.EnumerateFiles(_pasta, "*.corrompido-*"));
    }

    [Fact]
    public void Gravacoes_simultaneas_terminam_com_um_arquivo_inteiro()
    {
        var conteudos = Enumerable.Range(0, 40).Select(i => "{\"Nome\":\"" + new string((char)('a' + i % 26), 5000) + "\"}").ToArray();
        Parallel.ForEach(conteudos, c => ArquivoSeguro.Gravar(Caminho(), c, manterCopia: true));
        Assert.Contains(File.ReadAllText(Caminho()), conteudos);
        Assert.Contains(File.ReadAllText(ArquivoSeguro.Copia(Caminho())), conteudos);
        Assert.Empty(Sobras());
    }

    [Fact]
    public void Config_danificado_volta_da_copia_e_avisa()
    {
        var caminho = Caminho("config.json");
        var primeira = Config.Carregar(caminho);
        Assert.Equal(OrigemDaLeitura.Novo, primeira.Origem);
        primeira.Workspaces = [@"D:\um"];
        primeira.Salvar();
        primeira.Workspaces = [@"D:\um", @"D:\dois"];
        primeira.Salvar();
        File.WriteAllText(caminho, "{\"Workspaces\":[\"D:\\\\u");

        var lida = Config.Carregar(caminho);

        Assert.True(lida.Recuperada);
        Assert.Equal([@"D:\um"], lida.Workspaces);
        Assert.NotNull(lida.Guardado);
        lida.Salvar();
        Assert.Equal(OrigemDaLeitura.Principal, Config.Carregar(caminho).Origem);
    }

    [Fact]
    public void Config_ilegivel_nao_e_sobrescrito()
    {
        var caminho = Caminho("config.json");
        var original = Config.Carregar(caminho);
        original.Workspaces = [@"D:\importante"];
        original.Salvar();
        var antes = File.ReadAllText(caminho);

        Config travada;
        using (new FileStream(caminho, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            travada = Config.Carregar(caminho);
        Assert.Equal(OrigemDaLeitura.Ilegivel, travada.Origem);
        Assert.False(travada.Recuperada);
        travada.Salvar();

        Assert.Equal(antes, File.ReadAllText(caminho));
        Assert.Equal([@"D:\importante"], Config.Carregar(caminho).Workspaces);
    }

    [Fact]
    public void Workspace_ilegivel_nao_e_trocado_so_pelas_pastas()
    {
        var principal = Path.Combine(_pasta, "projeto");
        Directory.CreateDirectory(principal);
        var arquivo = Path.Combine(principal, "projeto.code-workspace");
        const string meu = "{ \"folders\": [ { \"path\": \".\" } ], \"settings\": { \"editor.tabSize\": 2 }, \"launch\": ";
        File.WriteAllText(arquivo, meu);

        var erro = Assert.Throws<ArquivoIlegivelException>(() => Workspaces.Salvar(new Ambiente("projeto", principal, [], arquivo)));

        Assert.IsAssignableFrom<IOException>(erro);
        Assert.Equal(meu, File.ReadAllText(arquivo));
    }

    [Fact]
    public void Workspace_valido_mantem_settings_e_troca_as_pastas()
    {
        var principal = Path.Combine(_pasta, "app");
        var outra = Path.Combine(_pasta, "lib");
        Directory.CreateDirectory(principal);
        Directory.CreateDirectory(outra);
        var arquivo = Path.Combine(principal, "app.code-workspace");
        File.WriteAllText(arquivo, "{\n  // comentario\n  \"folders\": [{ \"path\": \".\" }],\n  \"settings\": { \"editor.tabSize\": 2 },\n}");

        Workspaces.Salvar(new Ambiente("app", principal, [outra], arquivo));

        var texto = File.ReadAllText(arquivo);
        Assert.Contains("editor.tabSize", texto);
        Assert.Contains("../lib", texto);
        Assert.Empty(Directory.EnumerateFiles(principal, "*.tmp"));
    }

    [Fact]
    public void Aviso_de_recuperacao_so_aparece_quando_algo_foi_recuperado_ou_nao_lido()
    {
        var nada = Nexora.ViewModels.JanelaVM.Recuperacoes((OrigemDaLeitura.Novo, "config.json", null), (OrigemDaLeitura.Principal, "nuvem.json", null));
        Assert.Empty(nada);
        var duas = Nexora.ViewModels.JanelaVM.Recuperacoes((OrigemDaLeitura.Copia, "config.json", "x.corrompido"), (OrigemDaLeitura.Ilegivel, "nuvem.json", null));
        Assert.Equal(2, duas.Count);
        Assert.Contains("config.json", duas[0].Texto);
        Assert.Contains("cópia boa", duas[0].Texto);
        Assert.Contains("em uso", duas[1].Texto);
    }
}
