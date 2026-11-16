using SkiaSharp;

namespace Nexora.Fundos;

sealed class Pulso(Paleta paleta) : MotorNexora(paleta, 17)
{
    const float Celula = 150;

    sealed class Sinal
    {
        public int De;
        public int Para;
        public float Andado;
        public float Velocidade;
    }

    readonly Lote _linhas = new();
    readonly Lote _rastro = new();
    readonly Lote _cabecas = new();
    readonly Lote _brilhos = new();
    readonly Lote _pontos = new();
    readonly List<Sinal> _sinais = [];
    SKPoint[] _fracao = [];
    Estrela[] _nos = [];
    float[] _flash = [];
    List<int>[] _vizinhos = [];
    (int A, int B)[] _arestas = [];
    Estrela[] _fundo = [];
    int _cols;
    int _rows;
    float _acumulado;
    double _relogio;

    protected override void Medir()
    {
        _fundo = Semear(_fundo, Math.Clamp((int)(W * H / 8000), 20, 360));
        int cols = Math.Max(2, (int)MathF.Round(W / Celula)), rows = Math.Max(2, (int)MathF.Round(H / Celula));
        if (cols == _cols && rows == _rows)
        {
            Posicionar();
            return;
        }
        (_cols, _rows) = (cols, rows);
        var sorte = Fresca(0);
        var n = cols * rows;
        _fracao = new SKPoint[n];
        _nos = new Estrela[n];
        for (var j = 0; j < rows; j++)
            for (var i = 0; i < cols; i++)
            {
                var k = j * cols + i;
                _fracao[k] = new SKPoint((i + 0.18f + 0.64f * sorte.NextSingle()) / cols, (j + 0.18f + 0.64f * sorte.NextSingle()) / rows);
                _nos[k] = NovaEstrela(sorte, 0, 0, 2);
            }
        _flash = new float[n];
        Posicionar();
        _vizinhos = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
        var arestas = new HashSet<(int, int)>();
        var limite = 2.2f * MathF.Max(W / cols, H / rows);
        for (var a = 0; a < n; a++)
            foreach (var b in Enumerable.Range(0, n).Where(b => b != a)
                .OrderBy(b => Distancia(_nos[a].X - _nos[b].X, _nos[a].Y - _nos[b].Y)).Take(2))
            {
                if (Distancia(_nos[a].X - _nos[b].X, _nos[a].Y - _nos[b].Y) > limite || !arestas.Add((Math.Min(a, b), Math.Max(a, b)))) continue;
                _vizinhos[a].Add(b);
                _vizinhos[b].Add(a);
            }
        _arestas = [.. arestas];
        _sinais.Clear();
        for (var i = 0; i < 6; i++) Lancar(sorte.Next(n), sorte.NextSingle());
    }

    void Posicionar()
    {
        var tempo = (float)(_relogio % 100000);
        for (var i = 0; i < _nos.Length; i++)
        {
            ref var e = ref _nos[i];
            e.X = _fracao[i].X * W + 5 * MathF.Sin(tempo * 0.31f + e.Fase);
            e.Y = _fracao[i].Y * H + 5 * MathF.Cos(tempo * 0.27f + e.Fase * 1.7f);
        }
    }

    void Lancar(int de, float andado = 0)
    {
        if (_vizinhos[de].Count == 0) return;
        _sinais.Add(new Sinal { De = de, Para = _vizinhos[de][Sorte.Next(_vizinhos[de].Count)], Andado = andado, Velocidade = 110 + 70 * Sorte.NextSingle() });
        _flash[de] = MathF.Max(_flash[de], 0.5f);
    }

    protected override void AoClicar(float x, float y)
    {
        if (_nos.Length == 0) return;
        var perto = Enumerable.Range(0, _nos.Length).MinBy(i => Distancia(_nos[i].X - x, _nos[i].Y - y));
        _flash[perto] = 1;
        foreach (var v in _vizinhos[perto]) _sinais.Add(new Sinal { De = perto, Para = v, Velocidade = 170 + 40 * Sorte.NextSingle() });
    }

    protected override void Avancar(float dt)
    {
        _relogio += dt;
        Derivar(_fundo, -1.2f * dt, 0.4f * dt);
        Posicionar();
        for (var i = 0; i < _flash.Length; i++) _flash[i] = MathF.Max(0, _flash[i] - dt * 1.3f);
        var limite = 30 + 40 * Atividade;
        _acumulado += dt * (0.7f + 3.3f * Atividade);
        while (_acumulado >= 1)
        {
            _acumulado -= 1;
            if (_sinais.Count < limite && _nos.Length > 0) Lancar(Sorte.Next(_nos.Length));
        }
        var ritmo = 1 + 0.9f * Atividade;
        for (var i = _sinais.Count - 1; i >= 0; i--)
        {
            var s = _sinais[i];
            var comprimento = MathF.Max(1, Distancia(_nos[s.Para].X - _nos[s.De].X, _nos[s.Para].Y - _nos[s.De].Y));
            s.Andado += s.Velocidade * ritmo * dt / comprimento;
            if (s.Andado < 1) continue;
            _flash[s.Para] = 1;
            var opcoes = _vizinhos[s.Para].Where(v => v != s.De).ToList();
            if (opcoes.Count == 0 || Sorte.NextSingle() < 0.25f - 0.12f * Atividade || _sinais.Count > limite)
            {
                _sinais.RemoveAt(i);
                continue;
            }
            (s.De, s.Para, s.Andado) = (s.Para, opcoes[Sorte.Next(opcoes.Count)], 0);
        }
    }

    protected override void Desenhar(SKCanvas tela, double t)
    {
        DesenharEstrelas(tela, _fundo, t, 0.5f);
        foreach (var (a, b) in _arestas)
        {
            ref readonly var na = ref _nos[a];
            ref readonly var nb = ref _nos[b];
            var alfa = 0.13f + 0.32f * MathF.Max(_flash[a], _flash[b]) + 0.06f * Atividade;
            alfa *= 1 + 1.4f * Realce((na.X + nb.X) / 2, (na.Y + nb.Y) / 2);
            _linhas.Linha(na.X, na.Y, nb.X, nb.Y, alfa);
        }
        Linhas(tela, _linhas, P.Linha);
        foreach (var s in _sinais)
        {
            ref readonly var de = ref _nos[s.De];
            ref readonly var para = ref _nos[s.Para];
            float dx = para.X - de.X, dy = para.Y - de.Y, comprimento = MathF.Max(1, Distancia(dx, dy));
            float x = de.X + dx * s.Andado, y = de.Y + dy * s.Andado;
            _brilhos.Ponto(x, y, 0.22f);
            _cabecas.Ponto(x, y, 0.95f);
            for (var k = 1; k <= 6; k++)
            {
                var atras = s.Andado - k * 6 / comprimento;
                if (atras < 0) break;
                _rastro.Ponto(de.X + dx * atras, de.Y + dy * atras, 0.7f * (1 - k / 7f));
            }
        }
        Pontos(tela, _brilhos, P.Destaque, 12);
        Pontos(tela, _rastro, P.Destaque, 2.2f);
        Pontos(tela, _cabecas, P.Anel, 3.2f);
        for (var i = 0; i < _nos.Length; i++)
        {
            ref readonly var e = ref _nos[i];
            _pontos.Ponto(e.X, e.Y, MathF.Min(1, 0.35f + 0.5f * Cintilar(e, t) + _flash[i] * 0.4f + 0.4f * Realce(e.X, e.Y)));
            if (_flash[i] > 0.02f) Anel(tela, e.X, e.Y, 4 + 18 * (1 - _flash[i]), P.Destaque, _flash[i] * 0.6f, 1.2f);
        }
        Pontos(tela, _pontos, P.No, 4.2f);
    }
}
