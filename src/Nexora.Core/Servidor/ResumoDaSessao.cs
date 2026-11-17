namespace Nexora.Core.Servidor;

public sealed record ResumoDaSessao(
    string Id,
    string Agente,
    string Pasta,
    string? Titulo,
    string? SessaoId,
    bool Ativa,
    bool Trabalhando,
    int? Pid,
    long Ultimo,
    int Assinantes);
