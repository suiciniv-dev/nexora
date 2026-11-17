namespace Nexora.Servicos;

public static class VagasDeSubagentes
{
    public const string Perguntar = "perguntar";
    public const string Haiku = "haiku";
    public const string Sonnet = "sonnet";
    public const string Opus = "opus";
    public const string Conversa = "conversa";

    sealed record Pedido(object Dono, string Id, Action Liberar);

    static readonly List<Pedido> Fila = [];
    static readonly Dictionary<string, object> Ocupadas = [];

    public static event Action? Mudou;

    public static int Limite => App.Config.SubagentesAoMesmoTempo;
    public static int EmUso => Ocupadas.Count;
    public static int Esperando => Fila.Count;

    static bool TemVaga => Limite <= 0 || Ocupadas.Count < Limite;

    public static string? ModeloFixo(string escolha) => escolha is Haiku or Sonnet or Opus ? escolha : null;

    public static bool Pedir(object dono, string id, Action liberar)
    {
        if (Ocupadas.ContainsKey(id) || Fila.Any(p => p.Id == id)) return Ocupadas.ContainsKey(id);
        if (TemVaga)
        {
            Ocupadas[id] = dono;
            liberar();
            Mudou?.Invoke();
            return true;
        }
        Fila.Add(new Pedido(dono, id, liberar));
        Mudou?.Invoke();
        return false;
    }

    public static void Soltar(string id)
    {
        var mudou = Ocupadas.Remove(id) | Fila.RemoveAll(p => p.Id == id) > 0;
        if (mudou) Andar();
    }

    public static void SoltarDe(object dono)
    {
        var livres = Ocupadas.Where(o => o.Value == dono).Select(o => o.Key).ToList();
        foreach (var id in livres) Ocupadas.Remove(id);
        if (livres.Count + Fila.RemoveAll(p => p.Dono == dono) > 0) Andar();
    }

    public static void Andar()
    {
        while (Fila.Count > 0 && TemVaga)
        {
            var proximo = Fila[0];
            Fila.RemoveAt(0);
            Ocupadas[proximo.Id] = proximo.Dono;
            proximo.Liberar();
        }
        Mudou?.Invoke();
    }

    public static void Zerar()
    {
        Fila.Clear();
        Ocupadas.Clear();
    }
}
