using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Git;
using Nexora.Core.Historico;
using Nexora.Core.Uso;

namespace Nexora.Core.Servidor;

public sealed class NucleoRemoto : INucleo, IDisposable
{
    static readonly TimeSpan EsperaPelaResposta = TimeSpan.FromSeconds(60);

    readonly NamedPipeClientStream _cano;
    readonly StreamWriter _escritor;
    readonly SemaphoreSlim _trava = new(1, 1);
    readonly ConcurrentDictionary<long, TaskCompletionSource<JsonNode?>> _pendentes = new();
    readonly CancellationTokenSource _fim = new();
    readonly ConcurrentDictionary<string, Action<long, Claude.EventoClaude>> _ouvintes = new();
    long _proximo;
    volatile bool _caiu;

    NucleoRemoto(NamedPipeClientStream cano)
    {
        _cano = cano;
        _escritor = new StreamWriter(cano, new UTF8Encoding(false));
        _ = LerAsync();
    }

    public bool Conectado => !_caiu;

    public event Action? Caiu;

    public static async Task<NucleoRemoto?> ConectarAsync(string nome, TimeSpan espera, CancellationToken cancelar = default)
    {
        var cano = new NamedPipeClientStream(".", nome, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await cano.ConnectAsync(espera, cancelar).ConfigureAwait(false);
            return new NucleoRemoto(cano);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            await cano.DisposeAsync().ConfigureAwait(false);
            return null;
        }
    }

    public async Task<IReadOnlyList<ConversaSalva>> ListarConversasAsync(CancellationToken cancelar = default) =>
        await PedirOuLocal<List<ConversaSalva>>(MetodosDoNucleo.Conversas, null, cancelar).ConfigureAwait(false)
        ?? await NucleoLocal.Instancia.ListarConversasAsync(cancelar).ConfigureAwait(false);

    public async Task<IReadOnlyList<Parcela>> LerUsoAsync(string? cache, DateTime desde, bool porDia, CancellationToken cancelar = default) =>
        await PedirOuLocal<List<Parcela>>(MetodosDoNucleo.Uso, new JsonObject { ["cache"] = cache, ["desde"] = desde, ["porDia"] = porDia }, cancelar).ConfigureAwait(false)
        ?? await NucleoLocal.Instancia.LerUsoAsync(cache, desde, porDia, cancelar).ConfigureAwait(false);

    public async Task<Andamento> LerAndamentoAsync(string arquivo, string? pasta, CancellationToken cancelar = default) =>
        await PedirOuLocal<Andamento>(MetodosDoNucleo.Andamento, new JsonObject { ["arquivo"] = arquivo, ["pasta"] = pasta }, cancelar).ConfigureAwait(false)
        ?? await NucleoLocal.Instancia.LerAndamentoAsync(arquivo, pasta, cancelar).ConfigureAwait(false);

    public async Task<ResultadoGit> RodarComandoAsync(PedidoDeComando pedido, CancellationToken cancelar = default)
    {
        var dados = JsonSerializer.SerializeToNode(pedido, ServidorDoNucleo.Json)!.AsObject();
        var espera = (pedido.Limite ?? TimeSpan.FromSeconds(30)) + TimeSpan.FromSeconds(30);
        var (enviado, resultado) = await Pedir<ResultadoGit>(MetodosDoNucleo.Comando, dados, espera, cancelar).ConfigureAwait(false);
        if (resultado is not null) return resultado;
        if (!enviado) return await NucleoLocal.Instancia.RodarComandoAsync(pedido, cancelar).ConfigureAwait(false);
        return new ResultadoGit(-1, "", Texto.Idioma.F("O servidor do Nexora parou no meio do {0}. Confira se o comando chegou a rodar antes de tentar de novo.", pedido.Programa));
    }

    public async Task<TrechoBruto> LerTurnosAsync(string arquivo, int turnos, long ate, CancellationToken cancelar = default) =>
        await PedirOuLocal<TrechoBruto>(MetodosDoNucleo.Turnos, new JsonObject { ["arquivo"] = arquivo, ["turnos"] = turnos, ["ate"] = ate }, cancelar).ConfigureAwait(false)
        ?? await NucleoLocal.Instancia.LerTurnosAsync(arquivo, turnos, ate, cancelar).ConfigureAwait(false);

    public async Task<string?> UltimoModoAsync(string arquivo, CancellationToken cancelar = default) =>
        await PedirOuLocal<RespostaDeTexto>(MetodosDoNucleo.Modo, new JsonObject { ["arquivo"] = arquivo }, cancelar).ConfigureAwait(false) is { } resposta
            ? resposta.Texto
            : await NucleoLocal.Instancia.UltimoModoAsync(arquivo, cancelar).ConfigureAwait(false);

    public void Ouvir(string sessao, Action<long, Claude.EventoClaude> aoEvento) => _ouvintes[sessao] = aoEvento;

    public void Esquecer(string sessao) => _ouvintes.TryRemove(sessao, out _);

    public async Task<T?> ChamarAsync<T>(string metodo, JsonObject? dados, TimeSpan? espera = null, CancellationToken cancelar = default) where T : class =>
        (await Pedir<T>(metodo, dados, espera ?? EsperaPelaResposta, cancelar).ConfigureAwait(false)).Resultado;

    public async Task<IReadOnlyList<ResumoDaSessao>> ListarSessoesAsync(CancellationToken cancelar = default) =>
        await ChamarAsync<List<ResumoDaSessao>>(MetodosDoNucleo.ListarSessoes, null, null, cancelar).ConfigureAwait(false) ?? [];

    async Task<T?> PedirOuLocal<T>(string metodo, JsonObject? dados, CancellationToken cancelar) where T : class =>
        (await Pedir<T>(metodo, dados, EsperaPelaResposta, cancelar).ConfigureAwait(false)).Resultado;

    async Task<(bool Enviado, T? Resultado)> Pedir<T>(string metodo, JsonObject? dados, TimeSpan espera, CancellationToken cancelar) where T : class
    {
        if (_caiu) return (false, null);
        var enviado = false;
        var id = Interlocked.Increment(ref _proximo);
        var resposta = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendentes[id] = resposta;
        try
        {
            await _trava.WaitAsync(cancelar).ConfigureAwait(false);
            try
            {
                var pedido = new JsonObject { ["id"] = id, ["metodo"] = metodo, ["dados"] = dados };
                await _escritor.WriteLineAsync(pedido.ToJsonString().AsMemory(), cancelar).ConfigureAwait(false);
                await _escritor.FlushAsync(cancelar).ConfigureAwait(false);
                enviado = true;
            }
            finally
            {
                _trava.Release();
            }
            var no = await resposta.Task.WaitAsync(espera, cancelar).ConfigureAwait(false);
            return (true, no?.Deserialize<T>(ServidorDoNucleo.Json));
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or TimeoutException or InvalidOperationException)
        {
            Derrubar();
            return (enviado, null);
        }
        catch (Exception ex) when (ex is JsonException or ErroNoServidor)
        {
            return (enviado, null);
        }
        finally
        {
            _pendentes.TryRemove(id, out _);
        }
    }

    async Task LerAsync()
    {
        try
        {
            var leitor = new StreamReader(_cano, Encoding.UTF8);
            while (await leitor.ReadLineAsync(_fim.Token).ConfigureAwait(false) is { } linha)
            {
                var resposta = JsonNode.Parse(linha)?.AsObject();
                if (resposta?["sessao"] is { } sessao && resposta["evento"] is { } evento)
                {
                    if (_ouvintes.TryGetValue(sessao.GetValue<string>(), out var ouvinte) && evento.Deserialize<Claude.EventoClaude>(ServidorDoNucleo.Json) is { } lido)
                        ouvinte(resposta["numero"]?.GetValue<long>() ?? 0, lido);
                    continue;
                }
                if (resposta?["id"]?.GetValue<long>() is not { } id || !_pendentes.TryGetValue(id, out var pendente)) continue;
                if (resposta["erro"] is { } erro) pendente.TrySetException(new ErroNoServidor(erro.GetValue<string>()));
                else pendente.TrySetResult(resposta["dados"]?.DeepClone());
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException or JsonException)
        {
        }
        Derrubar();
    }

    void Derrubar()
    {
        if (_caiu) return;
        _caiu = true;
        foreach (var pendente in _pendentes.Values) pendente.TrySetException(new IOException("O servidor do Nexora parou."));
        Caiu?.Invoke();
    }

    sealed class ErroNoServidor(string mensagem) : Exception(mensagem);

    public void Dispose()
    {
        _caiu = true;
        _fim.Cancel();
        _cano.Dispose();
    }
}
