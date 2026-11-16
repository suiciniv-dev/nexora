namespace Nexora.Core.Texto;

public static class Tempo
{
    public static string Relativo(DateTime quando, DateTime? agora = null)
    {
        var referencia = agora ?? DateTime.Now;
        var passou = referencia - quando;
        if (passou < TimeSpan.FromMinutes(1)) return Idioma.T("agora");
        if (passou < TimeSpan.FromHours(1)) return Idioma.F("há {0} min", (int)passou.TotalMinutes);
        if (quando.Date == referencia.Date) return Idioma.F("há {0} h", (int)passou.TotalHours);
        if (quando.Date == referencia.Date.AddDays(-1)) return Idioma.T("ontem");
        if (passou < TimeSpan.FromDays(7)) return Idioma.F("há {0} dias", (int)Math.Ceiling(passou.TotalDays));
        return quando.ToString(quando.Year == referencia.Year ? Idioma.T("dd/MM") : Idioma.T("dd/MM/yy"), Idioma.Cultura);
    }

    public static string Duracao(long milissegundos)
    {
        var t = TimeSpan.FromMilliseconds(milissegundos);
        if (t.TotalSeconds < 60) return $"{Math.Max(1, (int)Math.Round(t.TotalSeconds))} s";
        if (t.TotalMinutes < 60) return $"{(int)t.TotalMinutes} min {t.Seconds:00} s";
        return $"{(int)t.TotalHours} h {t.Minutes:00} min";
    }
}
