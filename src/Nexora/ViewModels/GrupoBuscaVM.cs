using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Arquivos;
using Nexora.Core.Git;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed class GrupoBuscaVM(string nome, string diretorio, IReadOnlyList<LinhaBuscaVM> linhas)
{
    public string Nome { get; } = nome;
    public string Diretorio { get; } = diretorio;
    public IReadOnlyList<LinhaBuscaVM> Linhas { get; } = linhas;
    public string Total => Linhas.Count.ToString();
}
