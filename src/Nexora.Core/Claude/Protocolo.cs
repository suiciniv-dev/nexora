using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nexora.Core.Claude;

public static class Protocolo
{
    static readonly EventoClaude[] Nada = [];

    public static EventoClaude[] Ler(string? linha)
    {
        if (string.IsNullOrWhiteSpace(linha)) return Nada;
        try
        {
            using var doc = JsonDocument.Parse(linha);
            var raiz = doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object) return Nada;
            if (PaiDoSubagente(raiz) is { } pai) return LerDoSubagente(raiz, pai);
            return Texto(raiz, "type") switch
            {
                "system" => LerSistema(raiz),
                "stream_event" => LerParcial(raiz),
                "assistant" => [.. LerBlocosAssistente(Objeto(raiz, "message")), .. LerUso(Objeto(raiz, "message"), null)],
                "user" => LerBlocosUsuario(Objeto(raiz, "message"), incluirTexto: false, Objeto(raiz, "tool_use_result")),
                "result" => [LerResultado(raiz)],
                "control_request" => [LerPedidoControle(raiz)],
                "control_response" => [LerRespostaControle(raiz)],
                "rate_limit_event" => LerLimite(raiz),
                _ => Nada,
            };
        }
        catch (JsonException)
        {
            return Nada;
        }
    }

    public static EventoClaude[] LerBlocosAssistente(JsonElement mensagem)
    {
        var lista = new List<EventoClaude>();
        foreach (var bloco in Itens(Objeto(mensagem, "content")))
            switch (Texto(bloco, "type"))
            {
                case "text" when Texto(bloco, "text") is { Length: > 0 } texto:
                    lista.Add(new TextoCompleto(texto));
                    break;
                case "tool_use":
                    lista.Add(new FerramentaChamada(Texto(bloco, "id") ?? "", Texto(bloco, "name") ?? "?", Copia(Objeto(bloco, "input"))));
                    break;
            }
        return [.. lista];
    }

    public static EventoClaude[] LerBlocosUsuario(JsonElement mensagem, bool incluirTexto, JsonElement resultado = default)
    {
        var conteudo = Objeto(mensagem, "content");
        if (conteudo.ValueKind == JsonValueKind.String)
        {
            var simples = conteudo.GetString();
            if (AvisosDeTarefa(simples) is { Length: > 0 } avisos) return avisos;
            return incluirTexto && TextoDoUsuario(simples) is { } s ? [new MensagemDoUsuario(s)] : Nada;
        }

        var lista = new List<EventoClaude>();
        var imagens = 0;
        var unico = Itens(conteudo).Count(b => Texto(b, "type") == "tool_result") == 1;
        var resumo = unico ? LerResumoDeSubagente(resultado) : null;
        foreach (var bloco in Itens(conteudo))
            switch (Texto(bloco, "type"))
            {
                case "tool_result":
                    lista.Add(new FerramentaRespondeu(Texto(bloco, "tool_use_id") ?? "", ConteudoDoResultado(Objeto(bloco, "content")), Booleano(bloco, "is_error"), resumo));
                    break;
                case "image":
                    imagens++;
                    break;
                case "text" when AvisosDeTarefa(Texto(bloco, "text")) is { Length: > 0 } avisos:
                    lista.AddRange(avisos);
                    break;
                case "text" when incluirTexto && TextoDoUsuario(Texto(bloco, "text")) is { } texto:
                    lista.Add(new MensagemDoUsuario(texto));
                    break;
            }
        if (incluirTexto && imagens > 0)
        {
            var primeira = lista.FindIndex(e => e is MensagemDoUsuario);
            if (primeira >= 0) lista[primeira] = ((MensagemDoUsuario)lista[primeira]) with { Imagens = imagens };
            else lista.Add(new MensagemDoUsuario("", imagens));
        }
        return [.. lista];
    }

    public static string Mensagem(string texto, IReadOnlyList<Anexo>? imagens = null)
    {
        var conteudo = new JsonArray();
        foreach (var imagem in imagens ?? [])
            conteudo.Add(new JsonObject
            {
                ["type"] = "image",
                ["source"] = new JsonObject { ["type"] = "base64", ["media_type"] = imagem.TipoMidia, ["data"] = Convert.ToBase64String(imagem.Dados) },
            });
        if (texto.Length > 0 || conteudo.Count == 0) conteudo.Add(new JsonObject { ["type"] = "text", ["text"] = texto });
        return Linha(new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject { ["role"] = "user", ["content"] = conteudo },
            ["parent_tool_use_id"] = null,
            ["session_id"] = "",
        });
    }

    public static string Controle(string id, JsonObject pedido) =>
        Linha(new JsonObject { ["type"] = "control_request", ["request_id"] = id, ["request"] = pedido });

    public const string GanchoDeSubagente = "nexora_subagente";

    public static string Inicializar(string id) => Controle(id, new JsonObject
    {
        ["subtype"] = "initialize",
        ["hooks"] = new JsonObject
        {
            ["PreToolUse"] = new JsonArray(new JsonObject { ["matcher"] = "Agent|Task", ["hookCallbackIds"] = new JsonArray(GanchoDeSubagente), ["timeout"] = 86400 }),
        },
    });

    public static string LiberarSubagente(string pedidoId, JsonNode? entrada) => Responder(pedidoId, entrada is null
        ? new JsonObject()
        : new JsonObject
        {
            ["hookSpecificOutput"] = new JsonObject { ["hookEventName"] = "PreToolUse", ["permissionDecision"] = "allow", ["updatedInput"] = entrada },
        });

    public static string BarrarSubagente(string pedidoId, string motivo) => Responder(pedidoId, new JsonObject
    {
        ["hookSpecificOutput"] = new JsonObject { ["hookEventName"] = "PreToolUse", ["permissionDecision"] = "deny", ["permissionDecisionReason"] = motivo },
    });

    public static string ControleRemoto(string id, bool ligado) => Controle(id, new JsonObject { ["subtype"] = "remote_control", ["enabled"] = ligado });

    public static ComandosDisponiveis LerInicio(JsonElement dados) => new(
        [.. Itens(Objeto(dados, "commands"))
            .Where(c => Texto(c, "name") is { Length: > 0 })
            .Select(c => new ComandoBarra(Texto(c, "name")!, Texto(c, "description") is { Length: > 0 } d ? d : null, Texto(c, "argumentHint") is { Length: > 0 } h ? h : null))],
        Objeto(dados, "remote_control_available").ValueKind != JsonValueKind.False,
        [.. Itens(Objeto(dados, "models"))
            .Where(m => Texto(m, "value") is { Length: > 0 } && Texto(m, "displayName") is { Length: > 0 })
            .Select(m => new ModeloDisponivel(
                Texto(m, "value")!,
                Texto(m, "displayName")!,
                Texto(m, "description") is { Length: > 0 } d ? d : null,
                Texto(m, "resolvedModel") is { Length: > 0 } r ? r : null,
                Booleano(m, "supportsEffort")
                    ? [.. Itens(Objeto(m, "supportedEffortLevels")).Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!)]
                    : [],
                Booleano(m, "supportsFastMode")))]);

    public static string Interromper(string id) => Controle(id, new JsonObject { ["subtype"] = "interrupt" });

    public static string MudarModo(string id, string modo) =>
        Controle(id, new JsonObject { ["subtype"] = "set_permission_mode", ["mode"] = modo });

    public static string AplicarConfiguracoes(string id, JsonObject configuracoes) =>
        Controle(id, new JsonObject { ["subtype"] = "apply_flag_settings", ["settings"] = configuracoes });

    public static string MudarModelo(string id, string? modelo) =>
        Controle(id, new JsonObject { ["subtype"] = "set_model", ["model"] = modelo });

    public static string Permitir(string pedidoId, JsonNode? entrada, JsonNode? regras = null)
    {
        var resposta = new JsonObject { ["behavior"] = "allow", ["updatedInput"] = entrada ?? new JsonObject() };
        if (regras is not null) resposta["updatedPermissions"] = regras;
        return Responder(pedidoId, resposta);
    }

    public static string Negar(string pedidoId, string motivo) =>
        Responder(pedidoId, new JsonObject { ["behavior"] = "deny", ["message"] = motivo });

    static string Responder(string pedidoId, JsonObject resposta) => Linha(new JsonObject
    {
        ["type"] = "control_response",
        ["response"] = new JsonObject { ["subtype"] = "success", ["request_id"] = pedidoId, ["response"] = resposta },
    });

    public static string RecusarControle(string pedidoId, string erro) => Linha(new JsonObject
    {
        ["type"] = "control_response",
        ["response"] = new JsonObject { ["subtype"] = "error", ["request_id"] = pedidoId, ["error"] = erro },
    });

    public static JsonNode? ParaNo(JsonElement? elemento) =>
        elemento is { ValueKind: not JsonValueKind.Undefined } e ? JsonNode.Parse(e.GetRawText()) : null;

    public static string? Texto(JsonElement e, string nome) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static JsonElement Objeto(JsonElement e, string nome) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) ? v : default;

    public static IEnumerable<JsonElement> Itens(JsonElement e) =>
        e.ValueKind == JsonValueKind.Array ? e.EnumerateArray() : [];

    static EventoClaude[] LerSistema(JsonElement raiz) => Texto(raiz, "subtype") switch
    {
        "init" => [new SessaoIniciada(Texto(raiz, "session_id") ?? "", Texto(raiz, "model"), Texto(raiz, "cwd"), Texto(raiz, "permissionMode"))],
        "thinking_tokens" => [new Pensando((int)(Numero(raiz, "estimated_tokens") ?? 0))],
        "compact_boundary" when Objeto(raiz, "compact_metadata") is var dados => [new ConversaCompactada(
            Texto(dados, "trigger") == "auto",
            (long)(Numero(dados, "pre_tokens") ?? 0),
            Numero(dados, "post_tokens") is { } depois ? (long)depois : null)],
        "task_progress" when Texto(raiz, "tool_use_id") is { Length: > 0 } id => [new TarefaAtualizada(
            FaseTarefa.Andou, id,
            Ferramentas: Inteiro(Objeto(raiz, "usage"), "tool_uses"),
            DuracaoMs: Longo(Objeto(raiz, "usage"), "duration_ms"),
            UltimaFerramenta: Texto(raiz, "last_tool_name"),
            Resumo: Texto(raiz, "summary"))],
        "task_notification" when Texto(raiz, "tool_use_id") is { Length: > 0 } id => [new TarefaAtualizada(
            FaseTarefa.Terminou, id, Texto(raiz, "status"),
            Ferramentas: Inteiro(Objeto(raiz, "usage"), "tool_uses"),
            DuracaoMs: Longo(Objeto(raiz, "usage"), "duration_ms"))],
        _ => Nada,
    };

    static EventoClaude[] LerDoSubagente(JsonElement raiz, string pai)
    {
        var mensagem = Objeto(raiz, "message");
        EventoClaude[] internos = Texto(raiz, "type") switch
        {
            "assistant" => LerBlocosAssistente(mensagem),
            "user" => LerBlocosUsuario(mensagem, incluirTexto: false, Objeto(raiz, "tool_use_result")),
            _ => Nada,
        };
        return [.. internos.Select(e => new EventoDeSubagente(pai, e))];
    }

    public static ResumoDeSubagente? LerResumoDeSubagente(JsonElement r)
    {
        if (r.ValueKind != JsonValueKind.Object || Texto(r, "agentId") is null && Numero(r, "totalToolUseCount") is null) return null;
        return new ResumoDeSubagente(Texto(r, "status"), Texto(r, "agentId"), Texto(r, "agentType"),
            Inteiro(r, "totalToolUseCount"), Longo(r, "totalDurationMs"), Longo(r, "totalTokens"),
            Texto(Objeto(r, "handbackReport"), "text") is { Length: > 0 } relato ? relato : null);
    }

    static readonly System.Text.RegularExpressions.Regex Aviso =
        new("<task-notification>(.*?)</task-notification>", System.Text.RegularExpressions.RegexOptions.Singleline);

    public static TarefaAtualizada[] AvisosDeTarefa(string? texto)
    {
        if (texto is null || !texto.Contains("<task-notification>", StringComparison.Ordinal)) return [];
        var lista = new List<TarefaAtualizada>();
        foreach (System.Text.RegularExpressions.Match aviso in Aviso.Matches(texto))
        {
            var corpo = aviso.Groups[1].Value;
            if (Etiqueta(corpo, "tool-use-id") is not { Length: > 0 } id) continue;
            lista.Add(new TarefaAtualizada(
                FaseTarefa.Terminou, id, Etiqueta(corpo, "status"),
                Ferramentas: int.TryParse(Etiqueta(corpo, "tool_uses"), out var usos) ? usos : null,
                DuracaoMs: long.TryParse(Etiqueta(corpo, "duration_ms"), out var ms) ? ms : null,
                Resultado: Etiqueta(corpo, "result")));
        }
        return [.. lista];
    }

    static string? Etiqueta(string texto, string nome)
    {
        var inicio = texto.IndexOf($"<{nome}>", StringComparison.Ordinal);
        if (inicio < 0) return null;
        inicio += nome.Length + 2;
        var fim = texto.IndexOf($"</{nome}>", inicio, StringComparison.Ordinal);
        return fim < 0 ? null : texto[inicio..fim].Trim();
    }

    static int? Inteiro(JsonElement e, string nome) => Numero(e, nome) is { } n ? (int)n : null;

    static long? Longo(JsonElement e, string nome) => Numero(e, nome) is { } n ? (long)n : null;

    static EventoClaude[] LerParcial(JsonElement raiz)
    {
        var evento = Objeto(raiz, "event");
        switch (Texto(evento, "type"))
        {
            case "content_block_start":
                var bloco = Objeto(evento, "content_block");
                return Texto(bloco, "type") switch
                {
                    "thinking" or "redacted_thinking" => [new PensamentoComecou()],
                    "tool_use" => [new FerramentaComecou(Texto(bloco, "name") ?? "?")],
                    _ => Nada,
                };
            case "content_block_stop":
                return [new BlocoEncerrado((int)(Numero(evento, "index") ?? 0))];
            case "content_block_delta":
                var delta = Objeto(evento, "delta");
                return Texto(delta, "type") switch
                {
                    "text_delta" when Texto(delta, "text") is { Length: > 0 } texto => [new TextoParcial(texto)],
                    "thinking_delta" when Texto(delta, "thinking") is { Length: > 0 } pensado => [new PensamentoParcial(pensado)],
                    _ => Nada,
                };
            default:
                return Nada;
        }
    }

    static EventoClaude LerResultado(JsonElement raiz)
    {
        var modelos = Objeto(raiz, "modelUsage");
        var janelas = modelos.ValueKind == JsonValueKind.Object
            ? modelos.EnumerateObject()
                .Where(m => Numero(m.Value, "contextWindow") is > 0)
                .ToDictionary(m => m.Name, m => (long)Numero(m.Value, "contextWindow")!.Value, StringComparer.OrdinalIgnoreCase)
            : null;
        return new TurnoEncerrado(
            Booleano(raiz, "is_error") || Texto(raiz, "subtype") is { } subtipo && subtipo != "success",
            Texto(raiz, "result"),
            Numero(raiz, "total_cost_usd"),
            (long)(Numero(raiz, "duration_ms") ?? 0),
            janelas);
    }

    public static EventoClaude[] LerUso(JsonElement mensagem, DateTimeOffset? quando)
    {
        var uso = Objeto(mensagem, "usage");
        if (uso.ValueKind != JsonValueKind.Object) return Nada;
        var tokens = (Numero(uso, "input_tokens") ?? 0) + (Numero(uso, "cache_creation_input_tokens") ?? 0)
            + (Numero(uso, "cache_read_input_tokens") ?? 0) + (Numero(uso, "output_tokens") ?? 0);
        if (tokens <= 0) return Nada;
        var criacao = Objeto(uso, "cache_creation");
        bool? longa = (Numero(criacao, "ephemeral_1h_input_tokens") ?? 0) > 0 ? true
            : (Numero(criacao, "ephemeral_5m_input_tokens") ?? 0) > 0 ? false
            : null;
        return [new ContextoUsado((long)tokens, longa, Texto(mensagem, "model"), quando)];
    }

    static EventoClaude LerPedidoControle(JsonElement raiz)
    {
        var id = Texto(raiz, "request_id") ?? "";
        var pedido = Objeto(raiz, "request");
        var subtipo = Texto(pedido, "subtype") ?? "";
        if (subtipo == "hook_callback" && Texto(pedido, "callback_id") == GanchoDeSubagente)
        {
            var dados = Objeto(pedido, "input");
            return new SubagentePedido(id, Texto(pedido, "tool_use_id") ?? Texto(dados, "tool_use_id"), Copia(Objeto(dados, "tool_input")));
        }
        if (subtipo != "can_use_tool") return new ControleNaoSuportado(id, subtipo);
        var sugestoes = Objeto(pedido, "permission_suggestions");
        return new PermissaoPedida(
            id,
            Texto(pedido, "tool_name") ?? "?",
            Copia(Objeto(pedido, "input")),
            Texto(pedido, "description"),
            Texto(pedido, "decision_reason"),
            sugestoes.ValueKind == JsonValueKind.Array && sugestoes.GetArrayLength() > 0 ? sugestoes.Clone() : null,
            Texto(pedido, "agent_id") is { Length: > 0 } agente ? agente : null,
            Texto(pedido, "tool_use_id"));
    }

    static EventoClaude LerRespostaControle(JsonElement raiz)
    {
        var resposta = Objeto(raiz, "response");
        var dados = Objeto(resposta, "response");
        return new ControleRespondido(
            Texto(resposta, "request_id") ?? "",
            Texto(resposta, "subtype") == "success",
            Texto(resposta, "error"),
            dados.ValueKind == JsonValueKind.Object ? dados.Clone() : null);
    }

    static EventoClaude[] LerLimite(JsonElement raiz)
    {
        var janelas = Objeto(Objeto(raiz, "rate_limit_info"), "unifiedWindows");
        var sessao = Objeto(janelas, "five_hour");
        var semana = Objeto(janelas, "seven_day");
        if (sessao.ValueKind != JsonValueKind.Object && semana.ValueKind != JsonValueKind.Object) return Nada;
        DateTimeOffset? reinicia = Numero(sessao, "resetsAt") is { } s ? DateTimeOffset.FromUnixTimeSeconds((long)s) : null;
        DateTimeOffset? semanaReinicia = Numero(semana, "resetsAt") is { } w ? DateTimeOffset.FromUnixTimeSeconds((long)w) : null;
        return [new UsoDoPlano(Numero(sessao, "utilization"), Numero(semana, "utilization"), reinicia, semanaReinicia)];
    }

    static string ConteudoDoResultado(JsonElement conteudo) => conteudo.ValueKind switch
    {
        JsonValueKind.String => conteudo.GetString() ?? "",
        JsonValueKind.Array => string.Join("\n", Itens(conteudo).Select(b => Texto(b, "text")).OfType<string>()),
        _ => "",
    };

    static string? TextoDoUsuario(string? texto) =>
        string.IsNullOrWhiteSpace(texto) || texto.TrimStart().StartsWith('<') ? null : texto.Trim();

    static string? PaiDoSubagente(JsonElement raiz) =>
        raiz.TryGetProperty("parent_tool_use_id", out var pai) && pai.ValueKind == JsonValueKind.String && pai.GetString() is { Length: > 0 } id ? id : null;

    static double? Numero(JsonElement e, string nome) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    static bool Booleano(JsonElement e, string nome) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.True;

    static JsonElement Copia(JsonElement e) => e.ValueKind == JsonValueKind.Undefined ? default : e.Clone();

    static string Linha(JsonObject objeto) => objeto.ToJsonString();
}
