using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using System.Text.RegularExpressions;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ColunasIssuesVM : ObservableObject
{
    [ObservableProperty]
    public partial bool Quadro { get; set; }

    [ObservableProperty]
    public partial bool Prioridade { get; set; }

    [ObservableProperty]
    public partial bool Tamanho { get; set; }

    [ObservableProperty]
    public partial bool Iteracao { get; set; }

    [ObservableProperty]
    public partial bool Prs { get; set; }

    [ObservableProperty]
    public partial bool Atualizada { get; set; }

    double _largura = 2000;

    [ObservableProperty]
    public partial bool VerEtiquetas { get; set; } = true;

    [ObservableProperty]
    public partial bool VerPrs { get; set; }

    [ObservableProperty]
    public partial bool VerPrioridade { get; set; }

    [ObservableProperty]
    public partial bool VerTamanho { get; set; }

    [ObservableProperty]
    public partial bool VerIteracao { get; set; }

    public void Ajustar(double largura)
    {
        _largura = largura;
        Recalcular();
    }

    void Recalcular()
    {
        var sobra = _largura - 420 - 140 - (Quadro ? 120 : 0) - (Atualizada ? 90 : 0);
        bool Cabe(bool tem, double largura)
        {
            if (!tem || sobra < largura) return false;
            sobra -= largura;
            return true;
        }
        VerEtiquetas = Cabe(true, 170);
        VerPrs = Cabe(Prs, 90);
        VerPrioridade = Cabe(Prioridade, 90);
        VerTamanho = Cabe(Tamanho, 85);
        VerIteracao = Cabe(Iteracao, 100);
    }

    partial void OnQuadroChanged(bool value) => Recalcular();
    partial void OnPrioridadeChanged(bool value) => Recalcular();
    partial void OnTamanhoChanged(bool value) => Recalcular();
    partial void OnIteracaoChanged(bool value) => Recalcular();
    partial void OnPrsChanged(bool value) => Recalcular();
    partial void OnAtualizadaChanged(bool value) => Recalcular();
}
