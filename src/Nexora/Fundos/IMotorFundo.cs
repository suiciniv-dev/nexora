using SkiaSharp;

namespace Nexora.Fundos;

public interface IMotorFundo : IDisposable
{
    int Fps { get; }
    void Quadro(SKCanvas tela, float largura, float altura, float dpr, double t, double dt, SKPoint? mouse);
    void Clique(float x, float y, double t);
    void Agito(double t, bool trabalhando);
}
