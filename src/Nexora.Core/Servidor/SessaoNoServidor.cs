using System.Text.Json.Nodes;
using Nexora.Core.Claude;

namespace Nexora.Core.Servidor;

public sealed class SessaoNoServidor
{
    readonly object _trava = new();
    readonly RegistroDeEventos _registro;
    readonly List<Action<EventoNumerado>> _assinantes = [];
    readonly Dictionary<string, EventoClaude> _pendentes = [];
    bool _trabalhando;
    long _soltaDesde;

    public SessaoNoServidor(string id, string agente, string pasta, string? titulo, ISessaoAgente sessao, int limite = 5000)
    {
        Id = id;
        Agente = agente;
        Pasta = pasta;
        Titulo = titulo;
        Sessao = sessao;
        _registro = new RegistroDeEventos(limite);
        _soltaDesde = Environment.TickCount64;
        sessao.Evento += AoEvento;
        sessao.Encerrou += (codigo, erros) => AoEvento(new SessaoEncerrada(codigo, erros));
    }

    public string Id { get; }
    public string Agente { get; }
    public string Pasta { get; }
    public string? Titulo { get; set; }
    public string? Dono { get; init; }
    public ISessaoAgente Sessao { get; }
    public bool Encerrada { get; private set; }

    public bool Trabalhando
    {
        get
        {
            lock (_trava) return _trabalhando || _pendentes.Count > 0;
        }
    }

    public int Assinantes
    {
        get
        {
            lock (_trava) return _assinantes.Count;
        }
    }

    public TimeSpan Solta
    {
        get
        {
            lock (_trava) return _assinantes.Count > 0 || _trabalhando || _pendentes.Count > 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(Environment.TickCount64 - _soltaDesde);
        }
    }

    public ResumoDaSessao Resumo()
    {
        lock (_trava)
            return new ResumoDaSessao(Id, Agente, Pasta, Titulo, Sessao.SessaoId, !Encerrada && Sessao.Ativa, _trabalhando || _pendentes.Count > 0, Sessao.Pid, _registro.Ultimo, _assinantes.Count, Dono);
    }

    public IReadOnlyList<EventoClaude> Pendentes()
    {
        lock (_trava) return [.. _pendentes.Values];
    }

    public (bool Completo, long Ultimo, IDisposable Assinatura) Assinar(long? desde, Action<EventoNumerado> aoEvento)
    {
        lock (_trava)
        {
            var (completo, eventos) = _registro.Desde(desde ?? _registro.Ultimo);
            foreach (var evento in eventos) aoEvento(evento);
            _assinantes.Add(aoEvento);
            return (completo, _registro.Ultimo, new Assinatura(this, aoEvento));
        }
    }

    void Soltar(Action<EventoNumerado> aoEvento)
    {
        lock (_trava)
        {
            if (_assinantes.Remove(aoEvento) && _assinantes.Count == 0) _soltaDesde = Environment.TickCount64;
        }
    }

    public void Enviar(string texto, IReadOnlyList<Anexo>? imagens)
    {
        lock (_trava) _trabalhando = true;
        Sessao.Enviar(texto, imagens);
    }

    public void Respondido(string pedidoId)
    {
        lock (_trava)
        {
            _pendentes.Remove(pedidoId);
            _soltaDesde = Environment.TickCount64;
        }
    }

    public void Aplicar(string acao, JsonObject dados)
    {
        string T(string chave) => (string?)dados[chave] ?? "";
        switch (acao)
        {
            case AcoesDaSessao.Permitir:
                Respondido(T("pedido"));
                Sessao.Permitir(T("pedido"), dados["entrada"]?.DeepClone(), dados["regras"]?.DeepClone());
                break;
            case AcoesDaSessao.Negar:
                Respondido(T("pedido"));
                Sessao.Negar(T("pedido"), T("motivo"));
                break;
            case AcoesDaSessao.LiberarSubagente:
                Respondido(T("pedido"));
                Sessao.LiberarSubagente(T("pedido"), dados["entrada"]?.DeepClone());
                break;
            case AcoesDaSessao.BarrarSubagente:
                Respondido(T("pedido"));
                Sessao.BarrarSubagente(T("pedido"), T("motivo"));
                break;
            case AcoesDaSessao.Interromper:
                Sessao.Interromper();
                break;
            case AcoesDaSessao.Modo:
                Sessao.MudarModo(T("modo"));
                break;
            case AcoesDaSessao.Modelo:
                Sessao.MudarModelo((string?)dados["modelo"]);
                break;
            case AcoesDaSessao.Configuracoes:
                Sessao.AplicarConfiguracoes(dados["configuracoes"] as JsonObject ?? []);
                break;
            case AcoesDaSessao.Encerrar:
                Sessao.Encerrar();
                break;
            default:
                throw new InvalidOperationException($"Ação desconhecida: {acao}");
        }
    }

    void AoEvento(EventoClaude evento)
    {
        lock (_trava)
        {
            switch (evento)
            {
                case PermissaoPedida pedido:
                    _pendentes[pedido.PedidoId] = evento;
                    break;
                case SubagentePedido pedido:
                    _pendentes[pedido.PedidoId] = evento;
                    break;
                case TurnoEncerrado:
                    _trabalhando = false;
                    _pendentes.Clear();
                    _soltaDesde = Environment.TickCount64;
                    break;
                case SessaoEncerrada:
                    Encerrada = true;
                    _trabalhando = false;
                    _pendentes.Clear();
                    _soltaDesde = Environment.TickCount64;
                    break;
            }
            var numerado = _registro.Acrescentar(evento);
            foreach (var assinante in _assinantes) assinante(numerado);
        }
    }

    sealed class Assinatura(SessaoNoServidor dona, Action<EventoNumerado> aoEvento) : IDisposable
    {
        int _solta;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _solta, 1) == 0) dona.Soltar(aoEvento);
        }
    }
}
