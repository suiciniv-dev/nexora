using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Nexora.Core.Ambientes;
using Nexora.Servicos;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class NovidadeTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-novidade-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    [Fact]
    public void Conversa_que_muda_fora_de_vista_fica_marcada_ate_ser_vista()
    {
        var janela = new JanelaVM();
        var local = new Local("Loja", _pasta, []);
        janela.AbrirSessao(local);
        janela.AbrirSessao(local);
        janela.Principal.Ativa = true;
        var vista = Assert.IsType<ChatVM>(janela.Principal.Conteudo);
        var outra = janela.Abertas.First(c => c != vista);

        vista.Estado = EstadoChat.Trabalhando;
        vista.Estado = EstadoChat.Pronta;
        Assert.False(vista.NaoVista);

        outra.Estado = EstadoChat.Trabalhando;
        Assert.False(outra.NaoVista);
        outra.Estado = EstadoChat.Pronta;
        Assert.True(outra.NaoVista);

        janela.Principal.Ativa = false;
        vista.Estado = EstadoChat.Trabalhando;
        vista.Estado = EstadoChat.Erro;
        Assert.True(vista.NaoVista);

        janela.Principal.Ativa = true;
        janela.MarcarVistas();
        Assert.False(vista.NaoVista);
        Assert.True(outra.NaoVista);

        janela.Mostrar(outra);
        Assert.False(outra.NaoVista);
    }

    [Fact]
    public void Conversa_parada_que_fica_pronta_nao_vira_novidade()
    {
        var janela = new JanelaVM();
        var local = new Local("Loja", _pasta, []);
        janela.AbrirSessao(local);
        janela.AbrirSessao(local);
        janela.Principal.Ativa = true;
        var outra = janela.Abertas.First(c => c != janela.Principal.Conteudo);

        outra.Estado = EstadoChat.Pronta;
        Assert.False(outra.NaoVista);
        outra.Estado = EstadoChat.PrecisaDeVoce;
        Assert.True(outra.NaoVista);
    }

    [Fact]
    public Task Aba_nao_vista_mostra_a_bolinha_e_o_titulo_em_destaque() => Sessao.Rodar(() =>
    {
        var janela = new JanelaVM();
        var local = new Local("Loja", _pasta, []);
        janela.AbrirSessao(local);
        janela.AbrirSessao(local);
        var area = janela.Principal;
        var a = (ChatVM)area.Guias[0].Conteudo;
        var b = (ChatVM)area.Guias[1].Conteudo;
        a.Titulo = "Notas offline";
        b.Titulo = "Cupom de desconto";
        area.Selecionada = area.Achar(b);
        area.Ativa = true;
        a.Estado = EstadoChat.Trabalhando;
        a.Estado = EstadoChat.Pronta;
        Assert.True(a.NaoVista);

        var abas = new AbasView { DataContext = area };
        var tela = new Window { Width = 700, Height = 60, Content = abas };
        tela.Show();
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        var titulos = abas.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("titulo-guia")).ToList();
        Assert.Contains(titulos, t => t.Text == "Notas offline" && t.Classes.Contains("novidade"));
        Assert.Contains(titulos, t => t.Text == "Cupom de desconto" && !t.Classes.Contains("novidade"));
        Assert.Single(abas.GetVisualDescendants().OfType<Nexora.Controles.Avatar>(), v => v.Novidade);

        if (Environment.GetEnvironmentVariable("NEXORA_FOTOS") is { Length: > 0 } pasta)
            tela.CaptureRenderedFrame()?.Save(Path.Combine(pasta, "abas-novidade.png"));

        area.Selecionada = area.Achar(a);
        Assert.False(a.NaoVista);
    });

    [Theory]
    [InlineData(3, TipoDoSelo.Precisa, "3")]
    [InlineData(12, TipoDoSelo.Pronta, "9+")]
    [InlineData(1, TipoDoSelo.Erro, "1")]
    public Task Selo_da_barra_tem_a_cor_do_tipo_e_o_numero(int total, TipoDoSelo tipo, string texto) => Sessao.Rodar(() =>
    {
        Assert.Equal(texto, SeloNaBarra.Texto(total));
        var dados = SeloNaBarra.Desenhar(total, tipo);
        Assert.Equal(SeloNaBarra.Lado * SeloNaBarra.Lado * 4, dados.Length);

        var cor = SeloNaBarra.Cor(tipo);
        var i = (SeloNaBarra.Lado / 2 * SeloNaBarra.Lado + 5) * 4;
        Assert.Equal(255, dados[i + 3]);
        Assert.InRange(dados[i + 2], cor.R - 8, cor.R + 8);
        Assert.InRange(dados[i + 1], cor.G - 8, cor.G + 8);
        Assert.InRange(dados[i], cor.B - 8, cor.B + 8);
        Assert.Equal(0, dados[3]);

        if (Environment.GetEnvironmentVariable("NEXORA_FOTOS") is not { Length: > 0 } pasta) return;
        var alca = GCHandle.Alloc(dados, GCHandleType.Pinned);
        try
        {
            using var imagem = new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Unpremul, alca.AddrOfPinnedObject(),
                new PixelSize(SeloNaBarra.Lado, SeloNaBarra.Lado), new Vector(96, 96), SeloNaBarra.Lado * 4);
            imagem.Save(Path.Combine(pasta, $"selo-{tipo}-{total}.png"));
        }
        finally
        {
            alca.Free();
        }
    });
}
