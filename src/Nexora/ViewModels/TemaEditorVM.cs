using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;
using Nexora.Fundos;
using Nexora.Temas;
using TextMateSharp.Grammars;

namespace Nexora.ViewModels;

public sealed partial class TemaEditorVM : ObservableObject
{
    static readonly Dictionary<string, string> Rotulos = new()
    {
        ["Fundo"] = Idioma.T("Fundo"),
        ["Lateral"] = Idioma.T("Lateral"),
        ["Superficie"] = Idioma.T("Cartões"),
        ["SuperficieAlta"] = Idioma.T("Cartões em destaque"),
        ["Borda"] = Idioma.T("Bordas"),
        ["Texto"] = Idioma.T("Texto"),
        ["TextoSuave"] = Idioma.T("Texto suave"),
        ["TextoFraco"] = Idioma.T("Texto fraco"),
        ["Acento"] = Idioma.T("Destaque"),
        ["AcentoHover"] = Idioma.T("Destaque ao passar o mouse"),
        ["SobreAcento"] = Idioma.T("Texto sobre o destaque"),
        ["Rosa"] = Idioma.T("Rosa"),
        ["Verde"] = Idioma.T("Verde (pronta)"),
        ["Laranja"] = Idioma.T("Laranja (precisa de você)"),
        ["Vermelho"] = Idioma.T("Vermelho (erro)"),
        ["Ciano"] = Idioma.T("Ciano"),
        ["CodigoFundo"] = Idioma.T("Fundo do código"),
        ["CodigoTexto"] = Idioma.T("Código dentro do texto"),
        ["BolhaUsuario"] = Idioma.T("Sua mensagem"),
        ["Marca"] = Idioma.T("Cor do Nexora (início)"),
        ["MarcaFim"] = Idioma.T("Cor do Nexora (fim)"),
    };

    readonly TemasVM _dono;
    bool _montando = true;

    public TemaEditorVM(Tema rascunho, TemasVM dono)
    {
        Rascunho = rascunho;
        _dono = dono;
        Nome = rascunho.Nome;
        Escuro = rascunho.Escuro;
        Fonte = rascunho.Fonte;
        FonteCodigo = rascunho.FonteCodigo;
        FundoAnimado = NomeDoFundo(rascunho.FundoAnimado);
        Imagem = rascunho.Imagem;
        Escurecer = rascunho.Escurecer;
        TemaEditor = Catalogo.EditorDe(rascunho);
        foreach (var chave in Catalogo.Chaves) Cores.Add(new CorVM(chave, Rotulos[chave], rascunho.Cor(chave), Mudou));
        _montando = false;
        Mudou();
    }

    public Tema Rascunho { get; }
    public ObservableCollection<CorVM> Cores { get; } = [];
    public IReadOnlyList<string> Fontes { get; } = [Catalogo.FonteEmbutida, .. FontManager.Current.SystemFonts.Select(f => f.Name).Distinct().Order()];
    public IReadOnlyList<string> FundosAnimados { get; } = [NomeDoFundo(Papeis.Nenhum), .. Papeis.Todos.Select(p => NomeDoFundo(p.Id))];
    public IReadOnlyList<string> TemasDeEditor { get; } = Enum.GetNames<ThemeName>();

    [ObservableProperty]
    public partial string Nome { get; set; }

    [ObservableProperty]
    public partial bool Escuro { get; set; }

    [ObservableProperty]
    public partial string Fonte { get; set; }

    [ObservableProperty]
    public partial string FonteCodigo { get; set; }

    [ObservableProperty]
    public partial string FundoAnimado { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemImagem))]
    public partial string? Imagem { get; set; }

    [ObservableProperty]
    public partial double Escurecer { get; set; }

    [ObservableProperty]
    public partial string TemaEditor { get; set; }

    public bool TemImagem => Imagem is not null;

    public static string NomeDoFundo(string id) =>
        Papeis.Encontrar(id) is { } papel ? Idioma.T(papel.Nome) : Idioma.T("Sem fundo animado");

    static string IdDoFundo(string nome) => Papeis.Todos.FirstOrDefault(p => NomeDoFundo(p.Id) == nome)?.Id ?? Papeis.Nenhum;

    partial void OnNomeChanged(string value) => Mudou();
    partial void OnEscuroChanged(bool value) => Mudou();
    partial void OnFonteChanged(string value) => Mudou();
    partial void OnFonteCodigoChanged(string value) => Mudou();
    partial void OnFundoAnimadoChanged(string value) => Mudou();
    partial void OnImagemChanged(string? value) => Mudou();
    partial void OnEscurecerChanged(double value) => Mudou();
    partial void OnTemaEditorChanged(string value) => Mudou();

    void Mudou()
    {
        if (_montando) return;
        Rascunho.Nome = Nome.Trim().Length > 0 ? Nome.Trim() : Idioma.T("Meu tema");
        Rascunho.Escuro = Escuro;
        Rascunho.Fonte = Fonte;
        Rascunho.FonteCodigo = FonteCodigo;
        Rascunho.FundoAnimado = IdDoFundo(FundoAnimado);
        Rascunho.Imagem = Imagem;
        Rascunho.Escurecer = Escurecer;
        Rascunho.Editor = TemaEditor;
        foreach (var cor in Cores) Rascunho.Cores[cor.Chave] = cor.Hex;
        Catalogo.Aplicar(Rascunho);
    }

    [RelayCommand]
    void Salvar()
    {
        Mudou();
        _dono.Salvar(Rascunho);
    }

    [RelayCommand]
    void Cancelar() => _dono.Cancelar();

    [RelayCommand]
    void TirarImagem() => Imagem = null;
}
