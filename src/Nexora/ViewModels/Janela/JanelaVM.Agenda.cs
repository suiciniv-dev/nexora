using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Agenda;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class JanelaVM
{
    DispatcherTimer? _relogioAgenda;
    AgendadosVM? _paginaAgendados;

    public AgendadosVM PaginaAgendados => _paginaAgendados ??= new AgendadosVM(this);
    public bool NaAgendados => Ativo is AgendadosVM;

    [RelayCommand]
    void MostrarAgendados() => Navegar(PaginaAgendados);

    ConfiguracoesVM? _paginaConfiguracoes;
    public ConfiguracoesVM PaginaConfiguracoes => _paginaConfiguracoes ??= new ConfiguracoesVM(this);
    public bool NaConfiguracoes => Ativo is ConfiguracoesVM;

    [RelayCommand]
    void MostrarConfiguracoes()
    {
        PaginaConfiguracoes.Recarregar();
        Navegar(PaginaConfiguracoes);
    }

    public void IniciarAgenda()
    {
        if (Isolada || Demo || _relogioAgenda is not null) return;
        _relogioAgenda = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) => VerificarAgenda());
        _relogioAgenda.Start();
        Dispatcher.UIThread.Post(VerificarAgenda, DispatcherPriority.Background);
    }

    void VerificarAgenda()
    {
        var agora = DateTime.Now;
        foreach (var agendamento in App.Config.Agendamentos.ToList())
            if (Agenda.Vencido(agendamento, agora)) RodarAgendamento(agendamento);
    }

    public ChatVM? RodarAgendamento(Agendamento agendamento)
    {
        var lista = App.Config.Agendamentos;
        var indice = lista.FindIndex(a => a.Id == agendamento.Id);
        if (indice >= 0) lista[indice] = lista[indice] with { UltimaExecucao = DateTime.Now };
        if (!Isolada) App.Config.Salvar();
        _paginaAgendados?.Recarregar();
        if (!Directory.Exists(agendamento.Pasta))
        {
            Avisos.Notificar(Idioma.T("Agendado não rodou"), Idioma.F("{0}: a pasta {1} não existe mais.", agendamento.Nome, agendamento.Pasta));
            return null;
        }
        var chat = CriarSessao(LocalDe(agendamento.Pasta));
        chat.Titulo = agendamento.Nome;
        chat.EnviarTexto(agendamento.Texto);
        Avisos.Notificar(Idioma.T("Agendado começou"), $"{agendamento.Nome} · {Path.GetFileName(agendamento.Pasta.TrimEnd('\\', '/'))}");
        return chat;
    }
}
