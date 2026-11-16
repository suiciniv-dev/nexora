using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Temas;
using Nexora.ViewModels;

namespace Nexora.Testes.Tela;

public sealed class PasseioAttribute : FactAttribute
{
    public PasseioAttribute()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_ROTEIRO") is not { Length: > 0 }) Skip = "Roda só com NEXORA_ROTEIRO apontando para um roteiro (scripts/passeio.ps1).";
    }
}

public sealed class Passeio
{
    [Passeio]
    public Task PercorrerRoteiro()
    {
        var roteiro = Environment.GetEnvironmentVariable("NEXORA_ROTEIRO")!;
        var saida = Environment.GetEnvironmentVariable("NEXORA_FOTOS") is { Length: > 0 } s ? s : Path.Combine(Path.GetDirectoryName(roteiro)!, "fotos");
        return Sessao.Rodar(() => new Roteiro(roteiro, saida).Rodar());
    }
}

sealed class Roteiro(string arquivo, string saida)
{
    sealed record Alvo(Control Controle, string Tipo, string Rotulo, Rect Caixa, string? CobertoPor);

    const int Silencio = 350;
    const int LimiteDeAssentar = 6000;
    static readonly string[] Alertas = ["LENTO", "TRAVOU", "DEMORA", "INTERCEPTADO", "SEM EFEITO", "ERRO"];

    readonly List<string> _log = [];
    readonly List<string> _externos = [];
    readonly List<(string Passo, long Ms)> _tempos = [];
    List<Alvo> _numerados = [];
    Janela _janela = null!;
    JanelaVM _vm = null!;
    Visual? _recebeu;
    long _ultimoLayout;
    int _bytesPorLinha;
    int _erros;

    public async Task Rodar()
    {
        Directory.CreateDirectory(saida);
        var estilo = new Style(x => x.OfType<Popup>()) { Setters = { new Setter(Popup.ShouldUseOverlayLayerProperty, true) } };
        Application.Current!.Styles.Add(estilo);
        Sistema.SoAnotar = Externo;
        Dispatcher.UIThread.UnhandledException += ErroSolto;
        var demoAntes = JanelaVM.Demo;
        try
        {
            var abertura = Stopwatch.StartNew();
            Montar();
            var montagem = abertura.ElapsedMilliseconds;
            var (assentou, travou) = await Assentar(10000);
            Anotar($"Abertura: janela montada em {montagem} ms, tela estável em {Tempo(assentou, montagem)}, maior trava {travou} ms.");
            var linhas = File.ReadAllLines(arquivo);
            for (var i = 0; i < linhas.Length; i++)
            {
                var linha = linhas[i].Trim();
                if (linha.Length == 0 || linha.StartsWith('#')) continue;
                Anotar($"[{i + 1}] {linha}");
                try
                {
                    await Executar(linha, $"[{i + 1}] {linha}");
                }
                catch (Exception ex)
                {
                    _erros++;
                    Anotar($"    ERRO {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        finally
        {
            Resumir();
            File.WriteAllLines(Path.Combine(saida, "log.txt"), _log, new UTF8Encoding(false));
            Sistema.SoAnotar = null;
            Dispatcher.UIThread.UnhandledException -= ErroSolto;
            try
            {
                _janela?.Close();
            }
            catch (Exception)
            {
            }
            Application.Current.Styles.Remove(estilo);
            JanelaVM.Demo = demoAntes;
            Core.Texto.Idioma.Definir("pt");
        }
    }

    void Resumir()
    {
        Anotar("");
        Anotar($"Fim: {_erros} erro(s), {_externos.Count} pedido(s) de abrir algo fora do Nexora (bloqueados).");
        var contagem = Alertas.Select(a => (a, _log.Count(l => l.TrimStart().StartsWith(a + ":") || l.TrimStart().StartsWith(a + " ")))).Where(c => c.Item2 > 0);
        Anotar("Alertas: " + (contagem.Any() ? string.Join(", ", contagem.Select(c => $"{c.a} {c.Item2}")) : "nenhum"));
        if (_tempos.Count == 0) return;
        Anotar("Passos mais lentos (gesto + tela assentar):");
        foreach (var (passo, ms) in _tempos.OrderByDescending(t => t.Ms).Take(5)) Anotar($"    {ms,5} ms  {passo}");
    }

    void Anotar(string texto) => _log.Add(texto);

    void Externo(string pedido)
    {
        _externos.Add(pedido);
        Anotar($"    (abriria fora do Nexora, bloqueado: {pedido})");
    }

    void ErroSolto(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _erros++;
        Anotar($"    ERRO SOLTO {e.Exception.GetType().Name}: {e.Exception.Message}");
        e.Handled = true;
    }

    void Montar()
    {
        JanelaVM.Isolada = true;
        JanelaVM.Demo = true;
        Core.Texto.Idioma.Definir(Environment.GetEnvironmentVariable("NEXORA_IDIOMA") ?? "pt");
        Catalogo.Modo = App.Config.ModoFundo == "parado" ? "vidro" : App.Config.ModoFundo;
        Catalogo.Escolher(App.Config.Tema);
        _vm = new JanelaVM();
        _janela = new Janela { DataContext = _vm, Width = 1440, Height = 900 };
        ToolTip.SetServiceEnabled(_janela, false);
        _janela.AddHandler(InputElement.PointerPressedEvent, (_, e) => _recebeu ??= e.Source as Visual, RoutingStrategies.Tunnel, handledEventsToo: true);
        _janela.LayoutUpdated += (_, _) => _ultimoLayout = Environment.TickCount64;
        _vm.PreencherDemo();
        _vm.Novidade = null;
        if (int.TryParse(Environment.GetEnvironmentVariable("NEXORA_CONVERSA_LONGA"), out var turnosLongos))
        {
            _vm.AdicionarConversaLonga(turnosLongos);
            _vm.AbrirConversaLonga(true);
        }
        _janela.Show();
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

    async Task<(long Assentou, long Travou)> Assentar(int limite = LimiteDeAssentar)
    {
        var inicio = Environment.TickCount64;
        _ultimoLayout = inicio;
        var anterior = inicio;
        long travou = 0;
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var agora = Environment.TickCount64;
            travou = Math.Max(travou, agora - anterior);
            if (agora - _ultimoLayout >= Silencio && agora - inicio >= 100) return (_ultimoLayout - inicio, travou);
            if (agora - inicio >= limite) return (-1, travou);
            await Task.Delay(16);
            anterior = Environment.TickCount64;
            travou = Math.Max(travou, anterior - agora - 40);
        }
    }

    static string Tempo(long assentou, long base_ = 0) => assentou < 0 ? "mais de 6 s (o layout não parou de mudar)" : $"{assentou + base_} ms";

    async Task Executar(string linha, string passo)
    {
        var espaco = linha.IndexOf(' ');
        var verbo = (espaco < 0 ? linha : linha[..espaco]).ToLowerInvariant();
        var resto = espaco < 0 ? "" : linha[(espaco + 1)..].Trim();
        switch (verbo)
        {
            case "foto":
                await Fotografar(resto.Length > 0 ? resto : $"foto-{DateTime.Now:HHmmss}");
                return;
            case "listar":
                foreach (var linhaAlvo in Listar(Alvos())) Anotar("    " + linhaAlvo);
                return;
            case "esperar":
                await Esperar(int.Parse(resto, CultureInfo.InvariantCulture));
                return;
            case "dicas":
                ToolTip.SetServiceEnabled(_janela, resto.ToLowerInvariant() is "sim" or "s" or "on");
                return;
            case "boasvindas":
                _vm.BoasVindas.Reiniciar();
                if (int.TryParse(resto, CultureInfo.InvariantCulture, out var passoBv)) _vm.BoasVindas.Passo = passoBv;
                _vm.MostraBoasVindas = true;
                await Assentar();
                return;
        }

        Alvo? alvo = null;
        var ponto = default(Point);
        var giro = 3;
        if (verbo is "clicar" or "duplo" or "direito" or "passar" or "rolar")
        {
            var texto = resto;
            if (verbo == "rolar" && resto.Split(' ') is { Length: > 1 } partes && int.TryParse(partes[^1], out var g))
            {
                giro = g;
                texto = string.Join(' ', partes[..^1]);
            }
            (alvo, ponto) = Achar(texto);
            if (verbo != "passar")
            {
                _janela.MouseMove(ponto);
                await Esperar(150);
            }
        }
        if (verbo is "digitar" or "tecla") Anotar($"    foco em {Focado()}");

        var antes = Pixels();
        _recebeu = null;
        var relogio = Stopwatch.StartNew();
        switch (verbo)
        {
            case "clicar" or "duplo" or "direito":
                {
                    var botao = verbo == "direito" ? MouseButton.Right : MouseButton.Left;
                    for (var i = 0; i < (verbo == "duplo" ? 2 : 1); i++)
                    {
                        _janela.MouseDown(ponto, botao);
                        _janela.MouseUp(ponto, botao);
                    }
                    break;
                }
            case "passar":
                _janela.MouseMove(ponto);
                break;
            case "rolar":
                _janela.MouseWheel(ponto, new Vector(0, -giro));
                break;
            case "digitar":
                _janela.KeyTextInput(resto);
                break;
            case "tecla":
                Tecla(resto);
                break;
            case "tamanho":
                {
                    var partes = resto.ToLowerInvariant().Split('x');
                    (_janela.Width, _janela.Height) = (double.Parse(partes[0], CultureInfo.InvariantCulture), double.Parse(partes[1], CultureInfo.InvariantCulture));
                    break;
                }
            case "tema":
                Catalogo.Escolher(resto);
                break;
            case "pagina":
                Pagina(resto.ToLowerInvariant());
                break;
            default:
                throw new InvalidOperationException($"Comando desconhecido: {verbo}. Use clicar, duplo, direito, passar, rolar, digitar, tecla, esperar, foto, listar, dicas, tamanho, tema ou pagina.");
        }
        var gesto = relogio.ElapsedMilliseconds;
        var recebeu = _recebeu;
        var (assentou, travou) = await Assentar();
        var mudanca = Diferenca(antes, Pixels());

        var acao = verbo switch
        {
            "clicar" => "clique em",
            "duplo" => "duplo clique em",
            "direito" => "clique direito em",
            "passar" => "mouse sobre",
            "rolar" => $"rolou {giro} em",
            _ => verbo,
        };
        var onde = alvo is null ? "" : $" {Descrever(alvo)} ({ponto.X:0},{ponto.Y:0})";
        var tela = mudanca.Pixels switch
        {
            < 0 => "a tela mudou de tamanho",
            0 => "nada mudou na tela",
            _ => $"mudou {mudanca.Fracao.ToString("P1", CultureInfo.GetCultureInfo("pt-BR"))} da tela em ({mudanca.Regiao.X},{mudanca.Regiao.Y}) {mudanca.Regiao.Width}x{mudanca.Regiao.Height}",
        };
        Anotar($"    {acao}{onde} · resposta {(assentou < 0 ? "mais de 6 s" : $"{gesto + assentou} ms")} (gesto {gesto} ms) · {tela}");
        _tempos.Add((passo, gesto + Math.Max(0, assentou)));

        if (verbo is "clicar" or "duplo" or "direito" && alvo is not null && !resto.StartsWith('@'))
        {
            if (recebeu is null) Anotar("    INTERCEPTADO: o clique não chegou a nenhum elemento.");
            else if (recebeu != alvo.Controle && !alvo.Controle.IsVisualAncestorOf(recebeu))
                Anotar($"    INTERCEPTADO: o clique foi parar em {DescreverRecebedor(recebeu)}, não em {Descrever(alvo)}.");
        }
        if (gesto > 400) Anotar($"    LENTO: o gesto prendeu a interface por {gesto} ms (repita o passo: a primeira vez de cada tela inclui o JIT).");
        if (travou > 400) Anotar($"    TRAVOU: a interface ficou parada uns {travou} ms enquanto a tela assentava.");
        if (assentou < 0 || assentou > 1200) Anotar($"    DEMORA: a tela levou {Tempo(assentou)} para parar de mudar o layout.");
        if (mudanca.Pixels == 0 && verbo is "clicar" or "duplo" or "direito" or "tecla" or "pagina") Anotar("    SEM EFEITO: nada mudou na tela.");
    }

    byte[] Pixels()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var quadro = _janela.CaptureRenderedFrame();
        if (quadro is null) return [];
        using var trava = quadro.Lock();
        var bytes = new byte[trava.RowBytes * trava.Size.Height];
        Marshal.Copy(trava.Address, bytes, 0, bytes.Length);
        _bytesPorLinha = trava.RowBytes;
        return bytes;
    }

    (int Pixels, double Fracao, PixelRect Regiao) Diferenca(byte[] a, byte[] b)
    {
        if (a.Length == 0 || a.Length != b.Length) return (-1, 1, default);
        var pa = MemoryMarshal.Cast<byte, int>(a);
        var pb = MemoryMarshal.Cast<byte, int>(b);
        var porLinha = _bytesPorLinha / 4;
        int n = 0, x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
        for (var i = 0; i < pa.Length; i++)
        {
            if (pa[i] == pb[i]) continue;
            n++;
            var (y, x) = Math.DivRem(i, porLinha);
            x0 = Math.Min(x0, x);
            x1 = Math.Max(x1, x);
            y0 = Math.Min(y0, y);
            y1 = Math.Max(y1, y);
        }
        return n == 0 ? (0, 0, default) : (n, n / (double)pa.Length, new PixelRect(x0, y0, x1 - x0 + 1, y1 - y0 + 1));
    }

    static string DescreverRecebedor(Visual v)
    {
        if (v.GetSelfAndVisualAncestors().Any(a => a.GetType().Name == "LightDismissOverlayLayer"))
            return "a camada de um popup aberto (o clique só fechou o popup)";
        var dono = v.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(Clicavel) ?? v as Control;
        return dono is null ? v.GetType().Name : $"{dono.GetType().Name} \"{Curto(Rotulo(dono), 60)}\"";
    }

    void Pagina(string nome)
    {
        var comando = nome switch
        {
            "visao" or "sessoes" => _vm.MostrarVisaoGeralCommand,
            "projetos" => _vm.MostrarProjetosCommand,
            "recursos" => _vm.MostrarRecursosCommand,
            "uso" => _vm.MostrarUsoCommand,
            "issues" => _vm.MostrarIssuesCommand,
            "agendados" => _vm.MostrarAgendadosCommand,
            "configuracoes" => _vm.MostrarConfiguracoesCommand,
            "temas" => _vm.MostrarTemasCommand,
            "orquestra" => _vm.MostrarOrquestraCommand,
            _ => throw new InvalidOperationException($"Página desconhecida: {nome}. Use visao, projetos, recursos, uso, issues, agendados, configuracoes, temas ou orquestra."),
        };
        comando.Execute(null);
    }

    void Tecla(string combinacao)
    {
        var partes = combinacao.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var mods = RawInputModifiers.None;
        foreach (var m in partes[..^1])
            mods |= m.ToLowerInvariant() switch
            {
                "ctrl" or "control" => RawInputModifiers.Control,
                "shift" => RawInputModifiers.Shift,
                "alt" => RawInputModifiers.Alt,
                "win" or "meta" or "cmd" => RawInputModifiers.Meta,
                _ => throw new InvalidOperationException($"Modificador desconhecido: {m}"),
            };
        var nome = partes[^1];
        var tecla = nome.ToLowerInvariant() switch
        {
            "esc" => PhysicalKey.Escape,
            "cima" or "up" => PhysicalKey.ArrowUp,
            "baixo" or "down" => PhysicalKey.ArrowDown,
            "esquerda" or "left" => PhysicalKey.ArrowLeft,
            "direita" or "right" => PhysicalKey.ArrowRight,
            "espaco" or "space" => PhysicalKey.Space,
            "apagar" => PhysicalKey.Backspace,
            "del" => PhysicalKey.Delete,
            _ when nome.Length == 1 && char.IsDigit(nome[0]) => Enum.Parse<PhysicalKey>("Digit" + nome),
            _ => Enum.Parse<PhysicalKey>(nome, ignoreCase: true),
        };
        _janela.KeyPressQwerty(tecla, mods);
        _janela.KeyReleaseQwerty(tecla, mods);
    }

    string Focado() => _janela.FocusManager?.GetFocusedElement() is Control c ? $"{c.GetType().Name} {Rotulo(c)}".Trim() : "nada";

    (Alvo Alvo, Point Ponto) Achar(string texto)
    {
        if (texto.StartsWith('@'))
        {
            var xy = texto[1..].Split(',');
            var ponto = new Point(double.Parse(xy[0], CultureInfo.InvariantCulture), double.Parse(xy[1], CultureInfo.InvariantCulture));
            var topo = _janela.InputHitTest(ponto) as Control ?? _janela;
            return (new Alvo(topo, topo.GetType().Name, Rotulo(topo), new Rect(ponto, new Size(1, 1)), null), ponto);
        }
        if (texto.StartsWith('#') && int.TryParse(texto[1..], out var numero))
        {
            if (numero < 1 || numero > _numerados.Count) throw new InvalidOperationException($"Não há o número {numero} na última foto ({_numerados.Count} marcados).");
            var marcado = _numerados[numero - 1];
            var atual = Alvos().FirstOrDefault(a => a.Controle == marcado.Controle)
                ?? throw new InvalidOperationException($"O #{numero} ({marcado.Rotulo}) não está mais visível.");
            return (atual, atual.Caixa.Center);
        }
        var ordem = 1;
        if (texto.LastIndexOf('#') is var cerquilha and > 0 && int.TryParse(texto[(cerquilha + 1)..], out var n))
        {
            ordem = n;
            texto = texto[..cerquilha].Trim();
        }
        texto = texto.Trim('"');
        var alvos = Alvos();
        var iguais = alvos.Where(a => a.Rotulo.Split(" | ").Any(p => p.Equals(texto, StringComparison.OrdinalIgnoreCase))).ToList();
        var candidatos = iguais.Count > 0 ? iguais : alvos.Where(a => a.Rotulo.Contains(texto, StringComparison.OrdinalIgnoreCase)).ToList();
        if (candidatos.Count == 0)
            candidatos = Textos().Where(a => a.Rotulo.Contains(texto, StringComparison.OrdinalIgnoreCase)).ToList();
        if (candidatos.Count == 0) throw new InvalidOperationException($"Nada visível com \"{texto}\".");
        if (ordem > candidatos.Count) throw new InvalidOperationException($"Só há {candidatos.Count} com \"{texto}\".");
        if (candidatos.Count > 1) Anotar($"    {candidatos.Count} combinam com \"{texto}\", usei o {ordem}º (use \"texto#n\" ou \"#número\" da foto)");
        var escolhido = candidatos[ordem - 1];
        if (escolhido.CobertoPor is { } coberto) Anotar($"    atenção: o centro está coberto por {coberto}");
        return (escolhido, escolhido.Caixa.Center);
    }

    static string Descrever(Alvo a) => $"{a.Tipo} \"{Curto(a.Rotulo, 70)}\"";

    static string Curto(string texto, int max) => texto.Length <= max ? texto : texto[..(max - 1)] + "…";

    static bool Clicavel(Control c) =>
        c is Button or MenuItem or ListBoxItem or TabItem or ComboBox or TextBox or Slider or ToggleSwitch
        || c is not TextBlock && c.TemplatedParent is not TextBox && (c.IsSet(InputElement.CursorProperty) || ToolTip.GetTip(c) is string);

    List<Alvo> Alvos() => Coletar(Clicavel);

    List<Alvo> Textos() => Coletar(c => c is TextBlock { Text.Length: > 0 });

    List<Alvo> Coletar(Func<Control, bool> filtro)
    {
        var lista = new List<Alvo>();
        var janela = new Rect(_janela.ClientSize);
        foreach (var c in _janela.GetVisualDescendants().OfType<Control>())
        {
            if (!filtro(c) || !c.IsEffectivelyVisible || c.Opacity <= 0.01) continue;
            if (Caixa(c) is not { } caixa) continue;
            caixa = caixa.Intersect(janela);
            if (caixa.Width < 2 || caixa.Height < 2) continue;
            var rotulo = Rotulo(c);
            if (rotulo.Length == 0 && c is not TextBox) rotulo = c.Name ?? "";
            lista.Add(new Alvo(c, c.GetType().Name, rotulo, caixa, Cobertura(c, caixa.Center)));
        }
        return lista;
    }

    Rect? Caixa(Control c)
    {
        if (c.TransformToVisual(_janela) is not { } m) return null;
        var caixa = new Rect(c.Bounds.Size).TransformToAABB(m);
        foreach (var pai in c.GetVisualAncestors().OfType<Visual>())
        {
            if (!pai.ClipToBounds || pai.TransformToVisual(_janela) is not { } mp) continue;
            caixa = caixa.Intersect(new Rect(pai.Bounds.Size).TransformToAABB(mp));
            if (caixa.Width <= 0 || caixa.Height <= 0) return null;
        }
        return caixa;
    }

    string? Cobertura(Control c, Point centro)
    {
        if (_janela.InputHitTest(centro) is not Visual topo) return null;
        if (topo == c || c.IsVisualAncestorOf(topo) || topo.IsVisualAncestorOf(c)) return null;
        var dono = topo.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(Clicavel) ?? topo as Control;
        return dono is null ? topo.GetType().Name : $"{dono.GetType().Name} \"{Curto(Rotulo(dono), 50)}\"";
    }

    static string Rotulo(Control c)
    {
        var partes = new List<string>();
        void Por(string? s)
        {
            s = s?.Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (s is { Length: > 0 } && !partes.Contains(s)) partes.Add(s);
        }
        Por(AutomationProperties.GetName(c));
        if (c is TextBlock tb) Por(tb.Text);
        if (c is ContentControl { Content: string conteudo }) Por(conteudo);
        if (c is HeaderedSelectingItemsControl { Header: string cabecalho }) Por(cabecalho);
        if (c is TextBox caixa)
        {
            Por(caixa.PlaceholderText is { Length: > 0 } dica ? $"campo: {dica}" : "campo");
            if (caixa.Text is { Length: > 0 } t) Por($"texto: {Curto(t, 40)}");
        }
        if (c is ToggleButton { IsChecked: true }) Por("[marcado]");
        if (c is not TextBlock and not TextBox)
            foreach (var t in c.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Text is { Length: > 0 }).Take(3))
                Por(t.Text);
        if (ToolTip.GetTip(c) is string tip) Por($"dica: {tip}");
        return Curto(string.Join(" | ", partes), 140);
    }

    static IEnumerable<string> Listar(List<Alvo> alvos) =>
        alvos.Select((a, i) => $"#{i + 1} {a.Tipo} \"{a.Rotulo}\" em ({a.Caixa.X:0},{a.Caixa.Y:0}) {a.Caixa.Width:0}x{a.Caixa.Height:0}{(a.CobertoPor is { } c ? $"  [coberto por {c}]" : "")}");

    async Task Fotografar(string nome)
    {
        await Esperar(150);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var quadro = _janela.CaptureRenderedFrame() ?? throw new InvalidOperationException("A janela não rendeu nenhum quadro.");
        var arquivoFoto = Path.Combine(saida, nome + ".png");
        quadro.Save(arquivoFoto, PngBitmapEncoderOptions.Default);
        var todos = Alvos();
        _numerados = [.. todos.Where(a => a.CobertoPor is null)];
        var cobertos = todos.Where(a => a.CobertoPor is not null).Select(a => $"- {a.Tipo} \"{a.Rotulo}\" em ({a.Caixa.X:0},{a.Caixa.Y:0}) coberto por {a.CobertoPor}");
        File.WriteAllLines(Path.Combine(saida, nome + ".txt"), [.. Listar(_numerados), "", "Cobertos por outro elemento (sem número):", .. cobertos], new UTF8Encoding(false));
        Marcar(quadro, _numerados, Path.Combine(saida, nome + "-marcas.png"));
        Anotar($"    salvei {nome}.png, {nome}-marcas.png e {nome}.txt ({_numerados.Count} clicáveis numerados, {todos.Count - _numerados.Count} cobertos)");
    }

    static void Marcar(Bitmap quadro, List<Alvo> alvos, string destino)
    {
        var tamanho = quadro.PixelSize;
        using var imagem = new RenderTargetBitmap(tamanho, new Vector(96, 96));
        var cores = new[] { Colors.Magenta, Colors.Cyan, Colors.Yellow, Colors.Lime, Colors.OrangeRed };
        var fonte = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        using (var ctx = imagem.CreateDrawingContext())
        {
            ctx.DrawImage(quadro, new Rect(0, 0, tamanho.Width, tamanho.Height));
            for (var i = 0; i < alvos.Count; i++)
            {
                var cor = cores[i % cores.Length];
                var caixa = alvos[i].Caixa;
                ctx.DrawRectangle(null, new Pen(new SolidColorBrush(cor), 1.2), caixa);
                var texto = new FormattedText((i + 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, fonte, 10, Brushes.Black);
                var etiqueta = new Rect(caixa.X, Math.Max(0, caixa.Y - texto.Height + 2), texto.Width + 4, texto.Height);
                ctx.FillRectangle(new SolidColorBrush(cor), etiqueta);
                ctx.DrawText(texto, new Point(etiqueta.X + 2, etiqueta.Y));
            }
        }
        imagem.Save(destino, PngBitmapEncoderOptions.Default);
    }
}
