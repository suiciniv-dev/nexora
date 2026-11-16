using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using Nexora.Core.Claude;
using Nexora.Core.Cli;
using Nexora.Core.Ambientes;
using Nexora.ViewModels;

namespace Nexora;

public sealed class ControleDoApp(JanelaVM janela)
{
    public static ServidorDoCanal? Servidor { get; private set; }

    public static string PastaDeDados => global::Nexora.Core.DadosDoNexora.Pasta;

    public static void Ligar(JanelaVM janela, string[] args)
    {
        var nome = App.Argumento(args, "--canal") ?? (JanelaVM.Isolada || JanelaVM.Demo ? null : CanalLocal.Nome(PastaDeDados));
        if (nome is null) return;
        var controle = new ControleDoApp(janela);
        Servidor = new ServidorDoCanal(nome, controle.AtenderAsync);
    }

    public Task<JsonObject> AtenderAsync(JsonObject pedido, CancellationToken cancelar) =>
        Dispatcher.UIThread.InvokeAsync(() => AtenderNaTelaAsync(pedido, cancelar));

    async Task<JsonObject> AtenderNaTelaAsync(JsonObject pedido, CancellationToken cancelar)
    {
        string? T(string nome) => pedido[nome]?.ToString().Trim() is { Length: > 0 } t ? t : null;
        int? N(string nome) => pedido[nome] is JsonValue v && v.TryGetValue<int>(out var n) ? n : null;
        try
        {
            return T("comando") switch
            {
                "status" => Status(),
                "abrir" => Abrir(T("projeto")),
                "sessoes" => Sessoes(T("projeto"), pedido["precisam"]?.GetValue<bool>() == true),
                "iniciar" => Iniciar(T("projeto"), T("mensagem"), T("agente"), T("modelo")),
                "enviar" => Enviar(T("id"), T("mensagem")),
                "ver" => Ver(T("id"), N("ultimos") ?? 12),
                "esperar" => await EsperarAsync(T("id"), N("segundos") ?? 120, pedido["resposta"]?.GetValue<bool>() == true, cancelar),
                "parar" => Parar(T("id")),
                var outro => ServidorDoCanal.Falha($"Comando desconhecido: {outro}. Atualize o Nexora."),
            };
        }
        catch (InvalidOperationException ex)
        {
            return ServidorDoCanal.Falha(ex.Message);
        }
    }

    IEnumerable<ChatVM> Sessoes() => janela.Abertas.Where(c => !c.EhLateral);

    JsonObject Status() => ServidorDoCanal.Ok(new JsonObject
    {
        ["versao"] = Atualizacoes.Atual.ToString(3),
        ["sessoes"] = Sessoes().Count(),
        ["trabalhando"] = Sessoes().Count(c => c.Trabalhando),
        ["precisam"] = Sessoes().Count(c => c.Precisa),
    });

    Local LocalDoProjeto(string? projeto)
    {
        if (projeto is null) throw new InvalidOperationException("Informe o projeto.");
        if (!Path.IsPathRooted(projeto))
            return janela.Ambientes.FirstOrDefault(a => a.Nome.Equals(projeto, StringComparison.OrdinalIgnoreCase))?.Local
                ?? throw new InvalidOperationException($"Projeto não encontrado: {projeto}. Use o caminho da pasta.");
        if (!Directory.Exists(projeto)) throw new InvalidOperationException($"A pasta não existe: {projeto}");
        if (janela.DonoDaPasta(projeto) is { } dono) return dono.Local;
        return janela.LocalDe(projeto);
    }

    JsonObject Abrir(string? projeto)
    {
        var chat = janela.CriarSessao(LocalDoProjeto(projeto));
        janela.Mostrar(chat);
        Janelas.TrazerPrincipal();
        return ServidorDoCanal.Ok(new JsonObject { ["id"] = chat.Codigo, ["pasta"] = chat.Pasta });
    }

    JsonObject Sessoes(string? projeto, bool soPrecisam)
    {
        var dono = projeto is null ? null : janela.DonoDaPasta(LocalDoProjeto(projeto).Pasta);
        var lista = Sessoes()
            .Where(c => projeto is null || (dono is not null ? janela.DonoDaPasta(c.Pasta) == dono : Workspaces.MesmaPasta(c.Pasta, projeto)))
            .Where(c => !soPrecisam || c.Precisa);
        return ServidorDoCanal.Ok(new JsonObject { ["sessoes"] = new JsonArray([.. lista.Select(Descrever)]) });
    }

    JsonObject Descrever(ChatVM c)
    {
        var o = new JsonObject
        {
            ["id"] = c.Codigo,
            ["titulo"] = c.Titulo,
            ["projeto"] = janela.DonoDaPasta(c.Pasta)?.Nome ?? Path.GetFileName(c.Pasta.TrimEnd('\\', '/')),
            ["pasta"] = c.Pasta,
            ["branch"] = c.Branch,
            ["agente"] = c.Agente,
            ["estado"] = Estado(c),
        };
        if (Pendencia(c) is { } p) o["pendencia"] = p;
        return o;
    }

    static string Estado(ChatVM c) => c.Estado switch
    {
        EstadoChat.Trabalhando => "trabalhando",
        EstadoChat.PrecisaDeVoce => "precisa de você",
        EstadoChat.Pronta => "pronta",
        EstadoChat.Erro => "erro",
        _ => "nova",
    };

    static string? Pendencia(ChatVM c) => c.PermissaoPendente is { } p ? p.Titulo : c.Precisa ? "a conversa espera uma resposta" : null;

    JsonObject Iniciar(string? projeto, string? mensagem, string? agente, string? modelo)
    {
        if (mensagem is null) return ServidorDoCanal.Falha("A mensagem está vazia.", CodigosDaCli.Uso);
        var local = LocalDoProjeto(projeto);
        if (agente is not null && Agentes.Normalizar(agente) is var normal && !normal.Equals(agente, StringComparison.OrdinalIgnoreCase))
            return ServidorDoCanal.Falha($"Agente desconhecido: {agente}. Use claude ou codex.", CodigosDaCli.Uso);
        var chat = janela.CriarSessao(local);
        if (agente is not null) chat.Agente = Agentes.Normalizar(agente);
        if (modelo is not null) chat.ModeloValor = ChatVM.ModeloDoIndice(ChatVM.IndiceDoModelo(modelo));
        chat.EnviarTexto(mensagem);
        if (!chat.Trabalhando && !chat.Precisa)
            return ServidorDoCanal.Falha($"{chat.Codigo} não aceitou a mensagem: {chat.Itens.OfType<ItemAviso>().LastOrDefault()?.Texto}");
        return ServidorDoCanal.Ok(new JsonObject { ["id"] = chat.Codigo, ["pasta"] = chat.Pasta, ["estado"] = Estado(chat) });
    }

    ChatVM Sessao(string? id) =>
        Sessoes().FirstOrDefault(c => c.Codigo == id) ?? throw new InvalidOperationException($"Sessão não encontrada: {id}. Veja nexora sessoes.");

    JsonObject Enviar(string? id, string? mensagem)
    {
        var chat = Sessao(id);
        if (mensagem is null) return ServidorDoCanal.Falha("A mensagem está vazia.", CodigosDaCli.Uso);
        chat.EnviarTexto(mensagem);
        return chat.Trabalhando || chat.Precisa
            ? ServidorDoCanal.Ok(new JsonObject { ["id"] = chat.Codigo, ["estado"] = Estado(chat) })
            : ServidorDoCanal.Falha($"{chat.Codigo} não aceitou a mensagem: {chat.Itens.OfType<ItemAviso>().LastOrDefault()?.Texto}");
    }

    JsonObject Ver(string? id, int ultimos)
    {
        var chat = Sessao(id);
        var sb = new StringBuilder();
        sb.AppendLine($"{chat.Codigo} · {chat.Titulo} · {Estado(chat)} · {chat.Pasta}");
        foreach (var item in chat.Itens.TakeLast(Math.Clamp(ultimos, 1, 60)))
            if (OrquestraVM.LinhaDoItem(item) is { } linha) sb.AppendLine(linha);
        var o = Descrever(chat);
        o["texto"] = sb.ToString().TrimEnd();
        return ServidorDoCanal.Ok(o);
    }

    async Task<JsonObject> EsperarAsync(string? id, int segundos, bool resposta, CancellationToken cancelar)
    {
        var chat = Sessao(id);
        var limite = DateTime.UtcNow.AddSeconds(Math.Clamp(segundos, 0, 600));
        while (DateTime.UtcNow < limite && chat.Trabalhando && !chat.Precisa)
        {
            try
            {
                await Task.Delay(400, cancelar);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        var o = Descrever(chat);
        if (resposta && !chat.Trabalhando && UltimaResposta(chat) is { } texto) o["resposta"] = texto;
        return ServidorDoCanal.Ok(o);
    }

    static string? UltimaResposta(ChatVM chat)
    {
        var partes = new List<string>();
        foreach (var item in chat.Itens.Reverse())
        {
            if (item is ItemUsuario) break;
            if (item is ItemTexto t && !string.IsNullOrWhiteSpace(t.Texto)) partes.Insert(0, t.Texto);
        }
        return partes.Count == 0 ? null : string.Join("\n\n", partes);
    }

    JsonObject Parar(string? id)
    {
        var chat = Sessao(id);
        chat.PararCommand.Execute(null);
        return ServidorDoCanal.Ok(new JsonObject { ["id"] = chat.Codigo });
    }
}
