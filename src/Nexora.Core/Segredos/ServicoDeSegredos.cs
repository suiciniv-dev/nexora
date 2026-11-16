using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Orquestra;
using Nexora.Core.Texto;

namespace Nexora.Core.Segredos;

public static class FerramentaDeSegredo
{
    public const string Servidor = "nexora-segredos";
    public const string Pedir = "pedir_segredo";
    public const string NomeCompleto = "mcp__" + Servidor + "__" + Pedir;

    public static readonly IReadOnlyList<FerramentaMcp> Todas =
    [
        new(Pedir,
            "Pede ao usuário um segredo (token, chave de API, senha) por um formulário protegido do Nexora, sem passar pelo chat. Use isto sempre que precisar de um segredo; nunca peça para colarem o valor na conversa. "
            + "Você não recebe o valor: recebe só a confirmação de onde ele foi guardado. destino \"env\" grava a chave num arquivo .env do projeto (só se o arquivo estiver no .gitignore); "
            + "destino \"ambiente\" guarda o valor no cofre do sistema e o Nexora o põe como variável de ambiente nos processos que abre para este projeto (sessões, terminal, Executar). "
            + "Se a resposta disser que o usuário ainda não respondeu, chame de novo com os mesmos dados para continuar esperando.",
            new JsonObject
            {
                ["nome"] = Texto("Nome da variável ou chave, como OPENAI_API_KEY."),
                ["motivo"] = Texto("Para que o segredo vai servir, em uma frase. O usuário lê isso antes de decidir."),
                ["destino"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("env", "ambiente"), ["description"] = "Onde guardar: \"env\" (arquivo .env do projeto, ignorado pelo git) ou \"ambiente\" (variável de ambiente guardada pelo Nexora)." },
                ["arquivo"] = Texto("Com destino \"env\": caminho do arquivo relativo à pasta do projeto (padrão .env). Precisa começar com .env e estar no .gitignore."),
            },
            ["nome", "motivo", "destino"], SoLeitura: false),
    ];

    static JsonObject Texto(string descricao) => new() { ["type"] = "string", ["description"] = descricao };
}

public sealed class ServicoDeSegredos : IDisposable
{
    sealed record Dono(string Pasta, Action<PedidoDeSegredo> Mostrar);

    sealed class Pendente(PedidoDeSegredo pedido, string dono)
    {
        public PedidoDeSegredo Pedido { get; } = pedido;
        public string Dono { get; } = dono;
        public TaskCompletionSource<ResultadoFerramenta> Fim { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Salvando;
    }

    readonly ConcurrentDictionary<string, Dono> _donos = new();
    readonly ConcurrentDictionary<string, Pendente> _pendentes = new();
    readonly object _trava = new();
    ServidorMcp? _servidor;

    public ServicoDeSegredos(CofreDeSegredos cofre) => Cofre = cofre;

    public CofreDeSegredos Cofre { get; }
    public TimeSpan Espera { get; set; } = TimeSpan.FromSeconds(Ferramentas.EsperaMaxima);

    public string Registrar(string dono, string pasta, Action<PedidoDeSegredo> mostrar)
    {
        _donos[dono] = new Dono(pasta, mostrar);
        lock (_trava) _servidor ??= new ServidorMcp(FerramentaDeSegredo.Todas, ExecutarAsync, FerramentaDeSegredo.Servidor);
        return _servidor.Registrar(dono);
    }

    public string Configuracao(string chave) => _servidor?.Configuracao(chave) ?? "";

    public void Esquecer(string dono, string? chave)
    {
        _donos.TryRemove(dono, out _);
        if (chave is not null) _servidor?.Esquecer(chave);
        foreach (var p in _pendentes.Values.Where(p => p.Dono == dono).ToList()) Cancelar(p.Pedido.Id, "A sessão foi fechada no Nexora antes da resposta.");
    }

    public bool Aberto(string pedidoId) => _pendentes.TryGetValue(pedidoId, out var p) && !p.Fim.Task.IsCompleted;

    public Task<ResultadoFerramenta> ChamarAsync(string dono, JsonElement argumentos, CancellationToken cancelar) =>
        ExecutarAsync(dono, FerramentaDeSegredo.Pedir, argumentos, cancelar);

    async Task<ResultadoFerramenta> ExecutarAsync(string dono, string ferramenta, JsonElement argumentos, CancellationToken cancelar)
    {
        if (ferramenta != FerramentaDeSegredo.Pedir) return new ResultadoFerramenta($"Ferramenta desconhecida: {ferramenta}", Erro: true);
        if (!_donos.TryGetValue(dono, out var quem)) return new ResultadoFerramenta("Esta sessão não está mais aberta no Nexora.", Erro: true);
        var (pedido, erro) = await LeituraDoPedido.LerAsync(argumentos, quem.Pasta);
        if (pedido is null) return new ResultadoFerramenta(erro ?? "Pedido inválido.", Erro: true);
        if (pedido.Destino == DestinoDoSegredo.Variavel && !Cofre.GuardaVariaveis)
            return new ResultadoFerramenta("Este sistema não tem cofre para guardar variáveis pelo Nexora. Use destino \"env\".", Erro: true);
        var pendente = _pendentes.Values.FirstOrDefault(p => p.Dono == dono && p.Pedido.MesmoPedido(pedido));
        if (pendente is null)
        {
            pendente = new Pendente(pedido, dono);
            _pendentes[pedido.Id] = pendente;
            quem.Mostrar(pedido);
        }
        try
        {
            var resultado = await pendente.Fim.Task.WaitAsync(Espera, cancelar);
            _pendentes.TryRemove(pendente.Pedido.Id, out _);
            return resultado;
        }
        catch (TimeoutException)
        {
            return new ResultadoFerramenta($"O usuário ainda não respondeu ao pedido de {pedido.Nome}. O formulário continua aberto no Nexora; chame pedir_segredo de novo com os mesmos dados para continuar esperando. Não peça o valor no chat.");
        }
    }

    public async Task<string?> SalvarAsync(string pedidoId, string valor)
    {
        if (!_pendentes.TryGetValue(pedidoId, out var p) || p.Fim.Task.IsCompleted) return Idioma.T("Este pedido já foi respondido.");
        if (string.IsNullOrEmpty(valor)) return Idioma.T("Digite o valor ou recuse o pedido.");
        if (Interlocked.Exchange(ref p.Salvando, 1) == 1) return null;
        try
        {
            var pedido = p.Pedido;
            string texto;
            if (pedido.Destino == DestinoDoSegredo.ArquivoEnv)
            {
                if (await ArquivoEnv.ProblemaAsync(pedido.Pasta, pedido.Arquivo!) is { } problema) return problema;
                ArquivoEnv.Gravar(pedido.Caminho!, pedido.Nome, valor);
                Cofre.RegistrarArquivo(pedido.Nome, pedido.Pasta, pedido.Arquivo!);
                texto = $"Guardado em {pedido.Arquivo} como {pedido.Nome} (o arquivo está no .gitignore). Você não recebeu o valor: use-o pelo programa que lê o {pedido.Arquivo}, sem imprimir, copiar ou registrar o conteúdo.";
            }
            else
            {
                Cofre.GuardarVariavel(pedido.Nome, pedido.Pasta, valor);
                texto = $"Guardado pelo Nexora como a variável de ambiente {pedido.Nome} deste projeto. Ela entra nos processos que o Nexora abrir para o projeto (sessões novas, terminal e Executar). "
                    + "Esta sessão recebe a variável depois que o turno terminar, quando o Nexora a reinicia com o mesmo histórico. Não imprima o valor.";
            }
            p.Fim.TrySetResult(new ResultadoFerramenta(texto));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or PlatformNotSupportedException or InvalidOperationException)
        {
            return ex.Message;
        }
        finally
        {
            Interlocked.Exchange(ref p.Salvando, 0);
        }
    }

    public void Recusar(string pedidoId) =>
        Cancelar(pedidoId, "O usuário recusou o pedido. Não peça o valor no chat; siga sem ele ou pergunte ao usuário como prefere continuar.");

    public void Cancelar(string pedidoId, string motivo)
    {
        if (_pendentes.TryGetValue(pedidoId, out var p)) p.Fim.TrySetResult(new ResultadoFerramenta(motivo));
    }

    public void Dispose() => _servidor?.Dispose();
}
