using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class AmbienteVM
{
    public bool PodeIrParaNuvem
    {
        get => ChatVM.PodeIrParaNuvem(Principal);
        set
        {
            if (value == PodeIrParaNuvem) return;
            var chave = Config.ChaveDePasta(Principal);
            App.Config.ProjetosNaNuvem.RemoveAll(p => string.Equals(p, chave, StringComparison.OrdinalIgnoreCase));
            if (value) App.Config.ProjetosNaNuvem.Add(chave);
            if (!JanelaVM.Isolada && !JanelaVM.Demo) App.Config.Salvar();
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextoNuvem));
        }
    }

    public string TextoNuvem => !PodeIrParaNuvem
        ? Idioma.T("Desligado: as conversas deste projeto rodam sempre neste PC e o Nexora nunca oferece a nuvem.")
        : ChatVM.MaquinasProntas.Count == 0
            ? Idioma.T("Ligado. Falta conectar uma máquina em Configurações › Máquinas na nuvem.")
            : Idioma.T("Ligado. Cada conversa nova escolhe Neste PC ou a nuvem, ao lado do agente. O padrão continua Neste PC.");
}
