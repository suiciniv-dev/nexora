using SkiaSharp;

namespace Nexora.Fundos;

sealed class Constelacao(Paleta paleta, bool carta) : MotorNexora(paleta, 11)
{
    static readonly float[] Giro = [0.0030f, 0.0042f, 0.0056f];

    readonly Lote _linhas = new();
    readonly SKPathEffect _tracejado = SKPathEffect.CreateDash([3, 6], 0);
    readonly double[] _giro = new double[3];
    SKPoint _polo;
    float _inicio;
    float _faixa;
    float _raioMinimo;
    float _raioMaximo;
    Estrela[] _estrelas = [];
    float[] _raio = [];
    float[] _angulo = [];
    (int A, int B)[] _ligacoes = [];
    bool[] _ligada = [];

    protected override (float Min, float Max)? Cadentes => carta ? null : (9, 20);

    void Geometria()
    {
        _polo = new SKPoint(-0.2f * W, 1.9f * H);
        float px = Math.Clamp(_polo.X, 0, W), py = Math.Clamp(_polo.Y, 0, H);
        _raioMinimo = Distancia(px - _polo.X, py - _polo.Y);
        SKPoint[] cantos = [new(0, 0), new(W, 0), new(0, H), new(W, H)];
        _raioMaximo = cantos.Max(c => Distancia(c.X - _polo.X, c.Y - _polo.Y)) + 10;
        var angulos = cantos.Select(c => MathF.Atan2(c.Y - _polo.Y, c.X - _polo.X)).ToArray();
        _inicio = angulos.Min() - 0.02f;
        _faixa = angulos.Max() + 0.02f - _inicio;
    }

    protected override void Medir()
    {
        Geometria();
        var area = 0.5f * _faixa * (_raioMaximo * _raioMaximo - _raioMinimo * _raioMinimo);
        int[] quantas = [Math.Clamp((int)(area / 4200), 40, 900), Math.Clamp((int)(area / 13000), 14, 300), Math.Clamp((int)(area / 30000), 10, 140)];
        var total = quantas.Sum();
        _estrelas = new Estrela[total];
        _raio = new float[total];
        _angulo = new float[total];
        var k = 0;
        for (var c = 0; c < 3; c++)
        {
            var sorte = Fresca(c);
            for (var i = 0; i < quantas[c]; i++, k++)
            {
                _estrelas[k] = NovaEstrela(sorte, 0, 0, c);
                _raio[k] = MathF.Sqrt(_raioMinimo * _raioMinimo + sorte.NextSingle() * (_raioMaximo * _raioMaximo - _raioMinimo * _raioMinimo));
                _angulo[k] = sorte.NextSingle() * _faixa;
            }
        }
        Posicionar();
        Ligar(total - quantas[2], total);
    }

    void Ligar(int de, int ate)
    {
        var pares = new List<(int A, int B, float D)>();
        for (var i = de; i < ate; i++)
            for (var j = i + 1; j < ate; j++)
            {
                var d = Distancia(_estrelas[i].X - _estrelas[j].X, _estrelas[i].Y - _estrelas[j].Y);
                if (d < 220) pares.Add((i, j, d));
            }
        var grau = new int[_estrelas.Length];
        var sorte = Fresca(9);
        var fora = Enumerable.Range(0, _estrelas.Length).Select(_ => sorte.NextSingle() < 0.2f).ToArray();
        var limite = Enumerable.Range(0, _estrelas.Length).Select(_ => sorte.NextSingle() < 0.18f ? 3 : 2).ToArray();
        var grupo = Enumerable.Range(0, _estrelas.Length).ToArray();
        int Raiz(int i)
        {
            while (grupo[i] != i) i = grupo[i] = grupo[grupo[i]];
            return i;
        }
        var ligacoes = new List<(int, int)>();
        foreach (var (a, b, _) in pares.OrderBy(p => p.D))
        {
            if (fora[a] || fora[b] || grau[a] >= limite[a] || grau[b] >= limite[b]) continue;
            int ra = Raiz(a), rb = Raiz(b);
            if (ra == rb) continue;
            grupo[ra] = rb;
            grau[a]++;
            grau[b]++;
            ligacoes.Add((a, b));
        }
        _ligacoes = [.. ligacoes];
        _ligada = grau.Select(g => g > 0).ToArray();
    }

    void Posicionar()
    {
        for (var i = 0; i < _estrelas.Length; i++)
        {
            ref var e = ref _estrelas[i];
            var giro = (float)(_giro[e.Camada] % _faixa);
            var a = _angulo[i] + giro;
            if (a >= _faixa) a -= _faixa;
            a += _inicio;
            e.X = _polo.X + _raio[i] * MathF.Cos(a);
            e.Y = _polo.Y + _raio[i] * MathF.Sin(a);
        }
    }

    protected override void Avancar(float dt)
    {
        var ritmo = dt * (1 + 1.5f * Atividade);
        for (var c = 0; c < 3; c++) _giro[c] += Giro[c] * ritmo;
        Posicionar();
    }

    void Grade(SKCanvas tela)
    {
        Traco.Color = Alfa(P.Linha, 0.1f);
        for (var r = MathF.Ceiling(_raioMinimo / 150) * 150; r < _raioMaximo; r += 150) tela.DrawCircle(_polo, r, Traco);
        Traco.PathEffect = _tracejado;
        var giro = (float)(_giro[2] % 0.12);
        for (var a = MathF.Floor(_inicio / 0.12f) * 0.12f + giro; a < _inicio + _faixa; a += 0.12f)
            tela.DrawLine(_polo.X + _raioMinimo * MathF.Cos(a), _polo.Y + _raioMinimo * MathF.Sin(a),
                _polo.X + _raioMaximo * MathF.Cos(a), _polo.Y + _raioMaximo * MathF.Sin(a), Traco);
        Traco.PathEffect = null;
    }

    protected override void Desenhar(SKCanvas tela, double t)
    {
        if (carta) Grade(tela);
        var forca = (carta ? 0.42f : 0.26f) + 0.08f * Atividade;
        foreach (var (a, b) in _ligacoes)
        {
            ref readonly var ea = ref _estrelas[a];
            ref readonly var eb = ref _estrelas[b];
            var d = Distancia(ea.X - eb.X, ea.Y - eb.Y);
            if (d > 230) continue;
            var alfa = forca * (1 + 1.4f * Realce((ea.X + eb.X) / 2, (ea.Y + eb.Y) / 2));
            _linhas.Linha(ea.X, ea.Y, eb.X, eb.Y, alfa);
        }
        Linhas(tela, _linhas, P.Linha, carta ? 1.1f : 1);
        DesenharEstrelas(tela, _estrelas, t, carta ? 0 : 1);
        for (var i = 0; i < _estrelas.Length; i++)
        {
            if (!_ligada[i]) continue;
            ref readonly var e = ref _estrelas[i];
            if (e.X < -10 || e.Y < -10 || e.X > W + 10 || e.Y > H + 10) continue;
            Anel(tela, e.X, e.Y, carta ? 4.2f : 3.8f, P.Anel, (carta ? 0.55f : 0.32f) + 0.5f * Realce(e.X, e.Y), 1.1f);
        }
    }

    public override void Dispose()
    {
        _tracejado.Dispose();
        base.Dispose();
    }
}
