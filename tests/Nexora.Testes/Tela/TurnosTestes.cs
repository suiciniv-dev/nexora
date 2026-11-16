using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Git;
using Nexora.Core.Historico;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class TurnosTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-turnos-").FullName;
    readonly string _conversas = Directory.CreateTempSubdirectory("nexora-turnos-conversas-").FullName;

    public void Dispose()
    {
        foreach (var pasta in new[] { _pasta, _conversas })
            try
            {
                foreach (var arquivo in Directory.EnumerateFiles(pasta, "*", SearchOption.AllDirectories)) File.SetAttributes(arquivo, FileAttributes.Normal);
                Directory.Delete(pasta, recursive: true);
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

    static async Task Esperar(Func<bool> condicao, int segundos = 20)
    {
        var limite = DateTime.Now.AddSeconds(segundos);
        while (!condicao() && DateTime.Now < limite)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
        Dispatcher.UIThread.RunJobs();
        Assert.True(condicao(), "a condição não aconteceu a tempo");
    }

    static IEnumerable<string> TextosVisiveis(Window janela) =>
        janela.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Text is { Length: > 0 }).Select(t => t.Text!);

    [Fact]
    public Task ResumoDoTurnoApareceNoFimEAbreOPainelGit() => Sessao.Rodar(() =>
    {
        var chat = ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
        var pedido = new ItemUsuario("Faça o cupom");
        chat.Itens.Add(pedido);
        chat.Itens.Add(new ItemTexto("Feito."));
        chat.AdicionarTurnoDemo(pedido, [new("src/Cupom.cs", "M", 30, 10, false), new("src/Novo.cs", "A", 12, 0, false)]);
        var janela = new Window { Width = 1100, Height = 800, Content = new ChatView { DataContext = chat } };
        janela.Show();
        Dispatcher.UIThread.RunJobs();

        var textos = TextosVisiveis(janela).ToList();
        Assert.Contains("2 arquivos", textos);
        Assert.Contains("+42", textos);
        Assert.Contains("−10", textos);
        Assert.Contains("Ver o que mudou", textos);
        Assert.Contains("Desfazer este turno", textos);

        var turno = Assert.Single(chat.Turnos);
        turno.DesfazerCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(turno.TextoConfirmar, TextosVisiveis(janela));
        Assert.DoesNotContain("Desfazer este turno", TextosVisiveis(janela));
        turno.CancelarCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("Desfazer este turno", TextosVisiveis(janela));

        turno.VerCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("git", chat.AbaDetalhe);
        Assert.True(chat.Painel.MostraAlteracoes);
        Assert.Same(turno, chat.Painel.OpcaoDiff?.Turno);
        Assert.True(chat.Painel.TemOpcoesDiff);
        Assert.Equal(["Cupom.cs", "Novo.cs"], chat.Painel.Alteracoes.Select(a => a.Nome));
        Assert.True(chat.Painel.Alteracoes[1].Nova);
        Assert.Equal("+30", chat.Painel.Alteracoes[0].TextoMais);
        Assert.DoesNotContain(chat.Trecho, i => i is ItemTurno);
        janela.Close();
    });

    [Fact]
    public Task DesfazerVoltaArquivosEConversaDoClaudeSemMexerNoHead() => Sessao.Rodar(async () =>
    {
        Git(_pasta, "init", "-b", "main");
        Git(_pasta, "config", "user.email", "teste@exemplo.com");
        Git(_pasta, "config", "user.name", "Teste");
        Git(_pasta, "config", "core.autocrlf", "false");
        File.WriteAllText(Path.Combine(_pasta, ".gitignore"), "bin/\n");
        File.WriteAllText(Path.Combine(_pasta, "a.txt"), "v1");
        Git(_pasta, "add", ".");
        Git(_pasta, "commit", "-m", "primeiro");
        var head = Git(_pasta, "rev-parse", "HEAD");

        var id = "aaaaaaaa-0000-0000-0000-000000000001";
        var arquivo = Path.Combine(_conversas, id + ".jsonl");
        string Usuario(string texto, string uuid) => $"{{\"type\":\"user\",\"sessionId\":\"{id}\",\"uuid\":\"{uuid}\",\"message\":{{\"role\":\"user\",\"content\":\"{texto}\"}}}}\n";
        string Claude(string texto, string uuid) => $"{{\"type\":\"assistant\",\"sessionId\":\"{id}\",\"uuid\":\"{uuid}\",\"message\":{{\"role\":\"assistant\",\"content\":[{{\"type\":\"text\",\"text\":\"{texto}\"}}]}}}}\n";
        var primeiro = Usuario("primeiro pedido", "u1") + Claude("ok 1", "a1");
        File.WriteAllText(arquivo, primeiro + Usuario("segundo pedido", "u2") + Claude("ok 2", "a2"));
        var bytes = System.Text.Encoding.UTF8.GetByteCount(primeiro);

        string Mensagem(int turno, string texto, long b) => PontosDeRestauracao.Mensagem("teste", new Dictionary<string, string?>
        {
            ["turno"] = turno.ToString(),
            ["texto"] = PontosDeRestauracao.Impressao(texto),
            ["transcricao"] = arquivo,
            ["bytes"] = b.ToString(),
        });
        var antes1 = await PontosDeRestauracao.CapturarAsync(_pasta, Mensagem(1, "primeiro pedido", 0));
        File.WriteAllText(Path.Combine(_pasta, "a.txt"), "v2");
        File.WriteAllText(Path.Combine(_pasta, "b.txt"), "b");
        var depois1 = await PontosDeRestauracao.CapturarAsync(_pasta, "depois", antes1);
        var antes2 = await PontosDeRestauracao.CapturarAsync(_pasta, Mensagem(2, "segundo pedido", bytes));
        File.WriteAllText(Path.Combine(_pasta, "a.txt"), "v3");
        File.WriteAllText(Path.Combine(_pasta, "c.txt"), "c");
        Directory.CreateDirectory(Path.Combine(_pasta, "bin"));
        File.WriteAllText(Path.Combine(_pasta, "bin", "saida.dll"), "build");
        var depois2 = await PontosDeRestauracao.CapturarAsync(_pasta, "depois", antes2);
        Assert.True(await PontosDeRestauracao.GuardarAsync(_pasta, [
            (PontosDeRestauracao.RefDe(id, 1, PontosDeRestauracao.Antes), antes1!),
            (PontosDeRestauracao.RefDe(id, 1, PontosDeRestauracao.Depois), depois1!),
            (PontosDeRestauracao.RefDe(id, 2, PontosDeRestauracao.Antes), antes2!),
            (PontosDeRestauracao.RefDe(id, 2, PontosDeRestauracao.Depois), depois2!),
        ]));

        var chat = ChatVM.DeConversa(new ConversaSalva(id, arquivo, "Teste", _pasta, null, DateTime.Now), new Local("Loja", _pasta, []),
            new AnfitriaoFalso(), _ => { }, _ => { });
        await chat.Carregamento;
        await Esperar(() => chat.Turnos.Count == 2 && chat.Turnos.All(t => t.Visivel));
        var (turno1, turno2) = (chat.Turnos[0], chat.Turnos[1]);
        Assert.Equal(2, turno1.Resumo!.Arquivos.Count);
        Assert.Equal(["a.txt", "c.txt"], turno2.Resumo!.Arquivos.Select(a => a.Caminho).Order());
        Assert.Equal(chat.Itens.IndexOf(chat.Itens.OfType<ItemUsuario>().Last()) - 1, chat.Itens.IndexOf(turno1));
        Assert.Equal(1, turno1.Seguintes);

        turno2.ConfirmarCommand.Execute(null);
        await Esperar(() => chat.Itens.OfType<ItemAviso>().Any(a => a.Texto.StartsWith("Turno desfeito")));

        Assert.Equal("v2", File.ReadAllText(Path.Combine(_pasta, "a.txt")));
        Assert.True(File.Exists(Path.Combine(_pasta, "b.txt")));
        Assert.False(File.Exists(Path.Combine(_pasta, "c.txt")));
        Assert.Equal("build", File.ReadAllText(Path.Combine(_pasta, "bin", "saida.dll")));
        Assert.Equal(head, Git(_pasta, "rev-parse", "HEAD"));
        Assert.Equal("main", Git(_pasta, "branch", "--show-current"));
        Assert.Equal("", Git(_pasta, "diff", "--cached", "--name-only"));
        Assert.NotEqual(id, chat.SessaoId);
        Assert.Equal(id, chat.SessaoOrigem);
        var copia = Path.Combine(_conversas, chat.SessaoId + ".jsonl");
        Assert.Equal(2, File.ReadAllLines(copia).Length);
        Assert.DoesNotContain("segundo pedido", File.ReadAllText(copia));
        Assert.DoesNotContain(chat.Itens.OfType<ItemUsuario>(), m => m.Texto == "segundo pedido");
        Assert.Equal("segundo pedido", chat.Rascunho);
        Assert.Same(turno1, Assert.Single(chat.Turnos));
        Assert.Equal(0, turno1.Seguintes);
        var nova = chat.SessaoId!;
        await Esperar(() => Task.Run(() => PontosDeRestauracao.ListarAsync(_pasta, nova)).Result.Count == 2);

        chat.Rascunho = "";
        turno1.ConfirmarCommand.Execute(null);
        await Esperar(() => chat.Itens.OfType<ItemAviso>().Count(a => a.Texto.StartsWith("Turno desfeito")) == 1 && !chat.Itens.OfType<ItemUsuario>().Any());

        Assert.Equal("v1", File.ReadAllText(Path.Combine(_pasta, "a.txt")));
        Assert.False(File.Exists(Path.Combine(_pasta, "b.txt")));
        Assert.Null(chat.SessaoId);
        Assert.Empty(chat.Turnos);
        Assert.Equal("primeiro pedido", chat.Rascunho);
        Assert.Equal(head, Git(_pasta, "rev-parse", "HEAD"));
    });
}
