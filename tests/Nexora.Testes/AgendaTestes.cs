using Nexora.Core.Agenda;

namespace Nexora.Testes;

public sealed class AgendaTestes
{
    static readonly DateTime Segunda9h = new(2026, 11, 2, 9, 0, 0);

    static Agendamento Diario(DateTime? ultima = null, bool ativo = true) =>
        new("a", "PRs", @"D:\Proj", "revisar", new TimeOnly(9, 0), [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday], ativo, ultima);

    [Fact]
    public void VenceNaHoraERodaUmaVezPorDia()
    {
        Assert.False(Agenda.Vencido(Diario(), Segunda9h.AddMinutes(-1)));
        Assert.True(Agenda.Vencido(Diario(), Segunda9h));
        Assert.True(Agenda.Vencido(Diario(), Segunda9h.AddHours(6)));
        Assert.False(Agenda.Vencido(Diario(Segunda9h.AddSeconds(20)), Segunda9h.AddHours(1)));
        Assert.True(Agenda.Vencido(Diario(Segunda9h.AddSeconds(20)), Segunda9h.AddDays(1)));
        Assert.False(Agenda.Vencido(Diario(), Segunda9h.AddDays(5)));
        Assert.False(Agenda.Vencido(Diario(ativo: false), Segunda9h));
    }

    [Fact]
    public void ProximaPulaOFimDeSemana()
    {
        var sexta = Segunda9h.AddDays(4);
        Assert.Equal(Segunda9h.AddDays(7), Agenda.Proxima(Diario(sexta.AddMinutes(1)), sexta.AddHours(2)));
        Assert.Equal(Segunda9h, Agenda.Proxima(Diario(), Segunda9h.AddHours(-3)));
        Assert.Null(Agenda.Proxima(Diario(ativo: false), Segunda9h));
    }

    [Fact]
    public void DescreveOsDias()
    {
        Assert.Equal("de segunda a sexta às 09:00", Agenda.Descrever(Diario()));
        Assert.Equal("todo dia às 09:00", Agenda.Descrever(Diario() with { Dias = Agenda.Semana }));
        Assert.Equal("seg, qua às 18:30", Agenda.Descrever(Diario() with { Dias = [DayOfWeek.Wednesday, DayOfWeek.Monday], Hora = new TimeOnly(18, 30) }));
    }
}
