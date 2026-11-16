using System.Text.Json;

namespace Nexora.Core.Claude;

public sealed record PadroesDoClaude(string? Modelo, string? Esforco, string? Modo)
{
    public static PadroesDoClaude Ler(string? pasta, bool isolada, string? pastaDoUsuario = null)
    {
        var arquivos = new List<string>();
        if (!isolada)
            arquivos.Add(Path.Combine(pastaDoUsuario ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json"));
        if (!string.IsNullOrWhiteSpace(pasta))
        {
            arquivos.Add(Path.Combine(pasta, ".claude", "settings.json"));
            arquivos.Add(Path.Combine(pasta, ".claude", "settings.local.json"));
        }
        string? modelo = null, esforco = null, modo = null;
        foreach (var arquivo in arquivos)
        {
            try
            {
                if (!File.Exists(arquivo)) continue;
                using var doc = JsonDocument.Parse(File.ReadAllText(arquivo), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                var raiz = doc.RootElement;
                modelo = Protocolo.Texto(raiz, "model") is { Length: > 0 } m ? m : modelo;
                esforco = Protocolo.Texto(raiz, "effortLevel") is { Length: > 0 } e ? e : esforco;
                modo = Protocolo.Texto(Protocolo.Objeto(raiz, "permissions"), "defaultMode") is { Length: > 0 } d ? d : modo;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
            }
        }
        if (Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") is { Length: > 0 } doAmbiente) modelo = doAmbiente;
        return new PadroesDoClaude(modelo, esforco, modo);
    }
}
