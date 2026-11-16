namespace Nexora.Core.Avatares;

public enum TipoDeParte { Inflada, RevolveX, RevolveY }

public readonly record struct Caixa(double X, double Y, double W, double H);

public sealed record FatiaDeVolume(char Eixo, double Em, string D, List<Segmento> Desenho, IReadOnlyList<Ponto[]> Lacos, bool ParOuImpar, Caixa Caixa, double Recuo);

public sealed record ParteDoVolume(
    TipoDeParte Tipo, string D, List<Ponto[]> Polis, Limites Limites, Ponto Centro, double Espessura, double Profundidade,
    double Rxy, double Rz, double? Eixo, List<FatiaDeVolume> FatiasZ, List<FatiaDeVolume> FatiasX, Func<double, double, double> Altura);

public readonly record struct Vetor3(double X, double Y, double Z);

public sealed record VolumeDoAvatar(List<ParteDoVolume> Partes, Limites Limites, int ParteDosOlhos, double[] OlhoZ, double EscalaDosOlhos, Vetor3[][] NormaisDosOlhos);

public static class Volume
{
    public const double ProfundidadeMaxima = 30;
    public const double Realce = 1.45;
    public const double FolgaDoRealce = 3;
    const int MaxFatiasX = 40;
    const double Fino = 4;
    const double AlcanceFino = 5;
    const double MargemFina = 2;
    const double FinoZ = 0.6;

    static double SegDist2(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay;
        var len = dx * dx + dy * dy;
        var t = len > 0 ? ((px - ax) * dx + (py - ay) * dy) / len : 0;
        t = t < 0 ? 0 : t > 1 ? 1 : t;
        double ex = ax + t * dx - px, ey = ay + t * dy - py;
        return ex * ex + ey * ey;
    }

    public static double DistanciaDaBorda(IReadOnlyList<Ponto[]> polis, double x, double y)
    {
        var melhor = double.PositiveInfinity;
        foreach (var poli in polis)
        {
            for (int i = 0, j = poli.Length - 1; i < poli.Length; j = i++)
            {
                var d = Math.Sqrt(SegDist2(x, y, poli[j].X, poli[j].Y, poli[i].X, poli[i].Y));
                if (d < melhor) melhor = d;
            }
        }
        return melhor;
    }

    static double RecuoEm(double profundidade, double rxy, double rz, double nivel)
    {
        var meia = profundidade / 2;
        if (rxy <= 0 || rz <= 0 || nivel <= meia - rz) return 0;
        if (nivel >= meia) return rxy;
        var s = (nivel - (meia - rz)) / rz;
        return rxy * (1 - Math.Sqrt(Math.Max(0, 1 - s * s)));
    }

    static double SuperficieZ(double profundidade, double rxy, double rz, double dist)
    {
        var meia = profundidade / 2;
        if (rxy <= 0 || dist >= rxy) return meia;
        var k = 1 - Math.Max(0, dist) / rxy;
        return meia - rz + Math.Sqrt(Math.Max(0, 1 - k * k)) * rz;
    }

    static double ProfundidadeDaParte(double espessura, Limites b, double volume)
    {
        var longo = Math.Max(b.MaxX - b.MinX, b.MaxY - b.MinY);
        var cru = 0.8 * Math.Max(espessura, Math.Min(0.4 * longo, 2.4 * espessura));
        return Math.Min(ProfundidadeMaxima, Math.Max(3, cru)) * volume;
    }

    static List<double> Cruzamentos(IReadOnlyList<Ponto[]> polis, bool linhas, double s)
    {
        var saida = new List<double>();
        foreach (var poli in polis)
        {
            for (int i = 0, j = poli.Length - 1; i < poli.Length; j = i++)
            {
                var p = poli[j];
                var q = poli[i];
                double pa = linhas ? p.Y : p.X, qa = linhas ? q.Y : q.X, pb = linhas ? p.X : p.Y, qb = linhas ? q.X : q.Y;
                if ((pa <= s && qa > s) || (qa <= s && pa > s)) saida.Add(pb + (s - pa) * (qb - pb) / (qa - pa));
            }
        }
        saida.Sort();
        return saida;
    }

    sealed record Perfil(double Inicio, double Passo, double[] R);

    static Perfil? PerfilDeRevolucao(IReadOnlyList<Ponto[]> polis, bool linhas, double lo, double hi, double centro)
    {
        const double passo = 0.2;
        var n = Math.Max(2, (int)Math.Ceiling((hi - lo) / passo) + 1);
        var r = new double[n];
        double vao = 0;
        var meios = new List<double>();
        for (var k = 0; k < n; k++)
        {
            var s = Math.Min(hi - 0.01, Math.Max(lo + 0.01, lo + k * passo));
            var xs = Cruzamentos(polis, linhas, s);
            if (xs.Count == 0) continue;
            if (xs.Count != 2) return null;
            r[k] = (xs[1] - xs[0]) / 2;
            vao = Math.Max(vao, xs[1] - xs[0]);
            meios.Add((xs[0] + xs[1]) / 2);
        }
        var tol = Math.Max(0.8, 0.04 * vao);
        if (meios.Count == 0 || meios.Any(m => Math.Abs(m - centro) > tol)) return null;
        return new Perfil(lo, passo, r);
    }

    static double Q(double v) => v * v;

    static double RaioEm(Perfil p, double s)
    {
        var t = (s - p.Inicio) / p.Passo;
        if (t < 0 || t > p.R.Length - 1) return 0;
        var k = Math.Min(p.R.Length - 2, (int)Math.Floor(t));
        return p.R[k] + (p.R[k + 1] - p.R[k]) * (t - k);
    }

    static double? EixoDeRevolucao(AnaliseDoCorpo corpo)
    {
        var b = corpo.Limites;
        var contornos = corpo.Subcaminhos.Where(s => !s.Buraco).ToList();
        if (contornos.Count < 2 || b.MaxX - b.MinX < 1.1 * (b.MaxY - b.MinY)) return null;
        var eixo = (b.MinY + b.MaxY) / 2;
        foreach (var o in contornos)
        {
            var ob = Caminho.Limitar(o.Polis);
            if (Math.Abs((ob.MinY + ob.MaxY) / 2 - eixo) > 1.5) return null;
        }
        int cheio = 0, fora = 0;
        var xs = new List<double>();
        for (var x = b.MinX + 0.5; x < b.MaxX; x += 1) xs.Add(x);
        var vx = xs.ToArray();
        var topo = new byte[vx.Length];
        var baixo = new byte[vx.Length];
        for (var y = b.MinY + 0.5; y < eixo; y += 1)
        {
            Caminho.LinhaDentro(corpo.Polis, y, vx, topo);
            Caminho.LinhaDentro(corpo.Polis, 2 * eixo - y, vx, baixo);
            for (var k = 0; k < vx.Length; k++)
            {
                if (topo[k] != 0 || baixo[k] != 0) cheio++;
                if (topo[k] != baixo[k]) fora++;
            }
        }
        return cheio > 0 && (double)fora / cheio < 0.02 ? eixo : null;
    }

    sealed class Grade(double x0, double y0, double passo, int nx, int ny, double[] sd, double[] h)
    {
        public double X0 = x0, Y0 = y0, Passo = passo;
        public int Nx = nx, Ny = ny;
        public double[] Sd = sd, H = h;
    }

    sealed class IndiceDeBordas
    {
        const int Celula = 6;
        readonly Dictionary<int, List<int>> _baldes = [];
        readonly List<double> _e = [];
        readonly double _ox, _oy;
        readonly int _vao;

        public IndiceDeBordas(IReadOnlyList<Ponto[]> polis, Limites b)
        {
            _ox = b.MinX - 4;
            _oy = b.MinY - 4;
            _vao = (int)Math.Ceiling((Math.Max(b.MaxX - b.MinX, b.MaxY - b.MinY) + 8) / Celula) + 1;
            foreach (var poli in polis)
            {
                for (int i = 0, j = poli.Length - 1; i < poli.Length; j = i++)
                {
                    var k = _e.Count;
                    _e.Add(poli[j].X); _e.Add(poli[j].Y); _e.Add(poli[i].X); _e.Add(poli[i].Y);
                    double x0 = Math.Min(poli[j].X, poli[i].X), x1 = Math.Max(poli[j].X, poli[i].X);
                    double y0 = Math.Min(poli[j].Y, poli[i].Y), y1 = Math.Max(poli[j].Y, poli[i].Y);
                    for (var cy = Cy(y0); cy <= Cy(y1); cy++)
                    {
                        for (var cx = Cx(x0); cx <= Cx(x1); cx++)
                        {
                            var chave = cy * _vao + cx;
                            if (!_baldes.TryGetValue(chave, out var lista)) { lista = []; _baldes[chave] = lista; }
                            lista.Add(k);
                        }
                    }
                }
            }
        }

        int Cx(double x) => Math.Max(0, Math.Min(_vao - 1, (int)Math.Floor((x - _ox) / Celula)));
        int Cy(double y) => Math.Max(0, Math.Min(_vao - 1, (int)Math.Floor((y - _oy) / Celula)));

        public double Distancia(double x, double y)
        {
            int bx = Cx(x), by = Cy(y);
            var melhor = double.PositiveInfinity;
            for (var r = 0; r < _vao; r++)
            {
                for (var cy = by - r; cy <= by + r; cy++)
                {
                    if (cy < 0 || cy >= _vao) continue;
                    var borda = cy == by - r || cy == by + r;
                    var passo = borda ? 1 : (2 * r == 0 ? 1 : 2 * r);
                    for (var cx = bx - r; cx <= bx + r; cx += passo)
                    {
                        if (cx < 0 || cx >= _vao) continue;
                        if (!_baldes.TryGetValue(cy * _vao + cx, out var lista)) continue;
                        foreach (var k in lista)
                        {
                            var d = SegDist2(x, y, _e[k], _e[k + 1], _e[k + 2], _e[k + 3]);
                            if (d < melhor) melhor = d;
                        }
                    }
                }
                if (melhor <= (double)(r * Celula) * (r * Celula)) break;
            }
            return Math.Sqrt(melhor);
        }
    }

    static Grade Amostrar(IReadOnlyList<Ponto[]> polis, Limites b, double passo, Func<double, double, double, double> altura)
    {
        const double margem = 1;
        double x0 = b.MinX - margem, y0 = b.MinY - margem;
        var nx = (int)Math.Ceiling((b.MaxX - b.MinX + 2 * margem) / passo) + 1;
        var ny = (int)Math.Ceiling((b.MaxY - b.MinY + 2 * margem) / passo) + 1;
        var sd = new double[nx * ny];
        var h = new double[nx * ny];
        var indice = new IndiceDeBordas(polis, b);
        var xs = new double[nx];
        for (var i = 0; i < nx; i++) xs[i] = x0 + i * passo;
        var dentro = new byte[nx];
        for (var j = 0; j < ny; j++)
        {
            var y = y0 + j * passo;
            Caminho.LinhaDentro(polis, y, xs, dentro);
            for (var i = 0; i < nx; i++)
            {
                var d = indice.Distancia(xs[i], y);
                sd[j * nx + i] = dentro[i] != 0 ? d : -d;
                h[j * nx + i] = dentro[i] != 0 ? altura(xs[i], y, d) : 0;
            }
        }
        return new Grade(x0, y0, passo, nx, ny, sd, h);
    }

    static Caixa CaixaDosLacos(IReadOnlyList<Ponto[]> lacos)
    {
        var b = Caminho.Limitar(lacos);
        return new Caixa(b.MinX, b.MinY, b.MaxX - b.MinX, b.MaxY - b.MinY);
    }

    record struct CaixaDeCelulas(int I0, int I1, int J0, int J1, double Topo);

    delegate double CampoDeNivel(double x, double y, double sd, double nivel);

    static List<Ponto[]> LacosDoNivel(Grade g, double nivel, CampoDeNivel campo, CaixaDeCelulas? caixa)
    {
        int i0 = caixa is { } c0 ? Math.Max(0, c0.I0 - 1) : 0, i1 = caixa is { } c1 ? Math.Min(g.Nx - 1, c1.I1 + 1) : g.Nx - 1;
        int j0 = caixa is { } c2 ? Math.Max(0, c2.J0 - 1) : 0, j1 = caixa is { } c3 ? Math.Min(g.Ny - 1, c3.J1 + 1) : g.Ny - 1;
        int nx = i1 - i0 + 1, ny = j1 - j0 + 1;
        var valores = new double[nx * ny];
        for (var j = 0; j < ny; j++)
        {
            for (var i = 0; i < nx; i++)
            {
                var k = (j + j0) * g.Nx + i + i0;
                var sd = g.Sd[k];
                var v = Math.Min(campo(g.X0 + (i + i0) * g.Passo, g.Y0 + (j + j0) * g.Passo, sd, nivel), sd);
                if (caixa is { } c) v = Math.Min(v, g.Passo * Math.Min(Math.Min(i + i0 - c.I0, c.I1 - i - i0), Math.Min(j + j0 - c.J0, c.J1 - j - j0)));
                valores[j * nx + i] = v;
            }
        }
        return Contorno.Lacos(new Campo(valores, nx, ny, g.X0 + i0 * g.Passo, g.Y0 + j0 * g.Passo, g.Passo), 0)
            .Select(l => Contorno.Simplificar(l, 0.04))
            .Where(l => l.Length >= 3)
            .ToList();
    }

    static List<CaixaDeCelulas> CaixasFinas(Grade g)
    {
        int nx = g.Nx, ny = g.Ny;
        var alcance = (int)Math.Ceiling(AlcanceFino / g.Passo);
        var linhas = new double[nx * ny];
        var largura = new double[nx * ny];
        for (var j = 0; j < ny; j++)
        {
            for (var i = 0; i < nx; i++)
            {
                double m = 0;
                for (int k = Math.Max(0, i - alcance), e = Math.Min(nx - 1, i + alcance); k <= e; k++) m = Math.Max(m, g.Sd[j * nx + k]);
                linhas[j * nx + i] = m;
            }
        }
        for (var i = 0; i < nx; i++)
        {
            for (var j = 0; j < ny; j++)
            {
                double m = 0;
                for (int k = Math.Max(0, j - alcance), e = Math.Min(ny - 1, j + alcance); k <= e; k++) m = Math.Max(m, linhas[k * nx + i]);
                largura[j * nx + i] = m;
            }
        }
        bool Fina(int k) => g.Sd[k] > 0 && largura[k] < Fino;
        var visto = new bool[nx * ny];
        var cresce = (int)Math.Ceiling(MargemFina / g.Passo);
        var caixas = new List<CaixaDeCelulas>();
        for (var inicio = 0; inicio < nx * ny; inicio++)
        {
            if (visto[inicio] || !Fina(inicio)) continue;
            int bi0 = nx, bi1 = -1, bj0 = ny, bj1 = -1;
            double topo = 0;
            var celulas = 0;
            var pilha = new Stack<int>();
            pilha.Push(inicio);
            visto[inicio] = true;
            while (pilha.Count > 0)
            {
                var k = pilha.Pop();
                int i = k % nx, j = (k - i) / nx;
                celulas++;
                topo = Math.Max(topo, g.H[k]);
                bi0 = Math.Min(bi0, i); bi1 = Math.Max(bi1, i);
                bj0 = Math.Min(bj0, j); bj1 = Math.Max(bj1, j);
                foreach (var n in new[] { i > 0 ? k - 1 : -1, i < nx - 1 ? k + 1 : -1, j > 0 ? k - nx : -1, j < ny - 1 ? k + nx : -1 })
                {
                    if (n >= 0 && !visto[n] && Fina(n)) { visto[n] = true; pilha.Push(n); }
                }
            }
            if (celulas * g.Passo * g.Passo < 2) continue;
            caixas.Add(new CaixaDeCelulas(Math.Max(0, bi0 - cresce), Math.Min(nx - 1, bi1 + cresce), Math.Max(0, bj0 - cresce), Math.Min(ny - 1, bj1 + cresce), topo));
        }
        for (var a = 0; a < caixas.Count; a++)
        {
            for (var b = a + 1; b < caixas.Count; b++)
            {
                var p = caixas[a];
                var q = caixas[b];
                if (p.I0 > q.I1 || q.I0 > p.I1 || p.J0 > q.J1 || q.J0 > p.J1) continue;
                caixas[a] = new CaixaDeCelulas(Math.Min(p.I0, q.I0), Math.Max(p.I1, q.I1), Math.Min(p.J0, q.J0), Math.Max(p.J1, q.J1), Math.Max(p.Topo, q.Topo));
                caixas.RemoveAt(b);
                b = a;
            }
        }
        return caixas;
    }

    static List<Ponto[]> LacosDaColuna(Grade g, int c0, int c1, Func<double, double, double> alturaDaBorda)
    {
        var lacos = new List<Ponto[]>();
        var sd = new double[g.Ny];
        Array.Fill(sd, double.NegativeInfinity);
        var h = new double[g.Ny];
        for (var j = 0; j < g.Ny; j++)
        {
            for (var c = c0; c <= c1; c++)
            {
                var k = j * g.Nx + c;
                if (g.Sd[k] > sd[j]) sd[j] = g.Sd[k];
                if (g.Sd[k] > 0 && g.H[k] > h[j]) h[j] = g.H[k];
            }
        }
        double YEm(int jj) => g.Y0 + jj * g.Passo;
        double Borda(int dentro, int fora) => YEm(dentro) + (YEm(fora) - YEm(dentro)) * sd[dentro] / (sd[dentro] - sd[fora]);
        double Aro(double y, int j)
        {
            double topo = 0;
            for (var c = c0; c <= c1; c++) topo = Math.Max(topo, alturaDaBorda(g.X0 + c * g.Passo, y));
            return Math.Min(h[j], topo);
        }
        var jx = 0;
        while (jx < g.Ny)
        {
            if (sd[jx] <= 0) { jx++; continue; }
            var j0 = jx;
            while (jx < g.Ny && sd[jx] > 0) jx++;
            var j1 = jx - 1;
            var yTopo = j0 > 0 ? Borda(j0, j0 - 1) : YEm(j0);
            var yBaixo = j1 < g.Ny - 1 ? Borda(j1, j1 + 1) : YEm(j1);
            var direita = new List<Ponto> { new(j0 > 0 ? Aro(yTopo, j0) : h[j0], yTopo) };
            for (var k = j0; k <= j1; k++) direita.Add(new Ponto(h[k], YEm(k)));
            direita.Add(new Ponto(j1 < g.Ny - 1 ? Aro(yBaixo, j1) : h[j1], yBaixo));
            if (direita.Max(p => p.X) < 0.05) continue;
            var esquerda = direita.Select(p => new Ponto(-p.X, p.Y)).Reverse();
            lacos.Add(Contorno.Simplificar([.. direita, .. esquerda], 0.04));
        }
        return lacos;
    }

    static List<double> Niveis(double plano, double redondo, int passos)
    {
        var saida = new List<double>();
        for (var k = 1; k < passos; k++) saida.Add(plano + redondo * Math.Sin((double)k / passos * (Math.PI / 2)));
        return saida;
    }

    public static VolumeDoAvatar Construir(DefinicaoAvatar def, double volume, AnaliseDoCorpo corpo)
    {
        var contornos = corpo.Subcaminhos.Where(s => !s.Buraco).ToList();
        var buracos = corpo.Subcaminhos.Where(s => s.Buraco).ToList();
        var v = double.IsFinite(volume) ? Math.Max(0, volume) : 1;
        var eixoX = EixoDeRevolucao(corpo);

        var partes = contornos.Select(o =>
        {
            var cortes = buracos.Where(h => h.Sonda is { } s && Caminho.Dentro(o.Polis, s.X, s.Y)).ToList();
            var polis = o.Polis.Concat(cortes.SelectMany(h => h.Polis)).ToList();
            var b = Caminho.Limitar(o.Polis);
            var centro = new Ponto((b.MinX + b.MaxX) / 2, (b.MinY + b.MaxY) / 2);
            var minDim = Math.Min(b.MaxX - b.MinX, b.MaxY - b.MinY);
            var passo = Math.Max(0.25, Math.Min(0.45, minDim / 24));

            var tipo = TipoDeParte.Inflada;
            Perfil? perfil = null;
            if (eixoX is not null)
            {
                perfil = PerfilDeRevolucao(polis, false, b.MinX, b.MaxX, eixoX.Value);
                if (perfil is not null) tipo = TipoDeParte.RevolveX;
            }
            else if (contornos.Count > 1 && cortes.Count == 0)
            {
                perfil = PerfilDeRevolucao(polis, true, b.MinY, b.MaxY, centro.X);
                if (perfil is not null) tipo = TipoDeParte.RevolveY;
            }

            double espessura, profundidade, rxy = 0, rz = 0;
            Func<double, double, double, double> alturaDe;
            CampoDeNivel campoDeNivel;
            if (tipo == TipoDeParte.Inflada || perfil is null)
            {
                var grosso = Amostrar(polis, b, Math.Max(passo, 0.6), (_, _, _) => 0);
                espessura = 2 * grosso.Sd.Aggregate(0.0, Math.Max);
                profundidade = ProfundidadeDaParte(espessura, b, v);
                rxy = Math.Min(espessura / 2, profundidade / 2) * 0.9;
                rz = profundidade / 2 * 0.85;
                double pf = profundidade, prxy = rxy, prz = rz;
                alturaDe = (_, _, sd) => SuperficieZ(pf, prxy, prz, sd);
                campoDeNivel = (_, _, sd, nivel) => sd - RecuoEm(pf, prxy, prz, nivel);
            }
            else
            {
                var p = perfil;
                var eixo = tipo == TipoDeParte.RevolveX ? eixoX!.Value : centro.X;
                var raio = p.R.Max();
                espessura = 2 * raio;
                profundidade = 2 * raio * v;
                var revolveX = tipo == TipoDeParte.RevolveX;
                alturaDe = revolveX
                    ? (x, y, _) => v * Math.Sqrt(Math.Max(0, Q(RaioEm(p, x)) - Q(y - eixo)))
                    : (x, y, _) => v * Math.Sqrt(Math.Max(0, Q(RaioEm(p, y)) - Q(x - eixo)));
                campoDeNivel = (x, y, _, nivel) =>
                {
                    var r = RaioEm(p, revolveX ? x : y);
                    var t = revolveX ? y - eixo : x - eixo;
                    return (r * r - t * t - Q(nivel / Math.Max(v, 1e-6))) / (2 * Math.Max(r, 0.5));
                };
            }
            var tipoFinal = tipo;
            double Altura(double x, double y) =>
                Caminho.Dentro(polis, x, y) ? alturaDe(x, y, tipoFinal == TipoDeParte.Inflada ? DistanciaDaBorda(polis, x, y) : 0) : 0;

            var dCore = o.D + string.Concat(cortes.Select(h => h.D));
            var desenhoCore = o.Segmentos.Concat(cortes.SelectMany(h => h.Segmentos)).ToList();
            var core = new FatiaDeVolume('z', 0, dCore, desenhoCore, polis, false, new Caixa(b.MinX, b.MinY, b.MaxX - b.MinX, b.MaxY - b.MinY), 0);
            var fatiasZ = new List<FatiaDeVolume> { core };
            var fatiasX = new List<FatiaDeVolume>();
            var meia = profundidade / 2;
            if (meia > 0.05)
            {
                var g = Amostrar(polis, b, passo, alturaDe);
                var passos = tipo == TipoDeParte.Inflada
                    ? Math.Max(4, Math.Min(14, (int)Js.Arredondar(rz / 0.8)))
                    : Math.Max(5, Math.Min(18, (int)Js.Arredondar(meia / 1.1)));
                var ls = tipo == TipoDeParte.Inflada ? new List<double> { meia - rz }.Concat(Niveis(meia - rz, rz, passos)).ToList() : Niveis(0, meia, passos);
                bool Adicionar(double nivel, CaixaDeCelulas? caixa)
                {
                    var lacos = LacosDoNivel(g, nivel, campoDeNivel, caixa);
                    if (lacos.Count == 0) return false;
                    var amostra = lacos.SelectMany(l => l).Where((_, k) => k % 3 == 0);
                    var recuo = amostra.Min(p => DistanciaDaBorda(polis, p.X, p.Y));
                    var (d, desenho) = Contorno.ParaCaminho(lacos);
                    var caixaL = CaixaDosLacos(lacos);
                    fatiasZ.Add(new FatiaDeVolume('z', nivel, d, desenho, lacos, true, caixaL, recuo));
                    fatiasZ.Add(new FatiaDeVolume('z', -nivel, d, desenho, lacos, true, caixaL, recuo));
                    return true;
                }
                var usados = new List<double> { 0 };
                foreach (var nivel in ls) if (nivel >= 0.02 && Adicionar(nivel, null)) usados.Add(nivel);
                var finas = tipo == TipoDeParte.Inflada ? CaixasFinas(g) : [];
                foreach (var caixa in finas)
                {
                    for (var k = 1; k < usados.Count && usados[k - 1] < caixa.Topo; k++)
                    {
                        double a = usados[k - 1], vao = usados[k] - a;
                        var m = (int)Math.Ceiling(vao / FinoZ - 1e-9);
                        for (var q = 1; q < m && a + vao * q / m < caixa.Topo; q++) Adicionar(a + vao * q / m, caixa);
                    }
                }
                fatiasZ = fatiasZ.OrderBy(f => f.Em).ToList();

                var largura = b.MaxX - b.MinX;
                var cada = Math.Max(1, (int)Js.Arredondar(Math.Max(1.1, largura / MaxFatiasX) / g.Passo));
                double AlturaDaBorda(double x, double y) => alturaDe(x, y, 0);
                var antes = (cada - 1) / 2;
                for (var i = 1; i < g.Nx - 1; i += cada)
                {
                    var lacos = LacosDaColuna(g, Math.Max(0, i - antes), Math.Min(g.Nx - 1, i - antes + cada - 1), AlturaDaBorda);
                    if (lacos.Count == 0) continue;
                    var (d, desenho) = Contorno.ParaCaminho(lacos);
                    fatiasX.Add(new FatiaDeVolume('x', g.X0 + i * g.Passo, d, desenho, lacos, true, CaixaDosLacos(lacos), 0));
                }
            }
            return new ParteDoVolume(tipo, dCore, polis, b, centro, espessura, profundidade, rxy, rz,
                tipo == TipoDeParte.RevolveX ? eixoX : tipo == TipoDeParte.RevolveY ? centro.X : null, fatiasZ, fatiasX, Altura);
        }).ToList();

        var escalaDosOlhos = EscalaRealcada(corpo, def.Olhos);
        var (ca, cb) = Corpo.CentrosDosOlhos(def.Olhos);
        Ponto[] centros = [ca, cb];
        int parteDosOlhos = 0;
        var melhorZ = double.NegativeInfinity;
        for (var i = 0; i < partes.Count; i++)
        {
            var zs = centros.Select(c => partes[i].Altura(c.X, c.Y)).ToArray();
            if (zs.Any(z => z <= 0)) continue;
            var z = zs.Min();
            if (z > melhorZ) { melhorZ = z; parteDosOlhos = i; }
        }
        var hospede = partes.Count > parteDosOlhos ? partes[parteDosOlhos] : null;
        double w = Corpo.OlhoL * escalaDosOlhos / 2 + Corpo.OlharX * escalaDosOlhos, hh = Corpo.OlhoA * escalaDosOlhos / 2 + Corpo.OlharY * escalaDosOlhos;
        double[] fs = [-1, -0.5, 0, 0.5, 1];
        var olhoZ = centros.Select(c =>
        {
            double topo = 0;
            foreach (var fx in fs) foreach (var fy in fs) topo = Math.Max(topo, hospede is not null ? hospede.Altura(c.X + fx * w, c.Y + fy * hh) : 0);
            return topo;
        }).ToArray();

        Vetor3 Normal(double x, double y)
        {
            if (hospede is null) return new Vetor3(0, 0, 1);
            const double e = 0.75;
            var gx = (hospede.Altura(x + e, y) - hospede.Altura(x - e, y)) / (2 * e);
            var gy = (hospede.Altura(x, y + e) - hospede.Altura(x, y - e)) / (2 * e);
            var n = Js.Hypot(gx, gy, 1);
            return new Vetor3(-gx / n, -gy / n, 1 / n);
        }
        var meioOlho = Corpo.OlhoL * escalaDosOlhos / 2;
        var normais = centros.Select(c => new[] { Normal(c.X - meioOlho, c.Y), Normal(c.X, c.Y), Normal(c.X + meioOlho, c.Y) }).ToArray();
        return new VolumeDoAvatar(partes, corpo.Limites, parteDosOlhos, olhoZ, escalaDosOlhos, normais);
    }

    public static double EscalaRealcada(AnaliseDoCorpo corpo, OlhosAvatar olhos)
    {
        foreach (var k in new[] { Realce, 1.38, 1.3, 1.22, 1.15, 1.08 })
            if (Corpo.OlhosDesenhadosCabem(corpo.Polis, olhos with { Escala = olhos.Escala * k }, 0, 0, FolgaDoRealce) is null) return olhos.Escala * k;
        return olhos.Escala;
    }
}
