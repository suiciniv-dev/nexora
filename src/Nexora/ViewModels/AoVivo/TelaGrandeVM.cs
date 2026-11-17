using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class TelaGrandeVM : ObservableObject
{
    readonly JanelaVM _janela;
    readonly FileiraVM[] _todas;
    readonly DispatcherTimer _relogio;

    public TelaGrandeVM(JanelaVM janela)
    {
        _janela = janela;
        _todas =
        [
            new FileiraVM(Idioma.T("Precisa de você"), "precisa", c => c.Precisa),
            new FileiraVM(Idioma.T("Trabalhando"), "trabalhando", c => c.Trabalhando && !c.Precisa),
            new FileiraVM(Idioma.T("Prontas para revisar"), "pronta", c => c.Pronta && !c.Trabalhando && !c.Precisa),
            new FileiraVM(Idioma.T("Paradas"), "parada", c => !c.Pronta && !c.Trabalhando && !c.Precisa),
        ];
        Atalhos =
        [
            new AtalhoVM("inicio", Idioma.T("Início"), Abrir),
            new AtalhoVM("mais", Idioma.T("Nova sessão"), () => janela.NovaSessaoCommand.Execute(null)),
            new AtalhoVM("painel-geral", Idioma.T("Ao vivo"), () => janela.MostrarVisaoGeralCommand.Execute(null), c => c is VisaoGeralVM),
            new AtalhoVM("conversa", Idioma.T("Conversas"), MostrarConversas, c => c is ChatVM),
            new AtalhoVM("camadas", Idioma.T("Projetos"), () => janela.MostrarProjetosCommand.Execute(null), c => c is ProjetosVM or AmbienteVM),
            new AtalhoVM("issue", Idioma.T("Issues"), () => janela.MostrarIssuesCommand.Execute(null), c => c is IssuesVM),
            new AtalhoVM("orquestra", Idioma.T("Orquestrador"), () => janela.MostrarOrquestraCommand.Execute(null), c => c is OrquestraVM),
            new AtalhoVM("relogio", Idioma.T("Agendados"), () => janela.MostrarAgendadosCommand.Execute(null), c => c is AgendadosVM),
            new AtalhoVM("grafico", Idioma.T("Uso"), () => janela.MostrarUsoCommand.Execute(null), c => c is UsoVM),
            new AtalhoVM("memoria", Idioma.T("Recursos"), () => janela.MostrarRecursosCommand.Execute(null), c => c is RecursosVM),
            new AtalhoVM("ajustes", Idioma.T("Configurações"), () => janela.MostrarConfiguracoesCommand.Execute(null), c => c is ConfiguracoesVM),
            new AtalhoVM("dashboard", Idioma.T("Dashboard"), () => janela.MostrarDashboardCommand.Execute(null), c => c is DashboardVM),
        ];
        _relogio = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, (_, _) => Tique());
        janela.VisaoGeral.Reagrupou += Montar;
        janela.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(JanelaVM.Conteudo)) MarcarPagina();
        };
    }

    public JanelaVM Janela => _janela;
    public IReadOnlyList<AtalhoVM> Atalhos { get; }
    public ObservableCollection<FileiraVM> Fileiras { get; } = [];
    public event Action? PedirFocoMenu;
    public event Action? PedirFocoPagina;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NaPagina), nameof(MostraDicaMenu))]
    public partial bool Ligada { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NaPagina), nameof(MostraDicaMenu))]
    public partial bool Aberta { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NasFileiras), nameof(MostraAcoes), nameof(MostraPedido), nameof(MostraDicaMenu))]
    public partial bool NoMenu { get; set; }

    [ObservableProperty]
    public partial int Linha { get; set; }

    [ObservableProperty]
    public partial int Atalho { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemFocado), nameof(Vazia), nameof(Pedido), nameof(MostraAcoes), nameof(MostraPedido), nameof(PodePermitir), nameof(TextoAbrir), nameof(MostraAtividade))]
    public partial CartaoVM? Focado { get; set; }

    public bool NaPagina => Ligada && !Aberta;
    public bool MostraDicaMenu => NaPagina && !NoMenu;
    public bool NasFileiras => !NoMenu;
    public bool TemFocado => Focado is not null;
    public bool Vazia => Focado is null;
    public bool MostraAcoes => NasFileiras && TemFocado;
    public PedidoFocoVM? Pedido => Focado is ChatVM { PermissaoPendente: { Pendente: true } item } chat ? new PedidoFocoVM(chat, item) : null;
    public bool MostraPedido => MostraAcoes && Pedido is not null;
    public bool MostraAtividade => Focado?.UltimaAtividade is { Length: > 0 } && Pedido is not { TemUltimaAtividade: false };
    public bool PodePermitir => Pedido is { EhPermissao: true } or { EhPergunta: true, Item.PrecisaBotaoResponder: true };
    public string TextoAbrir => Focado is SessaoForaVM ? Idioma.T("Ver conversa") : Idioma.T("Abrir conversa");
    public string Hora => DateTime.Now.ToString("HH:mm", Idioma.Cultura);
    public string Saudacao => IssuesVM.Saudacao(DateTime.Now);
    public string Resumo
    {
        get
        {
            if (Fileiras.Count == 0) return Idioma.T("Nenhuma sessão aberta");
            int precisam = _todas[0].Cartoes.Count, trabalhando = _todas[1].Cartoes.Count, prontas = _todas[2].Cartoes.Count;
            return string.Join("  ·  ", new[]
            {
                precisam == 0 ? null : precisam == 1 ? Idioma.T("1 sessão precisa de você") : Idioma.F("{0} sessões precisam de você", precisam),
                trabalhando == 0 ? null : Idioma.F("{0} trabalhando", trabalhando),
                prontas == 0 ? null : prontas == 1 ? Idioma.T("1 pronta para revisar") : Idioma.F("{0} prontas para revisar", prontas),
                precisam + trabalhando + prontas == 0 ? Idioma.T("tudo parado") : null,
            }.OfType<string>());
        }
    }
    public bool Chamando => _todas[0].Cartoes.Count > 0;

    partial void OnLigadaChanged(bool value)
    {
        if (value)
        {
            _relogio.Start();
            Tique();
            return;
        }
        _relogio.Stop();
        Aberta = false;
        NoMenu = false;
        MarcarPagina();
    }

    partial void OnAbertaChanged(bool value)
    {
        MarcarPagina();
        if (value)
        {
            _janela.VisaoGeral.Ativar(true);
            return;
        }
        _janela.VisaoGeral.Ativar(_janela.Areas.Any(a => a.Conteudo == _janela.VisaoGeral));
    }

    partial void OnFocadoChanged(CartaoVM? oldValue, CartaoVM? newValue)
    {
        if (oldValue is not null) oldValue.PropertyChanged -= AoMudarFocado;
        if (newValue is not null) newValue.PropertyChanged += AoMudarFocado;
    }

    void AoMudarFocado(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(ChatVM.PermissaoPendente) or nameof(CartaoVM.UltimaAtividade))) return;
        OnPropertyChanged(nameof(Pedido));
        OnPropertyChanged(nameof(MostraPedido));
        OnPropertyChanged(nameof(PodePermitir));
        OnPropertyChanged(nameof(MostraAtividade));
    }

    void Tique()
    {
        OnPropertyChanged(nameof(Hora));
        OnPropertyChanged(nameof(Saudacao));
    }

    void MarcarPagina()
    {
        foreach (var atalho in Atalhos)
            atalho.Ativo = Ligada && (atalho == Atalhos[0] ? Aberta : !Aberta && atalho.DaPagina(_janela.Conteudo));
    }

    int AtalhoAtivo => Math.Max(0, Atalhos.ToList().FindIndex(a => a.Ativo));

    [RelayCommand]
    public void Abrir()
    {
        Aberta = true;
        Montar();
        NoMenu = Fileiras.Count == 0;
        if (NoMenu) Atalho = 0;
        Marcar();
    }

    [RelayCommand]
    public void Fechar()
    {
        Aberta = false;
        NoMenu = false;
    }

    public void IrAoMenu()
    {
        if (!Ligada) return;
        NoMenu = true;
        Atalho = AtalhoAtivo;
        Marcar();
        PedirFocoMenu?.Invoke();
    }

    public void Voltar()
    {
        if (!Aberta) Abrir();
        else if (!NoMenu) IrAoMenu();
    }

    void Montar()
    {
        if (!Aberta) return;
        var sessoes = _janela.VisaoGeral.Sessoes;
        var ativa = Fileiras.ElementAtOrDefault(Linha);
        var vivas = _todas.Where(f => f.Atualizar(sessoes)).ToList();
        VisaoGeralVM.Encher(Fileiras, vivas, false);
        if (ativa is not null && Fileiras.IndexOf(ativa) is >= 0 and var onde) Linha = onde;
        Linha = Fileiras.Count == 0 ? 0 : Math.Clamp(Linha, 0, Fileiras.Count - 1);
        if (Fileiras.Count == 0) NoMenu = true;
        Marcar();
        OnPropertyChanged(nameof(Resumo));
        OnPropertyChanged(nameof(Chamando));
    }

    void Marcar()
    {
        for (var i = 0; i < Fileiras.Count; i++) Fileiras[i].Ativa = !NoMenu && i == Linha;
        Focado = Fileiras.ElementAtOrDefault(Linha)?.Focado;
    }

    public void Mover(int dx, int dy)
    {
        if (NoMenu)
        {
            if (dx != 0) Atalho = Math.Clamp(Atalho + Math.Sign(dx), 0, Atalhos.Count - 1);
            else if (dy > 0 && !Aberta)
            {
                NoMenu = false;
                PedirFocoPagina?.Invoke();
            }
            else if (dy > 0 && Fileiras.Count > 0) NoMenu = false;
        }
        else if (!Aberta) return;
        else if (dx != 0) Fileiras.ElementAtOrDefault(Linha)?.Ir(Fileiras[Linha].Foco + Math.Sign(dx));
        else if (dy < 0 && Linha == 0)
        {
            NoMenu = true;
            Atalho = AtalhoAtivo;
        }
        else Linha = Math.Clamp(Linha + Math.Sign(dy), 0, Math.Max(0, Fileiras.Count - 1));
        Marcar();
    }

    public void Rodar(int passo)
    {
        if (Fileiras.Count == 0 || !Aberta) return;
        Linha = NoMenu ? (passo > 0 ? 0 : Fileiras.Count - 1) : ((Linha + passo) % Fileiras.Count + Fileiras.Count) % Fileiras.Count;
        NoMenu = false;
        Marcar();
    }

    public void Selecionar(CartaoVM cartao)
    {
        if (Fileiras.FirstOrDefault(f => f.Cartoes.Contains(cartao)) is not { } fileira) return;
        if (!NoMenu && ReferenceEquals(Focado, cartao))
        {
            AbrirFocado();
            return;
        }
        NoMenu = false;
        Linha = Fileiras.IndexOf(fileira);
        fileira.Ir(fileira.Cartoes.IndexOf(cartao));
        Marcar();
    }

    [RelayCommand]
    public void Escolher(AtalhoVM atalho)
    {
        Atalho = Math.Max(0, Atalhos.ToList().IndexOf(atalho));
        if (atalho == Atalhos[0])
        {
            Abrir();
            return;
        }
        Aberta = false;
        NoMenu = false;
        atalho.Abrir();
        PedirFocoPagina?.Invoke();
    }

    public void Confirmar()
    {
        if (NoMenu) Escolher(Atalhos[Atalho]);
        else AbrirFocado();
    }

    [RelayCommand]
    public void AbrirFocado()
    {
        switch (Focado)
        {
            case ChatVM chat:
                Aberta = false;
                _janela.Navegar(chat);
                break;
            case SessaoForaVM fora:
                Aberta = false;
                fora.VerCommand.Execute(null);
                break;
        }
    }

    [RelayCommand]
    public void Permitir()
    {
        if (NoMenu || !Aberta || Pedido is not { } pedido) return;
        if (!pedido.EhPergunta) pedido.Item.PermitirCommand.Execute(null);
        else if (pedido.Item.PodeResponder) pedido.Item.ResponderCommand.Execute(null);
    }

    [RelayCommand]
    public void Negar()
    {
        if (!NoMenu && Aberta && Pedido is { EhPergunta: false } pedido) pedido.Item.NegarCommand.Execute(null);
    }

    [RelayCommand]
    public void Responder(OpcaoNumeradaVM? opcao)
    {
        if (!NoMenu && Aberta && opcao is not null) opcao.Opcao.EscolherCommand.Execute(null);
    }

    public void ResponderNumero(int numero) => Responder(Pedido?.Opcoes.FirstOrDefault(o => o.Numero == numero));

    [RelayCommand]
    void ModoFoco()
    {
        if (!_janela.Foco.Aberto) _janela.Foco.Abrir();
    }

    [RelayCommand]
    void Sair() => _janela.TelaCheia = false;

    void MostrarConversas() => _janela.LateralAberta = true;
}
