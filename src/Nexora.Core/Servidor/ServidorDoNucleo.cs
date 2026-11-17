using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace Nexora.Core.Servidor;

public static class ServidorDoNucleo
{
    public const string Argumento = "--servidor";
    public const int OutroServidor = 2;

    internal static JsonSerializerOptions Json => JsonDoNucleo.Opcoes;

    public static string NomeDoCano(string usuario, string pasta, Guid versao)
    {
        var pastaNormal = Path.GetFullPath(pasta).TrimEnd('\\', '/').ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{usuario.ToLowerInvariant()}|{pastaNormal}|{versao:N}"));
        return "nexora-nucleo-" + Convert.ToHexString(hash, 0, 12).ToLowerInvariant();
    }

    public static async Task<int> RodarAsync(string nome, INucleo nucleo, TimeSpan ocioso, CancellationToken cancelar = default, Func<bool>? ocupado = null, SessoesDoServidor? sessoes = null)
    {
        NamedPipeServerStream proximo;
        try
        {
            proximo = Abrir(nome, primeiro: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return OutroServidor;
        }
        using var parar = CancellationTokenSource.CreateLinkedTokenSource(cancelar);
        var conectados = 0;
        var sozinhoDesde = Environment.TickCount64;
        var atendeu = false;
        var clientes = new List<Task>();
        var vigia = Vigiar();
        try
        {
            while (true)
            {
                try
                {
                    await proximo.WaitForConnectionAsync(parar.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                var cano = proximo;
                Interlocked.Increment(ref conectados);
                atendeu = true;
                clientes.RemoveAll(t => t.IsCompleted);
                clientes.Add(Task.Run(async () =>
                {
                    try
                    {
                        await AtenderAsync(cano, nucleo, sessoes, parar.Token).ConfigureAwait(false);
                    }
                    finally
                    {
                        await cano.DisposeAsync().ConfigureAwait(false);
                        Volatile.Write(ref sozinhoDesde, Environment.TickCount64);
                        Interlocked.Decrement(ref conectados);
                    }
                }, CancellationToken.None));
                proximo = Abrir(nome, primeiro: false);
            }
        }
        finally
        {
            await proximo.DisposeAsync().ConfigureAwait(false);
            parar.Cancel();
        }
        await Task.WhenAll(clientes).ConfigureAwait(false);
        await vigia.ConfigureAwait(false);
        return atendeu ? 0 : 1;

        async Task Vigiar()
        {
            var intervalo = TimeSpan.FromMilliseconds(Math.Clamp(ocioso.TotalMilliseconds / 4, 10, 1000));
            try
            {
                while (true)
                {
                    await Task.Delay(intervalo, parar.Token).ConfigureAwait(false);
                    sessoes?.Arrumar();
                    if (Volatile.Read(ref conectados) > 0) continue;
                    if (ocupado?.Invoke() == true || sessoes?.Ocupado == true)
                    {
                        Volatile.Write(ref sozinhoDesde, Environment.TickCount64);
                        continue;
                    }
                    if (Environment.TickCount64 - Volatile.Read(ref sozinhoDesde) < ocioso.TotalMilliseconds) continue;
                    parar.Cancel();
                    return;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    static NamedPipeServerStream Abrir(string nome, bool primeiro)
    {
        var opcoes = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;
        if (primeiro && OperatingSystem.IsWindows()) opcoes |= PipeOptions.FirstPipeInstance;
        return new NamedPipeServerStream(nome, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, opcoes);
    }

    static async Task AtenderAsync(NamedPipeServerStream cano, INucleo nucleo, SessoesDoServidor? sessoes, CancellationToken cancelar)
    {
        var leitor = new StreamReader(cano, new UTF8Encoding(false));
        var escritor = new StreamWriter(cano, new UTF8Encoding(false));
        var saida = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        var assinaturas = new Dictionary<string, IDisposable>();
        var escrita = Task.Run(async () =>
        {
            try
            {
                while (await saida.Reader.WaitToReadAsync(cancelar).ConfigureAwait(false))
                {
                    while (saida.Reader.TryRead(out var linha)) await escritor.WriteLineAsync(linha.AsMemory(), cancelar).ConfigureAwait(false);
                    await escritor.FlushAsync(cancelar).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
            {
            }
        }, CancellationToken.None);
        void Responder(JsonObject resposta) => saida.Writer.TryWrite(resposta.ToJsonString());
        SessoesDoServidor Registro() => sessoes ?? throw new InvalidOperationException("Este servidor não guarda sessões.");
        SessaoNoServidor Sessao(JsonObject? dados) =>
            Registro().Achar((string?)dados?["id"] ?? "") ?? throw new InvalidOperationException($"Sessão não encontrada: {(string?)dados?["id"]}");
        async Task<object> Resolver(string metodo, JsonObject? dados) => metodo switch
        {
            MetodosDoNucleo.Conversas => await nucleo.ListarConversasAsync(cancelar).ConfigureAwait(false),
            MetodosDoNucleo.Uso => await nucleo.LerUsoAsync((string?)dados?["cache"], dados!["desde"]!.GetValue<DateTime>(), dados["porDia"]!.GetValue<bool>(), cancelar).ConfigureAwait(false),
            MetodosDoNucleo.Andamento => await nucleo.LerAndamentoAsync(dados!["arquivo"]!.GetValue<string>(), (string?)dados["pasta"], cancelar).ConfigureAwait(false),
            MetodosDoNucleo.Turnos => await nucleo.LerTurnosAsync(dados!["arquivo"]!.GetValue<string>(), dados["turnos"]!.GetValue<int>(), dados["ate"]!.GetValue<long>(), cancelar).ConfigureAwait(false),
            MetodosDoNucleo.Modo => new RespostaDeTexto(await nucleo.UltimoModoAsync(dados!["arquivo"]!.GetValue<string>(), cancelar).ConfigureAwait(false)),
            MetodosDoNucleo.Comando => await nucleo.RodarComandoAsync(dados.Deserialize<Git.PedidoDeComando>(Json)!, cancelar).ConfigureAwait(false),
            MetodosDoNucleo.AbrirSessao => Registro().Abrir(dados!["id"]!.GetValue<string>(), dados["agente"]!.GetValue<string>(), (string?)dados["titulo"], dados["opcoes"].Deserialize<Claude.OpcoesSessao>(Json)!).Resumo(),
            MetodosDoNucleo.AssinarSessao => Assinar(Sessao(dados), dados?["desde"]?.GetValue<long>()),
            MetodosDoNucleo.SoltarSessao => Soltar((string?)dados?["id"] ?? ""),
            MetodosDoNucleo.EnviarASessao => Enviar(Sessao(dados), dados!),
            MetodosDoNucleo.AcaoNaSessao => Agir(Sessao(dados), dados!),
            MetodosDoNucleo.PedirASessao => await Sessao(dados).Sessao.PedirAsync(dados!["pedido"]!.DeepClone().AsObject(), dados["segundos"]?.GetValue<double>() is { } segundos ? TimeSpan.FromSeconds(segundos) : null).ConfigureAwait(false),
            MetodosDoNucleo.RemotoDaSessao => await Sessao(dados).Sessao.LigarRemotoAsync(dados!["ligado"]!.GetValue<bool>()).ConfigureAwait(false),
            MetodosDoNucleo.HistoricoDaSessao => Sessao(dados).Sessao is Claude.IHistoricoDoAgente historico
                ? new List<Claude.EventoClaude>(await historico.LerHistoricoAsync().ConfigureAwait(false))
                : new List<Claude.EventoClaude>(),
            MetodosDoNucleo.ListarSessoes => sessoes?.Listar() ?? [],
            MetodosDoNucleo.EsquecerSessao => Esquecer((string?)dados?["id"] ?? ""),
            MetodosDoNucleo.LigarHub => Registro().LigarHub(dados!["pasta"]!.GetValue<string>(), TimeSpan.FromMinutes(Math.Max(1, dados["minutos"]?.GetValue<double>() ?? 10))),
            MetodosDoNucleo.RegistrarNoHub => Registro().HubLigado().Registrar(dados!["pasta"]!.GetValue<string>(), (string?)dados["rotulo"] ?? "", (string?)dados["casa"]),
            MetodosDoNucleo.RenomearNoHub => Feito(() => Registro().HubLigado().Renomear(dados!["chave"]!.GetValue<string>(), (string?)dados["rotulo"] ?? "")),
            MetodosDoNucleo.EsquecerNoHub => await FeitoAsync(Registro().HubLigado().EsquecerAsync(dados!["chave"]!.GetValue<string>())).ConfigureAwait(false),
            MetodosDoNucleo.PararNoHub => await FeitoAsync(Registro().HubLigado().PararAsync(dados!["instancia"]!.GetValue<string>())).ConfigureAwait(false),
            MetodosDoNucleo.EstadoDoHub => Registro().Hub?.Estado() ?? [],
            MetodosDoNucleo.PidsDoHub => Registro().Hub?.Pids() ?? [],
            var outro => throw new InvalidOperationException($"Método desconhecido: {outro}"),
        };
        RespostaDeAssinatura Assinar(SessaoNoServidor sessao, long? desde)
        {
            lock (assinaturas)
            {
                if (assinaturas.Remove(sessao.Id, out var antiga)) antiga.Dispose();
                var id = sessao.Id;
                var (completo, ultimo, assinatura) = sessao.Assinar(desde, e => saida.Writer.TryWrite(new JsonObject
                {
                    ["sessao"] = id,
                    ["numero"] = e.Numero,
                    ["evento"] = JsonSerializer.SerializeToNode(e.Evento, Json),
                }.ToJsonString()));
                assinaturas[sessao.Id] = assinatura;
                var resumo = sessao.Resumo();
                return new RespostaDeAssinatura(completo, ultimo, resumo.Trabalhando, resumo.Ativa, sessao.Pendentes());
            }
        }
        static RespostaVazia Feito(Action acao)
        {
            acao();
            return new RespostaVazia();
        }
        static async Task<RespostaVazia> FeitoAsync(Task tarefa)
        {
            await tarefa.ConfigureAwait(false);
            return new RespostaVazia();
        }
        void AvisarHub() => saida.Writer.TryWrite(new JsonObject
        {
            ["aviso"] = AvisosDoNucleo.Hub,
            ["contagem"] = JsonSerializer.SerializeToNode(sessoes?.ContagemDoHub(), Json),
        }.ToJsonString());
        if (sessoes is not null) sessoes.HubMudou += AvisarHub;
        RespostaVazia Soltar(string id)
        {
            lock (assinaturas)
                if (assinaturas.Remove(id, out var assinatura)) assinatura.Dispose();
            return new RespostaVazia();
        }
        RespostaVazia Esquecer(string id)
        {
            Soltar(id);
            sessoes?.Esquecer(id);
            return new RespostaVazia();
        }
        static RespostaVazia Enviar(SessaoNoServidor sessao, JsonObject dados)
        {
            sessao.Enviar(dados["texto"]?.GetValue<string>() ?? "", dados["imagens"].Deserialize<List<Claude.Anexo>>(Json));
            return new RespostaVazia();
        }
        static RespostaVazia Agir(SessaoNoServidor sessao, JsonObject dados)
        {
            sessao.Aplicar(dados["acao"]!.GetValue<string>(), dados["dados"] as JsonObject ?? []);
            return new RespostaVazia();
        }
        async Task Atender(long id, string metodo, JsonObject? dados)
        {
            JsonObject resposta;
            try
            {
                var resultado = await Resolver(metodo, dados).ConfigureAwait(false);
                resposta = new JsonObject { ["id"] = id, ["dados"] = JsonSerializer.SerializeToNode(resultado, resultado.GetType(), Json) };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                resposta = new JsonObject { ["id"] = id, ["erro"] = ex.Message };
            }
            Responder(resposta);
        }
        var emAndamento = new List<Task>();
        try
        {
            while (await leitor.ReadLineAsync(cancelar).ConfigureAwait(false) is { } linha)
            {
                long id = 0;
                string metodo;
                JsonObject? dados;
                try
                {
                    var pedido = JsonNode.Parse(linha)!.AsObject();
                    id = pedido["id"]!.GetValue<long>();
                    metodo = pedido["metodo"]!.GetValue<string>();
                    dados = pedido["dados"] as JsonObject;
                }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException or NullReferenceException or FormatException)
                {
                    Responder(new JsonObject { ["id"] = id, ["erro"] = ex.Message });
                    continue;
                }
                emAndamento.RemoveAll(t => t.IsCompleted);
                if (MetodosDoNucleo.EmOrdem(metodo)) await Atender(id, metodo, dados).ConfigureAwait(false);
                else emAndamento.Add(Task.Run(() => Atender(id, metodo, dados), cancelar));
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
        }
        if (sessoes is not null) sessoes.HubMudou -= AvisarHub;
        lock (assinaturas)
        {
            foreach (var assinatura in assinaturas.Values) assinatura.Dispose();
            assinaturas.Clear();
        }
        saida.Writer.TryComplete();
        await escrita.ConfigureAwait(false);
    }
}
