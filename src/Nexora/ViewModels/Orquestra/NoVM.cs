using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.Core.Orquestra;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class NoVM(ChatVM chat, OrquestraVM dono) : ObservableObject
{
    public ChatVM Chat { get; } = chat;
    public OrquestraVM Dono { get; } = dono;
    public bool EhChefe => Chat.EhChefe;

    [ObservableProperty]
    public partial double X { get; set; }

    [ObservableProperty]
    public partial double Y { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemGrupo))]
    public partial GrupoVM? Grupo { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DelegarCommand))]
    public partial string Recado { get; set; } = "";

    public bool TemGrupo => Grupo is not null;

    [RelayCommand]
    void Abrir() => Dono.Mostrar(Chat);

    bool PodeDelegar() => !string.IsNullOrWhiteSpace(Recado);

    [RelayCommand(CanExecute = nameof(PodeDelegar))]
    void Delegar()
    {
        Chat.EnviarTexto(Recado.Trim());
        Recado = "";
    }

    [RelayCommand]
    void Remover() => Dono.Remover(this);

    [RelayCommand]
    void AgruparEm(GrupoVM grupo) => Dono.Agrupar(this, grupo);

    [RelayCommand]
    void NovoGrupo() => Dono.NovoGrupo(this);

    [RelayCommand]
    void TirarDoGrupo() => Dono.Agrupar(this, null);
}
