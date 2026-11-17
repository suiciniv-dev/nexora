namespace Nexora.Core.Mcp;

public interface IHubMcp : IDisposable
{
    event Action? Mudou;
    int Ativos { get; }
    int SessoesRegistradas { get; }
    RegistroHub Registrar(string pasta, string rotulo, string? casa = null);
    void Renomear(string chave, string rotulo);
    Task EsquecerAsync(string chave);
    Task PararAsync(string instancia);
    IReadOnlyList<EstadoServidor> Estado();
    IReadOnlyList<int> Pids();
}
