using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.Core.Git;
using Nexora.Temas;
using Nexora.ViewModels;

namespace Nexora.Testes.Tela;

[Collection("Orquestrador")]
public sealed class GravarDemo
{
    const int Largura = 1440, Altura = 900, Fps = 30;
    static readonly Point Inicio = new(1180, 760), Descanso = new(1010, 640);

    sealed record Roteiro(string Projeto, string Branch, string Titulo, string Pedido, string Comandos);

    sealed record Linha(long ViuPedido, long Clicou, Point Alvo, Point Partida)
    {
        public (Point Cursor, double Anel, double Raio) Em(long t)
        {
            if (t < ViuPedido + 1000) return (Partida, 0, 0);
            if (t < Clicou)
            {
                var p = Math.Clamp((t - ViuPedido - 1000) / 900.0, 0, 1);
                var s = p < .5 ? 4 * p * p * p : 1 - Math.Pow(-2 * p + 2, 3) / 2;
                return (Mistura(Partida, Alvo, s), 0, 0);
            }
            var depois = t - Clicou;
            var anel = depois < 450 ? 1 - depois / 450.0 : 0;
            var raio = 9 + 15 * Math.Min(1, depois / 450.0);
            var volta = Math.Clamp((depois - 900) / 1400.0, 0, 1);
            return (Mistura(Alvo, Descanso, 1 - Math.Pow(1 - volta, 3)), anel, raio);
        }

        static Point Mistura(Point a, Point b, double s) => new(a.X + (b.X - a.X) * s, a.Y + (b.Y - a.Y) * s);
    }

    static Roteiro[] Roteiros(bool ingles) => ingles
        ?
        [
            new("Store", "coupon-migration", "Coupon migration", "Add the coupon tables and migrate the database",
                "trabalhe 4.5 src/Store/Coupons/CouponService.cs,src/Store/Coupons/Coupon.cs ; permissao dotnet ef database update --project src/Store.Infra | Applies the coupon migration ; trabalhe 4 src/Store.Infra/Migrations/AddCoupons.cs,tests/Store.Tests/CouponTests.cs ; diga Migration applied. All coupon tests pass."),
            new("Store.Api", "rate-limiting", "Rate limiting", "Add rate limiting to the public API",
                "trabalhe 40 src/Api/RateLimit.cs,src/Api/Program.cs,tests/Api.Tests/RateLimitTests.cs ; diga Rate limiting is in place."),
            new("Notes", "offline-sync", "Offline sync", "Make notes sync when the connection comes back",
                "trabalhe 11 src/Notes/Sync.cs,src/Notes/OfflineQueue.cs,tests/Notes.Tests/SyncTests.cs ; diga Notes now sync offline and the new test passes."),
        ]
        :
        [
            new("Store", "coupon-migration", "Migração de cupons", "Crie as tabelas de cupom e migre o banco",
                "trabalhe 4.5 src/Store/Coupons/CouponService.cs,src/Store/Coupons/Coupon.cs ; permissao dotnet ef database update --project src/Store.Infra | Aplica a migration de cupons ; trabalhe 4 src/Store.Infra/Migrations/AddCoupons.cs,tests/Store.Tests/CouponTests.cs ; diga Migration aplicada. Todos os testes de cupom passam."),
            new("Store.Api", "rate-limiting", "Limite de requisições", "Coloque limite de requisições na API pública",
                "trabalhe 40 src/Api/RateLimit.cs,src/Api/Program.cs,tests/Api.Tests/RateLimitTests.cs ; diga O limite de requisições está no ar."),
            new("Notes", "offline-sync", "Sincronização offline", "Faça as notas sincronizarem quando a conexão voltar",
                "trabalhe 11 src/Notes/Sync.cs,src/Notes/OfflineQueue.cs,tests/Notes.Tests/SyncTests.cs ; diga As notas agora sincronizam offline e o teste novo passou."),
        ];

    [Fact]
    public Task Gravar() => Sessao.Rodar(async () =>
    {
        if (Environment.GetEnvironmentVariable("NEXORA_VIDEO") is not { Length: > 0 } saida) return;
        var ingles = (Environment.GetEnvironmentVariable("NEXORA_IDIOMA") ?? "en") == "en";
        var raiz = Directory.CreateTempSubdirectory("nexora-demo-").FullName;
        Directory.CreateDirectory(saida);

        var roteiros = Roteiros(ingles);
        var arquivoDeRoteiros = Path.Combine(raiz, "roteiros.json");
        File.WriteAllText(arquivoDeRoteiros, new JsonObject(roteiros.Select(r => KeyValuePair.Create<string, JsonNode?>(r.Pedido, r.Comandos))).ToJsonString());
        foreach (var r in roteiros)
        {
            var pasta = Path.Combine(raiz, r.Projeto);
            Directory.CreateDirectory(pasta);
            await Task.Run(async () =>
            {
                await ComandoGit.ExecutarAsync(pasta, "init", "-q", "-b", r.Branch);
                await ComandoGit.ExecutarAsync(pasta, "-c", "user.name=demo", "-c", "user.email=demo@example.com", "commit", "-q", "--allow-empty", "-m", "start");
            });
        }

        var simuladoAntes = SessaoClaude.Simulado;
        var raizesAntes = App.Config.Raizes;
        var lateralAntes = App.Config.LateralVisivel;
        var temaAntes = App.Config.Tema;
        var fundoAntes = App.Config.ModoFundo;
        Core.Texto.Idioma.Definir(ingles ? "en" : "pt");
        Environment.SetEnvironmentVariable("AGENTE_DE_TESTE_ROTEIROS", arquivoDeRoteiros);
        SessaoClaude.Simulado = AgenteDeTeste.Dll;
        App.Config.Raizes = [raiz];
        App.Config.LateralVisivel = false;
        App.Config.Tema = "padrao";
        App.Config.ModoFundo = "solido";
        Catalogo.Modo = "solido";
        Catalogo.Escolher("padrao");
        JanelaVM? vm = null;
        var brutos = Path.Combine(saida, "quadros.bgra");
        var tempos = new List<long>();
        var formato = "bgra";
        Linha? linha = null;
        var cores = new List<string>();
        string Cor(string momento)
        {
            var laranja = Application.Current!.Resources.TryGetResource("Laranja", null, out var v) ? (v as SolidColorBrush)?.Color.ToString() : "?";
            return $"{momento}: tema={Catalogo.Atual.Id} laranja={laranja} variante={Application.Current.ActualThemeVariant}";
        }
        try
        {
            vm = new JanelaVM();
            var janela = new Janela { DataContext = vm, Width = Largura, Height = Altura };
            ToolTip.SetServiceEnabled(janela, false);
            janela.Show();
            vm.MostrarVisaoGeralCommand.Execute(null);
            vm.VisaoGeral.ModoMesa = false;
            vm.VisaoGeral.ModoQuadro = true;
            Catalogo.Escolher("padrao");
            cores.Add(Cor("abriu"));

            var chats = roteiros.Select(r =>
            {
                var chat = vm.CriarSessao(new Local(r.Projeto, Path.Combine(raiz, r.Projeto), []));
                chat.Titulo = r.Titulo;
                return chat;
            }).ToList();
            await Esperar(400);
            for (var i = 0; i < chats.Count; i++) chats[i].EnviarTexto(roteiros[i].Pedido);

            await using var bruto = new FileStream(brutos, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 22);
            var buffer = new byte[Largura * 4];
            var relogio = Stopwatch.StartNew();
            long? viuPedido = null, clicou = null, terminou = null;
            Point alvo = default;
            while (true)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                var agora = relogio.ElapsedMilliseconds;

                if (viuPedido is null && chats[0].PermissaoPendente is not null && BotaoPermitir(janela, ingles) is { } botao
                    && botao.TranslatePoint(new Point(botao.Bounds.Width / 2, botao.Bounds.Height / 2), janela) is { } centro)
                {
                    viuPedido = agora;
                    cores.Add(Cor("pedido"));
                    alvo = centro;
                }
                if (viuPedido is { } visto && clicou is null && agora - visto >= 2600)
                {
                    janela.MouseMove(alvo);
                    janela.MouseDown(alvo, MouseButton.Left);
                    janela.MouseUp(alvo, MouseButton.Left);
                    clicou = agora;
                }
                if (terminou is null && clicou is not null && chats[0].Estado == EstadoChat.Pronta && chats[2].Estado == EstadoChat.Pronta) terminou = agora;
                if (terminou is { } fim && agora - fim > 2600) break;
                if (agora > 40_000) throw new InvalidOperationException("A demonstração não terminou em 40 s: " + string.Join(", ", chats.Select(x => x.Estado)));

                using var quadro = janela.CaptureRenderedFrame() ?? throw new InvalidOperationException("Sem quadro.");
                using (var pixels = quadro.Lock())
                {
                    formato = pixels.Format == Avalonia.Platform.PixelFormat.Rgba8888 ? "rgba" : "bgra";
                    for (var y = 0; y < Altura; y++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(pixels.Address + y * pixels.RowBytes, buffer, 0, buffer.Length);
                        bruto.Write(buffer, 0, buffer.Length);
                    }
                }
                tempos.Add(agora);
                await Task.Delay(5);
            }
            cores.Add(Cor("fim"));
            linha = new Linha(viuPedido!.Value, clicou!.Value, alvo, Inicio);
            janela.Close();
        }
        finally
        {
            foreach (var chat in vm?.Abertas.ToList() ?? []) chat.Encerrar(esperar: true);
            SessaoClaude.Simulado = simuladoAntes;
            App.Config.Raizes = raizesAntes;
            App.Config.LateralVisivel = lateralAntes;
            App.Config.Tema = temaAntes;
            App.Config.ModoFundo = fundoAntes;
            Environment.SetEnvironmentVariable("AGENTE_DE_TESTE_ROTEIROS", null);
            Core.Texto.Idioma.Definir("pt");
        }

        var video = Path.Combine(saida, "demo.mp4");
        await Montar(brutos, tempos, linha, Seta(), video, formato);
        File.WriteAllText(Path.Combine(saida, "marcos.txt"), string.Create(CultureInfo.InvariantCulture,
            $"pedido {linha.ViuPedido}\nclique {linha.Clicou}\nduracao {tempos[^1]}\nquadros {tempos.Count}\n{string.Join("\n", cores)}\n"));
        File.Delete(brutos);
    });

    static async Task Montar(string brutos, List<long> tempos, Linha linha, (byte[] Pixels, int Lado) seta, string video, string formato)
    {
        var info = new ProcessStartInfo("ffmpeg") { RedirectStandardInput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", formato, "-s", $"{Largura}x{Altura}", "-r", Fps.ToString(CultureInfo.InvariantCulture), "-i", "-",
                     "-vf", "scale=1280:-2:flags=lanczos", "-c:v", "libx264", "-preset", "slow", "-crf", "23", "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-an", video })
            info.ArgumentList.Add(a);
        using var ffmpeg = Process.Start(info) ?? throw new InvalidOperationException("O ffmpeg não abriu.");
        var erros = ffmpeg.StandardError.ReadToEndAsync();
        var tamanho = Largura * Altura * 4;
        var quadro = new byte[tamanho];
        await using (var leitor = new FileStream(brutos, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 22))
        await using (var entrada = ffmpeg.StandardInput.BaseStream)
        {
            var atual = -1;
            for (long t = 0; t <= tempos[^1]; t += 1000 / Fps)
            {
                var indice = Math.Max(0, tempos.FindLastIndex(x => x <= t));
                if (indice != atual)
                {
                    leitor.Seek((long)indice * tamanho, SeekOrigin.Begin);
                    await leitor.ReadExactlyAsync(quadro);
                    atual = indice;
                }
                var saida = (byte[])quadro.Clone();
                var (cursor, anel, raio) = linha.Em(t);
                if (anel > 0) Anel(saida, linha.Alvo, raio, anel, formato == "rgba");
                Colar(saida, seta, (int)cursor.X - 1, (int)cursor.Y - 1);
                await entrada.WriteAsync(saida);
            }
        }
        await ffmpeg.WaitForExitAsync();
        if (ffmpeg.ExitCode != 0) throw new InvalidOperationException("ffmpeg: " + await erros);
    }

    static (byte[] Pixels, int Lado) Seta()
    {
        const int lado = 26;
        var forma = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M1.5,1.5 L1.5,18.5 L6.1,14.4 L9.1,21.3 L11.9,20.1 L8.9,13.4 L14.9,13.4 Z"),
            Fill = Brushes.White,
            Stroke = new SolidColorBrush(Color.FromRgb(22, 21, 20)),
            StrokeThickness = 1.4,
            Width = lado,
            Height = lado,
        };
        forma.Measure(new Size(lado, lado));
        forma.Arrange(new Rect(0, 0, lado, lado));
        using var alvo = new RenderTargetBitmap(new PixelSize(lado, lado));
        alvo.Render(forma);
        using var copia = new WriteableBitmap(new PixelSize(lado, lado), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using (var destino = copia.Lock())
            alvo.CopyPixels(new PixelRect(0, 0, lado, lado), destino.Address, destino.RowBytes * lado, destino.RowBytes);
        var pixels = new byte[lado * lado * 4];
        using (var lido = copia.Lock())
            for (var y = 0; y < lado; y++)
                System.Runtime.InteropServices.Marshal.Copy(lido.Address + y * lido.RowBytes, pixels, y * lado * 4, lado * 4);
        return (pixels, lado);
    }

    static void Colar(byte[] quadro, (byte[] Pixels, int Lado) seta, int x0, int y0)
    {
        for (var y = 0; y < seta.Lado; y++)
            for (var x = 0; x < seta.Lado; x++)
            {
                int px = x0 + x, py = y0 + y;
                if (px < 0 || py < 0 || px >= Largura || py >= Altura) continue;
                var o = (y * seta.Lado + x) * 4;
                var a = seta.Pixels[o + 3];
                if (a == 0) continue;
                var d = (py * Largura + px) * 4;
                for (var c = 0; c < 3; c++) quadro[d + c] = (byte)(seta.Pixels[o + c] + quadro[d + c] * (255 - a) / 255);
            }
    }

    static void Anel(byte[] quadro, Point centro, double raio, double opacidade, bool rgba)
    {
        var (r, g, b) = rgba ? (218, 180, 189) : (189, 180, 218);
        for (var y = (int)(centro.Y - raio - 3); y <= centro.Y + raio + 3; y++)
            for (var x = (int)(centro.X - raio - 3); x <= centro.X + raio + 3; x++)
            {
                if (x < 0 || y < 0 || x >= Largura || y >= Altura) continue;
                var distancia = Math.Abs(Math.Sqrt((x - centro.X) * (x - centro.X) + (y - centro.Y) * (y - centro.Y)) - raio);
                var a = Math.Clamp(1.6 - distancia, 0, 1) * opacidade * .9;
                if (a <= 0) continue;
                var d = (y * Largura + x) * 4;
                quadro[d] = (byte)(b * a + quadro[d] * (1 - a));
                quadro[d + 1] = (byte)(g * a + quadro[d + 1] * (1 - a));
                quadro[d + 2] = (byte)(r * a + quadro[d + 2] * (1 - a));
            }
    }

    static Button? BotaoPermitir(Window janela, bool ingles)
    {
        var rotulo = ingles ? "Allow" : "Permitir";
        return janela.GetVisualDescendants().OfType<Button>()
            .Where(b => b.IsEffectivelyVisible && (b.Content as string ?? (b.Content as TextBlock)?.Text) == rotulo)
            .OrderBy(b => b.TranslatePoint(new Point(0, 0), janela)?.Y ?? double.MaxValue)
            .FirstOrDefault();
    }

    static async Task Esperar(int ms)
    {
        var fim = Environment.TickCount64 + ms;
        do
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await Task.Delay(16);
        }
        while (Environment.TickCount64 < fim);
    }
}
