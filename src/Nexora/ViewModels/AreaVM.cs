using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class AreaVM : ObservableObject
{
    public AreaVM(JanelaVM janela, bool principal)
    {
        Janela = janela;
        Principal = principal;
        Divisao.CollectionChanged += (_, _) => AvisarDivisao();
    }

    public const int MaxPaineis = 12;

    bool _aoLado;

    public JanelaVM Janela { get; }
    public bool Principal { get; }
    public bool Ativa { get; set; }
    public ObservableCollection<GuiaVM> Guias { get; } = [];
    public event Action? PedirFoco;
    public event Action? PedirFechar;
    public ObservableCollection<GuiaVM> Divisao { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Conteudo), nameof(Conteudos), nameof(SemConteudo), nameof(Titulo))]
    public partial GuiaVM? Selecionada { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Conteudo), nameof(Conteudos), nameof(SemConteudo), nameof(Titulo), nameof(NaPagina))]
    public partial object? Pagina { get; set; }

    public bool NaPagina => Pagina is not null;

    public static bool EhPagina(object conteudo) =>
        conteudo is VisaoGeralVM or ProjetosVM or IssuesVM or OrquestraVM or AgendadosVM or UsoVM or RecursosVM or ConfiguracoesVM or AmbienteVM or NovoAmbienteVM;

    partial void OnPaginaChanged(object? value)
    {
        foreach (var guia in Guias) guia.Avisar();
        Janela.AoSelecionar(this);
    }

    public void MostrarPagina(object pagina)
    {
        if (Achar(pagina) is { } antiga) Soltar(antiga);
        Pagina = pagina;
    }

    [ObservableProperty]
    public partial bool SplitLigado { get; set; }

    public object? Conteudo => Pagina ?? Selecionada?.Conteudo;
    public bool Dividido => Pagina is null && SplitLigado && Divisao.Count > 1;
    public bool TemSplitGuardado => Divisao.Count > 1;
    public bool PodeDividir => Divisao.Count < MaxPaineis;
    public IReadOnlyList<object> Conteudos => Pagina is { } pagina ? [pagina] : Dividido ? [.. Divisao.Select(g => g.Conteudo)] : Selecionada is { } guia ? [guia.Conteudo] : [];
    public bool SemConteudo => Pagina is null && Selecionada is null;
    public string Titulo => Pagina is { } pagina ? $"{GuiaVM.TituloDe(pagina)} · Nexora" : Selecionada is { } guia ? $"{guia.Titulo} · Nexora" : "Nexora";

    partial void OnSplitLigadoChanged(bool value) => AvisarDivisao();

    void AvisarDivisao()
    {
        OnPropertyChanged(nameof(Dividido));
        OnPropertyChanged(nameof(TemSplitGuardado));
        OnPropertyChanged(nameof(PodeDividir));
        OnPropertyChanged(nameof(Conteudos));
        foreach (var guia in Guias) guia.Avisar();
    }

    partial void OnSelecionadaChanged(GuiaVM? oldValue, GuiaVM? newValue)
    {
        if (newValue is not null) Pagina = null;
        if (newValue is not null && !Divisao.Contains(newValue))
        {
            if (_aoLado && oldValue is not null)
            {
                if (!Dividido)
                {
                    Divisao.Clear();
                    Divisao.Add(oldValue);
                }
                Acrescentar(newValue, oldValue);
                SplitLigado = true;
            }
            else SplitLigado = false;
        }
        foreach (var guia in Guias) guia.Avisar();
        Janela.AoSelecionar(this);
    }

    void Acrescentar(GuiaVM guia, GuiaVM? depoisDe)
    {
        if (!PodeDividir)
        {
            var sai = depoisDe is not null && Divisao.Contains(depoisDe) ? Divisao.IndexOf(depoisDe) : Divisao.Count - 1;
            Divisao[sai] = guia;
            return;
        }
        var i = depoisDe is null ? -1 : Divisao.IndexOf(depoisDe);
        Divisao.Insert(i < 0 ? Divisao.Count : i + 1, guia);
    }

    public void AbrirAoLado(Action abrir)
    {
        _aoLado = true;
        try { abrir(); }
        finally { _aoLado = false; }
    }

    public void Dividir(GuiaVM guia)
    {
        if (!Divisao.Contains(guia))
        {
            if (Dividido) Acrescentar(guia, Selecionada);
            else if (TemSplitGuardado) Acrescentar(guia, null);
            else
            {
                Divisao.Clear();
                if (Selecionada is { } atual && atual != guia) Divisao.Add(atual);
                Divisao.Add(guia);
            }
        }
        SplitLigado = Divisao.Count > 1;
        Selecionada = guia;
    }

    public void Desdividir(GuiaVM guia)
    {
        var indice = Divisao.IndexOf(guia);
        if (indice < 0) return;
        var estavaLigado = Dividido;
        Divisao.RemoveAt(indice);
        if (estavaLigado && Selecionada == guia && Divisao.Count > 0) Selecionada = Divisao[Math.Min(indice, Divisao.Count - 1)];
        if (Divisao.Count <= 1)
        {
            Divisao.Clear();
            SplitLigado = false;
        }
    }

    public void DefinirDivisao(IReadOnlyList<GuiaVM> guias)
    {
        Divisao.Clear();
        if (guias.Count == 0) return;
        foreach (var guia in guias.Take(MaxPaineis)) Divisao.Add(guia);
        SplitLigado = Divisao.Count > 1;
        Selecionada = guias[0];
        OnPropertyChanged(nameof(Conteudos));
    }

    public void SairDaDivisao(GuiaVM guia)
    {
        SplitLigado = false;
        Selecionada = guia;
    }

    [RelayCommand]
    public void AlternarSplit()
    {
        if (Dividido)
        {
            SplitLigado = false;
            return;
        }
        if (!TemSplitGuardado)
        {
            Janela.AbrirAtivasEmSplit();
            return;
        }
        var foco = Selecionada is { } atual && Divisao.Contains(atual) ? atual : Divisao[0];
        SplitLigado = true;
        Selecionada = foco;
        Focar();
    }

    public void FocarConteudo(object conteudo)
    {
        if (Divisao.FirstOrDefault(g => ReferenceEquals(g.Conteudo, conteudo)) is { } guia) Selecionada = guia;
    }

    public GuiaVM? Achar(object conteudo) => Guias.FirstOrDefault(g => g.Conteudo == conteudo);

    public GuiaVM Abrir(object conteudo)
    {
        var guia = Achar(conteudo) ?? Receber(new GuiaVM(conteudo, this));
        Selecionada = guia;
        return guia;
    }

    public GuiaVM Receber(GuiaVM guia, int? posicao = null)
    {
        guia.Area = this;
        Guias.Insert(Math.Clamp(posicao ?? Guias.Count, 0, Guias.Count), guia);
        guia.PropertyChanged += AoMudarGuia;
        return guia;
    }

    public void Soltar(GuiaVM guia)
    {
        var indice = Guias.IndexOf(guia);
        if (indice < 0) return;
        Desdividir(guia);
        guia.PropertyChanged -= AoMudarGuia;
        Guias.RemoveAt(indice);
        if (Selecionada == guia) Selecionada = Guias.Count == 0 ? null : Guias[Math.Min(indice, Guias.Count - 1)];
    }

    public void Mover(GuiaVM guia, int posicao)
    {
        var de = Guias.IndexOf(guia);
        if (de < 0 || posicao == de) return;
        Guias.Move(de, Math.Clamp(posicao, 0, Guias.Count - 1));
    }

    public void Focar() => PedirFoco?.Invoke();

    public void Fechar() => PedirFechar?.Invoke();

    void AoMudarGuia(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == Selecionada && e.PropertyName == nameof(GuiaVM.Titulo)) OnPropertyChanged(nameof(Titulo));
        if (e.PropertyName == nameof(GuiaVM.Conteudo) && sender is GuiaVM guia && Divisao.Contains(guia)) OnPropertyChanged(nameof(Conteudos));
    }

    [RelayCommand]
    void Proxima() => Andar(1);

    [RelayCommand]
    void Anterior() => Andar(-1);

    void Andar(int passo)
    {
        if (Guias.Count == 0) return;
        var atual = Selecionada is null ? -1 : Guias.IndexOf(Selecionada);
        Selecionada = Guias[((atual + passo) % Guias.Count + Guias.Count) % Guias.Count];
    }

    [RelayCommand]
    void FecharAtual() => Selecionada?.FecharCommand.Execute(null);

    [RelayCommand]
    void NovaSessao() => Janela.NovaSessaoEm(this);

    [RelayCommand]
    void NovaSessaoAoLado() => AbrirAoLado(() => Janela.NovaSessaoEm(this));
}

public sealed partial class GuiaVM : ObservableObject
{
    public GuiaVM(object conteudo, AreaVM area)
    {
        Conteudo = conteudo;
        Area = area;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Titulo), nameof(Icone), nameof(Chat))]
    public partial object Conteudo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NaPrincipal))]
    public partial AreaVM Area { get; set; }

    public bool Selecionada => Area.Selecionada == this && Area.Pagina is null;
    public bool NaDivisao => Area.Dividido && Area.Divisao.Contains(this);
    public bool NoSplitGuardado => Area.TemSplitGuardado && Area.Divisao.Contains(this);
    public bool PodeDividirAo => !NaDivisao;
    public bool NaPrincipal => Area.Principal;
    public ChatVM? Chat => Conteudo as ChatVM;

    public string Titulo => TituloDe(Conteudo);

    public static string TituloDe(object conteudo) => conteudo switch
    {
        ChatVM c => c.Titulo,
        VisaoGeralVM => Idioma.T("Ao vivo"),
        OrquestraVM => Idioma.T("Orquestrador"),
        AmbienteVM a => a.Nome,
        NovoAmbienteVM => Idioma.T("Novo projeto"),
        ExecutarVM e => e.Titulo,
        ProjetosVM => Idioma.T("Projetos"),
        RecursosVM => Idioma.T("Recursos"),
        UsoVM => Idioma.T("Uso"),
        IssuesVM => Idioma.T("Issues"),
        AgendadosVM => Idioma.T("Agendados"),
        ConfiguracoesVM => Idioma.T("Configurações"),
        _ => "",
    };

    public string Icone => Conteudo switch
    {
        ChatVM { EhChefe: true } => "orquestra",
        ChatVM => "conversa",
        VisaoGeralVM => "painel-geral",
        OrquestraVM => "orquestra",
        AmbienteVM => "camadas",
        ExecutarVM => "executar",
        ProjetosVM => "camadas",
        RecursosVM => "memoria",
        UsoVM => "grafico",
        IssuesVM => "issue",
        AgendadosVM => "relogio",
        ConfiguracoesVM => "ajustes",
        _ => "mais",
    };

    public string? Chave => Conteudo switch
    {
        VisaoGeralVM => "visao",
        OrquestraVM => "orquestra",
        ProjetosVM => "projetos",
        RecursosVM => "recursos",
        UsoVM => "uso",
        IssuesVM => "issues",
        AgendadosVM => "agendados",
        ConfiguracoesVM => "configuracoes",
        ExecutarVM e => $"executar|{e.Local.Pasta}",
        _ => null,
    };

    partial void OnConteudoChanged(object oldValue, object newValue)
    {
        if (oldValue is INotifyPropertyChanged antigo) antigo.PropertyChanged -= AoMudarConteudo;
        if (newValue is INotifyPropertyChanged novo) novo.PropertyChanged += AoMudarConteudo;
    }

    void AoMudarConteudo(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChatVM.Titulo) or nameof(AmbienteVM.Nome)) OnPropertyChanged(nameof(Titulo));
    }

    public void Avisar()
    {
        OnPropertyChanged(nameof(Selecionada));
        OnPropertyChanged(nameof(NaDivisao));
        OnPropertyChanged(nameof(NoSplitGuardado));
        OnPropertyChanged(nameof(PodeDividirAo));
    }

    public void Desligar()
    {
        if (Conteudo is INotifyPropertyChanged observavel) observavel.PropertyChanged -= AoMudarConteudo;
    }

    [RelayCommand]
    void Selecionar()
    {
        Area.Pagina = null;
        Area.Selecionada = this;
    }

    [RelayCommand]
    void Fechar() => Area.Janela.FecharGuia(this);

    [RelayCommand]
    void Dividir() => Area.Dividir(this);

    [RelayCommand]
    void FecharPainel() => Area.Desdividir(this);

    [RelayCommand]
    void SoEsta() => Area.SairDaDivisao(this);

    [RelayCommand]
    void NovaJanela() => Area.Janela.Destacar(this, null);

    [RelayCommand]
    void VoltarParaPrincipal() => Area.Janela.Levar(this, Area.Janela.Principal, null);
}
