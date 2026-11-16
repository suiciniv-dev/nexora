using System.Text.RegularExpressions;

namespace Nexora.Core.Revisao;

public enum TipoVerificacao { Teste, Build }

public sealed record PassoDaSessao(string Ferramenta, string? Comando, bool Concluido, bool Erro, bool Edita);

public sealed record Verificacao(TipoVerificacao Tipo, string Comando, bool Passou, int Posicao);

public sealed record ResumoDaRevisao(Verificacao? Teste, Verificacao? Build, bool Editou, bool EditouDepoisDoTeste, bool EditouDepoisDoBuild)
{
    public bool TemAlgo => Editou || Teste is not null || Build is not null;
}

public static partial class Verificacoes
{
    [GeneratedRegex(@"\b(dotnet\s+test|(npm|pnpm|yarn|bun)\s+(run\s+)?test|npx\s+(jest|vitest)|pytest|python\s+-m\s+(pytest|unittest)|go\s+test|cargo\s+test|mvnw?\s+(\S+\s+)*(test|verify)|gradlew?\s+(\S+\s+)*test|phpunit|rspec)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ComandoDeTeste();

    [GeneratedRegex(@"\b(dotnet\s+(build|publish)|msbuild|(npm|pnpm|yarn|bun)\s+(run\s+)?build|npx\s+tsc|tsc\b|cargo\s+build|go\s+build|mvnw?\s+(\S+\s+)*(compile|package|install)|gradlew?\s+(\S+\s+)*(build|assemble))\b", RegexOptions.IgnoreCase)]
    private static partial Regex ComandoDeBuild();

    public static TipoVerificacao? Classificar(string? comando) =>
        comando is not { Length: > 0 } c ? null
        : ComandoDeTeste().IsMatch(c) ? TipoVerificacao.Teste
        : ComandoDeBuild().IsMatch(c) ? TipoVerificacao.Build
        : null;

    public static ResumoDaRevisao Resumir(IReadOnlyList<PassoDaSessao> passos)
    {
        Verificacao? teste = null, build = null;
        int ultimaEdicao = -1;
        for (var i = 0; i < passos.Count; i++)
        {
            var passo = passos[i];
            if (passo.Edita && passo.Concluido && !passo.Erro) ultimaEdicao = i;
            if (!passo.Concluido || !string.Equals(passo.Ferramenta, "Bash", StringComparison.OrdinalIgnoreCase)) continue;
            switch (Classificar(passo.Comando))
            {
                case TipoVerificacao.Teste:
                    teste = new Verificacao(TipoVerificacao.Teste, passo.Comando!, !passo.Erro, i);
                    break;
                case TipoVerificacao.Build:
                    build = new Verificacao(TipoVerificacao.Build, passo.Comando!, !passo.Erro, i);
                    break;
            }
        }
        return new ResumoDaRevisao(teste, build, ultimaEdicao >= 0,
            ultimaEdicao >= 0 && (teste is null || ultimaEdicao > teste.Posicao),
            ultimaEdicao >= 0 && (build is null || ultimaEdicao > build.Posicao));
    }
}
