using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ConversaPrVM(ConversaDoPr dados, Action<ConversaDoPr> abrirArquivo, Action<ConversaDoPr> pedir, Action? aoMarcar = null) : ObservableObject
{
    public ConversaDoPr Dados { get; } = dados;
    public string Iniciais => Core.Revisao.ComentariosDeRevisao.Iniciais(Autor);
    public string? Agente => Core.Revisao.ComentariosDeRevisao.AgenteDoLogin(Autor);
    public bool EhAgente => Agente is not null;
    public bool EhPessoa => Agente is null;

    [ObservableProperty]
    public partial bool Incluir { get; set; } = true;

    partial void OnIncluirChanged(bool value) => aoMarcar?.Invoke();
    public string Linha => dados.Linha is { } l ? Idioma.F("linha {0}", l) : Idioma.T("arquivo");
    public string Autor => dados.Primeiro?.Autor ?? "";
    public string Corpo => PainelPrVM.Resumir(dados.Primeiro?.Corpo ?? "", 420);
    public string Quando => dados.UltimaEm is { } q ? Core.Texto.Tempo.Relativo(q.LocalDateTime) : "";
    public bool Resolvida => dados.Resolvida;
    public bool Desatualizada => dados.Desatualizada;
    public int Respostas => Math.Max(0, dados.Comentarios.Count - 1);
    public bool TemRespostas => Respostas > 0;
    public string TextoRespostas => Respostas == 1 ? Idioma.T("1 resposta") : Idioma.F("{0} respostas", Respostas);
    public bool TemLink => dados.Primeiro?.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true;

    [RelayCommand]
    void Abrir()
    {
        if (TemLink) PainelPrVM.AbrirLink(dados.Primeiro!.Url);
    }

    [RelayCommand]
    void AbrirArquivo() => abrirArquivo(dados);

    [RelayCommand]
    void PedirCorrecao() => pedir(dados);
}
