using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed record Local(string Nome, string Pasta, IReadOnlyList<string> Vinculados)
{
    public string? Branch => Repositorio.BranchAtual(Pasta);
    public string Descricao => Branch is { Length: > 0 } b ? $"{Nome}  ·  {b}" : Nome;
    public override string ToString() => Nome;
}

public interface IAnfitriao
{
    void AbrirSessao(Local local, string? primeiraMensagem = null);
    bool Ocupada(string pasta);
    void BranchMudou(string pasta);
    AmbienteVM Registrar(Ambiente ambiente);
    void Esconder(AmbienteVM ambiente);
    void PedirExclusao(AmbienteVM ambiente);
    bool LiberadaEmTodas(Core.Claude.PermissaoPedida pedido);
    void LiberarEmTodas(string chave);
    void Lembrar(Ambiente ambiente);
    AmbienteVM? AmbienteDe(string pasta);
    IReadOnlyList<Local> Locais();
    Local LocalDe(string pasta);
    void Mostrar(ChatVM chat);
    IReadOnlyList<ConversaVM> ConversasDe(string pasta);
    string? EmOutroLugar(string sessaoId);
    void MostrarExecutar(Local local);
    IReadOnlyList<string> UsosDe(string pasta);
    void Esquecer(string pasta);
    void AbrirLateral(ChatVM origem, string? pergunta) { }
    void AbrirEmSplit(object conteudo) { }
    IReadOnlyList<ServidorNoAr> ServidoresNoAr(Local local) => [];
}

public sealed partial class AmbienteVM : ObservableObject
{
    readonly IAnfitriao _anfitriao;

    public AmbienteVM(Ambiente dados, IAnfitriao anfitriao)
    {
        Dados = dados;
        _anfitriao = anfitriao;
        foreach (var v in dados.Vinculados) Vinculados.Add(new VinculadoVM(v, RemoverVinculado));
        Branch = Repositorio.BranchAtual(dados.Principal);
        NomeTarefa = "";
        Descricao = "";
    }

    public Ambiente Dados { get; private set; }
    public string Nome => Dados.Nome;
    public string Principal => Dados.Principal;
    public string? Arquivo => Dados.Arquivo;
    public ObservableCollection<VinculadoVM> Vinculados { get; } = [];
    public ObservableCollection<string> Branches { get; } = [];
    public ObservableCollection<WorktreeVM> Worktrees { get; } = [];
    public Local Local => new(Nome, Principal, Dados.Vinculados);
    public string Resumo => Vinculados.Count switch
    {
        0 => Idioma.T("sem projetos vinculados"),
        1 => Idioma.T("1 projeto vinculado"),
        var n => Idioma.F("{0} projetos vinculados", n),
    };
    public bool TemVinculados => Vinculados.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BranchCurta), nameof(TextoNaBranch))]
    public partial string? Branch { get; set; }

    [ObservableProperty]
    public partial string? BranchEscolhida { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAlteracoes), nameof(TextoAlteracoes))]
    public partial int Alteracoes { get; set; }

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    public partial string? Mensagem { get; set; }

    [ObservableProperty]
    public partial bool MensagemDeErro { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviaBranch), nameof(PreviaPasta), nameof(PodeCriar))]
    public partial string NomeTarefa { get; set; }

    [ObservableProperty]
    public partial string? BaseTarefa { get; set; }

    bool _baseCarregando;

    string? BaseSalva => App.Config.BasesDasTarefas.GetValueOrDefault(Config.ChaveDePasta(Principal));

    partial void OnBaseTarefaChanged(string? value)
    {
        if (_baseCarregando || value is not { Length: > 0 } || value == BaseSalva) return;
        App.Config.BasesDasTarefas[Config.ChaveDePasta(Principal)] = value;
        if (!JanelaVM.Isolada) App.Config.Salvar();
    }

    public async Task GarantirBranchesAsync()
    {
        if (Branches.Count > 0 && BaseTarefa is not null) return;
        await Atualizar();
    }

    [ObservableProperty]
    public partial string Descricao { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeCriar))]
    public partial bool Criando { get; set; }

    public string BranchCurta => Branch ?? Idioma.T("sem git");
    public string TextoNaBranch => Idioma.F("na branch {0}", BranchCurta);
    public bool TemAlteracoes => Alteracoes > 0;
    public string TextoAlteracoes => Alteracoes == 1 ? Idioma.T("1 arquivo alterado") : Idioma.F("{0} arquivos alterados", Alteracoes);
    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);
    public bool PodeCriar => !Criando && Repositorio.Slug(NomeTarefa).Length > 0;
    string Slug => Repositorio.Slug(NomeTarefa);
    public string PreviaBranch => Slug.Length == 0 ? "—" : Repositorio.NomeDaTarefa(NomeTarefa, DateOnly.FromDateTime(DateTime.Now));
    public string PreviaPasta => Slug.Length == 0 ? "—" : Repositorio.PastaDoWorktree(RepositorioPrincipal, Slug);

    string RepositorioPrincipal => Worktrees.FirstOrDefault(w => w.Principal)?.Caminho ?? Principal;

    [RelayCommand]
    public async Task Atualizar()
    {
        Carregando = true;
        Branch = Repositorio.BranchAtual(Principal);
        var branches = await Repositorio.BranchesAsync(Principal);
        var worktrees = await Repositorio.WorktreesAsync(Principal);
        var padrao = await Repositorio.BranchPadraoAsync(Principal);
        Alteracoes = await Repositorio.AlteracoesAsync(Principal);
        if (JanelaVM.Demo && branches.Count == 0)
        {
            branches = ["dev", "main", "cupom-checkout", "frete-gratis"];
            padrao = "dev";
            Branch ??= "main";
        }

        Branches.Clear();
        foreach (var b in branches) Branches.Add(b);
        BranchEscolhida = Branch;
        _baseCarregando = true;
        BaseTarefa ??= BaseSalva ?? padrao;
        _baseCarregando = false;

        Worktrees.Clear();
        foreach (var w in worktrees) Worktrees.Add(new WorktreeVM(w, Workspaces.MesmaPasta(w.Caminho, Principal), AbrirWorktree, RemoverWorktreeAsync));
        OnPropertyChanged(nameof(EhWorktree));
        _ = CarregarPrAsync();
        foreach (var v in Vinculados) v.Atualizar();
        OnPropertyChanged(nameof(PreviaPasta));
        AtualizarProjeto();
        Carregando = false;
    }

    [RelayCommand]
    void NovaSessao() => _anfitriao.AbrirSessao(Local);

    [RelayCommand]
    void Executar() => _anfitriao.MostrarExecutar(Local);

    [RelayCommand]
    async Task TrocarBranch()
    {
        if (string.IsNullOrEmpty(BranchEscolhida) || BranchEscolhida == Branch) return;
        if (_anfitriao.Ocupada(Principal))
        {
            Avisar(Idioma.T("Tem uma sessão trabalhando nesta pasta. Espere ela terminar para trocar de branch."), erro: true);
            return;
        }
        var r = await Repositorio.TrocarAsync(Principal, BranchEscolhida);
        if (r.Ok) Avisar(Idioma.F("Agora em {0}.", BranchEscolhida), erro: false);
        else Avisar(r.Mensagem, erro: true);
        await Atualizar();
        _anfitriao.BranchMudou(Principal);
    }

    [RelayCommand]
    async Task CriarTarefa()
    {
        if (!PodeCriar || BaseTarefa is null) return;
        Criando = true;
        Avisar(Idioma.F("Criando o worktree a partir de {0}…", BaseTarefa), erro: false);
        var (novo, mensagem) = await CriarWorktreeAsync(NomeTarefa, BaseTarefa);
        Criando = false;
        Avisar(mensagem, erro: novo is null);
        if (novo is null) return;
        var primeira = string.IsNullOrWhiteSpace(Descricao) ? null : Descricao.Trim();
        NomeTarefa = "";
        Descricao = "";
        await Atualizar();
        _anfitriao.AbrirSessao(novo.Local, primeira);
    }

    public async Task<(AmbienteVM? Novo, string Mensagem)> CriarWorktreeAsync(string nome, string? baseBranch = null)
    {
        var slug = Repositorio.Slug(nome);
        if (slug.Length == 0) return (null, Idioma.T("O nome da tarefa precisa ter letras ou números."));
        if ((baseBranch ?? BaseTarefa ?? await Repositorio.BranchPadraoAsync(Principal)) is not { } origem)
            return (null, Idioma.F("{0} não tem branch para servir de base.", Nome));
        var branch = Repositorio.NomeDaTarefa(nome, DateOnly.FromDateTime(DateTime.Now));
        var pasta = Repositorio.PastaDoWorktree(RepositorioPrincipal, slug);
        var r = await Repositorio.CriarWorktreeAsync(RepositorioPrincipal, pasta, branch, origem);
        if (!r.Ok) return (null, r.Mensagem);
        var copia = await ArquivosLocais.CopiarAsync(RepositorioPrincipal, pasta);
        var arquivo = Workspaces.Escrever(Dados, pasta);
        var novo = _anfitriao.Registrar(Workspaces.Ler(arquivo) ?? Dados.ParaWorktree(pasta));
        return (novo, Idioma.F("Worktree {0} criado na branch {1}. {2}", Path.GetFileName(pasta), branch, copia.Resumo).Trim());
    }

    [RelayCommand]
    void AbrirNoVsCode() => Sistema.AbrirNoVsCode(Dados.Arquivo ?? Principal);

    [RelayCommand]
    void AbrirPasta() => Sistema.AbrirPasta(Principal);

    [RelayCommand]
    void Esconder() => _anfitriao.Esconder(this);

    [RelayCommand]
    void Excluir() => _anfitriao.PedirExclusao(this);

    public void AdicionarVinculado(string pasta)
    {
        if (Workspaces.MesmaPasta(pasta, Principal) || Dados.Vinculados.Any(v => Workspaces.MesmaPasta(v, pasta)))
        {
            Avisar(Idioma.T("Essa pasta já faz parte do ambiente."), erro: true);
            return;
        }
        Guardar(Dados with { Vinculados = [.. Dados.Vinculados, pasta] }, Idioma.F("{0} entrou no ambiente.", Path.GetFileName(pasta)));
    }

    void RemoverVinculado(VinculadoVM vinculado) =>
        Guardar(Dados with { Vinculados = Dados.Vinculados.Where(v => !Workspaces.MesmaPasta(v, vinculado.Pasta)).ToList() },
            Idioma.F("{0} saiu do ambiente.", vinculado.Nome));

    void Guardar(Ambiente novo, string mensagem)
    {
        try
        {
            Dados = Workspaces.Salvar(novo);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Avisar(Idioma.F("Não consegui salvar o workspace: {0}", ex.Message), erro: true);
            return;
        }
        _anfitriao.Lembrar(Dados);
        Vinculados.Clear();
        foreach (var v in Dados.Vinculados)
        {
            var vm = new VinculadoVM(v, RemoverVinculado);
            vm.Atualizar();
            Vinculados.Add(vm);
        }
        OnPropertyChanged(nameof(Resumo));
        OnPropertyChanged(nameof(TemVinculados));
        OnPropertyChanged(nameof(Arquivo));
        Avisar(Idioma.F("{0} As próximas sessões já usam a lista nova.", mensagem), erro: false);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemPr), nameof(PrMergeado))]
    public partial PullRequest? Pr { get; set; }

    public bool TemPr => Pr is not null;
    public bool PrMergeado => Pr is { Mergeado: true } && EhWorktree;
    public bool EhWorktree => Worktrees.Any(w => w.Atual && !w.Principal);

    async Task CarregarPrAsync()
    {
        if (Branch is not { Length: > 0 } branch || !Worktrees.Any(w => !w.Principal || w.Atual)) return;
        Pr = await Repositorio.PrAsync(Principal, branch);
    }

    [RelayCommand]
    void AbrirPr()
    {
        if (Pr is not { Url: { Length: > 0 } url }) return;
        try
        {
            Sistema.Abrir(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    [RelayCommand]
    void EncerrarTarefa() => Worktrees.FirstOrDefault(w => w.Atual && !w.Principal)?.PedirRemocaoCommand.Execute(null);

    async Task RemoverWorktreeAsync(WorktreeVM worktree)
    {
        if (_anfitriao.UsosDe(worktree.Caminho) is { Count: > 0 } usos)
        {
            Avisar(Idioma.F("Antes de remover {0}, feche o que está aberto nele: {1}.", worktree.Nome, string.Join(", ", usos)), erro: true);
            return;
        }
        var r = await Repositorio.RemoverWorktreeAsync(RepositorioPrincipal, worktree.Caminho, worktree.BranchReal, worktree.ApagarBranch);
        Avisar(r.Mensagem, erro: !r.Ok);
        if (!r.Ok) return;
        var eraEste = worktree.Atual;
        _anfitriao.Esquecer(worktree.Caminho);
        if (!eraEste) await Atualizar();
    }

    void AbrirWorktree(WorktreeVM worktree)
    {
        var ambiente = _anfitriao.AmbienteDe(worktree.Caminho) ?? _anfitriao.Registrar(Dados.ParaWorktree(worktree.Caminho));
        _anfitriao.AbrirSessao(ambiente.Local);
    }

    void Avisar(string texto, bool erro)
    {
        MensagemDeErro = erro;
        Mensagem = texto;
    }

    static void Abrir(string programa, IEnumerable<string> args)
    {
        var info = new ProcessStartInfo(programa) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) info.ArgumentList.Add(a);
        try
        {
            Sistema.Abrir(info)?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }
}

public sealed partial class VinculadoVM(string pasta, Action<VinculadoVM> remover) : ObservableObject
{
    [RelayCommand]
    void Remover() => remover(this);

    public string Pasta { get; } = pasta;
    public string Nome { get; } = Path.GetFileName(pasta.TrimEnd('\\', '/'));
    public bool Existe => Directory.Exists(Pasta);

    [ObservableProperty]
    public partial string? Branch { get; set; }

    public void Atualizar() => Branch = Existe ? Repositorio.BranchAtual(Pasta) ?? Idioma.T("sem git") : Idioma.T("pasta não existe");
}

public sealed partial class WorktreeVM(Worktree dados, bool atual, Action<WorktreeVM> abrir, Func<WorktreeVM, Task> remover) : ObservableObject
{
    public string Caminho => dados.Caminho;
    public string Nome => Path.GetFileName(dados.Caminho.TrimEnd('\\', '/'));
    public string Branch => dados.Branch ?? Idioma.T("(sem branch)");
    public string? BranchReal => dados.Branch;
    public bool Principal => dados.Principal;
    public bool Removivel => !dados.Principal;
    public bool Atual => atual;

    [ObservableProperty]
    public partial bool Confirmando { get; set; }

    [ObservableProperty]
    public partial bool ApagarBranch { get; set; } = true;

    [ObservableProperty]
    public partial bool Removendo { get; set; }

    public string TextoConfirmar => Idioma.F("Remover a pasta {0}? O trabalho com commit continua na branch.", Nome);
    public string TextoApagarBranch => Idioma.F("Apagar também a branch {0} (só se já estiver mergeada)", Branch);

    [RelayCommand]
    void NovaSessao() => abrir(this);

    [RelayCommand]
    void PedirRemocao() => Confirmando = true;

    [RelayCommand]
    void CancelarRemocao() => Confirmando = false;

    [RelayCommand]
    async Task Remover()
    {
        Removendo = true;
        await remover(this);
        Removendo = false;
        Confirmando = false;
    }
}
