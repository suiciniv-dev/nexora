using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    public const double LimiteParaCompactar = 0.8;
    double? _compactarDispensadoEm;

    public bool SugerirCompactar =>
        ContextoFracao >= LimiteParaCompactar
        && Estado is EstadoChat.Pronta or EstadoChat.Erro
        && Itens.Count > 0
        && (_compactarDispensadoEm is not { } dispensado || ContextoFracao >= Math.Min(0.95, dispensado + 0.08));

    public string TextoCompactar =>
        Idioma.F("O contexto está em {0}%. Compactar resume a conversa até aqui e libera espaço para continuar sem perder o fio.", Math.Round(ContextoFracao * 100));

    void IniciarCompactar() => PropertyChanged += (_, e) =>
    {
        if (e.PropertyName is not (nameof(ContextoTokens) or nameof(JanelaContexto) or nameof(Estado))) return;
        OnPropertyChanged(nameof(SugerirCompactar));
        OnPropertyChanged(nameof(TextoCompactar));
    };

    [RelayCommand]
    void Compactar()
    {
        _compactarDispensadoEm = null;
        EnviarTexto("/compact");
    }

    [RelayCommand]
    void DispensarCompactar()
    {
        _compactarDispensadoEm = ContextoFracao;
        OnPropertyChanged(nameof(SugerirCompactar));
    }

    void AoCompactar(ConversaCompactada compactada)
    {
        _compactarDispensadoEm = null;
        if (compactada.TokensDepois is { } depois) ContextoTokens = depois;
        var resumo = ItemPensamento.TextoTokens((int)Math.Min(int.MaxValue, compactada.TokensAntes));
        Adicionar(ItemAviso.Troca(compactada.Automatica ? Idioma.F("O Claude compactou a conversa sozinho ({0} resumidos)", resumo) : Idioma.F("Conversa compactada ({0} resumidos)", resumo)));
        OnPropertyChanged(nameof(SugerirCompactar));
    }
}
