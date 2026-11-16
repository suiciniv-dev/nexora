using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Texto;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

[Collection("Idioma")]
public sealed class SeletorDeIdiomaTestes
{
    [Fact]
    public Task EscolherIdiomaGravaNaConfigEAvisaParaReiniciar() => Sessao.Rodar(() =>
    {
        Idioma.Registrar(Traducao.Ingles.Pares);
        var anterior = App.Config.Idioma;
        var demoAntes = JanelaVM.Demo;
        try
        {
            App.Config.Idioma = "auto";
            JanelaVM.Demo = true;
            var config = new ConfiguracoesVM(new JanelaVM());
            Assert.Equal(["Automático", "Português", "English"], config.NomesDosIdiomas);
            Assert.Equal(0, config.IdiomaIndice);
            Assert.False(config.TemAviso);

            config.IdiomaIndice = 2;
            Assert.Equal("en", App.Config.Idioma);
            Assert.Equal("Reinicie o Nexora para trocar o idioma.  ·  Restart Nexora to switch the language.", config.Aviso);

            config.IdiomaIndice = 1;
            Assert.Equal("pt", App.Config.Idioma);

            config.IdiomaIndice = 0;
            Assert.Equal("auto", App.Config.Idioma);
        }
        finally
        {
            JanelaVM.Demo = demoAntes;
            App.Config.Idioma = anterior;
        }
    });

    [Fact]
    public Task ConfiguracoesMostraOSeletorEmPortuguesEEmIngles() => Sessao.Rodar(() =>
    {
        Idioma.Registrar(Traducao.Ingles.Pares);
        var anterior = App.Config.Idioma;
        var demoAntes = JanelaVM.Demo;
        try
        {
            App.Config.Idioma = "pt";
            JanelaVM.Demo = true;
            foreach (var (codigo, rotulo) in new[] { ("pt", "Idioma"), ("en", "Language") })
            {
                Idioma.Definir(codigo);
                var janela = new Window { Width = 1300, Height = 800, Content = new ConfiguracoesView { DataContext = new ConfiguracoesVM(new JanelaVM()) } };
                janela.Show();
                Dispatcher.UIThread.RunJobs();
                janela.UpdateLayout();
                var textos = janela.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains(rotulo, textos);
                var caixas = janela.GetVisualDescendants().OfType<ComboBox>().Where(c => c.SelectedIndex >= 0 && c.ItemCount == 3 && c.Items.OfType<string>().Contains("English")).ToList();
                Assert.Single(caixas);
                Assert.Equal(1, caixas[0].SelectedIndex);
                janela.Close();
            }
        }
        finally
        {
            Idioma.Definir("pt");
            JanelaVM.Demo = demoAntes;
            App.Config.Idioma = anterior;
        }
    });
}
