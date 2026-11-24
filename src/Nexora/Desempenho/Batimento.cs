using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using Nexora.Core.Claude;
using Nexora.Core.Desempenho;
using Nexora.ViewModels;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.Desempenho;

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
    readonly Func<bool> _ausente;
    readonly Lock _trava = new();
    readonly Dictionary<long, Foto> _fotos = [];
    readonly Dictionary<long, Fim> _fins = [];
    long _postadoEm;
    long _fotografado;
    long _ultimaFoto;
    int _fotografando;
    int _threadDaInterface;
    volatile bool _ausenteAoPostar;

    public Batimento(TimeSpan intervalo, Func<(string Tela, int Abas, int Trabalhando)> contexto, Func<bool>? ausente = null)
    {
        _responder = Responder;
        _contexto = contexto;
        _ausente = ausente ?? Presenca.Ausente;
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
        var ausente = _ausente();
        if (Interlocked.CompareExchange(ref _postadoEm, Stopwatch.GetTimestamp(), 0) != 0) return;
        _ausenteAoPostar = ausente;
        Dispatcher.UIThread.Post(_responder, DispatcherPriority.Input);
    }

    void Fotografar(long postado)
    {
        var atraso = Stopwatch.GetElapsedTime(postado);
        var thread = Volatile.Read(ref _threadDaInterface);
        if (atraso.TotalMilliseconds < LimiteDaFoto || thread == 0 || !FotoDaThread.Disponivel || Interlocked.Read(ref _fotografado) == postado) return;
        if (_ausenteAoPostar || _ausente()) return;
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
        if (atraso >= LimiteDaFoto && (_ausenteAoPostar || _ausente()))
        {
            _medida.Registrar(0);
            lock (_trava) _fotos.Remove(postado);
            return;
        }
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
