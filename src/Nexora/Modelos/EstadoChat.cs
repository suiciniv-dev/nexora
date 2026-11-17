using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json.Nodes;
using Nexora.Core.Claude;
using Nexora.Core.Git;
using Nexora.Core.Arquivos;
using Nexora.Core.Historico;
using Nexora.Core.Texto;

namespace Nexora.Modelos;

public enum EstadoChat { Nova, Pronta, Trabalhando, PrecisaDeVoce, Erro }
