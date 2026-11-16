using System.Globalization;

namespace Nexora.Core.Avatares;

public static class Js
{
    public static double Arredondar(double x)
    {
        if (!double.IsFinite(x)) return x;
        var baixo = Math.Floor(x);
        return x - baixo >= 0.5 ? baixo + 1 : baixo;
    }

    public static string Numero(double n)
    {
        if (n == 0) return "0";
        if (double.IsNaN(n)) return "NaN";
        if (double.IsInfinity(n)) return n > 0 ? "Infinity" : "-Infinity";
        var a = Math.Abs(n);
        if (a >= 1e21 || a < 1e-6)
        {
            var e = n.ToString("E16", CultureInfo.InvariantCulture);
            var curto = double.Parse(e, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture).ToLowerInvariant();
            var partes = curto.Split('e');
            var expoente = int.Parse(partes[1], CultureInfo.InvariantCulture);
            return partes[0] + "e" + (expoente >= 0 ? "+" : "-") + Math.Abs(expoente).ToString(CultureInfo.InvariantCulture);
        }
        return n.ToString("R", CultureInfo.InvariantCulture);
    }

    public static string Base36(uint valor)
    {
        if (valor == 0) return "0";
        const string digitos = "0123456789abcdefghijklmnopqrstuvwxyz";
        var buffer = new char[8];
        var i = buffer.Length;
        while (valor > 0)
        {
            buffer[--i] = digitos[(int)(valor % 36)];
            valor /= 36;
        }
        return new string(buffer, i, buffer.Length - i);
    }

    public static double Hypot(double a, double b) => Hypot([a, b]);

    public static double Hypot(double a, double b, double c) => Hypot([a, b, c]);

    static double Hypot(ReadOnlySpan<double> valores)
    {
        double maior = 0;
        var nan = false;
        foreach (var v in valores)
        {
            var abs = Math.Abs(v);
            if (double.IsPositiveInfinity(abs)) return double.PositiveInfinity;
            if (double.IsNaN(abs)) nan = true;
            else if (abs > maior) maior = abs;
        }
        if (nan) return double.NaN;
        if (maior == 0) return 0;
        double soma = 0, compensacao = 0;
        foreach (var v in valores)
        {
            var n = Math.Abs(v) / maior;
            var parcela = n * n - compensacao;
            var preliminar = soma + parcela;
            compensacao = preliminar - soma - parcela;
            soma = preliminar;
        }
        return Math.Sqrt(soma) * maior;
    }
}
