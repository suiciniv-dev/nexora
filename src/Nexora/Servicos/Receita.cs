using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Nexora.Core.Texto;

namespace Nexora.Servicos;

public sealed record Receita(string Id, string Site, string[] Passos, string? Pacote = null, bool Script = false, string? Brew = null)
{
    public bool Instalavel => Script || (OperatingSystem.IsMacOS()
        ? Brew is not null && Instalacao.BrewInstalado() is not null
        : Pacote is not null && Instalacao.Winget() is not null);

    public string Comando => Id switch
    {
        "claude" => OperatingSystem.IsMacOS() ? "curl -fsSL https://claude.ai/install.sh | bash" : "irm https://claude.ai/install.ps1 | iex",
        "dotnet" => OperatingSystem.IsMacOS() ? Idioma.T("Vem junto com o Nexora") : "winget install Microsoft.DotNet.Runtime.10",
        _ => OperatingSystem.IsMacOS() ? (Brew is null ? "" : $"brew install {Brew}") : $"winget install {Pacote}",
    };
}
