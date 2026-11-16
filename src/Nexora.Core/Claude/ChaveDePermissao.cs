using System.Text.Json;
using Nexora.Core.Texto;

namespace Nexora.Core.Claude;

public static class ChaveDePermissao
{
    public static bool Liberavel(string ferramenta) => ferramenta is not ("ExitPlanMode" or "AskUserQuestion");

    public static string De(string ferramenta, JsonElement entrada)
    {
        string? Campo(string c) => Protocolo.Texto(entrada, c);
        var alvo = ferramenta switch
        {
            "Bash" or "PowerShell" => Campo("command")?.Trim(),
            "Write" or "Edit" or "MultiEdit" or "Read" => Campo("file_path"),
            "NotebookEdit" => Campo("notebook_path"),
            "WebFetch" => Uri.TryCreate(Campo("url"), UriKind.Absolute, out var u) ? u.Host : Campo("url"),
            _ => entrada.ValueKind == JsonValueKind.Object ? entrada.GetRawText() : null,
        };
        return $"{ferramenta}|{alvo}";
    }

    public static string Descrever(string ferramenta, JsonElement entrada)
    {
        string? Campo(string c) => Protocolo.Texto(entrada, c);
        return ferramenta switch
        {
            "Bash" or "PowerShell" => Idioma.F("o comando {0}", Resumos.UmaLinha(Campo("command") ?? "", 70)),
            "Write" or "Edit" or "MultiEdit" => Idioma.F("editar {0}", Path.GetFileName(Campo("file_path") ?? "")),
            "WebFetch" => Idioma.F("abrir {0}", Uri.TryCreate(Campo("url"), UriKind.Absolute, out var u) ? u.Host : Campo("url")),
            _ => ferramenta,
        };
    }
}
