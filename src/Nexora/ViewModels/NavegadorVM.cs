using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Navegador;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public interface INavegadorNativo
{
    void Navegar(string url);
    void Voltar();
    void Avancar();
    void Recarregar();
    Task DesignAsync(bool ligado);
    Task RestaurarAsync(IReadOnlyList<Anotacao> anotacoes);
    Task RemoverAsync(int id);
    Task LimparAsync();
    Task<byte[]?> CapturarAsync();
}

public sealed record ServidorNoAr(string Nome, string Url, string Projeto, bool NoAr)
{
    public string Endereco => Enderecos.Curto(Url);
    public string Detalhe => NoAr ? Idioma.F("{0} · no ar", Projeto) : Idioma.F("{0} · parado", Projeto);
}

public sealed partial class AnotacaoVM(Anotacao dados, Action<AnotacaoVM> remover) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Comentario), nameof(Resumo), nameof(Seletor))]
    public partial Anotacao Dados { get; set; } = dados;

    [ObservableProperty]
    public partial int Numero { get; set; }

    public int Id => Dados.Id;
    public string Comentario => Dados.Comentario;
    public string Resumo => Dados.Resumo;
    public string Seletor => Dados.Seletor;

    [RelayCommand]
    void Remover() => remover(this);
}

public sealed partial class NavegadorVM : ObservableObject
{
    public const int MaximoLembrados = 200;

    readonly Func<string?> _sessao;
    readonly Func<string> _pasta;
    readonly Func<IReadOnlyList<ServidorNoAr>> _servidores;
    readonly Action<string, byte[]?> _mandar;
    readonly Action? _abrirExecutar;
    INavegadorNativo? _nativo;
    string? _pendente;

    public NavegadorVM(Func<string?> sessao, Func<string> pasta, Func<IReadOnlyList<ServidorNoAr>> servidores, Action<string, byte[]?> mandar, Action? abrirExecutar = null)
    {
        _sessao = sessao;
        _pasta = pasta;
        _servidores = servidores;
        _mandar = mandar;
        _abrirExecutar = abrirExecutar;
        Anotacoes.CollectionChanged += (_, _) =>
        {
            for (var i = 0; i < Anotacoes.Count; i++) Anotacoes[i].Numero = i + 1;
            OnPropertyChanged(nameof(TemAnotacoes));
            OnPropertyChanged(nameof(TextoAnotacoes));
            OnPropertyChanged(nameof(MostraBarraDesign));
            MandarParaAgenteCommand.NotifyCanExecuteChanged();
        };
        Endereco = _pendente = Lembrado();
        AtualizarServidores();
    }

#if MAC
    public static bool Disponivel => false;
#else
    public static bool Disponivel => OperatingSystem.IsWindows();
#endif

    public static Action<NavegadorVM>? Flutuar { get; set; }
    public static Action<string>? AbrirFora { get; set; }

    public ObservableCollection<AnotacaoVM> Anotacoes { get; } = [];
    public ObservableCollection<ServidorNoAr> Servidores { get; } = [];
    public bool TemAnotacoes => Anotacoes.Count > 0;
    public bool TemServidores => Servidores.Count > 0;
    public bool MostraBarraDesign => ModoDesign || TemAnotacoes;
    public string TextoAnotacoes => Anotacoes.Count == 1 ? Idioma.T("1 anotação") : Idioma.F("{0} anotações", Anotacoes.Count);

    [ObservableProperty]
    public partial string? Endereco { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Vazio), nameof(MostraPagina), nameof(EnderecoCurto))]
    public partial string? Atual { get; set; }

    [ObservableProperty]
    public partial string? Titulo { get; set; }

    [ObservableProperty]
    public partial bool PodeVoltar { get; set; }

    [ObservableProperty]
    public partial bool PodeAvancar { get; set; }

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemFalha), nameof(Vazio), nameof(MostraPagina), nameof(TituloFalha), nameof(DicaFalha), nameof(FalhaDeServidor))]
    public partial FalhaDeNavegacao? Falha { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraBarraDesign))]
    public partial bool ModoDesign { get; set; }

    [ObservableProperty]
    public partial bool Destacado { get; set; }

    [ObservableProperty]
    public partial bool MenuServidoresAberto { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAviso))]
    public partial string? Aviso { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MandarParaAgenteCommand))]
    public partial bool Mandando { get; set; }

    public bool TemFalha => Falha is not null;
    public bool TemAviso => Aviso is not null;
    public string TituloFalha => Falha?.Titulo ?? "";
    public string DicaFalha => Falha?.Dica ?? "";
    public bool FalhaDeServidor => Falha is { ServidorParado: true };
    public bool Vazio => Atual is null && _pendente is null && Falha is null;
    public bool MostraPagina => !Vazio && Falha is null;
    public string EnderecoCurto => Enderecos.Curto(Atual);

    public INavegadorNativo? Nativo
    {
        get => _nativo;
        set
        {
            if (_nativo == value) return;
            _nativo = value;
            if (value is null) return;
            var destino = _pendente ?? Atual;
            if (destino is not null && Falha is null)
            {
                _pendente = destino;
                Carregando = true;
                value.Navegar(destino);
            }
        }
    }

    public void Soltar(INavegadorNativo nativo)
    {
        if (_nativo != nativo) return;
        _nativo = null;
        if (ModoDesign) ModoDesign = false;
    }

    string? Chave() => _sessao() is { Length: > 0 } id ? id : null;
    string ChaveDaPasta() => "pasta:" + _pasta();

    string? Lembrado()
    {
        var enderecos = App.Config.EnderecosDoNavegador;
        if (Chave() is { } id && enderecos.TryGetValue(id, out var daConversa)) return daConversa;
        return enderecos.TryGetValue(ChaveDaPasta(), out var daPasta) ? daPasta : null;
    }

    void Lembrar(string url)
    {
        var enderecos = App.Config.EnderecosDoNavegador;
        if ((Chave() is not { } atual || enderecos.GetValueOrDefault(atual) == url) && enderecos.GetValueOrDefault(ChaveDaPasta()) == url) return;
        if (Chave() is { } id) enderecos[id] = url;
        enderecos[ChaveDaPasta()] = url;
        while (enderecos.Count > MaximoLembrados) enderecos.Remove(enderecos.Keys.First());
        if (!JanelaVM.Isolada) App.Config.Salvar();
    }

    [RelayCommand]
    public void AtualizarServidores()
    {
        var achados = _servidores();
        if (achados.SequenceEqual(Servidores)) return;
        Servidores.Clear();
        foreach (var s in achados) Servidores.Add(s);
        OnPropertyChanged(nameof(TemServidores));
    }

    [RelayCommand]
    void Ir()
    {
        if (Enderecos.Normalizar(Endereco) is not { } url)
        {
            Falha = new FalhaDeNavegacao(Idioma.T("Endereço inválido"), Idioma.T("Digite um endereço como localhost:5173, 5173 ou https://site.com."), false);
            return;
        }
        Abrir(url);
    }

    public void Abrir(string url)
    {
        MenuServidoresAberto = false;
        Falha = null;
        Aviso = null;
        Endereco = url;
        _pendente = url;
        OnPropertyChanged(nameof(Vazio));
        OnPropertyChanged(nameof(MostraPagina));
        if (_nativo is null) return;
        Carregando = true;
        _nativo.Navegar(url);
    }

    [RelayCommand]
    void AbrirServidor(ServidorNoAr servidor) => Abrir(servidor.Url);

    [RelayCommand]
    void AlternarServidores()
    {
        if (!MenuServidoresAberto) AtualizarServidores();
        MenuServidoresAberto = !MenuServidoresAberto;
    }

    [RelayCommand]
    void Voltar() => _nativo?.Voltar();

    [RelayCommand]
    void Avancar() => _nativo?.Avancar();

    [RelayCommand]
    void Recarregar()
    {
        if (Falha is not null && (Atual ?? _pendente ?? Enderecos.Normalizar(Endereco)) is { } url)
        {
            Abrir(url);
            return;
        }
        Carregando = true;
        _nativo?.Recarregar();
    }

    [RelayCommand]
    void AbrirExecutar() => _abrirExecutar?.Invoke();

    [RelayCommand]
    void AbrirNoNavegador()
    {
        if ((Atual ?? Enderecos.Normalizar(Endereco)) is { } url) AbrirFora?.Invoke(url);
    }

    [RelayCommand]
    void Destacar()
    {
        if (Destacado) return;
        Destacado = true;
        Flutuar?.Invoke(this);
    }

    [RelayCommand]
    public void Encaixar() => Destacado = false;

    [RelayCommand]
    async Task AlternarDesign()
    {
        if (!MostraPagina || _nativo is null)
        {
            Aviso = Idioma.T("Abra uma página antes de anotar.");
            return;
        }
        ModoDesign = !ModoDesign;
        await _nativo.DesignAsync(ModoDesign);
    }

    [RelayCommand]
    async Task LimparAnotacoes()
    {
        Anotacoes.Clear();
        if (_nativo is not null) await _nativo.LimparAsync();
    }

    bool PodeMandar() => TemAnotacoes && !Mandando;

    [RelayCommand(CanExecute = nameof(PodeMandar))]
    async Task MandarParaAgente()
    {
        if (Anotacoes.Count == 0) return;
        Mandando = true;
        try
        {
            byte[]? print = null;
            if (_nativo is not null)
            {
                if (ModoDesign) await _nativo.DesignAsync(false);
                print = await _nativo.CapturarAsync();
            }
            var lista = Anotacoes.Select(a => a.Dados).ToList();
            _mandar(Core.Navegador.Anotacoes.Mensagem(Atual ?? Endereco ?? "", lista, print is not null), print);
            ModoDesign = false;
            Anotacoes.Clear();
            if (_nativo is not null) await _nativo.LimparAsync();
            Aviso = null;
        }
        finally
        {
            Mandando = false;
        }
    }

    public void AoComecar(string url)
    {
        Carregando = true;
        Aviso = null;
        Endereco = url;
    }

    public void AoMudar(string? url, string? titulo, bool podeVoltar, bool podeAvancar)
    {
        if (!string.IsNullOrEmpty(url) && url != "about:blank")
        {
            Atual = url;
            Endereco = url;
        }
        Titulo = titulo;
        PodeVoltar = podeVoltar;
        PodeAvancar = podeAvancar;
    }

    public void AoTerminar(string url, bool sucesso, string status, int codigoHttp)
    {
        Carregando = false;
        _pendente = null;
        if (!sucesso && status != "OperationCanceled")
        {
            Falha = ErrosDeNavegacao.Descrever(status, url, codigoHttp);
            Atual = url;
            if (ModoDesign) ModoDesign = false;
            return;
        }
        if (!sucesso) return;
        Falha = null;
        Atual = url;
        Endereco = url;
        if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) Lembrar(url);
        _ = DepoisDeCarregarAsync(url);
    }

    async Task DepoisDeCarregarAsync(string url)
    {
        if (_nativo is not { } nativo) return;
        var daPagina = Anotacoes.Where(a => a.Dados.Url.Length == 0 || Core.Navegador.Anotacoes.MesmaPagina(a.Dados.Url, url)).Select(a => a.Dados).ToList();
        if (daPagina.Count > 0) await nativo.RestaurarAsync(daPagina);
        if (ModoDesign) await nativo.DesignAsync(true);
    }

    public void AoFalharWebView(string mensagem)
    {
        Carregando = false;
        Falha = new FalhaDeNavegacao(Idioma.T("O WebView2 não abriu"), mensagem, false);
    }

    public void AoReceber(string? json)
    {
        if (string.IsNullOrEmpty(json)) return;
        JsonElement m;
        try
        {
            using var doc = JsonDocument.Parse(json);
            m = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return;
        }
        if (m.ValueKind != JsonValueKind.Object || !m.TryGetProperty("t", out var t) || t.ValueKind != JsonValueKind.String) return;
        switch (t.GetString())
        {
            case "anotacao" when Core.Navegador.Anotacoes.Ler(m) is { } anotacao:
                if (Anotacoes.FirstOrDefault(a => a.Id == anotacao.Id) is { } existente) existente.Dados = anotacao;
                else Anotacoes.Add(new AnotacaoVM(anotacao, Remover));
                break;
            case "removida" when m.TryGetProperty("id", out var id) && id.TryGetInt32(out var numero):
                if (Anotacoes.FirstOrDefault(a => a.Id == numero) is { } removida) Anotacoes.Remove(removida);
                break;
            case "sair":
                ModoDesign = false;
                break;
        }
    }

    void Remover(AnotacaoVM anotacao)
    {
        Anotacoes.Remove(anotacao);
        _ = _nativo?.RemoverAsync(anotacao.Id);
    }
}
