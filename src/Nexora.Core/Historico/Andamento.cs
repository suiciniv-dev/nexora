using System.Text.Json;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.Core.Historico;

public sealed record Andamento(string? Titulo, string? Branch, string? Atividade, DateTimeOffset? AtividadeEm, ContextoUsado? Uso)
{
    const int Cauda = 256 * 1024;

    public static Andamento Ler(string arquivo, string? pasta = null)
    {
        string? tituloIa = null, tituloProprio = null, branch = null, atividade = null;
        DateTimeOffset? quando = null;
        ContextoUsado? uso = null;
        void Linha(ReadOnlyMemory<byte> linha)
        {
            try
            {
                using var doc = JsonDocument.Parse(linha);
                var o = doc.RootElement;
                switch (Protocolo.Texto(o, "type"))
                {
                    case "ai-title":
                        tituloIa = Protocolo.Texto(o, "aiTitle") ?? tituloIa;
                        return;
                    case "custom-title":
                        tituloProprio = Protocolo.Texto(o, "customTitle") ?? tituloProprio;
                        return;
                    case not ("user" or "assistant"):
                        return;
                }
                branch = Protocolo.Texto(o, "gitBranch") ?? branch;
                foreach (var evento in Transcricao.LerLinha(o))
                {
                    if (evento is ContextoUsado u) uso = u;
                    var texto = evento switch
                    {
                        MensagemDoUsuario m => Idioma.T("Você: ") + m.Texto,
                        TextoCompleto t => t.Texto,
                        FerramentaChamada f => $"{Resumos.NomeDaFerramenta(f.Nome)}  {Resumos.DaFerramenta(f.Nome, f.Entrada, pasta)}",
                        _ => null,
                    };
                    if (texto is null) continue;
                    atividade = Resumos.UmaLinha(texto, 160);
                    quando = Transcricao.Quando(o) ?? quando;
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
            }
        }
        try
        {
            ConversasSalvas.UltimasLinhasEmBytes(arquivo, Cauda, Linha);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new Andamento(null, null, null, null, null);
        }
        return new Andamento(tituloProprio ?? tituloIa, branch is "HEAD" or "" ? null : branch, atividade, quando, uso);
    }
}
