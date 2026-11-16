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

public sealed partial class ChatVM
{
    public ObservableCollection<MensagemNaFilaVM> Fila { get; } = [];
    public bool TemFila => Fila.Count > 0;
    public string TextoFila => Fila.Count == 1 ? Idioma.T("NA FILA · SAI QUANDO O CLAUDE TERMINAR") : Idioma.F("NA FILA ({0}) · SAEM UMA POR VEZ QUANDO O CLAUDE TERMINAR", Fila.Count);

    bool DeveEnfileirar => Trabalhando && !SoNoFundo || Precisa;

    void IniciarFila() => Fila.CollectionChanged += (_, _) =>
    {
        OnPropertyChanged(nameof(TemFila));
        OnPropertyChanged(nameof(TextoFila));
    };

    void Enfileirar(string texto, List<AnexoVM> anexos)
    {
        Fila.Add(new MensagemNaFilaVM(texto, anexos, this));
        Rascunho = "";
        Anexos.Clear();
    }

    internal void EnviarDaFila(MensagemNaFilaVM item)
    {
        if (!Fila.Remove(item)) return;
        EnviarConteudo(item.Texto, [.. item.Anexos]);
    }

    internal void EditarDaFila(MensagemNaFilaVM item)
    {
        if (!Fila.Remove(item)) return;
        Rascunho = Rascunho.Trim().Length == 0 ? item.Texto : $"{item.Texto}\n{Rascunho}";
        foreach (var anexo in item.Anexos) Anexos.Add(anexo);
        PedirFoco?.Invoke();
    }

    void SoltarFilaDepoisDoTurno(bool erro)
    {
        if (erro || Fila.Count == 0) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (Fila.Count == 0 || DeveEnfileirar) return;
            var proxima = Fila[0];
            Fila.RemoveAt(0);
            EnviarConteudo(proxima.Texto, [.. proxima.Anexos]);
        }, DispatcherPriority.Background);
    }
}
