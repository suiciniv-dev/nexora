using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using Nexora.Core.Claude;
using Nexora.Core.Desempenho;
using Nexora.ViewModels;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora;

public sealed record DadosDeDesempenho(Amostra? Agora, ResumoDoDia Hoje, IReadOnlyList<ResumoDoDia> Anteriores, DateTime Desde, bool Grava);

public sealed class Batimento : IDisposable
{
    public const int LimiteDaFoto = 300;
    static readonly TimeSpan IntervaloEntreFotos = TimeSpan.FromSeconds(10);
    static readonly TimeSpan EsperaPeloFim = TimeSpan.FromMinutes(1);

    sealed record Foto(DateTime Hora, int Em, IReadOnlyList<string> Quadros);
    sealed record Fim(int Duracao, string Tela, int Abas, int Trabalhando);

    readonly Responsividade _medida = new();
    readonly Timer _relogio;
    readonly Action _responder;
    readonly Func<(string Tela, int Abas, int Trabalhando)> _contexto;
    readonly Lock _trava = new();
    readonly Dictionary<long, Foto> _fotos = [];
    readonly Dictionary<long, Fim> _fins = [];
    long _postadoEm;
    long _fotografado;
    long _ultimaFoto;
    int _fotografando;
    int _threadDaInterface;

    public Batimento(TimeSpan intervalo, Func<(string Tela, int Abas, int Trabalhando)> contexto)
    {
        _responder = Responder;
        _contexto = contexto;
        _relogio = new Timer(_ => Bater(), null, intervalo, intervalo);
    }

    public void Dispose() => _relogio.Dispose();

    void Bater()
    {
        var postado = Interlocked.Read(ref _postadoEm);
        if (postado != 0)
        {
            Fotografar(postado);
            return;
        }
        if (Interlocked.CompareExchange(ref _postadoEm, Stopwatch.GetTimestamp(), 0) != 0) return;
        Dispatcher.UIThread.Post(_responder, DispatcherPriority.Input);
    }

    void Fotografar(long postado)
    {
        var atraso = Stopwatch.GetElapsedTime(postado);
        var thread = Volatile.Read(ref _threadDaInterface);
        if (atraso.TotalMilliseconds < LimiteDaFoto || thread == 0 || !FotoDaThread.Disponivel || Interlocked.Read(ref _fotografado) == postado) return;
        var ultima = Interlocked.Read(ref _ultimaFoto);
        if (ultima != 0 && Stopwatch.GetElapsedTime(ultima) < IntervaloEntreFotos) return;
        if (Interlocked.Exchange(ref _fotografando, 1) == 1) return;
        try
        {
            Interlocked.Exchange(ref _fotografado, postado);
            Interlocked.Exchange(ref _ultimaFoto, Stopwatch.GetTimestamp());
            var hora = DateTime.Now - atraso;
            if (FotoDaThread.Tirar(thread) is { Count: > 0 } quadros)
                lock (_trava) _fotos[postado] = new Foto(hora, (int)atraso.TotalMilliseconds, quadros);
        }
        finally
        {
            Volatile.Write(ref _fotografando, 0);
        }
    }

    void Responder()
    {
        Volatile.Write(ref _threadDaInterface, Environment.CurrentManagedThreadId);
        var postado = Interlocked.Exchange(ref _postadoEm, 0);
        if (postado == 0) return;
        var atraso = (int)Stopwatch.GetElapsedTime(postado).TotalMilliseconds;
        _medida.Registrar(atraso);
        if (atraso < LimiteDaFoto) return;
        var (tela, abas, trabalhando) = _contexto();
        lock (_trava) _fins[postado] = new Fim(atraso, tela, abas, trabalhando);
    }

    public (int Maior, int Travadas, IReadOnlyList<Travada> Fotos) Colher()
    {
        var postado = Interlocked.Read(ref _postadoEm);
        var (maior, travadas) = _medida.Colher(postado == 0 ? 0 : (int)Stopwatch.GetElapsedTime(postado).TotalMilliseconds);
        var prontas = new List<Travada>();
        var agora = DateTime.Now;
        lock (_trava)
        {
            foreach (var (chave, foto) in _fotos.ToList())
            {
                var fim = _fins.GetValueOrDefault(chave);
                if (fim is null && chave == postado && agora - foto.Hora < EsperaPeloFim) continue;
                _fotos.Remove(chave);
                _fins.Remove(chave);
                prontas.Add(new Travada
                {
                    Hora = foto.Hora.AddTicks(-(foto.Hora.Ticks % TimeSpan.TicksPerMillisecond)),
                    Duracao = fim?.Duracao ?? (int)(agora - foto.Hora).TotalMilliseconds,
                    FotoEm = foto.Em,
                    Causa = Pilha.Causa(foto.Quadros) ?? "",
                    Ponta = Pilha.Ponta(foto.Quadros) ?? "",
                    Tela = fim?.Tela ?? "",
                    Abas = fim?.Abas ?? 0,
                    Trabalhando = fim?.Trabalhando ?? 0,
                    Pilha = foto.Quadros,
                });
            }
            var emFoto = Interlocked.Read(ref _fotografado);
            foreach (var chave in _fins.Keys.Where(k => k != emFoto && !_fotos.ContainsKey(k)).ToList()) _fins.Remove(chave);
        }
        return (maior, travadas, prontas);
    }
}

public sealed class MonitorDeDesempenho
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(30);
    static readonly TimeSpan IntervaloDoBatimento = TimeSpan.FromMilliseconds(100);
    const int TravadasGuardadasPorDia = 500;

    public static MonitorDeDesempenho? Instancia { get; private set; }

    public static string Pasta => Path.Combine(global::Nexora.Core.DadosDoNexora.Pasta, "desempenho");

    public static string? PastaDoTeste { get; set; }

    readonly JanelaVM _janela;
    readonly Gravacao? _gravacao;
    readonly Medidor _medidor = new();
    readonly Batimento _batimento;
    readonly DispatcherTimer _relogio;
    readonly Lock _trava = new();
    readonly List<Amostra> _hoje = [];
    readonly List<Travada> _travadasDeHoje = [];
    readonly Dictionary<DateOnly, ResumoDoDia?> _dias = [];
    DateOnly _dia = DateOnly.FromDateTime(DateTime.Now);
    int _medindo;
    int _abasAntes = -1;
    int _gen2Antes;
    TimeSpan _pausaAntes;
    long _abasMudaramEm = Stopwatch.GetTimestamp();
    readonly long _abriuEm = Stopwatch.GetTimestamp();
    bool _enviando;
    DateTime _proximaTentativa;

    MonitorDeDesempenho(JanelaVM janela)
    {
        _janela = janela;
        _batimento = new Batimento(IntervaloDoBatimento, Contexto);
        _gravacao = PastaDoTeste is { } teste ? new Gravacao(teste) : JanelaVM.Isolada ? null : new Gravacao(Pasta);
        _relogio = new DispatcherTimer(Intervalo, DispatcherPriority.Background, (_, _) => Amostrar());
        _relogio.Start();
        _ = Task.Run(Preparar);
    }

    (string Tela, int Abas, int Trabalhando) Contexto()
    {
        var tela = _janela.Conteudo?.GetType().Name ?? "";
        if (tela.EndsWith("VM", StringComparison.Ordinal)) tela = tela[..^2];
        return (tela, _janela.Abertas.Count, _janela.TotalTrabalhando);
    }

    public DateTime Desde { get; } = DateTime.Now;
    public Amostra? Ultima { get; private set; }
    public bool Grava => _gravacao is not null;
    public event Action? Mediu;

    public static void Iniciar(JanelaVM janela)
    {
        if (JanelaVM.Demo || Instancia is not null) return;
        Instancia = new MonitorDeDesempenho(janela);
    }

    void Preparar()
    {
        try
        {
            if (_gravacao is not null)
            {
                _gravacao.Arrumar(_dia);
                var lidas = _gravacao.LerAmostras(_dia);
                var travadas = _gravacao.LerTravadas(_dia);
                lock (_trava)
                {
                    _hoje.InsertRange(0, lidas);
                    _travadasDeHoje.InsertRange(0, travadas.TakeLast(TravadasGuardadasPorDia));
                }
            }
            lock (_medidor) _medidor.Medir(SessaoClaude.PidsDoNexora, PidsDosMcps());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
        }
    }

    static IReadOnlyList<int> PidsDosMcps()
    {
        try
        {
            return App.Hub?.Pids() ?? [];
        }
        catch (ObjectDisposedException)
        {
            return [];
        }
    }

    void Amostrar()
    {
        if (Interlocked.Exchange(ref _medindo, 1) == 1) return;
        var abas = _janela.Abertas.Count;
        var trabalhando = _janela.TotalTrabalhando;
        _ = Task.Run(() => Medir(abas, trabalhando));
    }

    void Medir(int abas, int trabalhando)
    {
        try
        {
            var (amostra, travadas) = Amostrar(abas, trabalhando);
            Registrar(amostra);
            foreach (var travada in travadas) Registrar(travada);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
        }
        finally
        {
            Volatile.Write(ref _medindo, 0);
        }
        Dispatcher.UIThread.Post(Avisar, DispatcherPriority.Background);
    }

    (Amostra, IReadOnlyList<Travada>) Amostrar(int abas, int trabalhando)
    {
        var inicio = Stopwatch.GetTimestamp();
        MedidaDeProcessos medida;
        lock (_medidor) medida = _medidor.Medir(SessaoClaude.PidsDoNexora, PidsDosMcps());
        var (atraso, travadas, fotos) = _batimento.Colher();
        if (abas != _abasAntes)
        {
            if (_abasAntes >= 0 && abas > _abasAntes) _abasMudaramEm = inicio;
            _abasAntes = abas;
        }
        var gen2 = GC.CollectionCount(2);
        var pausa = GC.GetTotalPauseDuration();
        var gen2Novas = gen2 - _gen2Antes;
        var pausaNova = pausa - _pausaAntes;
        _gen2Antes = gen2;
        _pausaAntes = pausa;
        var agora = DateTime.Now;
        return (new Amostra
        {
            Hora = agora.AddTicks(-(agora.Ticks % TimeSpan.TicksPerSecond)),
            NexoraRam = medida.NexoraRam,
            NexoraWs = medida.NexoraWs,
            NexoraPrivada = medida.NexoraPrivada,
            Heap = GC.GetTotalMemory(false),
            Handles = medida.Handles,
            CpuNexora = Math.Round(medida.CpuNexora, 2),
            Sessoes = Arredondar(medida.Sessoes),
            Mcps = Arredondar(medida.Mcps),
            Builds = Arredondar(medida.Builds),
            Outros = Arredondar(medida.Outros),
            AtrasoMaximo = atraso,
            Travadas = travadas,
            Abas = abas,
            Trabalhando = trabalhando,
            Custo = Math.Round(Stopwatch.GetElapsedTime(inicio).TotalMilliseconds, 2),
            Gen2 = gen2Novas,
            PausaGc = Math.Round(pausaNova.TotalMilliseconds, 1),
            DesdeAbrir = (int)Stopwatch.GetElapsedTime(_abriuEm, inicio).TotalSeconds,
            AbaNovaHa = (int)Stopwatch.GetElapsedTime(_abasMudaramEm, inicio).TotalSeconds,
        }, fotos);
    }

    void Registrar(Travada travada)
    {
        lock (_trava)
        {
            if (DateOnly.FromDateTime(travada.Hora) != _dia) return;
            _travadasDeHoje.Add(travada);
            if (_travadasDeHoje.Count > TravadasGuardadasPorDia) _travadasDeHoje.RemoveAt(0);
        }
        _gravacao?.AcrescentarTravada(travada);
    }

    static Consumo Arredondar(Consumo consumo) => consumo with { Cpu = Math.Round(consumo.Cpu, 2) };

    void Registrar(Amostra amostra)
    {
        var dia = DateOnly.FromDateTime(amostra.Hora);
        if (dia != _dia)
        {
            DateOnly ontem;
            Amostra[] deOntem;
            Travada[] travadasDeOntem;
            lock (_trava)
            {
                ontem = _dia;
                deOntem = [.. _hoje];
                travadasDeOntem = [.. _travadasDeHoje];
                _hoje.Clear();
                _travadasDeHoje.Clear();
                _dias.Clear();
                _dia = dia;
            }
            var resumo = _gravacao?.Fechar(ontem) ?? Agregacao.Resumir(ontem, deOntem, travadasDeOntem);
            _gravacao?.Limpar(dia);
            lock (_trava) _dias[ontem] = resumo.Amostras > 0 ? resumo : null;
        }
        lock (_trava) _hoje.Add(amostra);
        Ultima = amostra;
        _gravacao?.Acrescentar(amostra);
    }

    void Avisar()
    {
        Mediu?.Invoke();
        VerificarRelatorio();
    }

    public Task<DadosDeDesempenho> DadosAsync(int dias) => Task.Run(() =>
    {
        Amostra[] hoje;
        Travada[] travadas;
        DateOnly dia;
        lock (_trava)
        {
            hoje = [.. _hoje];
            travadas = [.. _travadasDeHoje];
            dia = _dia;
        }
        var anteriores = new List<ResumoDoDia>();
        for (var d = dia.AddDays(-(dias - 1)); d < dia; d = d.AddDays(1))
        {
            ResumoDoDia? resumo;
            bool guardado;
            lock (_trava) guardado = _dias.TryGetValue(d, out resumo);
            if (!guardado)
            {
                resumo = _gravacao?.LerDia(d);
                lock (_trava) _dias[d] = resumo;
            }
            if (resumo is not null) anteriores.Add(resumo);
        }
        return new DadosDeDesempenho(Ultima, Agregacao.Resumir(dia, hoje, travadas), anteriores, Desde, Grava);
    });

    public static InfoDoComputador Computador() => new(
        Atualizacoes.Atual.ToString(3),
        OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "mac" : "linux",
        Environment.ProcessorCount,
        GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);

    public static IReadOnlyList<ResumoDoDia> DiasDoResumo(DadosDeDesempenho dados, bool semanaFechada)
    {
        var hoje = dados.Hoje.Data;
        var anteriores = dados.Anteriores.Where(d => d.Data >= hoje.AddDays(semanaFechada ? -7 : -6)).ToList();
        if (!semanaFechada || anteriores.Count == 0) anteriores.Add(dados.Hoje);
        return [.. anteriores.Where(d => d.Amostras > 0)];
    }

    public static bool PodeEnviar => Relatorio.PodeEnviar(App.Config.RelatorioPorEmail, JanelaVM.Demo, JanelaVM.Isolada);

    public async Task<(bool Ok, string Mensagem)> EnviarAsync(bool semanaFechada)
    {
        if (!PodeEnviar) return (false, Idioma.T("O envio do resumo está desligado nesta janela."));
        var dias = DiasDoResumo(await DadosAsync(8), semanaFechada);
        if (dias.Count == 0) return (false, Idioma.T("Ainda não há medições para enviar."));
        return await EnvioDoResumo.EnviarAsync(Relatorio.Montar(dias, Computador(), DateTime.Now));
    }

    void VerificarRelatorio()
    {
        if (!PodeEnviar || _enviando || DateTime.Now < _proximaTentativa) return;
        if (!Relatorio.Vencido(DateTime.Now, Relatorio.DiaDe(App.Config.RelatorioDia), Relatorio.HoraDe(App.Config.RelatorioHora), App.Config.RelatorioEnviadoEm)) return;
        _ = EnviarDaSemanaAsync();
    }

    async Task EnviarDaSemanaAsync()
    {
        _enviando = true;
        try
        {
            var (ok, _) = await EnviarAsync(true);
            if (ok)
            {
                App.Config.RelatorioEnviadoEm = DateTime.Now;
                App.Config.Salvar();
            }
            else _proximaTentativa = DateTime.Now.AddHours(1);
        }
        finally
        {
            _enviando = false;
        }
    }
}

static class EnvioDoResumo
{
    public static async Task<(bool Ok, string Mensagem)> EnviarAsync(JsonObject corpo, CancellationToken cancelar = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"Nexora/{Atualizacoes.Atual.ToString(3)}");
            using var conteudo = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json");
            using var resposta = await http.PostAsync(Relatorio.Endereco, conteudo, cancelar);
            if (resposta.IsSuccessStatusCode) return (true, Idioma.F("Resumo enviado às {0:HH:mm}.", DateTime.Now));
            return (false, (int)resposta.StatusCode switch
            {
                429 => Idioma.T("Muitos envios seguidos. Tente de novo mais tarde."),
                503 => Idioma.T("O envio por e-mail ainda não está configurado no site."),
                var codigo => Idioma.F("O site recusou o resumo (erro {0}).", codigo),
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (false, Idioma.T("Não deu para falar com o site agora."));
        }
    }
}
