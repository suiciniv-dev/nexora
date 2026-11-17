using System.Collections.Concurrent;
using Nexora.Core.Claude;

namespace Nexora.Core.Servidor;

public sealed class SessoesDoServidor(Func<string, OpcoesSessao, ISessaoAgente> criar)
{
    readonly ConcurrentDictionary<string, SessaoNoServidor> _sessoes = new();

    public TimeSpan EsperaSolta { get; init; } = TimeSpan.FromMinutes(3);

    public TimeSpan GuardarEncerrada { get; init; } = TimeSpan.FromMinutes(10);

    public SessaoNoServidor Abrir(string id, string agente, string? titulo, OpcoesSessao opcoes)
    {
        if (_sessoes.TryGetValue(id, out var existente) && !existente.Encerrada) return existente;
        var sessao = new SessaoNoServidor(id, agente, opcoes.Pasta, titulo, criar(agente, opcoes));
        _sessoes[id] = sessao;
        sessao.Sessao.Iniciar();
        return sessao;
    }

    public SessaoNoServidor? Achar(string id) => _sessoes.GetValueOrDefault(id);

    public IReadOnlyList<ResumoDaSessao> Listar() => [.. _sessoes.Values.Select(s => s.Resumo()).OrderBy(r => r.Id, StringComparer.Ordinal)];

    public bool Ocupado => _sessoes.Values.Any(s => !s.Encerrada);

    public void Esquecer(string id) => _sessoes.TryRemove(id, out _);

    public void Arrumar()
    {
        foreach (var sessao in _sessoes.Values)
        {
            var solta = sessao.Solta;
            if (sessao.Encerrada)
            {
                if (solta >= GuardarEncerrada) _sessoes.TryRemove(sessao.Id, out _);
                continue;
            }
            if (solta >= EsperaSolta) sessao.Sessao.Encerrar();
        }
    }

    public async Task EncerrarTodasAsync()
    {
        foreach (var sessao in _sessoes.Values)
            try
            {
                await sessao.Sessao.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException)
            {
            }
    }
}
