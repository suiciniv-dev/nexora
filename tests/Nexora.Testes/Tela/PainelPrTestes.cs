using Avalonia.Threading;
using Nexora.Core.Git;
using Nexora.ViewModels;

namespace Nexora.Testes.Tela;

public sealed class PainelPrTestes
{
    static async Task NoDemo(Func<JanelaVM, Task> teste)
    {
        var (demo, isolada) = (JanelaVM.Demo, JanelaVM.Isolada);
        var anotar = Sistema.SoAnotar;
        var abertos = new List<string>();
        JanelaVM.Isolada = true;
        JanelaVM.Demo = true;
        Sistema.SoAnotar = abertos.Add;
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

    static async Task Esperar(PainelPrVM painel)
    {
        for (var i = 0; i < 100 && painel.Carregando; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
    }

    [Fact]
    public Task PainelMostraChecksFalhosPrimeiroEPedeCorrecaoNoRascunho() => Sessao.Rodar(() => NoDemo(async vm =>
    {
        var chat = vm.Abertas[0];
        chat.MostrarAbaCommand.Execute("pr");
        await Esperar(chat.PainelPr);
        var painel = chat.PainelPr;
        Assert.Equal(52, painel.Pr!.Numero);
        Assert.True(painel.Checks[0].Falhou);
        Assert.True(painel.Checks[1].Pendente);
        Assert.Equal(4, painel.Grupos.Count);
        Assert.Equal("ana-dev", painel.Revisores[0].Login);
        Assert.True(painel.TemConversasAbertas);
        Assert.False(painel.PodeMergear);
        Assert.True(painel.PodeLigarAutoMerge);

        painel.Checks[0].PedirCorrecaoCommand.Execute(null);
        Assert.Contains("testes (windows)", chat.Rascunho);
        Assert.Contains("gh run view 4171 --log-failed", chat.Rascunho);
        chat.Rascunho = "";
        painel.PedirConversasCommand.Execute(null);
        Assert.Contains("Não responda nem comente no PR", chat.Rascunho);
        Assert.Contains("src/Loja.Domain/Pedidos/Pedido.cs:88", chat.Rascunho);
        Assert.DoesNotContain("CheckoutController", chat.Rascunho);
    }));

    [Fact]
    public Task AcoesPedemConfirmacaoAntesDeMexerNoGitHub() => Sessao.Rodar(() => NoDemo(async vm =>
    {
        var chat = vm.Abertas[1];
        chat.MostrarAbaCommand.Execute("pr");
        await Esperar(chat.PainelPr);
        var painel = chat.PainelPr;
        Assert.True(painel.TemConflito);
        Assert.True(painel.PodeMarcarPronto);
        painel.MarcarProntoCommand.Execute(null);
        Assert.True(painel.TemConfirmacao);
        Assert.True(painel.Pr!.Rascunho);
        painel.CancelarConfirmacaoCommand.Execute(null);
        Assert.False(painel.TemConfirmacao);
        painel.MarcarProntoCommand.Execute(null);
        await painel.ConfirmarCommand.ExecuteAsync(null);
        Assert.False(painel.Pr!.Rascunho);
        Assert.False(chat.Pr!.Rascunho);
        painel.PedirConflitoCommand.Execute(null);
        Assert.Contains("origin/main", chat.Rascunho);
    }));

    [Fact]
    public Task SemPrPropoeTituloEDescricaoECriaDepoisDeConfirmar() => Sessao.Rodar(() => NoDemo(async vm =>
    {
        var chat = vm.Abertas[2];
        Assert.Null(chat.Pr);
        chat.ComitarEPrCommand.Execute(null);
        await Esperar(chat.PainelPr);
        var painel = chat.PainelPr;
        Assert.True(painel.SemPr);
        Assert.Equal("main", painel.BaseEscolhida);
        Assert.Equal("Notas offline", painel.NovoTitulo);
        Assert.Contains("## Changelog", painel.NovoCorpo);
        Assert.Contains("Closes voce/app-notas#55", painel.NovoCorpo);
        painel.NovoTitulo = "Notas offline com fila";
        painel.BaseEscolhida = "develop";
        Assert.Equal("Notas offline com fila", painel.NovoTitulo);
        painel.PedirPrAoAgenteCommand.Execute(null);
        Assert.Contains("Título: Notas offline com fila", chat.Rascunho);
        Assert.Contains("em develop", chat.Rascunho);
        painel.CriarPrCommand.Execute(null);
        Assert.True(painel.TemConfirmacao);
        await painel.ConfirmarCommand.ExecuteAsync(null);
        Assert.False(painel.SemPr);
        Assert.Equal("develop", painel.Pr!.BaseBranch);
        Assert.NotNull(chat.Pr);
    }));

    [Fact]
    public Task PilulaDaIssueAbreOPainelDaSessaoOuOGitHub() => Sessao.Rodar(() => NoDemo(async vm =>
    {
        var abertos = new List<string>();
        Sistema.SoAnotar = abertos.Add;
        var issues = vm.PaginaIssues;
        issues.Ativar(true);
        var linhas = issues.Grupos.SelectMany(g => g.Linhas).ToList();
        var cupom = linhas.Single(l => l.Item.Numero == 48);
        Assert.Equal("#52 · checks falharam", cupom.Pilulas.Single().Texto);
        Assert.True(cupom.Pilulas[0].Falhou);
        cupom.Pilulas[0].AbrirCommand.Execute(null);
        var chat = vm.Abertas[0];
        Assert.True(chat.AbaPr);
        await Esperar(chat.PainelPr);
        Assert.Equal(52, chat.PainelPr.Pr!.Numero);
        Assert.Empty(abertos);
        var pix = linhas.Single(l => l.Item.Numero == 39);
        Assert.Equal(2, pix.Pilulas.Count);
        Assert.True(pix.Pilulas.Single(p => p.Dados.Numero == 46).Mergeado);
        pix.Pilulas.Single(p => p.Dados.Numero == 53).AbrirCommand.Execute(null);
        Assert.Single(abertos);
    }));
}
