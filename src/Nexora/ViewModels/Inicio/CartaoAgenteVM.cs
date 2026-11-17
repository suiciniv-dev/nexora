using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Provedores;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class CartaoAgenteVM(DescricaoAgente agente, EscolhaDeAgenteVM dono) : ObservableObject
{
    public DescricaoAgente Agente { get; } = agente;
    public string Id => Agente.Id;
    public string Nome => Agente.Nome;
    public string Descricao => Agente.Descricao;
    public string Instalar => Agente.Instalar;
    public string Empresa => Agente.Empresa.ToUpperInvariant();
    public bool EhClaude => Agente.Protocolo == ProtocoloAgente.Claude;
    public bool EhCodex => Agente.Protocolo == ProtocoloAgente.Codex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoEstado))]
    public partial bool Detectado { get; set; }

    [ObservableProperty]
    public partial bool Escolhido { get; set; }

    public string TextoEstado => Detectado ? Idioma.T("DETECTADO") : Idioma.T("NÃO ENCONTRADO");

    [RelayCommand]
    void Escolher() => dono.Escolher(this);
}
