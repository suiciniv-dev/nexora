using Nexora.Core.Painel;

namespace Nexora.ViewModels;

public partial class JanelaVM
{
    static readonly TimeSpan IntervaloDeGravacao = TimeSpan.FromMinutes(2);

    RegistroDoPainel? _registro;
    DateTime? _ativoDesde;
    DateTime _registroGravadoEm = DateTime.Now;
    bool _registroLigado;

    public RegistroDoPainel Registro => _registro ??= Demo ? RegistroDemo(DateTime.Now) : Isolada ? new RegistroDoPainel() : RegistroDoPainel.Ler(ArquivoDoRegistro);

    public event Action? RegistroMudou;

    DashboardVM? _paginaDashboard;
    public DashboardVM PaginaDashboard => _paginaDashboard ??= new DashboardVM(this);
    public bool NaDashboard => Ativo is DashboardVM;

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    void MostrarDashboard() => Navegar(PaginaDashboard);

    public IEnumerable<ConversaVM> ConversasRecentes => _todas;

    static string ArquivoDoRegistro => RegistroDoPainel.Caminho(Core.DadosDoNexora.Pasta);

    bool GravaRegistro => !Demo && !Isolada;

    public void LigarRegistro()
    {
        if (_registroLigado || !GravaRegistro) return;
        _registroLigado = true;
        Pulso.Instancia.AtividadeMudou += MarcarAtividade;
        ChatVM.TurnoTerminou += AoTerminarTurno;
        MarcarAtividade();
    }

    void AoTerminarTurno(TimeSpan duracao)
    {
        Registro.SomarTurno(DateTime.Now, duracao);
        RegistroMudou?.Invoke();
    }

    void MarcarAtividade()
    {
        var agora = DateTime.Now;
        if (Pulso.Instancia.Rodando) _ativoDesde ??= agora;
        else FecharAtivo(agora);
    }

    void FecharAtivo(DateTime agora)
    {
        if (_ativoDesde is not { } desde) return;
        if (desde.Date < agora.Date)
        {
            Registro.SomarAtivo(desde, agora.Date - desde);
            desde = agora.Date;
        }
        Registro.SomarAtivo(desde, agora - desde);
        _ativoDesde = Pulso.Instancia.Rodando ? agora : null;
    }

    public void AnotarNoRegistro()
    {
        if (!GravaRegistro) return;
        var agora = DateTime.Now;
        FecharAtivo(agora);
        Registro.AnotarPlano(new PontoDoPlano(DateTimeOffset.Now, PlanoSessao, PlanoSemana, PlanoCodex?.Sessao, PlanoCodex?.Semana));
        RegistroMudou?.Invoke();
        if (agora - _registroGravadoEm >= IntervaloDeGravacao) GravarRegistro();
    }

    public void GravarRegistro()
    {
        if (!GravaRegistro || _registro is null) return;
        FecharAtivo(DateTime.Now);
        _registroGravadoEm = DateTime.Now;
        _registro.Podar(DateTime.Now);
        try
        {
            _registro.Gravar(ArquivoDoRegistro);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    static RegistroDoPainel RegistroDemo(DateTime agora)
    {
        var registro = new RegistroDoPainel();
        var sorte = new Random(11);
        var hoje = DateOnly.FromDateTime(agora);
        for (var i = 0; i < 150; i++)
        {
            var dia = hoje.AddDays(-i);
            if (dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday && sorte.NextDouble() < 0.7) continue;
            registro.Dias[RegistroDoPainel.Chave(dia)] = new DiaDeUso
            {
                Ativo = 3600 * (1.5 + 5 * sorte.NextDouble()),
                Agentes = 3600 * (0.8 + 3.5 * sorte.NextDouble()),
                Turnos = 8 + sorte.Next(40),
            };
        }
        var inicio = new DateTimeOffset(agora) - TimeSpan.FromHours(24);
        double sessao = 0.05, semana = 0.29, codex = 0.55, codexSemana = 0.36;
        for (var hora = inicio; hora <= new DateTimeOffset(agora); hora += TimeSpan.FromMinutes(20))
        {
            sessao = hora.Hour is 4 or 9 or 14 or 19 && hora.Minute < 20 ? 0.02 : Math.Min(0.95, sessao + 0.025 * sorte.NextDouble());
            semana = Math.Min(1, semana + 0.0015 * sorte.NextDouble());
            codex = Math.Min(0.9, codex + 0.004 * sorte.NextDouble());
            codexSemana = Math.Min(1, codexSemana + 0.0008 * sorte.NextDouble());
            registro.Plano.Add(new PontoDoPlano(hora, sessao, semana, codex, codexSemana));
        }
        return registro;
    }
}
