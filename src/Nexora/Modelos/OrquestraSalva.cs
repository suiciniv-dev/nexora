using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.Core.Orquestra;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.Modelos;

public sealed class OrquestraSalva
{
    public string Pasta { get; set; } = "";
    public string Titulo { get; set; } = "";
    public DateTime Atualizado { get; set; }
    public double ChefeX { get; set; }
    public double ChefeY { get; set; }
    public List<AgenteSalvo> Agentes { get; set; } = [];
}
