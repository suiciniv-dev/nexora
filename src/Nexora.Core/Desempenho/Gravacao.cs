using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Nexora.Core.Desempenho;

public sealed class Gravacao(string pasta, int diasGuardados = Gravacao.DiasPadrao)
{
    public const int DiasPadrao = 30;
    public const long LimiteDoArquivo = 4L << 20;
    public const long LimiteDeTravadas = 2L << 20;
    const string PrefixoDeAmostras = "amostras-";
    const string PrefixoDeTravadas = "travadas-";
    const string ExtensaoDeAmostras = ".jsonl";
    const string PrefixoDoDia = "dia-";
    const string ExtensaoDoDia = ".json";

    public string Pasta { get; } = pasta;

    string ArquivoDeAmostras(DateOnly dia) => Path.Combine(Pasta, $"{PrefixoDeAmostras}{Data(dia)}{ExtensaoDeAmostras}");
    string ArquivoDoDia(DateOnly dia) => Path.Combine(Pasta, $"{PrefixoDoDia}{Data(dia)}{ExtensaoDoDia}");
    public string ArquivoDeTravadas(DateOnly dia) => Path.Combine(Pasta, $"{PrefixoDeTravadas}{Data(dia)}{ExtensaoDeAmostras}");
    static string Data(DateOnly dia) => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public bool Acrescentar(Amostra amostra) => AcrescentarLinha(ArquivoDeAmostras(DateOnly.FromDateTime(amostra.Hora)), amostra, LimiteDoArquivo);

    public bool AcrescentarTravada(Travada travada) => AcrescentarLinha(ArquivoDeTravadas(DateOnly.FromDateTime(travada.Hora)), travada, LimiteDeTravadas);

    bool AcrescentarLinha<T>(string caminho, T valor, long limite)
    {
        Directory.CreateDirectory(Pasta);
        if (new FileInfo(caminho) is { Exists: true } info && info.Length >= limite) return false;
        var linha = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(valor) + "\n");
        using var arquivo = new FileStream(caminho, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        arquivo.Write(linha);
        return true;
    }

    public List<Amostra> LerAmostras(DateOnly dia) => LerLinhas<Amostra>(ArquivoDeAmostras(dia));

    public List<Travada> LerTravadas(DateOnly dia) => LerLinhas<Travada>(ArquivoDeTravadas(dia));

    static List<T> LerLinhas<T>(string arquivo)
    {
        var lista = new List<T>();
        if (!File.Exists(arquivo)) return lista;
        try
        {
            using var leitor = new StreamReader(new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
            while (leitor.ReadLine() is { } linha)
            {
                if (linha.Length == 0) continue;
                try
                {
                    if (JsonSerializer.Deserialize<T>(linha) is { } item) lista.Add(item);
                }
                catch (JsonException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return lista;
    }

    public ResumoDoDia? LerDia(DateOnly dia)
    {
        try
        {
            var arquivo = ArquivoDoDia(dia);
            return File.Exists(arquivo) ? JsonSerializer.Deserialize<ResumoDoDia>(File.ReadAllText(arquivo)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public IReadOnlyList<ResumoDoDia> LerDias(DateOnly desde, DateOnly ate)
    {
        var lista = new List<ResumoDoDia>();
        for (var dia = desde; dia <= ate; dia = dia.AddDays(1))
            if (LerDia(dia) is { } resumo) lista.Add(resumo);
        return lista;
    }

    public ResumoDoDia? Fechar(DateOnly dia)
    {
        var amostras = LerAmostras(dia);
        ResumoDoDia? resumo = null;
        try
        {
            if (amostras.Count > 0)
            {
                resumo = Agregacao.Resumir(dia, amostras, LerTravadas(dia));
                var temporario = ArquivoDoDia(dia) + ".tmp";
                File.WriteAllText(temporario, JsonSerializer.Serialize(resumo));
                File.Move(temporario, ArquivoDoDia(dia), true);
            }
            File.Delete(ArquivoDeAmostras(dia));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return resumo;
    }

    public void Arrumar(DateOnly hoje)
    {
        foreach (var (dia, arquivo) in Arquivos())
            if (Path.GetFileName(arquivo).StartsWith(PrefixoDeAmostras, StringComparison.Ordinal) && dia < hoje) Fechar(dia);
        Limpar(hoje);
    }

    public int Limpar(DateOnly hoje)
    {
        var limite = hoje.AddDays(-diasGuardados);
        var apagados = 0;
        foreach (var (dia, arquivo) in Arquivos())
        {
            if (dia > limite) continue;
            try
            {
                File.Delete(arquivo);
                apagados++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
        return apagados;
    }

    IEnumerable<(DateOnly Dia, string Arquivo)> Arquivos()
    {
        if (!Directory.Exists(Pasta)) yield break;
        foreach (var arquivo in Directory.GetFiles(Pasta))
        {
            var nome = Path.GetFileName(arquivo);
            var data = nome switch
            {
                _ when nome.StartsWith(PrefixoDeAmostras, StringComparison.Ordinal) && nome.EndsWith(ExtensaoDeAmostras, StringComparison.Ordinal)
                    => nome[PrefixoDeAmostras.Length..^ExtensaoDeAmostras.Length],
                _ when nome.StartsWith(PrefixoDeTravadas, StringComparison.Ordinal) && nome.EndsWith(ExtensaoDeAmostras, StringComparison.Ordinal)
                    => nome[PrefixoDeTravadas.Length..^ExtensaoDeAmostras.Length],
                _ when nome.StartsWith(PrefixoDoDia, StringComparison.Ordinal) && nome.EndsWith(ExtensaoDoDia, StringComparison.Ordinal)
                    => nome[PrefixoDoDia.Length..^ExtensaoDoDia.Length],
                _ => null,
            };
            if (data is not null && DateOnly.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia))
                yield return (dia, arquivo);
        }
    }
}
