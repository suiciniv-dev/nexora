using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Agenda;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class AgendamentoVM(Agendamento dados, AgendadosVM pagina) : ObservableObject
{
    public Agendamento Dados { get; } = dados;
    public string Nome => Dados.Nome;
    public string Projeto => Path.GetFileName(Dados.Pasta.TrimEnd('\\', '/'));
    public string Texto => Dados.Texto;
    public string Quando => Agenda.Descrever(Dados);
    public bool Ativo => Dados.Ativo;

    public string Situacao
    {
        get
        {
            var proxima = Agenda.Proxima(Dados, DateTime.Now) is { } quando ? Idioma.F("próxima: {0} {1:dd/MM HH:mm}", Agenda.Curto(quando.DayOfWeek), quando) : Idioma.T("desligado");
            return Dados.UltimaExecucao is { } ultima ? Idioma.F("{0}  ·  rodou {1:dd/MM HH:mm}", proxima, ultima) : proxima;
        }
    }

    [RelayCommand]
    void Alternar() => pagina.Trocar(Dados with { Ativo = !Dados.Ativo });

    [RelayCommand]
    void RodarAgora() => pagina.Rodar(Dados);

    [RelayCommand]
    void Excluir() => pagina.Excluir(Dados);
}
