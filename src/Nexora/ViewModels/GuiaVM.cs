using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

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
        DashboardVM => Idioma.T("Dashboard"),
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
        DashboardVM => "dashboard",
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
