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

public sealed class AgenteSalvo
{
    public string SessaoId { get; set; } = "";
    public string Pasta { get; set; } = "";
    public string? Grupo { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}
