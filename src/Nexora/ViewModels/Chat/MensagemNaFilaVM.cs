using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class MensagemNaFilaVM(string texto, IReadOnlyList<AnexoVM> anexos, ChatVM chat) : ObservableObject
{
    public string Texto { get; } = texto;
    public IReadOnlyList<AnexoVM> Anexos { get; } = anexos;
    public string Resumo => Texto.Length > 0 ? Resumos.UmaLinha(Texto, 140) : Anexos.Count == 1 ? Idioma.T("1 imagem") : Idioma.F("{0} imagens", Anexos.Count);
    public bool TemImagens => Anexos.Count > 0 && Texto.Length > 0;
    public string TextoImagens => Anexos.Count == 1 ? Idioma.T("+1 imagem") : Idioma.F("+{0} imagens", Anexos.Count);

    [RelayCommand]
    void EnviarAgora() => chat.EnviarDaFila(this);

    [RelayCommand]
    void Editar() => chat.EditarDaFila(this);

    [RelayCommand]
    void Remover() => chat.Fila.Remove(this);
}
