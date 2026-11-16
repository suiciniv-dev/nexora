using System.Text.Json;
using System.Text.RegularExpressions;
using Nexora.Core.Claude;
using Nexora.Core.Git;

namespace Nexora.Core.Segredos;

public enum DestinoDoSegredo { ArquivoEnv, Variavel }

public sealed record PedidoDeSegredo(string Id, string Nome, string Motivo, DestinoDoSegredo Destino, string Pasta, string? Arquivo)
{
    public string? Caminho => Arquivo is null ? null : Path.GetFullPath(Path.Combine(Pasta, Arquivo));

    public bool MesmoPedido(PedidoDeSegredo outro) =>
        Nome == outro.Nome && Destino == outro.Destino && string.Equals(Caminho, outro.Caminho, StringComparison.OrdinalIgnoreCase);
}

public static partial class LeituraDoPedido
{
    public const int MotivoMaximo = 400;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,127}$")]
    private static partial Regex NomeValido();

    public static bool NomeAceito(string nome) => NomeValido().IsMatch(nome);

    public static async Task<(PedidoDeSegredo? Pedido, string? Erro)> LerAsync(JsonElement argumentos, string pasta)
    {
        var nome = Protocolo.Texto(argumentos, "nome")?.Trim() ?? "";
        if (!NomeAceito(nome))
            return (null, "O nome precisa ser um identificador de variável, como OPENAI_API_KEY (letras, números e _; não começa com número).");
        var motivo = Protocolo.Texto(argumentos, "motivo")?.Trim() ?? "";
        if (motivo.Length == 0) return (null, "Diga em motivo para que o segredo vai servir; o usuário lê isso antes de decidir.");
        if (motivo.Length > MotivoMaximo) motivo = motivo[..MotivoMaximo];
        var destino = Protocolo.Texto(argumentos, "destino")?.Trim().ToLowerInvariant() switch
        {
            "env" or "arquivo" or ".env" => DestinoDoSegredo.ArquivoEnv,
            "ambiente" or "variavel" or "variável" => DestinoDoSegredo.Variavel,
            _ => (DestinoDoSegredo?)null,
        };
        if (destino is null) return (null, "destino precisa ser \"env\" (uma chave num arquivo .env do projeto) ou \"ambiente\" (variável de ambiente guardada pelo Nexora).");
        string? arquivo = null;
        if (destino == DestinoDoSegredo.ArquivoEnv)
        {
            arquivo = (Protocolo.Texto(argumentos, "arquivo")?.Trim() is { Length: > 0 } a ? a : ".env").Replace('\\', '/');
            if (await ArquivoEnv.ProblemaAsync(pasta, arquivo) is { } problema) return (null, problema);
            arquivo = Path.GetRelativePath(pasta, Path.GetFullPath(Path.Combine(pasta, arquivo))).Replace('\\', '/');
        }
        return (new PedidoDeSegredo(Guid.NewGuid().ToString("N")[..12], nome, motivo, destino.Value, pasta, arquivo), null);
    }
}

public static class ArquivoEnv
{
    public static async Task<string?> ProblemaAsync(string pasta, string arquivo)
    {
        if (Path.IsPathRooted(arquivo)) return "Use um caminho relativo à pasta do projeto em arquivo, como .env ou api/.env.local.";
        var raiz = Path.GetFullPath(pasta).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var caminho = Path.GetFullPath(Path.Combine(pasta, arquivo));
        if (!caminho.StartsWith(raiz, StringComparison.OrdinalIgnoreCase)) return "O arquivo precisa ficar dentro da pasta do projeto.";
        if (!Path.GetFileName(caminho).StartsWith(".env", StringComparison.OrdinalIgnoreCase))
            return "O Nexora só grava segredo em arquivos .env (como .env, .env.local ou api/.env). Para outro lugar, use destino \"ambiente\".";
        if (Directory.Exists(caminho)) return $"{arquivo} é uma pasta.";
        var relativo = Path.GetRelativePath(pasta, caminho).Replace('\\', '/');
        var r = await ComandoGit.ExecutarAsync(pasta, ["check-ignore", "-q", "--", relativo], TimeSpan.FromSeconds(15));
        return r.Codigo switch
        {
            0 => null,
            1 => $"Recusado: {relativo} não está no .gitignore (ou já está no git), então o segredo poderia ir parar num commit. Peça ao usuário para pôr o arquivo no .gitignore, ou use destino \"ambiente\".",
            _ => $"Recusado: não deu para confirmar com o git que {relativo} é ignorado ({Primeira(r.Mensagem)}). Use destino \"ambiente\".",
        };
    }

    static string Primeira(string texto) => texto.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "sem resposta";

    public static void Gravar(string caminho, string chave, string valor)
    {
        var (linhas, quebra, fimComQuebra) = Ler(caminho);
        var nova = $"{chave}={Formatar(valor)}";
        var achou = false;
        for (var i = 0; i < linhas.Count; i++)
        {
            if (!Define(linhas[i], chave)) continue;
            if (achou)
            {
                linhas.RemoveAt(i--);
                continue;
            }
            linhas[i] = linhas[i].TrimStart().StartsWith("export ", StringComparison.Ordinal) ? "export " + nova : nova;
            achou = true;
        }
        if (!achou) linhas.Add(nova);
        Escrever(caminho, linhas, quebra, fimComQuebra || !achou);
    }

    public static bool Remover(string caminho, string chave)
    {
        if (!File.Exists(caminho)) return false;
        var (linhas, quebra, fimComQuebra) = Ler(caminho);
        var antes = linhas.Count;
        linhas.RemoveAll(l => Define(l, chave));
        if (linhas.Count == antes) return false;
        Escrever(caminho, linhas, quebra, fimComQuebra);
        return true;
    }

    public static bool Tem(string caminho, string chave) => File.Exists(caminho) && Ler(caminho).Linhas.Any(l => Define(l, chave));

    static bool Define(string linha, string chave)
    {
        var t = linha.TrimStart();
        if (t.StartsWith("export ", StringComparison.Ordinal)) t = t["export ".Length..].TrimStart();
        if (!t.StartsWith(chave, StringComparison.Ordinal)) return false;
        return t[chave.Length..].TrimStart().StartsWith('=');
    }

    static string Formatar(string valor)
    {
        if (valor.Length > 0 && valor.All(c => char.IsLetterOrDigit(c) || "-_./:+@%,~".Contains(c))) return valor;
        return "\"" + valor.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    }

    static (List<string> Linhas, string Quebra, bool FimComQuebra) Ler(string caminho)
    {
        if (!File.Exists(caminho)) return ([], Environment.NewLine, true);
        var texto = File.ReadAllText(caminho);
        var quebra = texto.Contains("\r\n") ? "\r\n" : "\n";
        var fim = texto.Length == 0 || texto.EndsWith('\n');
        var linhas = texto.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        if (fim && linhas.Count > 0 && linhas[^1].Length == 0) linhas.RemoveAt(linhas.Count - 1);
        return (linhas, quebra, fim);
    }

    static void Escrever(string caminho, List<string> linhas, string quebra, bool fimComQuebra)
    {
        if (Path.GetDirectoryName(caminho) is { Length: > 0 } pasta) Directory.CreateDirectory(pasta);
        var texto = string.Join(quebra, linhas) + (fimComQuebra && linhas.Count > 0 ? quebra : "");
        File.WriteAllText(caminho, texto, new System.Text.UTF8Encoding(false));
    }
}
