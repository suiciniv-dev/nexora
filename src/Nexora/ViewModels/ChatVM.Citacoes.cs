using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    public ObservableCollection<CitadoVM> Citados { get; } = [];
    public bool TemCitados => Citados.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemCitacao))]
    public partial CitacaoVM? Citacao { get; set; }

    public bool TemCitacao => Citacao is not null;

    partial void OnRascunhoChanged(string value)
    {
        var achados = Mencoes.Arquivos(value ?? "");
        if (achados.SequenceEqual(Citados.Select(c => c.Caminho), StringComparer.OrdinalIgnoreCase)) return;
        Citados.Clear();
        foreach (var caminho in achados) Citados.Add(new CitadoVM(caminho, RemoverCitado));
        OnPropertyChanged(nameof(TemCitados));
    }

    void RemoverCitado(CitadoVM citado) => Rascunho = Mencoes.Remover(Rascunho, citado.Caminho);

    public void ResponderA(string trecho)
    {
        if (string.IsNullOrWhiteSpace(trecho)) return;
        Citacao = new CitacaoVM(trecho.Trim(), c =>
        {
            if (Citacao == c) Citacao = null;
        });
        PedirFoco?.Invoke();
    }

    string Citar(string texto)
    {
        if (Citacao is not { } citacao) return texto;
        Citacao = null;
        return Citacoes.Montar(citacao.Texto, texto);
    }
}
