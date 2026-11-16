using Nexora.Core.Historico;

namespace Nexora.Testes;

public sealed class SessoesTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-sessoes-").FullName;

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    [Fact]
    public void LeSessoesAtivasEDescartaProcessoMortoOuArquivoQuebrado()
    {
        File.WriteAllText(Path.Combine(_pasta, "100.json"), """{"pid":100,"sessionId":"s1","cwd":"D:\\Proj","kind":"interactive","entrypoint":"claude-vscode","name":"proj-81","status":"busy","updatedAt":1793312562586}""");
        File.WriteAllText(Path.Combine(_pasta, "200.json"), """{"pid":200,"sessionId":"s2","cwd":"D:\\Outro","entrypoint":"cli","status":"idle"}""");
        File.WriteAllText(Path.Combine(_pasta, "300.json"), """{"pid":300}""");
        File.WriteAllText(Path.Combine(_pasta, "400.json"), "{ quebrado");
        File.WriteAllText(Path.Combine(_pasta, "pipe.key"), "x");

        var lista = SessoesAtivas.Listar(_pasta, pid => pid != 200);
        var s = Assert.Single(lista);
        Assert.Equal(new SessaoAtiva(100, "s1", @"D:\Proj", "proj-81", "claude-vscode", true, DateTimeOffset.FromUnixTimeMilliseconds(1793312562586)), s);
        Assert.Equal("VS Code", s.OrigemLegivel);
        Assert.Equal("terminal", SessoesAtivas.Ler(Path.Combine(_pasta, "200.json"))!.OrigemLegivel);
        Assert.Empty(SessoesAtivas.Listar(Path.Combine(_pasta, "nao-existe")));
    }

    [Fact]
    public void AchaAsConversasRecentesQueJaFecharam()
    {
        var projeto = Directory.CreateDirectory(Path.Combine(_pasta, "D--Proj")).FullName;
        void Conversa(string id, string linhas, TimeSpan idade)
        {
            var arquivo = Path.Combine(projeto, id + ".jsonl");
            File.WriteAllText(arquivo, linhas);
            File.SetLastWriteTimeUtc(arquivo, DateTime.UtcNow - idade);
        }
        Conversa("vscode", """
            {"type":"queue-operation"}
            {"type":"user","cwd":"D:\\Proj","entrypoint":"claude-vscode","isSidechain":false}
            """, TimeSpan.FromMinutes(10));
        Conversa("velha", """{"type":"user","cwd":"D:\\Proj","entrypoint":"cli"}""", TimeSpan.FromHours(3));
        Conversa("lado", """{"type":"user","cwd":"D:\\Proj","entrypoint":"cli","isSidechain":true}""", TimeSpan.FromMinutes(1));
        Conversa("doNexora", """{"type":"user","cwd":"D:\\Proj","entrypoint":"sdk-cli"}""", TimeSpan.FromMinutes(1));
        Conversa("semOrigem", """{"type":"summary"}""", TimeSpan.FromMinutes(1));

        var recentes = SessoesAtivas.Recentes(TimeSpan.FromMinutes(65), id => id == "doNexora", _pasta);

        var s = Assert.Single(recentes);
        Assert.Equal("vscode", s.SessaoId);
        Assert.Equal(@"D:\Proj", s.Pasta);
        Assert.Equal("VS Code", s.OrigemLegivel);
        Assert.True(s.Encerrada);
        Assert.False(s.Ocupada);
        Assert.Empty(SessoesAtivas.Recentes(TimeSpan.FromMinutes(65), _ => false, Path.Combine(_pasta, "nao-existe")));
    }

    [Fact]
    public void HistoricoUsaOHorarioDaUltimaMensagemENaoODoArquivo()
    {
        var arquivo = Path.Combine(_pasta, "conversa.jsonl");
        File.WriteAllText(arquivo, """
            {"type":"user","uuid":"u1","cwd":"D:/Proj","timestamp":"2026-11-07T14:05:00.000Z","message":{"role":"user","content":"oi"}}
            {"type":"assistant","uuid":"a1","timestamp":"2026-11-07T14:06:00.000Z","message":{"role":"assistant","content":[{"type":"text","text":"ola"}]}}
            {"type":"last-prompt","lastPrompt":"oi"}
            {"type":"session-owner"}
            """);
        File.SetLastWriteTimeUtc(arquivo, DateTime.UtcNow);

        var conversa = ConversasSalvas.Resumir(new FileInfo(arquivo))!;
        Assert.Equal(new DateTimeOffset(2026, 11, 7, 14, 6, 0, TimeSpan.Zero).LocalDateTime, conversa.Atualizada);
    }

    [Fact]
    public void SessaoEsperandoGuardaDesdeQuando()
    {
        File.WriteAllText(Path.Combine(_pasta, "500.json"), """{"pid":500,"sessionId":"s5","status":"waiting","waitingFor":"dialog open","updatedAt":1793312562586,"statusUpdatedAt":1793312560000}""");

        var s = SessoesAtivas.Ler(Path.Combine(_pasta, "500.json"))!;
        Assert.True(s.Esperando);
        Assert.False(s.Ocupada);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1793312560000), s.EstadoDesde);
    }
}
