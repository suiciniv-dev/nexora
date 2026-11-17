using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Nexora.Aplicativo;
using Nexora.Core.Claude;
using Nexora.Core.Servidor;
using Nexora.Modelos;
using Nexora.ViewModels;
using Avalonia.Threading;

namespace Nexora.Testes.Tela;

[Collection("Orquestrador")]
public sealed class AgenteNoServidorTestes : IDisposable
{
    readonly string? _simuladoAntes = SessaoClaude.Simulado;
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-agente-servidor-").FullName;

    public AgenteNoServidorTestes() => SessaoClaude.Simulado = AgenteDeTeste.Dll;

    public void Dispose()
    {
        SessaoClaude.Simulado = _simuladoAntes;
        try
        {
            Directory.Delete(_pasta, true);
        }
        catch (IOException)
        {
        }
    }

    static async Task<T> Esperar<T>(ConcurrentQueue<EventoClaude> eventos, string oQue) where T : EventoClaude
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < limite)
        {
            if (eventos.OfType<T>().FirstOrDefault() is { } achado) return achado;
            await Task.Delay(25);
        }
        throw new TimeoutException(oQue + ": " + string.Join(", ", eventos.Select(e => e.GetType().Name)));
    }

    [Fact]
    public async Task Agente_de_verdade_segue_no_servidor_depois_que_o_app_fecha_e_o_proximo_responde_a_permissao()
    {
        var cano = "nexora-teste-" + Guid.NewGuid().ToString("N");
        using var fim = new CancellationTokenSource();
        var sessoes = new SessoesDoServidor(FabricaDeSessoes.Criar);
        var servidor = Task.Run(() => ServidorDoNucleo.RodarAsync(cano, NucleoLocal.Instancia, TimeSpan.FromSeconds(20), fim.Token, sessoes: sessoes));
        try
        {
            var primeiro = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(10)) ?? throw new InvalidOperationException("Sem servidor.");
            var sessao = new SessaoRemota(primeiro, Agentes.Claude, new OpcoesSessao(_pasta), "Testes");
            var antes = new ConcurrentQueue<EventoClaude>();
            sessao.Evento += antes.Enqueue;
            sessao.Iniciar();
            sessao.Enviar("permissao dotnet test | Roda os testes");
            var pedido = await Esperar<PermissaoPedida>(antes, "pedido de permissão");
            Assert.Equal("Bash", pedido.Ferramenta);
            Assert.NotNull(sessao.SessaoId);
            var pid = sessao.Pid;
            sessao.Soltar();
            primeiro.Dispose();

            await Task.Delay(500);
            using var segundo = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(10)) ?? throw new InvalidOperationException("Sem servidor.");
            var resumo = Assert.Single(await segundo.ListarSessoesAsync());
            Assert.True(resumo.Ativa);
            Assert.True(resumo.Trabalhando);
            Assert.Equal(sessao.SessaoId, resumo.SessaoId);
            Assert.Equal(pid, resumo.Pid);
            var anexada = SessaoRemota.Anexar(segundo, resumo);
            var depois = new ConcurrentQueue<EventoClaude>();
            anexada.Evento += depois.Enqueue;
            var estado = await anexada.AssinarAsync();
            Assert.NotNull(estado);
            var pendente = Assert.IsType<PermissaoPedida>(Assert.Single(estado.Pendentes));
            Assert.Equal(pedido.PedidoId, pendente.PedidoId);

            anexada.Permitir(pendente.PedidoId, new JsonObject { ["command"] = "dotnet test" });
            var texto = await Esperar<TextoCompleto>(depois, "resposta depois da permissão");
            await Esperar<TurnoEncerrado>(depois, "fim do turno");

            Assert.Equal("permitido", texto.Texto);
            Assert.False(Assert.Single(await segundo.ListarSessoesAsync()).Trabalhando);
            var encerrou = new TaskCompletionSource<int>();
            anexada.Encerrou += (codigo, _) => encerrou.TrySetResult(codigo);
            anexada.Encerrar();
            await encerrou.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(anexada.Ativa);
        }
        finally
        {
            fim.Cancel();
            await servidor.WaitAsync(TimeSpan.FromSeconds(15));
            await sessoes.EncerrarTodasAsync();
        }
    }

    [Fact]
    public Task Aba_reaberta_de_uma_sessao_viva_mostra_a_permissao_pendente_e_continua_o_turno() => Sessao.Rodar(async () =>
    {
        var cano = "nexora-teste-" + Guid.NewGuid().ToString("N");
        using var fim = new CancellationTokenSource();
        var sessoes = new SessoesDoServidor(FabricaDeSessoes.Criar);
        var servidor = Task.Run(() => ServidorDoNucleo.RodarAsync(cano, NucleoLocal.Instancia, TimeSpan.FromSeconds(20), fim.Token, sessoes: sessoes));
        ChatVM? chat = null;
        try
        {
            using (var primeiro = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(10)) ?? throw new InvalidOperationException("Sem servidor."))
            {
                var sessao = new SessaoRemota(primeiro, Agentes.Claude, new OpcoesSessao(_pasta), "Testes");
                var antes = new ConcurrentQueue<EventoClaude>();
                sessao.Evento += antes.Enqueue;
                sessao.Iniciar();
                sessao.Enviar("permissao dotnet test | Roda os testes");
                await Esperar<PermissaoPedida>(antes, "pedido de permissão");
                sessao.Soltar();
            }
            using var segundo = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(10)) ?? throw new InvalidOperationException("Sem servidor.");
            var resumo = Assert.Single(await segundo.ListarSessoesAsync());
            var janela = new JanelaVM();
            chat = ChatVM.DeSessaoViva(SessaoRemota.Anexar(segundo, resumo), resumo, null, new Local("Testes", _pasta, []), janela, _ => { }, _ => { });
            await Ate(() => chat.Itens.OfType<ItemPermissao>().Any(), "permissão na aba");

            Assert.Equal(EstadoChat.PrecisaDeVoce, chat.Estado);
            Assert.Equal(resumo.Id, chat.IdNoServidor);
            Assert.Equal(resumo.SessaoId, chat.SessaoId);
            chat.Itens.OfType<ItemPermissao>().Single().PermitirCommand.Execute(null);
            await Ate(() => chat.Itens.OfType<ItemTexto>().Any(t => t.Texto == "permitido") && chat.Estado == EstadoChat.Pronta, "turno terminado");
        }
        finally
        {
            chat?.Encerrar(esperar: true);
            fim.Cancel();
            await servidor.WaitAsync(TimeSpan.FromSeconds(15));
            await sessoes.EncerrarTodasAsync();
        }
    });

    static async Task Ate(Func<bool> condicao, string oQue)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (!condicao())
        {
            if (DateTime.UtcNow > limite) throw new TimeoutException(oQue);
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
    }
}
