using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ResultadoBuscaVM(string icone, string titulo, string detalhe, Action abrir)
{
    public string Icone { get; } = icone;
    public string Titulo { get; } = titulo;
    public string Detalhe { get; } = detalhe;
    public bool TemDetalhe => Detalhe.Length > 0;

    [RelayCommand]
    public void Abrir() => abrir();
}

public sealed partial class JanelaVM
{
    public ObservableCollection<ResultadoBuscaVM> Resultados { get; } = [];

    [ObservableProperty]
    public partial bool BuscaAberta { get; set; }

    [ObservableProperty]
    public partial ResultadoBuscaVM? ResultadoSelecionado { get; set; }

    public bool SemResultados => BuscaAberta && Resultados.Count == 0;

    [RelayCommand]
    void FecharBusca()
    {
        BuscaAberta = false;
        Busca = "";
    }

    public void AbrirResultado(ResultadoBuscaVM? resultado)
    {
        if ((resultado ?? ResultadoSelecionado ?? Resultados.FirstOrDefault()) is not { } escolhido) return;
        FecharBusca();
        escolhido.Abrir();
    }

    public void MoverResultado(int passo)
    {
        if (Resultados.Count == 0) return;
        var i = ResultadoSelecionado is { } atual ? Resultados.IndexOf(atual) : -1;
        ResultadoSelecionado = Resultados[Math.Clamp(i + passo, 0, Resultados.Count - 1)];
    }

    (string Icone, string Nome, Action Abrir)[] Telas() =>
    [
        ("quadro", Idioma.T("Ao vivo"), () => MostrarVisaoGeralCommand.Execute(null)),
        ("camadas", Idioma.T("Projetos"), () => MostrarProjetosCommand.Execute(null)),
        ("issue", Idioma.T("Issues"), () => MostrarIssuesCommand.Execute(null)),
        ("orquestra", Idioma.T("Orquestrador"), () => MostrarOrquestraCommand.Execute(null)),
        ("relogio", Idioma.T("Agendados"), () => MostrarAgendadosCommand.Execute(null)),
        ("grafico", Idioma.T("Uso"), () => MostrarUsoCommand.Execute(null)),
        ("memoria", Idioma.T("Recursos"), () => MostrarRecursosCommand.Execute(null)),
        ("ajustes", Idioma.T("Configurações"), () => MostrarConfiguracoesCommand.Execute(null)),
        ("maximizar", Idioma.T("Tela cheia"), () => AlternarTelaCheiaCommand.Execute(null)),
        ("paleta", Idioma.T("Temas"), () => MostrarTemasCommand.Execute(null)),
        ("modelo", Idioma.T("Templates"), TemplatesDoApp.AbrirEditor),
        ("foco", Idioma.T("Modo foco"), () => AbrirFocoCommand.Execute(null)),
    ];

    void MontarResultados()
    {
        Resultados.Clear();
        var termo = Busca.Trim();
        bool Combina(string texto) => termo.Length == 0 || texto.Contains(termo, StringComparison.CurrentCultureIgnoreCase);
        var atalhos = CatalogoDeAtalhos.Todos();
        foreach (var (icone, nome, abrir) in Telas().Where(t => Combina(t.Nome)).Take(termo.Length == 0 ? 4 : 6))
            Resultados.Add(new ResultadoBuscaVM(icone, nome,
                atalhos.FirstOrDefault(a => a.NaBusca && a.Icone == icone) is { } atalho ? Idioma.F("tela · {0}", atalho.TeclaExibida) : Idioma.T("tela"), abrir));
        var telas = Telas().Select(t => t.Icone).ToHashSet();
        foreach (var comando in ComandosDaBusca(termo).Where(c => !telas.Contains(c.Icone)))
            Resultados.Add(comando);
        foreach (var chat in Abertas.Where(c => Combina(c.Titulo) || Combina(c.NomeProjeto)).Take(5))
            Resultados.Add(new ResultadoBuscaVM("conversa", chat.Titulo, Idioma.F("{0} · aberta", chat.NomeProjeto), () => Navegar(chat)));
        foreach (var ambiente in Ambientes.Where(a => Combina(a.Nome)).Take(termo.Length == 0 ? 3 : 5))
            Resultados.Add(new ResultadoBuscaVM("camadas", ambiente.Nome, Idioma.T("projeto"), () => Navegar(ambiente)));
        var abertas = Abertas.Select(c => c.SessaoId).OfType<string>().ToHashSet();
        foreach (var conversa in ConversasVisiveis.Where(c => !abertas.Contains(c.Dados.SessaoId)).Take(termo.Length == 0 ? 5 : 8))
            Resultados.Add(new ResultadoBuscaVM("relogio", conversa.Titulo,
                $"{DonoDaPasta(conversa.Dados.Pasta)?.Nome ?? Path.GetFileName(conversa.Dados.Pasta?.TrimEnd('\\', '/') ?? "")} · {conversa.Quando}", () => Abrir(conversa)));
        ResultadoSelecionado = Resultados.FirstOrDefault();
        OnPropertyChanged(nameof(SemResultados));
    }
}
