using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Git;
using Nexora.Core.Arquivos;
using Nexora.Core.Historico;
using Nexora.Core.Texto;

namespace Nexora.Modelos;

public enum EstadoChat { Nova, Pronta, Trabalhando, PrecisaDeVoce, Erro }
