using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace Nexora.Aplicativo;

static class Foto
{
    public static void Agendar(Window janela, string caminho, IClassicDesktopStyleApplicationLifetime vida, int espera)
    {
        janela.Opened += async (_, _) =>
        {
            await Task.Delay(espera);
            await SalvarAsync(janela, caminho);
            var outras = vida.Windows.Where(w => w != janela).ToList();
            for (var i = 0; i < outras.Count; i++)
            {
                var destino = Path.ChangeExtension(caminho, null) + $"-{i + 2}" + Path.GetExtension(caminho);
#if !MAC
                if (outras[i] is Apresentacao apresentacao && await apresentacao.FotografarAsync(destino)) continue;
#endif
                await SalvarAsync(outras[i], destino);
            }
            vida.Shutdown();
        };
    }

    static async Task SalvarAsync(Window janela, string caminho)
    {
        var escala = janela.RenderScaling;
        var tamanho = new PixelSize((int)(janela.Bounds.Width * escala), (int)(janela.Bounds.Height * escala));
        using var imagem = new RenderTargetBitmap(tamanho, new Vector(96 * escala, 96 * escala));
        Visual raiz = janela;
        while (raiz.GetVisualParent() is { } pai) raiz = pai;
        var paginas = new List<(byte[] Png, Rect Area)>();
        foreach (var navegador in Views.NavegadorView.Abertas())
            if (TopLevel.GetTopLevel(navegador) == janela && await navegador.FotografarAsync(raiz) is { } pagina) paginas.Add(pagina);
        imagem.Render(raiz);
        if (paginas.Count > 0)
            using (var desenho = imagem.CreateDrawingContext(false))
                foreach (var (png, area) in paginas)
                {
                    using var bitmap = new Bitmap(new MemoryStream(png));
                    desenho.DrawImage(bitmap, area);
                }
        imagem.Save(caminho, PngBitmapEncoderOptions.Default);
        if (Environment.GetEnvironmentVariable("NEXORA_ARVORE") == "1")
        {
            var linhas = new List<string>();
            void Descer(Visual v, int nivel)
            {
                if (v.TransformToVisual(raiz) is { } m)
                {
                    var r = new Rect(v.Bounds.Size).TransformToAABB(m);
                    var extra = v is Avalonia.Layout.Layoutable l ? $" margem={l.Margin} alinh={l.HorizontalAlignment} largura={l.Width}" : "";
                    if (v is StackPanel sp) extra += $" espaco={sp.Spacing}";
                    if (v is Decorator d) extra += $" padding={d.Padding}";
                    if (v is Avalonia.Controls.Primitives.TemplatedControl tc) extra += $" padding={tc.Padding}";
                    linhas.Add($"{new string(' ', nivel * 2)}{v.GetType().Name} {(v as StyledElement)?.Name} [{r.X:0},{r.Y:0} {r.Width:0}x{r.Height:0}] visivel={v.IsVisible}{extra}");
                }
                foreach (var filho in v.GetVisualChildren()) Descer(filho, nivel + 1);
            }
            Descer(raiz, 0);
            File.WriteAllLines(Path.ChangeExtension(caminho, ".txt"), linhas);
        }
    }
}
