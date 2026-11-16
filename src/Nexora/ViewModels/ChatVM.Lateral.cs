using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    static readonly HashSet<string> _laterais = [];

    public static bool EhIdLateral(string sessaoId)
    {
        lock (_laterais) return _laterais.Contains(sessaoId);
    }

    [ObservableProperty]
    public partial bool EhLateral { get; set; }

    public static bool EhComandoLateral(string texto, out string? pergunta)
    {
        pergunta = null;
        if (!texto.StartsWith("/btw", StringComparison.OrdinalIgnoreCase)) return false;
        if (texto.Length > 4 && !char.IsWhiteSpace(texto[4])) return false;
        pergunta = texto[4..].Trim();
        if (pergunta.Length == 0) pergunta = null;
        return true;
    }

    public static ChatVM Lateral(ChatVM origem, IAnfitriao anfitriao, Action<UsoDoPlano> aoUsar, Action<ChatVM> aoFechar)
    {
        var sessaoId = origem.EhClaude ? origem.SessaoId : null;
        var chat = new ChatVM(origem.LocalAtual, Idioma.T("Pergunta lateral"), sessaoId, anfitriao, aoUsar, aoFechar)
        {
            Agente = origem.Agente,
            EhLateral = true,
            Bifurcar = sessaoId is not null,
            ModoIndice = origem.ModoIndice,
            ModeloValor = origem.ModeloValor,
            ModeloDoAgente = origem.ModeloDoAgente,
            EsforcoValor = origem.EsforcoValor,
        };
        chat.Adicionar(ItemAviso.Info(sessaoId is null
            ? Idioma.T("Pergunta lateral: uma conversa à parte, que some ao fechar a aba. Para guardá-la, clique com o botão direito na aba e escolha Manter conversa.")
            : Idioma.F("Pergunta lateral: usa o contexto de \"{0}\", mas nada daqui volta para ela. Ao fechar a aba, a conversa some. Para guardá-la, clique com o botão direito na aba e escolha Manter conversa.", origem.Titulo)));
        return chat;
    }

    public void FocarCampo() => PedirFoco?.Invoke();

    [RelayCommand]
    void AbrirEmSplit() => _anfitriao.AbrirEmSplit(this);

    void RegistrarLateral(string sessaoId)
    {
        lock (_laterais) _laterais.Add(sessaoId);
    }

    [RelayCommand]
    void ManterLateral()
    {
        if (!EhLateral) return;
        EhLateral = false;
        if (SessaoId is { } id) lock (_laterais) _laterais.Remove(id);
        Adicionar(ItemAviso.Info(Idioma.T("Conversa guardada. Ela aparece no histórico.")));
    }

    public void ApagarRastro()
    {
        if (!EhLateral || SessaoId is not { } id) return;
        _ = Task.Run(async () =>
        {
            for (var tentativa = 0; tentativa < 6; tentativa++)
            {
                await Task.Delay(1500);
                try
                {
                    var raiz = Core.Historico.ConversasSalvas.PastaPadrao;
                    if (!Directory.Exists(raiz)) return;
                    foreach (var pasta in Directory.EnumerateDirectories(raiz))
                    {
                        var arquivo = Path.Combine(pasta, id + ".jsonl");
                        if (File.Exists(arquivo)) File.Delete(arquivo);
                        var extras = Path.Combine(pasta, id);
                        if (Directory.Exists(extras)) Directory.Delete(extras, true);
                    }
                    lock (_laterais) _laterais.Remove(id);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        });
    }
}
