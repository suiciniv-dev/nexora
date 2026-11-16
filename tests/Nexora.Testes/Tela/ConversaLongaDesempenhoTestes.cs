using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Nexora.Core.Ambientes;
using Nexora.Core.Historico;
using Nexora.ViewModels;
using Nexora.Views;
using Xunit.Abstractions;

namespace Nexora.Testes.Tela;

public sealed class ConversaLongaDesempenhoTestes(ITestOutputHelper saida) : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-longa-desempenho-").FullName;

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

    double _carregar;

    async Task<ChatVM> Abrir(int turnos)
    {
        var arquivo = Path.Combine(_pasta, $"longa-{turnos}.jsonl");
        TranscricaoDeExemplo.Escrever(arquivo, turnos);
        var relogio = Stopwatch.StartNew();
        var chat = ChatVM.DeConversa(new ConversaSalva("longa", arquivo, "Longa", _pasta, null, DateTime.Now), new Local("Loja", _pasta, []),
            new AnfitriaoFalso(), _ => { }, _ => { });
        await chat.Carregamento;
        _carregar = relogio.Elapsed.TotalMilliseconds;
        return chat;
    }

    static double MontarVista(Window janela, ChatVM chat)
    {
        janela.Content = null;
        janela.UpdateLayout();
        var relogio = Stopwatch.StartNew();
        janela.Content = new ChatView { DataContext = chat };
        janela.UpdateLayout();
        return relogio.Elapsed.TotalMilliseconds;
    }

    [Fact]
    public Task AbrirConversaLongaMontaPoucasMensagensNoPrimeiroQuadro() => Sessao.Rodar(async () =>
    {
        var chat = await Abrir(300);
        var carregar = _carregar;
        var janela = new Window { Width = 1440, Height = 900 };
        janela.Show();
        MontarVista(janela, chat);

        var primeiro = Enumerable.Range(0, 9).Select(_ => MontarVista(janela, chat)).Order().ToList();
        Assert.Equal(ChatVM.LoteInicial, chat.ItensNaTela.Count);
        while (chat.MontarMais(1000)) { }
        var tudo = Enumerable.Range(0, 9).Select(_ => MontarVista(janela, chat)).Order().ToList();
        saida.WriteLine($"carregar {carregar:0} ms; primeiro quadro com {ChatVM.LoteInicial} itens: mínimo {primeiro[0]:0} ms, mediana {primeiro[4]:0} ms; com {chat.ItensNaTela.Count} itens: mínimo {tudo[0]:0} ms, mediana {tudo[4]:0} ms");
        janela.Close();
    });
}
