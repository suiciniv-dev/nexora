using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Historico;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ConversaVM(ConversaSalva dados, Action<ConversaVM> abrir, Action<ConversaVM>? fixar = null, Action<ConversaVM>? split = null) : ObservableObject
{
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    void AbrirEmSplit() => split?.Invoke(this);

    public ConversaSalva Dados { get; } = dados;
    public string Titulo => Dados.Titulo;
    public string Quando => Tempo.Relativo(Dados.Atualizada);
    public string Pasta => Dados.Pasta is { } p ? Path.GetFileName(p.TrimEnd('\\', '/')) : "?";

    public string? NomeDoAgente => Dados.Agente is { } a && a != Core.Claude.Agentes.Claude ? Core.Claude.Agentes.Nome(a) : null;

    public string Local => (Dados.Branch is { Length: > 0 } b ? $"{Pasta} · {b}" : Pasta) + (NomeDoAgente is { } nome ? $" · {nome}" : "");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoFixar))]
    public partial bool Fixada { get; set; }

    public string TextoFixar => Fixada ? Idioma.T("Desafixar") : Idioma.T("Fixar no topo");

    public bool Combina(string busca) =>
        busca.Length == 0
        || Titulo.Contains(busca, StringComparison.CurrentCultureIgnoreCase)
        || (Dados.Pasta?.Contains(busca, StringComparison.CurrentCultureIgnoreCase) ?? false)
        || (Dados.Branch?.Contains(busca, StringComparison.CurrentCultureIgnoreCase) ?? false)
        || (NomeDoAgente?.Contains(busca, StringComparison.CurrentCultureIgnoreCase) ?? false);

    [RelayCommand]
    void Abrir() => abrir(this);

    [RelayCommand]
    void AlternarFixada() => fixar?.Invoke(this);
}
