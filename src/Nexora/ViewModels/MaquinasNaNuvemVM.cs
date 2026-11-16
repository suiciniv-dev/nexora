using System.Collections.ObjectModel;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Nuvem;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class MaquinasNaNuvemVM : ObservableObject
{
    readonly ServicoDaNuvem _nuvem;

    public MaquinasNaNuvemVM(ServicoDaNuvem nuvem)
    {
        _nuvem = nuvem;
        _nuvem.Mudou += () => Dispatcher.UIThread.Post(Recarregar);
        LinhaPublica = _nuvem.ChavePublicaPronta;
        Recarregar();
    }

    public ObservableCollection<MaquinaVM> Maquinas { get; } = [];
    public bool TemMaquinas => Maquinas.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemLinhaPublica))]
    public partial string? LinhaPublica { get; set; }

    public bool TemLinhaPublica => !string.IsNullOrEmpty(LinhaPublica);

    [ObservableProperty]
    public partial bool Formulario { get; set; }

    [ObservableProperty]
    public partial string Nome { get; set; } = "";

    [ObservableProperty]
    public partial string Host { get; set; } = "";

    [ObservableProperty]
    public partial string Usuario { get; set; } = "";

    [ObservableProperty]
    public partial decimal Porta { get; set; } = 22;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAviso))]
    public partial string? Aviso { get; set; }

    public bool TemAviso => !string.IsNullOrEmpty(Aviso);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestarNovaCommand))]
    public partial bool Testando { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeSalvarToken))]
    public partial string Token { get; set; } = "";

    [ObservableProperty]
    public partial bool MostrandoToken { get; set; }

    public bool PodeSalvarToken => Token.Trim().Length > 0;
    public bool TemToken => _nuvem.TemTokenDoClaude;
    public bool GuardaToken => _nuvem.GuardaToken;

    public string TextoToken => TemToken
        ? Idioma.T("Guardado no cofre deste PC. O Nexora só o põe como variável de ambiente no processo do Claude na máquina; nada vai para disco lá.")
        : Idioma.T("Rode claude setup-token neste PC (ele abre o navegador e mostra um token de um ano) e cole aqui. Assim o Claude da máquina usa o seu plano sem você entrar nele por lá.");

    public string TextoChave => TemLinhaPublica
        ? Idioma.T("Cole esta linha no arquivo ~/.ssh/authorized_keys do usuário na máquina. A chave privada fica só neste PC, numa pasta que só o seu usuário lê.")
        : Idioma.T("O Nexora cria uma chave SSH própria (ed25519). A parte privada fica só neste PC; a linha pública vai para a máquina.");

    public void Recarregar()
    {
        Maquinas.Clear();
        foreach (var maquina in _nuvem.Maquinas.Where(m => m.Tipo != TiposDeMaquina.Teste || JanelaVM.Demo))
        {
            var vm = new MaquinaVM(maquina, this);
            Maquinas.Add(vm);
            if (vm.CriadaPeloNexora && !JanelaVM.Demo && (vm.Dados.Tipo == TiposDeMaquina.Freestyle ? _nuvem.TemChaveFreestyle : _nuvem.TemChaveInstaBox)) _ = AtualizarEstadoAsync(vm);
        }
        OnPropertyChanged(nameof(TemMaquinas));
        OnPropertyChanged(nameof(TemToken));
        OnPropertyChanged(nameof(TextoToken));
        OnPropertyChanged(nameof(TemChaveInstaBox));
        OnPropertyChanged(nameof(TemChaveFreestyle));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeSalvarChaveInstaBox))]
    public partial string ChaveInstaBox { get; set; } = "";

    [ObservableProperty]
    public partial bool MostrandoChaveInstaBox { get; set; }

    [ObservableProperty]
    public partial bool FormularioInstaBox { get; set; }

    [ObservableProperty]
    public partial string NomeInstaBox { get; set; } = "";

    [ObservableProperty]
    public partial ModeloInstaBoxVM? ModeloInstaBox { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CriarInstaBoxCommand))]
    public partial bool CriandoInstaBox { get; set; }

    public ObservableCollection<ModeloInstaBoxVM> ModelosInstaBox { get; } = [];
    public bool PodeSalvarChaveInstaBox => ChaveInstaBox.Trim().Length > 0;
    public bool TemChaveInstaBox => _nuvem.TemChaveInstaBox;

    [RelayCommand]
    async Task SalvarChaveInstaBoxAsync()
    {
        try
        {
            _nuvem.GuardarChaveInstaBox(ChaveInstaBox);
            ChaveInstaBox = "";
            MostrandoChaveInstaBox = false;
            OnPropertyChanged(nameof(TemChaveInstaBox));
            Aviso = Idioma.T("Chave do InstaBox guardada no cofre. Conferindo…");
            await CarregarModelosAsync();
            if (ModelosInstaBox.Count > 0) Aviso = Idioma.T("O InstaBox aceitou a chave. Já dá para criar máquinas.");
        }
        catch (Exception ex) when (ex is ArgumentException or PlatformNotSupportedException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Aviso = ex.Message;
        }
    }

    [RelayCommand]
    void ApagarChaveInstaBox()
    {
        _nuvem.ApagarChaveInstaBox();
        FormularioInstaBox = false;
        ModelosInstaBox.Clear();
        OnPropertyChanged(nameof(TemChaveInstaBox));
        Aviso = Idioma.T("Chave do InstaBox apagada deste PC. As máquinas continuam lá; revogue a chave no painel do InstaBox se não for mais usar.");
    }

    [RelayCommand]
    void AlternarChaveInstaBox() => MostrandoChaveInstaBox = !MostrandoChaveInstaBox;

    [RelayCommand]
    async Task NovaInstaBoxAsync()
    {
        FormularioInstaBox = true;
        Aviso = null;
        if (NomeInstaBox.Length == 0) NomeInstaBox = Idioma.T("Máquina InstaBox");
        if (ModelosInstaBox.Count == 0) await CarregarModelosAsync();
    }

    [RelayCommand]
    void CancelarInstaBox()
    {
        FormularioInstaBox = false;
        Aviso = null;
    }

    async Task CarregarModelosAsync()
    {
        try
        {
            var modelos = await Task.Run(() => _nuvem.InstaBox.Cliente.ModelosAsync());
            ModelosInstaBox.Clear();
            foreach (var modelo in modelos.Where(m => m.Criavel)) ModelosInstaBox.Add(new ModeloInstaBoxVM(modelo));
            ModeloInstaBox = ModelosInstaBox.FirstOrDefault(m => m.Dados.Padrao) ?? ModelosInstaBox.FirstOrDefault();
        }
        catch (Exception ex) when (ex is ErroDoInstaBox or HttpRequestException or TaskCanceledException)
        {
            Aviso = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(PodeCriarInstaBox))]
    async Task CriarInstaBoxAsync()
    {
        var nome = NomeInstaBox.Trim().Length > 0 ? NomeInstaBox.Trim() : Idioma.T("Máquina InstaBox");
        CriandoInstaBox = true;
        Aviso = Idioma.T("Criando a máquina no InstaBox…");
        try
        {
            var (_, relatorio) = await Task.Run(() => _nuvem.CriarNoInstaBoxAsync(nome, ModeloInstaBox?.Dados.Id, p => Dispatcher.UIThread.Post(() => Aviso = p)));
            FormularioInstaBox = false;
            NomeInstaBox = "";
            Aviso = relatorio.Conectou ? nome + ": " + Descrever(relatorio) : Idioma.F("A máquina foi criada, mas o SSH não conectou: {0}", relatorio.Erro);
        }
        catch (Exception ex) when (ex is ErroDoInstaBox or HttpRequestException or TaskCanceledException or TimeoutException or IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Aviso = ex.Message;
        }
        finally
        {
            CriandoInstaBox = false;
        }
    }

    bool PodeCriarInstaBox() => !CriandoInstaBox;

    internal async Task AtualizarEstadoAsync(MaquinaVM maquina)
    {
        try
        {
            maquina.EstadoRemoto = maquina.Dados.Tipo == TiposDeMaquina.Freestyle
                ? await Task.Run(() => _nuvem.Freestyle.EstadoAsync(maquina.Dados))
                : (await Task.Run(() => _nuvem.InstaBox.EstadoAsync(maquina.Dados))).Estado;
        }
        catch (Exception ex) when (ex is ErroDoInstaBox { Status: 404 } or ErroDaFreestyle { Status: 404 })
        {
            maquina.EstadoRemoto = "deleted";
        }
        catch (Exception ex) when (ex is ErroDoInstaBox or ErroDaFreestyle or HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            maquina.EstadoRemoto = null;
        }
    }

    internal async Task LigarOuPausarAsync(MaquinaVM maquina, bool pausar)
    {
        maquina.Ocupada = true;
        Aviso = pausar ? Idioma.F("Pausando {0}…", maquina.Nome) : Idioma.F("Ligando {0}…", maquina.Nome);
        try
        {
            maquina.EstadoRemoto = pausar ? "suspending" : "resuming";
            await Task.Run(() => pausar ? _nuvem.PausarAsync(maquina.Dados) : _nuvem.RetomarAsync(maquina.Dados));
            maquina.EstadoRemoto = pausar ? "suspended" : "running";
            Aviso = pausar
                ? Idioma.F("{0} pausou. Pausada, ela só cobra o disco; o Nexora liga de novo quando uma conversa precisar.", maquina.Nome)
                : Idioma.F("{0} está ligada.", maquina.Nome);
        }
        catch (Exception ex) when (ex is ErroDoInstaBox or ErroDaFreestyle or HttpRequestException or TaskCanceledException or TimeoutException or InvalidOperationException)
        {
            Aviso = ex.Message;
            _ = AtualizarEstadoAsync(maquina);
        }
        finally
        {
            maquina.Ocupada = false;
        }
    }

    [RelayCommand]
    void NovaMaquina()
    {
        Formulario = true;
        Aviso = null;
        if (Nome.Length == 0) Nome = Idioma.T("Minha máquina");
        _ = GerarChaveAsync();
    }

    [RelayCommand]
    void CancelarNova()
    {
        Formulario = false;
        Aviso = null;
    }

    async Task GerarChaveAsync()
    {
        if (TemLinhaPublica) return;
        try
        {
            LinhaPublica = await Task.Run(() => _nuvem.ChavePublicaAsync());
            OnPropertyChanged(nameof(TextoChave));
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            Aviso = Idioma.F("Não deu para criar a chave: {0}", ex.Message);
        }
    }

    [RelayCommand]
    async Task CopiarChaveAsync()
    {
        if (LinhaPublica is { } linha && App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } area })
        {
            await area.SetTextAsync(linha);
            Aviso = Idioma.T("Linha pública copiada. Cole no ~/.ssh/authorized_keys da máquina.");
        }
    }

    [RelayCommand(CanExecute = nameof(PodeTestar))]
    async Task TestarNovaAsync()
    {
        var host = Host.Trim();
        if (host.Contains('@') && Usuario.Trim().Length == 0)
        {
            Usuario = host[..host.IndexOf('@')];
            host = host[(host.IndexOf('@') + 1)..];
            Host = host;
        }
        if (host.Length == 0)
        {
            Aviso = Idioma.T("Falta o endereço da máquina.");
            return;
        }
        var maquina = new MaquinaNaNuvem(MaquinaNaNuvem.NovoId(), Nome.Trim().Length > 0 ? Nome.Trim() : host, TiposDeMaquina.Ssh, host, Usuario.Trim(), (int)Math.Clamp(Porta, 1, 65535));
        var relatorio = await TestarAsync(maquina);
        if (relatorio is null) return;
        if (!relatorio.Conectou)
        {
            Aviso = relatorio.Erro;
            return;
        }
        Formulario = false;
        Nome = Host = Usuario = "";
        Porta = 22;
        Aviso = Descrever(relatorio);
    }

    bool PodeTestar() => !Testando;

    internal async Task<RelatorioDaMaquina?> TestarAsync(MaquinaNaNuvem maquina)
    {
        Testando = true;
        Aviso = Idioma.F("Testando {0}…", maquina.Endereco);
        try
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            return await Task.Run(() => _nuvem.TestarAsync(maquina, limite.Token));
        }
        catch (OperationCanceledException)
        {
            Aviso = Idioma.T("A máquina demorou demais para responder.");
            return null;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            Aviso = ex.Message;
            return null;
        }
        finally
        {
            Testando = false;
        }
    }

    public static string Descrever(RelatorioDaMaquina relatorio)
    {
        string Item(string nome) => relatorio.Versao(nome) is { } v ? Idioma.F("{0}: {1}", nome, v) : Idioma.F("{0}: não tem", nome);
        var falta = new[] { "git", "claude" }.Where(f => !relatorio.Tem(f)).ToList();
        var texto = Idioma.F("Conectou. {0}. {1}.", relatorio.Sistema ?? "?", string.Join(" · ", new[] { "git", "node", "claude", "codex" }.Select(Item)));
        return falta.Count == 0 ? texto : texto + " " + Idioma.F("Instale na máquina: {0}.", string.Join(", ", falta));
    }

    internal async Task ApagarAsync(MaquinaVM maquina)
    {
        if (!maquina.CriadaPeloNexora)
        {
            _nuvem.Apagar(maquina.Dados.Id);
            Aviso = Idioma.F("{0} saiu da lista. A chave dela continua no authorized_keys da máquina até você tirar.", maquina.Nome);
            return;
        }
        maquina.Ocupada = true;
        Aviso = Idioma.F("Apagando {0} em {1}…", maquina.Nome, maquina.Provedor);
        try
        {
            await Task.Run(() => _nuvem.ApagarComAMaquinaAsync(maquina.Dados));
            Aviso = Idioma.F("{0} foi apagada em {1}, com os arquivos dela.", maquina.Nome, maquina.Provedor);
        }
        catch (Exception ex) when (ex is ErroDoInstaBox or ErroDaFreestyle or HttpRequestException or TaskCanceledException or TimeoutException or InvalidOperationException)
        {
            maquina.Ocupada = false;
            maquina.Confirmando = false;
            Aviso = ex.Message;
        }
    }

    [RelayCommand]
    void SalvarToken()
    {
        try
        {
            _nuvem.GuardarTokenDoClaude(Token);
            Token = "";
            MostrandoToken = false;
            Aviso = Idioma.T("Token do Claude guardado no cofre.");
        }
        catch (Exception ex) when (ex is ArgumentException or PlatformNotSupportedException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Aviso = ex.Message;
        }
        OnPropertyChanged(nameof(TemToken));
        OnPropertyChanged(nameof(TextoToken));
    }

    [RelayCommand]
    void ApagarToken()
    {
        _nuvem.ApagarTokenDoClaude();
        OnPropertyChanged(nameof(TemToken));
        OnPropertyChanged(nameof(TextoToken));
    }

    [RelayCommand]
    void AlternarToken() => MostrandoToken = !MostrandoToken;
}

public sealed partial class MaquinaVM(MaquinaNaNuvem dados, MaquinasNaNuvemVM pai) : ObservableObject
{
    public MaquinaNaNuvem Dados { get; } = dados;
    public string Nome => Dados.Nome;
    public string Endereco => Dados.Endereco;
    public bool Testada => Dados.Testada;
    public bool CriadaPeloNexora => Dados.Tipo is TiposDeMaquina.InstaBox or TiposDeMaquina.Freestyle;
    public string Provedor => Dados.Tipo == TiposDeMaquina.Freestyle ? "Freestyle" : "InstaBox";
    public string TextoTirar => CriadaPeloNexora ? Idioma.F("Apagar em {0}", Provedor) : Idioma.T("Tirar");
    public string DicaTirar => CriadaPeloNexora ? Idioma.F("Apagar esta máquina em {0}, com os arquivos dela", Provedor) : Idioma.T("Tirar esta máquina do Nexora");

    [ObservableProperty]
    public partial bool Ocupada { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Ligada), nameof(MostraLigar), nameof(MostraPausar), nameof(TextoEstado), nameof(TemEstado))]
    public partial string? EstadoRemoto { get; set; }

    public bool Ligada => EstadoRemoto is "running" or "starting" or "resuming" or "creating";
    public bool MostraEnergia => CriadaPeloNexora && !Confirmando;
    public bool MostraLigar => !Ligada && EstadoRemoto is not ("deleted" or "deleting" or "suspending" or "stopping");
    public bool MostraPausar => EstadoRemoto is null or "running";
    public string? TextoEstado => TextoDoEstado(EstadoRemoto);
    public bool TemEstado => TextoEstado is not null;

    public static string? TextoDoEstado(string? estado) => estado switch
    {
        null or "" => null,
        "running" => Idioma.T("ligada"),
        "starting" or "resuming" or "creating" => Idioma.T("ligando…"),
        "suspended" => Idioma.T("pausada"),
        "suspending" => Idioma.T("pausando…"),
        "stopped" => Idioma.T("desligada"),
        "stopping" => Idioma.T("desligando…"),
        "deleted" or "deleting" => Idioma.T("apagada no provedor"),
        "error" => Idioma.T("com erro"),
        _ => estado,
    };

    public string Situacao => Dados.Testada
        ? Idioma.F("Conectada em {0:g}  ·  {1}", Dados.TestadaEm, Dados.Resumo ?? "")
        : CriadaPeloNexora ? Idioma.T("Ainda não conectou. Teste a conexão.") : Idioma.T("Ainda não conectou. Cole a chave do Nexora na máquina e teste.");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraEnergia))]
    public partial bool Confirmando { get; set; }

    [RelayCommand]
    async Task TestarAsync()
    {
        if (await pai.TestarAsync(Dados) is { } relatorio)
            pai.Aviso = relatorio.Conectou ? Nome + ": " + MaquinasNaNuvemVM.Descrever(relatorio) : relatorio.Erro;
    }

    [RelayCommand]
    void PedirApagar() => Confirmando = true;

    [RelayCommand]
    void Cancelar() => Confirmando = false;

    [RelayCommand]
    Task Apagar() => pai.ApagarAsync(this);

    [RelayCommand]
    Task Pausar() => pai.LigarOuPausarAsync(this, true);

    [RelayCommand]
    Task Ligar() => pai.LigarOuPausarAsync(this, false);
}

public sealed class ModeloInstaBoxVM(ModeloInstaBox dados)
{
    public ModeloInstaBox Dados { get; } = dados;
    public string Nome => Dados.Id;
    public string Detalhe => Dados.Vcpus > 0 ? Idioma.F("{0} vCPU · {1} GB", Dados.Vcpus, Dados.MemoriaMiB / 1024) : "";
    public override string ToString() => Nome;
}
