using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ItemFerramenta : ItemChat
{
    const int MaiorResultado = 6000;
    static readonly HashSet<string> ComArquivo = ["Read", "Edit", "Write", "MultiEdit", "NotebookEdit"];
    static readonly HashSet<string> ComDiff = ["Edit", "Write", "MultiEdit"];

    readonly Action<ItemFerramenta, AcaoFerramenta>? _acao;

    public ItemFerramenta(string nome, string resumo, JsonElement entrada = default, Action<ItemFerramenta, AcaoFerramenta>? acao = null)
    {
        Nome = nome;
        Resumo = resumo;
        Entrada = entrada;
        _acao = acao;
        Caminho = Protocolo.Texto(entrada, "file_path") ?? Protocolo.Texto(entrada, "notebook_path");
    }

    public static bool EditaArquivo(string nome) => ComDiff.Contains(nome);

    public string Nome { get; }
    public string Rotulo => Resumos.NomeDaFerramenta(Nome);
    public string Resumo { get; }
    public JsonElement Entrada { get; }
    public string? Caminho { get; }
    public bool TemArquivo => _acao is not null && Caminho is not null && ComArquivo.Contains(Nome);
    public bool TemDiff => _acao is not null && Caminho is not null && ComDiff.Contains(Nome);

    [RelayCommand]
    void VerDiff() => _acao?.Invoke(this, AcaoFerramenta.Diff);

    [RelayCommand]
    void AbrirArquivo() => _acao?.Invoke(this, AcaoFerramenta.Abrir);

    [ObservableProperty]
    public partial string? Resultado { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Ok))]
    public partial bool Erro { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Ok), nameof(Rodando))]
    public partial bool Concluida { get; set; }

    [ObservableProperty]
    public partial bool Aberta { get; set; }

    public bool Ok => Concluida && !Erro;
    public bool Rodando => !Concluida;

    public void Concluir(string resultado, bool erro)
    {
        Resultado = string.IsNullOrWhiteSpace(resultado) ? Idioma.T("(sem saída)") : Resumos.Limitar(resultado.Trim(), MaiorResultado);
        Erro = erro;
        Concluida = true;
    }

    [RelayCommand]
    void Alternar() => Aberta = !Aberta;
}
