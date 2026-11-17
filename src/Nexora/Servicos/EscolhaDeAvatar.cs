using System.Collections.Concurrent;
using System.Text.Json;
using Avalonia.Platform;
using Nexora.Core.Avatares;

namespace Nexora.Servicos;

public sealed record EscolhaDeAvatar(DefinicaoAvatar Definicao, string Cor);
