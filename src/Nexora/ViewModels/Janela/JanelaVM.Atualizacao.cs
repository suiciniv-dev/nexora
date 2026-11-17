using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class JanelaVM
{
    bool _confirmandoAtualizacao;
    bool _procurando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAtualizacao), nameof(TextoAtualizacao), nameof(DicaAtualizacao))]
    public partial VersaoNova? Novidade { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoAtualizacao))]
    public partial string? ProgressoAtualizacao { get; set; }

    public bool TemAtualizacao => Novidade is not null;

    public string TextoAtualizacao => ProgressoAtualizacao
        ?? (_confirmandoAtualizacao ? Idioma.T("Fechar as sessões e atualizar") : Idioma.F("Atualizar para {0}", Novidade?.Rotulo));

    public string DicaAtualizacao => Novidade is { } v
        ? Idioma.F("Nexora {0} saiu (você está na {1}). Baixa, fecha o Nexora, instala e abre de novo.", v.Rotulo, Atualizacoes.Atual.ToString(3))
        : "";

    public void ProcurarAtualizacoes()
    {
        if (_procurando || Isolada || Demo) return;
        _procurando = true;
        _ = VigiarAtualizacoesAsync();
    }

    async Task VigiarAtualizacoesAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(20));
        while (true)
        {
            if (await Atualizacoes.ProcurarAsync() is { } nova && Novidade?.Versao != nova.Versao)
            {
                Novidade = nova;
                Avisos.Notificar(Idioma.F("Nexora {0} disponível", nova.Rotulo), Idioma.T("Clique em Atualizar, no alto da janela do Nexora."));
            }
            await Task.Delay(TimeSpan.FromHours(6));
        }
    }

    [RelayCommand]
    async Task AtualizarAgora()
    {
        if (Novidade is not { } v || ProgressoAtualizacao is not null) return;
        if (Demo) return;
        if (!_confirmandoAtualizacao && Abertas.Any(c => c.Trabalhando))
        {
            _confirmandoAtualizacao = true;
            OnPropertyChanged(nameof(TextoAtualizacao));
            return;
        }
        ProgressoAtualizacao = Idioma.T("Baixando…");
        var ok = await Atualizacoes.PrepararAsync(v, p => Avalonia.Threading.Dispatcher.UIThread.Post(() => ProgressoAtualizacao = Idioma.F("Baixando {0:P0}", p)));
        if (!ok)
        {
            ProgressoAtualizacao = null;
            _confirmandoAtualizacao = false;
            OnPropertyChanged(nameof(TextoAtualizacao));
            try
            {
                Sistema.Abrir(new ProcessStartInfo(Atualizacoes.Pagina) { UseShellExecute = true })?.Dispose();
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            return;
        }
        ProgressoAtualizacao = Idioma.T("Reiniciando…");
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime vida) vida.Shutdown();
    }
}
