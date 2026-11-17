using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    bool _esperandoPr;
    bool _issueEmRevisao;
    bool _contando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemIssue), nameof(TextoIssue), nameof(DicaIssue))]
    public partial IssueDaSessao? Issue { get; set; }

    public bool TemIssue => Issue is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemTarefa))]
    public partial string? TarefaTexto { get; set; }

    public bool TemTarefa => TarefaTexto is not null;

    public static string? TextoDaTarefa(int sessoes, int prontas) =>
        sessoes < 2 ? null : prontas == 1 ? Idioma.F("1 de {0} pronta", sessoes) : Idioma.F("{0} de {1} prontas", prontas, sessoes);
    public string TextoIssue => Issue is { } i ? $"#{i.Numero}" : "";
    public string DicaIssue => Issue is { } i ? $"{(i.EhPr ? "PR" : "Issue")} #{i.Numero}: {i.Titulo}" : "";

    public ObservableCollection<LinhaAlteradaVM> Contagem { get; } = [];

    [ObservableProperty]
    public partial string TextoContagem { get; set; } = "";

    public bool MostraContagem => Estado == EstadoChat.Pronta && Contagem.Count > 0;
    public bool MostraBotoesDePronta => MostraAcoesRapidas || MostraContagem;

    partial void OnSessaoIdChanged(string? value)
    {
        if (value is null) return;
        LembrarOrigem(value);
        if (Issue is null && App.Config.IssuesDasSessoes.TryGetValue(value, out var salva)) Issue = salva;
        else GuardarIssue();
    }

    bool _issueEmAndamento;

    public void VincularIssue(IssueDaSessao issue)
    {
        Issue = issue;
        GuardarIssue();
    }

    void AoEnviarMensagem()
    {
        if (Issue is null || _issueEmAndamento) return;
        _issueEmAndamento = true;
        _ = MoverIssueAsync(Etapa.Andamento);
    }

    void GuardarIssue()
    {
        if (Issue is not { } issue || SessaoId is not { } id || JanelaVM.Isolada) return;
        App.Config.IssuesDasSessoes[id] = issue;
        App.Config.Salvar();
    }

    async Task MoverIssueAsync(Etapa etapa)
    {
        if (Issue is not { } issue || JanelaVM.Demo) return;
        var movida = await FluxoDaIssue.MoverAsync(Pasta, issue.Url, etapa);
        if (movida is null) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Adicionar(ItemAviso.Info(Idioma.F("A issue #{0} foi para {1}.", issue.Numero, movida))));
    }

    public void EsperarPr() => _esperandoPr = true;

    void AoEncerrarTurno(bool erro)
    {
        if (Estado == EstadoChat.Pronta)
        {
            _ = ContarAlteracoesAsync();
            _ = AtualizarRevisaoAsync();
        }
        if (!_esperandoPr || erro) return;
        _esperandoPr = false;
        _ = ProcurarPrAsync();
    }

    async Task ProcurarPrAsync()
    {
        AtualizarBranch();
        if (Branch is not { Length: > 0 } branch || !GitHub.TemPr(branch)) return;
        var pr = await GitHub.PrDoBranchAsync(Pasta, branch);
        if (pr is null) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Pr = pr;
            if (AbaPr && DetalhesNaTela) _ = PainelPr.CarregarAsync();
        });
    }

    protected override void AoAbrirPr()
    {
        _anfitriao.Mostrar(this);
        MostrarAba("pr");
    }

    protected override void AoMudarPr(EstadoPr? pr)
    {
        if (pr is not { Aberto: true } || _issueEmRevisao || Issue is null) return;
        _issueEmRevisao = true;
        _ = MoverIssueAsync(Etapa.Revisao);
    }

    public async Task ContarAlteracoesAsync()
    {
        if (_contando || JanelaVM.Demo || !Directory.Exists(Pasta)) return;
        _contando = true;
        try
        {
            var lista = await Task.Run(() => Alteracoes.ContarAsync(Pasta));
            Avalonia.Threading.Dispatcher.UIThread.Post(() => PreencherContagem(lista));
        }
        finally
        {
            _contando = false;
        }
    }

    public void PreencherContagem(IReadOnlyList<LinhasDoArquivo> lista)
    {
        Contagem.Clear();
        foreach (var l in lista.OrderByDescending(l => l.Mais + l.Menos).Take(4))
            Contagem.Add(new LinhaAlteradaVM(l.Caminho.EndsWith('/') ? Path.GetFileName(l.Caminho.TrimEnd('/')) + "/" : Path.GetFileName(l.Caminho), l.Caminho, l.Binario ? "bin" : $"+{l.Mais}", l.Binario || l.Menos == 0 ? "" : $"−{l.Menos}", l.Novo));
        var mais = lista.Sum(l => l.Mais);
        var menos = lista.Sum(l => l.Menos);
        var arquivos = lista.Count == 1 ? Idioma.T("1 arquivo") : Idioma.F("{0} arquivos", lista.Count);
        TextoContagem = lista.Count == 0 ? "" : $"{arquivos}  ·  +{mais} −{menos}";
        AvisarPronta();
    }

    void AvisarPronta()
    {
        OnPropertyChanged(nameof(MostraContagem));
        OnPropertyChanged(nameof(MostraBotoesDePronta));
    }

    [RelayCommand]
    void VerDiff()
    {
        _anfitriao.Mostrar(this);
        MostrarAba("git");
    }

    public bool PermissaoLiberavel(ItemPermissao item) => ChaveDePermissao.Liberavel(item.Pedido.Ferramenta) && !item.EhPergunta && !item.TemPlano;

    public void AplicarLiberacao(string chave)
    {
        foreach (var item in Itens.OfType<ItemPermissao>().Where(p => p.Pendente && PermissaoLiberavel(p)).ToList())
            if (ChaveDePermissao.De(item.Pedido.Ferramenta, item.Pedido.Entrada) == chave)
                Decidir(item, Decisao.Permitir, Idioma.T("Permitido em todas as sessões"));
    }
}
