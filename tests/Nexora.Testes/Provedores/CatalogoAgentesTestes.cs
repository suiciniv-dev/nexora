using System.Text.Json;
using Nexora.Core.Claude;
using Nexora.Core.Codex;
using Nexora.Core.Provedores;

namespace Nexora.Testes.Provedores;

public sealed class CatalogoAgentesTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-agentes-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    [Fact]
    public void Catalogo_tem_ids_unicos_e_nomes_vindos_dele()
    {
        Assert.Equal(CatalogoAgentes.Todos.Count, CatalogoAgentes.Todos.Select(a => a.Id).Distinct().Count());
        Assert.Equal("Gemini CLI", Agentes.Nome("gemini"));
        Assert.Equal("antigravity", Agentes.Normalizar("antigravity"));
        Assert.Equal(Agentes.Claude, Agentes.Normalizar("nao-existe"));
        Assert.Equal(["acp"], CatalogoAgentes.De("antigravity").Argumentos);
        Assert.False(CatalogoAgentes.De("gemini").VoltaConversa);
        Assert.True(CatalogoAgentes.De(Agentes.Codex).VoltaConversa);
    }

    [Fact]
    public void Acha_o_executavel_no_path_e_ignora_o_atalho_do_vscode()
    {
        var shim = Path.Combine(_pasta, "copilot-shim");
        var bin = Path.Combine(_pasta, "bin");
        Directory.CreateDirectory(shim);
        Directory.CreateDirectory(bin);
        var extensao = OperatingSystem.IsWindows() ? ".cmd" : "";
        File.WriteAllText(Path.Combine(shim, "copilot" + extensao), "");
        Assert.Null(CatalogoAgentes.Executavel(["copilot"], _pasta, shim));

        File.WriteAllText(Path.Combine(bin, "gemini" + extensao), "");
        Assert.Equal(Path.Combine(bin, "gemini" + extensao), CatalogoAgentes.Executavel(["gemini"], _pasta, string.Join(Path.PathSeparator, shim, bin)));
    }

    [Fact]
    public void Acha_o_agy_na_pasta_dele_mesmo_fora_do_path()
    {
        if (!OperatingSystem.IsWindows()) return;
        var agy = Path.Combine(_pasta, "AppData", "Local", "agy", "bin");
        Directory.CreateDirectory(agy);
        File.WriteAllText(Path.Combine(agy, "agy.exe"), "");
        Assert.Equal(Path.Combine(agy, "agy.exe"), CatalogoAgentes.Executavel(["agy"], _pasta, ""));
        File.Delete(Path.Combine(agy, "agy.exe"));
        var gemini = Path.Combine(_pasta, ".gemini", "bin");
        Directory.CreateDirectory(gemini);
        File.WriteAllText(Path.Combine(gemini, "agy.exe"), "");
        Assert.Equal(Path.Combine(gemini, "agy.exe"), CatalogoAgentes.Executavel(["agy"], _pasta, ""));
    }

    [Fact]
    public void Lista_de_modelos_do_codex_poe_o_padrao_primeiro_e_esconde_os_ocultos()
    {
        using var doc = JsonDocument.Parse("""
            {"data":[
              {"id":"a","model":"gpt-mini","displayName":"GPT Mini","description":"rápido","hidden":false,"isDefault":false,"defaultReasoningEffort":"low","supportedReasoningEfforts":[{"reasoningEffort":"low","description":""}]},
              {"id":"b","model":"gpt-grande","displayName":"GPT Grande","description":"forte","hidden":false,"isDefault":true,"defaultReasoningEffort":"medium","supportedReasoningEfforts":[{"reasoningEffort":"low","description":""},{"reasoningEffort":"xhigh","description":""}]},
              {"id":"c","model":"interno","displayName":"Interno","description":"","hidden":true,"isDefault":false,"defaultReasoningEffort":"low","supportedReasoningEfforts":[]}
            ],"nextCursor":"pag2"}
            """);
        var (modelos, proximo) = ProtocoloCodex.Modelos(doc.RootElement);

        Assert.Equal(["gpt-grande", "gpt-mini"], modelos.Select(m => m.Valor));
        Assert.Equal("GPT Grande", modelos[0].Nome);
        Assert.Equal(["low", "xhigh"], modelos[0].Esforcos);
        Assert.Equal("pag2", proximo);
    }
}
