using System.Collections.ObjectModel;
using System.Text.Json;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.Modelos;

public enum EstadoSubagente { Rodando, SegundoPlano, Terminou, Erro, Parado }
