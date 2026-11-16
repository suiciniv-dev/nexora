using Avalonia.Threading;
using Nexora.Testes.Tela;

namespace Nexora.Testes;

[Collection("Erros")]
public sealed class ErrosTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-erros-").FullName;
    readonly string _arquivoAntes = Erros.Arquivo;
    readonly Action<string, string>? _avisarAntes = Erros.Avisar;
    readonly List<string> _avisos = [];

    public ErrosTestes()
    {
        Erros.Arquivo = Path.Combine(_pasta, "erros.log");
        Erros.Avisar = (titulo, _) => _avisos.Add(titulo);
    }

    public void Dispose()
    {
        Erros.Arquivo = _arquivoAntes;
        Erros.Avisar = _avisarAntes;
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    [Fact]
    public void ErroFicaAnotadoComOndeEAPilha()
    {
        Erros.Registrar(new InvalidOperationException("caiu no botão"), "interface");
        var texto = File.ReadAllText(Erros.Arquivo);
        Assert.Contains("interface", texto);
        Assert.Contains("InvalidOperationException", texto);
        Assert.Contains("caiu no botão", texto);
    }

    [Fact]
    public void AvisaUmaVezMesmoComVariosErrosSeguidos()
    {
        for (var i = 0; i < 5; i++) Erros.Tratar(new NullReferenceException(), "interface");
        Assert.True(_avisos.Count <= 1);
        Assert.Equal(5, File.ReadAllText(Erros.Arquivo).Split("NullReferenceException").Length - 1);
    }

    [Fact]
    public void ArquivoNaoCresceSemLimite()
    {
        var grande = new string('x', 4000);
        for (var i = 0; i < 400; i++) Erros.Registrar(new Exception(grande), "interface");
        Assert.True(new FileInfo(Erros.Arquivo).Length < 1024 * 1024);
    }

    [Fact]
    public void AvisoQueFalhaNaoDerrubaOTratador()
    {
        Erros.Avisar = (_, _) => throw new InvalidOperationException("sem notificação");
        var erro = Record.Exception(() => Erros.Tratar(new Exception("x"), "interface"));
        Assert.Null(erro);
    }

    [Fact]
    public Task ErroNumCliqueNaoSobeQuandoOTratadorEstaLigado() => Sessao.Rodar(() =>
    {
        var tratados = new List<Exception>();
        void Tratador(object? s, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            tratados.Add(e.Exception);
            Erros.Tratar(e.Exception, "interface");
        }
        Dispatcher.UIThread.UnhandledException += Tratador;
        try
        {
            Dispatcher.UIThread.Post(() => throw new NullReferenceException("clique quebrado"));
            var erro = Record.Exception(() => Dispatcher.UIThread.RunJobs());
            Assert.Null(erro);
            Assert.Single(tratados);
            Assert.Contains("clique quebrado", File.ReadAllText(Erros.Arquivo));
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= Tratador;
        }
    });
}
