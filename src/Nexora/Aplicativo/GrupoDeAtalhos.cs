using System.Windows.Input;
using Nexora.Core.Texto;
using Nexora.ViewModels;

namespace Nexora.Aplicativo;

public sealed record GrupoDeAtalhos(string Nome, IReadOnlyList<AtalhoDoApp> Itens);
