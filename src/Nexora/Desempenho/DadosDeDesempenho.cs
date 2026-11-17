using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using Nexora.Core.Claude;
using Nexora.Core.Desempenho;
using Nexora.ViewModels;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.Desempenho;

public sealed record DadosDeDesempenho(Amostra? Agora, ResumoDoDia Hoje, IReadOnlyList<ResumoDoDia> Anteriores, DateTime Desde, bool Grava);
