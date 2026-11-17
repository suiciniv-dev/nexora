using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Executar;
using Nexora.Core.Git;
using Nexora.Core.Nuvem;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed record OndeExecutaVM(string Nome, string? MaquinaId)
{
    public override string ToString() => Nome;
}
