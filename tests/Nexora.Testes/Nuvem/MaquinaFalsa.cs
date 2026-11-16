using Nexora.Core.Git;
using Nexora.Core.Nuvem;

namespace Nexora.Testes.Nuvem;

public sealed class MaquinaFalsa : IDisposable
{
    public const string ClaudeFalso = """
        #!/bin/sh
        [ "$1" = "--version" ] && { echo "9.9.9 (Claude Code falso)"; exit 0; }
        sid="sessao-falsa-1"
        while IFS= read -r linha; do
          case "$linha" in
            *'"subtype":"initialize"'*)
              id=$(printf '%s' "$linha" | sed 's/.*"request_id":"\([^"]*\)".*/\1/')
              echo '{"type":"control_response","response":{"subtype":"success","request_id":"'"$id"'","response":{"commands":[]}}}' ;;
            *'"type":"user"'*)
              texto=$(printf '%s' "$linha" | sed 's/.*"text":"\([^"]*\)".*/\1/')
              echo '{"type":"system","subtype":"init","session_id":"'"$sid"'","model":"claude-falso","cwd":"'"$(pwd)"'","permissionMode":"default","tools":[]}'
              case "$texto" in devagar*) sleep 4;; esac
              echo '{"type":"assistant","message":{"id":"m1","role":"assistant","model":"claude-falso","content":[{"type":"text","text":"eco: '"$texto"' token='"${CLAUDE_CODE_OAUTH_TOKEN:-nenhum}"' arquivos='"$(ls | tr '\n' ',')"'"}]},"session_id":"'"$sid"'"}'
              echo '{"type":"result","subtype":"success","is_error":false,"duration_ms":1,"result":"ok","session_id":"'"$sid"'","total_cost_usd":0}' ;;
          esac
        done
        """;

    readonly string _raiz = Directory.CreateTempSubdirectory("nexora-nuvem-").FullName;

    public MaquinaFalsa()
    {
        Casa = Path.Combine(_raiz, "casa");
        Directory.CreateDirectory(Path.Combine(Casa, ".local", "bin"));
        File.WriteAllText(Path.Combine(Casa, ".local", "bin", "claude"), ClaudeFalso.Replace("\r\n", "\n") + "\n");
        Dados = new MaquinaNaNuvem("falsa", "Máquina de teste", TiposDeMaquina.Teste);
        Maquina = new MaquinaRemota(Dados, new TransporteLocal(Casa));
    }

    public string Raiz => _raiz;
    public string Casa { get; }
    public MaquinaNaNuvem Dados { get; }
    public MaquinaRemota Maquina { get; }

    public static bool Disponivel => TransporteLocal.ShellPadrao() is not null;

    public string Execucao(string id) => Path.Combine(Casa, ".nexora", "execucoes", id);

    public async Task<string> HomeAsync()
    {
        var r = await Maquina.RodarAsync("printf %s \"$HOME\"");
        return r.Saida.Trim();
    }

    public async Task<string> RepositorioAsync(string nome)
    {
        var pasta = Path.Combine(_raiz, nome);
        Directory.CreateDirectory(pasta);
        await Git(pasta, "init", "-q", "-b", "main");
        await Git(pasta, "config", "user.name", "Teste");
        await Git(pasta, "config", "user.email", "teste@exemplo");
        await Git(pasta, "config", "core.autocrlf", "false");
        File.WriteAllText(Path.Combine(pasta, "leia.txt"), "um\n");
        File.WriteAllText(Path.Combine(pasta, ".gitignore"), "ignorado.txt\n");
        await Git(pasta, "add", "-A");
        await Git(pasta, "commit", "-q", "-m", "primeiro");
        return pasta;
    }

    public static string LerSolto(string arquivo)
    {
        using var fluxo = new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd();
    }

    public static async Task<string> Git(string pasta, params string[] args)
    {
        var r = await ComandoGit.ExecutarAsync(pasta, args);
        Assert.True(r.Ok, string.Join(' ', args) + ": " + r.Mensagem);
        return r.Saida.Trim();
    }

    public void Dispose()
    {
        for (var i = 0; i < 5; i++)
            try
            {
                foreach (var arquivo in Directory.EnumerateFiles(_raiz, "*", SearchOption.AllDirectories)) File.SetAttributes(arquivo, FileAttributes.Normal);
                Directory.Delete(_raiz, true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(300);
            }
    }
}
