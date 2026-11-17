using System.Text;
using Avalonia.Controls;
using Nexora.Core.Ambientes;
using Nexora.Core.Historico;
using Nexora.Core.Uso;
using Nexora.ViewModels;
using Xunit.Abstractions;

namespace Nexora.Testes.Tela;

public sealed class OrcamentoTestes(ITestOutputHelper saida) : IDisposable
{
    const double MB = 1024 * 1024;

    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-orcamento-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_pasta, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    static double Alocado(Action acao)
    {
        var antes = GC.GetAllocatedBytesForCurrentThread();
        acao();
        return (GC.GetAllocatedBytesForCurrentThread() - antes) / MB;
    }

    static string Blob(int kb) => "{\"type\":\"api-request-blob\",\"message\":{\"content\":\"" + new string('x', kb * 1024) + "\"}}";

    static string Usuario(int i, string sessao) =>
        $$$"""{"type":"user","uuid":"u{{{i}}}","sessionId":"{{{sessao}}}","cwd":"D:\\Projetos\\Loja","gitBranch":"main","timestamp":"2026-11-17T10:{{{i % 60:00}}}:00Z","message":{"role":"user","content":"Pedido {{{i}}}: ajuste o módulo."}}""";

    static string Resposta(int i, string sessao) =>
        $$$"""{"type":"assistant","uuid":"a{{{i}}}","sessionId":"{{{sessao}}}","cwd":"D:\\Projetos\\Loja","timestamp":"2026-11-17T10:{{{i % 60:00}}}:30Z","message":{"id":"msg_{{{sessao}}}_{{{i}}}","model":"claude-opus-5-5","role":"assistant","content":[{"type":"text","text":"Feito o passo {{{i}}}."}],"usage":{"input_tokens":10,"output_tokens":20,"cache_read_input_tokens":3000,"cache_creation_input_tokens":100""" + "}}}";

    string GravarSessao(string pasta, string id, int turnos, int blobKb)
    {
        Directory.CreateDirectory(pasta);
        var arquivo = Path.Combine(pasta, id + ".jsonl");
        var texto = new StringBuilder();
        for (var i = 0; i < turnos; i++)
        {
            texto.Append(Usuario(i, id)).Append('\n');
            if (i % 5 == 0) texto.Append(Blob(blobKb)).Append('\n');
            texto.Append(Resposta(i, id)).Append('\n');
        }
        File.WriteAllText(arquivo, texto.ToString());
        return arquivo;
    }

    [Fact]
    public void Listar_conversas_nao_copia_os_arquivos_inteiros_e_reaproveita_o_que_nao_mudou()
    {
        var raiz = Path.Combine(_pasta, "projetos");
        for (var i = 0; i < 40; i++) GravarSessao(Path.Combine(raiz, "D--Projetos-Loja" + i % 4), "s" + i, 60, 200);
        var primeira = Alocado(() => Assert.Equal(40, ConversasSalvas.Listar(raiz).Count));
        var segunda = Alocado(() => ConversasSalvas.Listar(raiz));
        saida.WriteLine($"ORCAMENTO listar: primeira {primeira:0.0} MB, segunda {segunda:0.00} MB");
        Assert.True(primeira < 8, $"Listar 40 conversas alocou {primeira:0.0} MB (limite 8 MB).");
        Assert.True(segunda < 0.5, $"Listar de novo sem mudanças alocou {segunda:0.00} MB (limite 0,5 MB).");
    }

    [Fact]
    public void Ler_uso_nao_transforma_o_arquivo_novo_em_texto()
    {
        var raiz = Path.Combine(_pasta, "uso");
        var arquivo = GravarSessao(raiz, "u1", 400, 300);
        var cache = Path.Combine(_pasta, "uso-cache.json");
        var tamanho = new FileInfo(arquivo).Length / MB;
        var alocado = Alocado(() => Assert.NotEmpty(UsoLocal.Ler([new FonteDeUso(UsoLocal.Claude, raiz, "*.jsonl")], cache, new DateTime(2026, 11, 1), porDia: true)));
        saida.WriteLine($"ORCAMENTO uso: arquivo {tamanho:0.0} MB, alocou {alocado:0.0} MB");
        Assert.True(alocado < 6, $"Ler {tamanho:0.0} MB novos de uso alocou {alocado:0.0} MB (limite 6 MB).");
    }

    [Fact]
    public void Ler_andamento_nao_cria_texto_grande()
    {
        var arquivo = GravarSessao(Path.Combine(_pasta, "andamento"), "a1", 200, 300);
        Andamento.Ler(arquivo);
        var alocado = Alocado(() => Andamento.Ler(arquivo));
        saida.WriteLine($"ORCAMENTO andamento: alocou {alocado:0.00} MB");
        Assert.True(alocado < 0.3, $"Ler o andamento alocou {alocado:0.00} MB (limite 0,3 MB).");
    }

    [Fact]
    public Task Montar_conversa_com_40_itens_fica_no_orcamento() => Sessao.Rodar(() =>
    {
        var chat = ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
        for (var i = 0; i < 40; i++) chat.Itens.Add(i % 2 == 0 ? ItemAviso.Info($"Aviso {i}") : new ItemTexto($"Resposta {i} com **negrito** e `codigo`."));
        var janela = new Window { Width = 1440, Height = 900 };
        janela.Show();
        for (var i = 0; i < 3; i++)
        {
            janela.Content = new ChatView { DataContext = chat };
            janela.UpdateLayout();
            janela.Content = null;
            janela.UpdateLayout();
        }
        var alocado = Alocado(() =>
        {
            janela.Content = new ChatView { DataContext = chat };
            janela.UpdateLayout();
        });
        saida.WriteLine($"ORCAMENTO tela: alocou {alocado:0.0} MB");
        Assert.True(alocado < 22, $"Montar a conversa com 40 itens alocou {alocado:0.0} MB (limite 22 MB).");
        janela.Close();
    });
}
