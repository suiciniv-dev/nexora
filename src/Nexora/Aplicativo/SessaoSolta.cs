namespace Nexora.Aplicativo;

public sealed class SessaoSolta
{
    public string? IdNoServidor { get; set; }
    public string? SessaoId { get; set; }
    public string Pasta { get; set; } = "";
    public string? Agente { get; set; }
    public string? Titulo { get; set; }
}
