using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora;

static class MedidorDeQuadros
{
    public static void Agendar(Janela janela, JanelaVM vm, string arquivo) =>
        janela.Opened += async (_, _) =>
        {
            await Task.Delay(4000);
            var texto = await MedirAsync(janela, vm);
            File.WriteAllText(arquivo, texto);
            (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        };

    static async Task MedirConversaLongaAsync(JanelaVM vm, StringBuilder saida, Func<string, Func<Task>, Task> etapa)
    {
        async Task<ChatVM> Abrir(bool mostrar)
        {
            var relogio = Stopwatch.StartNew();
            var chat = vm.AbrirConversaLonga(mostrar);
            while (chat.Carregando) await Task.Delay(5);
            saida.AppendLine(CultureInfo.InvariantCulture, $"  carregar do disco: {relogio.Elapsed.TotalMilliseconds:0} ms, itens {chat.Itens.Count}, na tela {chat.ItensNaTela.Count}");
            return chat;
        }
        vm.MostrarVisaoGeralCommand.Execute(null);
        await etapa("longa em segundo plano", async () =>
        {
            await Abrir(false);
            await Task.Delay(2500);
        });
        ChatVM? aberta = null;
        await etapa("abrir longa", async () =>
        {
            aberta = await Abrir(true);
            await Task.Delay(2000);
        });
        await etapa("voltar a longa", async () =>
        {
            vm.MostrarVisaoGeralCommand.Execute(null);
            await Task.Delay(600);
            vm.MostrarChatCommand.Execute(aberta);
            await Task.Delay(1200);
        });
        await etapa("digitar na longa", async () =>
        {
            for (var i = 0; i < 50; i++)
            {
                aberta!.Rascunho += i % 6 == 5 ? " " : "a";
                await Task.Delay(60);
            }
            aberta!.Rascunho = "";
        });
        await etapa("turnos anteriores", async () =>
        {
            for (var i = 0; i < 3; i++)
            {
                aberta!.MostrarAnterioresCommand.Execute(null);
                await Task.Delay(1200);
            }
        });
        await etapa("digitar com tudo carregado", async () =>
        {
            for (var i = 0; i < 50; i++)
            {
                aberta!.Rascunho += i % 6 == 5 ? " " : "a";
                await Task.Delay(60);
            }
            aberta!.Rascunho = "";
        });
        saida.AppendLine(CultureInfo.InvariantCulture, $"  depois: itens {aberta!.Itens.Count}, na tela {aberta.ItensNaTela.Count}");
    }

    static async Task RolarAsync(Control? dono, bool paraCima = false)
    {
        if (dono?.GetVisualDescendants().OfType<ScrollViewer>().Where(v => v.IsEffectivelyVisible && v.Extent.Height > v.Viewport.Height + 50)
                .OrderByDescending(v => v.Extent.Height).FirstOrDefault() is not { } rolagem) return;
        var inicio = rolagem.Offset.Y;
        var limite = Math.Max(0, rolagem.Extent.Height - rolagem.Viewport.Height);
        for (var volta = 0; volta < 2; volta++)
            for (var i = 0; i <= 60; i++)
            {
                var k = volta == 0 ? i / 60.0 : 1 - i / 60.0;
                var alvo = paraCima ? inicio - k * Math.Min(inicio, 1500) : k * Math.Min(limite, 1500);
                rolagem.Offset = new Avalonia.Vector(0, Math.Clamp(alvo, 0, limite));
                await Task.Delay(16);
            }
    }

    static async Task<string> MedirAsync(Janela janela, JanelaVM vm)
    {
        var relogio = Stopwatch.StartNew();
        var marcas = new List<double>();
        var medindo = true;
        void Quadro(TimeSpan _)
        {
            marcas.Add(relogio.Elapsed.TotalMilliseconds);
            if (medindo) janela.RequestAnimationFrame(Quadro);
        }
        janela.RequestAnimationFrame(Quadro);

        var saida = new StringBuilder();
        saida.AppendLine(CultureInfo.InvariantCulture, $"desenho: {(Program.PeloProcessador ? "processador" : "placa de video")}, escala {janela.RenderScaling:0.##}, janela {janela.Bounds.Width:0}x{janela.Bounds.Height:0}");
        var janelas = new List<(double Ini, double Fim)>();
        void Marcar(double ms) => janelas.Add((relogio.Elapsed.TotalMilliseconds, relogio.Elapsed.TotalMilliseconds + ms));
        async Task Etapa(string nome, Func<Task> acao)
        {
            await Task.Delay(300);
            janelas.Clear();
            var inicio = marcas.Count;
            var antes = relogio.Elapsed.TotalMilliseconds;
            await acao();
            var duracao = janelas.Count > 0 ? janelas.Sum(j => j.Fim - j.Ini) : relogio.Elapsed.TotalMilliseconds - antes;
            var pares = marcas.Skip(inicio).Zip(marcas.Skip(inicio + 1));
            if (janelas.Count > 0) pares = pares.Where(p => janelas.Any(j => p.First >= j.Ini && p.Second <= j.Fim + 8));
            var intervalos = pares.Select(p => p.Second - p.First).OrderBy(x => x).ToList();
            if (intervalos.Count == 0)
            {
                saida.AppendLine($"{nome}: sem quadros");
                return;
            }
            double P(double q) => intervalos[Math.Min(intervalos.Count - 1, (int)(q * intervalos.Count))];
            saida.AppendLine(CultureInfo.InvariantCulture,
                $"{nome,-22} quadros {intervalos.Count + 1,4}  fps {1000 * intervalos.Count / Math.Max(1, duracao),5:0.0}  mediana {P(0.5),5:0.0} ms  p95 {P(0.95),6:0.0} ms  max {intervalos[^1],6:0.0} ms  >25ms {intervalos.Count(x => x > 25),3}  >50ms {intervalos.Count(x => x > 50),3}");
            var lentos = marcas.Skip(inicio).Zip(marcas.Skip(inicio + 1)).Where(p => p.Second - p.First > 50).Select(p => $"+{p.First - antes:0}: {p.Second - p.First:0} ms").ToList();
            if (lentos.Count > 0) saida.AppendLine("  lentos " + string.Join(", ", lentos));
        }

        void Cronometrar(string nome, Action acao)
        {
            var r = Stopwatch.StartNew();
            acao();
            var executar = r.Elapsed.TotalMilliseconds;
            janela.UpdateLayout();
            saida.AppendLine(CultureInfo.InvariantCulture, $"  {nome}: comando {executar:0.0} ms, layout {r.Elapsed.TotalMilliseconds - executar:0.0} ms");
        }
        if (vm.ConversaLonga is not null)
        {
            await MedirConversaLongaAsync(vm, saida, Etapa);
            medindo = false;
            await Task.Delay(100);
            return saida.ToString();
        }
        for (var i = 0; i < 4; i++)
        {
            Cronometrar("lateral", () => vm.AlternarLateralCommand.Execute(null));
            await Task.Delay(600);
        }
        for (var i = 0; i < 2; i++)
        {
            Cronometrar("projetos", () => vm.MostrarProjetosCommand.Execute(null));
            await Task.Delay(600);
            Cronometrar("ao vivo", () => vm.MostrarVisaoGeralCommand.Execute(null));
            await Task.Delay(600);
        }
        foreach (var chat in vm.Abertas.ToList())
        {
            Cronometrar("conversa", () => vm.MostrarChatCommand.Execute(chat));
            await Task.Delay(600);
        }
        var capsulas = janela.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("capsula")).ToList();
        await Etapa("repouso", () => Task.Delay(1500));
        await Etapa("trilho abre e fecha", async () =>
        {
            for (var i = 0; i < 3; i++)
            {
                foreach (var c in capsulas) c.Classes.Set("aberta", true);
                Marcar(240);
                await Task.Delay(500);
                foreach (var c in capsulas) c.Classes.Set("aberta", false);
                Marcar(240);
                await Task.Delay(500);
            }
        });
        var carga = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Background, (_, _) =>
        {
            var fim = relogio.Elapsed.TotalMilliseconds + 9;
            while (relogio.Elapsed.TotalMilliseconds < fim) { }
        });
        carga.Start();
        await Etapa("trilho com carga", async () =>
        {
            for (var i = 0; i < 3; i++)
            {
                foreach (var c in capsulas) c.Classes.Set("aberta", true);
                Marcar(240);
                await Task.Delay(500);
                foreach (var c in capsulas) c.Classes.Set("aberta", false);
                Marcar(240);
                await Task.Delay(500);
            }
        });
        await Etapa("lateral com carga", async () =>
        {
            for (var i = 0; i < 4; i++)
            {
                vm.AlternarLateralCommand.Execute(null);
                Marcar(300);
                await Task.Delay(700);
            }
        });
        carga.Stop();
        await Etapa("lateral abre e fecha", async () =>
        {
            for (var i = 0; i < 4; i++)
            {
                vm.AlternarLateralCommand.Execute(null);
                Marcar(300);
                await Task.Delay(700);
            }
        });
        await Etapa("conversas 1a vez", async () =>
        {
            foreach (var chat in vm.Abertas.ToList())
            {
                vm.MostrarChatCommand.Execute(chat);
                await Task.Delay(700);
            }
        });
        await Etapa("conversas de novo", async () =>
        {
            foreach (var chat in vm.Abertas.ToList())
            {
                vm.MostrarChatCommand.Execute(chat);
                await Task.Delay(700);
            }
        });
        await Etapa("troca de aba", async () =>
        {
            for (var i = 0; i < 4; i++)
            {
                vm.Principal.ProximaCommand.Execute(null);
                await Task.Delay(700);
            }
        });
        await Etapa("paginas", async () =>
        {
            for (var i = 0; i < 2; i++)
            {
                vm.MostrarProjetosCommand.Execute(null);
                await Task.Delay(700);
                vm.MostrarVisaoGeralCommand.Execute(null);
                await Task.Delay(700);
            }
        });
        await Etapa("ao vivo parado", () => Task.Delay(1500));
        await Etapa("espiar com o mouse antes", async () =>
        {
            vm.MostrarVisaoGeralCommand.Execute(null);
            await Task.Delay(500);
            var visao = janela.GetVisualDescendants().OfType<VisaoGeralView>().FirstOrDefault();
            foreach (var chat in vm.Abertas.ToList())
            {
                visao?.Preparar(chat);
                await Task.Delay(250);
                vm.VisaoGeral.EspiarCommand.Execute(chat);
                Marcar(500);
                await Task.Delay(700);
                vm.VisaoGeral.Espiado = null;
                await Task.Delay(400);
            }
        });
        await Etapa("espiar de novo", async () =>
        {
            foreach (var chat in vm.Abertas.ToList())
            {
                vm.VisaoGeral.EspiarCommand.Execute(chat);
                Marcar(500);
                await Task.Delay(700);
                vm.VisaoGeral.Espiado = null;
                await Task.Delay(400);
            }
        });
        await Etapa("dashboard", async () =>
        {
            vm.MostrarDashboardCommand.Execute(null);
            await Task.Delay(1500);
        });
        await Etapa("rolar dashboard", () => RolarAsync(janela.GetVisualDescendants().OfType<DashboardView>().FirstOrDefault()));
        await Etapa("rolar conversa", async () =>
        {
            if (vm.Abertas.OrderByDescending(c => c.Itens.Count).FirstOrDefault() is { } longa) vm.MostrarChatCommand.Execute(longa);
            await Task.Delay(900);
            await RolarAsync(janela.GetVisualDescendants().OfType<ChatView>().FirstOrDefault(c => c.IsEffectivelyVisible), paraCima: true);
        });
        medindo = false;
        await Task.Delay(100);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        using var processo = Process.GetCurrentProcess();
        saida.AppendLine(CultureInfo.InvariantCulture, $"memoria: gerenciada {GC.GetTotalMemory(false) / 1048576.0:0} MB, processo {processo.WorkingSet64 / 1048576.0:0} MB, coletas gen2 {GC.CollectionCount(2)}");
        return saida.ToString();
    }
}
