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

public sealed partial class TemasVM : ObservableObject
{
    public static readonly string[] Modos = ["animado", "vidro", "solido"];

    readonly Action<string> _aoUsar;

    public TemasVM(Action<string> aoUsar)
    {
        _aoUsar = aoUsar;
        Recarregar();
    }

    public ObservableCollection<TemaCartaoVM> Cartoes { get; } = [];
    public ObservableCollection<GrupoTemasVM> Grupos { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Editando), nameof(NaGaleria))]
    public partial TemaEditorVM? Editor { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    public partial string? Mensagem { get; set; }

    public bool Editando => Editor is not null;
    public bool NaGaleria => Editor is null;
    public bool TemMensagem => Mensagem is not null;

    public static IEnumerable<Tema> Principais => Catalogo.Embutidos.Where(t => Papeis.Encontrar(t.FundoAnimado) is null);

    public static IEnumerable<Tema> Animados => Catalogo.Embutidos.Where(t => Papeis.Encontrar(t.FundoAnimado) is not null);

    public void Recarregar()
    {
        Cartoes.Clear();
        Cartoes.Add(TemaCartaoVM.DoSistema(this));
        foreach (var tema in Catalogo.Todos()) Cartoes.Add(new TemaCartaoVM(tema, this));
        Grupos.Clear();
        Grupos.Add(new GrupoTemasVM("Nexora", Idioma.T("Cores cheias e calmas. Seguir o sistema troca entre claro e escuro junto com o computador."),
            Cartoes.Where(c => c.EhSistema || c.Tema.Embutido && Papeis.Encontrar(c.Tema.FundoAnimado) is null)));
        Grupos.Add(new GrupoTemasVM(Idioma.T("Temas animados"), Idioma.T("Céus vivos do Nexora: estrelas que cintilam e um grafo sutil. Se mexem com o fundo em \"Vidro, com animação\"; nos outros modos ficam parados."),
            Cartoes.Where(c => !c.EhSistema && c.Tema.Embutido && Papeis.Encontrar(c.Tema.FundoAnimado) is not null)));
        var meus = Cartoes.Where(c => !c.EhSistema && !c.Tema.Embutido).ToList();
        if (meus.Count > 0) Grupos.Add(new GrupoTemasVM(Idioma.T("Seus temas"), Idioma.T("Salvos em %APPDATA%\\Nexora\\temas."), meus));
    }

    public void Usar(TemaCartaoVM cartao)
    {
        if (cartao.EhSistema) Catalogo.Escolher(Catalogo.IdSistema);
        else Catalogo.Escolher(cartao.Tema);
        _aoUsar(cartao.Id);
        foreach (var c in Cartoes) c.AtualizarUso();
    }

    public void Duplicar(Tema tema) =>
        Editor = new TemaEditorVM(tema.Copiar(Catalogo.NovoId(tema.Nome + " meu"), Idioma.F("{0} (meu)", tema.NomeExibido)), this);

    public void Editar(Tema tema) => Editor = new TemaEditorVM(tema.Copiar(tema.Id, tema.Nome), this);

    public void Apagar(Tema tema)
    {
        if (Catalogo.Escolhido == tema.Id) Usar(Cartoes[0]);
        Catalogo.Apagar(tema);
        Recarregar();
        Mensagem = Idioma.F("Tema {0} apagado.", tema.NomeExibido);
    }

    public void Exportar(Tema tema, string destino)
    {
        Catalogo.Gravar(tema, destino);
        Mensagem = Idioma.F("Tema exportado para {0}.", destino);
    }

    public void Importar(string arquivo)
    {
        if (Catalogo.Ler(arquivo) is not { } tema)
        {
            Mensagem = Idioma.T("Esse arquivo não é um tema do Nexora.");
            return;
        }
        tema.Id = Catalogo.NovoId(tema.Nome);
        Catalogo.Gravar(tema);
        Recarregar();
        Mensagem = Idioma.F("Tema {0} importado.", tema.NomeExibido);
    }

    [RelayCommand]
    void NovoTema() => Duplicar(Catalogo.Atual);

    public void Salvar(Tema tema)
    {
        Catalogo.Gravar(tema);
        Editor = null;
        Recarregar();
        Catalogo.Escolher(tema);
        _aoUsar(tema.Id);
        foreach (var c in Cartoes) c.AtualizarUso();
        Mensagem = Idioma.F("Tema {0} salvo e em uso.", tema.NomeExibido);
    }

    public void Cancelar()
    {
        Editor = null;
        Catalogo.Escolher(App.Config.Tema);
    }
}
