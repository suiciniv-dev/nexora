using System.Text.Json;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed record RepoGuardado(string Pasta, string? Repo, IReadOnlyList<IssueGh>? Issues);

public sealed record IssuesGuardadas(bool SoMinhas, IReadOnlyList<RepoGuardado> Repos, IReadOnlyList<QuadroGh> Quadros, string Fonte, string? Visao, IReadOnlyList<ItemDoQuadro>? Itens);

public sealed partial class IssuesVM
{
    public static string ArquivoGuardadas { get; set; } =
        Path.Combine(global::Nexora.Core.DadosDoNexora.Pasta, "issues.json");

    public static IssuesGuardadas? LerGuardadas()
    {
        try
        {
            return File.Exists(ArquivoGuardadas) ? JsonSerializer.Deserialize<IssuesGuardadas>(File.ReadAllText(ArquivoGuardadas)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    public static async Task GravarGuardadasAsync(IssuesGuardadas guardadas)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ArquivoGuardadas)!);
            var temporario = ArquivoGuardadas + ".tmp";
            await File.WriteAllTextAsync(temporario, JsonSerializer.Serialize(guardadas));
            File.Move(temporario, ArquivoGuardadas, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    void Guardar()
    {
        if (JanelaVM.Demo || _lidoEm == default || _listaSoMinhas != SoMinhas) return;
        var guardadas = new IssuesGuardadas(SoMinhas,
            [.. _lista.Select(x => new RepoGuardado(x.Projeto.Principal, x.Repo, x.Issues))],
            [.. Fontes.Select(f => f.Quadro).OfType<QuadroGh>().Select(q => _detalhados.GetValueOrDefault(q.Id) ?? q)],
            _quadro?.Id ?? Lista, _visao?.Nome, _quadro is null ? null : [.. _linhas.Select(l => l.Item)]);
        _ = Task.Run(() => GravarGuardadasAsync(guardadas));
    }

    bool MostrarGuardadas(IReadOnlyList<AmbienteVM> projetos)
    {
        if (LerGuardadas() is not { } g || g.SoMinhas != SoMinhas) return false;
        var porPasta = projetos.ToDictionary(p => p.Principal, StringComparer.OrdinalIgnoreCase);
        var lista = g.Repos.Where(r => porPasta.ContainsKey(r.Pasta)).Select(r => (Projeto: porPasta[r.Pasta], r.Repo, r.Issues)).ToList();
        if (lista.Count == 0) return false;
        var quadro = g.Quadros.FirstOrDefault(q => q.Id == g.Fonte);
        if (quadro is not null && (!quadro.Detalhado || g.Itens is null || VisaoAtual(quadro) != g.Visao)) return false;
        _lista = lista;
        _listaSoMinhas = g.SoMinhas;
        _porRepositorio = lista.Where(x => x.Repo is not null && x.Issues is not null)
            .GroupBy(x => x.Repo!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => MelhorProjeto(x.Key, x.Select(y => y.Projeto)), StringComparer.OrdinalIgnoreCase);
        _trocandoFonte = true;
        Fontes.Clear();
        foreach (var q in g.Quadros)
        {
            Fontes.Add(new FonteIssuesVM(q.Titulo, Idioma.F("quadro · {0}", q.Repositorio), q));
            if (q.Detalhado) _detalhados[q.Id] = q;
        }
        Fontes.Add(new FonteIssuesVM(Idioma.T("Lista simples"), Idioma.T("issues abertas por repositório, sem status"), null));
        Fonte = quadro is null ? Fontes[^1] : Fontes.First(f => f.Quadro?.Id == quadro.Id);
        _trocandoFonte = false;
        if (quadro is null) MostrarListaSimples();
        else
        {
            MostrarVisoes(quadro);
            MostrarItens(g.Itens!);
        }
        Resumo += "  ·  " + Idioma.T("atualizando…");
        return true;
    }

    static string VisaoAtual(QuadroGh quadro)
    {
        var nomes = quadro.Visoes.Count > 0 ? quadro.Visoes.Select(v => v.Nome).ToList() : [Idioma.T("Tudo")];
        var salva = App.Config.VisoesGitHub.GetValueOrDefault(quadro.Id);
        return nomes.FirstOrDefault(n => n == salva) ?? nomes[0];
    }
}
