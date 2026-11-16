using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Nexora.Core.Avatares;

public readonly record struct Ponto(double X, double Y);

public readonly record struct Limites(double MinX, double MinY, double MaxX, double MaxY);

public enum TipoSegmento { M, L, C, Q, A, Z }

public readonly record struct Segmento(
    TipoSegmento Tipo,
    double X = 0, double Y = 0,
    double X1 = 0, double Y1 = 0, double X2 = 0, double Y2 = 0,
    double Rx = 0, double Ry = 0, double Rotacao = 0, int Grande = 0, int Horario = 0);

public sealed class ErroDeCaminho(string mensagem) : Exception(mensagem);

public static partial class Caminho
{
    public const int MaxCaracteres = 6000;
    public const int MaxSegmentos = 400;
    public const int MaxSubcaminhos = 16;
    public const double CoordMin = -20;
    public const double CoordMax = 120;
    public const double Planura = 0.02;

    [GeneratedRegex(@"[^MmLlHhVvCcSsQqTtAaZz0-9.,eE+\-\s]")]
    private static partial Regex CaractereInvalido();

    [GeneratedRegex(@"\G(?:([MmLlHhVvCcSsQqTtAaZz])|([+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?)|([\s,]+))")]
    private static partial Regex Ficha();

    static int Argumentos(char c) => c switch
    {
        'M' or 'L' or 'T' => 2,
        'H' or 'V' => 1,
        'C' => 6,
        'S' or 'Q' => 4,
        'A' => 7,
        _ => 0,
    };

    static List<object> Tokenizar(string d)
    {
        if (CaractereInvalido().Match(d) is { Success: true } ruim)
            throw new ErroDeCaminho($"unsupported character \"{ruim.Value}\" (allowed commands: M L H V C S Q T A Z)");
        var fichas = new List<object>();
        var i = 0;
        while (i < d.Length)
        {
            var m = Ficha().Match(d, i);
            if (!m.Success || m.Length == 0) throw new ErroDeCaminho($"cannot parse path near \"{d[i..Math.Min(d.Length, i + 12)]}\"");
            if (m.Groups[1].Success) fichas.Add(m.Groups[1].Value[0]);
            else if (m.Groups[2].Success) fichas.Add(double.Parse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture));
            i = m.Index + m.Length;
        }
        return fichas;
    }

    public static List<Segmento> Ler(string d)
    {
        if (string.IsNullOrWhiteSpace(d)) throw new ErroDeCaminho("path is empty");
        if (d.Length > MaxCaracteres) throw new ErroDeCaminho($"path longer than {MaxCaracteres} characters");
        var fichas = Tokenizar(d);
        var saida = new List<Segmento>();
        var i = 0;
        double cx = 0, cy = 0, sx = 0, sy = 0;
        (double, double)? antC = null, antQ = null;
        var cmd = '\0';
        var subcaminhos = 0;
        var aberto = false;

        while (i < fichas.Count)
        {
            var t = fichas[i];
            if (t is char letra)
            {
                cmd = letra;
                i++;
                if (cmd is 'Z' or 'z')
                {
                    if (!aberto) throw new ErroDeCaminho("Z without an open subpath");
                    saida.Add(new Segmento(TipoSegmento.Z));
                    cx = sx; cy = sy; antC = antQ = null; aberto = false;
                    continue;
                }
            }
            else if (cmd == '\0') throw new ErroDeCaminho("path must start with M");
            else if (cmd is 'Z' or 'z') throw new ErroDeCaminho("numbers after Z");

            var cima = char.ToUpperInvariant(cmd);
            var rel = cmd != cima;
            if (saida.Count == 0 && cima != 'M') throw new ErroDeCaminho("path must start with M");
            var n = Argumentos(cima);
            var a = new double[n];
            for (var k = 0; k < n; k++)
            {
                if (i + k >= fichas.Count || fichas[i + k] is not double v) throw new ErroDeCaminho($"command {cmd} expects {n} numbers");
                if (!double.IsFinite(v)) throw new ErroDeCaminho("non-finite number");
                a[k] = v;
            }
            i += n;
            double ox = rel ? cx : 0, oy = rel ? cy : 0;
            (double, double)? proxC = null, proxQ = null;
            switch (cima)
            {
                case 'M':
                    if (aberto) saida.Add(new Segmento(TipoSegmento.Z));
                    cx = a[0] + ox; cy = a[1] + oy; sx = cx; sy = cy;
                    saida.Add(new Segmento(TipoSegmento.M, cx, cy));
                    subcaminhos++; aberto = true;
                    cmd = rel ? 'l' : 'L';
                    break;
                case 'L': cx = a[0] + ox; cy = a[1] + oy; saida.Add(new Segmento(TipoSegmento.L, cx, cy)); break;
                case 'H': cx = a[0] + ox; saida.Add(new Segmento(TipoSegmento.L, cx, cy)); break;
                case 'V': cy = a[0] + oy; saida.Add(new Segmento(TipoSegmento.L, cx, cy)); break;
                case 'C':
                    {
                        var s = new Segmento(TipoSegmento.C, a[4] + ox, a[5] + oy, a[0] + ox, a[1] + oy, a[2] + ox, a[3] + oy);
                        saida.Add(s); proxC = (s.X2, s.Y2); cx = s.X; cy = s.Y; break;
                    }
                case 'S':
                    {
                        double x1 = antC is { } pc ? 2 * cx - pc.Item1 : cx, y1 = antC is { } pc2 ? 2 * cy - pc2.Item2 : cy;
                        var s = new Segmento(TipoSegmento.C, a[2] + ox, a[3] + oy, x1, y1, a[0] + ox, a[1] + oy);
                        saida.Add(s); proxC = (s.X2, s.Y2); cx = s.X; cy = s.Y; break;
                    }
                case 'Q':
                    {
                        var s = new Segmento(TipoSegmento.Q, a[2] + ox, a[3] + oy, a[0] + ox, a[1] + oy);
                        saida.Add(s); proxQ = (s.X1, s.Y1); cx = s.X; cy = s.Y; break;
                    }
                case 'T':
                    {
                        double x1 = antQ is { } pq ? 2 * cx - pq.Item1 : cx, y1 = antQ is { } pq2 ? 2 * cy - pq2.Item2 : cy;
                        var s = new Segmento(TipoSegmento.Q, a[0] + ox, a[1] + oy, x1, y1);
                        saida.Add(s); proxQ = (x1, y1); cx = s.X; cy = s.Y; break;
                    }
                case 'A':
                    {
                        if (a[0] < 0 || a[1] < 0) throw new ErroDeCaminho("arc radii must be non-negative");
                        if (a[3] is not (0 or 1) || a[4] is not (0 or 1)) throw new ErroDeCaminho("arc flags must be 0 or 1, written as separate numbers");
                        cx = a[5] + ox; cy = a[6] + oy;
                        saida.Add(new Segmento(TipoSegmento.A, cx, cy, Rx: a[0], Ry: a[1], Rotacao: a[2], Grande: (int)a[3], Horario: (int)a[4]));
                        break;
                    }
                default:
                    throw new ErroDeCaminho($"unsupported command {cmd}");
            }
            antC = proxC; antQ = proxQ;
            if (saida.Count > MaxSegmentos) throw new ErroDeCaminho($"more than {MaxSegmentos} segments");
        }
        if (aberto) saida.Add(new Segmento(TipoSegmento.Z));
        if (subcaminhos > MaxSubcaminhos) throw new ErroDeCaminho($"more than {MaxSubcaminhos} subpaths");
        foreach (var s in saida)
        {
            foreach (var v in Coordenadas(s))
                if (v < CoordMin || v > CoordMax) throw new ErroDeCaminho($"coordinate {Js.Numero(v)} far outside the 0..100 viewBox");
        }
        return saida;
    }

    static IEnumerable<double> Coordenadas(Segmento s) => s.Tipo switch
    {
        TipoSegmento.M or TipoSegmento.L => [s.X, s.Y],
        TipoSegmento.C => [s.X1, s.Y1, s.X2, s.Y2, s.X, s.Y],
        TipoSegmento.Q => [s.X1, s.Y1, s.X, s.Y],
        TipoSegmento.A => [s.Rx, s.Ry, s.X, s.Y],
        _ => [],
    };

    static string F(double n) => Js.Numero(Js.Arredondar(n * 100) / 100);

    public static string Serializar(IEnumerable<Segmento> segmentos)
    {
        var sb = new StringBuilder();
        foreach (var s in segmentos)
        {
            switch (s.Tipo)
            {
                case TipoSegmento.M: sb.Append('M').Append(F(s.X)).Append(' ').Append(F(s.Y)); break;
                case TipoSegmento.L: sb.Append('L').Append(F(s.X)).Append(' ').Append(F(s.Y)); break;
                case TipoSegmento.C: sb.Append('C').Append(F(s.X1)).Append(' ').Append(F(s.Y1)).Append(' ').Append(F(s.X2)).Append(' ').Append(F(s.Y2)).Append(' ').Append(F(s.X)).Append(' ').Append(F(s.Y)); break;
                case TipoSegmento.Q: sb.Append('Q').Append(F(s.X1)).Append(' ').Append(F(s.Y1)).Append(' ').Append(F(s.X)).Append(' ').Append(F(s.Y)); break;
                case TipoSegmento.A: sb.Append('A').Append(F(s.Rx)).Append(' ').Append(F(s.Ry)).Append(' ').Append(F(s.Rotacao)).Append(' ').Append(s.Grande).Append(' ').Append(s.Horario).Append(' ').Append(F(s.X)).Append(' ').Append(F(s.Y)); break;
                case TipoSegmento.Z: sb.Append('Z'); break;
            }
        }
        return sb.ToString();
    }

    static void AmostrarArco(double x0, double y0, Segmento s, List<Ponto> saida)
    {
        double rx = s.Rx, ry = s.Ry, x = s.X, y = s.Y;
        if (rx == 0 || ry == 0 || (x0 == x && y0 == y)) { saida.Add(new Ponto(x, y)); return; }
        var phi = s.Rotacao * Math.PI / 180;
        double cos = Math.Cos(phi), sin = Math.Sin(phi);
        double dx = (x0 - x) / 2, dy = (y0 - y) / 2;
        double x1p = cos * dx + sin * dy, y1p = -sin * dx + cos * dy;
        rx = Math.Abs(rx); ry = Math.Abs(ry);
        var lambda = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
        if (lambda > 1) { var k = Math.Sqrt(lambda); rx *= k; ry *= k; }
        var num = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
        var den = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
        var coef = Math.Sqrt(Math.Max(0, num / den));
        if (s.Grande == s.Horario) coef = -coef;
        double cxp = coef * rx * y1p / ry, cyp = -coef * ry * x1p / rx;
        var cx = cos * cxp - sin * cyp + (x0 + x) / 2;
        var cy = sin * cxp + cos * cyp + (y0 + y) / 2;
        static double Angulo(double ux, double uy, double vx, double vy) => Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
        var t1 = Angulo(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
        var dt = Angulo((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
        if (s.Horario == 0 && dt > 0) dt -= 2 * Math.PI;
        if (s.Horario == 1 && dt < 0) dt += 2 * Math.PI;
        var passo = 2 * Math.Acos(Math.Max(-1, 1 - Planura / Math.Max(rx, ry)));
        var passos = Math.Min(256, Math.Max(4, (int)Math.Ceiling(Math.Abs(dt) / passo)));
        for (var k = 1; k <= passos; k++)
        {
            var t = t1 + dt * k / passos;
            double ex = rx * Math.Cos(t), ey = ry * Math.Sin(t);
            saida.Add(new Ponto(cos * ex - sin * ey + cx, sin * ex + cos * ey + cy));
        }
    }

    public static List<Ponto[]> Achatar(IEnumerable<Segmento> segmentos)
    {
        var polis = new List<List<Ponto>>();
        var atual = new List<Ponto>();
        double px = 0, py = 0;
        foreach (var s in segmentos)
        {
            switch (s.Tipo)
            {
                case TipoSegmento.M:
                    if (atual.Count > 0) polis.Add(atual);
                    atual = [new Ponto(s.X, s.Y)]; px = s.X; py = s.Y; break;
                case TipoSegmento.L: atual.Add(new Ponto(s.X, s.Y)); px = s.X; py = s.Y; break;
                case TipoSegmento.C:
                    {
                        var m = Math.Max(Js.Hypot(px - 2 * s.X1 + s.X2, py - 2 * s.Y1 + s.Y2), Js.Hypot(s.X1 - 2 * s.X2 + s.X, s.Y1 - 2 * s.Y2 + s.Y));
                        var n = Math.Min(128, Math.Max(2, (int)Math.Ceiling(Math.Sqrt(0.75 * m / Planura))));
                        for (var k = 1; k <= n; k++)
                        {
                            double t = (double)k / n, u = 1 - t;
                            atual.Add(new Ponto(
                                u * u * u * px + 3 * u * u * t * s.X1 + 3 * u * t * t * s.X2 + t * t * t * s.X,
                                u * u * u * py + 3 * u * u * t * s.Y1 + 3 * u * t * t * s.Y2 + t * t * t * s.Y));
                        }
                        px = s.X; py = s.Y; break;
                    }
                case TipoSegmento.Q:
                    {
                        var n = Math.Min(128, Math.Max(2, (int)Math.Ceiling(Math.Sqrt(0.25 * Js.Hypot(px - 2 * s.X1 + s.X, py - 2 * s.Y1 + s.Y) / Planura))));
                        for (var k = 1; k <= n; k++)
                        {
                            double t = (double)k / n, u = 1 - t;
                            atual.Add(new Ponto(u * u * px + 2 * u * t * s.X1 + t * t * s.X, u * u * py + 2 * u * t * s.Y1 + t * t * s.Y));
                        }
                        px = s.X; py = s.Y; break;
                    }
                case TipoSegmento.A: AmostrarArco(px, py, s, atual); px = s.X; py = s.Y; break;
                case TipoSegmento.Z:
                    if (atual.Count > 0) { polis.Add(atual); px = atual[0].X; py = atual[0].Y; }
                    atual = []; break;
            }
        }
        if (atual.Count > 0) polis.Add(atual);
        return polis.Where(p => p.Count >= 3).Select(p => p.ToArray()).ToList();
    }


    public static int Giro(IReadOnlyList<Ponto[]> polis, double x, double y)
    {
        var w = 0;
        foreach (var poli in polis)
        {
            for (int i = 0, j = poli.Length - 1; i < poli.Length; j = i++)
            {
                var (xi, yi) = poli[i];
                var (xj, yj) = poli[j];
                if (yj <= y)
                {
                    if (yi > y && (xi - xj) * (y - yj) - (x - xj) * (yi - yj) > 0) w++;
                }
                else if (yi <= y && (xi - xj) * (y - yj) - (x - xj) * (yi - yj) < 0) w--;
            }
        }
        return w;
    }

    public static bool Dentro(IReadOnlyList<Ponto[]> polis, double x, double y) => Giro(polis, x, y) != 0;

    public static void LinhaDentro(IReadOnlyList<Ponto[]> polis, double y, double[] xs, byte[] saida)
    {
        var cortes = new List<(double X, int Sinal)>();
        foreach (var poli in polis)
        {
            for (int i = 0, j = poli.Length - 1; i < poli.Length; j = i++)
            {
                var (xj, yj) = poli[j];
                var (xi, yi) = poli[i];
                if (yj <= y && yi > y) cortes.Add((xj + (y - yj) * (xi - xj) / (yi - yj), 1));
                else if (yi <= y && yj > y) cortes.Add((xj + (y - yj) * (xi - xj) / (yi - yj), -1));
            }
        }
        var ordenados = cortes.OrderBy(c => c.X).ToList();
        int w = 0, h = 0;
        for (var k = 0; k < xs.Length; k++)
        {
            while (h < ordenados.Count && ordenados[h].X < xs[k]) w += ordenados[h++].Sinal;
            saida[k] = (byte)(w != 0 ? 1 : 0);
        }
    }

    public static Limites Limitar(IEnumerable<IReadOnlyList<Ponto>> polis)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        foreach (var p in polis)
        {
            foreach (var (x, y) in p)
            {
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }
        return new Limites(minX, minY, maxX, maxY);
    }

    public static int AreaPreenchida(IReadOnlyList<Ponto[]> polis, Limites b)
    {
        var n = 0;
        var xs = new List<double>();
        for (var x = Math.Floor(b.MinX) + 0.5; x < b.MaxX; x += 1) xs.Add(x);
        var vx = xs.ToArray();
        var linha = new byte[vx.Length];
        for (var y = Math.Floor(b.MinY) + 0.5; y < b.MaxY; y += 1)
        {
            LinhaDentro(polis, y, vx, linha);
            foreach (var v in linha) n += v;
        }
        return n;
    }

    public static List<List<Segmento>> DividirSubcaminhos(IEnumerable<Segmento> segmentos)
    {
        var saida = new List<List<Segmento>>();
        foreach (var s in segmentos)
        {
            if (s.Tipo == TipoSegmento.M) saida.Add([]);
            if (saida.Count > 0) saida[^1].Add(s);
        }
        return saida;
    }
}
