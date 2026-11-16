using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Arquivos;
using Nexora.Core.Git;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class NoArquivoVM : ObservableObject
{
    static readonly NoArquivoVM Carregando = new("…", "", false, null, null);
    readonly PainelVM? _painel;
    bool _carregado;

    public NoArquivoVM(string nome, string caminho, bool ehPasta, PainelVM? painel, NoArquivoVM? pai, bool novo = false)
    {
        Nome = nome;
        Caminho = caminho;
        EhPasta = ehPasta;
        Pai = pai;
        Novo = novo;
        _painel = painel;
        NomeEditado = nome;
        if (ehPasta && !novo) Filhos.Add(Carregando);
        if (ehPasta && !novo && pai is not null && PastasAbertas.Contains(caminho)) Expandido = true;
    }

    static readonly HashSet<string> PastasAbertas = new(App.Config.PastasAbertas, StringComparer.OrdinalIgnoreCase);

    static void LembrarPasta(string caminho, bool aberta)
    {
        if (caminho.Length == 0 || (aberta ? !PastasAbertas.Add(caminho) : !PastasAbertas.Remove(caminho))) return;
        App.Config.PastasAbertas = [.. PastasAbertas.Where(Directory.Exists).Take(400)];
        if (!JanelaVM.Isolada) App.Config.Salvar();
    }

    public string Nome { get; }
    public string Caminho { get; }
    public bool EhPasta { get; }
    public NoArquivoVM? Pai { get; }
    public bool Novo { get; }
    public bool Real => Caminho.Length > 0 && !Novo;
    public bool Mexivel => Real && Pai is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraNome))]
    public partial bool Editando { get; set; }

    [ObservableProperty]
    public partial string NomeEditado { get; set; }

    [ObservableProperty]
    public partial bool Confirmando { get; set; }

    public bool MostraNome => !Editando;
    public string Dica => EhPasta ? Idioma.T("nome da pasta") : Idioma.T("nome do arquivo");
    public string TextoApagar => Idioma.F("Mandar {0} para a Lixeira?", Nome);

    [RelayCommand]
    void NovoArquivo() => _painel?.Novo(this, pasta: false);

    [RelayCommand]
    void NovaPasta() => _painel?.Novo(this, pasta: true);

    [RelayCommand]
    void Renomear()
    {
        NomeEditado = Nome;
        Editando = true;
    }

    [RelayCommand]
    void Confirmar() => _painel?.ConfirmarEdicao(this);

    [RelayCommand]
    void Cancelar() => _painel?.CancelarEdicao(this);

    [RelayCommand]
    void Apagar() => Confirmando = true;

    [RelayCommand]
    void NaoApagar() => Confirmando = false;

    [RelayCommand]
    void ConfirmarApagar() => _painel?.Apagar(this);

    [RelayCommand]
    void MostrarNoExplorer() => Sistema.Revelar(Caminho, EhPasta);
    public string Icone => EhPasta ? "pasta" : "arquivo";
    public ObservableCollection<NoArquivoVM> Filhos { get; } = [];

    [ObservableProperty]
    public partial bool Expandido { get; set; }

    partial void OnExpandidoChanged(bool value)
    {
        if (value) Carregar();
        if (EhPasta && Real && Pai is not null) LembrarPasta(Caminho, value);
    }

    public void Recarregar()
    {
        _carregado = false;
        if (Expandido) Carregar();
    }

    void Carregar()
    {
        if (_carregado || !EhPasta) return;
        _carregado = true;
        Filhos.Clear();
        foreach (var e in Pastas.Filhos(Caminho)) Filhos.Add(new NoArquivoVM(e.Nome, e.Caminho, e.EhPasta, _painel, this));
    }
}

public sealed partial class AlteracaoVM(Alteracao dados, string pasta, Action<AlteracaoVM> abrir, ItemTurno? turno = null, ArquivoDoTurno? linhas = null) : ObservableObject
{
    public Alteracao Dados { get; } = dados;
    public string Pasta { get; } = pasta;
    public ItemTurno? Turno { get; } = turno;
    public bool TemLinhas => linhas is { Binario: false };
    public string TextoMais => linhas is { } l ? $"+{l.Mais}" : "";
    public string TextoMenos => linhas is { Menos: > 0 } l ? $"−{l.Menos}" : "";
    public string Nome => Path.GetFileName(Dados.Caminho);
    public string Diretorio => Path.GetDirectoryName(Dados.Caminho)?.Replace('\\', '/') ?? "";
    public string Rotulo => Dados.Rotulo;
    public bool Nova => Dados.RotuloNovo;
    public bool Apagada => Dados.RotuloApagado;

    [RelayCommand]
    void Abrir() => abrir(this);
}

public sealed class OpcaoDiffVM(string rotulo, ItemTurno? turno)
{
    public string Rotulo { get; } = rotulo;
    public ItemTurno? Turno { get; } = turno;
}

public sealed class GrupoBuscaVM(string nome, string diretorio, IReadOnlyList<LinhaBuscaVM> linhas)
{
    public string Nome { get; } = nome;
    public string Diretorio { get; } = diretorio;
    public IReadOnlyList<LinhaBuscaVM> Linhas { get; } = linhas;
    public string Total => Linhas.Count.ToString();
}

public sealed partial class LinhaBuscaVM(Achado achado, Action<Achado> abrir) : ObservableObject
{
    public int Linha => achado.Linha;
    public string Texto => achado.Texto;

    [RelayCommand]
    void Abrir() => abrir(achado);
}

public sealed partial class PainelVM : ObservableObject
{
    readonly EditorVM _editor;
    readonly DispatcherTimer _espera;
    CancellationTokenSource? _busca;
    Local _local;

    public PainelVM(Local local, EditorVM editor)
    {
        _local = local;
        _editor = editor;
        _espera = new DispatcherTimer(TimeSpan.FromMilliseconds(350), DispatcherPriority.Background, async (_, _) =>
        {
            _espera!.Stop();
            await Buscar();
        });
        Montar();
    }

    public event Action? PedirFocoBusca;
    public ObservableCollection<GrupoBuscaVM> ResultadosBusca { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraArquivos))]
    public partial bool MostraBusca { get; set; }

    [ObservableProperty]
    public partial string TermoBusca { get; set; } = "";

    [ObservableProperty]
    public partial bool Maiusculas { get; set; }

    [ObservableProperty]
    public partial bool Regex { get; set; }

    [ObservableProperty]
    public partial bool Buscando { get; set; }

    [ObservableProperty]
    public partial string? ResumoBusca { get; set; }

    partial void OnTermoBuscaChanged(string value) => AgendarBusca();
    partial void OnMaiusculasChanged(bool value) => AgendarBusca();
    partial void OnRegexChanged(bool value) => AgendarBusca();

    void AgendarBusca()
    {
        _espera.Stop();
        _espera.Start();
    }

    [RelayCommand]
    void VerBusca()
    {
        MostraAlteracoes = false;
        MostraBusca = true;
        PedirFocoBusca?.Invoke();
    }

    [RelayCommand]
    void AbrirBusca()
    {
        Visivel = true;
        VerBusca();
    }

    [RelayCommand]
    void AlternarMaiusculas() => Maiusculas = !Maiusculas;

    [RelayCommand]
    void AlternarRegex() => Regex = !Regex;

    [RelayCommand]
    async Task Buscar()
    {
        _busca?.Cancel();
        var termo = TermoBusca;
        if (termo.Trim().Length == 0)
        {
            ResultadosBusca.Clear();
            ResumoBusca = null;
            return;
        }
        var cancelar = (_busca = new CancellationTokenSource()).Token;
        Buscando = true;
        var raizes = new[] { _local.Pasta }.Concat(_local.Vinculados).ToList();
        var (maiusculas, regex) = (Maiusculas, Regex);
        ResultadoBusca resultado;
        try
        {
            resultado = await Task.Run(() => Busca.ProcurarAsync(raizes, termo, maiusculas, regex, cancelar: cancelar), cancelar);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (cancelar.IsCancellationRequested) return;
        Buscando = false;
        ResultadosBusca.Clear();
        foreach (var arquivo in resultado.Achados.GroupBy(a => a.Caminho))
        {
            var primeiro = arquivo.First();
            var diretorio = Path.GetDirectoryName(primeiro.Relativo)?.Replace('\\', '/') ?? "";
            var raiz = Path.GetFileName(primeiro.Raiz.TrimEnd('\\', '/'));
            ResultadosBusca.Add(new GrupoBuscaVM(Path.GetFileName(primeiro.Caminho), diretorio.Length > 0 ? $"{raiz}/{diretorio}" : raiz,
                [.. arquivo.Select(a => new LinhaBuscaVM(a, x => _editor.Abrir(x.Caminho, x.Linha)))]));
        }
        var total = resultado.Achados.Count;
        ResumoBusca = total == 0 ? Idioma.T("Nada encontrado.")
            : Idioma.F("{0} em {1}", total == 1 ? Idioma.T("1 resultado") : Idioma.F("{0} resultados", total), ResultadosBusca.Count == 1 ? Idioma.T("1 arquivo") : Idioma.F("{0} arquivos", ResultadosBusca.Count))
              + (resultado.Cortado ? Idioma.T(" (mostrando os primeiros)") : "");
    }

    public ObservableCollection<NoArquivoVM> Raizes { get; } = [];
    public ObservableCollection<AlteracaoVM> Alteracoes { get; } = [];
    public ObservableCollection<OpcaoDiffVM> OpcoesDiff { get; } = [new(Idioma.T("Tudo que está alterado agora"), null)];
    public bool TemOpcoesDiff => OpcoesDiff.Count > 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoSemAlteracoes))]
    public partial OpcaoDiffVM? OpcaoDiff { get; set; }

    public string TextoSemAlteracoes => OpcaoDiff?.Turno is null ? Idioma.T("Nada alterado no git deste ambiente.") : Idioma.T("Este turno não mudou nenhum arquivo.");

    partial void OnOpcaoDiffChanged(OpcaoDiffVM? value)
    {
        if (!_trocandoOpcoes) _ = AtualizarAlteracoes();
    }

    bool _trocandoOpcoes;

    public void DefinirTurnos(IEnumerable<ItemTurno> turnos)
    {
        var escolhido = OpcaoDiff?.Turno;
        _trocandoOpcoes = true;
        try
        {
            while (OpcoesDiff.Count > 1) OpcoesDiff.RemoveAt(OpcoesDiff.Count - 1);
            foreach (var turno in turnos.Where(t => t.Visivel).OrderByDescending(t => t.Numero))
                OpcoesDiff.Add(new OpcaoDiffVM(Idioma.F("Turno {0}: {1}", turno.Numero, Core.Claude.Resumos.UmaLinha(turno.Mensagem.Texto, 48)), turno));
            OpcaoDiff = OpcoesDiff.FirstOrDefault(o => o.Turno is not null && o.Turno == escolhido) ?? OpcoesDiff[0];
        }
        finally
        {
            _trocandoOpcoes = false;
        }
        OnPropertyChanged(nameof(TemOpcoesDiff));
        if (escolhido is not null && OpcaoDiff.Turno is null) _ = AtualizarAlteracoes();
    }

    public void MostrarTurno(ItemTurno turno)
    {
        if (OpcoesDiff.FirstOrDefault(o => o.Turno == turno) is { } opcao) OpcaoDiff = opcao;
    }

    [ObservableProperty]
    public partial bool Visivel { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraArquivos))]
    public partial bool MostraAlteracoes { get; set; }

    [ObservableProperty]
    public partial NoArquivoVM? Selecionado { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoAlteracoes), nameof(SemAlteracoes))]
    public partial int TotalAlteracoes { get; set; }

    public bool MostraArquivos => !MostraAlteracoes && !MostraBusca;
    public bool SemAlteracoes => TotalAlteracoes == 0;
    public string TextoAlteracoes => TotalAlteracoes == 0 ? Idioma.T("Alterações") : $"{Idioma.T("Alterações")}  {TotalAlteracoes}";

    public void Trocar(Local local)
    {
        _local = local;
        Montar();
    }

    partial void OnSelecionadoChanged(NoArquivoVM? value)
    {
        if (value is null || value.Caminho.Length == 0) return;
        if (value.EhPasta) value.Expandido = !value.Expandido;
        else _editor.Abrir(value.Caminho);
    }

    partial void OnVisivelChanged(bool value)
    {
        if (value) _ = AtualizarAlteracoes();
    }

    partial void OnMostraAlteracoesChanged(bool value)
    {
        if (value) _ = AtualizarAlteracoes();
    }

    [RelayCommand]
    void VerArquivos()
    {
        MostraAlteracoes = false;
        MostraBusca = false;
    }

    [RelayCommand]
    void VerAlteracoes()
    {
        MostraBusca = false;
        MostraAlteracoes = true;
    }

    [RelayCommand]
    void Alternar() => Visivel = !Visivel;

    [RelayCommand]
    public async Task AtualizarAlteracoes()
    {
        var lista = new List<AlteracaoVM>();
        if (OpcaoDiff?.Turno is { Resumo: { } resumo } turno)
        {
            foreach (var a in resumo.Arquivos)
                lista.Add(new AlteracaoVM(new Alteracao(a.Status, a.Caminho, null), resumo.Raiz, AbrirDiff, turno, a));
            Alteracoes.Clear();
            foreach (var a in lista) Alteracoes.Add(a);
            TotalAlteracoes = lista.Count;
            return;
        }
        foreach (var pasta in new[] { _local.Pasta }.Concat(_local.Vinculados).Where(Directory.Exists))
            foreach (var a in await Core.Git.Alteracoes.ListarAsync(pasta))
                lista.Add(new AlteracaoVM(a, pasta, AbrirDiff));
        Alteracoes.Clear();
        foreach (var a in lista) Alteracoes.Add(a);
        TotalAlteracoes = lista.Count;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoArvore))]
    public partial string? AvisoArvore { get; set; }

    public bool TemAvisoArvore => AvisoArvore is not null;

    public void Novo(NoArquivoVM alvo, bool pasta)
    {
        if ((alvo.EhPasta ? alvo : alvo.Pai) is not { } destino) return;
        AvisoArvore = null;
        destino.Expandido = true;
        destino.Filhos.Insert(0, new NoArquivoVM("", "", pasta, this, destino, novo: true) { Editando = true });
    }

    public void ConfirmarEdicao(NoArquivoVM no)
    {
        AvisoArvore = null;
        try
        {
            if (no.Novo)
            {
                var pai = no.Pai!;
                var caminho = OperacoesArquivo.Criar(pai.Caminho, no.NomeEditado, no.EhPasta);
                pai.Recarregar();
                if (!no.EhPasta) _editor.Abrir(caminho);
                return;
            }
            if (_editor.TemModificadaEm(no.Caminho))
            {
                AvisoArvore = Idioma.F("Salve {0} (ou o que estiver aberto dentro dele) antes de renomear.", no.Nome);
                return;
            }
            var estavaAberto = _editor.FecharDe(no.Caminho);
            var novo = OperacoesArquivo.Renomear(no.Caminho, no.NomeEditado);
            no.Editando = false;
            no.Pai?.Recarregar();
            if (estavaAberto && !no.EhPasta) _editor.Abrir(novo);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AvisoArvore = ex.Message;
        }
    }

    public void CancelarEdicao(NoArquivoVM no)
    {
        AvisoArvore = null;
        if (no.Novo) no.Pai?.Filhos.Remove(no);
        else no.Editando = false;
    }

    public void Apagar(NoArquivoVM no)
    {
        AvisoArvore = null;
        no.Confirmando = false;
        if (_editor.TemModificadaEm(no.Caminho))
        {
            AvisoArvore = Idioma.F("Salve {0} (ou o que estiver aberto dentro dele) antes de apagar.", no.Nome);
            return;
        }
        try
        {
            _editor.FecharDe(no.Caminho);
            OperacoesArquivo.ParaLixeira(no.Caminho);
            no.Pai?.Recarregar();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            AvisoArvore = ex.Message;
        }
    }

    [RelayCommand]
    void RecarregarArvore()
    {
        foreach (var raiz in Raizes) raiz.Recarregar();
    }

    async void AbrirDiff(AlteracaoVM alteracao)
    {
        var arquivo = Path.Combine(alteracao.Pasta, alteracao.Dados.Caminho.Replace('/', Path.DirectorySeparatorChar));
        if (alteracao.Turno is { } turno)
        {
            if (turno is not { Antes: { } antes, Depois: { } depois }) return;
            var doTurno = await PontosDeRestauracao.DiffAsync(alteracao.Pasta, antes, depois, alteracao.Dados.Caminho);
            _editor.AbrirDiff($"{alteracao.Nome} ({Idioma.F("turno {0}", turno.Numero)})", $"{arquivo}#turno{turno.Numero}", Diff.LerUnificado(doTurno), arquivo);
            return;
        }
        var texto = await Core.Git.Alteracoes.DiffAsync(alteracao.Pasta, alteracao.Dados);
        _editor.AbrirDiff($"{alteracao.Nome} (git)", arquivo + "#git", Diff.LerUnificado(texto), arquivo);
    }

    void Montar()
    {
        Raizes.Clear();
        foreach (var pasta in new[] { _local.Pasta }.Concat(_local.Vinculados).Where(Directory.Exists))
        {
            var no = new NoArquivoVM(Path.GetFileName(pasta.TrimEnd('\\', '/')), pasta, true, this, null);
            Raizes.Add(no);
        }
        if (Raizes.Count > 0) Raizes[0].Expandido = true;
    }
}
