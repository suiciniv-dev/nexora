using Nexora.Core.Git;
using Nexora.Core.Texto;

namespace Nexora.Core.Nuvem;

public sealed record CodigoEnviado(string Pasta, string Branch, string Head, bool TinhaAlteracoes, bool Enviou);

public sealed record CodigoTrazido(string Pasta, string Branch, string Head, bool TinhaAlteracoes);

public static class CodigoNaNuvem
{
    const string RefDeEnvio = "refs/nexora/envio";
    const string RefDeVolta = "refs/nexora/trazer";

    static readonly IReadOnlyDictionary<string, string> Autor = new Dictionary<string, string>
    {
        ["GIT_AUTHOR_NAME"] = "Nexora",
        ["GIT_AUTHOR_EMAIL"] = "nexora@localhost",
        ["GIT_COMMITTER_NAME"] = "Nexora",
        ["GIT_COMMITTER_EMAIL"] = "nexora@localhost",
    };

    const string AutorRemoto = "GIT_AUTHOR_NAME=Nexora GIT_AUTHOR_EMAIL=nexora@localhost GIT_COMMITTER_NAME=Nexora GIT_COMMITTER_EMAIL=nexora@localhost";

    public static string PastaRemota(string home, string projeto, string branch)
    {
        var nome = Repositorio.Slug(Path.GetFileName(projeto.TrimEnd('\\', '/')));
        var ramo = Repositorio.Slug(branch);
        return home.TrimEnd('/') + "/nexora/" + (nome.Length > 0 ? nome : "projeto") + "/" + (ramo.Length > 0 ? ramo : "main");
    }

    public static async Task<CodigoEnviado> EnviarAsync(IMaquinaRemota maquina, string pastaLocal, string pastaRemota, Action<string>? progresso = null, CancellationToken cancelamento = default)
    {
        progresso?.Invoke(Idioma.T("Lendo o código deste PC…"));
        var head = (await Git(pastaLocal, "rev-parse", "HEAD")).Trim();
        var branch = (await Git(pastaLocal, "rev-parse", "--abbrev-ref", "HEAD")).Trim();
        if (branch == "HEAD") branch = "nexora-" + head[..8];
        var (wip, alterado) = await FotografarAsync(pastaLocal, head, "nexora: local changes");
        var consulta = await maquina.RodarAsync($"if [ -d {Shell.Aspas(pastaRemota)}/.git ]; then git -C {Shell.Aspas(pastaRemota)} rev-parse HEAD 2>/dev/null; git -C {Shell.Aspas(pastaRemota)} status --porcelain 2>/dev/null | head -n 1; else echo nenhum; fi", null, cancelamento).ConfigureAwait(false);
        if (!consulta.Ok) throw Falha(consulta);
        var linhas = consulta.Saida.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var remotoHead = linhas.Length > 0 && linhas[0] != "nenhum" ? linhas[0].Trim() : null;
        var remotoSujo = linhas.Length > 1;
        if (remotoHead == head && !alterado)
        {
            progresso?.Invoke(Idioma.T("O código da máquina já está igual."));
            return new CodigoEnviado(pastaRemota, branch, head, remotoSujo, false);
        }
        var bundle = Path.Combine(Path.GetTempPath(), "nexora-envio-" + Guid.NewGuid().ToString("N")[..8] + ".bundle");
        var nomeRemoto = "envio-" + Guid.NewGuid().ToString("N")[..8] + ".bundle";
        try
        {
            await Git(pastaLocal, "update-ref", RefDeEnvio, wip);
            List<string> args = ["bundle", "create", "-q", bundle, RefDeEnvio];
            if (remotoHead is not null && remotoHead != wip && (await ComandoGit.ExecutarAsync(pastaLocal, "cat-file", "-e", remotoHead + "^{commit}")).Ok)
                args.Add("^" + remotoHead);
            var criado = await ComandoGit.ExecutarAsync(pastaLocal, args, TimeSpan.FromMinutes(5));
            if (!criado.Ok)
            {
                args.RemoveAll(a => a.StartsWith('^'));
                criado = await ComandoGit.ExecutarAsync(pastaLocal, args, TimeSpan.FromMinutes(5));
                if (!criado.Ok) throw new InvalidOperationException(criado.Mensagem);
            }
            progresso?.Invoke(Idioma.F("Enviando o código ({0})…", Tamanho(new FileInfo(bundle).Length)));
            var envio = await maquina.EnviarAsync($"mkdir -p \"$HOME/.nexora/tmp\" && cat > \"$HOME/.nexora/tmp/{nomeRemoto}\"", async fluxo =>
            {
                await using var arquivo = File.OpenRead(bundle);
                await arquivo.CopyToAsync(fluxo, cancelamento).ConfigureAwait(false);
            }, cancelamento).ConfigureAwait(false);
            if (!envio.Ok) throw Falha(envio);
            progresso?.Invoke(Idioma.T("Aplicando na máquina…"));
            var origem = (await ComandoGit.ExecutarAsync(pastaLocal, "remote", "get-url", "origin")).Saida.Trim();
            var d = Shell.Aspas(pastaRemota);
            var script = string.Join("\n",
                "set -e",
                $"B=\"$HOME/.nexora/tmp/{nomeRemoto}\"",
                $"if [ ! -d {d}/.git ]; then mkdir -p {d}; git init -q {d}; fi",
                $"cd {d}",
                origem.Length > 0 && !origem.Contains('\'') ? $"git remote get-url origin >/dev/null 2>&1 || git remote add origin {Shell.Aspas(origem)}" : "true",
                $"if [ -n \"$(git status --porcelain 2>/dev/null)\" ]; then {AutorRemoto} git stash push -u -q -m \"nexora: before sync $(date +%Y-%m-%dT%H:%M:%S)\" || true; fi",
                $"git fetch -q \"$B\" {RefDeEnvio}:{RefDeEnvio}",
                $"git checkout -q -f -B {Shell.Aspas(branch)} {wip}",
                $"git reset -q {head}",
                $"git update-ref -d {RefDeEnvio}",
                "rm -f \"$B\"",
                "echo ok");
            var aplicado = await maquina.RodarAsync("sh -s", script + "\n", cancelamento).ConfigureAwait(false);
            if (!aplicado.Ok) throw Falha(aplicado);
            return new CodigoEnviado(pastaRemota, branch, head, alterado, true);
        }
        finally
        {
            await ComandoGit.ExecutarAsync(pastaLocal, "update-ref", "-d", RefDeEnvio);
            try { File.Delete(bundle); } catch (IOException) { }
        }
    }

    public static async Task<CodigoTrazido> TrazerAsync(IMaquinaRemota maquina, string pastaRemota, string repositorioLocal, string pastaDoWorktree, string branchNova, Action<string>? progresso = null, CancellationToken cancelamento = default)
    {
        progresso?.Invoke(Idioma.T("Preparando as mudanças na máquina…"));
        var d = Shell.Aspas(pastaRemota);
        var preparo = string.Join("\n",
            "set -e",
            $"cd {d}",
            "H=$(git rev-parse HEAD)",
            "I=\"$HOME/.nexora/tmp/indice.$$\"",
            "mkdir -p \"$HOME/.nexora/tmp\"",
            "if [ -f \"$(git rev-parse --git-path index)\" ]; then cp \"$(git rev-parse --git-path index)\" \"$I\"; fi",
            "GIT_INDEX_FILE=\"$I\" git add -A",
            "T=$(GIT_INDEX_FILE=\"$I\" git write-tree)",
            "rm -f \"$I\"",
            "if [ \"$T\" = \"$(git rev-parse 'HEAD^{tree}')\" ]; then W=$H; else W=$(" + AutorRemoto + " git commit-tree \"$T\" -p \"$H\" -m 'nexora: changes from the cloud'); fi",
            $"git update-ref {RefDeVolta} \"$W\"",
            "echo \"$H $W\"");
        var preparado = await maquina.RodarAsync("sh -s", preparo + "\n", cancelamento).ConfigureAwait(false);
        if (!preparado.Ok) throw Falha(preparado);
        var partes = preparado.Saida.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length < 2) throw new InvalidOperationException(Idioma.T("A máquina não disse em que ponto o código está."));
        var (head, wip) = (partes[^2], partes[^1]);
        var bundle = Path.Combine(Path.GetTempPath(), "nexora-volta-" + Guid.NewGuid().ToString("N")[..8] + ".bundle");
        try
        {
            var conhecidas = await ConhecidasAsync(repositorioLocal, maquina, pastaRemota, cancelamento);
            var excluir = string.Join(' ', conhecidas.Select(c => "^" + c));
            progresso?.Invoke(Idioma.T("Trazendo o código…"));
            if (!conhecidas.Contains(wip))
            {
                await using (var arquivo = File.Create(bundle))
                {
                    var recebido = await maquina.ReceberAsync($"cd {d} && git bundle create -q - {RefDeVolta} {excluir}", fluxo => fluxo.CopyToAsync(arquivo, cancelamento), cancelamento).ConfigureAwait(false);
                    if (!recebido.Ok) throw Falha(recebido);
                }
                var buscado = await ComandoGit.ExecutarAsync(repositorioLocal, ["fetch", "-q", bundle, $"{RefDeVolta}:{RefDeVolta}"], TimeSpan.FromMinutes(5));
                if (!buscado.Ok) throw new InvalidOperationException(buscado.Mensagem);
            }
            progresso?.Invoke(Idioma.T("Criando o worktree…"));
            var criado = await ComandoGit.ExecutarAsync(repositorioLocal, ["worktree", "add", "-q", "-b", branchNova, pastaDoWorktree, head], TimeSpan.FromMinutes(2));
            if (!criado.Ok) throw new InvalidOperationException(criado.Mensagem);
            if (wip != head)
            {
                var r1 = await ComandoGit.ExecutarAsync(pastaDoWorktree, "reset", "-q", "--hard", wip);
                if (!r1.Ok) throw new InvalidOperationException(r1.Mensagem);
                var r2 = await ComandoGit.ExecutarAsync(pastaDoWorktree, "reset", "-q", head);
                if (!r2.Ok) throw new InvalidOperationException(r2.Mensagem);
            }
            return new CodigoTrazido(pastaDoWorktree, branchNova, head, wip != head);
        }
        finally
        {
            await ComandoGit.ExecutarAsync(repositorioLocal, "update-ref", "-d", RefDeVolta);
            await maquina.RodarAsync($"cd {d} && git update-ref -d {RefDeVolta}", null, CancellationToken.None).ConfigureAwait(false);
            try { File.Delete(bundle); } catch (IOException) { }
        }
    }

    public static async Task<string> BranchLivreAsync(string repositorio, string desejada)
    {
        var nome = desejada;
        for (var i = 2; (await ComandoGit.ExecutarAsync(repositorio, "rev-parse", "--verify", "--quiet", "refs/heads/" + nome)).Ok; i++) nome = desejada + "-" + i;
        return nome;
    }

    static async Task<IReadOnlyList<string>> ConhecidasAsync(string repositorioLocal, IMaquinaRemota maquina, string pastaRemota, CancellationToken cancelamento)
    {
        var locais = await ComandoGit.ExecutarAsync(repositorioLocal, "for-each-ref", "--format=%(objectname)", "--count=50", "--sort=-committerdate", "refs/heads", "refs/remotes");
        if (!locais.Ok) return [];
        var candidatos = locais.Saida.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        if (candidatos.Count == 0) return [];
        var conferido = await maquina.RodarAsync($"cd {Shell.Aspas(pastaRemota)} && for c in {string.Join(' ', candidatos)}; do git cat-file -e \"$c^{{commit}}\" 2>/dev/null && echo \"$c\"; done; true", null, cancelamento).ConfigureAwait(false);
        return conferido.Ok ? [.. conferido.Saida.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)] : [];
    }

    static async Task<(string Commit, bool Alterado)> FotografarAsync(string pasta, string head, string mensagem)
    {
        var indice = Path.Combine(Path.GetTempPath(), "nexora-indice-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var original = (await Git(pasta, "rev-parse", "--path-format=absolute", "--git-path", "index")).Trim();
            if (File.Exists(original)) File.Copy(original, indice);
            var ambiente = new Dictionary<string, string>(Autor) { ["GIT_INDEX_FILE"] = indice };
            var adicionado = await ComandoGit.ExecutarComAmbienteAsync(pasta, ["add", "-A"], ambiente, null, TimeSpan.FromMinutes(2));
            if (!adicionado.Ok) throw new InvalidOperationException(adicionado.Mensagem);
            var arvore = (await ComandoGit.ExecutarComAmbienteAsync(pasta, ["write-tree"], ambiente)).Saida.Trim();
            var daHead = (await Git(pasta, "rev-parse", "HEAD^{tree}")).Trim();
            if (arvore.Length == 0 || arvore == daHead) return (head, false);
            var commit = await ComandoGit.ExecutarComAmbienteAsync(pasta, ["commit-tree", arvore, "-p", head, "-m", mensagem], ambiente);
            if (!commit.Ok) throw new InvalidOperationException(commit.Mensagem);
            return (commit.Saida.Trim(), true);
        }
        finally
        {
            try { File.Delete(indice); } catch (IOException) { }
        }
    }

    static async Task<string> Git(string pasta, params string[] args)
    {
        var r = await ComandoGit.ExecutarAsync(pasta, args);
        if (!r.Ok) throw new InvalidOperationException(r.Mensagem.Length > 0 ? r.Mensagem : Idioma.F("git {0} falhou.", args[0]));
        return r.Saida;
    }

    static Exception Falha(ResultadoRemoto resultado) =>
        resultado.CaiuAConexao ? new ConexaoCaiuException(MaquinaRemota.ExplicarFalha(resultado)) : new InvalidOperationException(MaquinaRemota.ExplicarFalha(resultado));

    static string Tamanho(long bytes) => bytes switch
    {
        < 1024 => bytes + " B",
        < 1024 * 1024 => (bytes / 1024.0).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " KB",
        _ => (bytes / 1024.0 / 1024.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB",
    };
}
