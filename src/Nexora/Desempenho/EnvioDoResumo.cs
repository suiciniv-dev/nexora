using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using Nexora.Core.Claude;
using Nexora.Core.Desempenho;
using Nexora.ViewModels;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.Desempenho;

static class EnvioDoResumo
{
    public static async Task<(bool Ok, string Mensagem)> EnviarAsync(JsonObject corpo, CancellationToken cancelar = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"Nexora/{Atualizacoes.Atual.ToString(3)}");
            using var conteudo = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json");
            using var resposta = await http.PostAsync(Relatorio.Endereco, conteudo, cancelar);
            if (resposta.IsSuccessStatusCode) return (true, Idioma.F("Resumo enviado às {0:HH:mm}.", DateTime.Now));
            return (false, (int)resposta.StatusCode switch
            {
                429 => Idioma.T("Muitos envios seguidos. Tente de novo mais tarde."),
                503 => Idioma.T("O envio por e-mail ainda não está configurado no site."),
                var codigo => Idioma.F("O site recusou o resumo (erro {0}).", codigo),
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (false, Idioma.T("Não deu para falar com o site agora."));
        }
    }
}
