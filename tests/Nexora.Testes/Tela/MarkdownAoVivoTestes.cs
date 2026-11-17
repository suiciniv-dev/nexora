using Avalonia.Controls;
using Avalonia.LogicalTree;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class MarkdownAoVivoTestes
{
    [Fact]
    public Task TextoCrescendoMantemOsBlocosProntosEAtualizaOCodigoNoLugar() => Sessao.Rodar(() =>
    {
        var md = new MarkdownTexto { Texto = "# Plano\n\nPrimeiro parágrafo com [link](https://exemplo.com).\n\n- um\n- dois" };
        Assert.Equal(3, md.Children.Count);
        var titulo = md.Children[0];
        var paragrafo = md.Children[1];

        md.Texto += "\n\n```csharp\nvar a = 1;";
        Assert.Equal(4, md.Children.Count);
        Assert.Same(titulo, md.Children[0]);
        Assert.Same(paragrafo, md.Children[1]);
        var codigo = md.Children[3];

        md.Texto += "\nvar b = 2;";
        Assert.Same(codigo, md.Children[3]);
        Assert.Contains(((Control)codigo).GetLogicalDescendants().OfType<SelectableTextBlock>(), t => t.Text == "var a = 1;\nvar b = 2;");

        md.Texto += "\n```\n\nFim.";
        Assert.Equal(5, md.Children.Count);
        Assert.Same(titulo, md.Children[0]);

        md.Texto = "Outro texto.";
        Assert.Single(md.Children);
        Assert.NotSame(titulo, md.Children[0]);
    });

    [Fact]
    public Task EscondidoSoMontaQuandoAparece() => Sessao.Rodar(() =>
    {
        var md = new MarkdownTexto { Texto = "Primeiro." };
        var painel = new Border { Child = md };
        var janela = new Window { Content = painel };
        janela.Show();
        var primeiro = md.Children[0];

        painel.IsVisible = false;
        janela.UpdateLayout();
        md.Texto += "\n\nSegundo.";
        md.Texto += "\n\nTerceiro.";
        janela.UpdateLayout();
        Assert.Single(md.Children);

        painel.IsVisible = true;
        janela.UpdateLayout();
        Assert.Equal(3, md.Children.Count);
        Assert.Same(primeiro, md.Children[0]);
        janela.Close();
    });
}
