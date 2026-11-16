using Avalonia;
using Avalonia.Headless;
using Nexora.Core.Ambientes;
using Nexora.ViewModels;

namespace Nexora.Testes.Tela;

public static class AppDeTeste
{
    public static AppBuilder BuildAvaloniaApp()
    {
        JanelaVM.Isolada = true;
        return AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).WithInterFont()
            .AfterSetup(_ =>
            {
                Core.Texto.Idioma.Definir("pt");
                Application.Current!.Resources["FonteUi"] = new Avalonia.Media.FontFamily("Segoe UI");
            });
    }
}

static class Sessao
{
    static readonly Lazy<HeadlessUnitTestSession> Unica = new(() => HeadlessUnitTestSession.StartNew(typeof(AppDeTeste)));

    public static Task Rodar(Action acao) => Unica.Value.Dispatch(acao, CancellationToken.None);

    public static Task Rodar(Func<Task> acao) => Unica.Value.Dispatch(async () =>
    {
        await acao();
        return true;
    }, CancellationToken.None);
}

sealed class AnfitriaoFalso : IAnfitriao
{
    public List<(Local Local, string? Mensagem)> Abertas { get; } = [];

    public void AbrirSessao(Local local, string? primeiraMensagem = null) => Abertas.Add((local, primeiraMensagem));
    public bool Ocupada(string pasta) => false;
    public void BranchMudou(string pasta) { }
    public AmbienteVM Registrar(Ambiente ambiente) => new(ambiente, this);
    public void Esconder(AmbienteVM ambiente) { }
    public void PedirExclusao(AmbienteVM ambiente) { }
    public bool LiberadaEmTodas(Core.Claude.PermissaoPedida pedido) => false;
    public void LiberarEmTodas(string chave) { }
    public void Lembrar(Ambiente ambiente) { }
    public AmbienteVM? AmbienteDe(string pasta) => null;
    public List<Local> ListaDeLocais { get; } = [];
    public IReadOnlyList<Local> Locais() => ListaDeLocais;
    public Local LocalDe(string pasta) => new(Path.GetFileName(pasta), pasta, []);
    public void Mostrar(ChatVM chat) { }
    public IReadOnlyList<ConversaVM> ConversasDe(string pasta) => [];
    public string? EmOutroLugar(string sessaoId) => null;
    public void MostrarExecutar(Local local) { }
    public IReadOnlyList<string> UsosDe(string pasta) => [];
    public void Esquecer(string pasta) { }
}
