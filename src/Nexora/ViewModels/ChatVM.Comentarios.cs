using Nexora.Core.Git;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    public RevisaoPendenteVM RevisaoPendente { get; private set; } = null!;

    EditorVM CriarEditor()
    {
        RevisaoPendente = new RevisaoPendenteVM(MandarRevisao);
        return new EditorVM(() => Pasta, InserirNoRascunho) { Revisao = RevisaoPendente, ConversasDoArquivo = ConversasDoArquivo };
    }

    IReadOnlyList<ConversaDoPr> ConversasDoArquivo(string relativo) =>
        _painelPr?.Pr is { } pr
            ? [.. pr.Conversas.Where(c => string.Equals(c.Arquivo.Replace('\\', '/'), relativo, StringComparison.OrdinalIgnoreCase))]
            : [];

    public void MandarRevisao(string texto) => PedirPelaVigia(texto);

    public void AbrirConversaNova(string texto)
    {
        if (Vazia && !Trabalhando)
        {
            PedirPelaVigia(texto);
            return;
        }
        _anfitriao.AbrirSessao(LocalAtual, texto);
    }
}
