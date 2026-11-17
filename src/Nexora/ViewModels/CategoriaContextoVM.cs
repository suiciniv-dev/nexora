using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed class CategoriaContextoVM(string nome, long tokens, double fracao, string cor)
{
    public string Nome { get; } = nome;
    public string Tokens { get; } = tokens >= 1000 ? $"{(tokens / 1000.0).ToString("0.#", Idioma.Cultura)}k" : tokens.ToString();
    public string Percentual { get; } = $"{(fracao * 100).ToString("0.#", Idioma.Cultura)}%";
    public Avalonia.Media.IBrush Cor { get; } = Avalonia.Media.Brush.Parse(cor);
}
