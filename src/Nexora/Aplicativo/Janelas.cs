using Avalonia;
using Avalonia.Controls;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Aplicativo;

public sealed class JanelaSalva
{
    public int X { get; set; }
    public int Y { get; set; }
    public double Largura { get; set; }
    public double Altura { get; set; }
    public bool Maximizada { get; set; }
    public List<string> Guias { get; set; } = [];
}

static class Janelas
{
    static JanelaVM? _vm;
    static Janela? _principal;
    static readonly List<JanelaExtra> Extras = [];

    public static void Iniciar(JanelaVM vm, Janela principal)
    {
        _vm = vm;
        _principal = principal;
        vm.NovaArea += (area, onde) => Abrir(area, onde, null);
        Ligar(principal, vm.Principal);
    }

    public static void TrazerPrincipal()
    {
        if (_principal is null) return;
        if (_principal.WindowState == WindowState.Minimized) _principal.WindowState = WindowState.Normal;
        _principal.Activate();
    }

    static void Ligar(Window janela, AreaVM area)
    {
        area.PedirFoco += () =>
        {
            if (janela.WindowState == WindowState.Minimized) janela.WindowState = WindowState.Normal;
            janela.Activate();
        };
        janela.Activated += (_, _) =>
        {
            area.Ativa = true;
            area.Janela.UltimaArea = area;
        };
        janela.Deactivated += (_, _) => area.Ativa = false;
    }

    static void Abrir(AreaVM area, PixelPoint? onde, JanelaSalva? salva)
    {
        var janela = new JanelaExtra { DataContext = area };
        if (salva is not null)
            Posicionar(janela, salva);
        else if (onde is { } ponto)
        {
            janela.WindowStartupLocation = WindowStartupLocation.Manual;
            janela.Position = new PixelPoint(ponto.X - 160, ponto.Y - 20);
        }
        Ligar(janela, area);
        area.PedirFechar += janela.Close;
        janela.Closed += (_, _) =>
        {
            Extras.Remove(janela);
            area.Janela.RemoverArea(area);
        };
        Extras.Add(janela);
        janela.Show();
    }

    public static void Soltar(GuiaVM guia, PixelPoint tela)
    {
        if (_vm is null || _principal is null) return;
        foreach (var (janela, abas) in Extras.Select(j => ((Window)j, j.FaixaDeAbas)).Prepend((_principal, _principal.FaixaDeAbas)))
        {
            if (!janela.IsVisible || !abas.Contem(tela) || abas.DataContext is not AreaVM area) continue;
            _vm.Levar(guia, area, abas.IndiceEm(tela));
            janela.Activate();
            return;
        }
        _vm.Destacar(guia, tela);
    }

    public static void RestaurarPrincipal()
    {
        if (_vm is null || _principal is null || JanelaVM.Isolada || App.Config.Principal is not { } principal) return;
        Posicionar(_principal, principal);
        foreach (var chave in principal.Guias)
            if (_vm.PorChave(chave) is { } conteudo && !AreaVM.EhPagina(conteudo)) _vm.Principal.Abrir(conteudo);
    }

    public static void RestaurarExtras()
    {
        if (_vm is null || JanelaVM.Isolada) return;
        foreach (var salva in App.Config.Janelas)
        {
            var area = _vm.NovaAreaVazia();
            foreach (var chave in salva.Guias)
                if (_vm.PorChave(chave) is { } conteudo && !AreaVM.EhPagina(conteudo)) area.Abrir(conteudo);
            if (area.Guias.Count == 0) _vm.RemoverArea(area);
            else Abrir(area, null, salva);
        }
    }

    public static void Salvar()
    {
        if (_vm is null || _principal is null || JanelaVM.Isolada) return;
        App.Config.Principal = Foto(_principal, _vm.Principal);
        App.Config.Janelas = [.. Extras.Select(j => Foto(j, (AreaVM)j.DataContext!)).Where(s => s.Guias.Count > 0)];
        App.Config.Salvar();
    }

    public static void FecharExtras()
    {
        foreach (var janela in Extras.ToList()) janela.Close();
    }

    static JanelaSalva Foto(Window janela, AreaVM area) => new()
    {
        X = janela.Position.X,
        Y = janela.Position.Y,
        Largura = janela.ClientSize.Width,
        Altura = janela.ClientSize.Height,
        Maximizada = janela.WindowState == WindowState.Maximized,
        Guias = [.. area.Guias.Select(g => g.Chave).OfType<string>()],
    };

    static void Posicionar(Window janela, JanelaSalva salva)
    {
        var ponto = new PixelPoint(salva.X, salva.Y);
        if (janela.Screens.All.Any(t => t.WorkingArea.Contains(new PixelPoint(ponto.X + 60, ponto.Y + 20))))
        {
            janela.WindowStartupLocation = WindowStartupLocation.Manual;
            janela.Position = ponto;
        }
        if (salva.Largura > 0) janela.Width = Math.Max(janela.MinWidth, salva.Largura);
        if (salva.Altura > 0) janela.Height = Math.Max(janela.MinHeight, salva.Altura);
        if (salva.Maximizada) janela.WindowState = WindowState.Maximized;
    }
}
