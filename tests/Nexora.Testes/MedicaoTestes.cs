using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Claude;
using Nexora.Core.Mcp;
using Xunit.Abstractions;

namespace Nexora.Testes;

public sealed partial class MedicaoTestes(ITestOutputHelper saida)
{
    const int Sessoes = 3;

    [Fact]
    public async Task MemoriaDasSessoesComESemOHub()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_MEDIR") != "1") return;
        var worktree = Environment.GetEnvironmentVariable("NEXORA_MEDIR_WORKTREE") ?? @"D:\Repositorios\Acme.Shop-melhorias";
        var scripts = Path.Combine(worktree, "Acme.Shop", "etc", "mcp").Replace('\\', '/');
        var pasta = Directory.CreateTempSubdirectory("nexora-medicao-").FullName;
        File.WriteAllText(Path.Combine(pasta, ".mcp.json"), $$"""
            { "mcpServers": {
                "banco-local": { "command": "node", "args": ["{{scripts}}/banco-local.mjs"] },
                "navegador": { "command": "node", "args": ["{{scripts}}/navegador.mjs"] } } }
            """);
        var relatorio = new StringBuilder();
        void Anotar(string linha)
        {
            relatorio.AppendLine(linha);
            saida.WriteLine(linha);
        }

        var semHub = await MedirAsync(pasta, null, Anotar, "Sem o hub (como a extensão do VS Code)");
        await Task.Delay(3000);

        var cache = Path.Combine(pasta, "cache");
        using (var aquecer = new HubMcp(cache))
        {
            var r = aquecer.Registrar(pasta, "aquecer");
            await using var s = new SessaoClaude(new OpcoesSessao(pasta, Isolada: true, Mcp: r.Configuracao));
            s.Iniciar();
            await EsperarMcpAsync(s);
        }
        await Task.Delay(2000);

        using var hub = new HubMcp(cache);
        var comHub = await MedirAsync(pasta, hub, Anotar, "Com o hub do Nexora, sessões paradas");
        Anotar("");

        Anotar($"Diferença com {Sessoes} sessões paradas: {Mb(semHub - comHub)} MB a menos com o hub");
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "nexora-medicao.txt"), relatorio.ToString());
        try
        {
            Directory.Delete(pasta, true);
        }
        catch (IOException)
        {
        }
        foreach (var d in Directory.EnumerateDirectories(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects"), "*nexora-medicao*"))
            Directory.Delete(d, true);
    }

    async Task<long> MedirAsync(string pasta, HubMcp? hub, Action<string> anotar, string titulo)
    {
        var sessoes = new List<SessaoClaude>();
        try
        {
            for (var i = 0; i < Sessoes; i++)
            {
                var registro = hub?.Registrar(pasta, $"s{i}");
                var s = new SessaoClaude(new OpcoesSessao(pasta, Isolada: true, Mcp: registro?.Configuracao));
                s.Iniciar();
                sessoes.Add(s);
            }
            foreach (var s in sessoes) await EsperarMcpAsync(s);
            await Task.Delay(4000);

            var arvore = Arvore();
            long claude = 0, mcps = 0;
            var processosMcp = 0;
            foreach (var s in sessoes)
            {
                if (s.Pid is not { } pid) continue;
                claude += Privada(pid);
                foreach (var filho in Descendentes(arvore, pid))
                {
                    mcps += Privada(filho);
                    processosMcp++;
                }
            }
            var doHub = hub?.Estado().Sum(e => e.Memoria) ?? 0;
            var processosHub = hub?.Estado().Sum(e => e.Processos) ?? 0;
            anotar($"== {titulo}");
            anotar($"   claude.exe ({Sessoes}x): {Mb(claude)} MB");
            anotar($"   MCPs dentro das sessões: {Mb(mcps)} MB em {processosMcp} processos");
            if (hub is not null)
            {
                anotar($"   MCPs no hub: {Mb(doHub)} MB em {processosHub} processos");
                foreach (var e in hub.Estado())
                    anotar($"     {e.Nome}{(e.Dono is { } d ? $" ({d})" : "")}: {(e.Ativo ? $"ativo, {e.Processos} processos, subiu por {e.SubiuPor}" : "parado")}");
            }
            var total = claude + mcps + doHub;
            anotar($"   total: {Mb(total)} MB");

            if (hub is not null && await UsarBancoAsync(hub, pasta) is { } usado)
            {
                anotar($"== Com o hub, depois de uma consulta ao banco (select 1): {usado.Mensagem}");
                anotar($"   MCPs no hub: {Mb(usado.Memoria)} MB em {usado.Processos} processos (um banco-local para as {Sessoes} sessões)");
            }
            return total;
        }
        finally
        {
            foreach (var s in sessoes) await s.DisposeAsync();
        }
    }

    static async Task<(string Mensagem, long Memoria, int Processos)?> UsarBancoAsync(HubMcp hub, string pasta)
    {
        var r = hub.Registrar(pasta, "consulta");
        var url = JsonNode.Parse(r.Configuracao!)!["mcpServers"]!["banco-local"]!["url"]!.GetValue<string>();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        async Task<JsonNode?> Pedir(string metodo, JsonObject? p)
        {
            var corpo = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = Guid.NewGuid().ToString("N"), ["method"] = metodo };
            if (p is not null) corpo["params"] = p;
            using var pedido = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json") };
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", r.Chave);
            using var resposta = await http.SendAsync(pedido);
            return JsonNode.Parse(await resposta.Content.ReadAsStringAsync());
        }
        var lista = await Pedir("tools/list", null);
        var ferramenta = lista?["result"]?["tools"]?.AsArray().Select(t => t?["name"]?.GetValue<string>()).FirstOrDefault(n => n?.StartsWith("execute_sql") == true);
        if (ferramenta is null) return null;
        var chamada = await Pedir("tools/call", new JsonObject { ["name"] = ferramenta, ["arguments"] = new JsonObject { ["sql"] = "select 1 as ok" } });
        var texto = chamada?["result"]?["content"]?[0]?["text"]?.GetValue<string>() ?? chamada?["error"]?["message"]?.GetValue<string>() ?? "?";
        await Task.Delay(1500);
        var banco = hub.Estado().Where(e => e.Nome == "banco-local" && e.Ativo).ToList();
        return ($"{ferramenta} respondeu {(texto.Contains("ok") ? "ok" : texto[..Math.Min(80, texto.Length)])}", banco.Sum(e => e.Memoria), banco.Sum(e => e.Processos));
    }

    static async Task EsperarMcpAsync(SessaoClaude s)
    {
        for (var i = 0; i < 60; i++)
        {
            var r = await s.PedirAsync(new JsonObject { ["subtype"] = "mcp_status" }, TimeSpan.FromSeconds(20));
            if (r.Dados is { } d && d.TryGetProperty("mcpServers", out var lista))
            {
                var nossos = lista.EnumerateArray().Where(x => x.GetProperty("name").GetString() is "banco-local" or "navegador").ToList();
                if (nossos.Count == 2 && nossos.All(x => x.GetProperty("status").GetString() is "connected" or "failed")) return;
            }
            await Task.Delay(500);
        }
    }

    static long Mb(long bytes) => bytes / (1024 * 1024);

    static long Privada(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.PrivateMemorySize64;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return 0;
        }
    }

    static IEnumerable<int> Descendentes(Dictionary<int, List<int>> arvore, int pid)
    {
        if (!arvore.TryGetValue(pid, out var filhos)) yield break;
        foreach (var f in filhos)
        {
            yield return f;
            foreach (var n in Descendentes(arvore, f)) yield return n;
        }
    }

    static Dictionary<int, List<int>> Arvore()
    {
        var mapa = new Dictionary<int, List<int>>();
        var foto = CreateToolhelp32Snapshot(2, 0);
        try
        {
            var e = new Entrada { Tamanho = (uint)Marshal.SizeOf<Entrada>() };
            for (var ok = Process32FirstW(foto, ref e); ok; ok = Process32NextW(foto, ref e))
            {
                if (!mapa.TryGetValue((int)e.Pai, out var lista)) mapa[(int)e.Pai] = lista = [];
                lista.Add((int)e.Pid);
            }
        }
        finally
        {
            CloseHandle(foto);
        }
        return mapa;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct Entrada
    {
        public uint Tamanho, Uso, Pid;
        public IntPtr Heap;
        public uint Modulo, Threads, Pai;
        public int Prioridade;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Nome;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool Process32FirstW(IntPtr foto, ref Entrada entrada);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool Process32NextW(IntPtr foto, ref Entrada entrada);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr h);
}
