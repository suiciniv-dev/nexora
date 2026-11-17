using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Nexora.Core.Texto;

namespace Nexora.Controles;

public sealed record SerieDoGrafico(string Nome, IReadOnlyList<double> Valores, IBrush Cor);
