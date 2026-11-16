using Nexora.Core.Historico;

namespace Nexora.Testes;

public sealed class VersoesDeConversaTestes : IDisposable
{
    readonly string _raiz = Directory.CreateTempSubdirectory("nexora-versoes-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_raiz, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    string Gravar(string sessao, DateTime quando, params string[] uuids)
    {
        var pasta = Path.Combine(_raiz, "D--Proj");
        Directory.CreateDirectory(pasta);
        var arquivo = Path.Combine(pasta, sessao + ".jsonl");
        File.WriteAllLines(arquivo, uuids.Select((u, i) => i % 2 == 0
            ? $$$"""{"type":"user","uuid":"{{{u}}}","sessionId":"{{{sessao}}}","cwd":"D:\\Proj","message":{"role":"user","content":"pedido {{{i}}}"}}"""
            : $$$"""{"type":"assistant","uuid":"{{{u}}}","sessionId":"{{{sessao}}}","message":{"content":[{"type":"text","text":"resposta {{{i}}}"}]}}"""));
        File.SetLastWriteTime(arquivo, quando);
        return arquivo;
    }

    [Fact]
    public void CopiaQueContemAOriginalEscondeAOriginal()
    {
        var agora = DateTime.Now;
        Gravar("original", agora.AddMinutes(-40), "u1", "u2", "u3");
        Gravar("copia", agora, "u1", "u2", "u3", "u4", "u5");
        Gravar("outra", agora.AddMinutes(-10), "x1", "x2");

        var lista = ConversasSalvas.Listar(_raiz);
        var antigas = VersoesDeConversa.Antigas(lista);

        Assert.Equal("u1", lista.Single(c => c.SessaoId == "original").PrimeiraMensagem);
        Assert.Equal("u3", lista.Single(c => c.SessaoId == "original").UltimaMensagem);
        Assert.Equal("copia", Assert.Single(antigas).Value);
        Assert.Equal("original", antigas.Single().Key);
    }

    [Fact]
    public void PrimeiraMensagemEnormeNaoEscondeALinhagem()
    {
        var agora = DateTime.Now;
        var pasta = Path.Combine(_raiz, "D--Proj");
        Directory.CreateDirectory(pasta);
        var imagem = new string('A', 700 * 1024);
        string Primeira(string sessao) =>
            $$$"""{"type":"user","uuid":"grande","sessionId":"{{{sessao}}}","cwd":"D:\\Proj","message":{"role":"user","content":[{"type":"image","source":{"type":"base64","media_type":"image/png","data":"{{{imagem}}}"}},{"type":"text","text":"olha isso"}]}}""";
        string Resposta(string sessao, string uuid) =>
            $$$"""{"type":"assistant","uuid":"{{{uuid}}}","sessionId":"{{{sessao}}}","message":{"content":[{"type":"text","text":"ok"}]}}""";
        File.WriteAllLines(Path.Combine(pasta, "original.jsonl"), [Primeira("original"), Resposta("original", "r1")]);
        File.WriteAllLines(Path.Combine(pasta, "copia.jsonl"), [Primeira("copia"), Resposta("copia", "r1"), Resposta("copia", "r2")]);
        File.SetLastWriteTime(Path.Combine(pasta, "original.jsonl"), agora.AddMinutes(-30));

        var antigas = VersoesDeConversa.Antigas(ConversasSalvas.Listar(_raiz));

        Assert.Equal("copia", Assert.Single(antigas).Value);
    }

    [Fact]
    public void ConversasQueDivergiramContinuamAsDuas()
    {
        var agora = DateTime.Now;
        Gravar("original", agora.AddMinutes(-5), "u1", "u2", "u3", "seguiu-no-vscode");
        Gravar("copia", agora, "u1", "u2", "u3", "seguiu-no-nexora");

        Assert.Empty(VersoesDeConversa.Antigas(ConversasSalvas.Listar(_raiz)));
    }

    [Fact]
    public void CadeiaDeCopiasEscondeTodasAsAntigas()
    {
        var agora = DateTime.Now;
        Gravar("v1", agora.AddMinutes(-30), "u1", "u2");
        Gravar("v2", agora.AddMinutes(-20), "u1", "u2", "u3", "u4");
        Gravar("v3", agora, "u1", "u2", "u3", "u4", "u5", "u6");

        var antigas = VersoesDeConversa.Antigas(ConversasSalvas.Listar(_raiz));

        Assert.Equal(2, antigas.Count);
        Assert.Equal("v3", antigas["v2"]);
        Assert.Contains(antigas["v1"], new[] { "v2", "v3" });
    }
}
