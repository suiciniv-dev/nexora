using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class PerguntaVM : ObservableObject
{
    public PerguntaVM(JsonElement pergunta)
    {
        Texto = Protocolo.Texto(pergunta, "question") ?? "";
        Cabecalho = Protocolo.Texto(pergunta, "header");
        Multipla = Protocolo.Objeto(pergunta, "multiSelect").ValueKind == JsonValueKind.True;
        Opcoes = Protocolo.Itens(Protocolo.Objeto(pergunta, "options"))
            .Select(o => new OpcaoVM(Protocolo.Texto(o, "label") ?? "", Protocolo.Texto(o, "description"), Escolher))
            .ToList();
    }

    public string Texto { get; }
    public string? Cabecalho { get; }
    public bool Multipla { get; }
    public List<OpcaoVM> Opcoes { get; }

    public event Action? Mudou;

    [ObservableProperty]
    public partial string? Outra { get; set; }

    partial void OnOutraChanged(string? value) => Mudou?.Invoke();

    void Escolher(OpcaoVM escolhida)
    {
        if (!Multipla && escolhida.Selecionada)
            foreach (var o in Opcoes)
                if (o != escolhida) o.Selecionada = false;
        Mudou?.Invoke();
    }
}
