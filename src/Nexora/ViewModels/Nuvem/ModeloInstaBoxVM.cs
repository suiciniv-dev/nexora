using System.Collections.ObjectModel;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Nuvem;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed class ModeloInstaBoxVM(ModeloInstaBox dados)
{
    public ModeloInstaBox Dados { get; } = dados;
    public string Nome => Dados.Id;
    public string Detalhe => Dados.Vcpus > 0 ? Idioma.F("{0} vCPU · {1} GB", Dados.Vcpus, Dados.MemoriaMiB / 1024) : "";
    public override string ToString() => Nome;
}
