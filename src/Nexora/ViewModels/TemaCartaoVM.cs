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

public sealed partial class TemaCartaoVM : ObservableObject
{
    readonly TemasVM? _dono;

    public TemaCartaoVM(Tema tema, TemasVM? dono)
    {
        Tema = tema;
        _dono = dono;
        if (Papeis.Encontrar(tema.FundoAnimado) is { } papel) _ = CarregarMiniatura(papel.Id);
    }

    public static TemaCartaoVM DoSistema(TemasVM? dono) => new(Catalogo.Embutidos[0], dono)
    {
        EhSistema = true,
        Escuro = new TemaCartaoVM(Catalogo.Embutidos[0], null),
        Claro = new TemaCartaoVM(Catalogo.Embutidos[1], null),
    };

    async Task CarregarMiniatura(string papel)
    {
        var imagem = await Miniaturas.Obter(papel);
        Dispatcher.UIThread.Post(() => Miniatura = imagem);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMiniatura))]
    public partial Bitmap? Miniatura { get; set; }

    public bool EhSistema { get; private init; }
    public bool NaoEhSistema => !EhSistema;
    public TemaCartaoVM? Escuro { get; private init; }
    public TemaCartaoVM? Claro { get; private init; }
    public bool TemMiniatura => Miniatura is not null;
    public Tema Tema { get; }
    public string Id => EhSistema ? Catalogo.IdSistema : Tema.Id;
    public string? Apelido { get; init; }
    public string Nome => Apelido ?? (EhSistema ? Idioma.T("Seguir o sistema") : Tema.NomeExibido);
    public string Descricao => EhSistema
        ? Idioma.T(OperatingSystem.IsMacOS() ? "Claro ou escuro, como o macOS" : "Claro ou escuro, como o Windows")
        : Papeis.Encontrar(Tema.FundoAnimado) is { } papel
            ? Idioma.F("{0}  ·  {1}", Tema.Escuro ? Idioma.T("Escuro") : Idioma.T("Claro"), Idioma.T(Tema.Embutido ? papel.Ideia : papel.Nome))
            : Tema.Escuro ? Idioma.T("Escuro") : Idioma.T("Claro");
    public bool Embutido => Tema.Embutido;
    public bool Meu => !EhSistema && !Tema.Embutido;
    public bool PodeDuplicar => !EhSistema;
    public bool EmUso => Catalogo.Escolhido == Id;
    public bool Disponivel => !EmUso;
    public string NomeAcessivel => EmUso ? Idioma.F("{0}, em uso", Nome) : Nome;
    public IBrush Fundo => P("Fundo");
    public IBrush Lateral => P("Lateral");
    public IBrush Superficie => P("Superficie");
    public IBrush SuperficieAlta => P("SuperficieAlta");
    public IBrush Acento => P("Acento");
    public IBrush Texto => P("Texto");
    public IBrush TextoFraco => P("TextoFraco");
    public IBrush Verde => P("Verde");
    public IBrush Laranja => P("Laranja");
    public IBrush Rosa => P("Rosa");

    IBrush P(string chave)
    {
        var cor = Tema.Cor(chave);
        return new SolidColorBrush(Color.FromRgb(cor.R, cor.G, cor.B));
    }

    public void AtualizarUso()
    {
        OnPropertyChanged(nameof(EmUso));
        OnPropertyChanged(nameof(Disponivel));
        OnPropertyChanged(nameof(NomeAcessivel));
    }

    [RelayCommand]
    void Usar() => _dono?.Usar(this);

    [RelayCommand]
    void Duplicar() => _dono?.Duplicar(Tema);

    [RelayCommand]
    void Editar() => _dono?.Editar(Tema);

    [RelayCommand]
    void Apagar() => _dono?.Apagar(Tema);
}
