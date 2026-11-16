using System.Text;
using System.Text.Json;

namespace Nexora.Core.Historico;

public static class TranscricaoDeExemplo
{
    static readonly string[] Ferramentas = ["Read", "Grep", "Read", "Edit", "Bash", "Read"];

    public static void Escrever(string arquivo, int turnos, string sessao = "longa", int tamanhoDoResultado = 1500)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(arquivo) ?? ".");
        var quando = DateTimeOffset.Now.AddHours(-turnos);
        var resultado = string.Join('\n', Enumerable.Range(1, Math.Max(1, tamanhoDoResultado / 60)).Select(i => $"    linha {i:000} do arquivo com algum código de exemplo;"));
        using var saida = new StreamWriter(arquivo, false, new UTF8Encoding(false));
        var id = 0;
        for (var t = 1; t <= turnos; t++)
        {
            quando = quando.AddMinutes(3);
            Linha(saida, new
            {
                type = "user",
                sessionId = sessao,
                timestamp = quando,
                message = new { role = "user", content = $"Pedido número {t}: ajuste o módulo {t % 7} e rode os testes." },
            });
            Linha(saida, Assistente(sessao, quando, $"msg_{t}_a", new object[] { new { type = "text", text = $"Vou olhar o módulo {t % 7} antes de mexer." } }));
            foreach (var nome in Ferramentas)
            {
                id++;
                var entrada = nome switch
                {
                    "Read" => (object)new { file_path = $@"D:\Projetos\Loja\src\Modulo{t % 7}\Arquivo{id % 13}.cs" },
                    "Grep" => new { pattern = $"Metodo{id % 5}", path = "src" },
                    "Edit" => new { file_path = $@"D:\Projetos\Loja\src\Modulo{t % 7}\Arquivo{id % 13}.cs", old_string = "antes", new_string = "depois" },
                    _ => new { command = "dotnet test tests/Loja.Testes" },
                };
                Linha(saida, Assistente(sessao, quando, $"msg_{t}_{id}", new object[] { new { type = "tool_use", id = $"toolu_{id}", name = nome, input = entrada } }));
                Linha(saida, new
                {
                    type = "user",
                    sessionId = sessao,
                    timestamp = quando,
                    message = new { role = "user", content = new object[] { new { type = "tool_result", tool_use_id = $"toolu_{id}", content = resultado, is_error = false } } },
                });
            }
            Linha(saida, Assistente(sessao, quando, $"msg_{t}_z", new object[] { new { type = "text", text = Resposta(t) } }));
        }
    }

    static object Assistente(string sessao, DateTimeOffset quando, string id, object[] conteudo) => new
    {
        type = "assistant",
        sessionId = sessao,
        timestamp = quando,
        message = new
        {
            id,
            role = "assistant",
            model = "claude-opus-5-5",
            content = conteudo,
            usage = new { input_tokens = 12, cache_read_input_tokens = 40_000, cache_creation_input_tokens = 800, output_tokens = 300 },
        },
    };

    static string Resposta(int turno) => $"""
        Pronto, o turno {turno} terminou. O que mudou:

        - **Validação:** o módulo agora recusa entradas vazias e devolve uma falha legível.
        - **Testes:** dois casos novos cobrem a entrada vazia e a entrada repetida.
        - **Limpeza:** tirei um `using` que sobrou e um método que ninguém chamava.

        ```csharp
        if (string.IsNullOrWhiteSpace(entrada))
            return Resultado.Falha("Entrada vazia");
        ```

        Todos os testes passaram.
        """;

    static void Linha(StreamWriter saida, object linha) => saida.Write(JsonSerializer.Serialize(linha) + "\n");
}
