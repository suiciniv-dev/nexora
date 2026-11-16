using System.Collections.Concurrent;
using System.Text;

namespace Nexora.Core.Historico;

public static class VersoesDeConversa
{
    static readonly ConcurrentDictionary<(string Arquivo, long Tamanho, DateTime Escrita, string Texto), bool> Buscas = new();

    public static IReadOnlyDictionary<string, string> Antigas(IReadOnlyList<ConversaSalva> conversas)
    {
        var novaDe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var linhagens = conversas
            .Where(c => c.PrimeiraMensagem is not null && File.Exists(c.Arquivo))
            .GroupBy(c => c.PrimeiraMensagem!, StringComparer.Ordinal)
            .Where(g => g.Count() > 1);
        foreach (var linhagem in linhagens)
        {
            var ordem = linhagem.OrderByDescending(c => c.Atualizada).ToList();
            for (var i = 1; i < ordem.Count; i++)
            {
                if (ordem[i].UltimaMensagem is not { } ultima) continue;
                var nova = ordem.Take(i).FirstOrDefault(n => !novaDe.ContainsKey(n.SessaoId) && Contem(n.Arquivo, ultima));
                if (nova is not null) novaDe[ordem[i].SessaoId] = nova.SessaoId;
            }
        }
        return novaDe;
    }

    public static IReadOnlyDictionary<string, string> ComDesfeitos(IReadOnlyDictionary<string, string> antigas, IReadOnlyDictionary<string, string> desfeitos, IReadOnlySet<string> existentes)
    {
        var novaDe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (antiga, nova) in antigas) novaDe[antiga] = nova;
        foreach (var (original, copia) in desfeitos)
        {
            if (!existentes.Contains(copia) || original == copia) continue;
            if (novaDe.TryGetValue(copia, out var alvo) && string.Equals(alvo, original, StringComparison.OrdinalIgnoreCase)) novaDe.Remove(copia);
            novaDe[original] = copia;
        }
        return novaDe;
    }

    static bool Contem(string arquivo, string texto)
    {
        try
        {
            var info = new FileInfo(arquivo);
            return Buscas.GetOrAdd((arquivo, info.Length, info.LastWriteTimeUtc, texto), _ => Procurar(arquivo, Encoding.UTF8.GetBytes(texto)));
        }
        catch (IOException)
        {
            return false;
        }
    }

    static bool Procurar(string arquivo, byte[] alvo)
    {
        using var fluxo = new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buffer = new byte[256 * 1024 + alvo.Length];
        var guardado = 0;
        int lidos;
        while ((lidos = fluxo.Read(buffer, guardado, buffer.Length - guardado)) > 0)
        {
            var total = guardado + lidos;
            if (buffer.AsSpan(0, total).IndexOf(alvo) >= 0) return true;
            guardado = Math.Min(alvo.Length - 1, total);
            Array.Copy(buffer, total - guardado, buffer, 0, guardado);
        }
        return false;
    }
}
