using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Platform;
using Avalonia.Threading;
using Nexora.Core.Avatares;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class AvatarVivoTestes
{
    static void Esperar(Func<bool> condicao)
    {
        var limite = DateTime.Now.AddSeconds(10);
        while (!condicao() && DateTime.Now < limite)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(10);
        }
        Dispatcher.UIThread.RunJobs();
        Assert.True(condicao(), "a condição não aconteceu a tempo");
    }

    static void Quadros(AvatarVivo avatar, int quantos)
    {
        for (var i = 0; i < quantos; i++)
        {
            Thread.Sleep(20);
            Dispatcher.UIThread.RunJobs();
            avatar.Avancar();
        }
    }

    static double OlhoX(AvatarVivo avatar) =>
        avatar.UltimoQuadro!.Formas.Where(f => f.Olho && f.Marca is { Arco: false }).Select(f => f.Marca!.X + f.Matriz.E).Average();

    [Fact]
    public Task CatalogoTemTodosOsAvataresQueCita() => Sessao.Rodar(() =>
    {
        using var fluxo = AssetLoader.Open(new Uri("avares://Nexora/Assets/Avatares/catalogo.json"));
        using var doc = JsonDocument.Parse(fluxo);
        var citados = doc.RootElement.GetProperty("agentes").EnumerateObject().Select(p => p.Value)
            .Concat(doc.RootElement.GetProperty("subagentes").EnumerateObject().Select(p => p.Value))
            .Append(doc.RootElement.GetProperty("orquestrador"))
            .Select(v => v.GetProperty("avatar").GetString()!)
            .Distinct();
        foreach (var id in citados) Assert.True(CatalogoDeAvatares.Definicao(id) is not null, $"o avatar {id} não carregou");
        Assert.NotNull(CatalogoDeAvatares.Agente("claude"));
        Assert.NotNull(CatalogoDeAvatares.Agente("um-agente-novo"));
        Assert.NotNull(CatalogoDeAvatares.Subagente("Explore"));
        Assert.NotNull(CatalogoDeAvatares.Subagente("tipo-que-ninguem-conhece"));
        Assert.NotNull(CatalogoDeAvatares.Orquestrador);
    });

    [Fact]
    public Task AvatarCarregaDesenhaEOlhaParaOMouse() => Sessao.Rodar(() =>
    {
        var avatar = new AvatarVivo { Agente = "codex", Width = 96, Height = 96, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        var janela = new Window { Width = 900, Height = 400, Content = avatar };
        janela.Show();
        Esperar(() => avatar.Preparado is not null && avatar.UltimoQuadro is not null);
        Assert.Equal("Book", AutomationProperties.GetName(avatar));

        janela.MouseMove(new Point(890, 200));
        Quadros(avatar, 40);
        var direita = OlhoX(avatar);
        janela.MouseMove(new Point(10, 200));
        Quadros(avatar, 40);
        var esquerda = OlhoX(avatar);

        Assert.True(direita > esquerda + 1, $"os olhos não seguiram o mouse ({esquerda:0.00} → {direita:0.00}), {avatar.QuadrosMontados} quadros, reduzido {Aparencia.AnimacoesReduzidas}");
    });

    [Fact]
    public Task AvatarParadoNaoSegueOMouseEDecorativoSaiDoLeitorDeTela() => Sessao.Rodar(() =>
    {
        var avatar = new AvatarVivo { Subagente = "Explore", Parado = true, Rotulo = "", Width = 96, Height = 96 };
        var janela = new Window { Width = 900, Height = 400, Content = avatar };
        janela.Show();
        Esperar(() => avatar.UltimoQuadro is not null);
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(avatar));

        janela.MouseMove(new Point(890, 200));
        Quadros(avatar, 10);
        var a = OlhoX(avatar);
        janela.MouseMove(new Point(10, 200));
        Quadros(avatar, 10);
        Assert.Equal(a, OlhoX(avatar), 6);
    });

    [Fact]
    public Task ComemorarDeixaOAvatarFelizPorUmTempo() => Sessao.Rodar(() =>
    {
        var avatar = new AvatarVivo { Subagente = "Plan", Width = 48, Height = 48 };
        var janela = new Window { Width = 300, Height = 300, Content = avatar };
        janela.Show();
        Esperar(() => avatar.UltimoQuadro is not null);
        Assert.Equal(HumorAvatar.Parado, avatar.HumorAtual);

        avatar.Comemorar = true;
        Assert.Equal(HumorAvatar.Feliz, avatar.HumorAtual);
        Quadros(avatar, 30);
        Assert.Contains(avatar.UltimoQuadro!.Formas, f => f.Olho && f.Marca is { Arco: true });
    });
}
