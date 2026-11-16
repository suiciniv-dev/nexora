using Nexora.Core.Claude;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    static readonly string[] ComandosDeLimpar = ["/clear", "/reset", "/new"];

    public static bool EhComandoDeLimpar(string texto) =>
        ComandosDeLimpar.Contains(texto.Trim(), StringComparer.OrdinalIgnoreCase);

    static ComandoBarra ComandoDeLimpar => new("clear", Idioma.T("Sessão nova nesta conversa: o agente esquece o que veio antes e o histórico continua visível"), null);

    public void ComecarSessaoNova()
    {
        if (_sessao is null && SessaoId is null && Itens.Count == 0) return;
        var antiga = _sessao;
        _sessao = null;
        _reiniciar = false;
        EnderecoRemoto = null;
        DescarregarParcial();
        FecharPensamento();
        _textoAtual = null;
        foreach (var pendente in Itens.OfType<ItemPermissao>().Where(p => p.Pendente)) pendente.Encerrar(Idioma.T("A conversa começou uma sessão nova."));
        PermissaoPendente = null;
        foreach (var solta in _ferramentas.Values) solta.Concluir("", false);
        _ferramentas.Clear();
        ZerarSegundoPlano();
        FecharTurno();
        SessaoId = null;
        Bifurcar = false;
        Hibernada = false;
        ContextoTokens = null;
        UltimaResposta = null;
        CacheLonga = null;
        TurnoDesde = null;
        Adicionar(ItemAviso.NovaSessao(Idioma.T("Nova sessão — o agente não lembra do que veio antes")));
        Atividade(Idioma.T("Nova sessão nesta conversa"));
        Conversou();
        AtividadeEm = DateTime.Now;
        Estado = EstadoChat.Pronta;
        OnPropertyChanged(nameof(Pid));
        OnPropertyChanged(nameof(PodeTrocarLocal));
        if (antiga is not null) _ = antiga.DisposeAsync().AsTask();
        SoltarFilaDepoisDoTurno(false);
    }
}
