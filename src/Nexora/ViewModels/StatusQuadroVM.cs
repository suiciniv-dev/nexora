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

public sealed partial class StatusQuadroVM : ObservableObject
{
    readonly Func<StatusQuadroVM, OpcaoStatus, Task> _mudar;
    bool _voltando;

    public StatusQuadroVM(StatusNoQuadro dados, Func<StatusQuadroVM, OpcaoStatus, Task> mudar)
    {
        Dados = dados;
        _mudar = mudar;
        _voltando = true;
        Escolhida = dados.Opcoes.FirstOrDefault(o => o.Id == dados.AtualId);
        _voltando = false;
    }

    public StatusNoQuadro Dados { get; private set; }
    public string Rotulo => Idioma.F("Status em {0}", Dados.Quadro);
    public IReadOnlyList<OpcaoStatus> Opcoes => Dados.Opcoes;

    [ObservableProperty]
    public partial OpcaoStatus? Escolhida { get; set; }

    [ObservableProperty]
    public partial bool Salvando { get; set; }

    partial void OnEscolhidaChanged(OpcaoStatus? value)
    {
        if (_voltando || value is null || value.Id == Dados.AtualId) return;
        _ = _mudar(this, value);
    }

    public void Confirmar(string opcaoId) => Dados = Dados with { AtualId = opcaoId };

    public void Voltar()
    {
        _voltando = true;
        Escolhida = Dados.Opcoes.FirstOrDefault(o => o.Id == Dados.AtualId);
        _voltando = false;
    }
}
