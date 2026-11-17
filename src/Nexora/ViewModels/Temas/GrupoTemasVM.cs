using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;
using Nexora.Fundos;
using Nexora.Temas;
using TextMateSharp.Grammars;

namespace Nexora.ViewModels;

public sealed class GrupoTemasVM(string titulo, string descricao, IEnumerable<TemaCartaoVM> cartoes)
{
    public string Titulo { get; } = titulo;
    public string Descricao { get; } = descricao;
    public IReadOnlyList<TemaCartaoVM> Cartoes { get; } = cartoes.ToList();
}
