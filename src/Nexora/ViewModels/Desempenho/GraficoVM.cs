using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Desempenho;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed record GraficoVM(string Titulo, string Resumo, IReadOnlyList<BarraVM> Barras, string Inicio, string Fim);
