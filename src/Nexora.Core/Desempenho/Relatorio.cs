using System.Globalization;
using System.Text.Json.Nodes;

namespace Nexora.Core.Desempenho;

public sealed record InfoDoComputador(string Versao, string Sistema, int Nucleos, long Memoria);

public static class Relatorio
{
    public const string Endereco = "https://nexora.suiciniv.dev/api/desempenho";
    public const int Formato = 1;
    public const DayOfWeek DiaPadrao = DayOfWeek.Monday;
    public static readonly TimeOnly HoraPadrao = new(9, 0);

    const long Mega = 1L << 20;

    public static JsonObject Montar(IReadOnlyList<ResumoDoDia> dias, InfoDoComputador computador, DateTime geradoEm) => new()
    {
        ["formato"] = Formato,
        ["versao"] = Versao(computador.Versao),
        ["sistema"] = computador.Sistema is "windows" or "mac" or "linux" ? computador.Sistema : "outro",
        ["nucleos"] = computador.Nucleos,
        ["memoriaDoPcMb"] = computador.Memoria / Mega,
        ["geradoEm"] = geradoEm.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture),
        ["dias"] = new JsonArray([.. dias.Where(d => d.Amostras > 0).OrderBy(d => d.Data).Select(Dia)]),
    };

    static JsonNode Dia(ResumoDoDia d) => new JsonObject
    {
        ["data"] = d.Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["amostras"] = d.Amostras,
        ["ramMb"] = Megas(d.Ram),
        ["ramNexoraMb"] = Megas(d.RamNexora),
        ["cpu"] = Percentuais(d.Cpu),
        ["cpuNexora"] = Percentuais(d.CpuNexora),
        ["atrasoMs"] = new JsonObject { ["media"] = (int)Math.Round(d.Atraso.Media), ["maximo"] = (int)d.Atraso.Maximo, ["p95"] = (int)d.Atraso.P95 },
        ["travadas"] = d.Travadas,
        ["divisaoMb"] = new JsonObject
        {
            ["nexora"] = d.Divisao.Nexora / Mega,
            ["sessoes"] = d.Divisao.Sessoes / Mega,
            ["mcps"] = d.Divisao.Mcps / Mega,
            ["builds"] = d.Divisao.Builds / Mega,
            ["outros"] = d.Divisao.Outros / Mega,
        },
        ["heapMaximoMb"] = d.HeapMaximo / Mega,
        ["privadaMaximaMb"] = d.PrivadaMaxima / Mega,
        ["handlesMaximo"] = d.HandlesMaximo,
        ["abasMaximo"] = d.AbasMaximo,
        ["trabalhandoMaximo"] = d.TrabalhandoMaximo,
        ["custoMedioMs"] = Math.Round(d.CustoMedio, 2),
        ["momentos"] = new JsonArray([.. d.Momentos.OrderBy(m => m.Hora).Select(Momento)]),
        ["causas"] = new JsonArray([.. d.Causas.Where(c => Pilha.EhNomeDeCodigo(c.Causa)).Take(Agregacao.CausasPorDia).Select(Causa)]),
    };

    static JsonNode Causa(CausaDeTravada c) => new JsonObject
    {
        ["causa"] = c.Causa,
        ["ponta"] = Pilha.EhNomeDeCodigo(c.Ponta) ? c.Ponta : c.Causa,
        ["vezes"] = c.Vezes,
        ["maiorMs"] = c.Maior,
    };

    static JsonNode Momento(Momento m) => new JsonObject
    {
        ["hora"] = m.Hora.ToString("HH:mm", CultureInfo.InvariantCulture),
        ["tipo"] = m.Tipo is TipoDeMomento.Ram or TipoDeMomento.Cpu or TipoDeMomento.Travada ? m.Tipo : TipoDeMomento.Ram,
        ["valor"] = m.Tipo == TipoDeMomento.Ram ? Math.Round(m.Valor / Mega) : Math.Round(m.Valor, 1),
        ["ramMb"] = m.Ram / Mega,
        ["cpu"] = Math.Round(m.Cpu, 1),
        ["abas"] = m.Abas,
        ["trabalhando"] = m.Trabalhando,
        ["nexoraMb"] = m.NexoraRam / Mega,
        ["pausaGcMs"] = (int)Math.Round(m.PausaGc),
        ["gen2"] = m.Gen2,
        ["desdeAbrirS"] = m.DesdeAbrir,
        ["abaNovaHaS"] = m.AbaNovaHa,
    };

    static JsonObject Megas(Estatistica e) => new()
    {
        ["media"] = (long)Math.Round(e.Media / Mega),
        ["maximo"] = (long)Math.Round(e.Maximo / Mega),
        ["p95"] = (long)Math.Round(e.P95 / Mega),
    };

    static JsonObject Percentuais(Estatistica e) => new()
    {
        ["media"] = Math.Round(e.Media, 1),
        ["maximo"] = Math.Round(e.Maximo, 1),
        ["p95"] = Math.Round(e.P95, 1),
    };

    static string Versao(string versao)
    {
        var limpa = new string([.. versao.TakeWhile(c => char.IsAsciiDigit(c) || c == '.').Take(16)]);
        return limpa.Length > 0 ? limpa : "0";
    }

    public static bool PodeEnviar(bool relatorioPorEmail, bool demo, bool isolada) => relatorioPorEmail && !demo && !isolada;

    public static DateTime UltimoHorario(DateTime agora, DayOfWeek dia, TimeOnly hora)
    {
        var data = agora.Date.AddDays(-(((int)agora.DayOfWeek - (int)dia + 7) % 7));
        var horario = data + hora.ToTimeSpan();
        return horario > agora ? horario.AddDays(-7) : horario;
    }

    public static bool Vencido(DateTime agora, DayOfWeek dia, TimeOnly hora, DateTime? ultimoEnvio) =>
        ultimoEnvio is not { } ultimo || ultimo < UltimoHorario(agora, dia, hora);

    public static DayOfWeek DiaDe(string? texto) => (texto ?? "").Trim().ToLowerInvariant() switch
    {
        "domingo" or "dom" or "sunday" or "0" or "7" => DayOfWeek.Sunday,
        "segunda" or "segunda-feira" or "seg" or "monday" or "1" => DayOfWeek.Monday,
        "terça" or "terca" or "terça-feira" or "terca-feira" or "ter" or "tuesday" or "2" => DayOfWeek.Tuesday,
        "quarta" or "quarta-feira" or "qua" or "wednesday" or "3" => DayOfWeek.Wednesday,
        "quinta" or "quinta-feira" or "qui" or "thursday" or "4" => DayOfWeek.Thursday,
        "sexta" or "sexta-feira" or "sex" or "friday" or "5" => DayOfWeek.Friday,
        "sábado" or "sabado" or "sáb" or "sab" or "saturday" or "6" => DayOfWeek.Saturday,
        _ => DiaPadrao,
    };

    public static TimeOnly HoraDe(string? texto) =>
        TimeOnly.TryParseExact((texto ?? "").Trim(), ["H:mm", "HH:mm", "%H"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora) ? hora : HoraPadrao;
}
