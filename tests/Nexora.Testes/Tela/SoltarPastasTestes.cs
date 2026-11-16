using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class SoltarPastasTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-soltar-").FullName;
    readonly string _sufixo = "-" + Guid.NewGuid().ToString("N")[..6];

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
        if (!Directory.Exists(JanelaVM.PastaDeProjetos)) return;
        foreach (var arquivo in Directory.EnumerateFiles(JanelaVM.PastaDeProjetos, $"*{_sufixo}*.code-workspace"))
            try { File.Delete(arquivo); } catch (IOException) { }
    }

    string Criar(string nome)
    {
        var caminho = Path.Combine(_pasta, nome + _sufixo);
        Directory.CreateDirectory(caminho);
        return caminho;
    }

    [Fact]
    public void RecadoContaNovosExistentesEArquivos()
    {
        Assert.Equal("Loja virou projeto.", JanelaVM.Recado(["Loja"], [], 0, []));
        Assert.Equal("2 pastas viraram projetos. Site já era um projeto. 1 arquivo ignorado: só pastas viram projeto.", JanelaVM.Recado(["A", "B"], ["Site"], 1, []));
        Assert.Equal("3 arquivos ignorados: só pastas viram projeto.", JanelaVM.Recado([], [], 3, []));
    }

    [Fact]
    public Task SoltarPastaViraProjetoEAbreEPastaRepetidaSoDestaca() => Sessao.Rodar(() =>
    {
        var loja = Criar("Loja");
        var site = Criar("Site");
        var arquivo = Path.Combine(_pasta, "notas.txt");
        File.WriteAllText(arquivo, "oi");
        var janela = new JanelaVM();

        janela.SoltarPastas([loja]);
        var ambiente = Assert.Single(janela.Ambientes, a => a.Principal.Equals(loja, StringComparison.OrdinalIgnoreCase));
        Assert.Equal($"Loja{_sufixo} virou projeto.", janela.RecadoSoltar);
        Assert.Same(ambiente, janela.Principal.Conteudo);

        var antes = janela.Ambientes.Count;
        janela.SoltarPastas([loja, site, arquivo]);
        Assert.Equal(antes + 1, janela.Ambientes.Count);
        Assert.Contains($"Loja{_sufixo} já era um projeto.", janela.RecadoSoltar);
        Assert.Contains($"Site{_sufixo} virou projeto.", janela.RecadoSoltar);
        Assert.Contains("1 arquivo ignorado", janela.RecadoSoltar);
        Assert.True(janela.NaProjetos);
        Assert.True(janela.Destacado(ambiente));
        Assert.Contains(janela.PaginaProjetos.Cartoes, c => c.Destacado && c.Nome == $"Site{_sufixo}");

        janela.SoltarPastas([arquivo]);
        Assert.Equal("1 arquivo ignorado: só pastas viram projeto.", janela.RecadoSoltar);
        Assert.Equal(antes + 1, janela.Ambientes.Count);
    });

    [Fact]
    public Task ArrastarPastaSobreALateralMostraORealceESoltarAdiciona() => Sessao.Rodar(async () =>
    {
        var pasta = Criar("Arrastada");
        var demoAntes = JanelaVM.Demo;
        try
        {
            JanelaVM.Demo = true;
            Nexora.Temas.Catalogo.Aplicar(Nexora.Temas.Catalogo.Atual);
            var vm = new JanelaVM();
            var janela = new Janela { DataContext = vm, Width = 1440, Height = 900 };
            janela.Show();
            vm.LateralAberta = true;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var lateral = janela.GetVisualDescendants().OfType<LateralView>().First(l => l.IsEffectivelyVisible);
            var realce = lateral.GetVisualDescendants().OfType<Border>().First(b => b.Name == "RealceSoltar");
            var centro = lateral.TranslatePoint(new Point(lateral.Bounds.Width / 2, lateral.Bounds.Height / 2), janela)!.Value;
            var item = await janela.StorageProvider.TryGetFolderFromPathAsync(new Uri(pasta));
            Assert.NotNull(item);
            var dados = new DataTransfer();
            dados.Add(DataTransferItem.CreateFile(item));

            janela.DragDrop(centro, RawDragEventType.DragEnter, dados, DragDropEffects.Copy, RawInputModifiers.None);
            janela.DragDrop(centro, RawDragEventType.DragOver, dados, DragDropEffects.Copy, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(realce.IsVisible);
            var foto = Environment.GetEnvironmentVariable("NEXORA_FOTOS");
            if (!string.IsNullOrEmpty(foto))
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                janela.CaptureRenderedFrame()?.Save(Path.Combine(foto, "lateral-arrastando.png"));
            }

            janela.DragDrop(centro, RawDragEventType.Drop, dados, DragDropEffects.Copy, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.False(realce.IsVisible);
            Assert.Contains(vm.Ambientes, a => a.Principal.Equals(pasta, StringComparison.OrdinalIgnoreCase));
            Assert.Equal($"Arrastada{_sufixo} virou projeto.", vm.RecadoSoltar);
            if (!string.IsNullOrEmpty(foto))
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                janela.CaptureRenderedFrame()?.Save(Path.Combine(foto, "lateral-solta.png"));
            }
            vm.SoltarPastas([pasta, Criar("Outra")]);
            Dispatcher.UIThread.RunJobs();
            Assert.True(vm.NaProjetos);
            if (!string.IsNullOrEmpty(foto))
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                janela.CaptureRenderedFrame()?.Save(Path.Combine(foto, "projetos-destacados.png"));
            }
            janela.Close();
        }
        finally
        {
            JanelaVM.Demo = demoAntes;
        }
    });
}
