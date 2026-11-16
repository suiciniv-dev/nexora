using System.Text.Json;
using Nexora.Core.Claude;
using SkiaSharp;

namespace Nexora.Testes;

public class ImagensTestes
{
    static byte[] Quadrado(int largura, int altura, SKColor cor)
    {
        using var bitmap = new SKBitmap(largura, altura);
        bitmap.Erase(cor);
        using var imagem = SKImage.FromBitmap(bitmap);
        using var png = imagem.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    [Fact]
    public void MensagemLevaAImagemAntesDoTexto()
    {
        var anexo = new Anexo("print.png", "image/png", [1, 2, 3]);
        var mensagem = JsonDocument.Parse(Protocolo.Mensagem("o que é isto?", [anexo])).RootElement.GetProperty("message").GetProperty("content");
        Assert.Equal(2, mensagem.GetArrayLength());
        Assert.Equal("image", mensagem[0].GetProperty("type").GetString());
        Assert.Equal("image/png", mensagem[0].GetProperty("source").GetProperty("media_type").GetString());
        Assert.Equal("AQID", mensagem[0].GetProperty("source").GetProperty("data").GetString());
        Assert.Equal("o que é isto?", mensagem[1].GetProperty("text").GetString());

        var soImagem = JsonDocument.Parse(Protocolo.Mensagem("", [anexo])).RootElement.GetProperty("message").GetProperty("content");
        Assert.Equal("image", Assert.Single(soImagem.EnumerateArray()).GetProperty("type").GetString());
    }

    [Fact]
    public void HistoricoContaAsImagensDaMensagem()
    {
        var com = JsonDocument.Parse("""{"content":[{"type":"image","source":{}},{"type":"image","source":{}},{"type":"text","text":"veja"}]}""").RootElement;
        Assert.Equal(new MensagemDoUsuario("veja", 2), Assert.Single(Protocolo.LerBlocosUsuario(com, incluirTexto: true)));
        var so = JsonDocument.Parse("""{"content":[{"type":"image","source":{}}]}""").RootElement;
        Assert.Equal(new MensagemDoUsuario("", 1), Assert.Single(Protocolo.LerBlocosUsuario(so, incluirTexto: true)));
        Assert.Empty(Protocolo.LerBlocosUsuario(so, incluirTexto: false));
    }

    [Fact]
    public void ImagemGrandeEncolheEArquivoQueNaoEImagemFica()
    {
        var anexo = Imagens.Preparar(Quadrado(4000, 2000, SKColors.Red), "grande.png")!;
        using var lida = SKBitmap.Decode(anexo.Dados);
        Assert.Equal((1800, 900), (lida.Width, lida.Height));
        Assert.Equal("image/png", anexo.TipoMidia);
        Assert.Null(Imagens.Preparar("não é imagem"u8.ToArray(), "x.png"));
    }

    [Fact]
    public async Task ClaudeDeVerdadeVeAImagem()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_INTEGRACAO") != "1") return;

        var pasta = Directory.CreateTempSubdirectory("nexora-imagem-").FullName;
        var anexo = Imagens.Preparar(Quadrado(96, 96, new SKColor(220, 20, 20)), "vermelho.png")!;
        var texto = "";
        var fim = new TaskCompletionSource();
        await using (var sessao = new SessaoClaude(new OpcoesSessao(pasta, Modelo: "haiku", Isolada: true)))
        {
            sessao.Evento += e =>
            {
                if (e is TextoCompleto t) texto += t.Texto;
                if (e is TurnoEncerrado) fim.TrySetResult();
            };
            sessao.Iniciar();
            sessao.Enviar("Qual é a cor desta imagem? Responda com uma palavra em português.", [anexo]);
            await fim.Task.WaitAsync(TimeSpan.FromSeconds(120));
        }

        Directory.Delete(pasta, recursive: true);
        foreach (var d in new DirectoryInfo(DadosIsolados.ClaudeReal).EnumerateDirectories($"*{Path.GetFileName(pasta).Replace('.', '-')}"))
            d.Delete(recursive: true);
        Assert.Contains("vermelh", texto, StringComparison.OrdinalIgnoreCase);
    }
}
