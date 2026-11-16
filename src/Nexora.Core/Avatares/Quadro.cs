using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Nexora.Core.Avatares;

public readonly record struct Matriz(double A, double B, double C, double D, double E, double F)
{
    public static readonly Matriz Identidade = new(1, 0, 0, 1, 0, 0);

    public static Matriz operator *(Matriz m, Matriz n) => new(
        m.A * n.A + m.C * n.B, m.B * n.A + m.D * n.B,
        m.A * n.C + m.C * n.D, m.B * n.C + m.D * n.D,
        m.A * n.E + m.C * n.F + m.E, m.B * n.E + m.D * n.F + m.F);
}

public sealed record MarcaDoOlho(bool Arco, double X, double Y, double Largura, double Altura, double Raio, Ponto Inicio, Ponto Controle, Ponto Fim, string D, double Traco);

public sealed record FormaDoQuadro(bool Olho, string Chave, string? Caminho, Matriz Matriz, MarcaDoOlho? Marca);

public sealed record CaminhoDoQuadro(string Id, string D, bool ParOuImpar, Caixa Caixa, IReadOnlyList<Segmento> Desenho);

public sealed record QuadroAvatar(string Nome, string Cor, string Tinta, IReadOnlyList<CaminhoDoQuadro> Caminhos, Matriz Raiz, IReadOnlyList<FormaDoQuadro> Formas);

public sealed record OpcoesDoQuadro
{
    public string? Cor { get; init; }
    public string? Tinta { get; init; }
    public double? Tamanho { get; init; }
    public double? Profundidade { get; init; }
    public Vista? Vista { get; init; }
    public double Tempo { get; init; }
    public Olhar? Olhar { get; init; }
    public HumorAvatar Humor { get; init; }
    public MisturaDeHumor? Mistura { get; init; }
    public bool Reduzido { get; init; }
}

public sealed class AvatarPreparado
{
    internal AvatarPreparado(DefinicaoAvatar def, VolumeDoAvatar vol, AlcanceDoOlhar alcance, string id, Dictionary<string, string> ids)
    {
        Definicao = def;
        Volume = vol;
        Alcance = alcance;
        Semente = Pose.Hash(def.Id);
        Id = id;
        Ids = ids;
    }

    public DefinicaoAvatar Definicao { get; }
    public VolumeDoAvatar Volume { get; }
    public AlcanceDoOlhar Alcance { get; }
    public uint Semente { get; }
    public string Id { get; }
    public IReadOnlyDictionary<string, string> Ids { get; }
    internal readonly Dictionary<string, bool[]> Detalhe = [];
    internal readonly Dictionary<string, CaminhoDoQuadro> Caminhos = [];
}

public static partial class Quadro
{
    public const string CorPadrao = "#ff6a1a";
    public const string TintaPadrao = "#151515";
    public const double ProfundidadePadrao = 6;
    const double ProfundidadeMaxima = 14;
    const double ElevacaoDoOlho = 1.5;
    const double FrenteDoOlho = 0.22;
    const double VaoDaFatiaPx = 0.75;

    static readonly ConcurrentDictionary<string, Lazy<AvatarPreparado>> Cache = new();

    [GeneratedRegex("^#([0-9a-f]{3}|[0-9a-f]{6})$", RegexOptions.IgnoreCase)]
    private static partial Regex Hex();

    static string F(double n) => Js.Numero(Js.Arredondar(n * 100) / 100);

    public static double OpcaoDeProfundidade(double? profundidade)
    {
        var d = Math.Clamp(profundidade is { } p && double.IsFinite(p) ? p : ProfundidadePadrao, 0, ProfundidadeMaxima);
        return Js.Arredondar(d * 2) / 2;
    }

    public static AvatarPreparado Preparar(DefinicaoAvatar def, double? profundidade = null)
    {
        var p = OpcaoDeProfundidade(profundidade);
        var chave = string.Join('\u0001', def.Id, def.Nome, def.Caminho, def.Olhos.X.ToString("R", CultureInfo.InvariantCulture),
            def.Olhos.Y.ToString("R", CultureInfo.InvariantCulture), def.Olhos.Espacamento.ToString("R", CultureInfo.InvariantCulture),
            def.Olhos.Escala.ToString("R", CultureInfo.InvariantCulture), p.ToString("R", CultureInfo.InvariantCulture));
        if (Cache.Count >= 64) Cache.Clear();
        return Cache.GetOrAdd(chave, _ => new Lazy<AvatarPreparado>(() => Construir(def, p))).Value;
    }

    static AvatarPreparado Construir(DefinicaoAvatar def, double profundidade)
    {
        var corpo = Corpo.Analisar(def.Caminho);
        var vol = Volume.Construir(def, profundidade / ProfundidadePadrao, corpo);
        var id = "oba-" + Js.Base36(Pose.Hash($"{def.Id}|{def.Caminho}|{Js.Numero(profundidade)}"));
        var ids = new Dictionary<string, string>();
        foreach (var parte in vol.Partes)
            foreach (var s in parte.FatiasZ.Concat(parte.FatiasX))
                if (!ids.ContainsKey(s.D)) ids[s.D] = $"{id}-{ids.Count}";
        var alcance = Corpo.Alcance(corpo.Polis, def.Olhos with { Escala = vol.EscalaDosOlhos });
        return new AvatarPreparado(def, vol, alcance, id, ids);
    }

    readonly record struct BaseDaVista(double CY, double SY, double CP, double SP);

    static BaseDaVista BaseDe(double guinada, double arfagem) => new(Math.Cos(guinada), Math.Sin(guinada), Math.Cos(arfagem), Math.Sin(arfagem));

    static double FrenteDe(BaseDaVista v, Vetor3 n) => v.SP * n.Y + v.CP * (-v.SY * n.X + v.CY * n.Z);

    static bool OlhoVisivel(VolumeDoAvatar vol, BaseDaVista v, int i) => vol.NormaisDosOlhos[i].All(n => FrenteDe(v, n) > FrenteDoOlho);

    static char PilhaAtiva(BaseDaVista v) => Math.Abs(v.CY) >= Math.Abs(v.SY) ? 'z' : 'x';

    static Matriz MatrizDoPlano(BaseDaVista v, double z, Ponto pivo)
    {
        var (cx, cy) = pivo;
        return new Matriz(v.CY, v.SY * v.SP, 0, v.CP, cx - v.CY * cx + v.SY * z, cy - v.CP * cy - v.SY * v.SP * cx - v.CY * v.SP * z);
    }

    static Matriz MatrizDoLado(BaseDaVista v, double x0, Ponto pivo)
    {
        var (cx, cy) = pivo;
        return new Matriz(-v.SY, v.CY * v.SP, 0, v.CP, cx + v.CY * (x0 - cx), cy - v.CP * cy + v.SY * v.SP * (x0 - cx));
    }

    static double ProfundidadeProjetada(BaseDaVista v, double x, double y, double z, Ponto pivo)
    {
        double X = x - pivo.X, Y = y - pivo.Y;
        return v.SP * Y + v.CP * (-v.SY * X + v.CY * z);
    }

    static Matriz MatrizRaiz(PoseAvatar pose, Limites b)
    {
        double bx = (b.MinX + b.MaxX) / 2, by = b.MaxY;
        var r = pose.Rolagem * Math.PI / 180;
        var rot = new Matriz(Math.Cos(r), Math.Sin(r), -Math.Sin(r), Math.Cos(r), 0, 0);
        var em = new Matriz(1, 0, 0, 1, bx, by) * ((rot * new Matriz(pose.Sx, 0, 0, pose.Sy, 0, 0)) * new Matriz(1, 0, 0, 1, -bx, -by));
        return new Matriz(1, 0, 0, 1, 0, pose.Elevacao) * em;
    }

    static Ponto PivoDe(Limites b) => new((b.MinX + b.MaxX) / 2, (b.MinY + b.MaxY) / 2);

    public static (double X, double Y, double Tamanho) Extensao(AvatarPreparado p)
    {
        var b = p.Volume.Limites;
        var (cx, cy) = PivoDe(b);
        var maiorProfundidade = p.Volume.Partes.Count > 0 ? p.Volume.Partes.Max(parte => parte.Profundidade) : 0;
        var raio = Js.Hypot(b.MaxX - b.MinX, b.MaxY - b.MinY, maiorProfundidade + 2 * ElevacaoDoOlho) / 2;
        var meia = raio + 4 + 0.2 * (raio + b.MaxY - cy);
        return (cx - meia, cy - meia, 2 * meia);
    }

    static bool[] Detalhe(AvatarPreparado p, char pilha, double? tamanho)
    {
        var px = tamanho is { } t && double.IsFinite(t) && t > 0 ? (int)Js.Arredondar(t) : 0;
        var chave = $"{pilha}{px}";
        lock (p.Detalhe)
        {
            if (p.Detalhe.TryGetValue(chave, out var guardado)) return guardado;
            var vao = px != 0 ? VaoDaFatiaPx * 100 / px : 0;
            var manter = new List<bool>();
            foreach (var parte in p.Volume.Partes)
            {
                var fatias = pilha == 'z' ? parte.FatiasZ : parte.FatiasX;
                var ultimo = double.NegativeInfinity;
                for (var j = 0; j < fatias.Count; j++)
                {
                    var s = fatias[j];
                    var nucleo = s.Eixo == 'z' && s.Em == 0 && s.Recuo == 0;
                    var mantida = nucleo || j == 0 || j == fatias.Count - 1 || s.Em - ultimo >= vao;
                    if (mantida) ultimo = s.Em;
                    manter.Add(mantida);
                }
            }
            if (p.Detalhe.Count >= 32) p.Detalhe.Clear();
            var saida = manter.ToArray();
            p.Detalhe[chave] = saida;
            return saida;
        }
    }

    sealed record EstadoDoOlho(double X, double Y, double W, double H, double Dx, double Dy, double Aberto, double Desvio, bool Feliz);

    static EstadoDoOlho[] EstadosDosOlhos(AvatarPreparado p, PoseAvatar pose)
    {
        var s = p.Volume.EscalaDosOlhos;
        double w = Corpo.OlhoL * s, h = Corpo.OlhoA * s;
        var r = p.Alcance;
        var dx = pose.OlharX * (pose.OlharX >= 0 ? r.Direita : r.Esquerda) * Corpo.OlharX * s;
        var dy = pose.OlharY * (pose.OlharY >= 0 ? r.Baixo : r.Cima) * Corpo.OlharY * s;
        var (a, b) = Corpo.CentrosDosOlhos(p.Definicao.Olhos);
        Ponto[] centros = [a, b];
        var saida = new EstadoDoOlho[2];
        for (var i = 0; i < 2; i++)
        {
            var aperto = i == 0 ? pose.ApertoE : pose.ApertoD;
            var aberto = Math.Max(h * Math.Max(0.09, (1 - pose.Piscar) * (1 - aperto * 0.4)), w * 0.55);
            saida[i] = new EstadoDoOlho(centros[i].X, centros[i].Y, w, h, dx, dy, aberto, (h - aberto) * 0.18, pose.OlhosFelizes);
        }
        return saida;
    }

    static MarcaDoOlho MarcaDe(EstadoDoOlho e)
    {
        if (e.Feliz)
        {
            double ex = e.X + e.Dx, ey = e.Y + e.Dy;
            double hw = e.W * Corpo.ArcoMeiaLargura, baseY = ey + e.H * Corpo.ArcoBase;
            var topo = ey - e.H * Corpo.ArcoPico;
            var d = $"M{F(ex - hw)} {F(baseY)}Q{F(ex)} {F(topo)} {F(ex + hw)} {F(baseY)}";
            return new MarcaDoOlho(true, 0, 0, 0, 0, 0, new Ponto(ex - hw, baseY), new Ponto(ex, topo), new Ponto(ex + hw, baseY), d, e.W * Corpo.ArcoTraco);
        }
        double cx = e.X + e.Dx, cy = e.Y + e.Dy + e.Desvio;
        return new MarcaDoOlho(false, cx - e.W / 2, cy - e.Aberto / 2, e.W, e.Aberto, Math.Min(e.W, e.Aberto) / 2, default, default, default, "", 0);
    }

    public static string CorHex(string? cor, string padrao)
    {
        var valor = cor is not null && Hex().IsMatch(cor.Trim()) ? cor.Trim() : padrao;
        var h = valor[1..];
        if (h.Length == 3) h = string.Concat(h[0], h[0], h[1], h[1], h[2], h[2]);
        return "#" + h.ToLowerInvariant();
    }

    public static PoseAvatar PoseDe(AvatarPreparado p, OpcoesDoQuadro o) =>
        Pose.Calcular(new EntradaDePose(p.Semente, double.IsFinite(o.Tempo) ? o.Tempo : 0, o.Humor, o.Olhar, o.Reduzido, o.Vista, o.Mistura));

    public static QuadroAvatar Montar(AvatarPreparado p, OpcoesDoQuadro o)
    {
        var vol = p.Volume;
        var b = vol.Limites;
        var pose = PoseDe(p, o);
        var pivo = PivoDe(b);
        var v = BaseDe(pose.Guinada, pose.Arfagem);
        var pilha = PilhaAtiva(v);
        var olhosMeio = new Ponto(p.Definicao.Olhos.X, p.Definicao.Olhos.Y);
        var varredura = Js.Hypot(v.SY, v.CY * v.SP);
        var recuoMin = Math.Min(Math.Abs(v.CY), Math.Abs(v.CP));
        var mantidas = Detalhe(p, pilha, o.Tamanho);

        var caminhos = new List<CaminhoDoQuadro>();
        var vistos = new HashSet<string>();
        var itens = new List<(double Profundidade, FormaDoQuadro Forma)>();
        var k = 0;
        for (var i = 0; i < vol.Partes.Count; i++)
        {
            var parte = vol.Partes[i];
            var fatias = pilha == 'z' ? parte.FatiasZ : parte.FatiasX;
            for (var j = 0; j < fatias.Count; j++)
            {
                var s = fatias[j];
                if (!mantidas[k++]) continue;
                if (!(s.Eixo != 'z' || s.Recuo <= 0 || Math.Abs(s.Em) * varredura > s.Recuo * recuoMin)) continue;
                var id = p.Ids[s.D];
                if (vistos.Add(s.D)) caminhos.Add(CaminhoDe(p, id, s));
                var m = pilha == 'z' ? MatrizDoPlano(v, s.Em, pivo) : MatrizDoLado(v, s.Em, pivo);
                var (rx, ry) = i == vol.ParteDosOlhos ? olhosMeio : parte.Centro;
                var profundidade = pilha == 'z' ? ProfundidadeProjetada(v, rx, ry, s.Em, pivo) : ProfundidadeProjetada(v, s.Em, ry, 0, pivo);
                itens.Add((profundidade, new FormaDoQuadro(false, $"{i}{pilha}{j}", id, m, null)));
            }
        }
        var estados = EstadosDosOlhos(p, pose);
        for (var idx = 0; idx < 2; idx++)
        {
            if (!OlhoVisivel(vol, v, idx)) continue;
            var z = vol.OlhoZ[idx] + ElevacaoDoOlho;
            itens.Add((ProfundidadeProjetada(v, olhosMeio.X, olhosMeio.Y, z, pivo) + 1e-6,
                new FormaDoQuadro(true, idx == 0 ? "eye-0" : "eye-1", null, MatrizDoPlano(v, z, pivo), MarcaDe(estados[idx]))));
        }
        var formas = itens.OrderBy(x => x.Profundidade).Select(x => x.Forma).ToList();
        return new QuadroAvatar(p.Definicao.Nome, CorHex(o.Cor, CorPadrao), CorHex(o.Tinta, TintaPadrao), caminhos, MatrizRaiz(pose, b), formas);
    }

    static CaminhoDoQuadro CaminhoDe(AvatarPreparado p, string id, FatiaDeVolume s)
    {
        lock (p.Caminhos)
        {
            if (p.Caminhos.TryGetValue(id, out var c)) return c;
            c = new CaminhoDoQuadro(id, s.D, s.ParOuImpar, s.Caixa, s.Desenho);
            p.Caminhos[id] = c;
            return c;
        }
    }

    public static QuadroAvatar Montar(DefinicaoAvatar def, OpcoesDoQuadro o) => Montar(Preparar(def, o.Profundidade), o);
}
