using Nexora.Core.Claude;

namespace Nexora.Core.Servidor;

public sealed record RespostaDeAssinatura(bool Completo, long Ultimo, bool Trabalhando, bool Ativa, IReadOnlyList<EventoClaude> Pendentes);
