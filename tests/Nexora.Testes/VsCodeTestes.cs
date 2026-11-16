using Nexora.Core.Ambientes;
using Xunit.Abstractions;

namespace Nexora.Testes;

public sealed class VsCodeTestes(ITestOutputHelper saida)
{
    static string Uri(string caminho) => "file:///" + caminho.Replace('\\', '/').Replace(":", "%3A");

    [Fact]
    public void AchaWorkspacesEPastasComGit()
    {
        var raiz = Directory.CreateTempSubdirectory("nexora-vscode-").FullName;
        try
        {
            var repos = Path.Combine(raiz, "Repos");
            var loja = Directory.CreateDirectory(Path.Combine(repos, "Loja")).FullName;
            File.WriteAllText(Path.Combine(loja, "Loja.code-workspace"), """{ "folders": [{ "path": "." }] }""");
            var api = Directory.CreateDirectory(Path.Combine(repos, "Loja.Api")).FullName;
            Directory.CreateDirectory(Path.Combine(api, ".git"));
            var solta = Directory.CreateDirectory(Path.Combine(repos, "Solta")).FullName;
            var appData = Path.Combine(raiz, "AppData");
            var global = Directory.CreateDirectory(Path.Combine(appData, "Code", "User", "globalStorage")).FullName;
            File.WriteAllText(Path.Combine(global, "storage.json"), $$"""
                {
                  "profileAssociations": { "workspaces": {
                    "{{Uri(Path.Combine(loja, "Loja.code-workspace"))}}": "__default__profile__",
                    "{{Uri(api)}}": "__default__profile__",
                    "{{Uri(solta)}}": "__default__profile__",
                    "{{Uri(Path.Combine(repos, "Sumiu"))}}": "__default__profile__"
                  } },
                  "backupWorkspaces": { "workspaces": [{ "id": "1", "configURIPath": "{{Uri(Path.Combine(loja, "Loja.code-workspace"))}}" }] },
                  "theme": "vs-dark"
                }
                """);
            var achados = VsCode.Recentes(appData);
            Assert.Equal(2, achados.Count);
            Assert.Contains(achados, a => a.Workspace && a.Nome == "Loja");
            Assert.Contains(achados, a => !a.Workspace && a.Nome == "Loja.Api");
        }
        finally
        {
            Directory.Delete(raiz, true);
        }
    }

    [Fact]
    public void RecentesDesteComputador()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_MEDIR") != "1") return;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var achados = VsCode.Recentes(appData);
        foreach (var a in achados) saida.WriteLine($"{(a.Workspace ? "W" : "P")} {a.Caminho}");
        foreach (var r in VsCode.Raizes(achados, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))) saida.WriteLine($"R {r.Pasta} {r.Quantos}");
    }
}
