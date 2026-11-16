using Avalonia.Threading;
using Nexora.Core.Git;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class JanelaVM
{
    DispatcherTimer? _vigia;
    bool _vigiando;
    readonly Dictionary<string, List<DateTime>> _pedidosDaVigia = new(StringComparer.OrdinalIgnoreCase);
    static readonly TimeSpan IntervaloDaVigia = TimeSpan.FromMinutes(2);
    const int PedidosPorHora = 3;

    public static Func<string, string, Task<RetratoPr?>> BuscarRetrato { get; set; } = VigiaDePr.BuscarAsync;
    public static Func<string, Task<string?>> BuscarLogin { get; set; } = GitHub.EuAsync;

    public void LigarVigia()
    {
        if (Demo || Isolada || _vigia is not null) return;
        _vigia = new DispatcherTimer(IntervaloDaVigia, DispatcherPriority.Background, async (_, _) => await VigiarAsync());
        _vigia.Start();
    }

    static bool VigiaLigada => App.Config.VigiaChecks != VigiaDePr.Desligado || App.Config.VigiaConflitos != VigiaDePr.Desligado || App.Config.VigiaRevisoes != VigiaDePr.Desligado;

    public async Task VigiarAsync()
    {
        if (_vigiando || !VigiaLigada) return;
        _vigiando = true;
        try
        {
            var alvos = Abertas.Where(c => !c.VigiaPausada && c.Pasta is { Length: > 0 } && GitHub.TemPr(c.Branch)).ToList();
            foreach (var chat in alvos)
            {
                if (await BuscarRetrato(chat.Pasta!, chat.Branch!) is not { } retrato) continue;
                var login = await BuscarLogin(chat.Pasta!);
                Avaliar(chat, retrato, login);
            }
            PodarVistos();
        }
        finally
        {
            _vigiando = false;
        }
    }

    public void Avaliar(ChatVM chat, RetratoPr retrato, string? login)
    {
        var vistos = App.Config.VigiaVistos;
        var chavePr = $"pr:{chat.Pasta}|{retrato.Numero}";
        var primeiraVez = !vistos.ContainsKey(chavePr);
        vistos[chavePr] = DateTime.Now;
        var eventos = VigiaDePr.Comparar(retrato, login, vistos.ContainsKey, primeiraVez);
        foreach (var evento in eventos)
        {
            foreach (var chave in evento.Chaves) vistos[chave] = DateTime.Now;
            var modo = evento.Tipo switch
            {
                TipoEventoPr.CheckFalhou => App.Config.VigiaChecks,
                TipoEventoPr.Conflito => App.Config.VigiaConflitos,
                _ => App.Config.VigiaRevisoes,
            };
            if (modo == VigiaDePr.Desligado || evento.Tipo == TipoEventoPr.Comentarios && evento.Comentarios.Count == 0) continue;
            var agir = modo is VigiaDePr.Agir or VigiaDePr.AgirEResponder && PodePedir(chavePr);
            if (agir) chat.PedirPelaVigia(VigiaDePr.Pedido(evento, modo));
            Avisos.Notificar(VigiaDePr.Aviso(evento),
                agir ? Idioma.F("Pedi ao agente de \"{0}\" para cuidar disso.", chat.Titulo) : chat.Titulo,
                chat.Codigo, AoClicarNoAviso);
        }
        if (!Isolada) App.Config.Salvar();
    }

    bool PodePedir(string chavePr)
    {
        if (!_pedidosDaVigia.TryGetValue(chavePr, out var vezes)) _pedidosDaVigia[chavePr] = vezes = [];
        vezes.RemoveAll(v => DateTime.Now - v > TimeSpan.FromHours(1));
        if (vezes.Count >= PedidosPorHora) return false;
        vezes.Add(DateTime.Now);
        return true;
    }

    static void PodarVistos()
    {
        foreach (var velha in App.Config.VigiaVistos.Where(v => DateTime.Now - v.Value > TimeSpan.FromDays(30)).Select(v => v.Key).ToList())
            App.Config.VigiaVistos.Remove(velha);
    }
}
