using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Nexora.Core.Executar;
using Nexora.Core.Git;
using Nexora.Core.Nuvem;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.Modelos;

public enum EstadoAlvo { Parado, Rodando, Pronto, Erro }
