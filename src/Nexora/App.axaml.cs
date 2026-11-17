using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Nexora.Temas;
using Nexora.ViewModels;

using Nexora.Views;

namespace Nexora;

public partial class App : Application
{
    public static Config Config { get; } = Config.Carregar();
    public static Core.Mcp.HubMcp? Hub { get; private set; }
    public static Core.Nuvem.ServicoDaNuvem Nuvem { get; set; } = new(null, new Core.Segredos.ArmazemNaMemoria());
    public static Core.Segredos.ServicoDeSegredos Segredos { get; set; } = new(new Core.Segredos.CofreDeSegredos(null, new Core.Segredos.ArmazemNaMemoria()));

    public override void Initialize()
    {
        Core.Texto.Idioma.Registrar(Traducao.Ingles.Pares);
        Core.Texto.Idioma.Definir(Argumento(Environment.GetCommandLineArgs(), "--idioma") ?? Config.Idioma);
        if (Core.Texto.Idioma.EmIngles)
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.DefaultThreadCurrentCulture = Core.Texto.Idioma.Cultura;
        AvaloniaXamlLoader.Load(this);
        Nexora.Controles.NomesAcessiveis.Ligar();
    }

    static void LigarHub(string[] args)
    {
        if (!Config.HubMcp || args.Contains("--sem-hub") || args.Contains("--demo")) return;
        Hub = new Core.Mcp.HubMcp(Path.Combine(global::Nexora.Core.DadosDoNexora.Pasta, "mcp"))
        {
            Ocioso = TimeSpan.FromMinutes(Math.Max(1, Config.McpOciosoMinutos)),
        };
    }

    static void LigarSegredos(string[] args)
    {
        if (args.Contains("--demo") || args.Contains("--isolada")) return;
        var pasta = global::Nexora.Core.DadosDoNexora.Pasta;
        Segredos = new Core.Segredos.ServicoDeSegredos(new Core.Segredos.CofreDeSegredos(pasta, Core.Segredos.CofreDeSegredos.ArmazemDoSistema(pasta)));
        Nuvem = new Core.Nuvem.ServicoDaNuvem(Path.Combine(pasta, "nuvem"), Core.Segredos.CofreDeSegredos.ArmazemDoSistema(pasta));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args ?? [];
            Erros.Ligar();
            Pulso.Acompanhar();
            Core.Claude.CatalogoModelos.Arquivo = Path.Combine(global::Nexora.Core.DadosDoNexora.Pasta, "modelos.json");
            LigarHub(args);
            LigarSegredos(args);
            NavegadorDoApp.Ligar();
            if (Argumento(args, "--fundo") is { } modoFundo) Config.ModoFundo = modoFundo;
            if (Config.ModoFundo == "parado") Config.ModoFundo = "vidro";
            Catalogo.Modo = Config.ModoFundo;
            if (double.TryParse(Argumento(args, "--zoom"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var zoom)) Config.Zoom = zoom;
            if (Argumento(args, "--animacoes") is { } animacoes) Config.Animacoes = animacoes;
            if (Argumento(args, "--fonte") is { } fonte) Config.Fonte = fonte;
            Aparencia.Ligar();
            Catalogo.FonteDaInterface = Config.Fonte;
            Catalogo.Escolher(Argumento(args, "--tema") ?? Config.Tema);
            Catalogo.AcompanharSistema();
            JanelaVM.Demo = args.Contains("--demo");
            if (args.Contains("--quadro") && args.Contains("--isolada")) Config.Quadro = true;
            if (args.Contains("--por-projeto") && args.Contains("--isolada")) Config.QuadroPorProjeto = true;
            var vm = new JanelaVM();
            var janela = new Janela { DataContext = vm };
            NavegadorDoApp.LigarServidor(vm, args);
            JanelaVM.Isolada = args.Contains("--isolada") || Argumento(args, "--pasta") is not null || Argumento(args, "--chefe") is not null;
            vm.IniciarAgenda();
            vm.LigarRegistro();
            ControleDoApp.Ligar(vm, args);
            Core.Nuvem.TransporteSsh.ProgramaDeSenha = OperatingSystem.IsWindows() && File.Exists(Path.Combine(AppContext.BaseDirectory, "Nexora.exe")) ? Path.Combine(AppContext.BaseDirectory, "Nexora.exe") : Environment.ProcessPath;
            if (JanelaVM.Isolada) Core.Claude.SessaoClaude.Simulado = Argumento(args, "--claude-simulado");
            MonitorDeDesempenho.PastaDoTeste = Argumento(args, "--desempenho");
            MonitorDeDesempenho.Iniciar(vm);
            vm.AcompanharMemoria();
            vm.AcompanharCodex();
            Janelas.Iniciar(vm, janela);
            if (args.Contains("--fps")) janela.RendererDiagnostics.DebugOverlays = Avalonia.Rendering.RendererDebugOverlays.Fps | Avalonia.Rendering.RendererDebugOverlays.RenderTimeGraph | Avalonia.Rendering.RendererDebugOverlays.LayoutTimeGraph;
            Janelas.RestaurarPrincipal();
            if (Argumento(args, "--janela") is { } medida && medida.Split('x') is [var largura, var altura]
                && double.TryParse(largura, out var l) && double.TryParse(altura, out var a))
            {
                janela.WindowState = Avalonia.Controls.WindowState.Normal;
                (janela.Width, janela.Height) = (l, a);
            }
            if (args.Contains("--issues-lista")) vm.PaginaIssues.EmLista = true;
            if (Argumento(args, "--raiz") is { } raiz) Config.Raizes = [raiz];
            if (Argumento(args, "--tarefa") is { } tarefa) _ = CriarTarefaAoCarregarAsync(vm, tarefa);
            if (args.Contains("--demo")) vm.PreencherDemo();
            if (args.Contains("--demo") && args.Contains("--segredo")) vm.SegredoDemo();
            if (args.Contains("--demo") && args.Contains("--nuvem")) vm.NuvemDemo();
            if (args.Contains("--demo") && int.TryParse(Argumento(args, "--conversa-longa"), out var turnosLongos)) vm.AdicionarConversaLonga(turnosLongos);
            if (args.Contains("--sem-atualizacao")) vm.Novidade = null;
            else
            {
                _ = vm.CarregarAsync();
                vm.ReabrirDaNuvem();
                vm.ProcurarAtualizacoes();
                if (!JanelaVM.Isolada) _ = Task.Run(IdentidadeJanela.GarantirAtalhos);
            }
            if (args.Contains("--visao")) vm.MostrarVisaoGeralCommand.Execute(null);
            if (args.Contains("--expandir") && vm.Abertas.FirstOrDefault(c => c.Itens.Count > 0) is { } aberto) aberto.Expandido = true;
            if (args.Contains("--expandir-fora")) _ = ExpandirForaAsync(vm);
            if (args.Contains("--orquestra")) vm.MostrarOrquestraCommand.Execute(null);
            if (args.Contains("--sem-lateral")) vm.LateralVisivel = false;
            if (args.Contains("--lateral")) vm.MostrarLateral();
            if (Argumento(args, "--icones") is { } pastaIcones)
            {
                Directory.CreateDirectory(pastaIcones);
                foreach (var t in Catalogo.Embutidos)
                {
                    var (topo, baseCor) = IconeDoTema.Cores(t);
                    using var b = IconeDoTema.Desenhar(topo, baseCor, 128);
                    b.Save(Path.Combine(pastaIcones, $"{t.Id.Replace(':', '-')}.png"));
                }
            }
            var primeiroUso = !Config.BoasVindasFeitas && !JanelaVM.Isolada && !args.Contains("--demo");
            if (primeiroUso && !Config.AgenteEscolhido) vm.AbrirPrimeiroUso();
            else if (args.Contains("--boas-vindas") || primeiroUso) vm.AbrirBoasVindasCommand.Execute(null);
            if (args.Contains("--agentes")) vm.AbrirEscolhaDeAgenteCommand.Execute(null);
            if (int.TryParse(Argumento(args, "--passo"), out var passo)) vm.BoasVindas.Passo = passo;
            if (args.Contains("--quadro")) vm.MostrarVisaoGeralCommand.Execute(null);
            else if (vm.Principal.Selecionada is null && !args.Contains("--demo")) vm.MostrarDashboardCommand.Execute(null);
            if (Argumento(args, "--pagina") == "projetos") vm.MostrarProjetosCommand.Execute(null);
            if (Argumento(args, "--pagina") == "dashboard") vm.MostrarDashboardCommand.Execute(null);
            if (Argumento(args, "--pagina") == "recursos") vm.MostrarRecursosCommand.Execute(null);
            if (Argumento(args, "--pagina") == "uso") vm.MostrarUsoCommand.Execute(null);
            if (Argumento(args, "--pagina") == "issues") vm.MostrarIssuesCommand.Execute(null);
            if (Argumento(args, "--pagina") == "agendados") vm.MostrarAgendadosCommand.Execute(null);
            if (Argumento(args, "--pagina") == "configuracoes") vm.MostrarConfiguracoesCommand.Execute(null);
            if (args.Contains("--conversas")) vm.AlternarLateralCommand.Execute(null);
            if (Argumento(args, "--secao") is { } secao) vm.PaginaConfiguracoes.IrPara(secao);
            if (args.Contains("--menu-modelo"))
                Avalonia.Threading.Dispatcher.UIThread.Post(() => (vm.Atual as ChatVM)?.AbrirMenuModeloCommand.Execute(null), Avalonia.Threading.DispatcherPriority.Background);
            if (args.Contains("--primeira-issue")) _ = vm.PaginaIssues.EscolherPrimeiraAsync();
            if (Argumento(args, "--aba") is { } aba) Avalonia.Threading.Dispatcher.UIThread.Post(() => vm.AbrirPainelDaSessaoCommand.Execute(aba), Avalonia.Threading.DispatcherPriority.Background);
            if (Argumento(args, "--executar") is { } executar) _ = ExecutarAoCarregarAsync(vm, executar, int.TryParse(Argumento(args, "--rodar"), out var rodar) ? rodar : null);
            if (Argumento(args, "--chefe") is { } chefe) Orquestrar(vm, chefe, args);
            if (Argumento(args, "--navegador") is { } enderecoDeTeste) _ = NavegadorDoApp.AbrirAsync(vm, enderecoDeTeste, args);
            if (args.Contains("--temas")) vm.MostrarTemasCommand.Execute(null);
            if (args.Contains("--editar-tema")) vm.EditarTemaAtual();
            if (args.Contains("--novo-ambiente")) vm.NovoAmbienteCommand.Execute(null);
            if (int.TryParse(Argumento(args, "--abrir"), out var indice)) _ = vm.AbrirAoCarregarAsync(indice);
            if (Argumento(args, "--ambiente") is { } ambiente) _ = vm.AbrirAmbienteAoCarregarAsync(ambiente);
            if (Argumento(args, "--excluir") is { } excluir) _ = ExcluirAoCarregarAsync(vm, excluir);
            if (args.Contains("--nova")) _ = NovaAoCarregarAsync(vm);
            if (Argumento(args, "--pasta") is { } pastas)
                foreach (var pasta in pastas.Split(';', StringSplitOptions.RemoveEmptyEntries)) Testar(vm, pasta, args);
            if (args.Contains("--destacar")) janela.Opened += (_, _) => { if (vm.Principal.Selecionada is { } guia) vm.Destacar(guia, null); };
            if (int.TryParse(Argumento(args, "--espiar"), out var espiar))
                janela.Opened += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (vm.VisaoGeral.Todas.ElementAtOrDefault(espiar) is { } cartao) vm.VisaoGeral.EspiarCommand.Execute(cartao);
                }, Avalonia.Threading.DispatcherPriority.Background);
            if (Argumento(args, "--medir-quadros") is { } medicao) MedidorDeQuadros.Agendar(janela, vm, medicao);
            if (Argumento(args, "--foto") is { } foto)
                Foto.Agendar(janela, foto, desktop, int.TryParse(Argumento(args, "--espera"), out var espera) ? espera : 1500);
            desktop.MainWindow = janela;
        }
        base.OnFrameworkInitializationCompleted();
    }

    static async Task CriarTarefaAoCarregarAsync(JanelaVM vm, string nome)
    {
        while (vm.AmbienteAtual is not { Carregando: false, Worktrees.Count: > 0 }) await Task.Delay(100);
        var ambiente = vm.AmbienteAtual;
        ambiente.NomeTarefa = nome;
        await ambiente.CriarTarefaCommand.ExecuteAsync(null);
    }

    static async Task ExcluirAoCarregarAsync(JanelaVM vm, string nome)
    {
        var espera = DateTime.UtcNow.AddSeconds(20);
        while ((vm.CarregandoAmbientes || vm.Ambientes.Count == 0) && DateTime.UtcNow < espera) await Task.Delay(100);
        if (vm.Ambientes.FirstOrDefault(a => a.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase)) is { } ambiente) vm.PedirExclusao(ambiente);
    }

    static async Task ExecutarAoCarregarAsync(JanelaVM vm, string pasta, int? rodar)
    {
        while (vm.CarregandoAmbientes) await Task.Delay(50);
        var painel = vm.ExecutarDe(vm.LocalDe(pasta));
        vm.MostrarExecutar(painel.Local);
        while (painel.Carregando) await Task.Delay(50);
        if (rodar is { } indice && indice < painel.Alvos.Count)
        {
            painel.Selecionado = painel.Alvos[indice];
            painel.Alvos[indice].ExecutarCommand.Execute(null);
        }
    }

    static async Task ExpandirForaAsync(JanelaVM vm)
    {
        SessaoForaVM? fora;
        while ((fora = vm.VisaoGeral.Todas.OfType<SessaoForaVM>().FirstOrDefault(f => f.UltimaAtividade is not null)) is null) await Task.Delay(100);
        fora.Expandido = true;
    }

    static async Task AbrirPrimeiraAlteracaoAsync(ChatVM chat)
    {
        chat.Painel.MostraAlteracoes = true;
        await chat.Painel.AtualizarAlteracoes();
        chat.Painel.Alteracoes.FirstOrDefault(a => !a.Nova)?.AbrirCommand.Execute(null);
    }

    static async Task NovaAoCarregarAsync(JanelaVM vm)
    {
        while (vm.CarregandoAmbientes || vm.Ambientes.Count == 0) await Task.Delay(50);
        vm.NovaSessaoCommand.Execute(null);
    }

    static void Testar(JanelaVM vm, string pasta, string[] args)
    {
        vm.AbrirSessao(vm.LocalDe(pasta));
        if (vm.Atual is not { } chat) return;
        if (Argumento(args, "--arquivo") is { } arquivo) chat.Editor.Abrir(arquivo, 40);
        if (args.Contains("--painel")) chat.Painel.Visivel = true;
        if (args.Contains("--terminal")) chat.TerminalAberto = true;
        if (args.Contains("--novo-arquivo")) Avalonia.Threading.Dispatcher.UIThread.Post(() => chat.Painel.Raizes.FirstOrDefault()?.NovoArquivoCommand.Execute(null), Avalonia.Threading.DispatcherPriority.Background);
        if (args.Contains("--alteracoes")) _ = AbrirPrimeiraAlteracaoAsync(chat);
        if (Argumento(args, "--anexar") is { } anexo) chat.AnexarArquivo(anexo);
        if (Argumento(args, "--buscar") is { } termo)
        {
            chat.Painel.AbrirBuscaCommand.Execute(null);
            chat.Painel.TermoBusca = termo;
        }
        if (Argumento(args, "--rascunho") is { } rascunho) Avalonia.Threading.Dispatcher.UIThread.Post(() => chat.InserirNoRascunho(rascunho), Avalonia.Threading.DispatcherPriority.Background);
        if (Argumento(args, "--mencao") is { } mencao) Avalonia.Threading.Dispatcher.UIThread.Post(() => chat.InserirNoRascunho("@" + mencao), Avalonia.Threading.DispatcherPriority.Background);
        if (int.TryParse(Argumento(args, "--modo"), out var modo)) chat.ModoIndice = modo;
        if (int.TryParse(Argumento(args, "--modelo"), out var modelo)) chat.ModeloValor = ChatVM.ModeloDoIndice(modelo);
        if (Argumento(args, "--enviar") is not { } texto) return;
        chat.Rascunho = texto;
        chat.EnviarCommand.Execute(null);
    }

    static void Orquestrar(JanelaVM vm, string pasta, string[] args)
    {
        vm.MostrarOrquestraCommand.Execute(null);
        if (vm.Orquestra.IniciarEm(vm.LocalDe(pasta)) is not { } chefe) return;
        if (int.TryParse(Argumento(args, "--modelo"), out var modelo)) chefe.ModeloValor = ChatVM.ModeloDoIndice(modelo);
        if (int.TryParse(Argumento(args, "--modo"), out var modo)) chefe.ModoIndice = modo;
        if (Argumento(args, "--orquestrar") is { } texto) chefe.EnviarTexto(texto);
        if (args.Contains("--retomar-depois")) _ = RetomarDepoisAsync(vm, chefe);
    }

    static async Task RetomarDepoisAsync(JanelaVM vm, ChatVM chefe)
    {
        await Task.Delay(3000);
        while (chefe.SessaoId is null || chefe.Trabalhando || chefe.Precisa) await Task.Delay(500);
        var id = chefe.SessaoId;
        await Task.Delay(2000);
        chefe.FecharCommand.Execute(null);
        await Task.Delay(1500);
        vm.Orquestra.Retomar(id);
    }

    internal static string? Argumento(string[] args, string nome)
    {
        var i = Array.IndexOf(args, nome);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
