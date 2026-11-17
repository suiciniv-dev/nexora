namespace Nexora.Core.Servidor;

public static class MetodosDoNucleo
{
    public const string Conversas = "conversas";
    public const string Uso = "uso";
    public const string Andamento = "andamento";
    public const string Comando = "comando";
    public const string Turnos = "turnos";
    public const string Modo = "modo";
    public const string AbrirSessao = "sessao.abrir";
    public const string AssinarSessao = "sessao.assinar";
    public const string SoltarSessao = "sessao.soltar";
    public const string EnviarASessao = "sessao.enviar";
    public const string AcaoNaSessao = "sessao.acao";
    public const string PedirASessao = "sessao.pedir";
    public const string RemotoDaSessao = "sessao.remoto";
    public const string HistoricoDaSessao = "sessao.historico";
    public const string ListarSessoes = "sessao.listar";
    public const string EsquecerSessao = "sessao.esquecer";
    public const string LigarHub = "hub.ligar";
    public const string RegistrarNoHub = "hub.registrar";
    public const string RenomearNoHub = "hub.renomear";
    public const string EsquecerNoHub = "hub.esquecer";
    public const string PararNoHub = "hub.parar";
    public const string EstadoDoHub = "hub.estado";
    public const string PidsDoHub = "hub.pids";

    public static bool EmOrdem(string metodo) =>
        metodo is AbrirSessao or AssinarSessao or SoltarSessao or EnviarASessao or AcaoNaSessao or ListarSessoes or EsquecerSessao;
}
