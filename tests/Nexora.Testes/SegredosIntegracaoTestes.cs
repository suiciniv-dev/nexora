using System.Diagnostics;
using Nexora.Core.Claude;
using Nexora.Core.Historico;
using Nexora.Core.Segredos;

namespace Nexora.Testes;

public sealed class SegredosIntegracaoTestes
{
    const string Valor = "sk-integracao-NAO-E-REAL-7d41aa";

    static async Task<(string Id, string Texto, List<EventoClaude> Eventos)> Turno(OpcoesSessao opcoes, string mensagem)
    {
        var fim = new TaskCompletionSource();
        var eventos = new List<EventoClaude>();
        var texto = "";
        await using var sessao = new SessaoClaude(opcoes);
        sessao.Evento += e =>
        {
            lock (eventos) eventos.Add(e);
            if (e is TextoCompleto t) texto += t.Texto;
            if (e is PermissaoPedida p) sessao.Permitir(p.PedidoId, Protocolo.ParaNo(p.Entrada));
            if (e is TurnoEncerrado) fim.TrySetResult();
        };
        sessao.Iniciar();
        sessao.Enviar(mensagem);
        await fim.Task.WaitAsync(TimeSpan.FromSeconds(180));
        return (sessao.SessaoId!, texto, eventos);
    }

    [Fact]
    public async Task ClaudeDeVerdadePedeOSegredoERecebeSoAConfirmacao()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_INTEGRACAO") != "1") return;

        var pasta = Directory.CreateTempSubdirectory("nexora-segredo-real-").FullName;
        using (var git = Process.Start(new ProcessStartInfo("git", "init -q") { WorkingDirectory = pasta, UseShellExecute = false, CreateNoWindow = true })!) git.WaitForExit(20_000);
        File.WriteAllText(Path.Combine(pasta, ".gitignore"), ".env\n");
        using var servico = new ServicoDeSegredos(new CofreDeSegredos(null, new ArmazemNaMemoria()));
        var mostrados = new List<PedidoDeSegredo>();
        var chave = servico.Registrar("real", pasta, p =>
        {
            lock (mostrados) mostrados.Add(p);
            _ = Task.Run(() => servico.SalvarAsync(p.Id, Valor));
        });
        var permitidas = new[] { FerramentaDeSegredo.NomeCompleto, "Bash" };
        try
        {
            var primeiro = await Turno(new OpcoesSessao(pasta, Modelo: "haiku", Isolada: true, Mcp: servico.Configuracao(chave), Permitidas: permitidas),
                "Use a ferramenta pedir_segredo com nome OPENAI_API_KEY, motivo \"teste do Nexora\" e destino env. Depois use de novo com nome DEPLOY_TOKEN, motivo \"teste do Nexora\" e destino ambiente. Responda só com os dois textos que a ferramenta devolveu.");
            var transcrito = File.ReadAllText(ConversasSalvas.ArquivoDe(primeiro.Id, pasta)!);
            Assert.Equal(2, mostrados.Count);
            Assert.Contains("pedir_segredo", transcrito);
            Assert.Contains("Guardado em .env como OPENAI_API_KEY", transcrito);
            Assert.DoesNotContain(Valor, transcrito);
            Assert.DoesNotContain(Valor, primeiro.Texto);
            Assert.Contains($"OPENAI_API_KEY={Valor}", File.ReadAllText(Path.Combine(pasta, ".env")));

            var segundo = await Turno(new OpcoesSessao(pasta, primeiro.Id, Modelo: "haiku", Isolada: true, Mcp: servico.Configuracao(chave), Permitidas: permitidas,
                    Ambiente: servico.Cofre.VariaveisDe(pasta)),
                "Rode no Bash exatamente: echo ${#DEPLOY_TOKEN} e responda só com o número que saiu.");
            Assert.Contains(Valor.Length.ToString(), segundo.Texto);
            Assert.DoesNotContain(Valor, File.ReadAllText(ConversasSalvas.ArquivoDe(segundo.Id, pasta)!));
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
            var projetos = new DirectoryInfo(ConversasSalvas.PastaPadrao);
            foreach (var d in projetos.EnumerateDirectories($"*{Path.GetFileName(pasta).Replace('.', '-')}"))
                d.Delete(recursive: true);
        }
    }
}
