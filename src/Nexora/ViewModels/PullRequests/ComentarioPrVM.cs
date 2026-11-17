using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ComentarioPrVM(ComentarioDoPr dados, bool meu, Action? aoMarcar = null) : ObservableObject
{
    public ComentarioDoPr Dados { get; } = dados;
    public string Autor => Dados.Autor;
    public string Corpo { get; } = PainelPrVM.Resumir(dados.Corpo, 600);
    public string Quando => Dados.Quando is { } q ? Core.Texto.Tempo.Relativo(q.LocalDateTime) : "";
    public bool Meu { get; } = meu;

    [ObservableProperty]
    public partial bool Incluir { get; set; } = !meu;

    partial void OnIncluirChanged(bool value) => aoMarcar?.Invoke();
}
