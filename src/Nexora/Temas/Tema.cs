using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Nexora.Fundos;

namespace Nexora.Temas;

public sealed class Tema
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public bool Escuro { get; set; } = true;
    public Dictionary<string, string> Cores { get; set; } = [];
    public string Fonte { get; set; } = Catalogo.FonteEmbutida;
    public string FonteCodigo { get; set; } = "Cascadia Mono";
    public string FundoAnimado { get; set; } = Papeis.Nenhum;
    public string? Imagem { get; set; }
    public double Escurecer { get; set; } = 0.75;
    public string? Editor { get; set; }
    public string? Luz { get; set; }
    public string? Pontos { get; set; }

    [JsonIgnore]
    public bool Embutido { get; set; }

    [JsonIgnore]
    public string NomeExibido => Embutido ? Nexora.Core.Texto.Idioma.T(Nome) : Nome;

    public Color Cor(string chave)
    {
        if (Cores.TryGetValue(chave, out var hex) && Color.TryParse(hex, out var cor)) return cor;
        if (chave is "Marca" or "MarcaFim") return Cor("Acento");
        var reserva = (Escuro ? Catalogo.Dracula : Catalogo.Alucard).Cores[chave];
        return Color.Parse(reserva);
    }

    public Tema Copiar(string id, string nome) => new()
    {
        Id = id,
        Nome = nome,
        Escuro = Escuro,
        Cores = new Dictionary<string, string>(Cores),
        Fonte = Fonte,
        FonteCodigo = FonteCodigo,
        FundoAnimado = FundoAnimado,
        Imagem = Imagem,
        Escurecer = Escurecer,
        Editor = Editor,
        Luz = Luz,
        Pontos = Pontos,
    };
}
