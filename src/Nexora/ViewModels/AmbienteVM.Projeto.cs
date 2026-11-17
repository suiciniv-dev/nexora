using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class AmbienteVM
{
    static readonly string[] ArquivosDeInstrucao = ["CLAUDE.md", "CLAUDE.local.md", Path.Combine(".claude", "CLAUDE.md"), "AGENTS.md"];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ComecarCommand))]
    public partial string Rascunho { get; set; } = "";

    [ObservableProperty]
    public partial bool MostraTarefa { get; set; }

    public ObservableCollection<ConversaVM> Recentes { get; } = [];
    public ObservableCollection<InstrucaoVM> Instrucoes { get; } = [];
    public bool TemRecentes => Recentes.Count > 0;
    public bool TemInstrucoes => Instrucoes.Count > 0;
    public string Iniciais => Siglas.De(Nome);

    Avalonia.Media.Imaging.Bitmap? _iconeRepo;
    bool _iconePedido;

    public Avalonia.Media.Imaging.Bitmap? IconeRepo
    {
        get
        {
            PedirIcone();
            return _iconeRepo;
        }
    }

    public bool TemIconeRepo => IconeRepo is not null;

    void PedirIcone()
    {
        if (_iconePedido || JanelaVM.Demo || Principal is not { Length: > 0 } pasta) return;
        _iconePedido = true;
        _ = CarregarIconeAsync(pasta);
    }

    async Task CarregarIconeAsync(string pasta)
    {
        if ((await IconesDeRepositorio.DeAsync(pasta)).Imagem is not { } imagem) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _iconeRepo = imagem;
            OnPropertyChanged(nameof(IconeRepo));
            OnPropertyChanged(nameof(TemIconeRepo));
        });
    }
    public string PlaceholderSessao => Idioma.F("Nova sessão em {0}  ·  Enter começa, Shift+Enter quebra a linha", Nome);

    bool PodeComecar() => !string.IsNullOrWhiteSpace(Rascunho);

    [RelayCommand(CanExecute = nameof(PodeComecar))]
    void Comecar()
    {
        var texto = Rascunho.Trim();
        if (texto.Length == 0) return;
        Rascunho = "";
        _anfitriao.AbrirSessao(Local, texto);
    }

    [RelayCommand]
    void AlternarTarefa() => MostraTarefa = !MostraTarefa;

    public void AtualizarProjeto()
    {
        Recentes.Clear();
        foreach (var c in _anfitriao.ConversasDe(Principal)) Recentes.Add(c);
        Instrucoes.Clear();
        foreach (var raiz in new[] { Principal }.Concat(Dados.Vinculados))
            foreach (var nome in ArquivosDeInstrucao)
                if (Path.Combine(raiz, nome) is var caminho && File.Exists(caminho))
                    Instrucoes.Add(new InstrucaoVM(caminho, raiz == Principal ? null : Path.GetFileName(raiz.TrimEnd('\\', '/'))));
        OnPropertyChanged(nameof(TemRecentes));
        OnPropertyChanged(nameof(TemInstrucoes));
    }
}
