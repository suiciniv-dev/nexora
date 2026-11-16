using System.Text;

namespace Nexora.Core.Avatares;

public sealed record Campo(double[] Valores, int Nx, int Ny, double X0, double Y0, double Passo);

public static class Contorno
{
    public static List<Ponto[]> Lacos(Campo campo, double nivel)
    {
        var (valores, nx, ny, x0, y0, passo) = (campo.Valores, campo.Nx, campo.Ny, campo.X0, campo.Y0, campo.Passo);
        double Em(int i, int j) => valores[j * nx + i] - nivel;
        var pontos = new Dictionary<int, Ponto>();
        var ligacoes = new Dictionary<int, List<int>>();
        var ordem = new List<int>();

        void Marcar(int chave)
        {
            if (pontos.ContainsKey(chave)) return;
            int idx = chave >> 1, i = idx % nx, j = (idx - i) / nx;
            var a = Em(i, j);
            if ((chave & 1) != 0)
            {
                var b = Em(i, j + 1);
                var t = a / (a - b);
                pontos[chave] = new Ponto(x0 + i * passo, y0 + (j + t) * passo);
            }
            else
            {
                var b = Em(i + 1, j);
                var t = a / (a - b);
                pontos[chave] = new Ponto(x0 + (i + t) * passo, y0 + j * passo);
            }
        }

        void Juntar(int p, int q)
        {
            Marcar(p);
            Marcar(q);
            if (!ligacoes.TryGetValue(p, out var lp)) { lp = []; ligacoes[p] = lp; ordem.Add(p); }
            lp.Add(q);
            if (!ligacoes.TryGetValue(q, out var lq)) { lq = []; ligacoes[q] = lq; ordem.Add(q); }
            lq.Add(p);
        }

        for (var j = 0; j < ny - 1; j++)
        {
            for (var i = 0; i < nx - 1; i++)
            {
                bool tl = Em(i, j) >= 0, tr = Em(i + 1, j) >= 0, br = Em(i + 1, j + 1) >= 0, bl = Em(i, j + 1) >= 0;
                var codigo = (tl ? 8 : 0) | (tr ? 4 : 0) | (br ? 2 : 0) | (bl ? 1 : 0);
                if (codigo is 0 or 15) continue;
                int topo = 2 * (j * nx + i), baixo = 2 * ((j + 1) * nx + i);
                int esquerda = 2 * (j * nx + i) + 1, direita = 2 * (j * nx + i + 1) + 1;
                if (codigo is 10 or 5)
                {
                    var centro = (Em(i, j) + Em(i + 1, j) + Em(i + 1, j + 1) + Em(i, j + 1)) / 4 >= 0;
                    if ((codigo == 10) == centro) { Juntar(topo, direita); Juntar(baixo, esquerda); }
                    else { Juntar(esquerda, topo); Juntar(direita, baixo); }
                    continue;
                }
                Span<int> cruzados = stackalloc int[4];
                var n = 0;
                if (tl != tr) cruzados[n++] = topo;
                if (tr != br) cruzados[n++] = direita;
                if (br != bl) cruzados[n++] = baixo;
                if (bl != tl) cruzados[n++] = esquerda;
                Juntar(cruzados[0], cruzados[1]);
            }
        }

        var lacos = new List<Ponto[]>();
        var vistos = new HashSet<int>();
        foreach (var inicio in ordem)
        {
            if (vistos.Contains(inicio)) continue;
            var laco = new List<Ponto>();
            int anterior = -1, atual = inicio;
            while (!vistos.Contains(atual))
            {
                vistos.Add(atual);
                laco.Add(pontos[atual]);
                var proximo = -1;
                foreach (var n in ligacoes[atual])
                {
                    if (n != anterior && !vistos.Contains(n)) { proximo = n; break; }
                }
                if (proximo < 0) break;
                anterior = atual;
                atual = proximo;
            }
            if (laco.Count >= 3) lacos.Add([.. laco]);
        }
        return lacos;
    }

    static double Perpendicular(Ponto p, Ponto a, Ponto b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        var len = Js.Hypot(dx, dy);
        if (len == 0) return Js.Hypot(p.X - a.X, p.Y - a.Y);
        return Math.Abs(dx * (a.Y - p.Y) - dy * (a.X - p.X)) / len;
    }

    public static Ponto[] Simplificar(Ponto[] laco, double tolerancia)
    {
        if (laco.Length <= 8) return laco;
        int longe = 0;
        double melhor = -1;
        for (var k = 1; k < laco.Length; k++)
        {
            var d = Js.Hypot(laco[k].X - laco[0].X, laco[k].Y - laco[0].Y);
            if (d > melhor) { melhor = d; longe = k; }
        }
        var manter = new bool[laco.Length];
        manter[0] = manter[longe] = true;
        var pilha = new Stack<(int, int)>();
        pilha.Push((0, longe));
        pilha.Push((longe, laco.Length));
        while (pilha.Count > 0)
        {
            var (s, e) = pilha.Pop();
            var fim = laco[e % laco.Length];
            int idx = -1;
            var max = tolerancia;
            for (var k = s + 1; k < e; k++)
            {
                var d = Perpendicular(laco[k], laco[s], fim);
                if (d > max) { max = d; idx = k; }
            }
            if (idx >= 0)
            {
                manter[idx] = true;
                pilha.Push((s, idx));
                pilha.Push((idx, e));
            }
        }
        var saida = new List<Ponto>();
        for (var k = 0; k < laco.Length; k++) if (manter[k]) saida.Add(laco[k]);
        return [.. saida];
    }

    const int Unidade = 100;
    const double Quina = 0.6;
    const double Flecha = 0.1;

    static string Num(long k)
    {
        long a = Math.Abs(k), inteiro = a / Unidade, frac = a % Unidade;
        var f = frac == 0 ? "" : frac % 10 == 0 ? "." + (frac / 10) : frac < 10 ? ".0" + frac : "." + frac;
        return (k < 0 ? "-" : "") + (inteiro != 0 || f.Length == 0 ? inteiro.ToString(System.Globalization.CultureInfo.InvariantCulture) : "") + f;
    }

    public static (string D, List<Segmento> Desenho) ParaCaminho(IReadOnlyList<Ponto[]> lacos)
    {
        var d = new StringBuilder();
        var cmd = "";
        var ultimo = "";
        var desenho = new List<Segmento>();
        void Emitir(string c, params long[] ks)
        {
            if (c != cmd) { d.Append(c); cmd = c; ultimo = ""; }
            foreach (var k in ks)
            {
                var t = Num(k);
                if (ultimo.Length > 0 && t[0] != '-' && !(t[0] == '.' && ultimo.Contains('.'))) d.Append(' ');
                d.Append(t);
                ultimo = t;
            }
        }
        static double U(long v) => v / (double)Unidade;
        foreach (var laco in lacos)
        {
            var n = laco.Length;
            var v = new (long X, long Y)[n];
            for (var k = 0; k < n; k++) v[k] = ((long)(2 * Js.Arredondar(laco[k].X * Unidade / 2)), (long)(2 * Js.Arredondar(laco[k].Y * Unidade / 2)));
            var quina = new bool[n];
            for (var k = 0; k < n; k++)
            {
                Ponto p = laco[k], a = laco[(k + n - 1) % n], b = laco[(k + 1) % n];
                var cross = (p.X - a.X) * (b.Y - p.Y) - (p.Y - a.Y) * (b.X - p.X);
                quina[k] = Math.Abs(Math.Atan2(cross, (p.X - a.X) * (b.X - p.X) + (p.Y - a.Y) * (b.Y - p.Y))) > Quina
                    || Math.Abs(cross) > 4 * Flecha * Js.Hypot(b.X - a.X, b.Y - a.Y);
            }
            (long, long) Meio(int k) => ((v[k % n].X + v[(k + 1) % n].X) / 2, (v[k % n].Y + v[(k + 1) % n].Y) / 2);
            var (cx, cy) = Meio(0);
            Emitir("M", cx, cy);
            desenho.Add(new Segmento(TipoSegmento.M, U(cx), U(cy)));
            var suave = false;
            for (var k = 1; k <= n; k++)
            {
                var (px, py) = v[k % n];
                if (quina[k % n])
                {
                    Emitir("l", px - cx, py - cy);
                    cx = px; cy = py;
                    desenho.Add(new Segmento(TipoSegmento.L, U(cx), U(cy)));
                    if (k < n && !quina[(k + 1) % n])
                    {
                        var (mx, my) = Meio(k);
                        Emitir("l", mx - cx, my - cy);
                        cx = mx; cy = my;
                        desenho.Add(new Segmento(TipoSegmento.L, U(cx), U(cy)));
                    }
                    suave = false;
                }
                else
                {
                    var (mx, my) = Meio(k);
                    if (suave) Emitir("t", mx - cx, my - cy);
                    else Emitir("q", px - cx, py - cy, mx - cx, my - cy);
                    desenho.Add(new Segmento(TipoSegmento.Q, U(mx), U(my), U(px), U(py)));
                    cx = mx; cy = my;
                    suave = true;
                }
            }
            d.Append('Z');
            cmd = "Z";
            ultimo = "";
            desenho.Add(new Segmento(TipoSegmento.Z));
        }
        return (d.ToString(), desenho);
    }
}
