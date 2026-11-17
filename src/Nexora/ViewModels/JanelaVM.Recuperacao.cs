using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Arquivos;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class JanelaVM
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoDeRecuperacao), nameof(TextoRecuperacao), nameof(DicaRecuperacao))]
    public partial bool RecuperacaoDispensada { get; set; }

    public static string? DescreverRecuperacao(OrigemDaLeitura origem, string arquivo) => origem switch
    {
        OrigemDaLeitura.Copia => Idioma.F("O {0} estava danificado e foi recuperado da última cópia boa. O arquivo danificado foi guardado ao lado.", arquivo),
        OrigemDaLeitura.Perdido => Idioma.F("O {0} estava danificado e não havia cópia boa, então começou vazio. O arquivo danificado foi guardado ao lado.", arquivo),
        OrigemDaLeitura.Ilegivel => Idioma.F("Não consegui ler o {0} porque ele estava em uso. Nada será gravado nele até você reabrir o Nexora, para não sobrescrevê-lo.", arquivo),
        _ => null,
    };

    public static IReadOnlyList<Recuperacao> Recuperacoes(params (OrigemDaLeitura Origem, string Arquivo, string? Guardado)[] lidos) =>
        [.. lidos.Where(l => DescreverRecuperacao(l.Origem, l.Arquivo) is not null).Select(l => new Recuperacao(l.Origem, DescreverRecuperacao(l.Origem, l.Arquivo)!, l.Guardado))];

    IReadOnlyList<Recuperacao> RecuperacoesDoApp => Isolada || Demo
        ? []
        : Recuperacoes((App.Config.Origem, "config.json", App.Config.Guardado), (App.Nuvem.Origem, "nuvem.json", App.Nuvem.Guardado));

    public bool TemAvisoDeRecuperacao => !RecuperacaoDispensada && RecuperacoesDoApp.Count > 0;

    public string TextoRecuperacao => RecuperacoesDoApp.Any(r => r.Origem == OrigemDaLeitura.Ilegivel)
        ? Idioma.T("Configuração não lida")
        : Idioma.T("Configuração recuperada");

    public string DicaRecuperacao => string.Join("\n\n", RecuperacoesDoApp.Select(r => r.Texto)) + "\n\n" + Idioma.T("Clique para ver a pasta e dispensar o aviso.");

    [RelayCommand]
    void AbrirRecuperacao()
    {
        if (RecuperacoesDoApp.Select(r => r.Guardado).FirstOrDefault(g => g is not null) is { } guardado) Sistema.Revelar(guardado, false);
        else Sistema.AbrirPasta(Path.GetDirectoryName(Config.CaminhoPadrao)!);
        RecuperacaoDispensada = true;
    }
}
