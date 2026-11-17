using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Agenda;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class DiaDaAgendaVM(DayOfWeek dia, bool marcado) : ObservableObject
{
    public DayOfWeek Dia { get; } = dia;
    public string Rotulo { get; } = Agenda.Curto(dia);

    [ObservableProperty]
    public partial bool Marcado { get; set; } = marcado;
}
