using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class GrupoConversasVM(string arquivo, IReadOnlyList<ConversaPrVM> conversas, IReadOnlyList<ConversaNaLinhaVM> resolvidas, Action<string> verDiff)
{
    public string Arquivo { get; } = arquivo;
    public IReadOnlyList<ConversaPrVM> Conversas { get; } = conversas;
    public IReadOnlyList<ConversaNaLinhaVM> Resolvidas { get; } = resolvidas;
    public IReadOnlyList<ConversaNaLinhaVM> ResolvidasVisiveis => [.. Resolvidas.Take(3)];
    public bool TemResolvidas => Resolvidas.Count > 0;
    public bool TemMaisResolvidas => Resolvidas.Count > 3;
    public string MaisResolvidas => $"+{Resolvidas.Count - 3}";
    public string TextoResolvidas => Resolvidas.Count == 1 ? Idioma.T("1 resolvida") : Idioma.F("{0} resolvidas", Resolvidas.Count);
    public string Nome => Path.GetFileName(Arquivo);
    public string Pasta => Path.GetDirectoryName(Arquivo)?.Replace('\\', '/') ?? "";
    public bool TemPasta => Pasta.Length > 0;

    [RelayCommand]
    void VerDiff() => verDiff(Arquivo);
}
