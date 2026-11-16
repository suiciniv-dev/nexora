using Nexora.Core.Claude;

namespace Nexora.Testes;

public class IntegracaoTestes
{
    [Fact]
    public async Task ConversaDeVerdadeComOClaudeInstalado()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_INTEGRACAO") != "1") return;

        var pasta = Directory.CreateTempSubdirectory("nexora-integracao-").FullName;
        var eventos = new List<EventoClaude>();
        var fim = new TaskCompletionSource();
        await using (var sessao = new SessaoClaude(new OpcoesSessao(pasta, Modelo: "haiku", Isolada: true)))
        {
            sessao.Evento += e =>
            {
                lock (eventos) eventos.Add(e);
                if (e is TurnoEncerrado) fim.TrySetResult();
            };
            sessao.Iniciar();
            sessao.Enviar("Responda apenas com a palavra nexora, em minúsculas.");
            await fim.Task.WaitAsync(TimeSpan.FromSeconds(120));
            Assert.False(string.IsNullOrEmpty(sessao.SessaoId));
        }

        ApagarRastro(pasta);
        Assert.Contains(eventos, e => e is SessaoIniciada);
        Assert.Contains(eventos, e => e is TextoParcial);
        Assert.Contains(eventos, e => e is TextoCompleto t && t.Texto.Contains("nexora", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(eventos, e => e is TurnoEncerrado { Erro: false });
    }

    [Fact]
    public async Task CopiaDaConversaContinuaComOHistoricoEOutroId()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_INTEGRACAO") != "1") return;

        var pasta = Directory.CreateTempSubdirectory("nexora-copia-").FullName;
        async Task<(string Id, string Texto)> Turno(OpcoesSessao opcoes, string mensagem)
        {
            var fim = new TaskCompletionSource();
            var texto = "";
            await using var sessao = new SessaoClaude(opcoes);
            sessao.Evento += e =>
            {
                if (e is TextoCompleto t) texto += t.Texto;
                if (e is TurnoEncerrado) fim.TrySetResult();
            };
            sessao.Iniciar();
            sessao.Enviar(mensagem);
            await fim.Task.WaitAsync(TimeSpan.FromSeconds(120));
            return (sessao.SessaoId!, texto);
        }

        var original = await Turno(new OpcoesSessao(pasta, Modelo: "haiku", Isolada: true), "Guarde a palavra girassol. Responda só: ok.");
        var copia = await Turno(new OpcoesSessao(pasta, original.Id, Modelo: "haiku", Isolada: true, Bifurcar: true),
            "Qual foi a palavra que eu pedi para guardar? Responda só com ela.");

        ApagarRastro(pasta);
        Assert.NotEqual(original.Id, copia.Id);
        Assert.Contains("girassol", copia.Texto, StringComparison.OrdinalIgnoreCase);
    }

    static void ApagarRastro(string pasta)
    {
        Directory.Delete(pasta, recursive: true);
        var projetos = new DirectoryInfo(DadosIsolados.ClaudeReal);
        foreach (var d in projetos.EnumerateDirectories($"*{Path.GetFileName(pasta).Replace('.', '-')}"))
            d.Delete(recursive: true);
    }
}
