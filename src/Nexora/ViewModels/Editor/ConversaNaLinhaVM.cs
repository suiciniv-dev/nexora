using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Nexora.Core.Revisao;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ConversaNaLinhaVM : ObservableObject
{
    public ConversaNaLinhaVM(ConversaDoPr dados)
    {
        Dados = dados;
        Autor = dados.Primeiro?.Autor ?? "";
        Agente = ComentariosDeRevisao.AgenteDoLogin(Autor);
        Iniciais = ComentariosDeRevisao.Iniciais(Autor);
        Corpo = PainelPrVM.Resumir(dados.Primeiro?.Corpo ?? "", 600);
        Respostas = [.. dados.Comentarios.Skip(1).Select(c => new RespostaNaLinhaVM(c.Autor, PainelPrVM.Resumir(c.Corpo, 400)))];
    }

    public ConversaDoPr Dados { get; }
    public string Autor { get; }
    public string? Agente { get; }
    public bool EhAgente => Agente is not null;
    public bool EhPessoa => Agente is null;
    public string Iniciais { get; }
    public string Corpo { get; }
    public IReadOnlyList<RespostaNaLinhaVM> Respostas { get; }
    public bool TemRespostas => Respostas.Count > 0;
    public bool Resolvida => Dados.Resolvida;
    public string Situacao => Dados.Resolvida ? Idioma.T("resolvida") : Idioma.T("aberta");
    public string Quando => Dados.UltimaEm is { } q ? Core.Texto.Tempo.Relativo(q.LocalDateTime) : "";
    public string Dica => $"{Autor}: {Core.Claude.Resumos.UmaLinha(Dados.Primeiro?.Corpo ?? "", 120)}";
    public bool TemLink => Dados.Primeiro?.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true;

    [RelayCommand]
    void Abrir()
    {
        if (TemLink) PainelPrVM.AbrirLink(Dados.Primeiro!.Url);
    }
}
