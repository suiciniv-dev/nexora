using System.Text.Json.Nodes;
using Nexora.Core.Orquestra;

namespace Nexora.Core.Servidor;

public sealed class PonteRemota(NucleoRemoto nucleo) : IPonteMcp
{
    public void Apontar(string servidor, int porta, string segredo) =>
        nucleo.ChamarAsync<RespostaVazia>(MetodosDoNucleo.ApontarNaPonte, new JsonObject { ["servidor"] = servidor, ["porta"] = porta, ["segredo"] = segredo }).GetAwaiter().GetResult();

    public RegistroNaPonte? Registrar(string servidor, string dono) =>
        nucleo.Conectado
            ? nucleo.ChamarAsync<RegistroNaPonte>(MetodosDoNucleo.RegistrarNaPonte, new JsonObject { ["servidor"] = servidor, ["dono"] = dono }).GetAwaiter().GetResult()
            : null;

    public void Esquecer(string chave) =>
        _ = nucleo.ChamarAsync<RespostaVazia>(MetodosDoNucleo.EsquecerNaPonte, new JsonObject { ["chave"] = chave });
}
