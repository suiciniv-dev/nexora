using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Agenda;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class AgendadosVM : ObservableObject
{
    readonly JanelaVM _janela;

    public AgendadosVM(JanelaVM janela)
    {
        _janela = janela;
        Recarregar();
    }

    public ObservableCollection<AgendamentoVM> Itens { get; } = [];
    public ObservableCollection<DiaDaAgendaVM> Dias { get; } =
        [.. Agenda.Semana.Select(d => new DiaDaAgendaVM(d, d is not (DayOfWeek.Saturday or DayOfWeek.Sunday)))];

    public IReadOnlyList<Local> Projetos => _janela.Locais();
    public bool Vazio => Itens.Count == 0;

    [ObservableProperty]
    public partial Local? NovoProjeto { get; set; }

    [ObservableProperty]
    public partial string NovoNome { get; set; } = "";

    [ObservableProperty]
    public partial string NovaHora { get; set; } = "09:00";

    [ObservableProperty]
    public partial string NovoTexto { get; set; } = "";

    [ObservableProperty]
    public partial string? Erro { get; set; }

    [ObservableProperty]
    public partial string? Confirmacao { get; set; }

    static readonly List<Agendamento> Demonstracao =
    [
        new("d1", Idioma.T("Revisar os PRs da manhã"), @"D:\Projetos\Loja", Idioma.T("Liste os PRs abertos que esperam a minha revisão e resuma o que cada um muda."),
            new TimeOnly(9, 0), [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday], true, DateTime.Today.AddHours(9).AddMinutes(1)),
        new("d2", Idioma.T("Dependências desatualizadas"), @"D:\Projetos\Loja.Api", Idioma.T("Veja os pacotes NuGet com versão nova e diga quais dá para atualizar sem quebrar nada."),
            new TimeOnly(18, 30), [DayOfWeek.Friday]),
        new("d3", Idioma.T("Testes do app de notas"), @"D:\Projetos\App.Notas", Idioma.T("Rode os testes e, se algum falhar, investigue e me explique a causa."),
            new TimeOnly(13, 0), [DayOfWeek.Monday, DayOfWeek.Wednesday], false),
    ];

    IEnumerable<Agendamento> Fonte => JanelaVM.Demo ? Demonstracao : App.Config.Agendamentos;

    public void Recarregar()
    {
        Itens.Clear();
        foreach (var agendamento in Fonte.OrderBy(a => a.Hora)) Itens.Add(new AgendamentoVM(agendamento, this));
        OnPropertyChanged(nameof(Vazio));
        OnPropertyChanged(nameof(Projetos));
    }

    [RelayCommand]
    void Salvar()
    {
        Erro = Validar(out var hora);
        if (Erro is not null || NovoProjeto is not { } projeto) return;
        var texto = NovoTexto.Trim();
        var nome = NovoNome.Trim() is { Length: > 0 } dado ? dado : Resumos.UmaLinha(texto, 50);
        var novo = new Agendamento(Guid.NewGuid().ToString("N"), nome, projeto.Pasta, texto, hora,
            [.. Dias.Where(d => d.Marcado).Select(d => d.Dia)]);
        if (JanelaVM.Demo)
        {
            Demonstracao.Add(novo);
            Recarregar();
        }
        else
        {
            App.Config.Agendamentos.Add(novo);
            Guardar();
        }
        NovoNome = "";
        NovoTexto = "";
        _ = Confirmar(Agenda.Proxima(novo, DateTime.Now) is { } quando
            ? Idioma.F("Agendado: \"{0}\". Próxima vez {1} às {2:HH:mm}.", nome, Agenda.Curto(quando.DayOfWeek), quando)
            : Idioma.F("Agendado: \"{0}\".", nome));
    }

    async Task Confirmar(string texto)
    {
        Confirmacao = texto;
        await Task.Delay(5000);
        if (Confirmacao == texto) Confirmacao = null;
    }

    string? Validar(out TimeOnly hora)
    {
        hora = default;
        if (NovoProjeto is null) return Idioma.T("Escolha o projeto.");
        if (string.IsNullOrWhiteSpace(NovoTexto)) return Idioma.T("Escreva o pedido que o agente vai receber.");
        if (!TimeOnly.TryParseExact(NovaHora.Trim(), ["HH:mm", "H:mm", "HH'h'mm", "H'h'"], out hora)) return Idioma.T("Use a hora no formato 09:00.");
        if (!Dias.Any(d => d.Marcado)) return Idioma.T("Marque pelo menos um dia.");
        return null;
    }

    internal void Trocar(Agendamento novo)
    {
        var lista = App.Config.Agendamentos;
        var indice = lista.FindIndex(a => a.Id == novo.Id);
        if (indice < 0) return;
        lista[indice] = novo;
        Guardar();
    }

    internal void Excluir(Agendamento agendamento)
    {
        App.Config.Agendamentos.RemoveAll(a => a.Id == agendamento.Id);
        Guardar();
    }

    internal void Rodar(Agendamento agendamento) => _janela.RodarAgendamento(agendamento);

    void Guardar()
    {
        if (!JanelaVM.Isolada) App.Config.Salvar();
        Recarregar();
    }
}
