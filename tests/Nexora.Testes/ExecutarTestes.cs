using Nexora.Core.Executar;

namespace Nexora.Testes;

public sealed class ExecutarTestes : IDisposable
{
    readonly string _raiz = Directory.CreateTempSubdirectory("nexora-executar-").FullName;

    public void Dispose() => Directory.Delete(_raiz, recursive: true);

    string Gravar(string relativo, string conteudo)
    {
        var caminho = Path.Combine(_raiz, relativo);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllText(caminho, conteudo);
        return caminho;
    }

    void MontarRepositorio(int portaLocal)
    {
        Gravar(@".vscode\launch.json", """
            {
                "version": "0.2.0",
                "configurations": [
                    {
                        // o dia a dia
                        "name": "1. Acme (Backend/API)",
                        "type": "coreclr",
                        "request": "launch",
                        "cwd": "${workspaceFolder}/src/Web",
                        "launchSettingsProfile": "Web",
                        "env": { "ASPNETCORE_ENVIRONMENT": "Development" },
                        "serverReadyAction": { "action": "openExternally", "uriFormat": "https://acme-local.dev.local:%s" },
                    },
                    { "name": "2. Navegador", "type": "blazorwasm", "request": "launch" }
                ]
            }
            """);
        Gravar(@"src\Web\Web.csproj", "<Project />");
        Gravar(@"src\Web\Properties\launchSettings.json", """
            { "profiles": {
                "IIS Express": { "commandName": "IISExpress" },
                "Web": { "commandName": "Project", "applicationUrl": "https://acme-local.dev.local:44315/", "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development", "X": "1" } },
                "Outro": { "commandName": "Project", "applicationUrl": "http://localhost:5000;https://localhost:5001" }
            } }
            """);
        Gravar(@"src\Web\appsettings.Development.json", $$"""{ "App": { "SelfUrl": "https://dev.local:{{portaLocal}}" } }""");
        Gravar(@"src\Web\bin\Debug\Properties\launchSettings.json", """{ "profiles": { "Lixo": { "commandName": "Project" } } }""");
        Gravar(@"front\package.json", """{ "scripts": { "dev": "vite" } }""");
        Gravar(@"node_modules\x\package.json", """{ "scripts": { "nao": "x" } }""");
    }

    [Fact]
    public void DescobreLaunchJsonLaunchSettingsEScripts()
    {
        MontarRepositorio(44315);
        var alvos = Alvos.Descobrir([_raiz]);

        Assert.Equal(["1. Acme (Backend/API)", "Web · Outro", "npm run dev"], alvos.Select(a => a.Nome));
        var web = alvos[0];
        Assert.Equal(Path.Combine(_raiz, "src", "Web"), web.Pasta);
        Assert.Equal(["run", "--project", Path.Combine(_raiz, "src", "Web", "Web.csproj"), "--launch-profile", "Web"], web.Argumentos);
        Assert.Equal("Development", web.Variaveis["ASPNETCORE_ENVIRONMENT"]);
        Assert.Equal("1", web.Variaveis["X"]);
        Assert.Equal(44315, web.Porta);
        Assert.Null(web.PortaLocal);
        Assert.Equal("https://acme-local.dev.local:44315", web.EnderecoCom(null, null));

        var outro = alvos[1];
        Assert.Equal("https://localhost:5001", outro.Url);
        Assert.Equal(["/c", "npm", "run", "dev"], alvos[2].Argumentos);
        Assert.Equal("front", alvos[2].Detalhe);
    }

    [Fact]
    public void WorktreeUsaAPortaDoAppsettingsLocal()
    {
        MontarRepositorio(44320);
        var web = Alvos.Descobrir([_raiz])[0];

        Assert.Equal(44320, web.PortaLocal);
        Assert.Equal(44320, web.PortaSugerida);
        Assert.Equal(["--urls", "https://acme-local.dev.local:44320"], web.ArgumentosCom(44320).TakeLast(2));
        Assert.Equal(web.Argumentos, web.ArgumentosCom(44315));
        Assert.Equal("https://acme-local.dev.local:44320", web.EnderecoCom(44320, "https://[::]:44320"));
        Assert.Equal("4000", Alvos.Descobrir([_raiz])[2].VariaveisCom(4000)["PORT"]);
    }

    [Fact]
    public async Task ExecucaoLeASaidaEAvisaQuandoTermina()
    {
        var alvo = new Alvo("eco", "teste", _raiz, "cmd.exe", ["/c", "echo \u001b[32mNow listening on: http://localhost:5123\u001b[0m"], new Dictionary<string, string>());
        var linhas = new List<string>();
        var ouvindo = new TaskCompletionSource<string>();
        var fim = new TaskCompletionSource<int>();
        using var execucao = new Execucao(alvo, null);
        execucao.Linha += (l, _) =>
        {
            lock (linhas) linhas.Add(l);
        };
        execucao.Ouvindo += u => ouvindo.TrySetResult(u);
        execucao.Terminou += c => fim.TrySetResult(c);
        execucao.Iniciar();

        Assert.Equal(0, await fim.Task.WaitAsync(TimeSpan.FromSeconds(20)));
        Assert.Equal("http://localhost:5123", await ouvindo.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("Now listening on: http://localhost:5123", linhas);
    }
}
