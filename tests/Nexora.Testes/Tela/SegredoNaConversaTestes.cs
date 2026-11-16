using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Segredos;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class SegredoNaConversaTestes : IDisposable
{
    const string Valor = "ghp_TESTE_nao_e_real_5c1e2a7d";

    readonly string _repo = Directory.CreateTempSubdirectory("nexora-segredo-tela-").FullName;
    ServicoDeSegredos? _antes;

    public SegredoNaConversaTestes()
    {
        var inicio = new ProcessStartInfo("git") { WorkingDirectory = _repo, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        inicio.ArgumentList.Add("init");
        inicio.ArgumentList.Add("-q");
        using (var p = Process.Start(inicio)!) p.WaitForExit(20_000);
        File.WriteAllText(Path.Combine(_repo, ".gitignore"), ".env\n");
    }

    public void Dispose()
    {
        if (_antes is { } antes) Sessao.Rodar(() => App.Segredos = antes).Wait();
        try
        {
            foreach (var arquivo in Directory.EnumerateFiles(_repo, "*", SearchOption.AllDirectories)) File.SetAttributes(arquivo, FileAttributes.Normal);
            Directory.Delete(_repo, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    static async Task Esperar(Func<bool> condicao, int segundos = 20)
    {
        var limite = DateTime.Now.AddSeconds(segundos);
        while (!condicao() && DateTime.Now < limite)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
        Dispatcher.UIThread.RunJobs();
        Assert.True(condicao(), "a condição não aconteceu a tempo");
    }

    static IEnumerable<string> Textos(object alvo, HashSet<object> vistos, int fundo = 0)
    {
        if (fundo > 3 || !vistos.Add(alvo)) yield break;
        foreach (var propriedade in alvo.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (propriedade.GetIndexParameters().Length > 0) continue;
            object? valor;
            try
            {
                valor = propriedade.GetValue(alvo);
            }
            catch (TargetInvocationException)
            {
                continue;
            }
            if (valor is string texto) yield return texto;
            else if (valor is ItemChat or PedidoDeSegredo)
                foreach (var t in Textos(valor, vistos, fundo + 1)) yield return t;
        }
    }

    [Fact]
    public Task ValorDoSegredoNuncaApareceNaConversaNemNoQueOAgenteRecebe() => Sessao.Rodar(async () =>
    {
        _antes = App.Segredos;
        var cofre = new CofreDeSegredos(null, new ArmazemNaMemoria());
        App.Segredos = new ServicoDeSegredos(cofre);
        var chat = ChatVM.Nova(new Local("Loja", _repo, []), new AnfitriaoFalso(), _ => { }, _ => { });
        App.Segredos.Registrar(chat.Codigo, _repo, p => Dispatcher.UIThread.Post(() => chat.MostrarPedidoDeSegredo(p)));
        var janela = new Window { Width = 1100, Height = 800, Content = new ChatView { DataContext = chat } };
        janela.Show();

        var chamada = App.Segredos.ChamarAsync(chat.Codigo,
            JsonDocument.Parse("""{"nome":"GITHUB_TOKEN","motivo":"Criar a release pelo script","destino":"env"}""").RootElement.Clone(), default);
        await Esperar(() => chat.SegredoPendente is not null);
        var item = chat.SegredoPendente!;
        Assert.Equal(EstadoChat.PrecisaDeVoce, chat.Estado);
        Assert.True(chat.TemSegredoNoCartao);

        var campo = janela.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "CampoDoSegredo");
        Assert.Equal('•', campo.PasswordChar);
        Assert.False(campo.RevealPassword);
        campo.Text = Valor;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Valor, item.Valor);
        Assert.DoesNotContain(janela.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains(Valor) == true);
        item.AlternarMostrarCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(campo.RevealPassword);

        await item.SalvarCommand.ExecuteAsync(null);
        var resultado = await chamada;
        Dispatcher.UIThread.RunJobs();

        Assert.False(resultado.Erro);
        Assert.Contains("Guardado em .env como GITHUB_TOKEN", resultado.Texto);
        Assert.DoesNotContain(Valor, resultado.Texto);
        Assert.Contains($"GITHUB_TOKEN={Valor}", File.ReadAllText(Path.Combine(_repo, ".env")));

        Assert.True(item.Respondida);
        Assert.Equal("", item.Valor);
        Assert.False(item.Mostrando);
        Assert.Null(chat.SegredoPendente);
        Assert.NotEqual(EstadoChat.PrecisaDeVoce, chat.Estado);

        var vistos = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var textos = chat.Itens.SelectMany(i => Textos(i, vistos)).Concat(chat.Trecho.SelectMany(i => Textos(i, vistos)))
            .Concat([chat.UltimaAtividade ?? "", chat.Rascunho, chat.Titulo]).ToList();
        Assert.NotEmpty(textos);
        Assert.DoesNotContain(textos, t => t.Contains(Valor));
        Assert.DoesNotContain(janela.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains(Valor) == true);
        Assert.DoesNotContain(janela.GetVisualDescendants().OfType<TextBox>(), t => t.Text?.Contains(Valor) == true);
        Assert.Contains(cofre.Lista(), s => s.Nome == "GITHUB_TOKEN" && s.Destino == DestinoDoSegredo.ArquivoEnv);
        janela.Close();
    });

    [Fact]
    public Task RecusarFechaOCartaoEAvisaOAgente() => Sessao.Rodar(async () =>
    {
        _antes = App.Segredos;
        App.Segredos = new ServicoDeSegredos(new CofreDeSegredos(null, new ArmazemNaMemoria()));
        var chat = ChatVM.Nova(new Local("Loja", _repo, []), new AnfitriaoFalso(), _ => { }, _ => { });
        App.Segredos.Registrar(chat.Codigo, _repo, p => Dispatcher.UIThread.Post(() => chat.MostrarPedidoDeSegredo(p)));
        var chamada = App.Segredos.ChamarAsync(chat.Codigo,
            JsonDocument.Parse("""{"nome":"API_KEY","motivo":"m","destino":"ambiente"}""").RootElement.Clone(), default);
        await Esperar(() => chat.SegredoPendente is not null);
        chat.SegredoPendente!.RecusarCommand.Execute(null);
        var resultado = await chamada;
        Assert.Contains("recusou", resultado.Texto);
        Assert.Null(chat.SegredoPendente);
        Assert.Equal("Recusado", Assert.Single(chat.Itens.OfType<ItemSegredo>()).Resposta);
        Assert.Empty(App.Segredos.Cofre.Lista());
    });
}
