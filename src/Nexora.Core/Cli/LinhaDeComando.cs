using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nexora.Core.Cli;

public delegate Task<JsonObject?> TransporteDaCli(JsonObject pedido, CancellationToken cancelar);

public sealed class LinhaDeComando(TransporteDaCli transporte, TextWriter saida, TextWriter erros, TextReader entrada, string pastaAtual)
{
    public const string Ajuda = """
        nexora: controla o Nexora aberto a partir do terminal.

        Uso:
          nexora status [--json]                    O Nexora está aberto? Versão e sessões.
          nexora abrir [pasta]                      Abre uma conversa nova no projeto (padrão: pasta atual).
          nexora sessoes [--projeto P] [--json]     Lista as sessões abertas.
          nexora iniciar <projeto> <mensagem|->     Começa uma conversa e manda a mensagem.
              [--agente claude|codex] [--modelo M] [--espere] [--json]
          nexora enviar <sessao> <mensagem|->       Manda uma mensagem (entra na fila se estiver trabalhando).
              [--espere] [--json]
          nexora ver <sessao> [--mensagens N]       Mostra o estado e os últimos itens da conversa.
          nexora esperar <sessao> [--segundos N]    Espera a sessão parar de trabalhar.
          nexora ao-vivo [--json]                   O que precisa de você agora.
          nexora parar <sessao>                     Interrompe o turno atual.

        <projeto> pode ser "." (a pasta atual), um caminho ou o nome do projeto.
        Use "-" no lugar da mensagem para ler do teclado ou de um pipe.

        Saída: 0 ok, 1 erro, 2 precisa de você, 3 o Nexora não está aberto, 64 uso errado.
        """;

    static readonly JsonSerializerOptions Bonito = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static TimeSpan IntervaloDeEspera { get; set; } = TimeSpan.FromSeconds(240);

    public async Task<int> RodarAsync(IReadOnlyList<string> args, CancellationToken cancelar = default)
    {
        var (posicionais, opcoes) = Separar(args);
        if (posicionais.Count == 0 || opcoes.ContainsKey("help") || posicionais[0] is "ajuda" or "help" or "-h")
        {
            await saida.WriteLineAsync(Ajuda);
            return posicionais.Count == 0 && !opcoes.ContainsKey("help") ? CodigosDaCli.Uso : CodigosDaCli.Ok;
        }
        var json = opcoes.ContainsKey("json");
        var comando = posicionais[0];
        var resto = posicionais.Skip(1).ToList();
        try
        {
            return comando switch
            {
                "status" => await StatusAsync(json, cancelar),
                "abrir" => await SimplesAsync(new JsonObject { ["comando"] = "abrir", ["projeto"] = Projeto(resto.ElementAtOrDefault(0) ?? ".") }, json, r => $"Aberta {r["id"]} em {r["pasta"]}.", cancelar),
                "sessoes" or "sessões" => await SessoesAsync(opcoes.GetValueOrDefault("projeto") is { } p ? Projeto(p) : null, json, cancelar),
                "iniciar" => await IniciarAsync(resto, opcoes, json, cancelar),
                "enviar" => await EnviarAsync(resto, opcoes, json, cancelar),
                "ver" => await VerAsync(resto, opcoes, json, cancelar),
                "esperar" => await EsperarAsync(Exigir(resto, 0, "a sessão"), int.TryParse(opcoes.GetValueOrDefault("segundos"), out var s) ? s : null, json, cancelar),
                "ao-vivo" => await AoVivoAsync(json, cancelar),
                "parar" => await SimplesAsync(new JsonObject { ["comando"] = "parar", ["id"] = Exigir(resto, 0, "a sessão") }, json, r => $"Pedido de parada enviado para {r["id"]}.", cancelar),
                _ => await UsoAsync($"Comando desconhecido: {comando}. Veja nexora ajuda."),
            };
        }
        catch (ErroDeUso ex)
        {
            return await UsoAsync(ex.Message);
        }
        catch (FalhaDoNexora ex)
        {
            if (json) await saida.WriteLineAsync(ex.Resposta.ToJsonString(Bonito));
            else await erros.WriteLineAsync(ex.Message);
            return ex.Codigo;
        }
    }

    async Task<int> UsoAsync(string texto)
    {
        await erros.WriteLineAsync(texto);
        return CodigosDaCli.Uso;
    }

    static (List<string> Posicionais, Dictionary<string, string> Opcoes) Separar(IReadOnlyList<string> args)
    {
        string[] semValor = ["json", "espere", "help"];
        var posicionais = new List<string>();
        var opcoes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a.StartsWith("--", StringComparison.Ordinal) && a.Length > 2)
            {
                var nome = a[2..];
                var igual = nome.IndexOf('=');
                if (igual > 0) opcoes[nome[..igual]] = nome[(igual + 1)..];
                else if (semValor.Contains(nome, StringComparer.OrdinalIgnoreCase) || i + 1 >= args.Count) opcoes[nome] = "";
                else opcoes[nome] = args[++i];
            }
            else posicionais.Add(a);
        }
        return (posicionais, opcoes);
    }

    static string Exigir(List<string> resto, int i, string oQue) =>
        resto.ElementAtOrDefault(i) is { Length: > 0 } valor ? valor : throw new ErroDeUso($"Falta {oQue}. Veja nexora ajuda.");

    string Projeto(string valor) =>
        valor == "." || valor.Contains('/') || valor.Contains('\\') || Directory.Exists(Path.Combine(pastaAtual, valor))
            ? Path.GetFullPath(Path.Combine(pastaAtual, valor))
            : valor;

    string Mensagem(string valor)
    {
        var texto = valor == "-" ? entrada.ReadToEnd() : valor;
        return texto.Trim() is { Length: > 0 } limpo ? limpo : throw new ErroDeUso("A mensagem está vazia.");
    }

    async Task<JsonObject> PedirAsync(JsonObject pedido, CancellationToken cancelar)
    {
        var resposta = await transporte(pedido, cancelar) ?? throw new FalhaDoNexora(
            "O Nexora não está aberto. Abra o Nexora e tente de novo.", CodigosDaCli.NexoraFechado,
            new JsonObject { ["ok"] = false, ["erro"] = "nexora-fechado", ["codigo"] = CodigosDaCli.NexoraFechado });
        if (resposta["ok"]?.GetValue<bool>() != true)
            throw new FalhaDoNexora(resposta["erro"]?.ToString() ?? "O Nexora recusou o pedido.", resposta["codigo"]?.GetValue<int>() ?? CodigosDaCli.Erro, resposta);
        resposta.Remove("ok");
        return resposta;
    }

    async Task<int> SimplesAsync(JsonObject pedido, bool json, Func<JsonObject, string> texto, CancellationToken cancelar)
    {
        var r = await PedirAsync(pedido, cancelar);
        await saida.WriteLineAsync(json ? r.ToJsonString(Bonito) : texto(r));
        return CodigosDaCli.Ok;
    }

    async Task<int> StatusAsync(bool json, CancellationToken cancelar)
    {
        var r = await PedirAsync(new JsonObject { ["comando"] = "status" }, cancelar);
        if (json) await saida.WriteLineAsync(r.ToJsonString(Bonito));
        else
        {
            await saida.WriteLineAsync($"Nexora {r["versao"]} aberto.");
            await saida.WriteLineAsync($"{r["sessoes"]} sessões abertas · {r["trabalhando"]} trabalhando · {r["precisam"]} precisam de você.");
        }
        return CodigosDaCli.Ok;
    }

    async Task<int> SessoesAsync(string? projeto, bool json, CancellationToken cancelar)
    {
        var pedido = new JsonObject { ["comando"] = "sessoes" };
        if (projeto is not null) pedido["projeto"] = projeto;
        var r = await PedirAsync(pedido, cancelar);
        var lista = r["sessoes"] as JsonArray ?? [];
        if (json) await saida.WriteLineAsync(lista.ToJsonString(Bonito));
        else if (lista.Count == 0) await saida.WriteLineAsync("Nenhuma sessão aberta.");
        else foreach (var s in lista.OfType<JsonObject>()) await saida.WriteLineAsync(Linha(s));
        return CodigosDaCli.Ok;
    }

    static string Linha(JsonObject s)
    {
        var branch = s["branch"]?.ToString() is { Length: > 0 } b ? $" ({b})" : "";
        return $"{s["id"],-5} {s["estado"],-20} {s["titulo"]} · {s["projeto"]}{branch}";
    }

    async Task<int> IniciarAsync(List<string> resto, Dictionary<string, string> opcoes, bool json, CancellationToken cancelar)
    {
        var pedido = new JsonObject
        {
            ["comando"] = "iniciar",
            ["projeto"] = Projeto(Exigir(resto, 0, "o projeto")),
            ["mensagem"] = Mensagem(Exigir(resto, 1, "a mensagem")),
        };
        if (opcoes.GetValueOrDefault("agente") is { Length: > 0 } agente) pedido["agente"] = agente;
        if (opcoes.GetValueOrDefault("modelo") is { Length: > 0 } modelo) pedido["modelo"] = modelo;
        var r = await PedirAsync(pedido, cancelar);
        return await DepoisDeMandarAsync(r, opcoes.ContainsKey("espere"), json, $"Sessão {r["id"]} começou em {r["pasta"]}.", cancelar);
    }

    async Task<int> EnviarAsync(List<string> resto, Dictionary<string, string> opcoes, bool json, CancellationToken cancelar)
    {
        var r = await PedirAsync(new JsonObject
        {
            ["comando"] = "enviar",
            ["id"] = Exigir(resto, 0, "a sessão"),
            ["mensagem"] = Mensagem(Exigir(resto, 1, "a mensagem")),
        }, cancelar);
        return await DepoisDeMandarAsync(r, opcoes.ContainsKey("espere"), json, $"Mensagem enviada para {r["id"]}.", cancelar);
    }

    async Task<int> DepoisDeMandarAsync(JsonObject r, bool espere, bool json, string texto, CancellationToken cancelar)
    {
        if (!espere)
        {
            await saida.WriteLineAsync(json ? r.ToJsonString(Bonito) : texto);
            return CodigosDaCli.Ok;
        }
        if (!json) await erros.WriteLineAsync(texto + " Esperando…");
        return await EsperarAsync(r["id"]!.ToString(), null, json, cancelar, resposta: true);
    }

    async Task<int> EsperarAsync(string id, int? segundos, bool json, CancellationToken cancelar, bool resposta = false)
    {
        var limite = segundos is { } s ? DateTime.UtcNow.AddSeconds(s) : DateTime.MaxValue;
        JsonObject r;
        while (true)
        {
            var falta = limite == DateTime.MaxValue ? IntervaloDeEspera : limite - DateTime.UtcNow;
            if (falta > IntervaloDeEspera) falta = IntervaloDeEspera;
            if (falta < TimeSpan.Zero) falta = TimeSpan.Zero;
            r = await PedirAsync(new JsonObject { ["comando"] = "esperar", ["id"] = id, ["segundos"] = (int)Math.Ceiling(falta.TotalSeconds), ["resposta"] = resposta }, cancelar);
            if (r["estado"]?.ToString() != "trabalhando" || DateTime.UtcNow >= limite) break;
        }
        var estado = r["estado"]?.ToString();
        if (json) await saida.WriteLineAsync(r.ToJsonString(Bonito));
        else
        {
            if (r["resposta"]?.ToString() is { Length: > 0 } texto) await saida.WriteLineAsync(texto);
            if (r["pendencia"]?.ToString() is { Length: > 0 } pendencia) await erros.WriteLineAsync($"Precisa de você: {pendencia}. Responda no Nexora.");
            else if (estado == "trabalhando") await erros.WriteLineAsync($"{id} ainda está trabalhando.");
            else if (estado == "erro") await erros.WriteLineAsync($"{id} terminou com erro.");
        }
        return estado switch
        {
            "precisa de você" => CodigosDaCli.PrecisaDeVoce,
            "erro" => CodigosDaCli.Erro,
            _ => CodigosDaCli.Ok,
        };
    }

    async Task<int> VerAsync(List<string> resto, Dictionary<string, string> opcoes, bool json, CancellationToken cancelar)
    {
        var pedido = new JsonObject { ["comando"] = "ver", ["id"] = Exigir(resto, 0, "a sessão") };
        if (int.TryParse(opcoes.GetValueOrDefault("mensagens"), out var n)) pedido["ultimos"] = n;
        var r = await PedirAsync(pedido, cancelar);
        await saida.WriteLineAsync(json ? r.ToJsonString(Bonito) : r["texto"]?.ToString());
        return CodigosDaCli.Ok;
    }

    async Task<int> AoVivoAsync(bool json, CancellationToken cancelar)
    {
        var r = await PedirAsync(new JsonObject { ["comando"] = "sessoes", ["precisam"] = true }, cancelar);
        var lista = r["sessoes"] as JsonArray ?? [];
        if (json) await saida.WriteLineAsync(lista.ToJsonString(Bonito));
        else if (lista.Count == 0) await saida.WriteLineAsync("Nada precisa de você agora.");
        else
            foreach (var s in lista.OfType<JsonObject>())
                await saida.WriteLineAsync(Linha(s) + (s["pendencia"]?.ToString() is { Length: > 0 } p ? $"\n      {p}" : ""));
        return lista.Count > 0 ? CodigosDaCli.PrecisaDeVoce : CodigosDaCli.Ok;
    }

    sealed class ErroDeUso(string mensagem) : Exception(mensagem);

    sealed class FalhaDoNexora(string mensagem, int codigo, JsonObject resposta) : Exception(mensagem)
    {
        public int Codigo { get; } = codigo;
        public JsonObject Resposta { get; } = resposta;
    }
}
