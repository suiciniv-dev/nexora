using Nexora.Core.Texto;

namespace Nexora.Core.Agenda;

public sealed record Agendamento(
    string Id,
    string Nome,
    string Pasta,
    string Texto,
    TimeOnly Hora,
    IReadOnlyList<DayOfWeek> Dias,
    bool Ativo = true,
    DateTime? UltimaExecucao = null);

public static class Agenda
{
    public static readonly DayOfWeek[] Semana =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    public static bool Vencido(Agendamento agendamento, DateTime agora)
    {
        if (!agendamento.Ativo || !agendamento.Dias.Contains(agora.DayOfWeek)) return false;
        var horario = agora.Date + agendamento.Hora.ToTimeSpan();
        return agora >= horario && (agendamento.UltimaExecucao is not { } ultima || ultima < horario);
    }

    public static DateTime? Proxima(Agendamento agendamento, DateTime agora)
    {
        if (!agendamento.Ativo || agendamento.Dias.Count == 0) return null;
        for (var dia = 0; dia <= 7; dia++)
        {
            var data = agora.Date.AddDays(dia);
            if (!agendamento.Dias.Contains(data.DayOfWeek)) continue;
            var horario = data + agendamento.Hora.ToTimeSpan();
            if (horario > agora || Vencido(agendamento, agora) && dia == 0) return horario;
        }
        return null;
    }

    public static string Descrever(Agendamento agendamento)
    {
        var dias = agendamento.Dias.Count switch
        {
            7 => Idioma.T("todo dia"),
            5 when !agendamento.Dias.Contains(DayOfWeek.Saturday) && !agendamento.Dias.Contains(DayOfWeek.Sunday) => Idioma.T("de segunda a sexta"),
            0 => Idioma.T("nenhum dia"),
            _ => string.Join(", ", Semana.Where(agendamento.Dias.Contains).Select(Curto)),
        };
        return Idioma.F("{0} às {1:HH\\:mm}", dias, agendamento.Hora);
    }

    public static string Curto(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => Idioma.T("seg"),
        DayOfWeek.Tuesday => Idioma.T("ter"),
        DayOfWeek.Wednesday => Idioma.T("qua"),
        DayOfWeek.Thursday => Idioma.T("qui"),
        DayOfWeek.Friday => Idioma.T("sex"),
        DayOfWeek.Saturday => Idioma.T("sáb"),
        _ => Idioma.T("dom"),
    };
}
