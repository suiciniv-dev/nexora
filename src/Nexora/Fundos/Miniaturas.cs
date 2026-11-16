using System.Diagnostics;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Nexora.Fundos;

public static class Miniaturas
{
    static readonly Dictionary<string, Task<Bitmap?>> Prontas = [];
    static readonly SemaphoreSlim Fila = new(1);

    public static Task<Bitmap?> Obter(string papel)
    {
        lock (Prontas)
        {
            if (!Prontas.TryGetValue(papel, out var tarefa)) Prontas[papel] = tarefa = Task.Run(() => Desenhar(papel));
            return tarefa;
        }
    }

    static async Task<Bitmap?> Desenhar(string papel)
    {
        await Fila.WaitAsync();
        try
        {
            const int largura = 720, altura = 396;
            const float escala = 4 / 3f;
            using var motor = Motores.Criar(papel);
            using (var vazio = new SKNoDrawCanvas((int)(largura * escala), (int)(altura * escala)))
                for (var q = 0; q < 72; q++) motor.Quadro(vazio, largura, altura, escala, q / 24.0, 1 / 24.0, null);
            using var superficie = SKSurface.Create(new SKImageInfo((int)(largura * escala), (int)(altura * escala)));
            motor.Quadro(superficie.Canvas, largura, altura, escala, 3, 1 / 24.0, null);
            using var imagem = superficie.Snapshot();
            using var dados = imagem.Encode(SKEncodedImageFormat.Png, 100);
            using var fluxo = dados.AsStream();
            return Bitmap.DecodeToWidth(fluxo, 480, BitmapInterpolationMode.HighQuality);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Trace.WriteLine($"{papel}: {ex}");
            return null;
        }
        finally
        {
            Fila.Release();
        }
    }
}
