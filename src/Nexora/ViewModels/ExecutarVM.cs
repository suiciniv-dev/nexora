using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Executar;
using Nexora.Core.Git;
using Nexora.Core.Nuvem;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public enum EstadoAlvo { Parado, Rodando, Pronto, Erro }

public sealed partial class ExecutarVM : ObservableObject
{
    public ExecutarVM(Local local, Action<string>? abrirNoNexora = null)
    {
        Local = local;
        AbrirNoNexora = abrirNoNexora;
        App.Nuvem.Mudou += () => Dispatcher.UIThread.Post(CarregarOndeRoda);
        _ = Recarregar();
    }

    public Action<string>? AbrirNoNexora { get; }
    public ObservableCollection<OndeExecutaVM> OpcoesOndeRoda { get; } = [];
    public bool MostraOndeRoda => OpcoesOndeRoda.Count > 1;
    public string ProjetoDaNuvem => Repositorio.PrincipalDoWorktree(Local.Pasta) ?? Local.Pasta;

    [ObservableProperty]
    public partial OndeExecutaVM? OndeRoda { get; set; }

    public MaquinaNaNuvem? MaquinaEscolhida => OndeRoda?.MaquinaId is { } id ? App.Nuvem.Maquina(id) : null;

    public void CarregarOndeRoda()
    {
        var antes = OndeRoda?.MaquinaId;
        OpcoesOndeRoda.Clear();
        OpcoesOndeRoda.Add(new OndeExecutaVM(Idioma.T("Neste PC"), null));
        if (ChatVM.PodeIrParaNuvem(ProjetoDaNuvem))
            foreach (var maquina in ChatVM.MaquinasProntas)
                OpcoesOndeRoda.Add(new OndeExecutaVM(Idioma.F("Nuvem · {0}", maquina.Nome), maquina.Id));
        OndeRoda = OpcoesOndeRoda.FirstOrDefault(o => o.MaquinaId == antes) ?? OpcoesOndeRoda[0];
        OnPropertyChanged(nameof(MostraOndeRoda));
    }

    public Local Local { get; }
    public string Titulo => Idioma.F("Executar · {0}", Local.Nome);
    public ObservableCollection<AlvoVM> Alvos { get; } = [];
    public bool Vazio => !Carregando && Alvos.Count == 0;
    public int Rodando => Alvos.Count(a => a.Ativo);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Vazio))]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemSelecionado))]
    public partial AlvoVM? Selecionado { get; set; }

    public bool TemSelecionado => Selecionado is not null;

    [RelayCommand]
    async Task Recarregar()
    {
        Carregando = true;
        CarregarOndeRoda();
        var raizes = new[] { Local.Pasta }.Concat(Local.Vinculados).ToList();
        var achados = await Task.Run(() => Core.Executar.Alvos.Descobrir(raizes));
        var antigos = Alvos.ToDictionary(a => a.Dados.Chave);
        Alvos.Clear();
        foreach (var dados in achados)
        {
            if (antigos.Remove(dados.Chave, out var existente)) existente.Dados = dados;
            else existente = new AlvoVM(dados, NomeDaRaiz(dados.Pasta, raizes), () => OnPropertyChanged(nameof(Rodando)), this);
            Alvos.Add(existente);
        }
        foreach (var ativo in antigos.Values.Where(a => a.Ativo)) Alvos.Add(ativo);
        Selecionado = Selecionado is { } s && Alvos.Contains(s) ? s : Alvos.FirstOrDefault();
        Carregando = false;
    }

    static string NomeDaRaiz(string pasta, IReadOnlyList<string> raizes) =>
        Path.GetFileName((raizes.FirstOrDefault(r => pasta.StartsWith(r, StringComparison.OrdinalIgnoreCase)) ?? pasta).TrimEnd('\\', '/'));

    public void PararTudo()
    {
        foreach (var alvo in Alvos) alvo.PararCommand.Execute(null);
    }
}

public sealed partial class AlvoVM : ObservableObject
{
    const int MaximoLinhas = 5000;

    readonly ConcurrentQueue<string> _fila = new();
    readonly DispatcherTimer _descarga;
    readonly Action _aoMudar;
    readonly ExecutarVM? _painel;
    IExecucaoDeAlvo? _execucao;
    bool _parando;

    public AlvoVM(Alvo dados, string raiz, Action aoMudar, ExecutarVM? painel = null)
    {
        _painel = painel;
        Dados = dados;
        Raiz = raiz;
        _aoMudar = aoMudar;
        Porta = (App.Config.Portas.TryGetValue(dados.Chave, out var salva) ? salva : dados.PortaSugerida)?.ToString() ?? "";
        Saida.UndoStack.SizeLimit = 0;
        _descarga = new DispatcherTimer(TimeSpan.FromMilliseconds(120), DispatcherPriority.Background, (_, _) => Descarregar());
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Nome), nameof(Detalhe), nameof(TemPorta))]
    public partial Alvo Dados { get; set; }

    public string Raiz { get; }
    public string Nome => Dados.Nome;
    public string Detalhe => $"{Raiz}  ·  {Dados.Detalhe}";
    public bool TemPorta => Dados.Porta is not null || Dados.Comando != "dotnet";
    public TextDocument Saida { get; } = new();

    [ObservableProperty]
    public partial string Porta { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Ativo), nameof(Parado), nameof(EstaRodando), nameof(EstaPronto), nameof(TemErro), nameof(EstadoTexto))]
    [NotifyCanExecuteChangedFor(nameof(ExecutarCommand), nameof(PararCommand), nameof(ReiniciarCommand))]
    public partial EstadoAlvo Estado { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemEndereco), nameof(PodeAbrirNoNexora))]
    public partial string? Endereco { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAviso))]
    public partial string? Aviso { get; set; }

    public bool Ativo => Estado is EstadoAlvo.Rodando or EstadoAlvo.Pronto;
    public bool Parado => !Ativo;
    public bool EstaRodando => Estado == EstadoAlvo.Rodando;
    public bool EstaPronto => Estado == EstadoAlvo.Pronto;
    public bool TemErro => Estado == EstadoAlvo.Erro;
    public bool TemEndereco => Endereco is not null;
    public bool TemAviso => Aviso is not null;
    public bool PodeAbrirNoNexora => TemEndereco && _painel?.AbrirNoNexora is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NaNuvem), nameof(EstadoTexto))]
    public partial string? MaquinaDaExecucao { get; set; }

    public bool NaNuvem => MaquinaDaExecucao is not null;

    public string EstadoTexto => (Estado switch
    {
        EstadoAlvo.Rodando => Idioma.T("subindo"),
        EstadoAlvo.Pronto => Idioma.T("no ar"),
        EstadoAlvo.Erro => Idioma.T("parou com erro"),
        _ => Idioma.T("parado"),
    }) + (Ativo && MaquinaDaExecucao is { } nome ? "  ·  " + Idioma.F("na nuvem, em {0}", nome) : "");

    int? PortaEscolhida => int.TryParse(Porta.Trim(), out var p) && p is > 0 and < 65536 ? p : null;

    bool PodeExecutar() => !Ativo;

    [RelayCommand(CanExecute = nameof(PodeExecutar))]
    void Executar()
    {
        Aviso = null;
        var porta = PortaEscolhida;
        if (_painel?.MaquinaEscolhida is { } maquina)
        {
            ExecutarNaNuvem(maquina, porta);
            return;
        }
        MaquinaDaExecucao = null;
        if ((porta ?? Dados.Porta) is { } usada && Execucao.PortaOcupada(usada))
        {
            Aviso = Idioma.F("A porta {0} já está em uso. Pare o que está nela ou escolha outra.", usada);
            return;
        }
        Lembrar(porta);
        Saida.Text = "";
        Escrever($"> {Dados.Comando} {string.Join(' ', Dados.ArgumentosCom(porta))}");
        Endereco = null;
        _parando = false;
        var execucao = new Execucao(Dados, porta, App.Segredos.Cofre.VariaveisDe(Dados.Pasta));
        execucao.Linha += (linha, _) => Escrever(linha);
        execucao.Ouvindo += url => Dispatcher.UIThread.Post(() =>
        {
            Endereco ??= Dados.EnderecoCom(porta, url);
            if (_execucao == execucao) Estado = EstadoAlvo.Pronto;
        });
        execucao.Terminou += codigo => Dispatcher.UIThread.Post(() => AoTerminar(execucao, codigo));
        try
        {
            execucao.Iniciar();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Aviso = ex.Message;
            Estado = EstadoAlvo.Erro;
            return;
        }
        _execucao = execucao;
        Estado = EstadoAlvo.Rodando;
        _aoMudar();
    }

    void ExecutarNaNuvem(MaquinaNaNuvem maquina, int? porta)
    {
        if (maquina.Home is not { Length: > 0 } home)
        {
            Aviso = Idioma.F("Teste a conexão com {0} em Configurações › Máquinas na nuvem antes de usar.", maquina.Nome);
            return;
        }
        var painel = _painel!;
        Lembrar(porta);
        Saida.Text = "";
        Escrever(Idioma.F("> {0} {1}   (na nuvem, em {2})", Dados.Comando, string.Join(' ', Dados.ArgumentosCom(porta)), maquina.Nome));
        Endereco = null;
        _parando = false;
        var remota = CodigoNaNuvem.PastaRemota(home, painel.ProjetoDaNuvem, painel.Local.Branch ?? "main");
        var execucao = new ExecucaoNaNuvem(App.Nuvem, maquina, Dados, porta, painel.Local.Pasta, remota, App.Segredos.Cofre.VariaveisDe(Dados.Pasta));
        execucao.Linha += (linha, _) => Escrever(linha);
        execucao.Mudou += m =>
        {
            if (m.Estado is EstadoDaNuvem.Desconectado) Escrever("— " + (m.Detalhe ?? Idioma.T("a conexão com a máquina caiu; tentando de novo…")));
            else if (m.Estado is EstadoDaNuvem.Sincronizando && m.Detalhe is { } detalhe) Escrever("— " + detalhe);
        };
        execucao.Ouvindo += url => Dispatcher.UIThread.Post(() =>
        {
            Endereco ??= url;
            if (_execucao == execucao) Estado = EstadoAlvo.Pronto;
        });
        execucao.Terminou += codigo => Dispatcher.UIThread.Post(() => AoTerminar(execucao, codigo));
        try
        {
            execucao.Iniciar();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            Aviso = ex.Message;
            Estado = EstadoAlvo.Erro;
            return;
        }
        _execucao = execucao;
        MaquinaDaExecucao = maquina.Nome;
        Estado = EstadoAlvo.Rodando;
        _aoMudar();
    }

    void Lembrar(int? porta)
    {
        if (porta is { } p && p != Dados.PortaSugerida) App.Config.Portas[Dados.Chave] = p;
        else App.Config.Portas.Remove(Dados.Chave);
        if (!JanelaVM.Isolada) App.Config.Salvar();
    }

    void AoTerminar(IExecucaoDeAlvo execucao, int codigo)
    {
        if (_execucao != execucao) return;
        Descarregar();
        Escrever(_parando ? Idioma.T("— parado.") : Idioma.F("— terminou com código {0}.", codigo));
        Estado = _parando || codigo == 0 ? EstadoAlvo.Parado : EstadoAlvo.Erro;
        Endereco = null;
        _execucao = null;
        MaquinaDaExecucao = null;
        execucao.Dispose();
        _aoMudar();
    }

    [RelayCommand(CanExecute = nameof(Ativo))]
    void Parar()
    {
        if (_execucao is not { } execucao) return;
        _parando = true;
        execucao.Parar();
    }

    [RelayCommand(CanExecute = nameof(Ativo))]
    async Task Reiniciar()
    {
        if (_execucao is not { } execucao) return;
        _parando = true;
        execucao.Parar();
        while (Ativo) await Task.Delay(100);
        Executar();
    }

    [RelayCommand]
    void Abrir()
    {
        if (Endereco is not { } url) return;
        try
        {
            Sistema.Abrir(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Aviso = ex.Message;
        }
    }

    [RelayCommand]
    void AbrirAqui()
    {
        if (Endereco is { } url) _painel?.AbrirNoNexora?.Invoke(url);
    }

    [RelayCommand]
    void Limpar() => Saida.Text = "";

    void Escrever(string linha)
    {
        _fila.Enqueue(linha);
        if (Dispatcher.UIThread.CheckAccess())
        {
            if (!_descarga.IsEnabled) _descarga.Start();
        }
        else
            Dispatcher.UIThread.Post(() =>
            {
                if (!_descarga.IsEnabled) _descarga.Start();
            });
    }

    void Descarregar()
    {
        _descarga.Stop();
        if (_fila.IsEmpty) return;
        var bloco = new System.Text.StringBuilder();
        while (_fila.TryDequeue(out var linha)) bloco.Append(linha).Append('\n');
        Saida.Insert(Saida.TextLength, bloco.ToString());
        if (Saida.LineCount > MaximoLinhas) Saida.Remove(0, Saida.GetLineByNumber(Saida.LineCount - MaximoLinhas + 1).Offset);
    }
}

public sealed record OndeExecutaVM(string Nome, string? MaquinaId)
{
    public override string ToString() => Nome;
}
