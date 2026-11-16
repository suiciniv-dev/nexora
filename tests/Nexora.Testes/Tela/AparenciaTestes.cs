using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Nexora.Temas;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class AparenciaTestes
{
    [Fact]
    public Task AnimacoesReduzidasTiramAsTransicoesEVoltamAoReligar() => Sessao.Rodar(() =>
    {
        var antes = App.Config.Animacoes;
        var estilo = new Style(x => x.OfType<Border>().Class("transicao-de-teste"))
        {
            Setters = { new Setter(Animatable.TransitionsProperty, new Transitions { new DoubleTransition { Property = Avalonia.Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(200) } }) },
        };
        var janela = new Window { Width = 400, Height = 300 };
        janela.Styles.Add(estilo);
        var caixa = new Border { Classes = { "transicao-de-teste" } };
        janela.Content = caixa;
        try
        {
            Aparencia.Ligar();
            Aparencia.DefinirAnimacoes(Aparencia.AnimacoesLigadas);
            janela.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.NotEmpty(caixa.Transitions!);

            Aparencia.DefinirAnimacoes(Aparencia.AnimacoesReduzidasId);
            Assert.True(Aparencia.AnimacoesReduzidas);
            Assert.True(caixa.Transitions is null or { Count: 0 });
            var nova = new Border { Classes = { "transicao-de-teste" } };
            janela.Content = nova;
            Dispatcher.UIThread.RunJobs();
            Assert.True(nova.Transitions is null or { Count: 0 });

            Aparencia.DefinirAnimacoes(Aparencia.AnimacoesLigadas);
            Assert.False(Aparencia.AnimacoesReduzidas);
            Assert.NotEmpty(nova.Transitions!);
        }
        finally
        {
            janela.Close();
            Aparencia.DefinirAnimacoes(antes);
            App.Config.Animacoes = antes;
        }
    });

    [Fact]
    public Task FonteDoSistemaTemPesoMedioESemiNegritoDeVerdade() => Sessao.Rodar(() =>
    {
        if (!FonteDoSistema.Disponivel) return;
        FontManager.Current.AddFontCollection(new FonteDoSistema());
        foreach (var peso in new[] { FontWeight.Medium, FontWeight.SemiBold })
        {
            Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(new FontFamily(FonteDoSistema.Familia), FontStyle.Normal, peso), out var fonte));
            Assert.Equal(FontWeight.SemiBold, fonte!.Weight);
        }
        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(new FontFamily(FonteDoSistema.Familia), FontStyle.Normal, FontWeight.Normal), out var normal));
        Assert.Equal(FontWeight.Normal, normal!.Weight);
    });

    [Fact]
    public Task GaleriaMarcaOTemaEscolhidoESeguirOSistema() => Sessao.Rodar(() =>
    {
        var antes = App.Config.Tema;
        var usados = new List<string>();
        try
        {
            var galeria = new TemasVM(usados.Add);
            var sistema = galeria.Cartoes[0];
            Assert.True(sistema.EhSistema);
            Assert.Equal("sistema", sistema.Id);

            var dracula = galeria.Cartoes.First(c => c.Id == "dracula");
            dracula.UsarCommand.Execute(null);
            Assert.Equal("dracula", Catalogo.Escolhido);
            Assert.True(dracula.EmUso);
            Assert.False(sistema.EmUso);

            sistema.UsarCommand.Execute(null);
            Assert.Equal("sistema", Catalogo.Escolhido);
            Assert.True(sistema.EmUso);
            Assert.False(dracula.EmUso);
            Assert.Equal(["dracula", "sistema"], usados);
            Assert.Equal(["Nexora", Core.Texto.Idioma.T("Temas animados")], galeria.Grupos.Take(2).Select(g => g.Titulo));
        }
        finally
        {
            Catalogo.Escolher(antes);
        }
    });

    [Fact]
    public Task ConfiguracoesTemOsTemasNaPropriaTela() => Sessao.Rodar(() =>
    {
        var demoAntes = JanelaVM.Demo;
        try
        {
            JanelaVM.Demo = true;
            var vm = new JanelaVM();
            var janela = new Janela { DataContext = vm, Width = 1440, Height = 900 };
            janela.Show();
            vm.MostrarTemasCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(vm.NaConfiguracoes);
            Assert.Contains(Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(janela).OfType<TemasView>(), t => t.IsEffectivelyVisible);
            janela.Close();
        }
        finally
        {
            JanelaVM.Demo = demoAntes;
        }
    });
}
