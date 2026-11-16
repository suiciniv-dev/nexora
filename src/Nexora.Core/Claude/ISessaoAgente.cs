using System.Text.Json.Nodes;

namespace Nexora.Core.Claude;

public interface ISessaoAgente : IAsyncDisposable
{
    event Action<EventoClaude>? Evento;
    event Action<int, string>? Encerrou;

    string Agente { get; }
    string? SessaoId { get; }
    bool Ativa { get; }
    int? Pid { get; }
    bool TemRemoto { get; }
    bool TemControles { get; }

    void Iniciar();
    void Enviar(string texto, IReadOnlyList<Anexo>? imagens = null);
    void Permitir(string pedidoId, JsonNode? entrada, JsonNode? regras = null);
    void Negar(string pedidoId, string motivo);
    void LiberarSubagente(string pedidoId, JsonNode? entrada) { }
    void BarrarSubagente(string pedidoId, string motivo) { }
    void Interromper();
    void MudarModo(string modo);
    void MudarModelo(string? modelo);
    void AplicarConfiguracoes(JsonObject configuracoes);
    Task<ControleRespondido> PedirAsync(Func<string, string> montar, TimeSpan? limite = null);
    Task<ControleRespondido> LigarRemotoAsync(bool ligado);
    void Encerrar();
}

public interface IHistoricoDoAgente
{
    Task<IReadOnlyList<EventoClaude>> LerHistoricoAsync(TimeSpan? espera = null);
}

public static class Agentes
{
    public const string Claude = "claude";
    public const string Codex = "codex";

    public static string Normalizar(string? agente) => Nexora.Core.Provedores.CatalogoAgentes.De(agente).Id;

    public static string Nome(string? agente) => Nexora.Core.Provedores.CatalogoAgentes.De(agente).Nome;
}
