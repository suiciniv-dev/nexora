using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ConfiguracoesVM : ObservableObject
{
    static readonly string[] CodigosDosIdiomas = ["auto", "pt", "en"];

    readonly JanelaVM _janela;

    public ConfiguracoesVM(JanelaVM janela)
    {
        _janela = janela;
        IdiomaIndice = Math.Max(0, Array.IndexOf(CodigosDosIdiomas, App.Config.Idioma));
        ModoIndice = Math.Max(0, Array.IndexOf(TemasVM.Modos, App.Config.ModoFundo));
        Hibernar = App.Config.HibernarSessoes;
        VigiaChecksIndice = Math.Max(0, Array.IndexOf(CodigosDeChecks, App.Config.VigiaChecks));
        VigiaConflitosIndice = Math.Max(0, Array.IndexOf(CodigosDeChecks, App.Config.VigiaConflitos));
        VigiaRevisoesIndice = Math.Max(0, Array.IndexOf(CodigosDeRevisoes, App.Config.VigiaRevisoes));
        HubMcp = App.Config.HubMcp;
        McpOcioso = App.Config.McpOciosoMinutos;
        AbrirPeloExe = App.Config.AbrirPeloExe;
        RelatorioPorEmail = App.Config.RelatorioPorEmail;
        NoticiasNoDashboard = App.Config.NoticiasNoDashboard;
        AnimacoesIndice = Math.Max(0, Array.IndexOf(Aparencia.ModosDeAnimacao, Aparencia.ModoDeAnimacao));
        SincronizarZoom();
        Aparencia.Mudou += AoMudarAparencia;
        App.Segredos.Cofre.Mudou += () => Avalonia.Threading.Dispatcher.UIThread.Post(RecarregarSegredos);
        Recarregar();
    }

    public const string SecaoAparencia = "aparencia";

    [ObservableProperty]
    public partial string? SecaoPedida { get; set; }

    public void IrPara(string secao)
    {
        SecaoPedida = null;
        SecaoPedida = secao;
    }

    public TemasVM Temas => _janela.GaleriaDeTemas;

    public MaquinasNaNuvemVM Nuvem { get; } = new(App.Nuvem);

    static readonly double[] Zooms = [0.9, 1, 1.1, 1.2, 1.3, 1.4, 1.5];

    public IReadOnlyList<string> NomesDosZooms { get; } = [.. Zooms.Select(z => z == 1 ? Idioma.T("100% (normal)") : $"{z * 100:0}%")];

    [ObservableProperty]
    public partial int ZoomIndice { get; set; }

    bool _sincronizando;

    void SincronizarZoom()
    {
        _sincronizando = true;
        ZoomIndice = Array.IndexOf(Zooms, Zooms.MinBy(z => Math.Abs(z - Aparencia.Zoom)));
        _sincronizando = false;
    }

    partial void OnZoomIndiceChanged(int value)
    {
        if (_sincronizando || value < 0 || value >= Zooms.Length) return;
        _janela.MudarZoom(Zooms[value]);
    }

    public string DicaZoom => Idioma.T(OperatingSystem.IsMacOS()
        ? "Aumenta tudo: textos, botões e espaços. Cmd + e Cmd − também mudam o tamanho."
        : "Aumenta tudo: textos, botões e espaços. Ctrl + e Ctrl − também mudam o tamanho.");

    public IReadOnlyList<string> NomesDasAnimacoes { get; } =
        [Idioma.T(OperatingSystem.IsMacOS() ? "Seguir o macOS" : "Seguir o Windows"), Idioma.T("Sempre ligadas"), Idioma.T("Reduzidas")];

    [ObservableProperty]
    public partial int AnimacoesIndice { get; set; }

    partial void OnAnimacoesIndiceChanged(int value)
    {
        if (value < 0 || value >= Aparencia.ModosDeAnimacao.Length || Aparencia.ModosDeAnimacao[value] == Aparencia.ModoDeAnimacao) return;
        _janela.MudarAnimacoes(Aparencia.ModosDeAnimacao[value]);
    }

    public string DicaAnimacoes => Aparencia.AnimacoesReduzidas
        ? Idioma.T("Agora reduzidas: sem transições, pulsos nem fundo em movimento.")
        : Idioma.T("Agora ligadas. Reduzidas tira as transições, os pulsos e o movimento do fundo.");

    void AoMudarAparencia()
    {
        SincronizarZoom();
        OnPropertyChanged(nameof(DicaAnimacoes));
    }

    IReadOnlyList<string>? _fontes;

    public IReadOnlyList<string> NomesDasFontes => _fontes ??=
    [
        Idioma.T(OperatingSystem.IsWindows() ? "Do sistema (Segoe UI)" : "Do sistema"),
        Nexora.Temas.Catalogo.FonteEmbutida,
        .. Avalonia.Media.FontManager.Current.SystemFonts.Select(f => f.Name).Where(n => !n.StartsWith('.') && n != Nexora.Temas.Catalogo.FonteEmbutida).Distinct().Order(),
    ];

    public int FonteIndice
    {
        get => App.Config.Fonte is Aparencia.FonteDoSistema or "" ? 0 : Math.Max(0, IndiceDaFonte(App.Config.Fonte));
        set
        {
            if (value < 0 || value >= NomesDasFontes.Count || value == FonteIndice) return;
            _janela.MudarFonte(value == 0 ? Aparencia.FonteDoSistema : NomesDasFontes[value]);
            OnPropertyChanged();
        }
    }

    int IndiceDaFonte(string nome)
    {
        for (var i = 1; i < NomesDasFontes.Count; i++)
            if (string.Equals(NomesDasFontes[i], nome, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    public IReadOnlyList<string> NomesDosIdiomas { get; } = [Idioma.T("Automático"), "Português", "English"];
    public IReadOnlyList<string> NomesDosModos { get; } = [Idioma.T("Vidro, com animação nos temas animados"), Idioma.T("Vidro em todos (animação em pausa)"), Idioma.T("Sólido e leve (sem transparência)")];
    public ObservableCollection<RaizVM> Pastas { get; } = [];
    public bool NoWindows => OperatingSystem.IsWindows();
    public string Versao => Idioma.F("Nexora {0}", Atualizacoes.Atual.ToString(3));
    IReadOnlyList<Core.Provedores.DescricaoAgente> _opcoesDeAgente = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> NomesDosAgentes { get; set; } = [];

    public string EstadoDosAgentes => ChatVM.AgentesInstalados.Count == 0
        ? Idioma.T("Nenhum agente encontrado neste computador.")
        : Idioma.F("Instalados: {0}", string.Join(", ", ChatVM.AgentesInstalados.Select(a => a.Nome)));

    [ObservableProperty]
    public partial int AgenteIndice { get; set; }

    partial void OnAgenteIndiceChanged(int value)
    {
        if (value < 0 || value >= _opcoesDeAgente.Count) return;
        var agente = _opcoesDeAgente[value].Id;
        if (App.Config.AgentePadrao == agente) return;
        App.Config.AgentePadrao = agente;
        Salvar();
    }

    void MontarAgentes()
    {
        var padrao = Core.Claude.Agentes.Normalizar(App.Config.AgentePadrao);
        var lista = ChatVM.AgentesInstalados.ToList();
        if (!lista.Any(a => a.Id == padrao)) lista.Insert(0, Core.Provedores.CatalogoAgentes.De(padrao));
        _opcoesDeAgente = lista;
        NomesDosAgentes = [.. lista.Select(a => a.Nome)];
        AgenteIndice = lista.FindIndex(a => a.Id == padrao);
    }

    [RelayCommand]
    void VerAgentes() => _janela.AbrirEscolhaDeAgenteCommand.Execute(null);

    [ObservableProperty]
    public partial int IdiomaIndice { get; set; }

    [ObservableProperty]
    public partial int ModoIndice { get; set; }

    [ObservableProperty]
    public partial bool Hibernar { get; set; }

    [ObservableProperty]
    public partial bool HubMcp { get; set; }

    [ObservableProperty]
    public partial decimal McpOcioso { get; set; }

    [ObservableProperty]
    public partial bool AbrirPeloExe { get; set; }

    [ObservableProperty]
    public partial bool RelatorioPorEmail { get; set; }

    [ObservableProperty]
    public partial bool NoticiasNoDashboard { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAviso))]
    public partial string? Aviso { get; set; }

    public bool TemAviso => !string.IsNullOrEmpty(Aviso);

    public void Recarregar()
    {
        Pastas.Clear();
        foreach (var pasta in App.Config.Raizes) Pastas.Add(new RaizVM(pasta, r => RemoverPasta(r.Pasta)));
        Temas.Recarregar();
        MontarAgentes();
        OnPropertyChanged(nameof(EstadoDosAgentes));
        RecarregarSegredos();
    }

    public ObservableCollection<SegredoGuardadoVM> Segredos { get; } = [];
    public bool TemSegredos => Segredos.Count > 0;

    public string TextoSegredos => App.Segredos.Cofre.GuardaVariaveis
        ? Idioma.F("Os segredos que os agentes pediram pelo formulário do Nexora. O valor nunca aparece aqui: as variáveis ficam no {0} e os .env ficam no projeto.", OperatingSystem.IsMacOS() ? Idioma.T("Porta-chaves") : Idioma.T("cofre do Windows (DPAPI)"))
        : Idioma.T("Os segredos que os agentes pediram pelo formulário do Nexora. O valor nunca aparece aqui.");

    public void RecarregarSegredos()
    {
        Segredos.Clear();
        foreach (var s in App.Segredos.Cofre.Lista()) Segredos.Add(new SegredoGuardadoVM(s, ApagarSegredo));
        OnPropertyChanged(nameof(TemSegredos));
    }

    void ApagarSegredo(SegredoGuardadoVM item)
    {
        try
        {
            App.Segredos.Cofre.Apagar(item.Dados.Id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Aviso = Idioma.F("Não deu para apagar {0}: {1}", item.Nome, ex.Message);
        }
        RecarregarSegredos();
    }

    public void AdicionarPasta(string pasta)
    {
        _janela.AdicionarRaiz(pasta);
        Recarregar();
    }

    void RemoverPasta(string pasta)
    {
        _janela.RemoverRaiz(pasta);
        Recarregar();
    }

    static void Salvar()
    {
        if (!JanelaVM.Isolada) App.Config.Salvar();
    }

    partial void OnIdiomaIndiceChanged(int value)
    {
        if (value < 0 || value >= CodigosDosIdiomas.Length || CodigosDosIdiomas[value] == App.Config.Idioma) return;
        App.Config.Idioma = CodigosDosIdiomas[value];
        Salvar();
        const string pt = "Reinicie o Nexora para trocar o idioma.";
        var en = Traducao.Ingles.Pares.First(p => p.Pt == pt).En;
        Aviso = Idioma.EmIngles ? $"{en}  ·  {pt}" : $"{pt}  ·  {en}";
    }

    partial void OnModoIndiceChanged(int value)
    {
        if (value < 0 || value >= TemasVM.Modos.Length || TemasVM.Modos[value] == App.Config.ModoFundo) return;
        _janela.MudarModoFundo(TemasVM.Modos[value]);
    }

    partial void OnHibernarChanged(bool value)
    {
        App.Config.HibernarSessoes = value;
        Salvar();
    }

    static readonly string[] CodigosDeModeloDeSubagente =
        [VagasDeSubagentes.Perguntar, VagasDeSubagentes.Haiku, VagasDeSubagentes.Sonnet, VagasDeSubagentes.Opus, VagasDeSubagentes.Conversa];

    public IReadOnlyList<string> NomesDosModelosDeSubagente { get; } =
        [Idioma.T("Perguntar a cada subagent"), "Haiku", Idioma.T("Sonnet (recomendado)"), "Opus", Idioma.T("Igual ao da conversa")];

    public int ModeloDeSubagenteIndice
    {
        get => Math.Max(0, Array.IndexOf(CodigosDeModeloDeSubagente, App.Config.ModeloDosSubagentes));
        set
        {
            if (value < 0 || value >= CodigosDeModeloDeSubagente.Length || CodigosDeModeloDeSubagente[value] == App.Config.ModeloDosSubagentes) return;
            App.Config.ModeloDosSubagentes = CodigosDeModeloDeSubagente[value];
            Salvar();
            OnPropertyChanged();
        }
    }

    static readonly int[] LimitesDeSubagentes = [1, 2, 3, 4, 6, 0];

    public IReadOnlyList<string> NomesDosLimitesDeSubagentes { get; } =
        [Idioma.T("1 por vez"), Idioma.T("2 (recomendado)"), "3", "4", "6", Idioma.T("Sem limite")];

    public int LimiteDeSubagentesIndice
    {
        get => Array.IndexOf(LimitesDeSubagentes, App.Config.SubagentesAoMesmoTempo) is >= 0 and var i ? i : 1;
        set
        {
            if (value < 0 || value >= LimitesDeSubagentes.Length || LimitesDeSubagentes[value] == App.Config.SubagentesAoMesmoTempo) return;
            App.Config.SubagentesAoMesmoTempo = LimitesDeSubagentes[value];
            Salvar();
            VagasDeSubagentes.Andar();
            OnPropertyChanged();
        }
    }

    static readonly string[] CodigosDeChecks = [Core.Git.VigiaDePr.Desligado, Core.Git.VigiaDePr.Avisar, Core.Git.VigiaDePr.Agir];
    static readonly string[] CodigosDeRevisoes = [Core.Git.VigiaDePr.Desligado, Core.Git.VigiaDePr.Avisar, Core.Git.VigiaDePr.Agir, Core.Git.VigiaDePr.AgirEResponder];

    public IReadOnlyList<string> OpcoesDeChecks { get; } = [Idioma.T("Não fazer nada"), Idioma.T("Só avisar"), Idioma.T("Pedir ao agente para corrigir")];
    public IReadOnlyList<string> OpcoesDeConflitos { get; } = [Idioma.T("Não fazer nada"), Idioma.T("Só avisar"), Idioma.T("Pedir ao agente para resolver")];
    public IReadOnlyList<string> OpcoesDeRevisoes { get; } = [Idioma.T("Não fazer nada"), Idioma.T("Só avisar"), Idioma.T("Pedir as mudanças, sem responder no GitHub"), Idioma.T("Pedir as mudanças e responder no GitHub")];

    [ObservableProperty]
    public partial int VigiaChecksIndice { get; set; }

    [ObservableProperty]
    public partial int VigiaConflitosIndice { get; set; }

    [ObservableProperty]
    public partial int VigiaRevisoesIndice { get; set; }

    partial void OnVigiaChecksIndiceChanged(int value) => Guardar(value, CodigosDeChecks, v => App.Config.VigiaChecks = v);

    partial void OnVigiaConflitosIndiceChanged(int value) => Guardar(value, CodigosDeChecks, v => App.Config.VigiaConflitos = v);

    partial void OnVigiaRevisoesIndiceChanged(int value) => Guardar(value, CodigosDeRevisoes, v => App.Config.VigiaRevisoes = v);

    void Guardar(int indice, string[] codigos, Action<string> aplicar)
    {
        if (indice < 0 || indice >= codigos.Length) return;
        aplicar(codigos[indice]);
        Salvar();
    }

    partial void OnHubMcpChanged(bool value)
    {
        if (App.Config.HubMcp == value) return;
        App.Config.HubMcp = value;
        Salvar();
        Aviso = Idioma.T("Vale para as sessões abertas depois de reiniciar o Nexora.");
    }

    partial void OnMcpOciosoChanged(decimal value)
    {
        var minutos = (int)Math.Clamp(value, 1, 240);
        if (App.Config.McpOciosoMinutos == minutos) return;
        App.Config.McpOciosoMinutos = minutos;
        Salvar();
        Aviso = Idioma.T("Vale para as sessões abertas depois de reiniciar o Nexora.");
    }

    partial void OnAbrirPeloExeChanged(bool value)
    {
        App.Config.AbrirPeloExe = value;
        Salvar();
    }

    partial void OnRelatorioPorEmailChanged(bool value)
    {
        App.Config.RelatorioPorEmail = value;
        Salvar();
    }

    partial void OnNoticiasNoDashboardChanged(bool value)
    {
        if (App.Config.NoticiasNoDashboard == value) return;
        App.Config.NoticiasNoDashboard = value;
        Salvar();
        _janela.PaginaDashboard.AtualizarNoticias();
    }

    [RelayCommand]
    void EditarTemplates() => TemplatesDoApp.AbrirEditor();

    [RelayCommand]
    void AbrirRecursos() => _janela.MostrarRecursosCommand.Execute(null);

    [RelayCommand]
    void AbrirGuia() => _janela.AbrirBoasVindasCommand.Execute(null);

    [RelayCommand]
    void AbrirUso() => _janela.MostrarUsoCommand.Execute(null);
}
