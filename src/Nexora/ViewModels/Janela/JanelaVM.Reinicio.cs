using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class JanelaVM
{
    [ObservableProperty]
    public partial bool PedindoReinicio { get; set; }

    [ObservableProperty]
    public partial string TextoReinicio { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoReinicio))]
    public partial string AvisoReinicio { get; set; } = "";

    public bool TemAvisoReinicio => AvisoReinicio.Length > 0;

    void PedirReinicio(string modo)
    {
        if (!Program.MudaODesenho(modo) || MostraBoasVindas)
        {
            PedindoReinicio = false;
            return;
        }
        TextoReinicio = modo == "solido"
            ? Idioma.T("O modo sólido desenha a tela pelo processador e gasta bem menos memória. Essa parte só vale depois de reiniciar; as abas voltam sozinhas.")
            : Idioma.T("O vidro e a animação usam a placa de vídeo, que só volta depois de reiniciar. As abas voltam sozinhas.");
        var trabalhando = Abertas.Count(c => c.Trabalhando);
        AvisoReinicio = trabalhando switch
        {
            0 => "",
            1 => Idioma.T("1 conversa está trabalhando e vai parar."),
            _ => Idioma.F("{0} conversas estão trabalhando e vão parar.", trabalhando),
        };
        PedindoReinicio = true;
    }

    [RelayCommand]
    void ReiniciarDepois() => PedindoReinicio = false;

    [RelayCommand]
    void ReiniciarAgora()
    {
        PedindoReinicio = false;
        if (Demo || Isolada || !Atualizacoes.ReabrirDepoisDeFechar()) return;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime vida) vida.Shutdown();
    }
}
