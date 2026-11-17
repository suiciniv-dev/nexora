namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    public const double LarguraDetalhesNormal = 320;
    public const double LarguraDetalhesNavegador = 560;

    NavegadorVM? _navegador;
    bool _navegadorUsado;

    public NavegadorVM Navegador
    {
        get
        {
            if (_navegador is not null) return _navegador;
            _navegador = new NavegadorVM(() => SessaoId, () => Pasta, () => _anfitriao.ServidoresNoAr(LocalAtual), MandarDoNavegador, Executar);
            _navegador.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(NavegadorVM.Destacado)) AvisarNavegador();
            };
            return _navegador;
        }
    }

    public bool AbaNavegador => AbaDetalhe == "navegador";
    public NavegadorVM? NavegadorNoPainel => _navegadorUsado && _navegador is { Destacado: false } n ? n : null;
    public bool NavegadorDestacado => _navegador is { Destacado: true };
    public double LarguraDetalhes => AbaNavegador && !NavegadorDestacado ? LarguraDetalhesNavegador : LarguraDetalhesNormal;

    void AvisarNavegador()
    {
        OnPropertyChanged(nameof(AbaNavegador));
        OnPropertyChanged(nameof(NavegadorNoPainel));
        OnPropertyChanged(nameof(NavegadorDestacado));
        OnPropertyChanged(nameof(LarguraDetalhes));
    }

    void UsarNavegador()
    {
        if (!_navegadorUsado)
        {
            _ = Navegador;
            _navegadorUsado = true;
        }
        Navegador.AtualizarServidores();
        AvisarNavegador();
    }

    public void AbrirNavegador(string? url)
    {
        if (Navegador.Destacado) NavegadorVM.Flutuar?.Invoke(Navegador);
        else MostrarAba("navegador");
        if (url is not null) Navegador.Abrir(url);
    }

    void MandarDoNavegador(string texto, byte[]? print)
    {
        _anfitriao.Mostrar(this);
        if (print is not null) Anexar(print, $"pagina-anotada-{DateTime.Now:HHmmss}.png");
        Rascunho = string.IsNullOrWhiteSpace(Rascunho) ? texto : Rascunho.TrimEnd() + "\n\n" + texto;
        if (JanelaVM.Demo) return;
        EnviarCommand.Execute(null);
    }

    void EncerrarNavegador()
    {
        if (_chaveDoNavegador is { } chave) NavegadorDoApp.Servidor?.Esquecer(chave);
        _chaveDoNavegador = null;
        if (_navegador is null) return;
        _navegador.Encaixar();
        _navegador.Nativo = null;
    }
}
