using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.Core.Orquestra;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class OrquestraAnteriorVM(string sessaoId, OrquestraSalva dados, Action<OrquestraAnteriorVM> retomar) : ObservableObject
{
    public string SessaoId { get; } = sessaoId;
    public string Titulo => dados.Titulo;
    public string Detalhe => $"{Path.GetFileName(dados.Pasta.TrimEnd('\\', '/'))}  ·  {(dados.Agentes.Count == 1 ? Idioma.T("1 agente") : Idioma.F("{0} agentes", dados.Agentes.Count))}  ·  {Core.Texto.Tempo.Relativo(dados.Atualizado)}";

    [RelayCommand]
    void Retomar() => retomar(this);
}
