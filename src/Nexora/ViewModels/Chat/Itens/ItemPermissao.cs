using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ItemPermissao : ItemChat
{
    readonly Action<ItemPermissao, Decisao> _decidir;

    public ItemPermissao(PermissaoPedida pedido, string? pasta, Action<ItemPermissao, Decisao> decidir)
    {
        Pedido = pedido;
        _decidir = decidir;
        Detalhe = Resumos.DetalheDaPermissao(pedido.Ferramenta, pedido.Entrada, pasta);
        Motivo = pedido.Motivo;
        switch (pedido.Ferramenta)
        {
            case "ExitPlanMode":
                Titulo = Idioma.T("O plano está pronto. Posso seguir?");
                Plano = Protocolo.Texto(pedido.Entrada, "plan");
                RotuloPermitir = Idioma.T("Aprovar o plano");
                RotuloNegar = Idioma.T("Continuar planejando");
                Motivo = null;
                break;
            case "AskUserQuestion":
                Titulo = Idioma.T("O agente tem uma pergunta");
                Perguntas = Protocolo.Itens(Protocolo.Objeto(pedido.Entrada, "questions")).Select(p => new PerguntaVM(p)).ToList();
                Motivo = null;
                foreach (var p in Perguntas)
                {
                    p.Mudou += () => OnPropertyChanged(nameof(PodeResponder));
                    if (RespostaRapida)
                        foreach (var o in p.Opcoes) o.Rapida = escolhida =>
                        {
                            escolhida.Selecionada = true;
                            Responder();
                        };
                }
                break;
            default:
                Titulo = Idioma.F("Permitir {0}", pedido.Ferramenta) + (pedido.Descricao is { Length: > 0 } d && d != Detalhe ? $": {Resumos.UmaLinha(d, 80)}" : "?");
                break;
        }
    }

    public PermissaoPedida Pedido { get; }
    public string? Subagente { get; init; }
    public bool TemSubagente => Subagente is { Length: > 0 };
    public string Titulo { get; }
    public string? Detalhe { get; }
    public string? Motivo { get; }
    public string? Plano { get; }
    public List<PerguntaVM> Perguntas { get; } = [];
    public string RotuloPermitir { get; } = Idioma.T("Permitir");
    public string RotuloNegar { get; } = Idioma.T("Negar");

    public bool TemDetalhe => !string.IsNullOrWhiteSpace(Detalhe);
    public bool TemMotivo => !string.IsNullOrWhiteSpace(Motivo);
    public bool TemPlano => !string.IsNullOrWhiteSpace(Plano);
    public bool EhPergunta => Perguntas.Count > 0;
    public bool MostraBotoes => Pendente && !EhPergunta;
    public bool MostraResponder => Pendente && EhPergunta;
    public bool RespostaRapida => Perguntas.Count == 1 && !Perguntas[0].Multipla && Perguntas[0].Opcoes.Count > 0;
    public bool PrecisaBotaoResponder => !RespostaRapida;
    public bool PodeResponder => Pendente && Perguntas.All(p => p.Opcoes.Any(o => o.Selecionada) || !string.IsNullOrWhiteSpace(p.Outra));
    public bool PodeSempre => Pedido.Sugestoes is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pendente), nameof(MostraBotoes), nameof(MostraResponder), nameof(PodeResponder))]
    public partial bool Respondida { get; set; }

    [ObservableProperty]
    public partial string? Resposta { get; set; }

    public bool Pendente => !Respondida;

    public JsonElement? Respostas()
    {
        var respostas = new Dictionary<string, string>();
        foreach (var p in Perguntas)
        {
            var escolhidas = p.Opcoes.Where(o => o.Selecionada).Select(o => o.Rotulo).ToList();
            if (!string.IsNullOrWhiteSpace(p.Outra)) escolhidas.Add(p.Outra.Trim());
            if (escolhidas.Count == 0) return null;
            respostas[p.Texto] = string.Join(", ", escolhidas);
        }
        return JsonSerializer.SerializeToElement(respostas);
    }

    public void Encerrar(string resposta)
    {
        Respondida = true;
        Resposta = resposta;
    }

    [RelayCommand]
    void Permitir() => _decidir(this, Decisao.Permitir);

    [RelayCommand]
    void PermitirSempre() => _decidir(this, Decisao.PermitirSempre);

    [RelayCommand]
    void PermitirEmTodas() => _decidir(this, Decisao.PermitirEmTodas);

    [RelayCommand]
    void Negar() => _decidir(this, Decisao.Negar);

    [RelayCommand]
    void Responder() => _decidir(this, Decisao.Responder);
}
