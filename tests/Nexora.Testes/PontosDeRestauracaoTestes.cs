using System.Diagnostics;
using Nexora.Core.Git;
using Nexora.Core.Historico;

namespace Nexora.Testes;

public sealed class PontosDeRestauracaoTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-pontos-").FullName;

    public void Dispose()
    {
        try
        {
            foreach (var arquivo in Directory.EnumerateFiles(_pasta, "*", SearchOption.AllDirectories)) File.SetAttributes(arquivo, FileAttributes.Normal);
            Directory.Delete(_pasta, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    static string Git(string pasta, params string[] argumentos)
    {
        var inicio = new ProcessStartInfo("git") { WorkingDirectory = pasta, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in argumentos) inicio.ArgumentList.Add(a);
        using var processo = Process.Start(inicio)!;
        var saida = processo.StandardOutput.ReadToEnd();
        processo.StandardError.ReadToEnd();
        processo.WaitForExit(20_000);
        return saida.Trim();
    }

    string Arquivo(string relativo) => Path.Combine(_pasta, relativo.Replace('/', Path.DirectorySeparatorChar));

    void Escrever(string relativo, string conteudo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Arquivo(relativo))!);
        File.WriteAllText(Arquivo(relativo), conteudo);
    }

    string Ler(string relativo) => File.ReadAllText(Arquivo(relativo));

    void Repositorio()
    {
        Git(_pasta, "init", "-b", "main");
        Git(_pasta, "config", "user.email", "teste@exemplo.com");
        Git(_pasta, "config", "user.name", "Teste");
        Git(_pasta, "config", "core.autocrlf", "false");
        Escrever(".gitignore", "bin/\n.env\n");
        Escrever("a.txt", "a");
        Escrever("b.txt", "b");
        Escrever("sub/c.txt", "c");
        Git(_pasta, "add", ".");
        Git(_pasta, "commit", "-m", "primeiro");
    }

    [Fact]
    public async Task Restaurar_volta_os_arquivos_sem_mexer_em_ignorados_head_branch_e_indice()
    {
        Repositorio();
        Escrever("a.txt", "a2");
        Git(_pasta, "add", "a.txt");
        Escrever("solto.txt", "solto");
        Escrever("bin/x.dll", "dll1");
        Escrever(".env", "SEGREDO=1");
        var head = Git(_pasta, "rev-parse", "HEAD");
        var indice = Git(_pasta, "ls-files", "-s");
        var preparado = Git(_pasta, "diff", "--cached", "--name-only");
        var refsAntes = Git(_pasta, "for-each-ref", "refs/heads", "refs/tags");
        var stashAntes = Git(_pasta, "stash", "list");

        var antes = await PontosDeRestauracao.CapturarAsync(_pasta, "teste");
        Assert.NotNull(antes);
        Assert.Equal(indice, Git(_pasta, "ls-files", "-s"));
        Assert.Equal(head, Git(_pasta, "rev-parse", "HEAD"));

        Escrever("b.txt", "b-mudou");
        File.Delete(Arquivo("sub/c.txt"));
        Escrever("novo.txt", "novo");
        Escrever("nova/pasta/d.txt", "d");
        File.Delete(Arquivo("solto.txt"));
        Escrever("bin/x.dll", "dll2");
        Escrever("bin/y.dll", "y");
        Escrever(".env", "SEGREDO=2");

        var r = await PontosDeRestauracao.RestaurarAsync(_pasta, antes!, PontosDeRestauracao.RefDesfeito("s1"));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("a2", Ler("a.txt"));
        Assert.Equal("b", Ler("b.txt"));
        Assert.Equal("c", Ler("sub/c.txt"));
        Assert.Equal("solto", Ler("solto.txt"));
        Assert.False(File.Exists(Arquivo("novo.txt")));
        Assert.False(Directory.Exists(Arquivo("nova")));
        Assert.Equal("dll2", Ler("bin/x.dll"));
        Assert.Equal("y", Ler("bin/y.dll"));
        Assert.Equal("SEGREDO=2", Ler(".env"));
        Assert.True(Directory.Exists(Arquivo(".git")));
        Assert.Equal(head, Git(_pasta, "rev-parse", "HEAD"));
        Assert.Equal("main", Git(_pasta, "branch", "--show-current"));
        Assert.Equal(indice, Git(_pasta, "ls-files", "-s"));
        Assert.Equal(preparado, Git(_pasta, "diff", "--cached", "--name-only"));
        Assert.Equal(refsAntes, Git(_pasta, "for-each-ref", "refs/heads", "refs/tags"));
        Assert.Equal(stashAntes, Git(_pasta, "stash", "list"));
        Assert.Contains("refs/nexora/desfeitos/s1/", Git(_pasta, "for-each-ref", "--format=%(refname)", "refs/nexora/"));
    }

    [Fact]
    public async Task Restaurar_nao_apaga_repositorio_aninhado_criado_depois()
    {
        Repositorio();
        var antes = await PontosDeRestauracao.CapturarAsync(_pasta, "teste");
        Assert.NotNull(antes);
        var aninhado = Arquivo("aninhado");
        Directory.CreateDirectory(aninhado);
        Git(aninhado, "init", "-b", "main");
        Git(aninhado, "config", "user.email", "teste@exemplo.com");
        Git(aninhado, "config", "user.name", "Teste");
        File.WriteAllText(Path.Combine(aninhado, "x.txt"), "x");
        Git(aninhado, "add", ".");
        Git(aninhado, "commit", "-m", "x");
        Escrever("novo.txt", "novo");

        var r = await PontosDeRestauracao.RestaurarAsync(_pasta, antes!);

        Assert.True(r.Ok, r.Mensagem);
        Assert.False(File.Exists(Arquivo("novo.txt")));
        Assert.True(File.Exists(Path.Combine(aninhado, "x.txt")));
        Assert.True(Directory.Exists(Path.Combine(aninhado, ".git")));
    }

    [Fact]
    public async Task Contagem_do_turno_compara_antes_e_depois()
    {
        Repositorio();
        var antes = await PontosDeRestauracao.CapturarAsync(_pasta, "antes");
        Escrever("b.txt", "b\nmais\numa");
        Escrever("novo.txt", "1\n2\n3\n");
        File.Delete(Arquivo("a.txt"));
        Escrever("bin/z.dll", "ignorado");
        var depois = await PontosDeRestauracao.CapturarAsync(_pasta, "depois", antes);

        var resumo = await PontosDeRestauracao.ContarAsync(_pasta, antes!, depois!);

        Assert.NotNull(resumo);
        Assert.Equal(["a.txt", "b.txt", "novo.txt"], resumo!.Arquivos.Select(a => a.Caminho).Order());
        Assert.True(resumo.Arquivos.Single(a => a.Caminho == "novo.txt").Novo);
        Assert.True(resumo.Arquivos.Single(a => a.Caminho == "a.txt").Apagado);
        Assert.Equal(3 + 3, resumo.Mais);
        Assert.Equal(1 + 1, resumo.Menos);
        Assert.Contains("+mais", await PontosDeRestauracao.DiffAsync(resumo.Raiz, antes!, depois!, "b.txt"));
    }

    [Fact]
    public async Task Refs_sao_guardadas_listadas_apagadas_e_podadas()
    {
        Repositorio();
        var mensagem = PontosDeRestauracao.Mensagem("nexora", new Dictionary<string, string?> { ["turno"] = "1", ["texto"] = PontosDeRestauracao.Impressao("oi\r\n"), ["bytes"] = "42" });
        var antes = await PontosDeRestauracao.CapturarAsync(_pasta, mensagem);
        var depois = await PontosDeRestauracao.CapturarAsync(_pasta, "depois", antes);
        var segundo = await PontosDeRestauracao.CapturarAsync(_pasta, mensagem);
        Assert.True(await PontosDeRestauracao.GuardarAsync(_pasta, [
            (PontosDeRestauracao.RefDe("sessao-1", 1, PontosDeRestauracao.Antes), antes!),
            (PontosDeRestauracao.RefDe("sessao-1", 1, PontosDeRestauracao.Depois), depois!),
            (PontosDeRestauracao.RefDe("sessao-1", 2, PontosDeRestauracao.Antes), segundo!),
            (PontosDeRestauracao.RefDe("outra", 1, PontosDeRestauracao.Antes), antes!),
        ]));

        var pontos = await PontosDeRestauracao.ListarAsync(_pasta, "sessao-1");
        Assert.Equal(3, pontos.Count);
        Assert.Equal(antes, pontos[0].Commit);
        Assert.Equal(PontosDeRestauracao.Antes, pontos[0].Tipo);
        Assert.Equal(PontosDeRestauracao.Impressao("oi"), pontos[0].Dados["texto"]);
        Assert.Equal("42", pontos[0].Dados["bytes"]);
        Assert.Equal(PontosDeRestauracao.Depois, pontos[1].Tipo);
        Assert.Equal(2, pontos[2].Turno);
        Assert.Empty(Git(_pasta, "log", "--all", "--oneline", "--", "naoexiste").Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal("1", Git(_pasta, "rev-list", "--count", "HEAD"));

        Assert.Equal(1, await PontosDeRestauracao.ApagarAsync(_pasta, "sessao-1", aPartirDe: 2));
        Assert.Equal(2, (await PontosDeRestauracao.ListarAsync(_pasta, "sessao-1")).Count);
        Assert.Equal(0, await PontosDeRestauracao.PodarAsync(_pasta, TimeSpan.FromDays(14)));
        Assert.Equal(3, await PontosDeRestauracao.PodarAsync(_pasta, TimeSpan.FromDays(14), DateTimeOffset.UtcNow.AddDays(15)));
        Assert.Empty(await PontosDeRestauracao.ListarAsync(_pasta, "sessao-1"));
        Assert.Equal("", Git(_pasta, "for-each-ref", "refs/nexora/"));

        await PontosDeRestauracao.GuardarAsync(_pasta, [
            (PontosDeRestauracao.RefDe("x", 1, PontosDeRestauracao.Antes), antes!),
            (PontosDeRestauracao.RefDesfeito("x"), antes!),
            (PontosDeRestauracao.RefDe("y", 1, PontosDeRestauracao.Antes), antes!),
        ]);
        Assert.Equal(2, await PontosDeRestauracao.ApagarSessoesAsync(_pasta, new HashSet<string> { "x" }));
        Assert.Equal(1, await PontosDeRestauracao.ApagarTodosAsync(_pasta));
    }

    [Fact]
    public async Task Pasta_sem_git_nao_tem_pontos()
    {
        Assert.Null(PontosDeRestauracao.Raiz(Path.GetPathRoot(_pasta)!));
        Assert.Null(await PontosDeRestauracao.CapturarAsync(_pasta, "x"));
        Repositorio();
        Assert.Equal(_pasta, PontosDeRestauracao.Raiz(Path.Combine(_pasta, "sub")));
    }

    [Fact]
    public void Caminhos_perigosos_sao_recusados()
    {
        Assert.True(PontosDeRestauracao.CaminhoSeguro("src/a.cs"));
        Assert.False(PontosDeRestauracao.CaminhoSeguro(".git/config"));
        Assert.False(PontosDeRestauracao.CaminhoSeguro("sub/.GIT/x"));
        Assert.False(PontosDeRestauracao.CaminhoSeguro("../fora.txt"));
        Assert.False(PontosDeRestauracao.CaminhoSeguro("/abs"));
        Assert.False(PontosDeRestauracao.CaminhoSeguro("C:/x"));
        Assert.False(PontosDeRestauracao.CaminhoSeguro("a\\b"));
        Assert.False(PontosDeRestauracao.IdValido("../x"));
        Assert.True(PontosDeRestauracao.IdValido("0a1b-2c"));
    }

    [Fact]
    public void Copia_da_transcricao_para_antes_da_mensagem_e_troca_o_id()
    {
        var antigo = "11111111-1111-1111-1111-111111111111";
        var novo = "22222222-2222-2222-2222-222222222222";
        var arquivo = Path.Combine(_pasta, antigo + ".jsonl");
        var l1 = $"{{\"type\":\"user\",\"sessionId\":\"{antigo}\",\"uuid\":\"u1\"}}\n";
        var l2 = $"{{\"type\":\"assistant\",\"sessionId\":\"{antigo}\",\"uuid\":\"a1\"}}\n";
        var l3 = $"{{\"type\":\"user\",\"sessionId\":\"{antigo}\",\"uuid\":\"u2\"}}\n";
        File.WriteAllText(arquivo, l1 + l2 + l3);
        var bytes = System.Text.Encoding.UTF8.GetByteCount(l1 + l2);

        var copia = Transcricao.CopiarAte(arquivo, bytes + 5, novo);

        Assert.Equal(Path.Combine(_pasta, novo + ".jsonl"), copia);
        var linhas = File.ReadAllLines(copia!);
        Assert.Equal(2, linhas.Length);
        Assert.All(linhas, l => Assert.Contains($"\"sessionId\":\"{novo}\"", l));
        Assert.DoesNotContain("u2", File.ReadAllText(copia!));
        Assert.Equal(l1 + l2 + l3, File.ReadAllText(arquivo));
        Assert.Null(Transcricao.CopiarAte(arquivo, bytes, novo));
        Assert.Null(Transcricao.CopiarAte(arquivo, 10_000, "33333333-3333-3333-3333-333333333333"));
    }

    [Fact]
    public void Conversa_desfeita_esconde_a_original_e_mostra_a_copia()
    {
        var antigas = new Dictionary<string, string> { ["copia"] = "original", ["velha"] = "outra" };
        var desfeitos = new Dictionary<string, string> { ["original"] = "copia", ["sumiu"] = "naoexiste" };

        var r = VersoesDeConversa.ComDesfeitos(antigas, desfeitos, new HashSet<string> { "original", "copia", "velha", "outra" });

        Assert.Equal("copia", r["original"]);
        Assert.False(r.ContainsKey("copia"));
        Assert.Equal("outra", r["velha"]);
        Assert.False(r.ContainsKey("sumiu"));
    }
}
