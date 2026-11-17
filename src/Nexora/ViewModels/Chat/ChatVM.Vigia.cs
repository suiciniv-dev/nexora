using CommunityToolkit.Mvvm.Input;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    public bool VigiaPausada => SessaoId is { Length: > 0 } id && App.Config.VigiasPausadas.Contains(id);

    public string TextoVigia => VigiaPausada ? Idioma.T("Voltar a vigiar o PR") : Idioma.T("Pausar a vigia do PR");

    [RelayCommand]
    void AlternarVigia()
    {
        if (SessaoId is not { Length: > 0 } id) return;
        if (!App.Config.VigiasPausadas.Remove(id)) App.Config.VigiasPausadas.Add(id);
        if (!JanelaVM.Isolada) App.Config.Salvar();
        OnPropertyChanged(nameof(VigiaPausada));
        OnPropertyChanged(nameof(TextoVigia));
    }

    public void PedirPelaVigia(string texto)
    {
        if (DeveEnfileirar || _sessao is { Ativa: true } && Trabalhando) Fila.Add(new MensagemNaFilaVM(texto, [], this));
        else EnviarConteudo(texto, []);
    }
}
