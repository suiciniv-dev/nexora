using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class JanelaVM
{
    readonly HashSet<AmbienteVM> _destacados = [];
    DispatcherTimer? _relogioRecado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemRecado))]
    public partial string RecadoSoltar { get; set; } = "";

    public bool TemRecado => RecadoSoltar.Length > 0;

    public bool Destacado(AmbienteVM ambiente) => _destacados.Contains(ambiente);

    public static string Recado(IReadOnlyList<string> novos, IReadOnlyList<string> existentes, int arquivos, IReadOnlyList<string> falhas)
    {
        var partes = new List<string>();
        if (novos.Count == 1) partes.Add(Idioma.F("{0} virou projeto.", novos[0]));
        else if (novos.Count > 1) partes.Add(Idioma.F("{0} pastas viraram projetos.", novos.Count));
        if (existentes.Count == 1) partes.Add(Idioma.F("{0} já era um projeto.", existentes[0]));
        else if (existentes.Count > 1) partes.Add(Idioma.F("{0} já eram projetos.", existentes.Count));
        if (falhas.Count == 1) partes.Add(Idioma.F("Não deu para adicionar {0}.", falhas[0]));
        else if (falhas.Count > 1) partes.Add(Idioma.F("Não deu para adicionar {0} pastas.", falhas.Count));
        if (arquivos == 1) partes.Add(Idioma.T("1 arquivo ignorado: só pastas viram projeto."));
        else if (arquivos > 1) partes.Add(Idioma.F("{0} arquivos ignorados: só pastas viram projeto.", arquivos));
        return string.Join(" ", partes);
    }

    public void SoltarPastas(IReadOnlyList<string> caminhos)
    {
        var novos = new List<AmbienteVM>();
        var existentes = new List<AmbienteVM>();
        var falhas = new List<string>();
        var arquivos = 0;
        foreach (var caminho in caminhos.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(caminho))
            {
                arquivos++;
                continue;
            }
            var limpa = Path.GetFullPath(caminho).TrimEnd('\\', '/');
            var antes = AmbienteDe(limpa);
            var ambiente = AdicionarPastaComoProjeto(limpa);
            if (ambiente is null) falhas.Add(Path.GetFileName(limpa) is { Length: > 0 } n ? n : limpa);
            else if (novos.Contains(ambiente) || existentes.Contains(ambiente)) continue;
            else if (antes is not null) existentes.Add(ambiente);
            else novos.Add(ambiente);
        }
        var todos = novos.Concat(existentes).ToList();
        _destacados.Clear();
        foreach (var a in todos) _destacados.Add(a);
        MostrarRecado(Recado([.. novos.Select(a => a.Nome)], [.. existentes.Select(a => a.Nome)], arquivos, falhas));
        if (todos.Count == 1) Navegar(todos[0]);
        else if (todos.Count > 1)
        {
            Navegar(PaginaProjetos);
            PaginaProjetos.Atualizar();
        }
        else if (Ativo is ProjetosVM projetos) projetos.Atualizar();
    }

    void MostrarRecado(string texto)
    {
        RecadoSoltar = texto;
        _relogioRecado?.Stop();
        if (texto.Length == 0) return;
        _relogioRecado ??= new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, (_, _) => ApagarRecado());
        _relogioRecado.Start();
    }

    void ApagarRecado()
    {
        _relogioRecado?.Stop();
        RecadoSoltar = "";
        if (_destacados.Count == 0) return;
        _destacados.Clear();
        if (Ativo is ProjetosVM projetos) projetos.Atualizar();
    }
}
