using System.Text;
using Nexora.ViewModels;

namespace Nexora.Testes;

public sealed class EditorTestes : IDisposable
{
    readonly string _raiz = Directory.CreateTempSubdirectory("nexora-editor-").FullName;

    static EditorTestes() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public void Dispose() => Directory.Delete(_raiz, recursive: true);

    ArquivoVM Abrir(string nome, byte[] bytes)
    {
        var caminho = Path.Combine(_raiz, nome);
        File.WriteAllBytes(caminho, bytes);
        return new ArquivoVM(caminho, _raiz, _ => { }, _ => { }, _ => { });
    }

    static byte[] Utf8(string texto, bool bom) => new UTF8Encoding(bom).GetPreamble().Concat(Encoding.UTF8.GetBytes(texto)).ToArray();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SalvarMantemOBomDoUtf8EAsQuebrasDeLinha(bool bom)
    {
        var arquivo = Abrir("a.cs", Utf8("// ação\r\nclass A {}\r\n", bom));
        Assert.Equal("// ação\r\nclass A {}\r\n", arquivo.Documento.Text);
        Assert.False(arquivo.Modificada);

        arquivo.Documento.Insert(arquivo.Documento.TextLength, "// fim\r\n");
        Assert.True(arquivo.Modificada);
        arquivo.Salvar();
        arquivo.Descartar();

        Assert.False(arquivo.Modificada);
        Assert.Equal(Utf8("// ação\r\nclass A {}\r\n// fim\r\n", bom), File.ReadAllBytes(Path.Combine(_raiz, "a.cs")));
    }

    [Fact]
    public void ArquivoEmWindows1252ContinuaEm1252()
    {
        var cp1252 = Encoding.GetEncoding(1252);
        var arquivo = Abrir("legado.cs", cp1252.GetBytes("// configuração\n"));
        Assert.Equal("// configuração\n", arquivo.Documento.Text);

        arquivo.Documento.Insert(0, "// início\n");
        arquivo.Salvar();
        arquivo.Descartar();
        Assert.Equal(cp1252.GetBytes("// início\n// configuração\n"), File.ReadAllBytes(Path.Combine(_raiz, "legado.cs")));
    }

    [Fact]
    public void BinarioEArquivoInexistenteNaoAbremNoEditor()
    {
        var binario = Abrir("x.dll", [0x4D, 0x5A, 0x00, 0x01]);
        Assert.False(binario.Legivel);
        Assert.Equal("Arquivo binário.", binario.Problema);
        binario.Descartar();

        var sumido = new ArquivoVM(Path.Combine(_raiz, "nao-existe.cs"), _raiz, _ => { }, _ => { }, _ => { });
        Assert.False(sumido.Legivel);
        sumido.Descartar();
    }

    [Fact]
    public void CitarMontaBlocoComCaminhoRelativoELinhas()
    {
        string? citado = null;
        var caminho = Path.Combine(_raiz, "src", "B.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllText(caminho, "a\nb\nc\n");
        var arquivo = new ArquivoVM(caminho, _raiz, _ => { }, _ => { }, t => citado = t);
        arquivo.Citar(2, 3, "b\nc\n");
        arquivo.Descartar();
        Assert.Equal("`src/B.cs` (linhas 2-3):\n```cs\nb\nc\n```\n", citado);
    }

    [Fact]
    public void EditorNaoRepeteAbaDoMesmoArquivoEFechaParaAVizinha()
    {
        File.WriteAllText(Path.Combine(_raiz, "a.cs"), "a");
        File.WriteAllText(Path.Combine(_raiz, "b.cs"), "b");
        var editor = new EditorVM(() => _raiz, _ => { });
        editor.Abrir(Path.Combine(_raiz, "a.cs"));
        editor.Abrir(Path.Combine(_raiz, "b.cs"));
        editor.Abrir(Path.Combine(_raiz, "A.CS"));
        Assert.Equal(2, editor.Abas.Count);
        Assert.Equal("a.cs", editor.Atual!.Titulo);

        editor.Fechar(editor.Atual);
        Assert.Equal("b.cs", editor.Atual!.Titulo);
        editor.Descartar();
    }
}
