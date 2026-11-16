using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Nexora.Core.Texto;

namespace Nexora.Core.Git;

public sealed record ArquivoDoTurno(string Caminho, string Status, int Mais, int Menos, bool Binario)
{
    public bool Novo => Status == "A";
    public bool Apagado => Status == "D";
}

public sealed record ResumoDoTurno(string Raiz, IReadOnlyList<ArquivoDoTurno> Arquivos)
{
    public int Mais => Arquivos.Sum(a => a.Mais);
    public int Menos => Arquivos.Sum(a => a.Menos);
}

public sealed record PontoSalvo(string Ref, int Turno, string Tipo, string Commit, DateTimeOffset Quando, IReadOnlyDictionary<string, string> Dados);

public sealed record Restauracao(bool Ok, string Mensagem, int Escritos = 0, int Apagados = 0);

public static class PontosDeRestauracao
{
    public const string Raizes = "refs/nexora/";
    public const string Prefixo = "refs/nexora/checkpoints/";
    public const string PrefixoDesfeitos = "refs/nexora/desfeitos/";
    public const string Antes = "antes";
    public const string Depois = "depois";
    const string Gitlink = "160000";

    static readonly Dictionary<string, string> Autoria = new()
    {
        ["GIT_AUTHOR_NAME"] = "Nexora",
        ["GIT_AUTHOR_EMAIL"] = "nexora@localhost",
        ["GIT_COMMITTER_NAME"] = "Nexora",
        ["GIT_COMMITTER_EMAIL"] = "nexora@localhost",
    };

    public static string? Raiz(string pasta)
    {
        try
        {
            for (var dir = new DirectoryInfo(pasta); dir is not null; dir = dir.Parent)
            {
                var git = Path.Combine(dir.FullName, ".git");
                if (Directory.Exists(git) || File.Exists(git)) return dir.FullName;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
        }
        return null;
    }

    public static bool IdValido(string? id) => id is { Length: > 0 and <= 128 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public static string RefDe(string sessao, int turno, string tipo) => $"{Prefixo}{sessao}/{turno}/{tipo}";

    public static string RefDesfeito(string sessao) => $"{PrefixoDesfeitos}{sessao}/{DateTime.UtcNow:yyyyMMddHHmmssfff}";

    public static string Impressao(string texto)
    {
        var normal = texto.Replace("\r\n", "\n").Trim();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normal)))[..16];
    }

    public static string Mensagem(string titulo, IReadOnlyDictionary<string, string?> dados)
    {
        var texto = new StringBuilder(titulo).Append("\n\n");
        foreach (var (chave, valor) in dados)
            if (!string.IsNullOrEmpty(valor)) texto.Append(chave).Append(": ").Append(valor.Replace('\n', ' ').Replace('\r', ' ')).Append('\n');
        return texto.ToString();
    }

    public static IReadOnlyDictionary<string, string> LerDados(string mensagem)
    {
        var dados = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var linha in mensagem.Split('\n'))
        {
            var dois = linha.IndexOf(": ", StringComparison.Ordinal);
            if (dois <= 0) continue;
            var chave = linha[..dois];
            if (chave.All(c => char.IsAsciiLetterLower(c) || c == '-')) dados[chave] = linha[(dois + 2)..].TrimEnd('\r');
        }
        return dados;
    }

    static async Task<string?> RaizDoGitAsync(string pasta)
    {
        var r = await ComandoGit.ExecutarAsync(pasta, "rev-parse", "--show-toplevel");
        if (!r.Ok || r.Saida.Trim() is not { Length: > 0 } raiz) return null;
        return Path.GetFullPath(raiz);
    }

    static string Temporario() => Path.Combine(Path.GetTempPath(), $"nexora-indice-{Guid.NewGuid():N}");

    static void Apagar(string arquivo)
    {
        foreach (var caminho in new[] { arquivo, arquivo + ".lock" })
            try
            {
                if (File.Exists(caminho)) File.Delete(caminho);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
    }

    static async Task<(string Raiz, string Arvore)?> ArvoreAtualAsync(string pasta)
    {
        if (await RaizDoGitAsync(pasta) is not { } raiz) return null;
        var indice = await ComandoGit.ExecutarAsync(raiz, "rev-parse", "--git-path", "index");
        var real = indice.Ok && indice.Saida.Trim() is { Length: > 0 } caminho ? Path.GetFullPath(Path.Combine(raiz, caminho)) : null;
        foreach (var semear in new[] { true, false })
        {
            if (semear && real is null) continue;
            var temporario = Temporario();
            try
            {
                if (semear)
                    try
                    {
                        if (!File.Exists(real)) continue;
                        File.Copy(real!, temporario);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        continue;
                    }
                var ambiente = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = temporario };
                var add = await ComandoGit.ExecutarComAmbienteAsync(raiz, ["-c", "advice.addEmbeddedRepo=false", "add", "--all"], ambiente, limite: TimeSpan.FromMinutes(3));
                if (!add.Ok) continue;
                var arvore = await ComandoGit.ExecutarComAmbienteAsync(raiz, ["write-tree"], ambiente, limite: TimeSpan.FromMinutes(1));
                if (arvore.Ok && arvore.Saida.Trim() is { Length: > 0 } sha) return (raiz, sha);
            }
            finally
            {
                Apagar(temporario);
            }
        }
        return null;
    }

    static async Task<string?> CommitAsync(string raiz, string arvore, string mensagem, string? pai)
    {
        List<string> args = ["commit-tree", "--no-gpg-sign", "-m", mensagem];
        if (pai is not null) args.AddRange(["-p", pai]);
        args.Add(arvore);
        var r = await ComandoGit.ExecutarComAmbienteAsync(raiz, args, Autoria);
        return r.Ok && r.Saida.Trim() is { Length: > 0 } sha ? sha : null;
    }

    public static async Task<string?> CapturarAsync(string pasta, string mensagem, string? pai = null)
    {
        if (await ArvoreAtualAsync(pasta) is not { } atual) return null;
        return await CommitAsync(atual.Raiz, atual.Arvore, mensagem, pai);
    }

    public static async Task<bool> GuardarAsync(string pasta, IEnumerable<(string Ref, string Commit)> refs)
    {
        var linhas = new StringBuilder();
        foreach (var (nome, commit) in refs)
            if (nome.StartsWith(Raizes, StringComparison.Ordinal)) linhas.Append("update ").Append(nome).Append(' ').Append(commit).Append('\n');
        if (linhas.Length == 0) return true;
        var r = await ComandoGit.ExecutarComAmbienteAsync(pasta, ["update-ref", "--stdin"], null, linhas.ToString());
        return r.Ok;
    }

    static async Task<IReadOnlyList<(string Ref, string Commit, DateTimeOffset Quando, string Mensagem)>> RefsAsync(string pasta, string prefixo)
    {
        var r = await ComandoGit.ExecutarAsync(pasta, "for-each-ref", "--format=%(refname)%00%(objectname)%00%(committerdate:unix)%00%(contents)%1e", prefixo);
        if (!r.Ok) return [];
        var lista = new List<(string, string, DateTimeOffset, string)>();
        foreach (var registro in r.Saida.Split('\x1e'))
        {
            var partes = registro.TrimStart('\n', '\r').Split('\0');
            if (partes.Length < 4 || partes[0].Length == 0) continue;
            var quando = long.TryParse(partes[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix) ? DateTimeOffset.FromUnixTimeSeconds(unix) : DateTimeOffset.MinValue;
            lista.Add((partes[0], partes[1], quando, partes[3]));
        }
        return lista;
    }

    public static async Task<IReadOnlyList<PontoSalvo>> ListarAsync(string pasta, string sessao)
    {
        if (!IdValido(sessao)) return [];
        var prefixo = $"{Prefixo}{sessao}/";
        var lista = new List<PontoSalvo>();
        foreach (var (nome, commit, quando, mensagem) in await RefsAsync(pasta, prefixo))
        {
            var partes = nome[prefixo.Length..].Split('/');
            if (partes.Length != 2 || !int.TryParse(partes[0], NumberStyles.None, CultureInfo.InvariantCulture, out var turno)) continue;
            lista.Add(new PontoSalvo(nome, turno, partes[1], commit, quando, LerDados(mensagem)));
        }
        return [.. lista.OrderBy(p => p.Turno).ThenBy(p => p.Tipo == Depois)];
    }

    static async Task<int> ApagarRefsAsync(string pasta, IEnumerable<string> refs)
    {
        var lista = refs.Where(r => r.StartsWith(Raizes, StringComparison.Ordinal)).ToList();
        if (lista.Count == 0) return 0;
        var entrada = string.Concat(lista.Select(r => $"delete {r}\n"));
        var r = await ComandoGit.ExecutarComAmbienteAsync(pasta, ["update-ref", "--stdin"], null, entrada);
        return r.Ok ? lista.Count : 0;
    }

    public static async Task<int> ApagarAsync(string pasta, string sessao, int aPartirDe = 0)
    {
        if (!IdValido(sessao)) return 0;
        var pontos = await ListarAsync(pasta, sessao);
        var refs = pontos.Where(p => p.Turno >= aPartirDe).Select(p => p.Ref).ToList();
        if (aPartirDe <= 0) refs.AddRange((await RefsAsync(pasta, $"{PrefixoDesfeitos}{sessao}/")).Select(r => r.Ref));
        return await ApagarRefsAsync(pasta, refs);
    }

    public static async Task<int> ApagarSessoesAsync(string pasta, IReadOnlySet<string> sessoes)
    {
        string? Sessao(string nome)
        {
            var resto = nome.StartsWith(Prefixo, StringComparison.Ordinal) ? nome[Prefixo.Length..]
                : nome.StartsWith(PrefixoDesfeitos, StringComparison.Ordinal) ? nome[PrefixoDesfeitos.Length..] : null;
            return resto?.Split('/')[0];
        }
        return await ApagarRefsAsync(pasta, (await RefsAsync(pasta, Raizes)).Where(r => Sessao(r.Ref) is { } s && sessoes.Contains(s)).Select(r => r.Ref));
    }

    public static async Task<int> ApagarTodosAsync(string pasta) =>
        await ApagarRefsAsync(pasta, (await RefsAsync(pasta, Raizes)).Select(r => r.Ref));

    public static async Task<int> PodarAsync(string pasta, TimeSpan idade, DateTimeOffset? agora = null)
    {
        var limite = (agora ?? DateTimeOffset.UtcNow) - idade;
        return await ApagarRefsAsync(pasta, (await RefsAsync(pasta, Raizes)).Where(r => r.Quando < limite).Select(r => r.Ref));
    }

    public static async Task<ResumoDoTurno?> ContarAsync(string pasta, string antes, string depois)
    {
        if (await RaizDoGitAsync(pasta) is not { } raiz) return null;
        var numeros = await ComandoGit.ExecutarAsync(raiz, "diff", "--numstat", "-z", "--no-renames", "--ignore-submodules=all", antes, depois);
        var estados = await ComandoGit.ExecutarAsync(raiz, "diff", "--name-status", "-z", "--no-renames", "--ignore-submodules=all", antes, depois);
        if (!numeros.Ok || !estados.Ok) return null;
        var linhas = new Dictionary<string, (int Mais, int Menos, bool Binario)>(StringComparer.Ordinal);
        foreach (var registro in numeros.Saida.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = registro.TrimStart('\n').Split('\t', 3);
            if (partes.Length < 3) continue;
            var binario = partes[0] == "-" && partes[1] == "-";
            linhas[partes[2]] = (int.TryParse(partes[0], out var mais) ? mais : 0, int.TryParse(partes[1], out var menos) ? menos : 0, binario);
        }
        var arquivos = new List<ArquivoDoTurno>();
        var campos = estados.Saida.Split('\0');
        for (var i = 0; i + 1 < campos.Length; i += 2)
        {
            var status = campos[i].Trim();
            var caminho = campos[i + 1];
            if (status.Length == 0 || caminho.Length == 0 || Alteracoes.DeAgente(caminho)) continue;
            var (mais, menos, binario) = linhas.GetValueOrDefault(caminho);
            arquivos.Add(new ArquivoDoTurno(caminho, status[..1], mais, menos, binario));
        }
        return new ResumoDoTurno(raiz, arquivos);
    }

    public static async Task<string> DiffAsync(string raiz, string antes, string depois, string caminho)
    {
        var r = await ComandoGit.ExecutarAsync(raiz, "diff", "--no-color", "--no-renames", antes, depois, "--", caminho);
        return r.Ok ? r.Saida : r.Mensagem;
    }

    public static bool CaminhoSeguro(string caminho)
    {
        if (caminho.Length == 0 || caminho.Contains('\\') || caminho.Contains(':') || caminho.Contains('\0') || caminho.StartsWith('/')) return false;
        foreach (var parte in caminho.Split('/'))
            if (parte.Length == 0 || parte is "." or ".." || parte.Equals(".git", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    sealed record Mudanca(string ModoAntes, string ModoDepois, char Status, string Caminho);

    static List<Mudanca> LerDiffTree(string saida)
    {
        var lista = new List<Mudanca>();
        var campos = saida.Split('\0');
        for (var i = 0; i + 1 < campos.Length; i += 2)
        {
            var cabeca = campos[i].TrimStart('\n');
            if (!cabeca.StartsWith(':')) continue;
            var partes = cabeca[1..].Split(' ');
            if (partes.Length < 5 || partes[4].Length == 0) continue;
            lista.Add(new Mudanca(partes[0], partes[1], partes[4][0], campos[i + 1]));
        }
        return lista;
    }

    static string Completo(string raiz, string caminho) => Path.GetFullPath(Path.Combine(raiz, caminho.Replace('/', Path.DirectorySeparatorChar)));

    static bool Dentro(string raiz, string completo) =>
        completo.StartsWith(raiz.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public static async Task<Restauracao> RestaurarAsync(string pasta, string commit, string? guardarAtualEm = null)
    {
        if (await ArvoreAtualAsync(pasta) is not { } atual)
            return new Restauracao(false, Idioma.T("não deu para ler o estado atual dos arquivos"));
        var (raiz, arvoreAtual) = atual;
        var alvo = await ComandoGit.ExecutarAsync(raiz, "rev-parse", "--verify", "--quiet", commit + "^{tree}");
        if (!alvo.Ok || alvo.Saida.Trim() is not { Length: > 0 } arvoreAlvo)
            return new Restauracao(false, Idioma.T("o ponto de restauração não existe mais"));
        if (guardarAtualEm is not null && await CommitAsync(raiz, arvoreAtual, "nexora: antes de desfazer", null) is { } guardado)
            await GuardarAsync(raiz, [(guardarAtualEm, guardado)]);
        if (arvoreAtual == arvoreAlvo) return new Restauracao(true, "");
        var diff = await ComandoGit.ExecutarAsync(raiz, "diff-tree", "-r", "-z", "--no-renames", arvoreAtual, arvoreAlvo);
        if (!diff.Ok) return new Restauracao(false, diff.Mensagem);
        var apagar = new List<string>();
        var escrever = new List<(string Caminho, bool Novo)>();
        foreach (var m in LerDiffTree(diff.Saida))
        {
            if (!CaminhoSeguro(m.Caminho) || !Dentro(raiz, Completo(raiz, m.Caminho))) continue;
            if (m.ModoAntes == Gitlink || m.ModoDepois == Gitlink) continue;
            switch (m.Status)
            {
                case 'D':
                    apagar.Add(m.Caminho);
                    break;
                case 'A':
                    escrever.Add((m.Caminho, true));
                    break;
                case 'M' or 'T':
                    escrever.Add((m.Caminho, false));
                    break;
            }
        }
        var apagados = 0;
        try
        {
            foreach (var caminho in apagar)
            {
                var completo = Completo(raiz, caminho);
                if (!File.Exists(completo)) continue;
                File.SetAttributes(completo, FileAttributes.Normal);
                File.Delete(completo);
                apagados++;
                LimparPastasVazias(raiz, Path.GetDirectoryName(completo));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new Restauracao(false, ex.Message, 0, apagados);
        }
        var paraEscrever = escrever
            .Where(e => !e.Novo || !File.Exists(Completo(raiz, e.Caminho)) && !Directory.Exists(Completo(raiz, e.Caminho)))
            .Select(e => e.Caminho)
            .ToList();
        if (paraEscrever.Count == 0) return new Restauracao(true, "", 0, apagados);
        var temporario = Temporario();
        try
        {
            var ambiente = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = temporario };
            var lido = await ComandoGit.ExecutarComAmbienteAsync(raiz, ["read-tree", arvoreAlvo], ambiente, limite: TimeSpan.FromMinutes(1));
            if (!lido.Ok) return new Restauracao(false, lido.Mensagem, 0, apagados);
            var entrada = string.Concat(paraEscrever.Select(c => c + "\0"));
            var saida = await ComandoGit.ExecutarComAmbienteAsync(raiz, ["checkout-index", "-f", "-z", "--stdin"], ambiente, entrada, TimeSpan.FromMinutes(3));
            return saida.Ok ? new Restauracao(true, "", paraEscrever.Count, apagados) : new Restauracao(false, saida.Mensagem, 0, apagados);
        }
        finally
        {
            Apagar(temporario);
        }
    }

    static void LimparPastasVazias(string raiz, string? pasta)
    {
        while (pasta is not null && Dentro(raiz, pasta) && !Path.GetFileName(pasta).Equals(".git", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (!Directory.Exists(pasta) || Directory.EnumerateFileSystemEntries(pasta).Any()) return;
                Directory.Delete(pasta);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return;
            }
            pasta = Path.GetDirectoryName(pasta);
        }
    }
}
