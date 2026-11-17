using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.Core.Provedores;
using Nexora.Core.Texto;
using Nexora.Temas;

namespace Nexora.ViewModels;

public sealed partial class RequisitoVM(string nome, string uso, string comoInstalar, Receita receita) : ObservableObject
{
    public string Nome { get; } = nome;
    public string Uso { get; } = uso;
    public string ComoInstalar { get; } = comoInstalar;
    public Receita Receita { get; } = receita;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Falta))]
    public partial bool Ok { get; set; }

    [ObservableProperty]
    public partial string Detalhe { get; set; } = "";

    public bool Falta => !Ok;
}
