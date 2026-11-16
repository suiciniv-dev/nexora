using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

Console.InputEncoding = new UTF8Encoding(false);
var saida = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
var trava = new object();
void Emitir(JsonNode n) { var l = n.ToJsonString(); lock (trava) saida.WriteLine(l); }

var cwd = Directory.GetCurrentDirectory();
var arquivoConf = Path.Combine(cwd, ".sim");
var conf = File.Exists(arquivoConf) ? File.ReadAllText(arquivoConf).Trim() : "passos=40;builds=";
var mapa = conf.Split(';').Select(p => p.Split('=')).ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : "");
var passos = int.Parse(mapa["passos"]);
var builds = mapa["builds"].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToHashSet();
var sessao = Guid.NewGuid().ToString();
var sorte = new Random(cwd.Length * 7919 + passos);
var arquivos = Directory.EnumerateFiles(Path.Combine(cwd, "src"), "*.cs", SearchOption.AllDirectories)
    .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj") && !f.Contains(Path.DirectorySeparatorChar + "bin")).ToArray();
long contexto = 18000;
var iniciado = false;
Task? turno = null;

string? linha;
while ((linha = Console.In.ReadLine()) is not null)
{
    JsonNode? n;
    try { n = JsonNode.Parse(linha); } catch { continue; }
    var tipo = (string?)n?["type"];
    if (tipo == "control_request")
    {
        var id = (string?)n!["request_id"];
        var sub = (string?)n["request"]?["subtype"];
        JsonNode dados = sub == "initialize"
            ? new JsonObject { ["commands"] = new JsonArray(), ["models"] = new JsonArray(new JsonObject { ["value"] = "default", ["displayName"] = "Opus", ["description"] = "sim" }) }
            : new JsonObject();
        Emitir(new JsonObject { ["type"] = "control_response", ["response"] = new JsonObject { ["subtype"] = "success", ["request_id"] = id, ["response"] = dados } });
    }
    else if (tipo == "user" && (turno is null || turno.IsCompleted))
    {
        if (!iniciado)
        {
            iniciado = true;
            Emitir(new JsonObject { ["type"] = "system", ["subtype"] = "init", ["session_id"] = sessao, ["model"] = "claude-opus-5-5", ["cwd"] = cwd, ["permissionMode"] = "default" });
        }
        turno = Task.Run(Turno);
    }
}
turno?.Wait();

async Task Turno()
{
    var inicio = Stopwatch.StartNew();
    for (var i = 0; i < passos; i++)
    {
        await Pensar();
        await Falar(Paragrafo(i));
        if (builds.Contains(i))
        {
            await Ferramenta("Bash", new JsonObject { ["command"] = "dotnet build src/Nexora", ["description"] = "Compila o app" }, Build);
            continue;
        }
        switch (sorte.Next(4))
        {
            case 0:
            case 1:
                var arq = arquivos[sorte.Next(arquivos.Length)];
                await Ferramenta("Read", new JsonObject { ["file_path"] = arq }, () => Task.FromResult(Numerar(File.ReadAllText(arq))));
                break;
            case 2:
                await Ferramenta("Grep", new JsonObject { ["pattern"] = "Idioma.T", ["path"] = "src" },
                    () => Task.FromResult(string.Join("\n", arquivos.Take(60).Select(a => Path.GetRelativePath(cwd, a) + ":12:    var x = Idioma.T(\"texto\");"))));
                break;
            default:
                var alvo = arquivos[sorte.Next(arquivos.Length)];
                var linhas = File.ReadAllLines(alvo);
                var trecho = string.Join("\n", linhas.Take(Math.Min(25, linhas.Length)));
                await Ferramenta("Edit", new JsonObject { ["file_path"] = alvo, ["old_string"] = trecho, ["new_string"] = trecho + "\n" },
                    () => Task.FromResult($"The file {alvo} has been updated successfully."));
                break;
        }
    }
    await Falar("Pronto: as três mudanças estão feitas e os testes passaram.");
    Emitir(new JsonObject
    {
        ["type"] = "result", ["subtype"] = "success", ["is_error"] = false, ["result"] = "Pronto.", ["total_cost_usd"] = 1.23,
        ["duration_ms"] = inicio.ElapsedMilliseconds, ["session_id"] = sessao,
        ["modelUsage"] = new JsonObject { ["claude-opus-5-5"] = new JsonObject { ["contextWindow"] = 200000 } },
    });
}

JsonObject Parcial(JsonObject evento) => new() { ["type"] = "stream_event", ["event"] = evento, ["session_id"] = sessao, ["parent_tool_use_id"] = null };

async Task Pensar()
{
    Emitir(Parcial(new JsonObject { ["type"] = "content_block_start", ["index"] = 0, ["content_block"] = new JsonObject { ["type"] = "thinking", ["thinking"] = "" } }));
    for (var k = 0; k < 25; k++)
    {
        Emitir(Parcial(new JsonObject { ["type"] = "content_block_delta", ["index"] = 0, ["delta"] = new JsonObject { ["type"] = "thinking_delta", ["thinking"] = "Vou conferir o fluxo antes de mexer. " } }));
        await Task.Delay(60);
    }
    Emitir(Parcial(new JsonObject { ["type"] = "content_block_stop", ["index"] = 0 }));
}

async Task Falar(string texto)
{
    Emitir(Parcial(new JsonObject { ["type"] = "content_block_start", ["index"] = 1, ["content_block"] = new JsonObject { ["type"] = "text", ["text"] = "" } }));
    for (var p = 0; p < texto.Length; p += 20)
    {
        Emitir(Parcial(new JsonObject { ["type"] = "content_block_delta", ["index"] = 1, ["delta"] = new JsonObject { ["type"] = "text_delta", ["text"] = texto.Substring(p, Math.Min(20, texto.Length - p)) } }));
        await Task.Delay(50);
    }
    Emitir(Parcial(new JsonObject { ["type"] = "content_block_stop", ["index"] = 1 }));
    Assistente(new JsonObject { ["type"] = "text", ["text"] = texto });
}

void Assistente(JsonObject bloco)
{
    contexto += 900;
    Emitir(new JsonObject
    {
        ["type"] = "assistant", ["session_id"] = sessao, ["parent_tool_use_id"] = null,
        ["message"] = new JsonObject
        {
            ["id"] = "msg_" + Guid.NewGuid().ToString("N"), ["role"] = "assistant", ["model"] = "claude-opus-5-5", ["content"] = new JsonArray(bloco),
            ["usage"] = new JsonObject { ["input_tokens"] = 4, ["cache_creation_input_tokens"] = 600, ["cache_read_input_tokens"] = contexto, ["output_tokens"] = 300, ["cache_creation"] = new JsonObject { ["ephemeral_5m_input_tokens"] = 600 } },
        },
    });
}

async Task Ferramenta(string nome, JsonObject entrada, Func<Task<string>> rodar)
{
    var id = "toolu_" + Guid.NewGuid().ToString("N")[..20];
    Emitir(Parcial(new JsonObject { ["type"] = "content_block_start", ["index"] = 2, ["content_block"] = new JsonObject { ["type"] = "tool_use", ["id"] = id, ["name"] = nome, ["input"] = new JsonObject() } }));
    await Task.Delay(300);
    Emitir(Parcial(new JsonObject { ["type"] = "content_block_stop", ["index"] = 2 }));
    Assistente(new JsonObject { ["type"] = "tool_use", ["id"] = id, ["name"] = nome, ["input"] = entrada });
    var resultado = await rodar();
    contexto += resultado.Length / 4;
    Emitir(new JsonObject
    {
        ["type"] = "user", ["session_id"] = sessao, ["parent_tool_use_id"] = null,
        ["message"] = new JsonObject
        {
            ["role"] = "user",
            ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = id, ["content"] = resultado, ["is_error"] = false }),
        },
    });
}

async Task<string> Build()
{
    var log = Path.Combine(cwd, "..", "builds.log");
    File.AppendAllText(log, $"{DateTime.Now:HH:mm:ss} {Path.GetFileName(cwd)} inicio\n");
    var info = new ProcessStartInfo("dotnet", "build src/Nexora -nologo --no-incremental")
    {
        WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
    };
    using var p = Process.Start(info)!;
    var o = p.StandardOutput.ReadToEndAsync();
    var e = p.StandardError.ReadToEndAsync();
    await p.WaitForExitAsync();
    var tudo = await o + await e;
    File.AppendAllText(log, $"{DateTime.Now:HH:mm:ss} {Path.GetFileName(cwd)} fim exit={p.ExitCode}\n");
    return tudo.Length > 30000 ? tudo[^30000..] : tudo;
}

string Numerar(string s) => string.Join("\n", s.Split('\n').Select((l, i) => $"{i + 1,6}\t{l.TrimEnd('\r')}"));

string Paragrafo(int i) =>
    $"Passo {i + 1}. Olhei o `ChatVM` e o fluxo de envio. O problema aparece quando a sessão termina antes do **build**:\n\n" +
    "- o evento chega fora de ordem;\n- a aba fica marcada como trabalhando;\n- o contador não zera.\n\n" +
    "```csharp\nif (sessao.Encerrada && !Trabalhando)\n    Contador.Zerar();\n```\n\nVou ajustar isso e conferir com os testes.";
