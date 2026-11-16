using System.Collections.Concurrent;

namespace Nexora.Core.Claude;

public static class CatalogoComandos
{
    static readonly ConcurrentDictionary<string, Task<ComandosDisponiveis>> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Task<ComandosDisponiveis> ObterAsync(string pasta, bool isolada) =>
        Cache.GetOrAdd(pasta.TrimEnd('\\', '/'), p => SondarAsync(p, isolada));

    public static void Guardar(string pasta, ComandosDisponiveis comandos) => Cache[pasta.TrimEnd('\\', '/')] = Task.FromResult(comandos);

    public static IReadOnlyList<ComandoBarra> Filtrar(IReadOnlyList<ComandoBarra> comandos, string termo, int maximo = 60)
    {
        var busca = termo.TrimStart('/');
        if (busca.Length == 0) return comandos;
        return [.. comandos
            .Select(c => (Comando: c, Peso: c.Nome.StartsWith(busca, StringComparison.OrdinalIgnoreCase) ? 0
                : c.Nome.Contains(busca, StringComparison.OrdinalIgnoreCase) ? 1
                : c.Descricao?.Contains(busca, StringComparison.OrdinalIgnoreCase) == true ? 2 : 3))
            .Where(x => x.Peso < 3)
            .OrderBy(x => x.Peso)
            .ThenBy(x => x.Comando.Nome.Length)
            .ThenBy(x => x.Comando.Nome, StringComparer.OrdinalIgnoreCase)
            .Take(maximo)
            .Select(x => x.Comando)];
    }

    static async Task<ComandosDisponiveis> SondarAsync(string pasta, bool isolada)
    {
        var vazio = new ComandosDisponiveis([], RemotoDisponivel: true);
        if (!Directory.Exists(pasta)) return vazio;
        var pronto = new TaskCompletionSource<ComandosDisponiveis>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sessao = new SessaoClaude(new OpcoesSessao(pasta, Isolada: isolada));
        sessao.Evento += e =>
        {
            if (e is ComandosDisponiveis comandos) pronto.TrySetResult(comandos);
        };
        try
        {
            sessao.Iniciar();
            return await pronto.Task.WaitAsync(TimeSpan.FromSeconds(40));
        }
        catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or FileNotFoundException or System.ComponentModel.Win32Exception)
        {
            Cache.TryRemove(pasta.TrimEnd('\\', '/'), out _);
            return vazio;
        }
    }
}
