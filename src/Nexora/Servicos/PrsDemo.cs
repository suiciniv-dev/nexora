using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.Servicos;

public static class PrsDemo
{
    static DateTimeOffset Ha(double minutos) => DateTimeOffset.Now.AddMinutes(-minutos);

    const string Servico = "src/Loja.Application/Cupons/CupomService.cs";

    static readonly Dictionary<string, string> Diffs = new(StringComparer.OrdinalIgnoreCase)
    {
        [Servico] = """
            diff --git a/src/Loja.Application/Cupons/CupomService.cs b/src/Loja.Application/Cupons/CupomService.cs
            @@ -24,12 +24,18 @@ public sealed class CupomService
                 public Resultado<Desconto> Aplicar(Pedido pedido, string codigo)
                 {
                     var cupom = _cupons.Buscar(codigo);
            -        if (cupom is null) throw new CupomInvalidoException(codigo);
            +        if (cupom is null)
            +            return Resultado.Falha<Desconto>("Cupom não encontrado");

                     var agora = _relogio.Agora();
            -        if (cupom.Validade < agora) throw new CupomVencidoException(codigo);
            +        if (cupom.Vencido(agora))
            +            throw new CupomVencidoException(codigo);

            +        if (pedido.TemCupom(cupom.Codigo))
            +            return Resultado.Falha<Desconto>("Este cupom já está no pedido");
            +
                     var desconto = cupom.Calcular(pedido.Subtotal);
            -        pedido.AplicarDesconto(desconto);
            +        pedido.AplicarDesconto(cupom.Codigo, desconto);
            +        _eventos.Publicar(new CupomAplicado(pedido.Id, cupom.Codigo));
                     return Resultado.Ok(desconto);
                 }
            """,
        ["src/Loja.Domain/Pedidos/Pedido.cs"] = """
            diff --git a/src/Loja.Domain/Pedidos/Pedido.cs b/src/Loja.Domain/Pedidos/Pedido.cs
            @@ -84,5 +84,8 @@ public sealed class Pedido
                 public void AplicarDesconto(string codigo, Desconto desconto)
                 {
            +        if (_cupons.Contains(codigo))
            +            return;
            +        _cupons.Add(codigo);
                     Total -= desconto.Valor;
                     Descontos.Add(desconto);
                 }
            """,
        ["tests/Loja.Application.Tests/CupomServiceTests.cs"] = """
            diff --git a/tests/Loja.Application.Tests/CupomServiceTests.cs b/tests/Loja.Application.Tests/CupomServiceTests.cs
            @@ -12,4 +12,12 @@ public class CupomServiceTests
                 [Fact]
                 public void CupomVencido_DevolveFalha()
                 {
            +        var servico = Criar(validade: Ontem);
            +        var resultado = servico.Aplicar(NovoPedido(), "NATAL10");
            +        Assert.False(resultado.Ok);
            +        Assert.Equal("Este cupom venceu", resultado.Erro);
            +    }
            +
            +    public void CupomRepetido_NaoSomaDuasVezes()
            +    {
                 }
            """,
        ["src/Loja.Api/Controllers/CheckoutController.cs"] = """
            diff --git a/src/Loja.Api/Controllers/CheckoutController.cs b/src/Loja.Api/Controllers/CheckoutController.cs
            @@ -26,7 +26,7 @@ public sealed class CheckoutController : ControllerBase
                 [HttpPost("cupom")]
            -    public IActionResult AplicarCupom(Guid p, string c)
            +    public IActionResult AplicarCupom(Guid pedidoId, string codigo)
                 {
            -        var pedido = _pedidos.Buscar(p);
            -        var resultado = _cupons.Aplicar(pedido, c);
            +        var pedido = _pedidos.Buscar(pedidoId);
            +        var resultado = _cupons.Aplicar(pedido, codigo);
                     return resultado.Ok ? Ok(resultado.Valor) : UnprocessableEntity(resultado.Erro);
                 }
            """,
    };

    public static string? DiffDoArquivo(string arquivo) => Diffs.GetValueOrDefault(arquivo.Replace('\\', '/'));

    static string Trecho(string arquivo, int linha)
    {
        if (DiffDoArquivo(arquivo) is not { } diff) return "";
        var saida = new List<string>();
        var nova = 0;
        foreach (var l in diff.Replace("\r", "").Split('\n'))
        {
            if (l.StartsWith("diff ", StringComparison.Ordinal)) continue;
            if (l.StartsWith("@@", StringComparison.Ordinal))
            {
                nova = int.Parse(l.Split(' ')[2].TrimStart('+').Split(',')[0], System.Globalization.CultureInfo.InvariantCulture);
                saida.Add(l);
                continue;
            }
            saida.Add(l);
            if (!l.StartsWith('-') && nova++ == linha) break;
        }
        return string.Join("\n", saida);
    }

    public static Core.Senna.Passagem? Passagem(string? branch) =>
        branch is { Length: > 0 } b ? new Core.Senna.Passagem($"Handoff: {b}", "Loja", DateTimeOffset.Now.AddMinutes(-1)) : null;

    static CheckDoPr Check(string fluxo, string nome, EstadoCheck estado, int execucao) =>
        new(nome, fluxo, estado, estado switch { EstadoCheck.Ok => "SUCCESS", EstadoCheck.Falhou => "FAILURE", EstadoCheck.Pulado => "SKIPPED", _ => "IN_PROGRESS" },
            $"https://github.com/voce/loja/actions/runs/{execucao}/job/{execucao + 7}", Ha(30), estado == EstadoCheck.Pendente ? null : Ha(12));

    public static DetalhePr? Detalhe(int? numero, string branch) => numero switch
    {
        52 => new DetalhePr(52, Idioma.T("Cupom vencido devolve falha em vez de exceção"), "https://github.com/voce/loja/pull/52", "OPEN")
        {
            Corpo = Idioma.T("O serviço de cupom agora devolve uma falha que a tela mostra, e o mesmo cupom não entra duas vezes no pedido.") + "\n\nCloses voce/loja#48",
            Autor = "voce",
            BaseBranch = "main",
            HeadBranch = branch,
            ShaDoHead = "4f1c2e9",
            Mergeavel = "MERGEABLE",
            EstadoDoMerge = "BLOCKED",
            Revisao = "CHANGES_REQUESTED",
            Atualizado = Ha(18),
            Checks =
            [
                Check("CI", "build", EstadoCheck.Ok, 4170),
                Check("CI", "testes (windows)", EstadoCheck.Falhou, 4171),
                Check("CI", "testes (linux)", EstadoCheck.Ok, 4172),
                Check("CI", "lint", EstadoCheck.Ok, 4173),
                Check("CodeQL", "analisar (csharp)", EstadoCheck.Pendente, 4174),
                Check("Deploy", "preview", EstadoCheck.Pulado, 4175),
            ],
            Revisoes =
            [
                new("PRR_1", "ana-dev", "CHANGES_REQUESTED", Idioma.T("Quase lá. Faltam o teste de concorrência e a mensagem do cupom vencido."), Ha(40)),
                new("PRR_2", "bruno", "COMMENTED", "", Ha(35)),
            ],
            RevisoresPedidos = ["carla"],
            Comentarios =
            [
                new("IC_1", "bruno", Idioma.T("Testei no celular e o desconto some certinho quando o cupom vence."), "https://github.com/voce/loja/pull/52#issuecomment-1", Ha(33)),
            ],
            Conversas =
            [
                new("T1", Servico, 32, false, false,
                [
                    new("C1", "ana-dev", Idioma.T("Aqui ainda lança exceção quando o cupom venceu. Devolva `Resultado.Falha(\"Este cupom venceu\")` para a tela mostrar a mensagem."), "https://github.com/voce/loja/pull/52#discussion_r1", Ha(40)),
                    new("C2", "bruno", Idioma.T("Concordo, e dá para reaproveitar a mensagem no carrinho."), "https://github.com/voce/loja/pull/52#discussion_r2", Ha(34)),
                ]) { Inicio = 31, Trecho = Trecho(Servico, 32) },
                new("T2", "src/Loja.Domain/Pedidos/Pedido.cs", 88, false, false,
                [
                    new("C3", "ana-dev", Idioma.T("Dois cliques rápidos aplicam o cupom duas vezes. Confira se o cupom já está no pedido antes de somar o desconto."), "https://github.com/voce/loja/pull/52#discussion_r3", Ha(39)),
                ]) { Trecho = Trecho("src/Loja.Domain/Pedidos/Pedido.cs", 88) },
                new("T3", "tests/Loja.Application.Tests/CupomServiceTests.cs", 17, false, false,
                [
                    new("C4", "ana-dev", Idioma.T("Falta um teste com dois pedidos ao mesmo tempo usando o mesmo cupom."), "https://github.com/voce/loja/pull/52#discussion_r4", Ha(38)),
                ]) { Trecho = Trecho("tests/Loja.Application.Tests/CupomServiceTests.cs", 17) },
                new("T4", "src/Loja.Api/Controllers/CheckoutController.cs", 27, true, false,
                [
                    new("C5", "bruno", Idioma.T("Nome do parâmetro ficou confuso."), "https://github.com/voce/loja/pull/52#discussion_r5", Ha(50)),
                ]),
                new("T5", Servico, 28, true, false,
                [
                    new("C6", "ana-dev", Idioma.T("Prefira devolver a falha a lançar exceção quando o cupom não existe."), "https://github.com/voce/loja/pull/52#discussion_r6", Ha(70)),
                    new("C7", "voce", Idioma.T("Feito."), "https://github.com/voce/loja/pull/52#discussion_r7", Ha(65)),
                ]),
                new("T6", Servico, 28, true, false,
                [
                    new("C8", "chatgpt-codex-connector[bot]", Idioma.T("A mensagem \"Cupom não encontrado\" está repetida no carrinho. Vale usar a mesma constante."), "https://github.com/voce/loja/pull/52#discussion_r8", Ha(68)),
                ]),
                new("T7", Servico, 38, true, false,
                [
                    new("C9", "claude[bot]", Idioma.T("AplicarDesconto agora recebe o código do cupom. Confira os outros lugares que chamam esse método."), "https://github.com/voce/loja/pull/52#discussion_r9", Ha(66)),
                    new("C10", "voce", Idioma.T("Só o checkout chama. Resolvido."), "https://github.com/voce/loja/pull/52#discussion_r10", Ha(60)),
                ]),
            ],
            ConversasLidas = true,
            Eu = "voce",
        },
        50 => new DetalhePr(50, Idioma.T("Tela de pedidos abre filtrada pelo dia"), "https://github.com/voce/loja/pull/50", "OPEN")
        {
            Corpo = Idioma.T("A lista de pedidos abre mostrando só os de hoje, com um botão para ver todos."),
            Autor = "voce",
            Rascunho = true,
            BaseBranch = "main",
            HeadBranch = branch,
            ShaDoHead = "9ab03d1",
            Mergeavel = "CONFLICTING",
            EstadoDoMerge = "DIRTY",
            Atualizado = Ha(55),
            Checks =
            [
                Check("CI", "build", EstadoCheck.Ok, 4100),
                Check("CI", "testes (windows)", EstadoCheck.Ok, 4101),
                Check("CI", "testes (linux)", EstadoCheck.Pendente, 4102),
            ],
            ConversasLidas = true,
            Eu = "voce",
        },
        { } n => new DetalhePr(n, Idioma.T("Notas sincronizam offline"), $"https://github.com/voce/loja/pull/{n}", "OPEN")
        {
            Autor = "voce",
            BaseBranch = "main",
            HeadBranch = branch,
            ShaDoHead = "77de0aa",
            Mergeavel = "MERGEABLE",
            EstadoDoMerge = "CLEAN",
            Revisao = "APPROVED",
            Atualizado = Ha(80),
            Checks = [Check("CI", "build", EstadoCheck.Ok, 4200), Check("CI", "testes", EstadoCheck.Ok, 4201)],
            Revisoes = [new("PRR_9", "ana-dev", "APPROVED", "", Ha(70))],
            ConversasLidas = true,
            Eu = "voce",
        },
        null => null,
    };

    public static BranchParaPr Branch(string baseBranch) => new($"origin/{baseBranch}",
        [
            new("a1", "feat(notas): sincronizar notas quando a conexão volta", Idioma.T("As notas criadas offline entram numa fila e sobem quando a internet volta.")),
            new("b2", "feat(notas): fila offline com tentativas", ""),
            new("c3", "fix: conflito ao editar a mesma nota em dois aparelhos", ""),
            new("d4", "test: sincronização e fila offline", ""),
        ],
        [
            new("src/Notas/Sincronizacao.cs", 86, 12, false, false),
            new("src/Notas/FilaOffline.cs", 64, 0, false, true),
            new("tests/Notas.Testes/SincronizacaoTestes.cs", 41, 0, false, true),
        ], 2);

    public static DetalhePr Novo(string branch, string baseBranch, string titulo, string corpo, bool rascunho) =>
        new(57, titulo, "https://github.com/voce/app-notas/pull/57", "OPEN")
        {
            Corpo = corpo,
            Autor = "voce",
            Rascunho = rascunho,
            BaseBranch = baseBranch,
            HeadBranch = branch,
            ShaDoHead = "e5f6a7b",
            Mergeavel = "MERGEABLE",
            EstadoDoMerge = rascunho ? "DRAFT" : "BLOCKED",
            Revisao = "REVIEW_REQUIRED",
            Atualizado = DateTimeOffset.Now,
            Checks = [Check("CI", "build", EstadoCheck.Pendente, 4300), Check("CI", "testes", EstadoCheck.Pendente, 4301)],
            ConversasLidas = true,
            Eu = "voce",
        };

    public static IReadOnlyList<PrLigado> Ligados(int issue) => issue switch
    {
        48 => [new PrLigado(52, "https://github.com/voce/loja/pull/52", "OPEN", false, "cupom-checkout", "voce/loja", "CHANGES_REQUESTED", "FAILURE")],
        39 => [new PrLigado(46, "https://github.com/voce/loja/pull/46", "MERGED", false, "pix-qr", "voce/loja", "APPROVED", "SUCCESS"),
               new PrLigado(53, "https://github.com/voce/loja/pull/53", "OPEN", false, "pix-qr-dinamico", "voce/loja", "APPROVED", "SUCCESS")],
        44 => [new PrLigado(54, "https://github.com/voce/loja/pull/54", "OPEN", true, "resumo-do-pedido", "voce/loja", null, "PENDING")],
        _ => [],
    };
}
