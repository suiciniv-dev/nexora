using Microsoft.Diagnostics.Runtime;

namespace Nexora.Core.Desempenho;

public static class FotoDaThread
{
    public const int MaximoDeQuadros = 80;

    public static bool Disponivel => OperatingSystem.IsWindows();

    public static IReadOnlyList<string>? Tirar(int threadGerenciada)
    {
        if (!Disponivel) return null;
        try
        {
            using var alvo = DataTarget.CreateSnapshotAndAttach(Environment.ProcessId);
            if (alvo.ClrVersions.Length == 0) return null;
            using var runtime = alvo.ClrVersions[0].CreateRuntime();
            if (runtime.Threads.FirstOrDefault(t => t.ManagedThreadId == threadGerenciada) is not { } thread) return null;
            var quadros = new List<string>();
            foreach (var quadro in thread.EnumerateStackTrace(false, MaximoDeQuadros * 2))
            {
                var nome = quadro.Method is { } metodo ? Pilha.Limpar(metodo.Type?.Name ?? "?", metodo.Name ?? "?")
                    : quadro.FrameName is { Length: > 0 } especial ? $"[{especial}]"
                    : null;
                if (nome is null || (quadros.Count > 0 && quadros[^1] == nome)) continue;
                quadros.Add(nome);
                if (quadros.Count == MaximoDeQuadros) break;
            }
            return quadros;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
