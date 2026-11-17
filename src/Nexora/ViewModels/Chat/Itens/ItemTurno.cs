using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ItemTurno(int numero, ItemUsuario mensagem, Action<ItemTurno> ver, Func<ItemTurno, Task> desfazer) : ItemChat
{
    public int Numero { get; } = numero;
    public ItemUsuario Mensagem { get; } = mensagem;
    public string? Antes { get; set; }
    public string? Depois { get; set; }
    public string? Transcricao { get; set; }
    public long Bytes { get; set; }
    public string? TurnoCodex { get; set; }
    public Task Marcacao { get; set; } = Task.CompletedTask;
    public int Seguintes { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visivel), nameof(TemArquivos), nameof(TextoArquivos), nameof(TextoMais), nameof(TextoMenos))]
    public partial Core.Git.ResumoDoTurno? Resumo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraAcoes))]
    public partial bool Confirmando { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraAcoes))]
    public partial bool Desfazendo { get; set; }

    public bool Visivel => Resumo is not null;
    public bool MostraAcoes => !Confirmando && !Desfazendo;
    public bool TemArquivos => Resumo is { Arquivos.Count: > 0 };
    public string TextoArquivos => Resumo switch
    {
        null => "",
        { Arquivos.Count: 0 } => Idioma.T("Nenhum arquivo mudou neste turno"),
        { Arquivos.Count: 1 } => Idioma.T("1 arquivo"),
        var r => Idioma.F("{0} arquivos", r.Arquivos.Count),
    };
    public string TextoMais => Resumo is { } r ? $"+{r.Mais}" : "";
    public string TextoMenos => Resumo is { } r ? $"−{r.Menos}" : "";
    public string TextoConfirmar => Seguintes == 0
        ? Idioma.T("Desfazer este turno? Os arquivos voltam a como estavam antes desta mensagem e a conversa volta para antes dela.")
        : Idioma.F("Desfazer este turno e os {0} seguintes? Os arquivos voltam a como estavam antes desta mensagem e a conversa volta para antes dela.", Seguintes);

    [RelayCommand]
    void Ver() => ver(this);

    [RelayCommand]
    void Desfazer()
    {
        OnPropertyChanged(nameof(TextoConfirmar));
        Confirmando = true;
    }

    [RelayCommand]
    void Cancelar() => Confirmando = false;

    [RelayCommand]
    Task Confirmar() => desfazer(this);
}
