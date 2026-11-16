using System.Diagnostics;
using Nexora.Fundos;
using SkiaSharp;
using Xunit.Abstractions;

namespace Nexora.Testes;

public sealed class FundosTestes(ITestOutputHelper saida)
{
    public static TheoryData<string> Todos => new(Papeis.Todos.Select(p => p.Id));

    [Theory]
    [MemberData(nameof(Todos))]
    public void CadaFundoDesenhaComMouseECliqueSemErro(string id)
    {
        var pasta = Environment.GetEnvironmentVariable("NEXORA_FUNDOS_PASTA");
        int largura = pasta is null ? 480 : 1280, altura = pasta is null ? 300 : 800;
        using var motor = Motores.Criar(id);
        using var superficie = SKSurface.Create(new SKImageInfo(largura, altura));
        var relogio = new Stopwatch();
        const int quadros = 48;
        for (var q = 0; q < quadros; q++)
        {
            var t = 2 + q / 12.0;
            if (q == 6) motor.Clique(largura / 2f, altura / 2f, t);
            motor.Agito(t, trabalhando: q == 10);
            superficie.Canvas.Clear(SKColors.Transparent);
            superficie.Canvas.ResetMatrix();
            relogio.Start();
            motor.Quadro(superficie.Canvas, largura, altura, 1, t, 1 / 12.0, q is > 30 and < 40 ? new SKPoint(largura * 0.4f + q * 6, altura * 0.5f) : null);
            relogio.Stop();
        }
        saida.WriteLine($"{id}: {relogio.Elapsed.TotalMilliseconds / quadros:0.0} ms por quadro em {largura}x{altura}");

        using var imagem = superficie.Snapshot();
        using var pixels = imagem.PeekPixels();
        var cores = new HashSet<uint>();
        for (var y = 0; y < altura; y += 7)
            for (var x = 0; x < largura; x += 7)
                cores.Add((uint)pixels.GetPixelColor(x, y));
        Assert.True(cores.Count > 20, $"{id} desenhou só {cores.Count} cores");

        motor.Quadro(superficie.Canvas, largura * 0.7f, altura * 0.6f, 1, 7, 1 / 12.0, null);
        motor.Quadro(superficie.Canvas, largura, altura, 1, 7.1, 1 / 12.0, null);

        if (pasta is null) return;
        Directory.CreateDirectory(pasta);
        using var png = imagem.Encode(SKEncodedImageFormat.Png, 90);
        File.WriteAllBytes(Path.Combine(pasta, id.Replace(':', '-') + ".png"), png.ToArray());
    }
}
