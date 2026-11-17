using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Nexora.Core.Painel;
using Nexora.Core.Texto;
using Nexora.Core.Uso;
using Nexora.Views;

namespace Nexora.ViewModels;

public sealed record DiaDaSemanaVM(string Nome, bool Hoje);
