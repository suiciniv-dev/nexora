using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.Core.Provedores;
using Nexora.Core.Texto;
using Nexora.Temas;

namespace Nexora.ViewModels;

public sealed partial class RequisitoVM(string nome, string uso, string comoInstalar, Receita receita) : ObservableObject
{
    public string Nome { get; } = nome;
    public string Uso { get; } = uso;
    public string ComoInstalar { get; } = comoInstalar;
    public Receita Receita { get; } = receita;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Falta))]
    public partial bool Ok { get; set; }

    [ObservableProperty]
    public partial string Detalhe { get; set; } = "";

    public bool Falta => !Ok;
}

public sealed partial class RaizVM(string pasta, Action<RaizVM> remover)
{
    public string Pasta { get; } = pasta;
    public bool Existe => Directory.Exists(Pasta);

    [RelayCommand]
    void Remover() => remover(this);
}

public sealed partial class BoasVindasVM : ObservableObject
{
    public const int Passos = 6;
    readonly JanelaVM _janela;

    public BoasVindasVM(JanelaVM janela)
    {
        _janela = janela;
        Requisitos =
        [
            RequisitoDoAgente(Agente),
            new("Git", Idioma.T("Para ver as branches e o que mudou em cada projeto."), Instalacao.Git.Comando, Instalacao.Git),
            new("Node.js", Idioma.T("Para as ferramentas extras que alguns projetos dão aos agentes (servidores MCP)."), Instalacao.Node.Comando, Instalacao.Node),
            new(".NET 10", Idioma.T("O próprio Nexora roda nele."), Instalacao.DotNet.Comando, Instalacao.DotNet),
        ];
    }

    public DescricaoAgente Agente => CatalogoAgentes.De(App.Config.AgentePadrao);

    static RequisitoVM RequisitoDoAgente(DescricaoAgente agente) => agente.EhClaude
        ? new("Claude Code", Idioma.T("As conversas do Nexora usam o seu próprio Claude Code."),Instalacao.Claude.Comando, Instalacao.Claude)
        : new(agente.Nome, Idioma.F("As conversas usam o seu {0}, com a sua conta.", agente.Nome), agente.Instalar, new Receita(agente.Id, agente.Instalar, []));

    string? _programaDoAgente;

    void AtualizarAgente()
    {
        var agente = Agente;
        if (Requisitos[0].Receita.Id != agente.Id) Requisitos[0] = RequisitoDoAgente(agente);
        _programaDoAgente = agente.EhClaude ? LocalizadorClaude.Procurar() : CatalogoAgentes.Procurar(agente);
        OnPropertyChanged(nameof(TextoLogin));
        OnPropertyChanged(nameof(FaltaLogin));
        OnPropertyChanged(nameof(MostraEntrar));
        OnPropertyChanged(nameof(TituloLogin));
        OnPropertyChanged(nameof(ExplicacaoLogin));
        OnPropertyChanged(nameof(AjudaLogin));
        OnPropertyChanged(nameof(TextoEntrar));
        OnPropertyChanged(nameof(PodeEntrar));
        OnPropertyChanged(nameof(ConfereLogin));
        OnPropertyChanged(nameof(SemConferencia));
    }

    public sealed record Descoberta(IReadOnlyList<Achado> Novos, int JaNoNexora, IReadOnlyList<(string Pasta, int Quantos)> Raizes);

    public async Task<Descoberta> ProcurarNoVsCodeAsync()
    {
        var casa = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Sistema.Mac ? Path.Combine(casa, "Library", "Application Support") : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var principaisConhecidos = _janela.Ambientes.Select(a => a.Dados.Principal).ToList();
        var conhecidas = _janela.Ambientes.SelectMany(a => new[] { a.Dados.Principal }.Concat(a.Dados.Vinculados)).ToList();
        var ocultos = App.Config.Ocultos.ToList();
        var raizes = App.Config.Raizes.ToList();
        return await Task.Run(() =>
        {
            var achados = VsCode.Recentes(appData);
            var principais = achados.Where(a => a.Workspace)
                .Select(a => (Achado: a, Ambiente: Workspaces.Ler(a.Caminho)))
                .Where(x => x.Ambiente is not null)
                .ToList();
            var pastasDosWorkspaces = principais.SelectMany(x => new[] { x.Ambiente!.Principal }.Concat(x.Ambiente.Vinculados)).ToList();
            bool JaTem(string pasta) => principaisConhecidos.Any(c => Workspaces.MesmaPasta(c, pasta)) || ocultos.Any(o => Workspaces.MesmaPasta(o, pasta));
            var novos = new List<Achado>();
            var ja = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var novosPrincipais = new List<string>();
            foreach (var (achado, ambiente) in principais)
            {
                if (JaTem(ambiente!.Principal)) ja.Add(ambiente.Principal.TrimEnd('\\', '/'));
                else if (!novosPrincipais.Any(p => Workspaces.MesmaPasta(p, ambiente.Principal)))
                {
                    novos.Add(achado);
                    novosPrincipais.Add(ambiente.Principal);
                }
            }
            foreach (var pasta in achados.Where(a => !a.Workspace))
            {
                if (JaTem(pasta.Caminho)) ja.Add(pasta.Caminho.TrimEnd('\\', '/'));
                else if (!conhecidas.Concat(pastasDosWorkspaces).Any(p => VsCode.Dentro(pasta.Caminho, p))) novos.Add(pasta);
            }
            var sugeridas = VsCode.Raizes(achados, casa).Where(r => !raizes.Any(x => VsCode.Dentro(r.Pasta, x))).ToList();
            return new Descoberta(novos.OrderBy(n => n.Nome, StringComparer.OrdinalIgnoreCase).ToList(), ja.Count, sugeridas);
        });
    }

    public void ImportarAchados(IEnumerable<string> caminhos)
    {
        foreach (var caminho in caminhos)
        {
            if (caminho.EndsWith(".code-workspace", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(caminho)) _janela.ImportarWorkspace(caminho);
                continue;
            }
            _janela.AdicionarPastaComoProjeto(caminho);
        }
        if (!JanelaVM.Isolada) App.Config.Salvar();
    }

    public ObservableCollection<RequisitoVM> Requisitos { get; }
    public ObservableCollection<RaizVM> Raizes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoInicio), nameof(NosRequisitos), nameof(NoLogin), nameof(NosProjetos), nameof(NoVisual), nameof(NoFim),
        nameof(TextoPasso), nameof(TextoAvancar), nameof(PodeVoltar))]
    public partial int Passo { get; set; }

    public bool NoInicio => Passo == 0;
    public bool NosRequisitos => Passo == 1;
    public bool NoLogin => Passo == 2;
    public bool NosProjetos => Passo == 3;
    public bool NoVisual => Passo == 4;
    public bool NoFim => Passo == 5;
    public bool PodeVoltar => Passo > 0;
    public string TextoPasso => Idioma.F("{0} de {1}", Passo + 1, Passos);
    public string TextoAvancar => Passo switch
    {
        0 => Idioma.T("Começar"),
        Passos - 1 => Idioma.T("Abrir o Nexora"),
        _ => Idioma.T("Continuar"),
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NaoLogado), nameof(TextoLogin), nameof(FaltaLogin), nameof(MostraEntrar))]
    public partial bool Logado { get; set; }

    public bool NaoLogado => !Logado;
    public bool ConfereLogin => Agente.Protocolo is ProtocoloAgente.Claude or ProtocoloAgente.Codex;
    public bool SemConferencia => !ConfereLogin;
    public bool FaltaLogin => ConfereLogin && !Logado;
    public bool MostraEntrar => !Logado;
    public bool PodeEntrar => _programaDoAgente is not null && ComandoDeLogin(Agente) is not null;

    public string TituloLogin => Agente.EhClaude ? Idioma.T("Entrar no Claude") : Idioma.F("Entrar no {0}", Agente.Nome);

    public string ExplicacaoLogin => Agente.EhClaude
        ? Idioma.T("O Nexora usa o seu Claude Code, com a sua conta e o seu plano. Ele nunca vê sua senha nem seu token: o login é feito pelo próprio Claude Code.")
        : Idioma.F("O Nexora usa o seu {0}, com a sua conta e o seu plano. Ele nunca vê sua senha nem seu token: o login é feito pelo próprio {0}.", Agente.Nome);

    public string AjudaLogin => Agente.Protocolo switch
    {
        ProtocoloAgente.Claude => Idioma.T("Abra o Claude Code, siga o /login no navegador e volte aqui. Se você já usa a extensão do VS Code logado, este passo já está feito."),
        _ when ComandoDeLogin(Agente) is null => Idioma.F("O login do {0} é feito no próprio app dele. Se faltar, ele pede na primeira conversa, e o Nexora mostra o link dentro dela.", Agente.Nome),
        _ when _programaDoAgente is null => Idioma.F("Instale o {0} no passo anterior. Depois, o login pode ser feito aqui ou na primeira conversa, quando ele pedir.", Agente.Nome),
        ProtocoloAgente.Codex => Idioma.T("Abra o Codex, entre com a sua conta do ChatGPT no navegador e volte aqui."),
        _ => Idioma.F("Abra o {0} e siga o login no navegador. Se pular este passo, o {0} pede o login na primeira conversa, e o Nexora mostra o link dentro dela.", Agente.Nome),
    };

    public string TextoEntrar => Agente.EhClaude ? Idioma.T("Abrir o Claude para entrar") : Idioma.F("Abrir o {0} para entrar", Agente.Nome);

    public string TextoLogin => Agente.Protocolo switch
    {
        ProtocoloAgente.Claude => Logado
            ? Idioma.T("Pronto: o Claude Code deste computador já tem um login.")
            : Idioma.T("Ainda não achei um login do Claude Code neste computador."),
        ProtocoloAgente.Codex => Logado
            ? Idioma.T("Pronto: o Codex deste computador já tem um login.")
            : Idioma.T("Ainda não achei um login do Codex neste computador."),
        _ => Idioma.F("O {0} confere o login quando a primeira conversa começa.", Agente.Nome),
    };

    static string? ComandoDeLogin(DescricaoAgente agente) => agente.Id switch
    {
        Agentes.Claude => "/login",
        Agentes.Codex => "login",
        "cursor" or "kiro" => "login",
        "opencode" => "auth login",
        "goose" => "configure",
        "gemini" or "qwen" or "copilot" or "grok" => "",
        _ => null,
    };

    [ObservableProperty]
    public partial string TextoProjetos { get; set; } = "";

    [ObservableProperty]
    public partial string TemaEscolhido { get; set; } = Catalogo.SegueOSistema(App.Config.Tema) ? Catalogo.IdSistema : App.Config.Tema;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ComVidro))]
    public partial bool Solido { get; set; } = App.Config.ModoFundo == "solido";

    public bool ComVidro
    {
        get => !Solido;
        set => Solido = !value;
    }

    IReadOnlyList<TemaCartaoVM>? _principais;
    IReadOnlyList<TemaCartaoVM>? _outros;

    public IReadOnlyList<TemaCartaoVM> TemasPrincipais => _principais ??=
    [
        TemaCartaoVM.DoSistema(null),
        new TemaCartaoVM(Catalogo.Embutidos[0], null) { Apelido = Idioma.T("Escuro") },
        new TemaCartaoVM(Catalogo.Embutidos[1], null) { Apelido = Idioma.T("Claro") },
    ];

    public IReadOnlyList<TemaCartaoVM> OutrosTemas => _outros ??= [.. Catalogo.Todos().Skip(2).Select(t => new TemaCartaoVM(t, null))];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoMaisTemas))]
    public partial bool MaisTemas { get; set; } = !Catalogo.SegueOSistema(App.Config.Tema) && App.Config.Tema is not ("padrao" or "padrao-claro" or "suiciniv" or "suiciniv-papel");

    public string TextoMaisTemas => MaisTemas ? Idioma.T("Menos temas") : Idioma.T("Mais temas");

    [RelayCommand]
    void AlternarMaisTemas() => MaisTemas = !MaisTemas;

    public void Reiniciar()
    {
        Passo = 0;
        AtualizarAgente();
        Verificar();
        CarregarRaizes();
    }

    [RelayCommand]
    void Avancar()
    {
        if (Passo >= Passos - 1)
        {
            Concluir();
            return;
        }
        Passo++;
        AtualizarAgente();
        if (NosRequisitos) Verificar();
        if (NoLogin) VerificarLogin();
        if (NosProjetos) _ = ContarProjetosAsync();
    }

    [RelayCommand]
    void Voltar()
    {
        if (Passo > 0) Passo--;
    }

    [RelayCommand]
    void Pular() => Concluir();

    public void Concluir()
    {
        App.Config.BoasVindasFeitas = true;
        if (!JanelaVM.Isolada) App.Config.Salvar();
        _janela.MostraBoasVindas = false;
        _ = _janela.CarregarAmbientesAsync();
    }

    [RelayCommand]
    public void Verificar()
    {
        Instalacao.AtualizarPath();
        AtualizarAgente();
        if (Agente.EhClaude)
        {
            var claude = LocalizadorClaude.Procurar();
            Marcar(0, claude, claude switch
            {
                null => Idioma.T("Não encontrado"),
                _ when claude.Contains("anthropic.claude-code", StringComparison.OrdinalIgnoreCase) => Idioma.T("Usando o que vem na extensão do VS Code"),
                _ => claude,
            });
        }
        else
        {
            var achado = CatalogoAgentes.Procurar(Agente);
            Marcar(0, achado, achado ?? Idioma.T("Não encontrado"));
        }
        var git = Instalacao.NoPath("git.exe");
        Marcar(1, git, git ?? Idioma.T("Não encontrado"));
        var node = Instalacao.NoPath("node.exe");
        Marcar(2, node, node ?? Idioma.T("Não encontrado: só faz falta se um projeto usar ferramentas extras (servidores MCP)"));
        Marcar(3, "ok", (File.Exists(Path.Combine(AppContext.BaseDirectory, "coreclr.dll")) || File.Exists(Path.Combine(AppContext.BaseDirectory, "libcoreclr.dylib")))
            ? Idioma.F("Vem junto com o Nexora (versão {0})", Environment.Version)
            : Idioma.F("Versão {0}", Environment.Version));
        VerificarLogin();
    }

    public async Task<bool> InstalarAsync(RequisitoVM requisito, Action<string> progresso)
    {
        var ok = await Instalacao.InstalarAsync(requisito.Receita, progresso);
        Verificar();
        return ok && requisito.Ok;
    }

    void Marcar(int i, string? achado, string detalhe)
    {
        Requisitos[i].Ok = achado is not null;
        Requisitos[i].Detalhe = detalhe;
    }

    [RelayCommand]
    void VerificarLogin()
    {
        var casa = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (Agente.Protocolo == ProtocoloAgente.Codex)
        {
            var pasta = Environment.GetEnvironmentVariable("CODEX_HOME") is { Length: > 0 } propria ? propria : Path.Combine(casa, ".codex");
            Logado = File.Exists(Path.Combine(pasta, "auth.json")) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
            return;
        }
        if (!ConfereLogin)
        {
            Logado = false;
            return;
        }
        Logado = File.Exists(Path.Combine(casa, ".claude", ".credentials.json"))
            || Sistema.Mac && Sistema.TemNoChaveiro("Claude Code-credentials")
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN"));
    }

    [RelayCommand]
    void Entrar()
    {
        var agente = Agente;
        if (ComandoDeLogin(agente) is not { } argumentos || _programaDoAgente is not { } programa) return;
        var resto = argumentos.Length > 0 ? " " + argumentos : "";
        if (Sistema.Mac)
        {
            Sistema.AbrirTerminal($"{Sistema.Aspas(programa)}{resto}")?.Dispose();
            return;
        }
        try
        {
            Sistema.Abrir(new ProcessStartInfo("cmd.exe", $"/k \"\"{programa}\"{resto}\"") { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    void CarregarRaizes()
    {
        Raizes.Clear();
        foreach (var r in App.Config.Raizes) Raizes.Add(new RaizVM(r, RemoverRaiz));
        _ = ContarProjetosAsync();
    }

    public void AdicionarRaiz(string pasta)
    {
        _janela.AdicionarRaiz(pasta);
        CarregarRaizes();
    }

    void RemoverRaiz(RaizVM raiz) => RemoverRaiz(raiz.Pasta);

    public void RemoverRaiz(string pasta)
    {
        _janela.RemoverRaiz(pasta);
        CarregarRaizes();
    }

    public void Importar(string arquivo)
    {
        _janela.ImportarWorkspace(arquivo);
        _ = ContarProjetosAsync();
    }

    public void AdicionarProjeto(string pasta)
    {
        _janela.AdicionarPastaComoProjeto(pasta);
        _ = ContarProjetosAsync();
    }

    public async Task<string> ContarProjetosAsync()
    {
        var raizes = App.Config.Raizes.ToList();
        var extras = App.Config.Workspaces.ToList();
        var total = await Task.Run(() => Workspaces.ProcurarProjetos(raizes)
            .Concat(extras.Select(Workspaces.Ler).OfType<Ambiente>())
            .Select(a => a.Principal.TrimEnd('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count());
        TextoProjetos = total switch
        {
            0 => Idioma.T("Nenhum projeto encontrado ainda. Escolha a pasta de um projeto, ou a pasta onde ficam vários."),
            1 => Idioma.T("1 projeto encontrado."),
            _ => Idioma.F("{0} projetos encontrados.", total),
        };
        return TextoProjetos;
    }

    [RelayCommand]
    void UsarTema(string id)
    {
        TemaEscolhido = id;
        App.Config.Tema = id;
        if (!JanelaVM.Isolada) App.Config.Salvar();
        Catalogo.Escolher(id);
        foreach (var cartao in TemasPrincipais.Concat(OutrosTemas)) cartao.AtualizarUso();
    }

    partial void OnSolidoChanged(bool value) => _janela.MudarModoFundo(value ? "solido" : "animado");
}
