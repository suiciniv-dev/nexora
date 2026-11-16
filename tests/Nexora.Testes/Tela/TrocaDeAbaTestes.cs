using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Claude;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class TrocaDeAbaTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-abas-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    ChatVM Conversa(int mensagens)
    {
        var chat = ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
        for (var i = 0; i < mensagens; i++)
            chat.Aplicar(new TextoCompleto($"## Parte {i}\n\nTexto com **negrito**, `codigo` e uma lista:\n\n- um\n- dois\n\n```csharp\nvar x = {i};\n```"));
        return chat;
    }

    static void Trocar(Window janela, AreaVM area, int indice)
    {
        area.Selecionada = area.Guias[indice];
        Dispatcher.UIThread.RunJobs();
        janela.UpdateLayout();
    }

    [Fact]
    public Task TrocarDeAbaReaproveitaATelaDaConversa() => Sessao.Rodar(() =>
    {
        var area = new AreaVM(new JanelaVM(), true);
        var a = Conversa(150);
        var b = Conversa(150);
        area.Abrir(a);
        area.Abrir(b);
        var janela = new Window { Width = 1200, Height = 800, Content = new AreaView { DataContext = area } };
        janela.Show();
        Trocar(janela, area, 0);
        var telaDeA = janela.GetVisualDescendants().OfType<ChatView>().Single(v => v.IsEffectivelyVisible);
        Assert.Same(a, telaDeA.DataContext);
        var mensagem = telaDeA.GetVisualDescendants().OfType<MarkdownTexto>().First();

        Trocar(janela, area, 1);
        Trocar(janela, area, 0);
        Trocar(janela, area, 1);

        Trocar(janela, area, 0);
        var visivel = janela.GetVisualDescendants().OfType<ChatView>().Single(v => v.IsEffectivelyVisible);
        Assert.Same(telaDeA, visivel);
        Assert.Same(a, visivel.DataContext);
        Assert.Contains(mensagem, visivel.GetVisualDescendants());
        Assert.Equal(2, janela.GetVisualDescendants().OfType<ChatView>().Count());

        area.Soltar(area.Guias[1]);
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(janela.GetVisualDescendants().OfType<ChatView>(), v => v.DataContext == b);
        janela.Close();
    });
}
