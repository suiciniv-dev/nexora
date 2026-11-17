using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Arquivos;
using Nexora.Core.Git;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ExclusaoVM : ObservableObject
{
    readonly JanelaVM _janela;

    public ExclusaoVM(AmbienteVM ambiente, JanelaVM janela)
    {
        Ambiente = ambiente;
        _janela = janela;
        Usos = janela.UsosDe(ambiente.Principal);
        _ = DiagnosticarAsync();
    }

    public AmbienteVM Ambiente { get; }
    public string Nome => Ambiente.Nome;
    public string Titulo => Idioma.F("Excluir o projeto {0}", Nome);
    public string Principal => Ambiente.Principal;
    public string? Arquivo => Ambiente.Arquivo is { } a && File.Exists(a) ? a : null;
    public string NomeArquivo => Path.GetFileName(Arquivo ?? "");
    bool ArquivoNaPasta => Arquivo is { } a && ExclusaoDeProjeto.DentroDe(a, Principal);
    public bool MostrarOpcaoArquivo => Arquivo is not null && !(ApagarPasta && ArquivoNaPasta);
    public string TextoArquivo => ArquivoNaPasta
        ? Idioma.F("Mandar {0} para a Lixeira", NomeArquivo)
        : Idioma.F("Mandar {0} para a Lixeira (fica em {1})", NomeArquivo, Path.GetDirectoryName(Arquivo));

    public IReadOnlyList<string> Usos { get; }
    public bool Bloqueado => Usos.Count > 0;
    public string TextoBloqueio => Idioma.F("Feche antes {0}.", string.Join(", ", Usos));
    public bool TemVinculados => Ambiente.Vinculados.Count > 0;
    public string TextoVinculados => Ambiente.Vinculados.Count == 1
        ? Idioma.T("A pasta vinculada não é tocada.")
        : Idioma.F("As {0} pastas vinculadas não são tocadas.", Ambiente.Vinculados.Count);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarOpcaoArquivo), nameof(Avisos), nameof(TemAvisos), nameof(PodeExcluir), nameof(TextoBotao), nameof(PodeApagarBranch))]
    public partial bool ApagarPasta { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoBotao))]
    public partial bool ApagarArquivo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoBotao))]
    public partial bool ApagarConversas { get; set; }

    [ObservableProperty]
    public partial bool ApagarBranch { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeExcluir))]
    public partial bool Diagnosticando { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EhWorktree), nameof(TextoPasta), nameof(DicaPasta), nameof(Avisos), nameof(TemAvisos), nameof(PodeExcluir),
        nameof(TemConversas), nameof(TextoConversas), nameof(PodeApagarBranch), nameof(TextoBranch))]
    public partial DiagnosticoDeExclusao? Dados { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeExcluir), nameof(TextoBotao))]
    public partial bool Excluindo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemErro))]
    public partial string? Erro { get; set; }

    public bool TemErro => Erro is not null;
    public bool EhWorktree => Dados?.EhWorktree == true;
    public string TextoPasta => EhWorktree ? Idioma.T("Remover o worktree") : Idioma.T("Mandar a pasta do projeto para a Lixeira");
    public string DicaPasta => EhWorktree ? Idioma.F("{0}  ·  sai pelo git worktree remove", Principal) : Principal;
    public bool PodeApagarBranch => EhWorktree && ApagarPasta && Dados?.Branch is not null;
    public string TextoBranch => Idioma.F("Apagar também a branch {0} (só se ela já estiver mergeada)", Dados?.Branch);
    public IReadOnlyList<string> Avisos => ApagarPasta && Dados is { } d ? ExclusaoDeProjeto.Avisos(d) : [];
    public bool TemAvisos => Avisos.Count > 0;
    public bool TemConversas => Dados?.Conversas > 0;
    public string TextoConversas => Dados?.Conversas == 1
        ? Idioma.T("Apagar a conversa deste projeto do histórico do Claude Code")
        : Idioma.F("Apagar as {0} conversas deste projeto do histórico do Claude Code", Dados?.Conversas);
    public string TextoBotao => Excluindo ? Idioma.T("Excluindo…")
        : ApagarPasta ? Idioma.T("Excluir com a pasta")
        : ApagarArquivo || ApagarConversas ? Idioma.T("Excluir")
        : Idioma.T("Tirar da lista");
    public bool PodeExcluir => !Bloqueado && !Excluindo && !(ApagarPasta && (Diagnosticando || Dados is { } d && ExclusaoDeProjeto.Impede(d)));

    async Task DiagnosticarAsync()
    {
        try
        {
            Dados = await ExclusaoDeProjeto.DiagnosticarAsync(Principal);
        }
        finally
        {
            Diagnosticando = false;
        }
    }

    [RelayCommand]
    void Cancelar()
    {
        if (!Excluindo) _janela.Exclusao = null;
    }

    [RelayCommand]
    async Task Excluir()
    {
        if (!PodeExcluir) return;
        Excluindo = true;
        Erro = null;
        try
        {
            if (ApagarConversas && TemConversas)
            {
                await EsquecerPontosAsync(ExclusaoDeProjeto.PastaDasConversas(Principal));
                await Lixeira(ExclusaoDeProjeto.PastaDasConversas(Principal));
                if (!JanelaVM.Demo && !JanelaVM.Isolada) Core.Historico.ConversasDosAgentes.RemoverDaPasta(Principal);
            }
            if (ApagarPasta)
            {
                if (EhWorktree && Dados?.RepositorioPrincipal is { } repositorio)
                {
                    var r = await Repositorio.RemoverWorktreeAsync(repositorio, Principal, Dados.Branch, ApagarBranch);
                    if (!r.Ok)
                    {
                        Erro = r.Mensagem;
                        return;
                    }
                }
                else await Lixeira(Principal);
                if (Directory.Exists(Principal))
                {
                    Erro = Idioma.T("A pasta continua no disco. Algum programa pode estar usando um arquivo dela.");
                    return;
                }
            }
            if (ApagarArquivo && Arquivo is { } arquivo) await Lixeira(arquivo);
            _janela.ConcluirExclusao(Ambiente, ApagarConversas && TemConversas);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException)
        {
            Erro = e is OperationCanceledException ? Idioma.T("Exclusão cancelada.") : Idioma.F("Não deu para excluir: {0}", e.Message);
        }
        finally
        {
            Excluindo = false;
        }
    }

    async Task EsquecerPontosAsync(string conversas)
    {
        var pasta = Principal;
        if (PontosDeRestauracao.Raiz(pasta) is null || !Directory.Exists(conversas)) return;
        await Task.Run(() => PontosDeRestauracao.ApagarSessoesAsync(pasta,
            Directory.EnumerateFiles(conversas, "*.jsonl").Select(Path.GetFileNameWithoutExtension).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase)));
    }

    static Task Lixeira(string caminho)
    {
        var fim = new TaskCompletionSource();
        var linha = new Thread(() =>
        {
            try
            {
                OperacoesArquivo.ParaLixeira(caminho);
                fim.SetResult();
            }
            catch (Exception e)
            {
                fim.SetException(e);
            }
        })
        { IsBackground = true };
        if (OperatingSystem.IsWindows()) linha.SetApartmentState(ApartmentState.STA);
        linha.Start();
        return fim.Task;
    }
}
