namespace Nexora.Testes;

public sealed class SistemaTestes
{
    [Theory]
    [InlineData("Ctrl K", "⌘K")]
    [InlineData("Nova sessão no projeto da aba atual (Ctrl+N)", "Nova sessão no projeto da aba atual (⌘N)")]
    [InlineData("Orquestrador (Ctrl+Shift+O)", "Orquestrador (⇧⌘O)")]
    [InlineData("Painel (Ctrl+Alt+B)", "Painel (⌥⌘B)")]
    [InlineData("Quadro de sessões (Ctrl+0)", "Quadro de sessões (⌘0)")]
    [InlineData("Próxima aba (Ctrl+Tab)", "Próxima aba (⌃Tab)")]
    [InlineData("Mensagem  ·  Ctrl+V cola print", "Mensagem  ·  ⌘V cola print")]
    [InlineData("sem atalho aqui", "sem atalho aqui")]
    public void TraduzOsAtalhosParaOMac(string windows, string mac)
    {
        Assert.Equal(mac, Sistema.Atalho(windows, mac: true));
        Assert.Equal(windows, Sistema.Atalho(windows, mac: false));
    }

    [Fact]
    public void AspasDoShell()
    {
        Assert.Equal("'/Users/x/.local/bin/claude'", Sistema.Aspas("/Users/x/.local/bin/claude"));
        Assert.Equal("'it'\\''s'", Sistema.Aspas("it's"));
    }
}
