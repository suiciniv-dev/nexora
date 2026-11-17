using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.Core.Servidor;

public sealed class SessaoRemota : ISessaoAgente, IHistoricoDoAgente
{
    readonly NucleoRemoto _nucleo;
    readonly OpcoesSessao? _opcoes;
    readonly string? _titulo;
    readonly object _trava = new();
    long _ultimo;
    bool _ativa;
    bool _encerrou;

    public SessaoRemota(NucleoRemoto nucleo, string agente, OpcoesSessao opcoes, string? titulo = null)
    {
        _nucleo = nucleo;
        _opcoes = opcoes;
        _titulo = titulo;
        Id = Guid.NewGuid().ToString("N");
        Agente = agente;
        SessaoId = opcoes.Retomar;
        nucleo.Caiu += AoCair;
    }

    SessaoRemota(NucleoRemoto nucleo, ResumoDaSessao resumo)
    {
        _nucleo = nucleo;
        Id = resumo.Id;
        Agente = resumo.Agente;
        SessaoId = resumo.SessaoId;
        _ultimo = resumo.Ultimo;
        _ativa = resumo.Ativa;
        Pid = resumo.Pid;
        nucleo.Caiu += AoCair;
    }

    public event Action<EventoClaude>? Evento;
    public event Action<int, string>? Encerrou;

    public string Id { get; }
    public string Agente { get; }
    public string? SessaoId { get; private set; }
    public int? Pid
    {
        get => _pid;
        private set
        {
            if (_pid is { } antigo) SessaoClaude.Esquecer(antigo);
            _pid = value;
            if (value is { } novo && Ativa) SessaoClaude.Registrar(novo);
        }
    }

    int? _pid;
    public bool Ativa => Volatile.Read(ref _ativa);
    public bool TemRemoto => Agente == Agentes.Claude;
    public bool TemControles => Agente == Agentes.Claude;

    public static SessaoRemota Anexar(NucleoRemoto nucleo, ResumoDaSessao resumo) => new(nucleo, resumo);

    public async Task<RespostaDeAssinatura?> AssinarAsync()
    {
        _nucleo.Ouvir(Id, Receber);
        var estado = await _nucleo.ChamarAsync<RespostaDeAssinatura>(MetodosDoNucleo.AssinarSessao, new JsonObject { ["id"] = Id }).ConfigureAwait(false);
        if (estado is null)
        {
            Terminar(-1, Idioma.T("O servidor do Nexora não achou a sessão."));
            return null;
        }
        lock (_trava) _ultimo = Math.Max(_ultimo, estado.Ultimo);
        Volatile.Write(ref _ativa, estado.Ativa);
        if (!estado.Ativa) Terminar(0, "");
        return estado;
    }

    public void Iniciar()
    {
        if (_opcoes is null) return;
        Volatile.Write(ref _ativa, true);
        _nucleo.Ouvir(Id, Receber);
        var dados = new JsonObject
        {
            ["id"] = Id,
            ["agente"] = Agente,
            ["titulo"] = _titulo,
            ["opcoes"] = JsonSerializer.SerializeToNode(_opcoes with { Lancador = null }, ServidorDoNucleo.Json),
        };
        _ = AbrirAsync(dados);
    }

    async Task AbrirAsync(JsonObject dados)
    {
        var abrir = _nucleo.ChamarAsync<ResumoDaSessao>(MetodosDoNucleo.AbrirSessao, dados);
        var assinar = _nucleo.ChamarAsync<RespostaDeAssinatura>(MetodosDoNucleo.AssinarSessao, new JsonObject { ["id"] = Id, ["desde"] = 0 });
        var resumo = await abrir.ConfigureAwait(false);
        if (resumo is not null) Pid = resumo.Pid;
        if (resumo is null || await assinar.ConfigureAwait(false) is null)
            Terminar(-1, Idioma.T("O servidor do Nexora não abriu a sessão."));
    }

    void Receber(long numero, EventoClaude evento)
    {
        lock (_trava)
        {
            if (numero <= _ultimo) return;
            _ultimo = numero;
        }
        switch (evento)
        {
            case SessaoEncerrada fim:
                Terminar(fim.Codigo, fim.Erros);
                return;
            case SessaoIniciada iniciada:
                SessaoId = iniciada.SessaoId;
                break;
        }
        Evento?.Invoke(evento);
    }

    void AoCair() => Terminar(-1, Idioma.T("O servidor do Nexora parou."));

    void Terminar(int codigo, string erros)
    {
        lock (_trava)
        {
            if (_encerrou) return;
            _encerrou = true;
        }
        Volatile.Write(ref _ativa, false);
        if (_pid is { } pid) SessaoClaude.Esquecer(pid);
        _nucleo.Esquecer(Id);
        _nucleo.Caiu -= AoCair;
        Encerrou?.Invoke(codigo, erros);
    }

    void Chamar(string metodo, JsonObject dados)
    {
        dados["id"] = Id;
        _ = _nucleo.ChamarAsync<RespostaVazia>(metodo, dados);
    }

    void Agir(string acao, JsonObject? dados = null) =>
        Chamar(MetodosDoNucleo.AcaoNaSessao, new JsonObject { ["acao"] = acao, ["dados"] = dados ?? [] });

    public void Enviar(string texto, IReadOnlyList<Anexo>? imagens = null) =>
        Chamar(MetodosDoNucleo.EnviarASessao, new JsonObject { ["texto"] = texto, ["imagens"] = imagens is { Count: > 0 } ? JsonSerializer.SerializeToNode(imagens, ServidorDoNucleo.Json) : null });

    public void Permitir(string pedidoId, JsonNode? entrada, JsonNode? regras = null) =>
        Agir(AcoesDaSessao.Permitir, new JsonObject { ["pedido"] = pedidoId, ["entrada"] = entrada?.DeepClone(), ["regras"] = regras?.DeepClone() });

    public void Negar(string pedidoId, string motivo) =>
        Agir(AcoesDaSessao.Negar, new JsonObject { ["pedido"] = pedidoId, ["motivo"] = motivo });

    public void LiberarSubagente(string pedidoId, JsonNode? entrada) =>
        Agir(AcoesDaSessao.LiberarSubagente, new JsonObject { ["pedido"] = pedidoId, ["entrada"] = entrada?.DeepClone() });

    public void BarrarSubagente(string pedidoId, string motivo) =>
        Agir(AcoesDaSessao.BarrarSubagente, new JsonObject { ["pedido"] = pedidoId, ["motivo"] = motivo });

    public void Interromper() => Agir(AcoesDaSessao.Interromper);

    public void MudarModo(string modo) => Agir(AcoesDaSessao.Modo, new JsonObject { ["modo"] = modo });

    public void MudarModelo(string? modelo) => Agir(AcoesDaSessao.Modelo, new JsonObject { ["modelo"] = modelo });

    public void AplicarConfiguracoes(JsonObject configuracoes) =>
        Agir(AcoesDaSessao.Configuracoes, new JsonObject { ["configuracoes"] = configuracoes.DeepClone() });

    public async Task<ControleRespondido> PedirAsync(JsonObject pedido, TimeSpan? limite = null)
    {
        var dados = new JsonObject { ["id"] = Id, ["pedido"] = pedido.DeepClone(), ["segundos"] = limite?.TotalSeconds };
        var espera = (limite ?? TimeSpan.FromSeconds(45)) + TimeSpan.FromSeconds(15);
        return await _nucleo.ChamarAsync<ControleRespondido>(MetodosDoNucleo.PedirASessao, dados, espera).ConfigureAwait(false)
            ?? new ControleRespondido("", false, Idioma.T("O servidor do Nexora não respondeu."), null);
    }

    public async Task<ControleRespondido> LigarRemotoAsync(bool ligado) =>
        await _nucleo.ChamarAsync<ControleRespondido>(MetodosDoNucleo.RemotoDaSessao, new JsonObject { ["id"] = Id, ["ligado"] = ligado }).ConfigureAwait(false)
        ?? new ControleRespondido("", false, Idioma.T("O servidor do Nexora não respondeu."), null);

    public async Task<IReadOnlyList<EventoClaude>> LerHistoricoAsync(TimeSpan? espera = null) =>
        await _nucleo.ChamarAsync<List<EventoClaude>>(MetodosDoNucleo.HistoricoDaSessao, new JsonObject { ["id"] = Id }, espera).ConfigureAwait(false) ?? [];

    public void Soltar()
    {
        if (_pid is { } pid) SessaoClaude.Esquecer(pid);
        _nucleo.Esquecer(Id);
        _nucleo.Caiu -= AoCair;
        _ = _nucleo.ChamarAsync<RespostaVazia>(MetodosDoNucleo.SoltarSessao, new JsonObject { ["id"] = Id });
    }

    public void Encerrar() => Agir(AcoesDaSessao.Encerrar);

    public ValueTask DisposeAsync()
    {
        Agir(AcoesDaSessao.Encerrar);
        _ = _nucleo.ChamarAsync<RespostaVazia>(MetodosDoNucleo.SoltarSessao, new JsonObject { ["id"] = Id });
        if (_pid is { } pid) SessaoClaude.Esquecer(pid);
        _nucleo.Esquecer(Id);
        _nucleo.Caiu -= AoCair;
        return ValueTask.CompletedTask;
    }
}
