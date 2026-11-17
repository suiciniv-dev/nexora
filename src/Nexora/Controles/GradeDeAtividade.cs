using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Nexora.Core.Texto;

namespace Nexora.Controles;

public sealed class GradeDeAtividade : DesenhoGuardado
{
    public static readonly StyledProperty<IReadOnlyList<int>?> NiveisProperty = AvaloniaProperty.Register<GradeDeAtividade, IReadOnlyList<int>?>(nameof(Niveis));
    public static readonly StyledProperty<DateTime> InicioProperty = AvaloniaProperty.Register<GradeDeAtividade, DateTime>(nameof(Inicio));
    public static readonly StyledProperty<IBrush?> CorProperty = AvaloniaProperty.Register<GradeDeAtividade, IBrush?>(nameof(Cor));
    public static readonly StyledProperty<IBrush?> VazioProperty = AvaloniaProperty.Register<GradeDeAtividade, IBrush?>(nameof(Vazio));
    public static readonly StyledProperty<IBrush?> RotuloProperty = AvaloniaProperty.Register<GradeDeAtividade, IBrush?>(nameof(Rotulo));
    const double Espaco = 3, Margem = 30, Topo = 18, MaximoDaCelula = 13;
    static readonly double[] Opacidades = [0, 0.32, 0.55, 0.78, 1];

    static GradeDeAtividade()
    {
        AffectsRender<GradeDeAtividade>(NiveisProperty, InicioProperty, CorProperty, VazioProperty, RotuloProperty);
        AffectsMeasure<GradeDeAtividade>(NiveisProperty);
    }

    public IReadOnlyList<int>? Niveis { get => GetValue(NiveisProperty); set => SetValue(NiveisProperty, value); }
    public DateTime Inicio { get => GetValue(InicioProperty); set => SetValue(InicioProperty, value); }
    public IBrush? Cor { get => GetValue(CorProperty); set => SetValue(CorProperty, value); }
    public IBrush? Vazio { get => GetValue(VazioProperty); set => SetValue(VazioProperty, value); }
    public IBrush? Rotulo { get => GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }

    int Colunas => Math.Max(1, (int)Math.Ceiling(((Niveis?.Count ?? 0) + (int)Inicio.DayOfWeek) / 7.0));

    public static double Celula(double largura, int colunas) =>
        Math.Clamp((largura - Margem - Espaco * (colunas - 1)) / colunas, 4, MaximoDaCelula);

    protected override Size MeasureOverride(Size disponivel)
    {
        var largura = double.IsInfinity(disponivel.Width) ? 700 : disponivel.Width;
        var celula = Celula(largura, Colunas);
        return new Size(largura, Topo + 7 * (celula + Espaco) - Espaco);
    }

    protected override void Pintar(DrawingContext contexto, Size tamanho)
    {
        if (Niveis is not { Count: > 0 } niveis) return;
        var colunas = Colunas;
        var celula = Celula(tamanho.Width, colunas);
        var passo = celula + Espaco;
        var cor = (Cor as ISolidColorBrush)?.Color ?? Colors.LimeGreen;
        var tintas = Opacidades.Select(o => (IBrush)new SolidColorBrush(cor, o)).ToArray();
        var deslocamento = (int)Inicio.DayOfWeek;
        var raio = Math.Max(1.5, celula * 0.22);
        for (var i = 0; i < niveis.Count; i++)
        {
            var posicao = i + deslocamento;
            var x = Margem + posicao / 7 * passo;
            var y = Topo + posicao % 7 * passo;
            var nivel = Math.Clamp(niveis[i], 0, 4);
            var tinta = nivel > 0 ? tintas[nivel] : Vazio;
            if (tinta is not null) contexto.DrawRectangle(tinta, null, new RoundedRect(new Rect(x, y, celula, celula), raio));
        }
    }

    protected override void PintarPorCima(DrawingContext contexto, Size tamanho)
    {
        if (Niveis is not { Count: > 0 }) return;
        var colunas = Colunas;
        var celula = Celula(tamanho.Width, colunas);
        var passo = celula + Espaco;
        var deslocamento = (int)Inicio.DayOfWeek;
        var ultimoMes = -1;
        for (var coluna = 0; coluna < colunas; coluna++)
        {
            var dia = Inicio.AddDays(coluna * 7 - deslocamento);
            var primeiro = dia.AddDays(Math.Max(0, deslocamento - coluna * 7));
            if (primeiro.Month == ultimoMes) continue;
            ultimoMes = primeiro.Month;
            if (coluna > colunas - 2 && coluna > 0) continue;
            if (coluna == 0 && primeiro.Day > 14) continue;
            var mes = primeiro.ToString("MMM", Idioma.Cultura).TrimEnd('.');
            contexto.DrawText(Rotulo(char.ToUpper(mes[0], Idioma.Cultura) + mes[1..], Rotulo), NoPixel(Margem + coluna * passo, 0));
        }
        foreach (var (linha, nome) in new[] { (1, Idioma.T("Seg")), (3, Idioma.T("Qua")), (5, Idioma.T("Sex")) })
        {
            var texto = Rotulo(nome, Rotulo);
            contexto.DrawText(texto, NoPixel(0, Topo + linha * passo + (celula - texto.Height) / 2));
        }
    }
}
