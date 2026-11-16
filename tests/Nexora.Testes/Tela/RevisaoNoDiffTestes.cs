using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Revisao;
using Nexora.Core.Texto;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

[Collection("Idioma")]
public sealed class RevisaoNoDiffTestes
{
    static async Task NoDemo(Func<JanelaVM, Task> teste)
    {
        var (demo, isolada) = (JanelaVM.Demo, JanelaVM.Isolada);
        var anotar = Sistema.SoAnotar;
        JanelaVM.Isolada = true;
        JanelaVM.Demo = true;
        Sistema.SoAnotar = _ => { };
        try
        {
            var vm = new JanelaVM();
            vm.PreencherDemo();
            await teste(vm);
        }
        finally
        {
            (JanelaVM.Demo, JanelaVM.Isolada) = (demo, isolada);
            Sistema.SoAnotar = anotar;
        }
    }

    static async Task Esperar(Func<bool> pronto)
    {
        for (var i = 0; i < 200 && !pronto(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
    }

    const string Servico = "src/Loja.Application/Cupons/CupomService.cs";

    static List<string> Mandados(ChatVM chat, int antes) =>
        [.. chat.Itens.Skip(antes).OfType<ItemUsuario>().Select(i => i.Texto), .. chat.Fila.Select(f => f.Texto)];

    static DiffVM AbrirDiff(ChatVM chat) =>
        chat.Editor.AbrirDiff("CupomService.cs (git)", $@"{chat.Pasta}\CupomService.cs#git", Diff.LerUnificado(PrsDemo.DiffDoArquivo(Servico)!), Path.Combine(chat.Pasta, Servico.Replace('/', '\\')));

    [Fact]
    public Task ComentariosNoDiffFicamPendentesEVaoJuntosParaAConversa() => Sessao.Rodar(() => NoDemo(async vm =>
    {
        var chat = vm.Abertas[0];
        var diff = AbrirDiff(chat);
        Assert.Equal(Servico, diff.Relativo);
        var l31 = diff.Linhas.First(l => l.NumeroNovo == 31);
        var l32 = diff.Linhas.First(l => l.NumeroNovo == 32);
        Assert.True(l31.Comentavel);
        Assert.False(diff.Linhas[0].Comentavel);

        l31.Comentar(false);
        l32.Comentar(true);
        Assert.False(l31.Escrevendo);
        Assert.True(l32.Escrevendo);
        Assert.Equal("linhas 31-32", l32.FaixaDoRascunho);
        Assert.True(l31.NaFaixa);
        l32.Rascunho = "Devolva a falha em vez de lançar.";
        l32.ConfirmarCommand.Execute(null);
        Assert.False(l32.Escrevendo);
        Assert.Single(l32.Comentarios);

        var l38 = diff.Linhas.First(l => l.NumeroNovo == 38);
        l38.Comentar(false);
        l38.Rascunho = "Confira os chamadores.";
        l38.ConfirmarCommand.Execute(null);
        var l26 = diff.Linhas.First(l => l.NumeroNovo == 26);
        l26.Comentar(false);
        l26.Rascunho = "Apagar depois";
        l26.ConfirmarCommand.Execute(null);

        var revisao = chat.RevisaoPendente;
        Assert.Equal("3 comentários", revisao.TextoQuantidade);
        revisao.Itens[2].ApagarCommand.Execute(null);
        Assert.Empty(l26.Comentarios);
        revisao.Itens[1].EditarCommand.Execute(null);
        revisao.Itens[1].Edicao = "Confira quem chama AplicarDesconto.";
        revisao.Itens[1].SalvarEdicaoCommand.Execute(null);
        Assert.Equal("2 comentários", revisao.TextoQuantidade);

        var reaberto = AbrirDiff(chat);
        Assert.Single(reaberto.Linhas.First(l => l.NumeroNovo == 32).Comentarios);

        var antes = chat.Itens.Count;
        revisao.MandarCommand.Execute(null);
        Assert.False(revisao.TemItens);
        Assert.Empty(reaberto.Linhas.First(l => l.NumeroNovo == 32).Comentarios);
        var enviado = Mandados(chat, antes).Single();
        Assert.Contains($"`{Servico}` (linhas 31-32)", enviado);
        Assert.Contains("+        if (cupom.Vencido(agora))", enviado);
        Assert.Contains("Devolva a falha em vez de lançar.", enviado);
        Assert.Contains("Confira quem chama AplicarDesconto.", enviado);
        Assert.DoesNotContain("Apagar depois", enviado);
        await Task.CompletedTask;
    }));

    [Fact]
    public Task DiffDoPrMostraResolvidasComoAvataresEAbertasNaLinha() => Sessao.Rodar(() => NoDemo(async vm =>
    {
        var chat = vm.Abertas[0];
        chat.MostrarAbaCommand.Execute("pr");
        var painel = chat.PainelPr;
        await Esperar(() => !painel.Carregando && painel.Pr is not null);
        var grupo = painel.Grupos.First(g => g.Arquivo == Servico);
        Assert.Equal(3, grupo.Resolvidas.Count);
        var soResolvidas = painel.Grupos.First(g => g.Arquivo.EndsWith("CheckoutController.cs"));
        Assert.Empty(soResolvidas.Conversas);
        Assert.True(soResolvidas.TemResolvidas);

        grupo.VerDiffCommand.Execute(null);
        await Esperar(() => chat.Editor.Atual is DiffVM);
        var diff = Assert.IsType<DiffVM>(chat.Editor.Atual);
        var l28 = diff.Linhas.First(l => l.NumeroNovo == 28);
        Assert.Equal(2, l28.Resolvidas.Count);
        Assert.Contains(l28.Resolvidas, r => r.EhAgente && r.Agente == "codex");
        Assert.Empty(l28.Conversas);
        l28.AlternarResolvidasCommand.Execute(null);
        Assert.Equal(2, l28.Conversas.Count);
        l28.AlternarResolvidasCommand.Execute(null);
        Assert.Empty(l28.Conversas);
        var l32 = diff.Linhas.First(l => l.NumeroNovo == 32);
        Assert.Single(l32.Conversas);
        Assert.False(l32.TemResolvidas);

        var view = new DiffView { DataContext = diff };
        var janela = new Window { Width = 1100, Height = 700, Content = view };
        janela.Show();
        Dispatcher.UIThread.RunJobs();
        janela.UpdateLayout();
        Assert.Contains(view.GetVisualDescendants().OfType<ItemsControl>(), i => i.Theme == view.FindResource("PilhaDeAvatares"));
        janela.Close();
    }));

    [Fact]
    public Task ComentariosDoPrVaoParaUmaConversaNovaComAPassagem() => Sessao.Rodar(() => NoDemo(async vm =>
    {
        var chat = vm.Abertas[0];
        chat.MostrarAbaCommand.Execute("pr");
        var painel = chat.PainelPr;
        await Esperar(() => painel.Pr is not null && painel.Passagem is not null);
        Assert.True(painel.TemEnvio);
        Assert.True(painel.TemOriginal);
        Assert.Equal(DestinoDaRevisao.Nova, painel.Destino);
        Assert.Equal("Nova + passagem", painel.TextoNova);
        Assert.Equal(5, painel.Selecionados);

        painel.Grupos.First(g => g.Arquivo.EndsWith("Pedido.cs")).Conversas[0].Incluir = false;
        Assert.Equal(4, painel.Selecionados);
        var abertasAntes = vm.Abertas.Count;
        painel.MandarComentariosCommand.Execute(null);
        Assert.Equal(abertasAntes + 1, vm.Abertas.Count);
        var nova = vm.Atual!;
        Assert.NotSame(chat, nova);
        var primeira = nova.Itens.OfType<ItemUsuario>().First().Texto;
        Assert.StartsWith("Retome o handoff \"Handoff: cupom-checkout\".", primeira);
        Assert.Contains("CupomService.cs` (linhas 31-32), de ana-dev", primeira);
        Assert.Contains("Comentário geral de bruno", primeira);
        Assert.DoesNotContain("Pedido.cs", primeira);
        Assert.False(painel.PodeMandar);

        foreach (var c in painel.Grupos.SelectMany(g => g.Conversas)) c.Incluir = true;
        painel.EscolherDestinoCommand.Execute("original");
        Assert.True(painel.ParaOriginal);
        var antes = chat.Itens.Count;
        painel.MandarComentariosCommand.Execute(null);
        var mandado = Mandados(chat, antes).Single();
        Assert.StartsWith("Resolva estes comentários de revisão do PR #52.", mandado);
        Assert.Contains("Mandei 4 comentários", painel.Aviso);
    }));
}
