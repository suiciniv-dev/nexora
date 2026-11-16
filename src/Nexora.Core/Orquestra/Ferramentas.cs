using System.Text.Json.Nodes;

namespace Nexora.Core.Orquestra;

public sealed record FerramentaMcp(string Nome, string Descricao, JsonObject Parametros, IReadOnlyList<string> Obrigatorios, bool SoLeitura)
{
    public JsonObject ParaJson() => new()
    {
        ["name"] = Nome,
        ["description"] = Descricao,
        ["inputSchema"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = Parametros.DeepClone(),
            ["required"] = new JsonArray([.. Obrigatorios.Select(o => (JsonNode)o)]),
        },
        ["annotations"] = new JsonObject { ["readOnlyHint"] = SoLeitura },
    };
}

public static class Ferramentas
{
    public const string Prefixo = "mcp__" + ServidorMcp.Nome + "__";
    public const string ListarAmbientes = "listar_ambientes";
    public const string ListarSessoes = "listar_sessoes";
    public const string CriarAgente = "criar_agente";
    public const string EnviarMensagem = "enviar_mensagem";
    public const string LerSessao = "ler_sessao";
    public const string Esperar = "esperar";
    public const string ResponderPermissao = "responder_permissao";
    public const string PararSessao = "parar_sessao";

    public const int EsperaMaxima = 240;

    public static readonly IReadOnlyList<FerramentaMcp> Todas =
    [
        new(ListarAmbientes,
            "Lista os ambientes do Nexora (repositórios e workspaces do usuário) com a pasta principal, a branch atual e os projetos vinculados. Use o nome ou a pasta em criar_agente.",
            [], [], SoLeitura: true),
        new(ListarSessoes,
            "Lista as sessões abertas no Nexora: id, título, pasta, branch, estado (trabalhando, precisa de você, pronta, erro), última atividade e permissão pendente. Os seus agentes vêm com agente=true.",
            [], [], SoLeitura: true),
        new(CriarAgente,
            "Abre uma sessão nova do Claude Code (um agente) e manda a primeira mensagem. O agente aparece no canvas do orquestrador e na página Ao vivo do Nexora.",
            new JsonObject
            {
                ["ambiente"] = Texto("Nome do ambiente (de listar_ambientes) ou caminho de uma pasta."),
                ["mensagem"] = Texto("Instrução completa para o agente. Ele não vê esta conversa: diga o objetivo, os arquivos relevantes, as restrições e como saber que terminou."),
                ["tarefa"] = Texto("Nome curto da tarefa. Com ele, o Nexora cria a branch <tarefa>-AAAAMMDD a partir da branch padrão e um worktree separado, e o agente trabalha lá. Use sempre que o agente for alterar código, para os agentes não pisarem uns nos outros."),
                ["titulo"] = Texto("Título curto do agente no canvas."),
                ["grupo"] = Texto("Nome de um grupo do canvas, para juntar agentes da mesma frente."),
                ["modelo"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("opus", "sonnet", "haiku", "fable"), ["description"] = "Modelo do agente. Sem ele, usa o padrão do Claude Code." },
                ["esforco"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("low", "medium", "high", "xhigh", "max"), ["description"] = "Esforço de raciocínio do agente. Sem ele, usa o padrão do Claude Code." },
            },
            ["ambiente", "mensagem"], SoLeitura: false),
        new(EnviarMensagem,
            "Manda uma mensagem a uma sessão aberta no Nexora (normalmente um agente seu). Se ela estiver trabalhando, a mensagem entra na fila.",
            new JsonObject { ["id"] = Texto("Id da sessão."), ["mensagem"] = Texto("Texto da mensagem.") },
            ["id", "mensagem"], SoLeitura: false),
        new(LerSessao,
            "Mostra o estado de uma sessão e os últimos itens da conversa: mensagens, respostas, ferramentas usadas e pedidos de permissão.",
            new JsonObject { ["id"] = Texto("Id da sessão."), ["ultimos"] = Numero("Quantos itens do fim da conversa trazer (padrão 12, máximo 60).") },
            ["id"], SoLeitura: true),
        new(Esperar,
            $"Espera até nenhuma das sessões estar trabalhando ou alguma precisar de resposta, por no máximo `segundos` (padrão 120, máximo {EsperaMaxima}). Sem ids, espera os seus agentes. Prefira isto a chamar ler_sessao repetidamente.",
            new JsonObject
            {
                ["ids"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" }, ["description"] = "Ids das sessões a esperar." },
                ["segundos"] = Numero("Tempo máximo de espera em segundos."),
            },
            [], SoLeitura: true),
        new(ResponderPermissao,
            "Responde ao pedido de permissão pendente de uma sessão. Permita só o que está claramente dentro da tarefa e é seguro; na dúvida, não responda e avise o usuário, que decide pelo Nexora. Para uma pergunta do agente, use negar e escreva a resposta em motivo.",
            new JsonObject
            {
                ["id"] = Texto("Id da sessão."),
                ["decisao"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("permitir", "negar") },
                ["motivo"] = Texto("Ao negar, o que o agente deve fazer em vez disso (ele lê este texto)."),
            },
            ["id", "decisao"], SoLeitura: false),
        new(PararSessao,
            "Interrompe o turno atual de uma sessão.",
            new JsonObject { ["id"] = Texto("Id da sessão.") },
            ["id"], SoLeitura: false),
    ];

    public static readonly IReadOnlyList<string> Livres = [.. Todas.Where(f => f.SoLeitura).Select(f => ServidorMcp.NomeCompleto(f.Nome))];

    public const string Instrucoes = """
        Você é o orquestrador do Nexora, o app em que o usuário acompanha várias sessões do Claude Code. Você coordena outras sessões (agentes) pelas ferramentas do servidor MCP "nexora".

        Como trabalhar:
        - Antes de dividir, levante o que já existe: o CLAUDE.md do projeto, as memórias e decisões guardadas, as skills e os servidores MCP disponíveis, e o código. Use as skills que se aplicam (por exemplo, a de UI/UX para design) para planejar.
        - Divida o pedido em tarefas independentes e crie um agente por tarefa com criar_agente. Quando o agente for alterar código, passe `tarefa`, para ele ter branch e worktree próprios.
        - Escolha modelo e esforço pelo peso de cada tarefa e diga o porquê ao usuário:
          - sonnet com esforço low ou medium para o simples e bem delimitado (um botão, um texto, um ajuste de estilo, um teste óbvio);
          - sonnet high ou opus medium para o que mexe em vários arquivos com caminho claro;
          - opus high ou xhigh para investigação, arquitetura, bug difícil ou mudança ampla;
          - haiku só para consulta rápida e mecânica.
        - A mensagem de cada agente precisa ser completa: ele não vê esta conversa. Inclua o contexto que você levantou (decisões, padrões do projeto, skill a usar) e como saber que terminou.
        - Acompanhe com esperar e ler_sessao; não fique chamando ler_sessao em sequência.
        - Responda permissões dos agentes só quando a ação for segura e estiver dentro da tarefa. Comandos destrutivos, push, deploy ou qualquer coisa fora do repositório ficam para o usuário decidir no Nexora: avise-o e siga esperando.
        - Não faça você mesmo o trabalho de código que deu a um agente.
        - Ao final, resuma para o usuário o que cada agente fez, em qual branch e o que falta revisar.
        """;

    static JsonObject Texto(string descricao) => new() { ["type"] = "string", ["description"] = descricao };

    static JsonObject Numero(string descricao) => new() { ["type"] = "integer", ["description"] = descricao };
}
