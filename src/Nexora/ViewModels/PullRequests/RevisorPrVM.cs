using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed record RevisorPrVM(RevisorDoPr Dados)
{
    public string Login => Dados.Login;
    public string Inicial => Dados.Login.Length > 0 ? Dados.Login[..1].ToUpperInvariant() : "?";
    public bool Aprovou => Dados.Estado == "APPROVED";
    public bool PediuMudancas => Dados.Estado == "CHANGES_REQUESTED";
    public bool Esperando => Dados.Estado == "REQUESTED";

    public string Estado => Dados.Estado switch
    {
        "APPROVED" => Idioma.T("aprovou"),
        "CHANGES_REQUESTED" => Idioma.T("pediu mudanças"),
        "COMMENTED" => Idioma.T("comentou"),
        "DISMISSED" => Idioma.T("revisão descartada"),
        "REQUESTED" => Idioma.T("revisão pedida"),
        _ => Dados.Estado.ToLowerInvariant(),
    };

    public string Icone => Dados.Estado switch
    {
        "APPROVED" => "ok",
        "CHANGES_REQUESTED" => "alerta",
        "REQUESTED" => "relogio",
        _ => "conversa",
    };
}
