using System.Globalization;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace Nexora.Testes.Tela;

public sealed partial class PonteAPonteTestes(ITestOutputHelper saida)
{
    readonly record struct Problema(string Comando, string Texto, bool Lentidao)
    {
        public override string ToString() => $"{Comando}: {Texto}";
    }

    static readonly string Pasta = Path.Combine(AppContext.BaseDirectory, "Tela", "Roteiros");

    static int LimiteFrio => Ler("NEXORA_E2E_LIMITE", 1500);
    static int LimiteQuente => Ler("NEXORA_E2E_LIMITE_QUENTE", 800);

    static int Ler(string variavel, int padrao) =>
        int.TryParse(Environment.GetEnvironmentVariable(variavel), CultureInfo.InvariantCulture, out var ms) ? ms : padrao;

    public static TheoryData<string> Roteiros()
    {
        var nomes = new TheoryData<string>();
        foreach (var arquivo in Directory.GetFiles(Pasta, "*.txt").Order()) nomes.Add(Path.GetFileNameWithoutExtension(arquivo));
        return nomes;
    }

    [Theory]
    [MemberData(nameof(Roteiros))]
    public Task Roteiro_roda_sem_erro_clique_perdido_nem_travada(string nome) => Sessao.Rodar(async () =>
    {
        var roteiro = Path.Combine(Pasta, nome + ".txt");
        var guardar = Environment.GetEnvironmentVariable("NEXORA_FOTOS") is { Length: > 0 } fotos ? Path.Combine(fotos, nome) : null;
        var pasta = guardar ?? Directory.CreateTempSubdirectory("nexora-e2e-").FullName;
        var lateral = App.Config.LateralVisivel;
        App.Config.LateralVisivel = true;
        try
        {
            var primeira = await Percorrer(roteiro, pasta);
            Assert.True(!primeira.Any(p => !p.Lentidao), $"{nome}: {string.Join(" | ", primeira.Where(p => !p.Lentidao))}");
            if (primeira.Count == 0) return;
            saida.WriteLine("Passos lentos na primeira vez; o roteiro roda de novo e reprova se os mesmos passos repetirem.");
            var segunda = await Percorrer(roteiro, pasta);
            var repetidos = segunda.Where(p => !p.Lentidao || primeira.Any(q => q.Comando == p.Comando)).ToList();
            Assert.True(repetidos.Count == 0, $"{nome}: {string.Join(" | ", repetidos)}");
        }
        finally
        {
            App.Config.LateralVisivel = lateral;
            if (guardar is null)
                try { Directory.Delete(pasta, true); } catch (IOException) { }
        }
    });

    async Task<List<Problema>> Percorrer(string roteiro, string pasta)
    {
        await new Roteiro(roteiro, pasta).Rodar();
        var log = File.ReadAllLines(Path.Combine(pasta, "log.txt"));
        saida.WriteLine(string.Join(Environment.NewLine, log));
        return [.. Problemas(log, QuenteDesde(File.ReadAllLines(roteiro)))];
    }

    static int QuenteDesde(string[] linhas)
    {
        var indice = Array.FindIndex(linhas, l => l.Trim().Equals("# quente", StringComparison.OrdinalIgnoreCase));
        return indice < 0 ? int.MaxValue : indice + 1;
    }

    static IEnumerable<Problema> Problemas(string[] log, int quenteDesde)
    {
        var passo = 0;
        var comando = "";
        foreach (var linha in log)
        {
            if (Passo().Match(linha) is { Success: true } inicio)
            {
                passo = int.Parse(inicio.Groups[1].Value, CultureInfo.InvariantCulture);
                comando = linha.Trim();
                continue;
            }
            var texto = linha.TrimStart();
            if (texto.StartsWith("ERRO", StringComparison.Ordinal) || texto.StartsWith("INTERCEPTADO", StringComparison.Ordinal) || texto.StartsWith("SEM EFEITO", StringComparison.Ordinal))
            {
                yield return new Problema(comando, texto, false);
                continue;
            }
            if (!texto.StartsWith("TRAVOU", StringComparison.Ordinal) && !texto.StartsWith("LENTO", StringComparison.Ordinal)) continue;
            var limite = passo >= quenteDesde ? LimiteQuente : LimiteFrio;
            if (Milissegundos().Match(texto) is { Success: true } achado && int.Parse(achado.Groups[1].Value, CultureInfo.InvariantCulture) > limite)
                yield return new Problema(comando, $"{texto} (limite {limite} ms)", true);
        }
    }

    [GeneratedRegex(@"^\[(\d+)\] ")]
    private static partial Regex Passo();

    [GeneratedRegex(@"(\d+) ms")]
    private static partial Regex Milissegundos();
}
