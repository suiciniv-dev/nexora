using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Nexora.ViewModels;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.Views;

public sealed class JanelaDoNavegador : Window
{
    static readonly Dictionary<NavegadorVM, JanelaDoNavegador> Abertas = [];

    readonly NavegadorVM _vm;

    JanelaDoNavegador(NavegadorVM vm)
    {
        _vm = vm;
        Width = 1100;
        Height = 780;
        MinWidth = 420;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.Bind(BackgroundProperty, this.GetResourceObservable("FundoSolido"));
        Content = new NavegadorView { DataContext = vm };
        AtualizarTitulo();
        vm.PropertyChanged += AoMudar;
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.W, KeyModifiers.Control), Command = vm.EncaixarCommand });
        Closed += (_, _) =>
        {
            vm.PropertyChanged -= AoMudar;
            Abertas.Remove(vm);
            if (vm.Destacado) vm.Encaixar();
        };
    }

    public static void Abrir(NavegadorVM vm)
    {
        if (Abertas.TryGetValue(vm, out var aberta))
        {
            if (aberta.WindowState == WindowState.Minimized) aberta.WindowState = WindowState.Normal;
            aberta.Activate();
            return;
        }
        var janela = new JanelaDoNavegador(vm);
        Abertas[vm] = janela;
        var dono = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (dono is not null)
        {
            janela.Icon = dono.Icon;
            janela.Show(dono);
        }
        else janela.Show();
    }

    void AoMudar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavegadorVM.Destacado) && !_vm.Destacado) Close();
        else if (e.PropertyName is nameof(NavegadorVM.Titulo) or nameof(NavegadorVM.Atual)) AtualizarTitulo();
    }

    void AtualizarTitulo() =>
        Title = string.IsNullOrWhiteSpace(_vm.Titulo) ? Idioma.T("Navegador") : Idioma.F("{0} · Navegador", _vm.Titulo);
}
