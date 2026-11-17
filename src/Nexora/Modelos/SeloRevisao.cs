using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Git;
using Nexora.Core.Revisao;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.Modelos;

public sealed record SeloRevisao(string Texto, string Tipo, string Dica, string? Correcao)
{
    public bool Ok => Tipo == "ok";
    public bool Falhou => Tipo == "erro";
    public bool Alerta => Tipo == "alerta";
    public bool PodeCorrigir => Correcao is not null;
    public string Icone => Tipo switch { "ok" => "ok", "erro" => "fechar", _ => "alerta" };
}
