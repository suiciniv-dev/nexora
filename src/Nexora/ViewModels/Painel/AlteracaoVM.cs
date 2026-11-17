using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Arquivos;
using Nexora.Core.Git;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class AlteracaoVM(Alteracao dados, string pasta, Action<AlteracaoVM> abrir, ItemTurno? turno = null, ArquivoDoTurno? linhas = null) : ObservableObject
{
    public Alteracao Dados { get; } = dados;
    public string Pasta { get; } = pasta;
    public ItemTurno? Turno { get; } = turno;
    public bool TemLinhas => linhas is { Binario: false };
    public string TextoMais => linhas is { } l ? $"+{l.Mais}" : "";
    public string TextoMenos => linhas is { Menos: > 0 } l ? $"−{l.Menos}" : "";
    public string Nome => Path.GetFileName(Dados.Caminho);
    public string Diretorio => Path.GetDirectoryName(Dados.Caminho)?.Replace('\\', '/') ?? "";
    public string Rotulo => Dados.Rotulo;
    public bool Nova => Dados.RotuloNovo;
    public bool Apagada => Dados.RotuloApagado;

    [RelayCommand]
    void Abrir() => abrir(this);
}
