using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.Core.Codex;

public sealed record UsoDoCodex(double? Sessao, double? Semana, DateTimeOffset? SessaoReinicia, DateTimeOffset? SemanaReinicia) : EventoClaude;

public abstract record MensagemCodex;

public sealed record RespostaCodex(long Id, JsonElement? Resultado, string? Erro) : MensagemCodex;

public sealed record PedidoDoCodex(JsonNode Id, string Metodo) : MensagemCodex;

public sealed record EventoDoCodex(EventoClaude Evento) : MensagemCodex;

public sealed class ProtocoloCodex
{
    public const string MarcaDaSessao = """[{"type":"codexSession"}]""";
    static readonly JsonSerializerOptions Compacto = new() { WriteIndented = false };

    readonly Dictionary<string, JsonElement> _mudancas = [];
    readonly Dictionary<string, List<(string Id, string Pergunta)>> _perguntas = [];
    readonly Dictionary<string, JsonNode?> _permissoes = [];
    readonly HashSet<string> _encerrados = [];
    JsonObject? _limites;
    string? _erroDoTurno;
    DateTime? _turnoDesde;

    public string? ThreadId { get; set; }
    public string? TurnoAtual { get; private set; }

    public void TurnoComecou(string turnoId)
    {
        if (_encerrados.Contains(turnoId)) return;
        TurnoAtual ??= turnoId;
        UltimoTurno = TurnoAtual;
        _turnoDesde ??= DateTime.UtcNow;
    }

    public TurnoEncerrado? Abandonar(string motivo)
    {
        if (TurnoAtual is null) return null;
        _turnoDesde = null;
        TurnoAtual = null;
        return new TurnoEncerrado(true, motivo, null, 0);
    }
    public string? UltimoTurno { get; private set; }
    public string? Modelo { get; set; }
    public long? JanelaDeContexto { get; private set; }

    public static string Pedido(long id, string metodo, JsonObject? parametros = null)
    {
        var o = new JsonObject { ["method"] = metodo, ["id"] = id };
        if (parametros is not null) o["params"] = parametros;
        return o.ToJsonString(Compacto);
    }

    public static string Aviso(string metodo, JsonObject? parametros = null) =>
        new JsonObject { ["method"] = metodo, ["params"] = parametros ?? [] }.ToJsonString(Compacto);

    public static string Resposta(JsonNode id, JsonObject resultado) =>
        new JsonObject { ["id"] = id.DeepClone(), ["result"] = resultado }.ToJsonString(Compacto);

    public static string Recusa(JsonNode id, string mensagem) =>
        new JsonObject { ["id"] = id.DeepClone(), ["error"] = new JsonObject { ["code"] = -32601, ["message"] = mensagem } }.ToJsonString(Compacto);

    public static JsonObject Inicializar(string versao) => new()
    {
        ["clientInfo"] = new JsonObject { ["name"] = "nexora", ["title"] = "Nexora", ["version"] = versao },
        ["capabilities"] = new JsonObject { ["experimentalApi"] = true, ["optOutNotificationMethods"] = new JsonArray("turn/diff/updated") },
    };

    public static (string Aprovacao, string Sandbox) Politica(string? modo) => modo switch
    {
        "bypassPermissions" => ("never", "danger-full-access"),
        "plan" => ("on-request", "read-only"),
        "manual" => ("untrusted", "workspace-write"),
        _ => ("on-request", "workspace-write"),
    };

    static JsonObject SandboxDoTurno(string sandbox) => sandbox switch
    {
        "danger-full-access" => new JsonObject { ["type"] = "dangerFullAccess" },
        "read-only" => new JsonObject { ["type"] = "readOnly" },
        _ => new JsonObject { ["type"] = "workspaceWrite" },
    };

    public static JsonObject Configuracao(string? mcp)
    {
        var config = new JsonObject
        {
            ["tools.update_plan.enabled"] = true,
            ["mcp_optional_startup_grace_ms"] = 10_000,
            ["features.concurrent_reasoning_summaries"] = true,
        };
        foreach (var (nome, servidor) in ServidoresMcp(mcp)) config[$"mcp_servers.{nome}"] = servidor;
        return config;
    }

    public static IEnumerable<(string Nome, JsonObject Servidor)> ServidoresMcp(string? mcp)
    {
        if (string.IsNullOrWhiteSpace(mcp)) return [];
        JsonObject? raiz;
        try
        {
            raiz = JsonNode.Parse(mcp) as JsonObject;
        }
        catch (JsonException)
        {
            return [];
        }
        if ((raiz?["mcpServers"] ?? raiz) is not JsonObject servidores) return [];
        var lista = new List<(string, JsonObject)>();
        foreach (var (nome, no) in servidores)
        {
            if (no is not JsonObject s) continue;
            var codex = new JsonObject();
            if (s["url"] is JsonValue url && url.TryGetValue<string>(out var endereco))
            {
                codex["url"] = endereco;
                if (nome == Segredos.FerramentaDeSegredo.Servidor) codex["tool_timeout_sec"] = 300;
                if (s["headers"] is JsonObject cabecalhos) codex["http_headers"] = cabecalhos.DeepClone();
            }
            else if (s["command"] is JsonValue comando && comando.TryGetValue<string>(out var executavel))
            {
                codex["command"] = executavel;
                if (s["args"] is JsonArray args) codex["args"] = args.DeepClone();
                if (s["env"] is JsonObject env) codex["env"] = env.DeepClone();
            }
            else continue;
            lista.Add((nome, codex));
        }
        return lista;
    }

    public static JsonObject NovaThread(string pasta, string? modelo, string? modo, string? mcp = null, string? instrucoes = null)
    {
        var (aprovacao, sandbox) = Politica(modo);
        var p = new JsonObject { ["cwd"] = pasta, ["approvalPolicy"] = aprovacao, ["sandbox"] = sandbox, ["serviceName"] = "nexora", ["config"] = Configuracao(mcp) };
        if (!string.IsNullOrEmpty(modelo)) p["model"] = modelo;
        if (!string.IsNullOrWhiteSpace(instrucoes)) p["developerInstructions"] = instrucoes;
        return p;
    }

    public static JsonObject Retomar(string threadId, string? pasta = null, string? modelo = null, string? mcp = null)
    {
        var p = new JsonObject { ["threadId"] = threadId, ["excludeTurns"] = true, ["config"] = Configuracao(mcp) };
        if (!string.IsNullOrEmpty(pasta)) p["cwd"] = pasta;
        if (!string.IsNullOrEmpty(modelo)) p["model"] = modelo;
        return p;
    }

    public static bool Arquivada(string? erro) => erro is not null && Regex.IsMatch(erro, @"session \S+ is archived", RegexOptions.IgnoreCase);

    static JsonArray Entrada(string texto, IReadOnlyList<string> imagens)
    {
        var entrada = new JsonArray();
        if (texto.Length > 0) entrada.Add(new JsonObject { ["type"] = "text", ["text"] = texto });
        foreach (var caminho in imagens) entrada.Add(new JsonObject { ["type"] = "localImage", ["path"] = caminho });
        return entrada;
    }

    public static JsonObject Turno(string threadId, string texto, IReadOnlyList<string> imagens, string? modelo, string? modo, string? esforco, string? pasta = null)
    {
        var (aprovacao, sandbox) = Politica(modo);
        var p = new JsonObject { ["threadId"] = threadId, ["input"] = Entrada(texto, imagens), ["summary"] = "detailed" };
        if (!string.IsNullOrEmpty(pasta)) p["cwd"] = pasta;
        if (!string.IsNullOrEmpty(modelo)) p["model"] = modelo;
        var nivel = Esforco(esforco);
        if (nivel is not null) p["effort"] = nivel;
        p["approvalPolicy"] = aprovacao;
        p["sandboxPolicy"] = SandboxDoTurno(sandbox);
        if (!string.IsNullOrEmpty(modelo))
        {
            var ajustes = new JsonObject { ["model"] = modelo };
            if (nivel is not null) ajustes["reasoning_effort"] = nivel;
            p["collaborationMode"] = new JsonObject { ["mode"] = modo == "plan" ? "plan" : "default", ["settings"] = ajustes };
        }
        return p;
    }

    public static JsonObject Direcionar(string threadId, string turnoId, string texto, IReadOnlyList<string> imagens) =>
        new() { ["threadId"] = threadId, ["expectedTurnId"] = turnoId, ["input"] = Entrada(texto, imagens) };

    public static string? Esforco(string? esforco) => esforco switch
    {
        "none" or "minimal" or "low" or "medium" or "high" or "xhigh" => esforco,
        "max" => "xhigh",
        _ => null,
    };

    public static JsonObject Decisao(bool permitir, bool naSessao) =>
        new() { ["decision"] = !permitir ? "decline" : naSessao ? "acceptForSession" : "accept" };

    public static JsonObject DecisaoAntiga(bool permitir, bool naSessao) =>
        new() { ["decision"] = !permitir ? "denied" : naSessao ? "approved_for_session" : "approved" };

    public static JsonObject DecisaoDePermissoes(bool permitir, bool naSessao, JsonNode? pedidas) => permitir
        ? new JsonObject { ["permissions"] = pedidas?.DeepClone() ?? new JsonObject(), ["scope"] = naSessao ? "session" : "turn" }
        : new JsonObject { ["permissions"] = new JsonObject(), ["scope"] = "turn" };

    public JsonObject RespostasDasPerguntas(string pedidoId, JsonNode? respostas)
    {
        var mapa = new JsonObject();
        _perguntas.Remove(pedidoId, out var perguntas);
        foreach (var (id, pergunta) in perguntas ?? [])
        {
            var lista = new JsonArray();
            if (respostas is JsonObject r && r[pergunta] is { } valor)
            {
                if (valor is JsonArray varias)
                    foreach (var v in varias) lista.Add(v?.ToString());
                else
                    foreach (var parte in valor.ToString().Split(", ", StringSplitOptions.RemoveEmptyEntries)) lista.Add(parte);
            }
            mapa[id] = new JsonObject { ["answers"] = lista };
        }
        return new JsonObject { ["answers"] = mapa };
    }

    public JsonNode? PermissoesPedidas(string pedidoId) => _permissoes.Remove(pedidoId, out var p) ? p : null;

    public IReadOnlyList<MensagemCodex> Ler(string linha)
    {
        if (string.IsNullOrWhiteSpace(linha)) return [];
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(linha);
        }
        catch (JsonException)
        {
            return [];
        }
        using (doc)
        {
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return [];
            var temId = r.TryGetProperty("id", out var id);
            var metodo = Texto(r, "method");
            var p = r.TryGetProperty("params", out var parametros) ? parametros.Clone() : default;
            if (metodo is null)
            {
                if (!temId || id.ValueKind != JsonValueKind.Number) return [];
                var erro = r.TryGetProperty("error", out var e) ? Texto(e, "message") ?? Idioma.T("O Codex recusou o pedido.") : null;
                JsonElement? resultado = r.TryGetProperty("result", out var res) ? res.Clone() : null;
                return [new RespostaCodex(id.GetInt64(), resultado, erro)];
            }
            if (temId) return Pedido(JsonNode.Parse(id.GetRawText())!, metodo, p);
            return [.. Notificacao(metodo, p).Select(e => new EventoDoCodex(e))];
        }
    }

    IReadOnlyList<MensagemCodex> Pedido(JsonNode id, string metodo, JsonElement p)
    {
        var mensagens = PedidoDaThread(id, metodo, p);
        if (Texto(p, "threadId") is not { Length: > 0 } thread || ThreadId is null || thread == ThreadId) return mensagens;
        return [.. mensagens.Select(m => m is EventoDoCodex { Evento: PermissaoPedida pedido } ? new EventoDoCodex(pedido with { AgenteId = thread }) : m)];
    }

    IReadOnlyList<MensagemCodex> PedidoDaThread(JsonNode id, string metodo, JsonElement p)
    {
        var pedidoId = id.ToJsonString();
        switch (metodo)
        {
            case "item/commandExecution/requestApproval":
            {
                var entrada = new JsonObject { ["command"] = Texto(p, "command") ?? "", ["description"] = Texto(p, "reason") };
                if (Texto(p, "cwd") is { } cwd) entrada["cwd"] = cwd;
                return
                [
                    new PedidoDoCodex(id, metodo),
                    new EventoDoCodex(new PermissaoPedida(pedidoId, "Bash", Elemento(entrada), Texto(p, "reason"), Texto(p, "reason"), Elemento(JsonNode.Parse(MarcaDaSessao)!))),
                ];
            }
            case "item/fileChange/requestApproval":
            {
                var item = Texto(p, "itemId");
                var entrada = new JsonObject();
                if (item is not null && _mudancas.TryGetValue(item, out var mudancas) && mudancas.ValueKind == JsonValueKind.Array)
                {
                    var caminhos = mudancas.EnumerateArray().Select(m => Texto(m, "path")).OfType<string>().ToList();
                    if (caminhos.Count > 0) entrada["file_path"] = caminhos[0];
                    if (caminhos.Count > 1) entrada["description"] = string.Join(", ", caminhos);
                    entrada["diff"] = Diffs(mudancas);
                }
                return
                [
                    new PedidoDoCodex(id, metodo),
                    new EventoDoCodex(new PermissaoPedida(pedidoId, "Edit", Elemento(entrada), Texto(p, "reason"), Texto(p, "reason"), Elemento(JsonNode.Parse(MarcaDaSessao)!))),
                ];
            }
            case "execCommandApproval":
            {
                var comando = p.TryGetProperty("command", out var partes) && partes.ValueKind == JsonValueKind.Array
                    ? string.Join(" ", partes.EnumerateArray().Select(x => x.GetString()))
                    : Texto(p, "command") ?? "";
                var entrada = new JsonObject { ["command"] = comando, ["description"] = Texto(p, "reason") };
                return
                [
                    new PedidoDoCodex(id, metodo),
                    new EventoDoCodex(new PermissaoPedida(pedidoId, "Bash", Elemento(entrada), Texto(p, "reason"), Texto(p, "reason"), Elemento(JsonNode.Parse(MarcaDaSessao)!))),
                ];
            }
            case "applyPatchApproval":
            {
                var entrada = new JsonObject();
                if (p.TryGetProperty("fileChanges", out var arquivos) && arquivos.ValueKind == JsonValueKind.Object)
                {
                    var caminhos = arquivos.EnumerateObject().Select(a => a.Name).ToList();
                    if (caminhos.Count > 0) entrada["file_path"] = caminhos[0];
                    if (caminhos.Count > 1) entrada["description"] = string.Join(", ", caminhos);
                }
                return
                [
                    new PedidoDoCodex(id, metodo),
                    new EventoDoCodex(new PermissaoPedida(pedidoId, "Edit", Elemento(entrada), Texto(p, "reason"), Texto(p, "reason"), Elemento(JsonNode.Parse(MarcaDaSessao)!))),
                ];
            }
            case "item/permissions/requestApproval":
            {
                var pedidas = p.TryGetProperty("permissions", out var perm) ? JsonNode.Parse(perm.GetRawText()) : null;
                _permissoes[pedidoId] = pedidas;
                var entrada = new JsonObject { ["description"] = Texto(p, "reason") ?? Idioma.T("O Codex pede mais acesso nesta sessão."), ["permissions"] = pedidas?.DeepClone() };
                return
                [
                    new PedidoDoCodex(id, metodo),
                    new EventoDoCodex(new PermissaoPedida(pedidoId, Idioma.T("mais acesso"), Elemento(entrada), Texto(p, "reason"), Texto(p, "reason"), Elemento(JsonNode.Parse(MarcaDaSessao)!))),
                ];
            }
            case "item/tool/requestUserInput":
            {
                var perguntas = new JsonArray();
                var guardadas = new List<(string, string)>();
                if (p.TryGetProperty("questions", out var lista) && lista.ValueKind == JsonValueKind.Array)
                    foreach (var q in lista.EnumerateArray())
                    {
                        var texto = Texto(q, "question") ?? "";
                        guardadas.Add((Texto(q, "id") ?? texto, texto));
                        var opcoes = new JsonArray();
                        if (q.TryGetProperty("options", out var ops) && ops.ValueKind == JsonValueKind.Array)
                            foreach (var o in ops.EnumerateArray())
                                opcoes.Add(new JsonObject { ["label"] = Texto(o, "label") ?? "", ["description"] = Texto(o, "description") ?? "" });
                        perguntas.Add(new JsonObject { ["question"] = texto, ["header"] = Texto(q, "header") ?? "", ["multiSelect"] = false, ["options"] = opcoes });
                    }
                _perguntas[pedidoId] = guardadas;
                return
                [
                    new PedidoDoCodex(id, metodo),
                    new EventoDoCodex(new PermissaoPedida(pedidoId, "AskUserQuestion", Elemento(new JsonObject { ["questions"] = perguntas }), null, null, null)),
                ];
            }
            default:
                return [new PedidoDoCodex(id, metodo)];
        }
    }

    IEnumerable<EventoClaude> Notificacao(string metodo, JsonElement p)
    {
        switch (metodo)
        {
            case "thread/started":
                if (p.TryGetProperty("thread", out var thread) && Texto(thread, "id") is { } novo) ThreadId ??= novo;
                break;
            case "turn/started":
                if (p.TryGetProperty("turn", out var inicio)) TurnoAtual = Texto(inicio, "id");
                UltimoTurno = TurnoAtual ?? UltimoTurno;
                _turnoDesde = DateTime.UtcNow;
                break;
            case "item/started":
                if (p.TryGetProperty("item", out var comecou) && Comecou(comecou) is { } chamada) yield return chamada;
                break;
            case "item/agentMessage/delta":
                if (Texto(p, "delta") is { Length: > 0 } texto) yield return new TextoParcial(texto);
                break;
            case "item/reasoning/summaryPartAdded":
                yield return new PensamentoParcial("\n\n");
                break;
            case "item/reasoning/summaryTextDelta":
                if (Texto(p, "delta") is { Length: > 0 } pensamento) yield return new PensamentoParcial(pensamento);
                break;
            case "turn/plan/updated":
                if (Plano(p) is { } plano) foreach (var e in plano) yield return e;
                break;
            case "model/rerouted":
                if (Texto(p, "toModel") is { Length: > 0 } novoModelo) Modelo = novoModelo;
                break;
            case "error":
                if (Erro(p) is { } aviso) yield return aviso;
                break;
            case "item/fileChange/patchUpdated":
                if (Texto(p, "itemId") is { } mudou && p.TryGetProperty("changes", out var trocas)) _mudancas[mudou] = trocas.Clone();
                break;
            case "item/completed":
                if (p.TryGetProperty("item", out var terminou))
                    foreach (var e in Terminou(terminou)) yield return e;
                break;
            case "turn/completed":
                yield return Fim(p);
                break;
            case "thread/tokenUsage/updated":
                if (Uso(p) is { } uso) yield return uso;
                break;
            case "account/rateLimits/updated":
                if (Juntar(p) is { } uso2) yield return uso2;
                break;
        }
    }

    IEnumerable<EventoClaude>? Plano(JsonElement p)
    {
        if (!p.TryGetProperty("plan", out var passos) || passos.ValueKind != JsonValueKind.Array) return null;
        var tarefas = new JsonArray();
        foreach (var passo in passos.EnumerateArray())
        {
            var estado = Texto(passo, "status") switch
            {
                "completed" => "completed",
                "inProgress" or "in_progress" => "in_progress",
                _ => "pending",
            };
            var texto = Texto(passo, "step") ?? "";
            tarefas.Add(new JsonObject { ["content"] = texto, ["activeForm"] = texto, ["status"] = estado });
        }
        var id = $"plano-{TurnoAtual ?? Texto(p, "turnId") ?? "x"}-{Guid.NewGuid():N}";
        return [new FerramentaChamada(id, "TodoWrite", Elemento(new JsonObject { ["todos"] = tarefas })), new FerramentaRespondeu(id, "", false)];
    }

    EventoClaude? Erro(JsonElement p)
    {
        var erro = p.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Object ? e : default;
        var mensagem = erro.ValueKind == JsonValueKind.Object ? Texto(erro, "message") : null;
        if (mensagem is null) return null;
        var detalhes = Texto(erro, "additionalDetails");
        var completa = detalhes is { Length: > 0 } ? $"{mensagem}\n{detalhes}" : mensagem;
        if (p.TryGetProperty("willRetry", out var tenta) && tenta.ValueKind == JsonValueKind.True)
            return new AvisoDoAgente(Idioma.F("O Codex vai tentar de novo: {0}", mensagem));
        _erroDoTurno = completa;
        return null;
    }

    UsoDoCodex? Juntar(JsonElement p)
    {
        var porLimite = p.TryGetProperty("rateLimitsByLimitId", out var mapa) && mapa.ValueKind == JsonValueKind.Object && mapa.TryGetProperty("codex", out var codex) && codex.ValueKind == JsonValueKind.Object ? codex : default;
        var limites = porLimite.ValueKind == JsonValueKind.Object ? porLimite : p.TryGetProperty("rateLimits", out var r) ? r : default;
        if (limites.ValueKind != JsonValueKind.Object) return null;
        var novo = JsonNode.Parse(limites.GetRawText()) as JsonObject ?? [];
        if (_limites is { } antigo)
            foreach (var (chave, valor) in antigo)
                if (novo[chave] is null && valor is not null) novo[chave] = valor.DeepClone();
        _limites = novo;
        using var doc = JsonDocument.Parse(novo.ToJsonString());
        return Limites(doc.RootElement);
    }

    static EventoClaude? Comecou(JsonElement item)
    {
        var id = Texto(item, "id") ?? "";
        switch (Texto(item, "type"))
        {
            case "reasoning":
                return new PensamentoComecou();
            case "commandExecution":
            {
                var entrada = new JsonObject { ["command"] = Texto(item, "command") ?? "" };
                return new FerramentaChamada(id, "Bash", Elemento(entrada));
            }
            case "fileChange":
            {
                var mudancas = item.TryGetProperty("changes", out var c) && c.ValueKind == JsonValueKind.Array ? c : default;
                var primeira = mudancas.ValueKind == JsonValueKind.Array ? mudancas.EnumerateArray().FirstOrDefault() : default;
                var nome = mudancas.ValueKind == JsonValueKind.Array && mudancas.GetArrayLength() > 1 ? "MultiEdit"
                    : primeira.ValueKind == JsonValueKind.Object && primeira.TryGetProperty("kind", out var k) && Texto(k, "type") == "add" ? "Write" : "Edit";
                var entrada = new JsonObject();
                if (primeira.ValueKind == JsonValueKind.Object && Texto(primeira, "path") is { } caminho) entrada["file_path"] = caminho;
                return new FerramentaChamada(id, nome, Elemento(entrada));
            }
            case "mcpToolCall":
            {
                var argumentos = item.TryGetProperty("arguments", out var a) ? a.Clone() : Elemento(new JsonObject());
                return new FerramentaChamada(id, $"mcp__{Texto(item, "server")}__{Texto(item, "tool")}", argumentos);
            }
            case "webSearch":
                return new FerramentaChamada(id, "WebSearch", Elemento(new JsonObject { ["query"] = Texto(item, "query") ?? "" }));
            case "imageView":
                return new FerramentaChamada(id, "Read", Elemento(new JsonObject { ["file_path"] = Texto(item, "path") ?? "" }));
            default:
                return null;
        }
    }

    IEnumerable<EventoClaude> Terminou(JsonElement item)
    {
        var id = Texto(item, "id") ?? "";
        var status = Texto(item, "status");
        var falhou = status is "failed" or "declined";
        switch (Texto(item, "type"))
        {
            case "agentMessage":
                if (Texto(item, "text") is { Length: > 0 } texto) yield return new TextoCompleto(texto);
                break;
            case "commandExecution":
            {
                var codigo = item.TryGetProperty("exitCode", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : (int?)null;
                var saida = Texto(item, "aggregatedOutput") ?? "";
                if (status == "declined") saida = Idioma.T("Negado.");
                yield return new FerramentaRespondeu(id, saida, falhou || codigo is not (null or 0));
                break;
            }
            case "fileChange":
            {
                var diffs = item.TryGetProperty("changes", out var mudancas) && mudancas.ValueKind == JsonValueKind.Array ? Diffs(mudancas) : "";
                _mudancas.Remove(id);
                yield return new FerramentaRespondeu(id, status == "declined" ? Idioma.T("Negado.") : diffs, falhou);
                break;
            }
            case "mcpToolCall":
            {
                var erro = item.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Object ? Texto(e, "message") : null;
                yield return new FerramentaRespondeu(id, erro ?? ConteudoMcp(item), falhou || erro is not null);
                break;
            }
            case "webSearch":
            {
                var consulta = Texto(item, "query") ?? (item.TryGetProperty("action", out var acao) ? Texto(acao, "query") : null);
                yield return new FerramentaRespondeu(id, consulta ?? "", falhou);
                break;
            }
            case "imageView":
                yield return new FerramentaRespondeu(id, "", falhou);
                break;
            case "contextCompaction":
                yield return new ConversaCompactada(true, 0, null);
                break;
        }
    }

    TurnoEncerrado Fim(JsonElement p)
    {
        var turno = p.TryGetProperty("turn", out var t) ? t : default;
        if (turno.ValueKind == JsonValueKind.Object && Texto(turno, "id") is { } terminado) _encerrados.Add(terminado);
        var status = turno.ValueKind == JsonValueKind.Object ? Texto(turno, "status") : null;
        var erro = turno.ValueKind == JsonValueKind.Object && turno.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Object ? Texto(e, "message") : null;
        var duracao = _turnoDesde is { } desde ? (long)(DateTime.UtcNow - desde).TotalMilliseconds : 0;
        _turnoDesde = null;
        TurnoAtual = null;
        var guardado = _erroDoTurno;
        _erroDoTurno = null;
        erro ??= guardado;
        IReadOnlyDictionary<string, long>? janelas = Modelo is { Length: > 0 } m && JanelaDeContexto is { } j ? new Dictionary<string, long> { [m] = j } : null;
        return status switch
        {
            "failed" => new TurnoEncerrado(true, erro ?? Idioma.T("O Codex não conseguiu terminar o turno."), null, duracao, janelas),
            "interrupted" => new TurnoEncerrado(true, Idioma.T("O turno foi interrompido."), null, duracao, janelas),
            _ => new TurnoEncerrado(false, null, null, duracao, janelas),
        };
    }

    ContextoUsado? Uso(JsonElement p)
    {
        var uso = p.TryGetProperty("tokenUsage", out var u) ? u : p.TryGetProperty("usage", out var u2) ? u2 : default;
        if (uso.ValueKind != JsonValueKind.Object) return null;
        if (Numero(uso, "modelContextWindow") is { } janela) JanelaDeContexto = janela;
        var ultimo = uso.TryGetProperty("last", out var l) && l.ValueKind == JsonValueKind.Object ? l : default;
        var total = uso.TryGetProperty("total", out var tt) && tt.ValueKind == JsonValueKind.Object ? tt : default;
        var tokens = (ultimo.ValueKind == JsonValueKind.Object ? Numero(ultimo, "inputTokens") ?? Numero(ultimo, "totalTokens") : null)
            ?? (total.ValueKind == JsonValueKind.Object ? Numero(total, "totalTokens") : null);
        return tokens is { } n ? new ContextoUsado(n, null, Modelo, DateTimeOffset.Now) : null;
    }

    public static (IReadOnlyList<ModeloDisponivel> Modelos, string? Proximo) Modelos(JsonElement resultado)
    {
        var lista = new List<ModeloDisponivel>();
        if (resultado.ValueKind == JsonValueKind.Object && resultado.TryGetProperty("data", out var dados) && dados.ValueKind == JsonValueKind.Array)
            foreach (var m in dados.EnumerateArray())
            {
                if (m.TryGetProperty("hidden", out var oculto) && oculto.ValueKind == JsonValueKind.True) continue;
                if ((Texto(m, "model") ?? Texto(m, "id")) is not { } valor) continue;
                var esforcos = m.TryGetProperty("supportedReasoningEfforts", out var e) && e.ValueKind == JsonValueKind.Array
                    ? e.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.Object ? Texto(x, "reasoningEffort") : x.ValueKind == JsonValueKind.String ? x.GetString() : null).OfType<string>().ToList()
                    : [];
                var padrao = m.TryGetProperty("isDefault", out var d) && d.ValueKind == JsonValueKind.True;
                var modelo = new ModeloDisponivel(valor, Texto(m, "displayName") ?? valor, Texto(m, "description"), null, esforcos);
                if (padrao) lista.Insert(0, modelo);
                else lista.Add(modelo);
            }
        return (lista, resultado.ValueKind == JsonValueKind.Object ? Texto(resultado, "nextCursor") : null);
    }

    public static IReadOnlyList<EventoClaude> Historico(JsonElement thread)
    {
        var eventos = new List<EventoClaude>();
        if (thread.ValueKind != JsonValueKind.Object || !thread.TryGetProperty("turns", out var turnos) || turnos.ValueKind != JsonValueKind.Array) return eventos;
        var leitor = new ProtocoloCodex();
        foreach (var turno in turnos.EnumerateArray())
        {
            if (!turno.TryGetProperty("items", out var itens) || itens.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in itens.EnumerateArray())
                switch (Texto(item, "type"))
                {
                    case "userMessage":
                    {
                        var partes = item.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.Array ? c.EnumerateArray().ToList() : [];
                        var texto = string.Join("\n", partes.Where(x => Texto(x, "type") == "text").Select(x => Texto(x, "text")).OfType<string>());
                        var imagens = partes.Count(x => Texto(x, "type") is "image" or "localImage");
                        if (texto.Length > 0 || imagens > 0) eventos.Add(new MensagemDoUsuario(texto, imagens));
                        break;
                    }
                    case "reasoning" or "plan" or "hookPrompt":
                        break;
                    default:
                        if (Comecou(item) is FerramentaChamada chamada) eventos.Add(chamada);
                        eventos.AddRange(leitor.Terminou(item));
                        break;
                }
        }
        return eventos;
    }

    public static UsoDoCodex? Limites(JsonElement limites)
    {
        if (limites.ValueKind != JsonValueKind.Object) return null;
        (double? Uso, DateTimeOffset? Reinicia) Janela(string nome) =>
            limites.TryGetProperty(nome, out var j) && j.ValueKind == JsonValueKind.Object
                ? (Numero(j, "usedPercent") is { } u ? u / 100.0 : null, Numero(j, "resetsAt") is { } r ? Instante(r) : null)
                : (null, null);
        var (sessao, sessaoReinicia) = Janela("primary");
        var (semana, semanaReinicia) = Janela("secondary");
        return sessao is null && semana is null ? null : new UsoDoCodex(sessao, semana, sessaoReinicia, semanaReinicia);
    }

    static DateTimeOffset Instante(long valor) =>
        valor > 100_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(valor) : DateTimeOffset.FromUnixTimeSeconds(valor);

    static string Diffs(JsonElement mudancas)
    {
        var sb = new StringBuilder();
        foreach (var m in mudancas.EnumerateArray())
        {
            if (Texto(m, "path") is { } caminho) sb.AppendLine(caminho);
            if (Texto(m, "diff") is { } diff) sb.AppendLine(diff);
        }
        return sb.ToString().Trim();
    }

    static string ConteudoMcp(JsonElement item)
    {
        if (!item.TryGetProperty("result", out var r) || r.ValueKind != JsonValueKind.Object || !r.TryGetProperty("content", out var c) || c.ValueKind != JsonValueKind.Array) return "";
        return string.Join("\n", c.EnumerateArray().Select(b => Texto(b, "text")).OfType<string>());
    }

    static JsonElement Elemento(JsonNode no) => JsonDocument.Parse(no.ToJsonString()).RootElement.Clone();

    static string? Texto(JsonElement e, string nome) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static long? Numero(JsonElement e, string nome) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n
        : e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var d) && d.ValueKind == JsonValueKind.Number ? (long)d.GetDouble() : null;
}
