using System.Globalization;
using System.Text.Json;
using Nexora.Core.Arquivos;

namespace Nexora.Core.Painel;

public sealed class DiaDeUso
{
    public double Ativo { get; set; }
    public double Agentes { get; set; }
    public int Turnos { get; set; }
}

public sealed record PontoDoPlano(DateTimeOffset Hora, double? Sessao, double? Semana, double? CodexSessao, double? CodexSemana);

public sealed class RegistroDoPainel
{
    public const int DiasGuardados = 180;
    public static readonly TimeSpan HistoricoDoPlano = TimeSpan.FromDays(8);
    static readonly TimeSpan IntervaloDoPlano = TimeSpan.FromMinutes(5);
    static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public Dictionary<string, DiaDeUso> Dias { get; set; } = [];
    public List<PontoDoPlano> Plano { get; set; } = [];

    public static string Chave(DateOnly dia) => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Caminho(string pasta) => Path.Combine(pasta, "painel.json");

    public static RegistroDoPainel Ler(string arquivo) =>
        ArquivoSeguro.LerJson(arquivo, () => new RegistroDoPainel(), Json).Valor;

    public void Gravar(string arquivo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
        ArquivoSeguro.Gravar(arquivo, JsonSerializer.Serialize(this, Json));
    }

    DiaDeUso Dia(DateTime quando)
    {
        var chave = Chave(DateOnly.FromDateTime(quando));
        if (!Dias.TryGetValue(chave, out var dia)) Dias[chave] = dia = new DiaDeUso();
        return dia;
    }

    public void SomarAtivo(DateTime quando, TimeSpan tempo)
    {
        if (tempo > TimeSpan.Zero) Dia(quando).Ativo += tempo.TotalSeconds;
    }

    public void SomarTurno(DateTime quando, TimeSpan duracao)
    {
        var dia = Dia(quando);
        dia.Turnos++;
        if (duracao > TimeSpan.Zero) dia.Agentes += duracao.TotalSeconds;
    }

    public bool AnotarPlano(PontoDoPlano ponto)
    {
        if (ponto is { Sessao: null, Semana: null, CodexSessao: null, CodexSemana: null }) return false;
        if (Plano.Count > 0 && Plano[^1] is var ultimo && ponto.Hora - ultimo.Hora < IntervaloDoPlano
            && ultimo with { Hora = ponto.Hora } == ponto) return false;
        if (Plano.Count > 0 && ponto.Hora - Plano[^1].Hora < IntervaloDoPlano) Plano[^1] = ponto;
        else Plano.Add(ponto);
        return true;
    }

    public void Podar(DateTime agora)
    {
        var limite = Chave(DateOnly.FromDateTime(agora).AddDays(-DiasGuardados));
        foreach (var velho in Dias.Keys.Where(k => string.CompareOrdinal(k, limite) < 0).ToList()) Dias.Remove(velho);
        var desde = new DateTimeOffset(agora) - HistoricoDoPlano;
        Plano.RemoveAll(p => p.Hora < desde);
    }

    public (TimeSpan Ativo, TimeSpan Agentes, int Turnos) Semana(DateOnly hoje)
    {
        var segunda = hoje.AddDays(-(((int)hoje.DayOfWeek + 6) % 7));
        double ativo = 0, agentes = 0;
        var turnos = 0;
        for (var d = segunda; d <= hoje; d = d.AddDays(1))
            if (Dias.TryGetValue(Chave(d), out var dia))
            {
                ativo += dia.Ativo;
                agentes += dia.Agentes;
                turnos += dia.Turnos;
            }
        return (TimeSpan.FromSeconds(ativo), TimeSpan.FromSeconds(agentes), turnos);
    }
}
