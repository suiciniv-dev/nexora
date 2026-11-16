namespace Nexora.Testes;

public sealed class SemEfeitoForaDoAppTestes
{
    [Fact]
    public void TestesNaoRodamComOAppDeVerdade() => Assert.False(Sistema.AppDeVerdade);

    [Fact]
    public void DitadoSoAnotaQuandoAlguemObserva()
    {
        var anotados = new List<string>();
        var antes = Sistema.SoAnotar;
        Sistema.SoAnotar = anotados.Add;
        try
        {
            Ditado.Abrir();
        }
        finally
        {
            Sistema.SoAnotar = antes;
        }
        if (Ditado.Disponivel) Assert.Contains(anotados, a => a.Contains("ditado"));
    }

    [Fact]
    public void ProgramaExternoNaoAbreForaDoAppDeVerdade()
    {
        var antes = Sistema.SoAnotar;
        Sistema.SoAnotar = null;
        try
        {
            Assert.Null(Sistema.Abrir(new System.Diagnostics.ProcessStartInfo("notepad.exe")));
        }
        finally
        {
            Sistema.SoAnotar = antes;
        }
    }
}
