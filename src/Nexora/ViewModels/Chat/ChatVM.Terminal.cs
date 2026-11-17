using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    TerminalVM? _terminal;

    public static bool TemTerminal => OperatingSystem.IsWindows();

    public TerminalVM Terminal
    {
        get
        {
            if (_terminal is not null) return _terminal;
            _terminal = new TerminalVM(() => Pasta);
            _terminal.PedirFechar += () => TerminalAberto = false;
            return _terminal;
        }
    }

    public TerminalVM? TerminalExibido => _terminal is not null && _terminalUsado ? _terminal : null;

    bool _terminalUsado;

    [ObservableProperty]
    public partial bool TerminalAberto { get; set; }

    [ObservableProperty]
    public partial double AlturaTerminal { get; set; } = App.Config.AlturaTerminal;

    partial void OnTerminalAbertoChanged(bool value)
    {
        if (!value) return;
        if (!_terminalUsado)
        {
            _ = Terminal;
            _terminalUsado = true;
            OnPropertyChanged(nameof(TerminalExibido));
        }
        Avalonia.Threading.Dispatcher.UIThread.Post(Terminal.Focar, Avalonia.Threading.DispatcherPriority.Background);
    }

    [RelayCommand]
    void AlternarTerminal()
    {
        if (TemTerminal) TerminalAberto = !TerminalAberto;
    }

    public void GuardarAlturaTerminal()
    {
        App.Config.AlturaTerminal = AlturaTerminal;
        if (!JanelaVM.Isolada) App.Config.Salvar();
    }

    void EncerrarTerminal()
    {
        _terminal?.Dispose();
        _terminal = null;
    }
}
