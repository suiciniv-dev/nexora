using Nexora.Core.Claude;

namespace Nexora.Core.Servidor;

public sealed record SessaoEncerrada(int Codigo, string Erros) : EventoClaude;
