namespace Nexora.Core.Acp;

public static class TextosAcp
{
    public const string NaoAbriu = "O agente não abriu.";
    public const string NaoRespondeu = "O agente não respondeu a tempo.";
    public const string Recusou = "O agente recusou o pedido.";
    public const string Fechou = "O agente fechou.";
    public const string SemConversa = "O agente não devolveu a conversa.";
    public const string SemComando = "Este agente não tem esse comando.";
    public const string SoClaude = "O Remote Control é só do Claude Code.";
    public const string FacaLogin = "O {0} pediu login. Abra um terminal, entre no {0} por lá e tente de novo.";
    public const string DisseOAgente = "O {0} respondeu: {1}";
    public const string AgenteAntigo = "O {0} instalado é antigo e não tem o modo que o Nexora usa. Atualize o {0} e tente de novo.";
    public const string AbraOLink = "Para entrar no {0}, abra este link no navegador: {1}";
    public const string NaoReabriu = "O {0} não consegue reabrir conversas antigas. Esta é uma conversa nova.";
    public const string SemImagens = "O {0} não aceita imagens. Elas ficaram de fora da mensagem.";
    public const string Recusa = "O agente se recusou a continuar.";
    public const string LimiteDeTokens = "O agente parou porque chegou ao limite de tokens da resposta.";
    public const string LimiteDePassos = "O agente parou porque chegou ao limite de passos do turno.";
    public const string NaoTerminou = "O agente não conseguiu terminar o turno.";
    public const string NaoResponde = "O Nexora ainda não responde a {0}.";
    public const string ForaDaPasta = "O arquivo {0} está fora das pastas desta sessão.";
    public const string NaoLeu = "Não deu para ler {0}: {1}";

    public static IReadOnlyList<(string Pt, string En)> Pares { get; } =
    [
        (NaoAbriu, "The agent did not start."),
        (NaoRespondeu, "The agent did not answer in time."),
        (Recusou, "The agent refused the request."),
        (Fechou, "The agent closed."),
        (SemConversa, "The agent did not return the conversation."),
        (SemComando, "This agent does not have that command."),
        (SoClaude, "Remote Control is only for Claude Code."),
        (FacaLogin, "{0} asked for a login. Open a terminal, sign in to {0} there and try again."),
        (DisseOAgente, "{0} answered: {1}"),
        (AgenteAntigo, "The installed {0} is too old and lacks the mode Nexora uses. Update {0} and try again."),
        (AbraOLink, "To sign in to {0}, open this link in the browser: {1}"),
        (NaoReabriu, "{0} cannot reopen old conversations. This is a new conversation."),
        (SemImagens, "{0} does not accept images. They were left out of the message."),
        (Recusa, "The agent refused to continue."),
        (LimiteDeTokens, "The agent stopped because it reached the response token limit."),
        (LimiteDePassos, "The agent stopped because it reached the step limit for the turn."),
        (NaoTerminou, "The agent could not finish the turn."),
        (NaoResponde, "Nexora does not answer {0} yet."),
        (ForaDaPasta, "The file {0} is outside the folders of this session."),
        (NaoLeu, "Could not read {0}: {1}"),
    ];
}
