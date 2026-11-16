using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.Core.Acp;

public abstract record MensagemAcp;

public sealed record RespostaAcp(long Id, JsonElement? Resultado, string? Erro, int? Codigo = null) : MensagemAcp;

public sealed record PedidoDoAgente(JsonNode Id, string Metodo, JsonElement Parametros) : MensagemAcp;

public sealed record PermissaoDoAgente(JsonNode Id, string PedidoId, IReadOnlyList<OpcaoDePermissao> Opcoes) : MensagemAcp;

public sealed record EventoDoAcp(EventoClaude Evento) : MensagemAcp;

public sealed record AgenteParou(string? Motivo, string? Erro) : MensagemAcp;

public sealed record LinkDeLogin(string Url) : MensagemAcp;

public sealed record OpcaoDePermissao(string Id, string Nome, string Tipo);

public sealed record MetodoDeLogin(string Id, string? Nome, string? Tipo);

public sealed record CapacidadesAcp(
    int Geracao,
    string? Agente,
    bool CarregarSessao,
    bool RetomarSessao,
    bool Imagem,
    bool McpHttp,
    bool McpSse,
    bool Diretorios,
    IReadOnlyList<MetodoDeLogin> Logins);

public sealed record ModoAcp(string Id, string Nome);

public sealed record ValorDeConfiguracao(string Valor, string Nome, string? Descricao);

public sealed record ConfiguracaoAcp(string Id, string? Categoria, string? Atual, IReadOnlyList<ValorDeConfiguracao> Valores);

public sealed class ProtocoloAcp
{
    public const string MarcaDaSessao = """[{"type":"acpSession"}]""";
    public const string PrefixoDoLink = "Open the following link to authenticate the ACP server: ";
    public const int ErroDeLogin = -32000;
    public const int MetodoDesconhecido = -32601;

    public static string ErroDeLoginComDetalhe(string nome, string? erro) =>
        string.IsNullOrWhiteSpace(erro) || erro.Contains("auth", StringComparison.OrdinalIgnoreCase) || erro.Contains("login", StringComparison.OrdinalIgnoreCase)
            ? Idioma.F(TextosAcp.FacaLogin, nome)
            : Idioma.F(TextosAcp.DisseOAgente, nome, erro.Trim());

    public static string ExplicarErros(string erros, string nome) =>
        erros.Contains("unexpected argument", StringComparison.OrdinalIgnoreCase) || erros.Contains("unknown option", StringComparison.OrdinalIgnoreCase) || erros.Contains("unknown command", StringComparison.OrdinalIgnoreCase)
            ? Idioma.F(TextosAcp.AgenteAntigo, nome) + "\n\n" + erros
            : erros;

    static readonly JsonSerializerOptions Compacto = new() { WriteIndented = false };

    readonly StringBuilder _texto = new();
    readonly StringBuilder _usuario = new();
    readonly HashSet<string> _ferramentas = [];
    readonly Dictionary<string, JsonElement> _conteudos = [];
    bool _pensando;
    int _planos;

    public int Geracao { get; set; } = 1;
    public string? SessaoId { get; set; }
    public string? Modelo { get; set; }
    public string? Modo { get; set; }
    public long? JanelaDeContexto { get; private set; }
    public bool Repetindo { get; set; }
    public IReadOnlyList<ModoAcp> Modos { get; private set; } = [];
    public IReadOnlyList<ConfiguracaoAcp> Configuracoes { get; private set; } = [];
    public IReadOnlyList<ModeloDisponivel> Modelos { get; private set; } = [];
    public IReadOnlyList<ComandoBarra> Comandos { get; private set; } = [];

    public static string Pedido(long id, string metodo, JsonObject? parametros = null)
    {
        var o = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = metodo };
        if (parametros is not null) o["params"] = parametros;
        return o.ToJsonString(Compacto);
    }

    public static string Aviso(string metodo, JsonObject? parametros = null) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["method"] = metodo, ["params"] = parametros ?? [] }.ToJsonString(Compacto);

    public static string Resposta(JsonNode id, JsonNode resultado) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id.DeepClone(), ["result"] = resultado.DeepClone() }.ToJsonString(Compacto);

    public static string Recusa(JsonNode id, string mensagem, int codigo = MetodoDesconhecido) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id.DeepClone(), ["error"] = new JsonObject { ["code"] = codigo, ["message"] = mensagem } }.ToJsonString(Compacto);

    public static JsonObject Inicializar(string versao) => new()
    {
        ["protocolVersion"] = 1,
        ["clientInfo"] = new JsonObject { ["name"] = "nexora", ["title"] = "Nexora", ["version"] = versao },
        ["clientCapabilities"] = new JsonObject
        {
            ["fs"] = new JsonObject { ["readTextFile"] = true, ["writeTextFile"] = false },
            ["terminal"] = false,
        },
    };

    public static CapacidadesAcp Capacidades(JsonElement resposta)
    {
        if (resposta.ValueKind != JsonValueKind.Object) return new CapacidadesAcp(1, null, false, false, false, false, false, false, []);
        var logins = new List<MetodoDeLogin>();
        if (resposta.TryGetProperty("authMethods", out var metodos) && metodos.ValueKind == JsonValueKind.Array)
            foreach (var m in metodos.EnumerateArray())
                if ((Texto(m, "methodId") ?? Texto(m, "id")) is { } id) logins.Add(new MetodoDeLogin(id, Texto(m, "name"), Texto(m, "type")));
        if (resposta.TryGetProperty("info", out var info))
        {
            var capacidades = Objeto(resposta, "capabilities");
            var sessao = Objeto(capacidades, "session");
            var temSessao = sessao.ValueKind == JsonValueKind.Object;
            var prompt = Objeto(sessao, "prompt");
            var mcp = Objeto(sessao, "mcp");
            return new CapacidadesAcp(2, Texto(info, "title") ?? Texto(info, "name"), temSessao, temSessao,
                Presente(prompt, "image"), Presente(mcp, "http"), Presente(mcp, "sse"), Presente(sessao, "additionalDirectories"), logins);
        }
        var agente = Objeto(resposta, "agentCapabilities");
        var promptV1 = Objeto(agente, "promptCapabilities");
        var mcpV1 = Objeto(agente, "mcpCapabilities");
        var sessaoV1 = Objeto(agente, "sessionCapabilities");
        var infoV1 = Objeto(resposta, "agentInfo");
        return new CapacidadesAcp(1, Texto(infoV1, "title") ?? Texto(infoV1, "name"), Verdade(agente, "loadSession"), Presente(sessaoV1, "resume"),
            Verdade(promptV1, "image"), Verdade(mcpV1, "http"), Verdade(mcpV1, "sse"), Presente(sessaoV1, "additionalDirectories"), logins);
    }

    public static string Metodo(string metodo, int geracao) => geracao < 2 ? metodo : metodo switch
    {
        "authenticate" => "auth/login",
        "session/load" => "session/resume",
        _ => metodo,
    };

    public static MetodoDeLogin? LoginDoAgente(CapacidadesAcp capacidades) =>
        capacidades.Logins.FirstOrDefault(m => m.Tipo is null or "agent");

    public static JsonArray ServidoresMcp(string? mcp, CapacidadesAcp capacidades)
    {
        var lista = new JsonArray();
        if (string.IsNullOrWhiteSpace(mcp)) return lista;
        JsonNode? raiz;
        try
        {
            raiz = JsonNode.Parse(mcp);
        }
        catch (JsonException)
        {
            return lista;
        }
        if (raiz?["mcpServers"] is not JsonObject servidores) return lista;
        foreach (var (nome, no) in servidores)
        {
            if (no is not JsonObject cfg) continue;
            var url = Cadeia(cfg["url"]);
            var tipo = Cadeia(cfg["type"]) ?? (url is not null ? "http" : "stdio");
            if (tipo == "stdio")
            {
                if (Cadeia(cfg["command"]) is not { Length: > 0 } comando) continue;
                var servidor = new JsonObject();
                if (capacidades.Geracao >= 2) servidor["type"] = "stdio";
                servidor["name"] = nome;
                servidor["command"] = comando;
                servidor["args"] = new JsonArray([.. (cfg["args"] as JsonArray ?? []).Select(a => (JsonNode?)JsonValue.Create(Cadeia(a) ?? a?.ToJsonString() ?? ""))]);
                servidor["env"] = Pares(cfg["env"]);
                lista.Add(servidor);
            }
            else if ((tipo == "http" && capacidades.McpHttp) || (tipo == "sse" && capacidades.McpSse))
            {
                if (url is null) continue;
                lista.Add(new JsonObject { ["type"] = tipo, ["name"] = nome, ["url"] = url, ["headers"] = Pares(cfg["headers"]) });
            }
        }
        return lista;
    }

    static JsonArray Pares(JsonNode? no)
    {
        var lista = new JsonArray();
        if (no is JsonObject o)
            foreach (var (nome, valor) in o) lista.Add(new JsonObject { ["name"] = nome, ["value"] = Cadeia(valor) ?? valor?.ToJsonString() ?? "" });
        return lista;
    }

    public static JsonObject NovaSessao(string pasta, JsonArray mcp, IReadOnlyList<string>? diretorios, CapacidadesAcp capacidades)
    {
        var p = new JsonObject { ["cwd"] = pasta, ["mcpServers"] = mcp };
        if (capacidades.Diretorios && diretorios is { Count: > 0 }) p["additionalDirectories"] = new JsonArray([.. diretorios.Select(d => (JsonNode?)JsonValue.Create(d))]);
        return p;
    }

    public static JsonObject CarregarSessao(string sessao, string pasta, JsonArray mcp, IReadOnlyList<string>? diretorios, CapacidadesAcp capacidades)
    {
        var p = NovaSessao(pasta, mcp, diretorios, capacidades);
        p["sessionId"] = sessao;
        if (capacidades.Geracao >= 2) p["replayFrom"] = new JsonObject { ["type"] = "start" };
        return p;
    }

    public static JsonObject RetomarSessao(string sessao, string pasta, JsonArray mcp, IReadOnlyList<string>? diretorios, CapacidadesAcp capacidades)
    {
        var p = NovaSessao(pasta, mcp, diretorios, capacidades);
        p["sessionId"] = sessao;
        return p;
    }

    public static JsonObject Prompt(string sessao, string? extra, string texto, IReadOnlyList<Anexo> imagens, bool aceitaImagens)
    {
        var blocos = new JsonArray();
        if (!string.IsNullOrEmpty(extra)) blocos.Add(new JsonObject { ["type"] = "text", ["text"] = extra });
        if (texto.Length > 0 || imagens.Count == 0 || !aceitaImagens) blocos.Add(new JsonObject { ["type"] = "text", ["text"] = texto });
        if (aceitaImagens)
            foreach (var imagem in imagens)
                blocos.Add(new JsonObject { ["type"] = "image", ["mimeType"] = imagem.TipoMidia, ["data"] = Convert.ToBase64String(imagem.Dados) });
        return new JsonObject { ["sessionId"] = sessao, ["prompt"] = blocos };
    }

    public static JsonObject Cancelado() => new() { ["outcome"] = new JsonObject { ["outcome"] = "cancelled" } };

    public static JsonObject Escolha(IReadOnlyList<OpcaoDePermissao> opcoes, bool permitir, bool sempre)
    {
        OpcaoDePermissao? escolhida = permitir
            ? (sempre ? opcoes.FirstOrDefault(o => o.Tipo == "allow_always") : null) ?? opcoes.FirstOrDefault(o => o.Tipo == "allow_once") ?? opcoes.FirstOrDefault(o => o.Tipo == "allow_always")
            : opcoes.FirstOrDefault(o => o.Tipo == "reject_once") ?? opcoes.FirstOrDefault(o => o.Tipo == "reject_always");
        return escolhida is null ? Cancelado() : new JsonObject { ["outcome"] = new JsonObject { ["outcome"] = "selected", ["optionId"] = escolhida.Id } };
    }

    static readonly (string Modo, string[] Nomes)[] Equivalentes =
    [
        ("plan", ["plan", "architect"]),
        ("bypassPermissions", ["yolo", "bypasspermissions", "fullaccess"]),
        ("acceptEdits", ["acceptedits", "autoedit"]),
        ("auto", ["auto"]),
        ("manual", ["manual"]),
        ("default", ["default", "code", "agent"]),
    ];

    static string Normalizado(string valor) => new([.. valor.ToLowerInvariant().Where(char.IsLetterOrDigit)]);

    public static string? MelhorModo(string? modo, IReadOnlyList<ModoAcp> modos)
    {
        if (modos.Count == 0) return null;
        var chave = string.IsNullOrEmpty(modo) ? "default" : modo;
        string? Procurar(string[] nomes)
        {
            foreach (var nome in nomes)
                foreach (var m in modos)
                    if (Normalizado(m.Id) == nome || Normalizado(m.Nome) == nome) return m.Id;
            return null;
        }
        var nomes = Equivalentes.FirstOrDefault(e => e.Modo == chave).Nomes ?? [Normalizado(chave)];
        return Procurar(nomes) ?? (chave == "manual" ? Procurar(Equivalentes[^1].Nomes) : null);
    }

    public ConfiguracaoAcp? ConfiguracaoDe(string categoria) =>
        Configuracoes.FirstOrDefault(c => c.Categoria == categoria) ?? Configuracoes.FirstOrDefault(c => c.Id == categoria);

    JsonObject Configurar(string configuracao, string valor)
    {
        var p = new JsonObject { ["sessionId"] = SessaoId, ["configId"] = configuracao, ["value"] = valor };
        if (Geracao >= 2) p["type"] = "id";
        return p;
    }

    public (string Metodo, JsonObject Parametros)? PedidoDeModo(string? modo)
    {
        var configuracao = ConfiguracaoDe("mode");
        var modos = Modos.Count > 0 ? Modos : configuracao is null ? [] : [.. configuracao.Valores.Select(v => new ModoAcp(v.Valor, v.Nome))];
        if (MelhorModo(modo, modos) is not { } id || id == Modo) return null;
        if (Geracao < 2 && Modos.Count > 0) return ("session/set_mode", new JsonObject { ["sessionId"] = SessaoId, ["modeId"] = id });
        return configuracao is null ? null : ("session/set_config_option", Configurar(configuracao.Id, id));
    }

    public (string Metodo, JsonObject Parametros)? PedidoDeModelo(string? modelo)
    {
        if (string.IsNullOrEmpty(modelo) || modelo == Modelo) return null;
        if (ConfiguracaoDe("model") is { } configuracao) return ("session/set_config_option", Configurar(configuracao.Id, modelo));
        return Geracao < 2 ? ("session/set_model", new JsonObject { ["sessionId"] = SessaoId, ["modelId"] = modelo }) : null;
    }

    public IReadOnlyList<EventoClaude> LerSessao(JsonElement resultado)
    {
        if (resultado.ValueKind != JsonValueKind.Object) return [];
        if (Texto(resultado, "sessionId") is { } id) SessaoId = id;
        var modelos = Objeto(resultado, "models");
        if (Texto(modelos, "currentModelId") is { } atual) Modelo = atual;
        if (modelos.ValueKind == JsonValueKind.Object && modelos.TryGetProperty("availableModels", out var lista) && lista.ValueKind == JsonValueKind.Array)
            Modelos = [.. lista.EnumerateArray()
                .Where(m => Texto(m, "modelId") is not null)
                .Select(m => new ModeloDisponivel(Texto(m, "modelId")!, Texto(m, "name") ?? Texto(m, "modelId")!, Texto(m, "description"), null, []))];
        var modos = Objeto(resultado, "modes");
        if (Texto(modos, "currentModeId") is { } modo) Modo = modo;
        if (modos.ValueKind == JsonValueKind.Object && modos.TryGetProperty("availableModes", out var disponiveis) && disponiveis.ValueKind == JsonValueKind.Array)
            Modos = [.. disponiveis.EnumerateArray()
                .Where(m => Texto(m, "id") is not null)
                .Select(m => new ModoAcp(Texto(m, "id")!, Texto(m, "name") ?? Texto(m, "id")!))];
        if (resultado.TryGetProperty("configOptions", out var opcoes)) LerConfiguracoes(opcoes);
        return Modelos.Count > 0 ? [new ComandosDisponiveis(Comandos, false, Modelos)] : [];
    }

    public void LerConfiguracoes(JsonElement opcoes)
    {
        if (opcoes.ValueKind != JsonValueKind.Array) return;
        var lista = new List<ConfiguracaoAcp>();
        foreach (var o in opcoes.EnumerateArray())
        {
            if ((Texto(o, "configId") ?? Texto(o, "id")) is not { } id) continue;
            var valores = new List<ValorDeConfiguracao>();
            if (o.TryGetProperty("options", out var itens) && itens.ValueKind == JsonValueKind.Array)
                foreach (var item in itens.EnumerateArray())
                {
                    if (item.TryGetProperty("options", out var grupo) && grupo.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var g in grupo.EnumerateArray())
                            if (Texto(g, "value") is { } v) valores.Add(new ValorDeConfiguracao(v, Texto(g, "name") ?? v, Texto(g, "description")));
                    }
                    else if (Texto(item, "value") is { } valor) valores.Add(new ValorDeConfiguracao(valor, Texto(item, "name") ?? valor, Texto(item, "description")));
                }
            lista.Add(new ConfiguracaoAcp(id, Texto(o, "category"), Texto(o, "currentValue"), valores));
        }
        Configuracoes = lista;
        if (ConfiguracaoDe("model") is { } modelo)
        {
            Modelo = modelo.Atual ?? Modelo;
            if (modelo.Valores.Count > 0) Modelos = [.. modelo.Valores.Select(v => new ModeloDisponivel(v.Valor, v.Nome, v.Descricao, null, []))];
        }
        if (ConfiguracaoDe("mode") is { } modo) Modo = modo.Atual ?? Modo;
    }

    public IReadOnlyList<MensagemAcp> Ler(string linha)
    {
        if (string.IsNullOrWhiteSpace(linha)) return [];
        var limpa = linha.Trim();
        if (limpa.StartsWith(PrefixoDoLink, StringComparison.Ordinal)) return [new LinkDeLogin(limpa[PrefixoDoLink.Length..].Trim())];
        if (!limpa.StartsWith('{')) return [];
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(limpa);
        }
        catch (JsonException)
        {
            return [];
        }
        using (doc)
        {
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return [];
            var temId = r.TryGetProperty("id", out var id) && id.ValueKind is JsonValueKind.Number or JsonValueKind.String;
            var metodo = Texto(r, "method");
            var p = r.TryGetProperty("params", out var parametros) ? parametros.Clone() : default;
            if (metodo is null)
            {
                if (!temId) return [];
                long numero;
                if (id.ValueKind == JsonValueKind.Number) numero = id.GetInt64();
                else if (!long.TryParse(id.GetString(), out numero)) return [];
                if (r.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Object)
                {
                    var mensagem = Texto(e, "message") ?? Idioma.T(TextosAcp.Recusou);
                    if (e.TryGetProperty("data", out var dados))
                    {
                        var detalhe = dados.ValueKind == JsonValueKind.String ? dados.GetString() : Texto(dados, "details") ?? Texto(dados, "message");
                        if (detalhe is { Length: > 0 } && !mensagem.Contains(detalhe, StringComparison.Ordinal)) mensagem = $"{mensagem}: {detalhe}";
                    }
                    var codigo = e.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number && c.TryGetInt32(out var n) ? n : (int?)null;
                    return [new RespostaAcp(numero, null, mensagem, codigo)];
                }
                JsonElement? resultado = r.TryGetProperty("result", out var res) ? res.Clone() : null;
                return [new RespostaAcp(numero, resultado, null)];
            }
            if (temId) return Pedido(JsonNode.Parse(id.GetRawText())!, metodo, p);
            return Notificacao(metodo, p);
        }
    }

    IReadOnlyList<MensagemAcp> Pedido(JsonNode id, string metodo, JsonElement p)
    {
        if (metodo != "session/request_permission") return [new PedidoDoAgente(id, metodo, p)];
        var pedidoId = id.ToJsonString();
        var opcoes = new List<OpcaoDePermissao>();
        if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("options", out var lista) && lista.ValueKind == JsonValueKind.Array)
            foreach (var o in lista.EnumerateArray())
                if (Texto(o, "optionId") is { } opcao) opcoes.Add(new OpcaoDePermissao(opcao, Texto(o, "name") ?? opcao, Texto(o, "kind") ?? ""));
        var chamada = Objeto(p, "toolCall");
        if (chamada.ValueKind != JsonValueKind.Object)
        {
            var sujeito = Objeto(p, "subject");
            chamada = Objeto(sujeito, "toolCall");
            if (chamada.ValueKind != JsonValueKind.Object)
            {
                var falsa = new JsonObject { ["title"] = Texto(p, "title") ?? "", ["kind"] = Texto(sujeito, "type") == "command" ? "execute" : "other" };
                if (Texto(sujeito, "command") is { } comando) falsa["rawInput"] = new JsonObject { ["command"] = comando };
                chamada = Elemento(falsa);
            }
        }
        var (nome, entrada) = Ferramenta(chamada);
        var titulo = Texto(chamada, "title") ?? Texto(p, "title");
        var sugestoes = opcoes.Any(o => o.Tipo == "allow_always") ? Elemento(JsonNode.Parse(MarcaDaSessao)!) : (JsonElement?)null;
        var eventos = new List<MensagemAcp> { new PermissaoDoAgente(id, pedidoId, opcoes) };
        eventos.AddRange(Descarregar().Select(e => new EventoDoAcp(e)));
        _pensando = false;
        eventos.Add(new EventoDoAcp(new PermissaoPedida(pedidoId, nome, Elemento(entrada), titulo, Texto(p, "description"), sugestoes)));
        return eventos;
    }

    IReadOnlyList<MensagemAcp> Notificacao(string metodo, JsonElement p)
    {
        if (metodo != "session/update") return [];
        var u = Objeto(p, "update");
        if (u.ValueKind != JsonValueKind.Object) return [];
        if (Texto(u, "sessionUpdate") == "state_update")
        {
            if (Texto(u, "state") != "idle") return [];
            var erro = Objeto(u, "error");
            return [new AgenteParou(Texto(u, "stopReason"), Texto(erro, "message"))];
        }
        return [.. Atualizar(u).Select(e => new EventoDoAcp(e))];
    }

    public IEnumerable<EventoClaude> Descarregar()
    {
        if (_texto.Length > 0)
        {
            yield return new TextoCompleto(_texto.ToString());
            _texto.Clear();
        }
        if (_usuario.Length > 0)
        {
            yield return new MensagemDoUsuario(_usuario.ToString());
            _usuario.Clear();
        }
    }

    IEnumerable<EventoClaude> DescarregarUsuario()
    {
        if (_usuario.Length == 0) yield break;
        yield return new MensagemDoUsuario(_usuario.ToString());
        _usuario.Clear();
    }

    IEnumerable<EventoClaude> Atualizar(JsonElement u)
    {
        switch (Texto(u, "sessionUpdate"))
        {
            case "agent_message_chunk":
            {
                foreach (var e in DescarregarUsuario()) yield return e;
                _pensando = false;
                if (TextoDoBloco(Objeto(u, "content")) is { Length: > 0 } texto)
                {
                    _texto.Append(texto);
                    yield return new TextoParcial(texto);
                }
                break;
            }
            case "agent_thought_chunk":
            {
                if (TextoDoBloco(Objeto(u, "content")) is not { Length: > 0 } pensamento) break;
                if (!_pensando)
                {
                    foreach (var e in Descarregar()) yield return e;
                    _pensando = true;
                    yield return new PensamentoComecou();
                }
                yield return new PensamentoParcial(pensamento);
                break;
            }
            case "user_message_chunk":
            {
                if (!Repetindo) break;
                if (_texto.Length > 0)
                {
                    yield return new TextoCompleto(_texto.ToString());
                    _texto.Clear();
                }
                _pensando = false;
                if (TextoDoBloco(Objeto(u, "content")) is { Length: > 0 } texto) _usuario.Append(texto);
                break;
            }
            case "tool_call":
            {
                foreach (var e in Descarregar()) yield return e;
                _pensando = false;
                var id = Texto(u, "toolCallId") ?? "";
                var (nome, entrada) = Ferramenta(u);
                _ferramentas.Add(id);
                if (u.TryGetProperty("content", out var conteudo) && conteudo.ValueKind == JsonValueKind.Array && conteudo.GetArrayLength() > 0) _conteudos[id] = conteudo.Clone();
                yield return new FerramentaChamada(id, nome, Elemento(entrada));
                if (Texto(u, "status") is "completed" or "failed") yield return Respondeu(id, u);
                break;
            }
            case "tool_call_update":
            {
                var id = Texto(u, "toolCallId") ?? "";
                var status = Texto(u, "status");
                if (u.TryGetProperty("content", out var conteudo) && conteudo.ValueKind == JsonValueKind.Array && conteudo.GetArrayLength() > 0) _conteudos[id] = conteudo.Clone();
                if (!_ferramentas.Contains(id) && (Texto(u, "title") is not null || Texto(u, "kind") is not null || status is "completed" or "failed"))
                {
                    foreach (var e in Descarregar()) yield return e;
                    _pensando = false;
                    var (nome, entrada) = Ferramenta(u);
                    _ferramentas.Add(id);
                    yield return new FerramentaChamada(id, nome, Elemento(entrada));
                }
                if (status is "completed" or "failed") yield return Respondeu(id, u);
                break;
            }
            case "plan":
            {
                foreach (var e in Descarregar()) yield return e;
                _pensando = false;
                var todos = new JsonArray();
                if (u.TryGetProperty("entries", out var entradas) && entradas.ValueKind == JsonValueKind.Array)
                    foreach (var item in entradas.EnumerateArray())
                    {
                        var conteudo = Texto(item, "content") ?? "";
                        todos.Add(new JsonObject { ["content"] = conteudo, ["activeForm"] = conteudo, ["status"] = Texto(item, "status") ?? "pending" });
                    }
                var id = $"acp-plano-{++_planos}";
                yield return new FerramentaChamada(id, "TodoWrite", Elemento(new JsonObject { ["todos"] = todos }));
                yield return new FerramentaRespondeu(id, "", false);
                break;
            }
            case "available_commands_update":
            {
                if (u.TryGetProperty("availableCommands", out var comandos) && comandos.ValueKind == JsonValueKind.Array)
                    Comandos = [.. comandos.EnumerateArray()
                        .Where(c => Texto(c, "name") is { Length: > 0 })
                        .Select(c => new ComandoBarra(Texto(c, "name")!.TrimStart('/'), Texto(c, "description") is { Length: > 0 } d ? d : null, Texto(Objeto(c, "input"), "hint") is { Length: > 0 } h ? h : null))];
                yield return new ComandosDisponiveis(Comandos, false, Modelos.Count > 0 ? Modelos : null);
                break;
            }
            case "current_mode_update":
                if (Texto(u, "currentModeId") is { } modo) Modo = modo;
                break;
            case "config_option_update":
                if (u.TryGetProperty("configOptions", out var opcoes)) LerConfiguracoes(opcoes);
                break;
            case "usage_update":
            {
                if (Numero(u, "size") is { } janela && janela > 0) JanelaDeContexto = janela;
                if (Numero(u, "used") is { } usado) yield return new ContextoUsado(usado, null, Modelo, DateTimeOffset.Now);
                break;
            }
        }
    }

    FerramentaRespondeu Respondeu(string id, JsonElement u)
    {
        var falhou = Texto(u, "status") == "failed";
        var conteudo = u.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.Array && c.GetArrayLength() > 0 ? c
            : _conteudos.TryGetValue(id, out var guardado) ? guardado : default;
        var texto = ConteudoDaFerramenta(conteudo);
        if (texto.Length == 0 && u.TryGetProperty("rawOutput", out var bruto)) texto = TextoBruto(bruto);
        _conteudos.Remove(id);
        _ferramentas.Remove(id);
        return new FerramentaRespondeu(id, texto, falhou);
    }

    public IReadOnlyList<EventoClaude> Fim(string? motivo, string? erro, long duracaoMs)
    {
        var eventos = Descarregar().ToList();
        _pensando = false;
        IReadOnlyDictionary<string, long>? janelas = Modelo is { Length: > 0 } m && JanelaDeContexto is { } j ? new Dictionary<string, long> { [m] = j } : null;
        var fim = motivo switch
        {
            "cancelled" => new TurnoEncerrado(true, Idioma.T("O turno foi interrompido."), null, duracaoMs, janelas),
            "refusal" => new TurnoEncerrado(true, Idioma.T(TextosAcp.Recusa), null, duracaoMs, janelas),
            "max_tokens" => new TurnoEncerrado(true, Idioma.T(TextosAcp.LimiteDeTokens), null, duracaoMs, janelas),
            "max_turn_requests" => new TurnoEncerrado(true, Idioma.T(TextosAcp.LimiteDePassos), null, duracaoMs, janelas),
            _ when erro is not null => new TurnoEncerrado(true, erro, null, duracaoMs, janelas),
            _ => new TurnoEncerrado(false, null, null, duracaoMs, janelas),
        };
        eventos.Add(fim);
        return eventos;
    }

    public static (string Nome, JsonObject Entrada) Ferramenta(JsonElement chamada)
    {
        var titulo = Texto(chamada, "title");
        var bruto = Objeto(chamada, "rawInput");
        var caminho = Caminho(chamada, bruto);
        switch (Texto(chamada, "kind"))
        {
            case "execute":
            {
                var entrada = bruto.ValueKind == JsonValueKind.Object ? (JsonObject)JsonNode.Parse(bruto.GetRawText())! : new JsonObject();
                entrada["command"] = Comando(bruto) ?? titulo ?? "";
                if (titulo is not null) entrada["description"] = titulo;
                return ("Bash", entrada);
            }
            case "edit":
            {
                if (Diff(chamada) is { } diff)
                {
                    if (diff.Depois is not null && string.IsNullOrEmpty(diff.Antes))
                        return ("Write", new JsonObject { ["file_path"] = diff.Caminho, ["content"] = diff.Depois });
                    var edicao = new JsonObject { ["file_path"] = diff.Caminho };
                    if (diff.Depois is not null)
                    {
                        edicao["old_string"] = diff.Antes;
                        edicao["new_string"] = diff.Depois;
                    }
                    return ("Edit", edicao);
                }
                var entrada = new JsonObject { ["file_path"] = caminho ?? "" };
                if (titulo is not null) entrada["description"] = titulo;
                return ("Edit", entrada);
            }
            case "read":
                return ("Read", new JsonObject { ["file_path"] = caminho ?? titulo ?? "" });
            case "search":
            {
                var entrada = new JsonObject { ["pattern"] = Texto(bruto, "pattern") ?? Texto(bruto, "query") ?? titulo ?? "" };
                if (caminho is not null) entrada["path"] = caminho;
                return ("Grep", entrada);
            }
            case "fetch":
            {
                var entrada = new JsonObject { ["prompt"] = titulo ?? "" };
                var url = Texto(bruto, "url") ?? (titulo is { } t && t.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? t : null);
                if (url is not null) entrada["url"] = url;
                return ("WebFetch", entrada);
            }
            default:
                return (titulo is { Length: > 0 } ? titulo : Texto(chamada, "kind") ?? "Tool", new JsonObject { ["description"] = titulo ?? "" });
        }
    }

    static string? Comando(JsonElement bruto)
    {
        if (bruto.ValueKind != JsonValueKind.Object) return null;
        foreach (var nome in new[] { "command", "cmd" })
            if (bruto.TryGetProperty(nome, out var c))
            {
                if (c.ValueKind == JsonValueKind.String) return c.GetString();
                if (c.ValueKind == JsonValueKind.Array) return string.Join(" ", c.EnumerateArray().Select(a => a.ValueKind == JsonValueKind.String ? a.GetString() : a.GetRawText()));
            }
        return null;
    }

    static string? Caminho(JsonElement chamada, JsonElement bruto)
    {
        if (chamada.TryGetProperty("locations", out var locais) && locais.ValueKind == JsonValueKind.Array)
            foreach (var l in locais.EnumerateArray())
                if (Texto(l, "path") is { Length: > 0 } caminho) return caminho;
        foreach (var nome in new[] { "file_path", "path", "abs_path", "absolute_path", "filePath" })
            if (Texto(bruto, nome) is { Length: > 0 } caminho) return caminho;
        return null;
    }

    static (string Caminho, string? Antes, string? Depois)? Diff(JsonElement chamada)
    {
        if (!chamada.TryGetProperty("content", out var conteudo) || conteudo.ValueKind != JsonValueKind.Array) return null;
        foreach (var bloco in conteudo.EnumerateArray())
        {
            if (Texto(bloco, "type") != "diff") continue;
            if (Texto(bloco, "path") is { } caminho) return (caminho, Texto(bloco, "oldText"), Texto(bloco, "newText"));
            if (bloco.TryGetProperty("changes", out var mudancas) && mudancas.ValueKind == JsonValueKind.Array)
                foreach (var m in mudancas.EnumerateArray())
                    if (Texto(m, "path") is { } outro) return (outro, null, null);
        }
        return null;
    }

    static string ConteudoDaFerramenta(JsonElement conteudo)
    {
        if (conteudo.ValueKind != JsonValueKind.Array) return "";
        var sb = new StringBuilder();
        foreach (var bloco in conteudo.EnumerateArray())
            switch (Texto(bloco, "type"))
            {
                case "content":
                    if (TextoDoBloco(Objeto(bloco, "content")) is { Length: > 0 } texto) sb.AppendLine(texto);
                    break;
                case "diff":
                    if (Texto(bloco, "path") is { } caminho)
                    {
                        sb.AppendLine(caminho);
                        foreach (var linha in Linhas(Texto(bloco, "oldText"))) sb.Append('-').AppendLine(linha);
                        foreach (var linha in Linhas(Texto(bloco, "newText"))) sb.Append('+').AppendLine(linha);
                    }
                    else if (bloco.TryGetProperty("changes", out var mudancas) && mudancas.ValueKind == JsonValueKind.Array)
                        foreach (var m in mudancas.EnumerateArray())
                            if (Texto(m, "path") is { } outro) sb.AppendLine($"{Texto(m, "operation")} {outro}".Trim());
                    break;
            }
        return sb.ToString().TrimEnd();
    }

    static IEnumerable<string> Linhas(string? texto) =>
        string.IsNullOrEmpty(texto) ? [] : texto.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

    static string TextoBruto(JsonElement bruto) => bruto.ValueKind switch
    {
        JsonValueKind.String => bruto.GetString() ?? "",
        JsonValueKind.Null or JsonValueKind.Undefined => "",
        JsonValueKind.Object => Texto(bruto, "output") ?? Texto(bruto, "stdout") ?? Texto(bruto, "content") ?? bruto.GetRawText(),
        _ => bruto.GetRawText(),
    };

    static string? TextoDoBloco(JsonElement bloco) => Texto(bloco, "type") switch
    {
        "text" => Texto(bloco, "text"),
        "resource_link" => Texto(bloco, "uri"),
        "resource" => Texto(Objeto(bloco, "resource"), "text"),
        _ => null,
    };

    public static (JsonObject? Resultado, string? Erro) LerArquivo(JsonElement parametros, IReadOnlyList<string> raizes)
    {
        if (Texto(parametros, "path") is not { Length: > 0 } caminho) return (null, Idioma.F(TextosAcp.NaoLeu, "?", "path"));
        string completo;
        try
        {
            completo = Path.GetFullPath(caminho);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return (null, Idioma.F(TextosAcp.NaoLeu, caminho, ex.Message));
        }
        if (!raizes.Any(r => Dentro(completo, r))) return (null, Idioma.F(TextosAcp.ForaDaPasta, caminho));
        try
        {
            var texto = File.ReadAllText(completo);
            var linha = Numero(parametros, "line");
            var limite = Numero(parametros, "limit");
            if (linha is not null || limite is not null)
            {
                var linhas = texto.Split('\n');
                var inicio = (int)Math.Clamp((linha ?? 1) - 1, 0, linhas.Length);
                var quantas = (int)Math.Clamp(limite ?? linhas.Length, 0, linhas.Length - inicio);
                texto = string.Join('\n', linhas, inicio, quantas);
            }
            return (new JsonObject { ["content"] = texto }, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, Idioma.F(TextosAcp.NaoLeu, caminho, ex.Message));
        }
    }

    static bool Dentro(string caminho, string raiz)
    {
        string pasta;
        try
        {
            pasta = Path.GetFullPath(raiz).TrimEnd('\\', '/');
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
        var comparacao = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return caminho.Equals(pasta, comparacao)
            || caminho.StartsWith(pasta + Path.DirectorySeparatorChar, comparacao)
            || caminho.StartsWith(pasta + Path.AltDirectorySeparatorChar, comparacao);
    }

    static JsonElement Elemento(JsonNode no) => JsonDocument.Parse(no.ToJsonString()).RootElement.Clone();

    static JsonElement Objeto(JsonElement e, string nome) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Object ? v : default;

    static bool Presente(JsonElement e, string nome) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) && v.ValueKind is not (JsonValueKind.Null or JsonValueKind.False or JsonValueKind.Undefined);

    static bool Verdade(JsonElement e, string nome) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.True;

    static string? Cadeia(JsonNode? no) => no is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    static string? Texto(JsonElement e, string nome) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static long? Numero(JsonElement e, string nome) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number
            ? (v.TryGetInt64(out var n) ? n : (long)v.GetDouble())
            : null;
}
