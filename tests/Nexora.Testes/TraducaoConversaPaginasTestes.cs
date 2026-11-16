using System.Reflection;
using System.Text.Json;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.Core.Texto;
using Nexora.Testes.Tela;
using Nexora.Traducao;
using Nexora.ViewModels;

namespace Nexora.Testes;

public sealed partial class TraducaoTestes
{
    static IEnumerable<(string Pt, string En)> Area(string nome) =>
        (IEnumerable<(string Pt, string En)>)typeof(Ingles).GetField(nome, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    [Theory]
    [InlineData("Conversa")]
    [InlineData("Paginas")]
    public void CadaTraducaoDaAreaTemUsoNoCodigoOuNoXaml(string area)
    {
        var usados = Usados().Select(u => u.Texto).ToHashSet(StringComparer.Ordinal);
        var pares = Area(area).ToList();
        Assert.True(pares.Count > 100, $"A área {area} veio quase vazia.");
        var soltas = pares.Select(p => p.Pt).Where(pt => !usados.Contains(pt)).ToList();
        Assert.True(soltas.Count == 0, "Traduções que nada usa (talvez um erro de digitação na chave):\n" + string.Join("\n", soltas));
    }

    [Fact]
    public void InglesDaConversaNaoFicaIgualAoPortuguesPorEngano()
    {
        var iguais = Area("Conversa").Concat(Area("Paginas")).Where(p => p.Pt == p.En).Select(p => p.Pt).ToList();
        var permitidos = new[] { "Handoffs", "Subagent", "subagent", "Subagent {0}: {1}", "Subagent {0}: {1}  {2}", "{0} subagents", "{0} tokens", "{0} claude, {1}: {2}", "{0} claude, {1}", "Interface", "interface", "1 item", "{0}, {1}, {2}", "{0}, heap {1}, {2:N0} handles" };
        Assert.True(iguais.Except(permitidos).Count() == 0, "Traduções iguais ao português:\n" + string.Join("\n", iguais.Except(permitidos)));
    }

    [Fact]
    public Task ConversaEmInglesTraduzEstadosPermissoesECartaoDeSubagent() => Sessao.Rodar(() =>
    {
        var pasta = Directory.CreateTempSubdirectory("nexora-idioma-").FullName;
        try
        {
            Idioma.Definir("en");
            var chat = ChatVM.Nova(new Local("Shop", pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
            Assert.Equal("New chat", chat.Titulo);
            Assert.Equal("New", chat.EstadoTexto);
            chat.Estado = EstadoChat.Pronta;
            Assert.Equal("Ready", chat.EstadoTexto);
            chat.Estado = EstadoChat.PrecisaDeVoce;
            Assert.Equal("Needs you", chat.EstadoTexto);
            Assert.Equal("Default mode", chat.Modos[0]);
            Assert.Equal("Plan", chat.Modos[4]);

            var pedido = new PermissaoPedida("p1", "Bash", JsonDocument.Parse("{\"command\":\"ls\"}").RootElement, null, null, null);
            var permissao = new ItemPermissao(pedido, null, (_, _) => { });
            Assert.Equal("Allow", permissao.RotuloPermitir);
            Assert.Equal("Deny", permissao.RotuloNegar);
            Assert.Equal("Allow Bash?", permissao.Titulo);

            var plano = new ItemPermissao(new PermissaoPedida("p2", "ExitPlanMode", JsonDocument.Parse("{\"plan\":\"x\"}").RootElement, null, null, null), null, (_, _) => { });
            Assert.Equal("Approve plan", plano.RotuloPermitir);
            Assert.Equal("Keep planning", plano.RotuloNegar);

            var agente = new ItemSubagente("a1", JsonDocument.Parse("{\"subagent_type\":\"Explore\",\"description\":\"Map the code\"}").RootElement) { Estado = EstadoSubagente.Rodando };
            Assert.Equal("running", agente.EstadoTexto);
            Assert.Equal("no tools", agente.TextoFerramentas);
            agente.Estado = EstadoSubagente.Erro;
            Assert.Equal("failed", agente.EstadoTexto);

            var pensamento = new ItemPensamento(DateTime.Now.AddSeconds(-7));
            pensamento.Encerrar();
            Assert.StartsWith("Thought for", pensamento.Fase);
            Assert.Equal("QUEUED (0) · SENT ONE AT A TIME WHEN CLAUDE FINISHES", chat.TextoFila);
        }
        finally
        {
            Idioma.Definir("pt");
            Directory.Delete(pasta, true);
        }
    });

    [Fact]
    public Task ConversaEmPortuguesContinuaIgual() => Sessao.Rodar(() =>
    {
        var pasta = Directory.CreateTempSubdirectory("nexora-idioma-").FullName;
        try
        {
            Idioma.Definir("pt");
            var chat = ChatVM.Nova(new Local("Loja", pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
            Assert.Equal("Nova conversa", chat.Titulo);
            Assert.Equal("Nova", chat.EstadoTexto);
            Assert.Equal("Modo padrão", chat.Modos[0]);
            var pedido = new PermissaoPedida("p1", "Bash", JsonDocument.Parse("{\"command\":\"ls\"}").RootElement, null, null, null);
            var permissao = new ItemPermissao(pedido, null, (_, _) => { });
            Assert.Equal("Permitir", permissao.RotuloPermitir);
            Assert.Equal("Negar", permissao.RotuloNegar);
            Assert.Equal("Permitir Bash?", permissao.Titulo);
        }
        finally
        {
            Directory.Delete(pasta, true);
        }
    });

    [Fact]
    public void PaginasEmInglesUsamCulturaEGramaticaAmericanas()
    {
        try
        {
            Idioma.Definir("en");
            Assert.Equal("1.5 GB", DesempenhoVM.Ram(1.5 * (1L << 30)));
            Assert.Equal("8.5%", DesempenhoVM.Pct(8.5));
            Assert.Equal("Good morning", IssuesVM.Saudacao(new DateTime(2026, 11, 5, 8, 0, 0)));
            Assert.Equal("Good afternoon claude, PR: https://x/pull/9", IssuesVM.Rascunho("https://x/pull/9", true, "PR", "  ", new DateTime(2026, 11, 5, 15, 0, 0)));
            Assert.Equal("Nov 5", new DateTime(2026, 11, 5).ToString(Idioma.T("d 'de' MMM"), Idioma.Cultura));
            Assert.Equal("11/5", new DateTime(2026, 11, 5).ToString(Idioma.T("d/M"), Idioma.Cultura));
            Assert.Equal("1,515 replies", Idioma.F("{0:N0} respostas", 1515));
            Assert.Equal("1 open issue in 1 repository", Idioma.F("{0} em {1}", Idioma.T("1 issue aberta"), Idioma.T("1 repositório")));
        }
        finally
        {
            Idioma.Definir("pt");
        }
        Assert.Equal("1,5 GB", DesempenhoVM.Ram(1.5 * (1L << 30)));
        Assert.StartsWith("5 de nov", new DateTime(2026, 11, 5).ToString(Idioma.T("d 'de' MMM"), Idioma.Cultura));
        Assert.Equal("1.515 respostas", Idioma.F("{0:N0} respostas", 1515));
    }
}
