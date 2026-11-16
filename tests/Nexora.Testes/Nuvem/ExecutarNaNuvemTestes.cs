using System.Collections.Concurrent;
using Nexora.Core.Executar;
using Nexora.Core.Nuvem;

namespace Nexora.Testes.Nuvem;

public sealed class ExecutarNaNuvemTestes : IDisposable
{
    readonly MaquinaFalsa _falsa = new();

    public ExecutarNaNuvemTestes() => ProcessoRemoto.Esperas = [TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500)];

    public void Dispose() => _falsa.Dispose();

    [Fact]
    public void Pasta_do_alvo_vira_subpasta_remota_e_fora_do_projeto_e_recusada()
    {
        var local = Path.Combine(Path.GetTempPath(), "proj");
        Assert.Equal("/home/u/nexora/proj/main", ExecucaoNaNuvem.PastaDoAlvo("/home/u/nexora/proj/main", local, local));
        Assert.Equal("/home/u/nexora/proj/main/web/app", ExecucaoNaNuvem.PastaDoAlvo("/home/u/nexora/proj/main", local, Path.Combine(local, "web", "app")));
        Assert.Throws<InvalidOperationException>(() => ExecucaoNaNuvem.PastaDoAlvo("/r", local, Path.Combine(Path.GetTempPath(), "outro")));
    }

    [Fact]
    public void Acha_a_porta_nos_enderecos_que_os_servidores_imprimem()
    {
        Assert.Equal(5173, ExecucaoNaNuvem.PortaDoEndereco("  ➜  Local:   http://localhost:5173/"));
        Assert.Equal(5000, ExecucaoNaNuvem.PortaDoEndereco("Now listening on: http://127.0.0.1:5000"));
        Assert.Equal(8000, ExecucaoNaNuvem.PortaDoEndereco("Serving HTTP on 0.0.0.0 port 8000 (http://0.0.0.0:8000/) ..."));
        Assert.Null(ExecucaoNaNuvem.PortaDoEndereco("Network: http://10.0.0.5:3000"));
        Assert.Null(ExecucaoNaNuvem.PortaDoEndereco("compilando…"));
    }

    [Fact]
    public void Tunel_ssh_so_encaminha_a_porta_no_localhost_e_falha_se_nao_conseguir()
    {
        var maquina = new MaquinaNaNuvem("a", "VPS", TiposDeMaquina.Ssh, "vps.exemplo.com", "ubuntu", 2200);
        var args = new TransporteSsh(maquina, "chave", "conhecidos").ArgumentosDoTunel(5174, 5173).ToList();
        Assert.Contains("-N", args);
        Assert.Contains("ExitOnForwardFailure=yes", args);
        Assert.Equal("127.0.0.1:5174:localhost:5173", args[args.IndexOf("-L") + 1]);
        Assert.Equal("ubuntu@vps.exemplo.com", args[^1]);
        Assert.Contains("2200", args);
    }

    [Fact]
    public void Sugestao_so_aparece_com_memoria_alta_projeto_liberado_e_maquina_pronta()
    {
        Assert.Contains("91%", SugestaoDaNuvem.Texto(0.91, true, "VPS"));
        Assert.Contains("VPS", SugestaoDaNuvem.Texto(0.91, true, "VPS"));
        Assert.Null(SugestaoDaNuvem.Texto(0.70, true, "VPS"));
        Assert.Null(SugestaoDaNuvem.Texto(0.95, false, "VPS"));
        Assert.Null(SugestaoDaNuvem.Texto(0.95, true, null));
        Assert.Null(SugestaoDaNuvem.Texto(null, true, "VPS"));
        if (OperatingSystem.IsWindows()) Assert.InRange(SugestaoDaNuvem.MemoriaEmUso()!.Value, 0.01, 1.0);
    }

    [Fact]
    public void Comando_junta_os_erros_na_saida()
    {
        var alvo = new Alvo("dev", "npm run dev", "/p", "npm", ["run", "dev"], new Dictionary<string, string>());
        Assert.Equal("exec 2>&1\nexec npm run dev\n", ExecucaoNaNuvem.Comando(alvo, null));
    }

    [Fact]
    public void Comando_do_windows_vira_comando_do_linux_na_maquina()
    {
        var npm = new Alvo("dev", "npm run dev", "/p", "cmd.exe", ["/c", "npm", "run", "dev"], new Dictionary<string, string>());
        Assert.Equal("exec 2>&1\nexec npm run dev\n", ExecucaoNaNuvem.Comando(npm, null));
        var dotnet = new Alvo("api", "api", "/p", "dotnet.exe", ["run"], new Dictionary<string, string>());
        Assert.Equal("exec 2>&1\nexec dotnet run\n", ExecucaoNaNuvem.Comando(dotnet, null));
    }

    [Fact]
    public async Task Executar_envia_o_codigo_roda_na_maquina_avisa_o_endereco_e_para()
    {
        if (!MaquinaFalsa.Disponivel) return;
        var repositorio = await _falsa.RepositorioAsync("site");
        var nuvem = new ServicoDaNuvem(null, null, Path.Combine(_falsa.Raiz, "maquinas"));
        var maquina = nuvem.Salvar(new MaquinaNaNuvem("m1", "Teste", TiposDeMaquina.Teste));
        var home = (await nuvem.Abrir(maquina).RodarAsync("printf %s \"$HOME\"")).Saida.Trim();
        var remota = CodigoNaNuvem.PastaRemota(home, repositorio, "main");
        var alvo = new Alvo("dev", "servidor", repositorio, "sh",
            ["-c", "echo \"Local: http://localhost:$PORT/\"; echo segredo=$SEGREDO; ls; echo erro-no-stderr >&2; sleep 60"],
            new Dictionary<string, string>(), Porta: 4321);
        var linhas = new ConcurrentQueue<string>();
        var endereco = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fim = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var execucao = new ExecucaoNaNuvem(nuvem, maquina, alvo, 4321, repositorio, remota, new Dictionary<string, string> { ["SEGREDO"] = "s3" });
        execucao.Linha += (l, _) => linhas.Enqueue(l);
        execucao.Ouvindo += u => endereco.TrySetResult(u);
        execucao.Terminou += c => fim.TrySetResult(c);
        execucao.Iniciar();
        Assert.Equal("http://localhost:4321/", await endereco.Task.WaitAsync(TimeSpan.FromSeconds(60)));
        var limite = DateTime.Now.AddSeconds(20);
        while (!linhas.Contains("erro-no-stderr") && DateTime.Now < limite) await Task.Delay(100);
        Assert.Contains(linhas, l => l.Contains("Enviando o código"));
        Assert.Contains("segredo=s3", linhas);
        Assert.Contains("leia.txt", linhas);
        Assert.Contains("erro-no-stderr", linhas);
        Assert.DoesNotContain(execucao.Tuneis, t => t.Aberto.IsCompletedSuccessfully && t.Aberto.Result);
        execucao.Parar();
        await fim.Task.WaitAsync(TimeSpan.FromSeconds(30));
        limite = DateTime.Now.AddSeconds(10);
        while (Directory.Exists(Path.Combine(nuvem.Teste.Casa(maquina), ".nexora", "execucoes", execucao.Id)) && DateTime.Now < limite) await Task.Delay(100);
        Assert.False(Directory.Exists(Path.Combine(nuvem.Teste.Casa(maquina), ".nexora", "execucoes", execucao.Id)));

        var segunda = new ExecucaoNaNuvem(nuvem, maquina, alvo with { Argumentos = ["-c", "echo pronto"] }, null, repositorio, remota);
        var linhas2 = new ConcurrentQueue<string>();
        var fim2 = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        segunda.Linha += (l, _) => linhas2.Enqueue(l);
        segunda.Terminou += c => fim2.TrySetResult(c);
        segunda.Iniciar();
        Assert.Equal(0, await fim2.Task.WaitAsync(TimeSpan.FromSeconds(60)));
        Assert.Contains(linhas2, l => l.Contains("já está na máquina"));
        Assert.Contains("pronto", linhas2);
        segunda.Dispose();
    }
}
