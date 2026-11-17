using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.Modelos;

public enum TipoAviso { Info, Erro, Rodape, Troca, NovaSessao }
