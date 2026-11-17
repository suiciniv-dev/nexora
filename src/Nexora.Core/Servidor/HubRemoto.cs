using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Mcp;

namespace Nexora.Core.Servidor;

public sealed class HubRemoto : IHubMcp
{
    readonly NucleoRemoto _nucleo;
    ContagemDoHub _contagem = new(0, 0);

    HubRemoto(NucleoRemoto nucleo)
    {
        _nucleo = nucleo;
        nucleo.Aviso += AoAviso;
    }

    public event Action? Mudou;

    public int Ativos => _contagem.Ativos;
    public int SessoesRegistradas => _contagem.Sessoes;

    public static async Task<HubRemoto?> LigarAsync(NucleoRemoto nucleo, string pastaCache, TimeSpan ocioso)
    {
        var contagem = await nucleo.ChamarAsync<ContagemDoHub>(MetodosDoNucleo.LigarHub, new JsonObject { ["pasta"] = pastaCache, ["minutos"] = ocioso.TotalMinutes }).ConfigureAwait(false);
        return contagem is null ? null : new HubRemoto(nucleo) { _contagem = contagem };
    }

    void AoAviso(JsonObject aviso)
    {
        if ((string?)aviso["aviso"] != AvisosDoNucleo.Hub) return;
        if (aviso["contagem"].Deserialize<ContagemDoHub>(ServidorDoNucleo.Json) is { } contagem) _contagem = contagem;
        Mudou?.Invoke();
    }

    T? Chamar<T>(string metodo, JsonObject? dados) where T : class =>
        _nucleo.ChamarAsync<T>(metodo, dados).GetAwaiter().GetResult();

    public RegistroHub Registrar(string pasta, string rotulo, string? casa = null) =>
        Chamar<RegistroHub>(MetodosDoNucleo.RegistrarNoHub, new JsonObject { ["pasta"] = pasta, ["rotulo"] = rotulo, ["casa"] = casa })
        ?? new RegistroHub("", null, []);

    public void Renomear(string chave, string rotulo) =>
        _ = _nucleo.ChamarAsync<RespostaVazia>(MetodosDoNucleo.RenomearNoHub, new JsonObject { ["chave"] = chave, ["rotulo"] = rotulo });

    public Task EsquecerAsync(string chave) =>
        _nucleo.ChamarAsync<RespostaVazia>(MetodosDoNucleo.EsquecerNoHub, new JsonObject { ["chave"] = chave });

    public Task PararAsync(string instancia) =>
        _nucleo.ChamarAsync<RespostaVazia>(MetodosDoNucleo.PararNoHub, new JsonObject { ["instancia"] = instancia });

    public IReadOnlyList<EstadoServidor> Estado() => Chamar<List<EstadoServidor>>(MetodosDoNucleo.EstadoDoHub, null) ?? [];

    public IReadOnlyList<int> Pids() => Chamar<List<int>>(MetodosDoNucleo.PidsDoHub, null) ?? [];

    public void Dispose() => _nucleo.Aviso -= AoAviso;
}
