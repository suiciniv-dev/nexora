using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Terminal;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class TerminalVM(Func<string> pasta) : ObservableObject, IDisposable
{
    const int MaiorHistorico = 400_000;
    const int HistoricoAposCorte = 300_000;

    readonly StringBuilder _historico = new();
    Pseudoconsole? _terminal;
    short _colunas = 120, _linhas = 30;

    public event Action<string>? Recebeu;
    public event Action? Limpou;
    public event Action? PedirFechar;
    public event Action? PedirFoco;

    public void Focar() => PedirFoco?.Invoke();

    public string Historico
    {
        get
        {
            lock (_historico) return _historico.ToString();
        }
    }

    public bool Rodando => _terminal is not null;

    public void Garantir(short colunas, short linhas)
    {
        (_colunas, _linhas) = (Math.Max((short)10, colunas), Math.Max((short)4, linhas));
        if (_terminal is null) Abrir();
        else _terminal.Redimensionar(_colunas, _linhas);
    }

    void Abrir()
    {
        try
        {
            var terminal = Pseudoconsole.Abrir(Pseudoconsole.ShellPadrao(), pasta(), _colunas, _linhas, App.Segredos.Cofre.VariaveisDe(pasta()));
            terminal.Saida += texto => Dispatcher.UIThread.Post(() => Receber(texto));
            terminal.Encerrou += codigo => Dispatcher.UIThread.Post(() => AoEncerrar(terminal, codigo));
            _terminal = terminal;
            OnPropertyChanged(nameof(Rodando));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or PlatformNotSupportedException or DirectoryNotFoundException)
        {
            Receber("\r\n\u001b[31m" + Idioma.F("Não consegui abrir o terminal: {0}", ex.Message) + "\u001b[0m\r\n");
        }
    }

    void Receber(string texto)
    {
        lock (_historico)
        {
            _historico.Append(texto);
            if (_historico.Length > MaiorHistorico) _historico.Remove(0, _historico.Length - HistoricoAposCorte);
        }
        Recebeu?.Invoke(texto);
    }

    void AoEncerrar(Pseudoconsole terminal, int codigo)
    {
        if (_terminal != terminal) return;
        _terminal = null;
        terminal.Dispose();
        OnPropertyChanged(nameof(Rodando));
        Receber("\r\n\u001b[90m[" + Idioma.F("o terminal fechou com o código {0}. Tecle Enter para abrir de novo", codigo) + "]\u001b[0m\r\n");
    }

    public void Escrever(string dados)
    {
        if (_terminal is { } terminal) terminal.Escrever(dados);
        else if (dados.Contains('\r')) Reiniciar();
    }

    public void Fechar() => PedirFechar?.Invoke();

    [RelayCommand]
    void Reiniciar()
    {
        var antigo = _terminal;
        _terminal = null;
        antigo?.Dispose();
        lock (_historico) _historico.Clear();
        Limpou?.Invoke();
        Abrir();
    }

    public void Dispose()
    {
        var antigo = _terminal;
        _terminal = null;
        antigo?.Dispose();
    }
}
