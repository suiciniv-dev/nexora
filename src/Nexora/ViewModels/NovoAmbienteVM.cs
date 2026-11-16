using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class NovoAmbienteVM : ObservableObject
{
    readonly JanelaVM _janela;

    public NovoAmbienteVM(JanelaVM janela)
    {
        _janela = janela;
        Nome = "";
        Ocultos = janela.Ocultos.Select(o => new OcultoVM(o, Reexibir)).ToList();
    }

    public ObservableCollection<PastaEscolhidaVM> Vinculados { get; } = [];
    public List<OcultoVM> Ocultos { get; }
    public bool TemOcultos => Ocultos.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeCriar), nameof(Previa))]
    public partial string Nome { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeCriar), nameof(Previa), nameof(TemPrincipal))]
    public partial string? Principal { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemErro))]
    public partial string? Erro { get; set; }

    public bool TemPrincipal => Principal is not null;
    public bool TemErro => Erro is not null;
    public bool PodeCriar => Principal is not null && Nome.Trim().Length > 0;
    public string Previa => Principal is null ? Idioma.T("Escolha a pasta principal") : Path.Combine(Principal, $"{Nome.Trim()}.code-workspace");

    public void UsarPrincipal(string pasta)
    {
        Principal = pasta;
        if (Nome.Trim().Length == 0) Nome = Path.GetFileName(pasta.TrimEnd('\\', '/'));
    }

    public void AdicionarVinculado(string pasta)
    {
        if ((Principal is not null && Workspaces.MesmaPasta(pasta, Principal)) || Vinculados.Any(v => Workspaces.MesmaPasta(v.Pasta, pasta))) return;
        Vinculados.Add(new PastaEscolhidaVM(pasta, v => Vinculados.Remove(v)));
    }

    [RelayCommand]
    void Criar()
    {
        if (!PodeCriar || Principal is null) return;
        try
        {
            var ambiente = Workspaces.Salvar(new Ambiente(Nome.Trim(), Principal, Vinculados.Select(v => v.Pasta).ToList(), null));
            _janela.Lembrar(ambiente);
            _janela.Substituir(this, _janela.Registrar(ambiente));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Erro = Idioma.F("Não consegui criar o workspace: {0}", ex.Message);
        }
    }

    void Reexibir(OcultoVM oculto)
    {
        _janela.Mostrar(oculto.Pasta);
        Ocultos.Remove(oculto);
        OnPropertyChanged(nameof(Ocultos));
        OnPropertyChanged(nameof(TemOcultos));
    }
}

public sealed partial class PastaEscolhidaVM(string pasta, Action<PastaEscolhidaVM> remover) : ObservableObject
{
    public string Pasta { get; } = pasta;
    public string Nome { get; } = Path.GetFileName(pasta.TrimEnd('\\', '/'));

    [RelayCommand]
    void Remover() => remover(this);
}

public sealed partial class OcultoVM(string pasta, Action<OcultoVM> mostrar) : ObservableObject
{
    public string Pasta { get; } = pasta;
    public string Nome { get; } = Path.GetFileName(pasta.TrimEnd('\\', '/'));

    [RelayCommand]
    void Mostrar() => mostrar(this);
}
