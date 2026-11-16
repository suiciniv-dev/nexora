using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nexora.Core.Avatares;

public sealed record OlhosAvatar(double X, double Y, double Espacamento, double Escala);

public sealed record DefinicaoAvatar(string Id, string Nome, string Caminho, OlhosAvatar Olhos);

public sealed record SubcaminhoDoCorpo(string D, List<Segmento> Segmentos, List<Ponto[]> Polis, bool Buraco, Ponto? Sonda);

public sealed record AnaliseDoCorpo(List<Ponto[]> Polis, Limites Limites, int Area, List<SubcaminhoDoCorpo> Subcaminhos);

public readonly record struct AlcanceDoOlhar(double Esquerda, double Direita, double Cima, double Baixo);

public static partial class Corpo
{
    public const double OlhoL = 4;
    public const double OlhoA = 11;
    public const double CorpoMin = 6;
    public const double CorpoMax = 94;
    const double MargemDoOlho = 1.2;
    public const double OlharX = 1.5;
    public const double OlharY = 1.1;
    public const double ArcoMeiaLargura = 0.72;
    public const double ArcoBase = 0.1;
    public const double ArcoPico = 0.42;
    public const double ArcoTraco = 0.6;
    public const double FolgaDoOlhar = 1.5;

    static readonly string[] ChavesTopo = ["version", "id", "name", "body", "eyes"];
    static readonly string[] ChavesOlhos = ["x", "y", "spacing", "scale"];

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,38}[a-z0-9])?$")]
    private static partial Regex Slug();

    [GeneratedRegex("[\u0000-\u001f<>]")]
    private static partial Regex NomeRuim();

    static List<Ponto> ContornoDoOlho(double cx, double cy, double escala, double margem)
    {
        var hw = OlhoL * escala / 2 + margem;
        var hh = OlhoA * escala / 2 + margem;
        var pts = new List<Ponto>(32);
        var reto = hh - hw;
        for (var k = 0; k < 16; k++)
        {
            var a = k / 16.0 * Math.PI * 2;
            var x = Math.Cos(a) * hw;
            var y = Math.Sin(a) * hw + Math.Sign(Math.Sin(a)) * Math.Max(0, reto);
            pts.Add(new Ponto(cx + x, cy + y));
        }
        foreach (var f in new[] { -0.5, 0, 0.5 })
        {
            pts.Add(new Ponto(cx - hw, cy + f * reto));
            pts.Add(new Ponto(cx + hw, cy + f * reto));
        }
        pts.Add(new Ponto(cx, cy));
        return pts;
    }

    static List<Ponto> ContornoDesenhado(double cx, double cy, double escala, double margem)
    {
        var pts = ContornoDoOlho(cx, cy, escala, margem);
        double w = OlhoL * escala, h = OlhoA * escala;
        double x0 = cx - ArcoMeiaLargura * w, x1 = cx + ArcoMeiaLargura * w;
        double yb = cy + ArcoBase * h, yc = cy - ArcoPico * h;
        var r = ArcoTraco * w / 2 + margem;
        for (var i = 0; i <= 8; i++)
        {
            double t = i / 8.0, m = 1 - t;
            var x = m * m * x0 + 2 * m * t * cx + t * t * x1;
            var y = (m * m + t * t) * yb + 2 * m * t * yc;
            for (var k = 0; k < 8; k++)
            {
                var a = k / 8.0 * Math.PI * 2;
                pts.Add(new Ponto(x + Math.Cos(a) * r, y + Math.Sin(a) * r));
            }
        }
        return pts;
    }

    public static (Ponto, Ponto) CentrosDosOlhos(OlhosAvatar olhos) =>
        (new Ponto(olhos.X - olhos.Espacamento / 2, olhos.Y), new Ponto(olhos.X + olhos.Espacamento / 2, olhos.Y));

    public static Ponto? OlhosCabem(IReadOnlyList<Ponto[]> polis, OlhosAvatar olhos, double dx, double dy, double margem)
    {
        var (a, b) = CentrosDosOlhos(olhos);
        foreach (var c in new[] { a, b })
            foreach (var p in ContornoDoOlho(c.X + dx, c.Y + dy, olhos.Escala, margem))
                if (!Caminho.Dentro(polis, p.X, p.Y)) return p;
        return null;
    }

    public static Ponto? OlhosDesenhadosCabem(IReadOnlyList<Ponto[]> polis, OlhosAvatar olhos, double dx, double dy, double margem)
    {
        var (a, b) = CentrosDosOlhos(olhos);
        foreach (var c in new[] { a, b })
            foreach (var p in ContornoDesenhado(c.X + dx, c.Y + dy, olhos.Escala, margem))
                if (!Caminho.Dentro(polis, p.X, p.Y)) return p;
        return null;
    }

    public static AlcanceDoOlhar Alcance(IReadOnlyList<Ponto[]> polis, OlhosAvatar olhos)
    {
        double Chega(double ux, double uy)
        {
            foreach (var k in new[] { 1, 0.8, 0.6, 0.4, 0.2 })
                if (OlhosDesenhadosCabem(polis, olhos, ux * k * OlharX * olhos.Escala, uy * k * OlharY * olhos.Escala, FolgaDoOlhar) is null) return k;
            return 0;
        }
        return new AlcanceDoOlhar(Chega(-1, 0), Chega(1, 0), Chega(0, -1), Chega(0, 1));
    }

    public static AnaliseDoCorpo Analisar(string caminho)
    {
        var segs = Caminho.Ler(caminho);
        var polis = Caminho.Achatar(segs);
        var b = Caminho.Limitar(polis);
        var subs = new List<SubcaminhoDoCorpo>();
        foreach (var sub in Caminho.DividirSubcaminhos(segs))
        {
            var proprio = Caminho.Achatar(sub);
            if (proprio.Count == 0) continue;
            var ob = Caminho.Limitar(proprio);
            double dentro = 0, corte = 0;
            Ponto? sonda = null, sondaCorte = null;
            var passo = (ob.MaxX - ob.MinX) * (ob.MaxY - ob.MinY) < 900 ? 0.5 : 1;
            var sozinho = proprio.Count == polis.Count;
            var xs = new List<double>();
            for (var x = Math.Floor(ob.MinX) + passo / 2; x < ob.MaxX; x += passo) xs.Add(x);
            var vx = xs.ToArray();
            var meu = new byte[vx.Length];
            var todos = new byte[vx.Length];
            for (var y = Math.Floor(ob.MinY) + passo / 2; y < ob.MaxY; y += passo)
            {
                Caminho.LinhaDentro(proprio, y, vx, meu);
                if (!sozinho) Caminho.LinhaDentro(polis, y, vx, todos);
                for (var k = 0; k < vx.Length; k++)
                {
                    if (meu[k] == 0) continue;
                    dentro++;
                    sonda ??= new Ponto(vx[k], y);
                    if (!sozinho && todos[k] == 0) { corte++; sondaCorte ??= new Ponto(vx[k], y); }
                }
            }
            corte *= passo * passo * 4;
            dentro *= passo * passo * 4;
            var d = Caminho.Serializar(sub);
            var buraco = dentro > 0 && corte / dentro > 0.6;
            subs.Add(new SubcaminhoDoCorpo(d, sub, proprio, buraco, buraco ? sondaCorte : sonda));
        }
        return new AnaliseDoCorpo(polis, b, Caminho.AreaPreenchida(polis, b), subs);
    }

    static double R1(double n) => Js.Arredondar(n * 10) / 10;

    public static (DefinicaoAvatar? Definicao, List<string> Erros) Validar(JsonElement entrada)
    {
        var erros = new List<string>();
        if (entrada.ValueKind != JsonValueKind.Object) return (null, ["definition must be a JSON object"]);
        foreach (var p in entrada.EnumerateObject())
            if (!ChavesTopo.Contains(p.Name)) erros.Add($"unknown field \"{p.Name}\"");
        if (!entrada.TryGetProperty("version", out var versao) || versao.ValueKind != JsonValueKind.Number || versao.GetDouble() != 1) erros.Add("version must be 1");
        var id = entrada.TryGetProperty("id", out var vid) && vid.ValueKind == JsonValueKind.String ? vid.GetString()! : null;
        if (id is null || !Slug().IsMatch(id)) erros.Add("id must be a lowercase slug of 1-40 chars [a-z0-9-]");
        var nome = entrada.TryGetProperty("name", out var vnome) && vnome.ValueKind == JsonValueKind.String ? vnome.GetString()!.Trim() : "";
        if (nome.Length == 0 || nome.Length > 40 || NomeRuim().IsMatch(nome)) erros.Add("name must be 1-40 printable characters without < or >");

        string? caminho = null;
        if (!entrada.TryGetProperty("body", out var corpo) || corpo.ValueKind != JsonValueKind.Object) erros.Add("body must be an object { \"path\": \"...\" }");
        else
        {
            foreach (var p in corpo.EnumerateObject()) if (p.Name != "path") erros.Add($"unknown field \"body.{p.Name}\"");
            if (corpo.TryGetProperty("path", out var vp) && vp.ValueKind == JsonValueKind.String) caminho = vp.GetString();
            else erros.Add("body.path must be a string");
        }

        OlhosAvatar? olhos = null;
        if (!entrada.TryGetProperty("eyes", out var e) || e.ValueKind != JsonValueKind.Object) erros.Add("eyes must be an object { x, y, spacing, scale }");
        else
        {
            foreach (var p in e.EnumerateObject()) if (!ChavesOlhos.Contains(p.Name)) erros.Add($"unknown field \"eyes.{p.Name}\"");
            double? Num(string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && double.IsFinite(v.GetDouble()) ? v.GetDouble() : null;
            if (Num("x") is { } x && Num("y") is { } y && Num("spacing") is { } espacamento && Num("scale") is { } escala)
            {
                olhos = new OlhosAvatar(x, y, espacamento, escala);
                if (escala < 0.6 || escala > 1.8) erros.Add($"eyes.scale {escala} out of range 0.6..1.8");
                var minimo = OlhoL * escala * 1.75;
                if (espacamento < minimo || espacamento > 40) erros.Add($"eyes.spacing {espacamento} out of range {R1(minimo)}..40");
            }
            else erros.Add("eyes.x, eyes.y, eyes.spacing and eyes.scale must be finite numbers");
        }

        var normalizado = "";
        AnaliseDoCorpo? analise = null;
        if (caminho is not null)
        {
            try
            {
                normalizado = Caminho.Serializar(Caminho.Ler(caminho));
                analise = Analisar(normalizado);
            }
            catch (ErroDeCaminho ex)
            {
                erros.Add($"body.path: {ex.Message}");
            }
        }

        if (analise is not null)
        {
            var b = analise.Limites;
            if (b.MinX < CorpoMin || b.MinY < CorpoMin || b.MaxX > CorpoMax || b.MaxY > CorpoMax) erros.Add("body.path leaves the safe area; keep geometry within 8..92");
            double w = b.MaxX - b.MinX, h = b.MaxY - b.MinY;
            if (Math.Max(w, h) < 40 || Math.Min(w, h) < 14) erros.Add("body is too small");
            if (analise.Area < 600) erros.Add($"body fill area {analise.Area} is too thin to carry eyes");
            if (olhos is not null && OlhosCabem(analise.Polis, olhos, 0, 0, MargemDoOlho) is not null) erros.Add("eyes must sit inside the body");
        }

        if (erros.Count > 0 || olhos is null || id is null) return (null, erros);
        return (new DefinicaoAvatar(id, nome, normalizado, olhos), erros);
    }

    public static (DefinicaoAvatar? Definicao, List<string> Erros) Ler(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return Validar(doc.RootElement);
        }
        catch (JsonException ex)
        {
            return (null, [ex.Message]);
        }
    }
}
