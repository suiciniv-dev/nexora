using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Segredos;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ItemSegredo : ItemChat
{
    readonly Func<ItemSegredo, string, Task<string?>> _salvar;
    readonly Action<ItemSegredo> _recusar;

    public ItemSegredo(PedidoDeSegredo pedido, Func<ItemSegredo, string, Task<string?>> salvar, Action<ItemSegredo> recusar)
    {
        Pedido = pedido;
        _salvar = salvar;
        _recusar = recusar;
        Valor = "";
    }

    public PedidoDeSegredo Pedido { get; }
    public string Nome => Pedido.Nome;
    public string Motivo => Pedido.Motivo;
    public string Titulo => Idioma.F("O agente pede um segredo: {0}", Pedido.Nome);
    public string Resumo => $"{Titulo} — {Pedido.Motivo}";

    public string Destino => Pedido.Destino == DestinoDoSegredo.ArquivoEnv
        ? Idioma.F("Vai para {0} como {1}. O arquivo está no .gitignore.", Pedido.Arquivo ?? ".env", Pedido.Nome)
        : Idioma.F("Fica guardado pelo Nexora no cofre do sistema e entra como a variável {0} nos processos deste projeto.", Pedido.Nome);

    public string IconeDestino => Pedido.Destino == DestinoDoSegredo.ArquivoEnv ? "arquivo" : "cadeado";

    public string Aviso => Idioma.T("O valor não entra na conversa nem no histórico. O agente recebe só a confirmação.");

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SalvarCommand))]
    public partial string Valor { get; set; }

    [ObservableProperty]
    public partial bool Mostrando { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pendente))]
    [NotifyCanExecuteChangedFor(nameof(SalvarCommand), nameof(RecusarCommand))]
    public partial bool Respondida { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SalvarCommand), nameof(RecusarCommand))]
    public partial bool Salvando { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemErro))]
    public partial string? Erro { get; set; }

    [ObservableProperty]
    public partial string? Resposta { get; set; }

    public bool Pendente => !Respondida;
    public bool TemErro => !string.IsNullOrWhiteSpace(Erro);

    public void Encerrar(string resposta)
    {
        Valor = "";
        Mostrando = false;
        Erro = null;
        Respondida = true;
        Resposta = resposta;
    }

    bool PodeSalvar() => Pendente && !Salvando && Valor.Length > 0;

    bool PodeRecusar() => Pendente && !Salvando;

    [RelayCommand(CanExecute = nameof(PodeSalvar))]
    async Task Salvar()
    {
        Salvando = true;
        Erro = null;
        try
        {
            var valor = Valor;
            Erro = await _salvar(this, valor);
            if (Erro is null) Encerrar(Pedido.Destino == DestinoDoSegredo.ArquivoEnv
                ? Idioma.F("Guardado em {0} como {1}", Pedido.Arquivo ?? ".env", Pedido.Nome)
                : Idioma.F("Guardado pelo Nexora como {0}", Pedido.Nome));
        }
        finally
        {
            Salvando = false;
        }
    }

    [RelayCommand(CanExecute = nameof(PodeRecusar))]
    void Recusar() => _recusar(this);

    [RelayCommand]
    void AlternarMostrar() => Mostrando = !Mostrando;
}

public sealed partial class SegredoGuardadoVM(SegredoGuardado dados, Action<SegredoGuardadoVM> apagar) : ObservableObject
{
    public SegredoGuardado Dados { get; } = dados;
    public string Nome => Dados.Nome;
    public string Projeto => Path.GetFileName(Dados.Projeto.TrimEnd('\\', '/')) is { Length: > 0 } nome ? nome : Dados.Projeto;
    public string CaminhoDoProjeto => Dados.Projeto;

    public string Onde => Dados.Destino == DestinoDoSegredo.ArquivoEnv
        ? Idioma.F("Arquivo {0}", Dados.Arquivo ?? ".env")
        : Idioma.T("Variável de ambiente (cofre do sistema)");

    public string DicaApagar => Dados.Destino == DestinoDoSegredo.ArquivoEnv
        ? Idioma.F("Tira a linha {0} do {1}", Dados.Nome, Dados.Arquivo ?? ".env")
        : Idioma.T("Apaga o valor do cofre do sistema");

    [ObservableProperty]
    public partial bool Confirmando { get; set; }

    [RelayCommand]
    void PedirApagar() => Confirmando = true;

    [RelayCommand]
    void Cancelar() => Confirmando = false;

    [RelayCommand]
    void Apagar() => apagar(this);
}
