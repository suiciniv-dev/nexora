using Avalonia.Controls;
using Avalonia.Threading;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.Core.Historico;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class ConversaLongaTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-longa-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_pasta, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public Task ConversaLongaDeixaSoAsUltimasMensagensNaTela() => Sessao.Rodar(() =>
    {
        var chat = ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
        var janela = new Window { Width = 1100, Height = 800, Content = new ChatView { DataContext = chat } };
        janela.Show();

        for (var i = 0; i < 300; i++) chat.Itens.Add(new ItemTexto($"mensagem {i}"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(300, chat.Itens.Count);
        Assert.InRange(chat.ItensNaTela.Count, ChatVM.MensagensNaTela, ChatVM.MensagensNaTela + 40);
        Assert.Same(chat.Itens[^1], chat.ItensNaTela[^1]);
        Assert.True(chat.TemEscondidas);
        Assert.Equal(chat.Itens.Skip(chat.Escondidas), chat.ItensNaTela);

        var antes = chat.ItensNaTela.Count;
        chat.MostrarAnterioresCommand.Execute(null);
        while (chat.MontarMais(10)) { }
        Assert.Equal(antes + ChatVM.MensagensNaTela, chat.ItensNaTela.Count);
        Assert.Equal(chat.Itens.Skip(chat.Escondidas), chat.ItensNaTela);

        chat.Itens.Add(new ItemTexto("nova enquanto leio lá em cima"));
        Assert.Equal(antes + ChatVM.MensagensNaTela + 1, chat.ItensNaTela.Count);

        chat.ChegouAoFim();
        Assert.Equal(ChatVM.MensagensNaTela, chat.ItensNaTela.Count);
        Assert.Same(chat.Itens[^1], chat.ItensNaTela[^1]);
        janela.Close();
    });

    [Fact]
    public void LerTurnosLeSoOFimEDepoisOsAnterioresSemPerderNada()
    {
        var arquivo = Path.Combine(_pasta, "turnos.jsonl");
        TranscricaoDeExemplo.Escrever(arquivo, 30, tamanhoDoResultado: 400_000);
        var inteira = Transcricao.Ler(arquivo, int.MaxValue);

        var fim = Transcricao.LerTurnos(arquivo, 10);
        Assert.Equal(10, fim.Eventos.OfType<MensagemDoUsuario>().Count());
        Assert.StartsWith("Pedido número 21:", fim.Eventos.OfType<MensagemDoUsuario>().First().Texto);
        Assert.True(fim.TemAnteriores);

        var partes = new List<IReadOnlyList<EventoClaude>> { fim.Eventos };
        var trecho = fim;
        while (trecho.TemAnteriores)
        {
            trecho = Transcricao.LerTurnos(arquivo, 10, trecho.Inicio);
            partes.Insert(0, trecho.Eventos);
        }
        Assert.Equal(3, partes.Count);
        Assert.Equal(inteira.Select(e => e.ToString()), partes.SelectMany(p => p).Select(e => e.ToString()));
    }

    [Fact]
    public Task ConversaDoHistoricoAbreComOsUltimosTurnosECarregaOsAnteriores() => Sessao.Rodar(async () =>
    {
        var arquivo = Path.Combine(_pasta, "longa.jsonl");
        TranscricaoDeExemplo.Escrever(arquivo, 30);
        var chat = ChatVM.DeConversa(new ConversaSalva("longa", arquivo, "Longa", _pasta, null, DateTime.Now, "Pedido número 1: ajuste o módulo 1 e rode os testes."),
            new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
        await chat.Carregamento;

        string[] Pedidos() => [.. chat.Itens.OfType<ItemUsuario>().Select(u => u.Texto.Split(':')[0])];
        Assert.Equal(ChatVM.TurnosPorVez, Pedidos().Length);
        Assert.Equal("Pedido número 30", Pedidos()[^1]);
        Assert.True(chat.TemAnterioresNoDisco);
        Assert.True(chat.TemEscondidas);
        Assert.Equal(ChatVM.LoteInicial, chat.ItensNaTela.Count);
        Assert.StartsWith("Pedido número 1:", chat.PedidoInicial);
        while (chat.MontarMais(10)) { }
        if (chat.Escondidas > 0) await chat.MostrarAnterioresCommand.ExecuteAsync(null);
        while (chat.MontarMais(10)) { }
        Assert.Equal("Carregar turnos anteriores", chat.TextoEscondidas);

        while (chat.TemEscondidas)
        {
            await chat.MostrarAnterioresCommand.ExecuteAsync(null);
            while (chat.MontarMais(10)) { }
            Assert.Equal(chat.Itens.Skip(chat.Itens.Count - chat.ItensNaTela.Count), chat.ItensNaTela);
        }

        Assert.Equal(Enumerable.Range(1, 30).Select(i => $"Pedido número {i}"), Pedidos());
        Assert.False(chat.TemAnterioresNoDisco);
        Assert.Equal(chat.Itens, chat.ItensNaTela);
        Assert.IsType<ItemAviso>(chat.Itens[^1]);
    });

    [Fact]
    public Task MudancasNoMeioNaoRemontamAsMensagensDaTela() => Sessao.Rodar(() =>
    {
        var chat = ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
        for (var i = 0; i < 50; i++) chat.Itens.Add(new ItemTexto($"mensagem {i}"));
        var refeitas = 0;
        chat.ItensNaTela.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) refeitas++;
        };

        var meio = new ItemTexto("no meio");
        chat.Itens.Insert(30, meio);
        Assert.Equal(chat.Itens, chat.ItensNaTela);
        chat.Itens.Remove(meio);
        chat.Itens.RemoveAt(10);
        chat.Itens[5] = new ItemTexto("trocada");

        Assert.Equal(0, refeitas);
        Assert.Equal(chat.Itens, chat.ItensNaTela);
    });
}
