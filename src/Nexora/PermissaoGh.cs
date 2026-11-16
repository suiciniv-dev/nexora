using System.Diagnostics;
using System.Text.RegularExpressions;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora;

public static partial class PermissaoGh
{
    [GeneratedRegex("^[A-Za-z0-9-]+$")]
    private static partial Regex ContaValida();

    public static async Task<bool> PedirAsync(IEnumerable<string> contasPedidas)
    {
        Instalacao.AtualizarPath();
        if (Instalacao.NoPath("gh.exe") is not { } gh) return false;
        var contas = await GitHub.ContasGhAsync() ?? [];
        var ativa = contas.FirstOrDefault(c => c.Ativa)?.Conta;
        var alvo = contasPedidas.Concat(contas.Where(c => !c.VeQuadros).Select(c => c.Conta))
            .Where(c => ContaValida().IsMatch(c) && contas.Any(x => x.Conta.Equals(c, StringComparison.OrdinalIgnoreCase) && !x.VeQuadros))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (alvo.Count == 0 || ativa is null || !ContaValida().IsMatch(ativa)) return false;
        if (Sistema.Mac) return await PedirNoMacAsync(gh, alvo, ativa);
        var partes = new List<string>();
        foreach (var conta in alvo)
        {
            if (!conta.Equals(ativa, StringComparison.OrdinalIgnoreCase)) partes.Add($"\"{gh}\" auth switch --hostname github.com --user {conta}");
            partes.Add("echo. & echo " + Idioma.F("Permissao de projetos para a conta {0}:", conta));
            partes.Add($"\"{gh}\" auth refresh --hostname github.com --scopes project");
        }
        if (alvo.Any(c => !c.Equals(ativa, StringComparison.OrdinalIgnoreCase))) partes.Add($"\"{gh}\" auth switch --hostname github.com --user {ativa}");
        partes.Add("echo. & echo " + Idioma.T("Pronto. Pode fechar esta janela.") + " & pause >nul");
        try
        {
            using var processo = Sistema.Abrir(new ProcessStartInfo("cmd.exe", $"/s /c \"{string.Join(" & ", partes)}\"") { UseShellExecute = true });
            if (processo is not null) await processo.WaitForExitAsync();
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
        finally
        {
            if ((await GitHub.ContasGhAsync())?.FirstOrDefault(c => c.Ativa)?.Conta is { } agora && !agora.Equals(ativa, StringComparison.OrdinalIgnoreCase))
                await GitHub.TornarAtivaAsync(ativa);
            GitHub.EsquecerContas();
        }
    }

    static async Task<bool> PedirNoMacAsync(string gh, IReadOnlyList<string> alvo, string ativa)
    {
        var g = Sistema.Aspas(gh);
        var partes = new List<string>();
        foreach (var conta in alvo)
        {
            if (!conta.Equals(ativa, StringComparison.OrdinalIgnoreCase)) partes.Add($"{g} auth switch --hostname github.com --user {conta}");
            partes.Add("echo; echo '" + Idioma.F("Permissão de projetos para a conta {0}:", conta) + "'");
            partes.Add($"{g} auth refresh --hostname github.com --scopes project");
        }
        if (alvo.Any(c => !c.Equals(ativa, StringComparison.OrdinalIgnoreCase))) partes.Add($"{g} auth switch --hostname github.com --user {ativa}");
        partes.Add("echo; echo '" + Idioma.T("Pronto. Pode fechar esta janela.") + "'");
        using (var abrir = Sistema.AbrirTerminal(string.Join("; ", partes)))
            if (abrir is null) return false;
        try
        {
            var limite = DateTime.UtcNow.AddMinutes(10);
            while (DateTime.UtcNow < limite)
            {
                await Task.Delay(3000);
                var contas = await GitHub.ContasGhAsync() ?? [];
                var prontas = alvo.All(a => contas.Any(c => c.Conta.Equals(a, StringComparison.OrdinalIgnoreCase) && c.VeQuadros));
                if (prontas && contas.FirstOrDefault(c => c.Ativa)?.Conta is { } agora && agora.Equals(ativa, StringComparison.OrdinalIgnoreCase)) return true;
                if (prontas) await Task.Delay(3000);
            }
            return false;
        }
        finally
        {
            if ((await GitHub.ContasGhAsync())?.FirstOrDefault(c => c.Ativa)?.Conta is { } agora && !agora.Equals(ativa, StringComparison.OrdinalIgnoreCase))
                await GitHub.TornarAtivaAsync(ativa);
            GitHub.EsquecerContas();
        }
    }
}
