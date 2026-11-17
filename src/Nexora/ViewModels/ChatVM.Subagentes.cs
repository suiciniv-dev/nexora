using Avalonia.Threading;
using Nexora.Core.Claude;
using Nexora.Core.Historico;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    readonly Dictionary<string, ItemSubagente> _subagentes = [];
    readonly Dictionary<string, ItemSubagente> _rotasDeSubagente = [];
    readonly Dictionary<string, ItemFerramenta> _ferramentasDeSubagente = [];
    readonly Dictionary<string, ItemSubagente> _subagenteDaFerramenta = [];
    DispatcherTimer? _relogioSubagentes;
    IDisposable? _esperaDoFundo;

    public static TimeSpan EsperaDepoisDoFundo { get; set; } = TimeSpan.FromSeconds(5);

    public static bool EhSubagente(string nome) => nome is "Agent" or "Task";

    public IReadOnlyCollection<ItemSubagente> Subagentes => _subagentes.Values;
    public System.Collections.ObjectModel.ObservableCollection<ItemSubagente> SubagentesAtivos { get; } = [];
    public System.Collections.ObjectModel.ObservableCollection<ItemSubagente> PainelSubagentes { get; } = [];
    int _antigosNoPainel = -1;
    public bool TemSubagentes => PainelSubagentes.Count > 0;
    public string TextoBotaoSubagentes => SubagentesRodando > 0 ? SubagentesRodando.ToString() : PainelSubagentes.Count.ToString();

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool MenuSubagentesAberto { get; set; }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    void AbrirSubagentes() => MenuSubagentesAberto = !MenuSubagentesAberto;

    public int SubagentesRodando => _subagentes.Values.Count(a => a.Rodando);
    public bool TemSubagentesRodando => SubagentesRodando > 0;
    public string TextoSubagentes => SubagentesRodando == 1 ? Idioma.T("1 subagent rodando") : Idioma.F("{0} subagents rodando", SubagentesRodando);

    int SubagentesNoFundo => _subagentes.Values.Count(a => a.Estado == EstadoSubagente.SegundoPlano);

    bool TemTrabalhoNoFundo => SubagentesNoFundo > 0;

    bool SoNoFundo => Estado == EstadoChat.Trabalhando && TurnoDesde is null && (TemTrabalhoNoFundo || _esperaDoFundo is not null);

    bool EsperandoSubagente => _subagentes.Values.Any(a => a.Estado == EstadoSubagente.Rodando);

    string TextoDoFundo => SubagentesNoFundo switch
    {
        0 => Idioma.T("Trabalhando"),
        1 => Idioma.T("Esperando 1 subagent em segundo plano"),
        var n => Idioma.F("Esperando {0} subagents em segundo plano", n),
    };

    void ComecarTurnoDoFundo(EventoClaude evento)
    {
        if (!SoNoFundo) return;
        if (evento is not (PensamentoComecou or Pensando or TextoParcial or TextoCompleto or FerramentaComecou or FerramentaChamada or ContextoUsado)) return;
        CancelarEsperaDoFundo();
        _respondeuNoTurno = false;
        _modeloQueRespondeu = null;
        TurnoDesde = DateTime.Now;
        OnPropertyChanged(nameof(AndamentoTexto));
    }

    void SegurarNoFundo()
    {
        Atividade($"{TextoDoFundo}: {_subagentes.Values.First(a => a.Estado == EstadoSubagente.SegundoPlano).Descricao}");
        OnPropertyChanged(nameof(AndamentoTexto));
    }

    void EsperarNovoTurno()
    {
        if (TemTrabalhoNoFundo || EsperandoSubagente || TurnoDesde is not null || Estado != EstadoChat.Trabalhando || _esperaDoFundo is not null) return;
        _esperaDoFundo = DispatcherTimer.RunOnce(() =>
        {
            _esperaDoFundo = null;
            if (TemTrabalhoNoFundo || EsperandoSubagente || TurnoDesde is not null || Estado != EstadoChat.Trabalhando) return;
            LiberarDoFundo();
        }, EsperaDepoisDoFundo, DispatcherPriority.Background);
        OnPropertyChanged(nameof(AndamentoTexto));
    }

    void LiberarDoFundo()
    {
        CancelarEsperaDoFundo();
        AtividadeEm = DateTime.Now;
        Estado = EstadoChat.Pronta;
        OnPropertyChanged(nameof(AndamentoTexto));
        AoEncerrarTurno(false);
        SoltarFilaDepoisDoTurno(false);
    }

    void CancelarEsperaDoFundo()
    {
        _esperaDoFundo?.Dispose();
        _esperaDoFundo = null;
    }

    void ZerarSegundoPlano()
    {
        CancelarEsperaDoFundo();
        PararSubagentes(true);
        AvisarSubagentes();
    }

    bool AplicarSubagente(EventoClaude evento)
    {
        switch (evento)
        {
            case FerramentaChamada chamada when EhSubagente(chamada.Nome):
                DescarregarParcial();
                FecharPensamento();
                _textoAtual = null;
                var agente = AbrirSubagente(chamada, animar: true);
                MudarFase(FaseTurno.Ferramenta, RotuloDaFase());
                Atividade(Idioma.F("Subagent {0}: {1}", agente.Tipo, agente.Descricao));
                return true;
            case FerramentaRespondeu resposta when _subagentes.TryGetValue(resposta.Id, out var aberto):
                ConcluirSubagente(aberto, resposta);
                VoltarDaFerramenta();
                AtualizarBranch();
                return true;
            case EventoDeSubagente interno:
                AplicarNoSubagente(interno, vivo: true);
                return true;
            case TarefaAtualizada tarefa:
                AplicarTarefa(tarefa);
                return true;
            case SubagentePedido pedido:
                PedirSubagente(pedido);
                return true;
            default:
                return false;
        }
    }

    void PedirSubagente(SubagentePedido pedido)
    {
        if (_sessao is not { } sessao) return;
        ItemSubagente? agente = null;
        if (pedido.FerramentaId is { } id) _subagentes.TryGetValue(id, out agente);
        var escolha = App.Config.ModeloDosSubagentes;
        if (escolha != VagasDeSubagentes.Perguntar || agente is null)
        {
            Reservar(sessao, pedido, agente, escolha == VagasDeSubagentes.Perguntar ? VagasDeSubagentes.Conversa : escolha);
            return;
        }
        agente.Espera = EsperaDoSubagente.Modelo;
        Atividade(Idioma.F("Subagent {0}: escolha o modelo", agente.Descricao));
        void Escolheu(string? modelo)
        {
            agente.ModeloEscolhido -= Escolheu;
            Reservar(sessao, pedido, agente, modelo ?? VagasDeSubagentes.Conversa);
        }
        agente.ModeloEscolhido += Escolheu;
    }

    void Reservar(ISessaoAgente sessao, SubagentePedido pedido, ItemSubagente? agente, string escolha)
    {
        var entrada = EntradaDoSubagente(pedido.Entrada, escolha);
        if (agente is not null && entrada is not null)
            agente.ModeloPedido = escolha == VagasDeSubagentes.Conversa ? Modelo : CartaoVM.NomeDoModelo(escolha);
        void Liberar()
        {
            if (agente is not null)
            {
                agente.Espera = EsperaDoSubagente.Nenhuma;
                agente.Inicio = DateTime.Now;
            }
            sessao.LiberarSubagente(pedido.PedidoId, entrada);
        }
        if (agente is null || !agente.Rodando)
        {
            Liberar();
            return;
        }
        if (VagasDeSubagentes.Pedir(this, agente.Id, Liberar)) return;
        agente.Espera = EsperaDoSubagente.Vaga;
        Atividade(Idioma.F("Subagent na fila: {0} de {1} rodando no Nexora", VagasDeSubagentes.EmUso, VagasDeSubagentes.Limite));
    }

    public static System.Text.Json.Nodes.JsonNode? EntradaDoSubagente(System.Text.Json.JsonElement entrada, string escolha)
    {
        var fixo = VagasDeSubagentes.ModeloFixo(escolha);
        if (fixo is null && escolha != VagasDeSubagentes.Conversa) return null;
        var no = Protocolo.ParaNo(entrada) as System.Text.Json.Nodes.JsonObject ?? [];
        if (fixo is null) no.Remove("model");
        else no["model"] = fixo;
        return no;
    }

    void VoltarDaFerramenta()
    {
        if (Fase != FaseTurno.Ferramenta || _ferramentas.Count > 0) return;
        if (EsperandoSubagente) MudarFase(FaseTurno.Ferramenta, RotuloDaFase());
        else MudarFase(FaseTurno.Pensando);
    }

    string RotuloDaFase()
    {
        var rodando = _subagentes.Values.Count(a => a.Estado == EstadoSubagente.Rodando);
        return rodando > 1 ? Idioma.F("{0} subagents", rodando) : Idioma.T("subagent");
    }

    ItemSubagente AbrirSubagente(FerramentaChamada chamada, bool animar)
    {
        var agente = new ItemSubagente(chamada.Id, chamada.Entrada);
        agente.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ItemSubagente.Estado)) return;
            if (!agente.Rodando) VagasDeSubagentes.Soltar(agente.Id);
            AvisarSubagentes();
        };
        _subagentes[chamada.Id] = agente;
        if (_antigosNoPainel >= 0) PainelSubagentes.Insert(_antigosNoPainel++, agente);
        else PainelSubagentes.Add(agente);
        OnPropertyChanged(nameof(TemSubagentes));
        OnPropertyChanged(nameof(TextoBotaoSubagentes));
        _rotasDeSubagente[chamada.Id] = agente;
        if (Destino.LastOrDefault() is ItemSubagentes grupo && grupo.AlgumRodando) grupo.Adicionar(agente);
        else
        {
            var novo = new ItemSubagentes();
            novo.Adicionar(agente);
            if (animar) Adicionar(novo);
            else Destino.Add(novo);
        }
        AvisarSubagentes();
        return agente;
    }

    static string? IdDoAgenteLancado(string conteudo) =>
        System.Text.RegularExpressions.Regex.Match(conteudo, @"agentId:\s*([A-Za-z0-9_-]+)") is { Success: true } achado ? achado.Groups[1].Value : null;

    static void ConcluirSubagente(ItemSubagente agente, FerramentaRespondeu resposta)
    {
        var resumo = resposta.Subagente;
        if (resumo?.EmSegundoPlano == true || resumo is null && resposta.Conteudo.StartsWith("Async agent launched", StringComparison.OrdinalIgnoreCase))
        {
            if (agente.Estado == EstadoSubagente.Rodando) agente.Estado = EstadoSubagente.SegundoPlano;
            agente.AgenteId ??= resumo?.AgenteId ?? IdDoAgenteLancado(resposta.Conteudo);
            return;
        }
        var estado = !resposta.Erro ? EstadoSubagente.Terminou
            : resposta.Conteudo.Contains("interrupted", StringComparison.OrdinalIgnoreCase) ? EstadoSubagente.Parado
            : EstadoSubagente.Erro;
        var (ferramentas, duracao) = Resumos.UsoDoSubagente(resposta.Conteudo);
        agente.Concluir(estado, resumo?.Relato ?? Resumos.ResultadoDeSubagente(resposta.Conteudo), resumo?.Ferramentas ?? ferramentas, resumo?.DuracaoMs ?? duracao, resumo?.Tokens);
    }

    void AplicarNoSubagente(EventoDeSubagente interno, bool vivo)
    {
        if (!_rotasDeSubagente.TryGetValue(interno.PaiId, out var agente)) return;
        switch (interno.Evento)
        {
            case FerramentaChamada chamada:
                var item = ItemFerramenta.EditaArquivo(chamada.Nome)
                    ? CriarFerramenta(chamada)
                    : new ItemFerramenta(chamada.Nome, Resumos.DaFerramenta(chamada.Nome, chamada.Entrada, Pasta), chamada.Entrada);
                _ferramentasDeSubagente[chamada.Id] = item;
                _subagenteDaFerramenta[chamada.Id] = agente;
                if (EhSubagente(chamada.Nome)) _rotasDeSubagente[chamada.Id] = agente;
                agente.Ferramentas.Add(item);
                agente.Atividade = $"{item.Rotulo}  {item.Resumo}";
                RegistrarSaida(item);
                if (vivo) Atividade(Idioma.F("Subagent {0}: {1}  {2}", agente.Tipo, item.Rotulo, item.Resumo));
                break;
            case FerramentaRespondeu resposta when _ferramentasDeSubagente.Remove(resposta.Id, out var feita):
                feita.Concluir(resposta.Conteudo, resposta.Erro);
                break;
            case TextoCompleto texto:
                agente.Atividade = Resumos.UmaLinha(texto.Texto, 220);
                break;
        }
    }

    string? SubagenteDoPedido(PermissaoPedida pedido)
    {
        ItemSubagente? agente = null;
        if (pedido.FerramentaId is { } id) _subagenteDaFerramenta.TryGetValue(id, out agente);
        if (agente is null && pedido.AgenteId is null) return null;
        agente ??= _subagentes.Values.Where(a => a.Rodando).ToList() is [var unico] ? unico : null;
        if (agente is null) return EhCodex ? Idioma.T("Subagent do Codex") : Idioma.T("Subagent");
        agente.Atividade = Idioma.F("Esperando você permitir {0}", Resumos.NomeDaFerramenta(pedido.Ferramenta));
        return Idioma.F("Subagent {0}: {1}", agente.Tipo, agente.Descricao);
    }

    ItemSubagente? SubagenteDaTarefa(string? tarefaId) =>
        tarefaId is { Length: > 0 } ? _subagentes.Values.FirstOrDefault(a => a.AgenteId == tarefaId) : null;

    void AplicarTarefa(TarefaAtualizada tarefa)
    {
        if (!_subagentes.TryGetValue(tarefa.FerramentaId, out var agente)) agente = SubagenteDaTarefa(tarefa.TarefaId);
        if (agente is null) return;
        if (tarefa.Fase == FaseTarefa.Andou)
        {
            if (!agente.Rodando) return;
            if (tarefa.Ferramentas is { } usos) agente.FerramentasInformadas = usos;
            if (tarefa.Resumo is { Length: > 0 } resumo) agente.Atividade = Resumos.UmaLinha(resumo, 220);
            else if (agente.Ferramentas.Count == 0 && tarefa.UltimaFerramenta is { Length: > 0 } ultima) agente.Atividade = Resumos.NomeDaFerramenta(ultima);
            return;
        }
        var estado = tarefa.Estado switch
        {
            "failed" => EstadoSubagente.Erro,
            "stopped" or "killed" => EstadoSubagente.Parado,
            _ => EstadoSubagente.Terminou,
        };
        agente.Concluir(estado, tarefa.Resultado, tarefa.Ferramentas, tarefa.DuracaoMs);
        EsperarNovoTurno();
    }

    void PararSubagentes(bool tambemEmSegundoPlano)
    {
        foreach (var agente in _subagentes.Values.Where(a => a.Estado == EstadoSubagente.Rodando || tambemEmSegundoPlano && a.Rodando).ToList())
            agente.Concluir(EstadoSubagente.Parado, null);
    }

    void AvisarSubagentes()
    {
        foreach (var parado in SubagentesAtivos.Where(a => !a.Rodando).ToList()) SubagentesAtivos.Remove(parado);
        foreach (var novo in _subagentes.Values.Where(a => a.Rodando && !SubagentesAtivos.Contains(a)).OrderBy(a => a.Inicio)) SubagentesAtivos.Add(novo);
        OnPropertyChanged(nameof(SubagentesRodando));
        OnPropertyChanged(nameof(TemSubagentesRodando));
        OnPropertyChanged(nameof(TextoSubagentes));
        OnPropertyChanged(nameof(TextoBotaoSubagentes));
        OnPropertyChanged(nameof(AndamentoTexto));
        if (SubagentesRodando == 0)
        {
            _relogioSubagentes?.Stop();
            return;
        }
        _relogioSubagentes ??= new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            foreach (var agente in _subagentes.Values) agente.Tique();
        });
        if (!_relogioSubagentes.IsEnabled) _relogioSubagentes.Start();
    }

    static Dictionary<string, IReadOnlyList<EventoClaude>> InternosDoHistorico(string arquivo, IReadOnlyList<EventoClaude> eventos) =>
        eventos.OfType<FerramentaRespondeu>()
            .Select(r => r.Subagente?.AgenteId)
            .OfType<string>()
            .Distinct()
            .ToDictionary(id => id, id => Transcricao.LerSubagente(arquivo, id));

    void ConcluirSubagenteDoHistorico(ItemSubagente agente, FerramentaRespondeu resposta, Dictionary<string, IReadOnlyList<EventoClaude>> internos)
    {
        if (resposta.Subagente?.AgenteId is { } id && internos.TryGetValue(id, out var lista))
            foreach (var evento in lista) AplicarNoSubagente(new EventoDeSubagente(agente.Id, evento), vivo: false);
        ConcluirSubagente(agente, resposta);
    }
}
