namespace Nexora.Core.Avatares;

public enum HumorAvatar { Parado, Pensando, Feliz }

public sealed record MisturaDeHumor(HumorAvatar De, MisturaDeHumor? Anterior, double T);

public readonly record struct Olhar(double X, double Y, double Peso = 1);

public readonly record struct Vista(double Guinada, double Arfagem);

public sealed record PoseAvatar
{
    public double Guinada { get; set; }
    public double Arfagem { get; set; }
    public double Rolagem { get; set; }
    public double Elevacao { get; set; }
    public double Sx { get; set; } = 1;
    public double Sy { get; set; } = 1;
    public double OlharX { get; set; }
    public double OlharY { get; set; }
    public double Piscar { get; set; }
    public double ApertoE { get; set; }
    public double ApertoD { get; set; }
    public bool OlhosFelizes { get; set; }
}

public sealed record EntradaDePose(uint Semente, double Tempo, HumorAvatar Humor, Olhar? Olhar, bool Reduzido, Vista? Vista, MisturaDeHumor? Mistura);

public static class Pose
{
    public const double GuinadaBase = -0.3;
    public const double ArfagemBase = 0.2;
    public const double GuinadaDoOlhar = 0.32;
    public const double ArfagemDoOlhar = 0.18;
    public const double OlhosDoOlhar = 0.7;
    const double Tau = Math.PI * 2;

    public static uint Hash(string s)
    {
        var h = 2166136261u;
        foreach (var c in s)
        {
            h ^= c;
            h = unchecked(h * 16777619u);
        }
        return h;
    }

    public static double Ruido(uint semente, double n)
    {
        var ni = unchecked((uint)(int)(long)n);
        var h = semente ^ unchecked(ni * 0x9e3779b1u);
        h = unchecked((h ^ (h >> 16)) * 0x85ebca6bu);
        h = unchecked((h ^ (h >> 13)) * 0xc2b2ae35u);
        h ^= h >> 16;
        return h / 4294967296.0;
    }

    static double Limitar(double v, double lo, double hi) => Math.Min(hi, Math.Max(lo, v));

    static double Suave(double u)
    {
        var x = Limitar(u, 0, 1);
        return x * x * (3 - 2 * x);
    }

    public static double PiscarEm(uint semente, double t)
    {
        const double fatia = 4.2, duracao = 0.19;
        var s = Math.Floor(t / fatia);
        double fechamento = 0;
        foreach (var f in new[] { s - 1, s })
        {
            var inicio = f * fatia + 0.5 + Ruido(semente, f * 2) * 2.9;
            var inicios = Ruido(semente, f * 2 + 1) > 0.8 ? new[] { inicio, inicio + 0.34 } : [inicio];
            foreach (var b in inicios)
            {
                var u = (t - b) / duracao;
                if (u > 0 && u < 1) fechamento = Math.Max(fechamento, Math.Sin(Math.PI * u));
            }
        }
        return fechamento;
    }

    public static (double X, double Y) VagarEm(uint semente, double t)
    {
        const double fatia = 1.9;
        var s = Math.Floor(t / fatia);
        (double, double) Alvo(double k)
        {
            if (Ruido(semente, 1000 + k * 3) < 0.4) return (0, 0);
            return ((Ruido(semente, 1001 + k * 3) * 2 - 1) * 0.6, (Ruido(semente, 1002 + k * 3) * 2 - 1) * 0.35);
        }
        var a = Alvo(s - 1);
        var b = Alvo(s);
        var e = Suave((t - s * fatia) / 0.16);
        return (a.Item1 + (b.Item1 - a.Item1) * e, a.Item2 + (b.Item2 - a.Item2) * e);
    }

    public static PoseAvatar Misturar(PoseAvatar a, PoseAvatar b, double t)
    {
        var k = Limitar(t, 0, 1);
        double M(double x, double y) => x + (y - x) * k;
        var pose = new PoseAvatar
        {
            Guinada = M(a.Guinada, b.Guinada), Arfagem = M(a.Arfagem, b.Arfagem), Rolagem = M(a.Rolagem, b.Rolagem), Elevacao = M(a.Elevacao, b.Elevacao),
            Sx = M(a.Sx, b.Sx), Sy = M(a.Sy, b.Sy), OlharX = M(a.OlharX, b.OlharX), OlharY = M(a.OlharY, b.OlharY),
            Piscar = M(a.Piscar, b.Piscar), ApertoE = M(a.ApertoE, b.ApertoE), ApertoD = M(a.ApertoD, b.ApertoD),
            OlhosFelizes = b.OlhosFelizes,
        };
        if (a.OlhosFelizes != b.OlhosFelizes)
        {
            pose.OlhosFelizes = k < 0.5 ? a.OlhosFelizes : b.OlhosFelizes;
            pose.Piscar = Math.Max(pose.Piscar, 1 - Math.Abs(2 * k - 1));
        }
        return pose;
    }

    public static PoseAvatar Calcular(EntradaDePose entrada)
    {
        var pose = DoHumor(entrada);
        if (entrada.Mistura is not { } mistura || !(mistura.T < 1)) return pose;
        var anterior = Calcular(entrada with { Humor = mistura.De, Mistura = mistura.Anterior });
        return Misturar(anterior, pose, double.IsFinite(mistura.T) ? mistura.T : 1);
    }

    static PoseAvatar DoHumor(EntradaDePose e)
    {
        var reduzido = e.Reduzido;
        var t = reduzido ? 0 : e.Tempo + Ruido(e.Semente, 7) * 10;
        double Onda(double periodo, double fase = 0) => reduzido ? 0 : Math.Sin(Tau * t / periodo + fase);
        (double X, double Y)? dado = e.Olhar is { } o && double.IsFinite(o.X) && double.IsFinite(o.Y) ? (Limitar(o.X, -1, 1), Limitar(o.Y, -1, 1)) : null;
        var peso = dado is not null ? (double.IsFinite(e.Olhar!.Value.Peso) ? Limitar(e.Olhar.Value.Peso, 0, 1) : 1) : 0;
        var balanco = 1 - 0.7 * peso;

        var pose = new PoseAvatar { Guinada = GuinadaBase, Arfagem = ArfagemBase };

        if (e.Humor == HumorAvatar.Feliz)
        {
            const double periodo = 0.92;
            var p = reduzido ? 0.5 : (t % periodo + periodo) % periodo / periodo;
            var ar = 4 * p * (1 - p);
            var pouso = reduzido ? 0 : Limitar(1 - Math.Min(p, 1 - p) / 0.12, 0, 1);
            pose.Elevacao = reduzido ? 0 : -3.2 * ar;
            pose.Sy = 1 - 0.09 * pouso + 0.03 * ar * (reduzido ? 0 : 1);
            pose.Sx = 1 + 0.07 * pouso - 0.015 * ar * (reduzido ? 0 : 1);
            pose.Rolagem = 4.5 * Onda(periodo * 2);
            pose.Guinada += 0.1 * Onda(periodo * 2, 0.6);
            pose.OlhosFelizes = true;
        }
        else if (e.Humor == HumorAvatar.Pensando)
        {
            pose.Guinada += 0.12 + 0.05 * balanco * Onda(7.3);
            pose.Arfagem += 0.08;
            pose.Rolagem = -5 + 1.4 * Onda(4.1);
            pose.Elevacao = -0.5 * (1 - Math.Cos(Tau * t / 5.2)) * (reduzido ? 0 : 1);
            pose.OlharX = 0.55 + 0.3 * Onda(5.6);
            pose.OlharY = -0.8;
            pose.ApertoE = 0.22;
            pose.ApertoD = 0.32 + 0.1 * Onda(3.1);
            if (!reduzido) pose.Piscar = PiscarEm(e.Semente, t * 0.7);
        }
        else
        {
            pose.Guinada += 0.07 * balanco * Onda(6.1);
            pose.Arfagem += 0.04 * balanco * Onda(4.7, 1);
            pose.Rolagem = 1.6 * Onda(5.3, 2);
            pose.Elevacao = reduzido ? 0 : -0.45 * (1 - Math.Cos(Tau * t / 3.4));
            var respiro = Onda(3.4, -Math.PI / 2);
            pose.Sy = 1 + 0.012 * respiro;
            pose.Sx = 1 - 0.008 * respiro;
            if (!reduzido)
            {
                pose.Piscar = PiscarEm(e.Semente, t);
                (pose.OlharX, pose.OlharY) = VagarEm(e.Semente, t);
            }
        }

        if (dado is { } alvo && peso > 0)
        {
            double M(double a, double b) => a + (b - a) * peso;
            var parte = reduzido ? 1 : OlhosDoOlhar;
            if (!pose.OlhosFelizes)
            {
                pose.OlharX = M(pose.OlharX, alvo.X * parte);
                pose.OlharY = M(pose.OlharY, alvo.Y * parte);
            }
            if (!reduzido)
            {
                pose.Guinada += peso * (alvo.X * GuinadaDoOlhar - GuinadaBase);
                pose.Arfagem += peso * (-alvo.Y * ArfagemDoOlhar - ArfagemBase / 2);
            }
        }
        if (e.Vista is { } vista && double.IsFinite(vista.Guinada) && double.IsFinite(vista.Arfagem))
        {
            pose.Guinada += vista.Guinada;
            pose.Arfagem += vista.Arfagem;
        }
        return pose;
    }
}

public static class Humor
{
    public const double DuracaoMs = 420;

    static double Suave(double t)
    {
        var x = Math.Min(1, Math.Max(0, t));
        return x * x * (3 - 2 * x);
    }

    public static double Progresso(double decorridoMs) => Suave(decorridoMs / DuracaoMs);

    public static (HumorAvatar Humor, MisturaDeHumor? Mistura) Assentar(HumorAvatar humor, MisturaDeHumor? mistura, double parte = 1, int profundidade = 0)
    {
        if (mistura is null) return (humor, null);
        var resto = parte * (1 - mistura.T);
        if (resto < 0.02 || profundidade >= 3) return (humor, null);
        var de = mistura.Anterior is null ? (mistura.De, (MisturaDeHumor?)null) : Assentar(mistura.De, mistura.Anterior, resto, profundidade + 1);
        return (humor, new MisturaDeHumor(de.Item1, de.Item2, mistura.T));
    }
}

public readonly record struct EstadoDoOlhar(double X, double Y, double Atencao);

public static class Seguir
{
    public static readonly EstadoDoOlhar Repouso = new(0, 0, 0);
    const double TauOlhar = 0.12;
    const double TauAtencao = 0.3;
    const double Alcance = 1;
    const double TamanhoMinimo = 160;

    public static (double X, double Y) OlharParaPonteiro(double px, double py, double esquerda, double topo, double largura, double altura, double origemX = 0.5, double origemY = 0.5)
    {
        var alcance = Math.Max(Math.Max(largura, altura), TamanhoMinimo) * Alcance;
        var dx = (px - (esquerda + largura * origemX)) / alcance;
        var dy = (py - (topo + altura * origemY)) / alcance;
        var m = Js.Hypot(dx, dy);
        if (!double.IsFinite(m) || m < 1e-9) return (0, 0);
        var k = Math.Tanh(m) / m;
        return (dx * k, dy * k);
    }

    public static (EstadoDoOlhar Estado, bool Movendo) Passo(EstadoDoOlhar estado, (double X, double Y)? alvo, double dt, bool reduzido)
    {
        var meta = alvo is { } a ? new EstadoDoOlhar(a.X, a.Y, 1) : Repouso;
        var passo = Math.Max(0, Math.Min(dt, 0.1));
        double Facilitar(double tau) => reduzido ? 1 : 1 - Math.Exp(-passo / tau);
        var k = Facilitar(alvo is not null ? TauOlhar : TauAtencao);
        var proximo = new EstadoDoOlhar(
            estado.X + (meta.X - estado.X) * k,
            estado.Y + (meta.Y - estado.Y) * k,
            estado.Atencao + (meta.Atencao - estado.Atencao) * Facilitar(TauAtencao));
        var movendo = Math.Abs(meta.X - proximo.X) > 0.002 || Math.Abs(meta.Y - proximo.Y) > 0.002 || Math.Abs(meta.Atencao - proximo.Atencao) > 0.002;
        return (movendo ? proximo : meta, movendo);
    }

    public static Olhar? OlharDoEstado(EstadoDoOlhar estado)
    {
        var a = Math.Min(1, Math.Max(0, estado.Atencao));
        return a > 0 ? new Olhar(estado.X, estado.Y, a * a * (3 - 2 * a)) : null;
    }
}

public sealed class Animador
{
    EstadoDoOlhar _seguir = Seguir.Repouso;
    double? _ultimo;
    HumorAvatar? _humor;
    MisturaDeHumor? _mistura;
    double _desde;

    public (Olhar? Olhar, MisturaDeHumor? Mistura, bool Movendo) Passo(double agoraMs, HumorAvatar humor, (double X, double Y)? alvo, bool segurar, bool reduzido)
    {
        var dt = _ultimo is null ? 0 : (agoraMs - _ultimo.Value) / 1000;
        _ultimo = agoraMs;
        var movendo = false;
        if (!segurar) (_seguir, movendo) = Seguir.Passo(_seguir, alvo, dt, reduzido);

        if (_humor is { } atual && humor != atual)
        {
            var mostrada = _mistura is null ? null : new MisturaDeHumor(_mistura.De, _mistura.Anterior, Humor.Progresso(agoraMs - _desde));
            if (reduzido) _mistura = null;
            else
            {
                var (de, anterior) = Humor.Assentar(atual, mostrada);
                _mistura = new MisturaDeHumor(de, anterior, 0);
            }
            _humor = humor;
            _desde = agoraMs;
        }
        if (_humor is null)
        {
            _humor = humor;
            _desde = agoraMs;
        }
        var t = reduzido ? 1 : Humor.Progresso(agoraMs - _desde);
        MisturaDeHumor? mistura = null;
        if (_mistura is not null && t < 1) mistura = _mistura with { T = t };
        else _mistura = null;
        return (Seguir.OlharDoEstado(_seguir), mistura, movendo || mistura is not null);
    }

    public void Reiniciar()
    {
        _seguir = Seguir.Repouso;
        _ultimo = null;
        _humor = null;
        _mistura = null;
    }
}
