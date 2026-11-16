using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Claude;
using Nexora.Core.Orquestra;

namespace Nexora.Core.Navegador;

public static class FerramentasDoNavegador
{
    public const string Servidor = "nexora-navegador";
    public const string AbrirNavegador = "abrir_navegador";
    public const string Prefixo = "mcp__" + Servidor + "__";

    public static readonly IReadOnlyList<FerramentaMcp> Todas =
    [
        new(AbrirNavegador,
            "Abre uma página no navegador embutido do Nexora, na lateral desta conversa, para o usuário ver o app que você está fazendo (por exemplo o servidor de desenvolvimento em localhost:5173). Lá ele pode anotar elementos da página e mandar as anotações para você. Não devolve o conteúdo da página.",
            new JsonObject
            {
                ["url"] = new JsonObject { ["type"] = "string", ["description"] = "Endereço da página: localhost:5173, 5173, http://localhost:3000/rota ou https://site.com." },
            },
            ["url"], SoLeitura: true),
    ];

    public static readonly IReadOnlyList<string> Livres = [.. Todas.Select(f => Prefixo + f.Nome)];

    public static async Task<ResultadoFerramenta> ExecutarAsync(string ferramenta, JsonElement argumentos, Func<string, Task<bool>> abrir)
    {
        if (ferramenta != AbrirNavegador) return new ResultadoFerramenta($"Ferramenta desconhecida: {ferramenta}", Erro: true);
        if (Enderecos.Normalizar(Protocolo.Texto(argumentos, "url")) is not { } url)
            return new ResultadoFerramenta("Endereço inválido. Use algo como localhost:5173 ou https://site.com.", Erro: true);
        return await abrir(url)
            ? new ResultadoFerramenta($"Abri {url} no navegador do Nexora, na lateral da conversa. Se a página não carregar, o usuário vê o erro lá.")
            : new ResultadoFerramenta("A conversa não está aberta no Nexora agora, então o navegador não abriu.", Erro: true);
    }
}
