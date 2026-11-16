namespace Nexora.Traducao;

public static partial class Ingles
{
    static readonly (string Pt, string En)[] Chat =
    [
        ("Esforço: {0}", "Effort: {0}"),
        ("Esforço: padrão", "Effort: default"),
        ("Modelo trocado para {0}", "Model switched to {0}"),
        ("Modelo trocado para o padrão ({0})", "Model switched to the default ({0})"),
        ("Trocar de modelo agora relê ~{0} tokens sem cache: o cache do prompt é de cada modelo, e a conversa inteira entra de novo como nova.",
            "Switching models now rereads ~{0} tokens without cache: each model has its own prompt cache, so the whole conversation goes in again as new."),
        ("Sessão nova nesta conversa: o agente esquece o que veio antes e o histórico continua visível",
            "New session in this chat: the agent forgets what came before and the history stays visible"),
        ("A conversa começou uma sessão nova.", "The chat started a new session."),
        ("Nova sessão — o agente não lembra do que veio antes", "New session — the agent doesn't remember what came before"),
        ("Nova sessão nesta conversa", "New session in this chat"),
        ("Tentando de novo…", "Trying again…"),
        ("Tentar de novo", "Try again"),
        ("Subagent do Codex", "Codex subagent"),
        ("Esperando você permitir {0}", "Waiting for you to allow {0}"),
        ("Este pedido veio de um subagent desta conversa", "This request came from a subagent of this chat"),
        ("Respondendo a", "Replying to"),
        ("Responder a esta mensagem. Com um trecho selecionado, cita só o trecho", "Reply to this message. With some text selected, quotes only that part"),
        ("Responder a esta mensagem", "Reply to this message"),
        ("Roda os testes de concorrência", "Runs the concurrency tests"),
        ("O teste novo cobre a nota editada nos dois lados.", "The new test covers a note edited on both sides."),
    ];
}
