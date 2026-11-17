using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ItemTexto : ItemChat
{
    public ItemTexto(string texto) => Texto = texto;

    [ObservableProperty]
    public partial string Texto { get; set; }
}
