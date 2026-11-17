using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nexora.Core.Uso;

public sealed record FonteDeUso(string Agente, string Raiz, string Padrao);

public sealed class Parcela
{
    public string Agente { get; set; } = UsoLocal.Claude;
    public string Modelo { get; set; } = "";
    public string Pasta { get; set; } = "";
    public string Semana { get; set; } = "";
    public string Dia { get; set; } = "";
    public long Entrada { get; set; }
    public long Saida { get; set; }
    public long Escrita { get; set; }
    public long Lida { get; set; }
    public double Peso { get; set; }
    public int Respostas { get; set; }
    public HashSet<string> Sessoes { get; set; } = [];
    public Dictionary<string, double> Modelos { get; set; } = [];

    [JsonIgnore]
    public long Tokens => Entrada + Saida + Escrita + Lida;

    public void Somar(Parcela outra)
    {
        Entrada += outra.Entrada;
        Saida += outra.Saida;
        Escrita += outra.Escrita;
        Lida += outra.Lida;
        Peso += outra.Peso;
        Respostas += outra.Respostas;
        Sessoes.UnionWith(outra.Sessoes);
        foreach (var (familia, peso) in outra.Modelos) Modelos[familia] = Modelos.GetValueOrDefault(familia) + peso;
    }
}

public static class UsoLocal
{
    public const string Claude = "Claude";
    public const string Codex = "Codex";
    public const string Gemini = "Gemini";
    const int Versao = 3;
    const int IdsGuardados = 64;
    static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public static string RaizPadrao =>
        Historico.ConversasSalvas.PastaPadrao;

    static string Casa => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static IReadOnlyList<FonteDeUso> FontesPadrao =>
    [
        new(Claude, RaizPadrao, "*.jsonl"),
        new(Codex, Path.Combine(Environment.GetEnvironmentVariable("CODEX_HOME") is { Length: > 0 } codex ? codex : Path.Combine(Casa, ".codex"), "sessions"), "*.jsonl"),
        new(Gemini, Path.Combine(Casa, ".gemini", "tmp"), "session-*.json"),
    ];

    public static DateOnly SemanaDe(DateTime local)
    {
        var dia = DateOnly.FromDateTime(local);
        var desdeSegunda = ((int)dia.DayOfWeek + 6) % 7;
        return dia.AddDays(-desdeSegunda);
    }

    public static string Chave(DateOnly semana) => semana.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    sealed class Cache
    {
        public int Versao { get; set; }
        public Dictionary<string, Arquivo> Arquivos { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    sealed class Arquivo
    {
        public long Offset { get; set; }
        public List<string> Ultimos { get; set; } = [];
        public List<Parcela> Parcelas { get; set; } = [];
        public string? Pasta { get; set; }
        public string? Modelo { get; set; }
        public string? Sessao { get; set; }
        public long UltimoTotal { get; set; }
    }

    public static IReadOnlyList<Parcela> Ler(string raiz, string? arquivoCache, DateTime desde, CancellationToken cancelar = default) =>
        Ler([new FonteDeUso(Claude, raiz, "*.jsonl")], arquivoCache, desde, cancelar);

    public static IReadOnlyList<Parcela> Ler(IEnumerable<FonteDeUso> fontes, string? arquivoCache, DateTime desde, CancellationToken cancelar = default, bool porDia = false)
    {
        var cache = Carregar(arquivoCache);
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var limite = Chave(SemanaDe(desde));
        foreach (var fonte in fontes)
        {
            if (!Directory.Exists(fonte.Raiz)) continue;
            var pastasGemini = fonte.Agente == Gemini ? PastasDoGemini(fonte.Raiz) : null;
            List<string> arquivos;
            try
            {
                arquivos = [.. Directory.EnumerateFiles(fonte.Raiz, fonte.Padrao, SearchOption.AllDirectories)];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            foreach (var caminho in arquivos)
            {
                cancelar.ThrowIfCancellationRequested();
                FileInfo info;
                try
                {
                    info = new FileInfo(caminho);
                    if (info.LastWriteTime < desde) continue;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }
                vistos.Add(caminho);
                if (fonte.Agente == Gemini)
                {
                    if (!cache.Arquivos.TryGetValue(caminho, out var sessao) || sessao.Offset != info.Length)
                    {
                        cache.Arquivos[caminho] = sessao = new Arquivo { Offset = info.Length };
                        LerGemini(caminho, sessao, pastasGemini!);
                    }
                    continue;
                }
                if (!cache.Arquivos.TryGetValue(caminho, out var registro) || info.Length < registro.Offset)
                    cache.Arquivos[caminho] = registro = new Arquivo();
                if (info.Length > registro.Offset) LerDesde(caminho, registro, fonte.Agente);
            }
        }
        foreach (var velho in cache.Arquivos.Keys.Where(k => !vistos.Contains(k)).ToList()) cache.Arquivos.Remove(velho);
        Gravar(arquivoCache, cache);

        var juntas = new Dictionary<(string, string, string, string), Parcela>();
        foreach (var p in cache.Arquivos.Values.SelectMany(a => a.Parcelas).Where(p => string.CompareOrdinal(p.Semana, limite) >= 0))
        {
            var chave = ChaveDa(p, porDia);
            if (!juntas.TryGetValue(chave, out var soma)) juntas[chave] = soma = Nova(p.Agente, p.Modelo, p.Pasta, p.Semana, porDia ? p.Dia : "");
            soma.Somar(p);
        }
        return [.. juntas.Values];
    }

    static (string, string, string, string) ChaveDa(Parcela p, bool porDia) => (p.Agente, p.Modelo, p.Pasta.ToLowerInvariant(), porDia ? p.Dia : p.Semana);

    static Parcela Nova(string agente, string modelo, string pasta, string semana, string dia) =>
        new() { Agente = agente, Modelo = modelo, Pasta = pasta, Semana = semana, Dia = dia };

    public static string Dia(DateTime local) => DateOnly.FromDateTime(local).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    static void Acrescentar(Dictionary<(string, string, string, string), Parcela> parcelas, string agente, RespostaLida r)
    {
        var familia = PesoDoModelo.Familia(r.Modelo);
        var chave = (agente, familia, r.Pasta.ToLowerInvariant(), r.Dia);
        if (!parcelas.TryGetValue(chave, out var p)) parcelas[chave] = p = Nova(agente, familia, r.Pasta, r.Semana, r.Dia);
        p.Entrada += r.Tokens.Entrada;
        p.Saida += r.Tokens.Saida;
        p.Escrita += r.Tokens.Escrita5m + r.Tokens.Escrita1h;
        p.Lida += r.Tokens.Lida;
        var peso = PesoDoModelo.Peso(r.Modelo, r.Tokens);
        p.Peso += peso;
        p.Respostas++;
        if (r.Sessao is { } s) p.Sessoes.Add(s);
        p.Modelos[familia] = p.Modelos.GetValueOrDefault(familia) + peso;
    }

    static void LerDesde(string caminho, Arquivo registro, string agente)
    {
        byte[] bytes;
        try
        {
            using var fluxo = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            fluxo.Seek(registro.Offset, SeekOrigin.Begin);
            bytes = new byte[fluxo.Length - registro.Offset];
            var lidos = 0;
            while (lidos < bytes.Length && fluxo.Read(bytes, lidos, bytes.Length - lidos) is var n && n > 0) lidos += n;
            if (lidos < bytes.Length) Array.Resize(ref bytes, lidos);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }
        var ultimaQuebra = Array.LastIndexOf(bytes, (byte)'\n');
        if (ultimaQuebra < 0) return;
        var parcelas = registro.Parcelas.ToDictionary(p => ChaveDa(p, porDia: true));
        var ids = new HashSet<string>(registro.Ultimos);
        var recentes = new Queue<string>(registro.Ultimos);
        var texto = Encoding.UTF8.GetString(bytes, 0, ultimaQuebra + 1);
        if (agente == Codex)
        {
            foreach (var linha in texto.Split('\n'))
                if (linha.Length > 20 && RespostaDoCodex(linha, registro) is { } c) Acrescentar(parcelas, Codex, c);
            registro.Offset += ultimaQuebra + 1;
            registro.Parcelas = [.. parcelas.Values];
            return;
        }
        foreach (var linha in texto.Split('\n'))
        {
            if (linha.Length < 40 || !linha.Contains("\"usage\"", StringComparison.Ordinal) || !linha.Contains("\"assistant\"", StringComparison.Ordinal)) continue;
            if (Resposta(linha) is not { } r) continue;
            if (r.Id is { } id)
            {
                if (!ids.Add(id)) continue;
                recentes.Enqueue(id);
                while (recentes.Count > IdsGuardados) ids.Remove(recentes.Dequeue());
            }
            Acrescentar(parcelas, agente, r);
        }
        registro.Offset += ultimaQuebra + 1;
        registro.Ultimos = [.. recentes];
        registro.Parcelas = [.. parcelas.Values];
    }

    sealed record RespostaLida(string? Id, string? Modelo, string Pasta, DateTime Quando, string? Sessao, TokensDaResposta Tokens)
    {
        public string Semana => Chave(SemanaDe(Quando));
        public string Dia => UsoLocal.Dia(Quando);
    }

    static RespostaLida? Resposta(string linha)
    {
        try
        {
            using var doc = JsonDocument.Parse(linha);
            var o = doc.RootElement;
            if (!Texto(o, "type").Equals("assistant", StringComparison.Ordinal)) return null;
            if (!o.TryGetProperty("message", out var m) || !m.TryGetProperty("usage", out var u) || u.ValueKind != JsonValueKind.Object) return null;
            var modelo = m.TryGetProperty("model", out var mo) && mo.ValueKind == JsonValueKind.String ? mo.GetString() : null;
            if (modelo is null || modelo.StartsWith('<')) return null;
            if (!DateTime.TryParse(Texto(o, "timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var quando)) return null;
            long escrita5 = 0, escrita1 = 0;
            if (u.TryGetProperty("cache_creation", out var cc) && cc.ValueKind == JsonValueKind.Object)
            {
                escrita5 = Numero(cc, "ephemeral_5m_input_tokens");
                escrita1 = Numero(cc, "ephemeral_1h_input_tokens");
            }
            if (escrita5 + escrita1 == 0) escrita5 = Numero(u, "cache_creation_input_tokens");
            var tokens = new TokensDaResposta(Numero(u, "input_tokens"), Numero(u, "output_tokens"), escrita5, escrita1,
                Numero(u, "cache_read_input_tokens"), Texto(u, "speed") == "fast");
            if (tokens.Total == 0) return null;
            var id = m.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : null;
            var pasta = Texto(o, "cwd") is { Length: > 0 } c ? c.TrimEnd('\\', '/') : "?";
            var sessao = Texto(o, "sessionId") is { Length: > 0 } s ? s : null;
            return new RespostaLida(id, modelo, pasta, quando.ToLocalTime(), sessao, tokens);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static readonly char[] Barras = ['\\', '/'];

    static RespostaLida? RespostaDoCodex(string linha, Arquivo registro)
    {
        try
        {
            using var doc = JsonDocument.Parse(linha);
            var o = doc.RootElement;
            if (!o.TryGetProperty("payload", out var p) || p.ValueKind != JsonValueKind.Object) return null;
            switch (Texto(o, "type"))
            {
                case "session_meta" or "turn_context":
                    if (Texto(p, "id") is { Length: > 0 } id) registro.Sessao = id;
                    if (Texto(p, "cwd") is { Length: > 0 } cwd) registro.Pasta = cwd.TrimEnd(Barras);
                    if (Texto(p, "model") is { Length: > 0 } m) registro.Modelo = m;
                    return null;
                case "event_msg" when Texto(p, "type") == "token_count":
                    if (!p.TryGetProperty("info", out var info) || info.ValueKind != JsonValueKind.Object) return null;
                    if (!info.TryGetProperty("last_token_usage", out var u) || u.ValueKind != JsonValueKind.Object) return null;
                    if (info.TryGetProperty("total_token_usage", out var t) && t.ValueKind == JsonValueKind.Object)
                    {
                        var total = Numero(t, "total_tokens");
                        if (total > 0 && total == registro.UltimoTotal) return null;
                        registro.UltimoTotal = total;
                    }
                    if (!DateTime.TryParse(Texto(o, "timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var quando)) return null;
                    var entrada = Numero(u, "input_tokens");
                    var lida = Math.Min(entrada, Numero(u, "cached_input_tokens"));
                    var tokens = new TokensDaResposta(entrada - lida, Numero(u, "output_tokens"), 0, 0, lida);
                    if (tokens.Total == 0) return null;
                    return new RespostaLida(null, registro.Modelo ?? "gpt", registro.Pasta ?? "?", quando.ToLocalTime(), registro.Sessao, tokens);
                default:
                    return null;
            }
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static Dictionary<string, string> PastasDoGemini(string raiz)
    {
        var pastas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var arquivo = Path.Combine(Path.GetDirectoryName(raiz.TrimEnd(Barras))!, "projects.json");
            if (!File.Exists(arquivo)) return pastas;
            using var doc = JsonDocument.Parse(File.ReadAllText(arquivo));
            if (doc.RootElement.TryGetProperty("projects", out var projetos) && projetos.ValueKind == JsonValueKind.Object)
                foreach (var p in projetos.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() is { Length: > 0 } nome) pastas[nome] = p.Name;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return pastas;
    }

    static void LerGemini(string caminho, Arquivo registro, Dictionary<string, string> pastas)
    {
        var parcelas = new Dictionary<(string, string, string, string), Parcela>();
        try
        {
            using var fluxo = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var doc = JsonDocument.Parse(fluxo);
            var o = doc.RootElement;
            if (o.ValueKind != JsonValueKind.Object || !o.TryGetProperty("messages", out var mensagens) || mensagens.ValueKind != JsonValueKind.Array) return;
            var projeto = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(caminho)) ?? "") is { Length: > 0 } nome ? nome : "?";
            var pasta = pastas.TryGetValue(projeto, out var achada) ? achada.TrimEnd(Barras) : projeto;
            var sessao = Texto(o, "sessionId") is { Length: > 0 } s ? s : Path.GetFileNameWithoutExtension(caminho);
            foreach (var m in mensagens.EnumerateArray())
            {
                if (m.ValueKind != JsonValueKind.Object || Texto(m, "type") != "gemini") continue;
                if (!m.TryGetProperty("tokens", out var t) || t.ValueKind != JsonValueKind.Object) continue;
                if (!DateTime.TryParse(Texto(m, "timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var quando)) continue;
                var entrada = Numero(t, "input");
                var lida = Math.Min(entrada, Numero(t, "cached"));
                var tokens = new TokensDaResposta(entrada - lida, Numero(t, "output") + Numero(t, "thoughts"), 0, 0, lida);
                if (tokens.Total == 0) continue;
                var modelo = Texto(m, "model") is { Length: > 0 } mo ? mo : "gemini";
                Acrescentar(parcelas, Gemini, new RespostaLida(null, modelo, pasta, quando.ToLocalTime(), sessao, tokens));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        registro.Parcelas = [.. parcelas.Values];
    }

    static string Texto(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    static long Numero(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;

    static Cache Carregar(string? arquivo)
    {
        try
        {
            if (arquivo is not null && File.Exists(arquivo) && JsonSerializer.Deserialize<Cache>(File.ReadAllText(arquivo), Json) is { Versao: Versao } c)
            {
                c.Arquivos = new Dictionary<string, Arquivo>(c.Arquivos, StringComparer.OrdinalIgnoreCase);
                return c;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return new Cache { Versao = Versao };
    }

    static void Gravar(string? arquivo, Cache cache)
    {
        if (arquivo is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
            var temporario = arquivo + ".novo";
            File.WriteAllText(temporario, JsonSerializer.Serialize(cache, Json));
            File.Move(temporario, arquivo, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
