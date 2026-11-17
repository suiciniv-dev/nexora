using Nexora.Core.Claude;
using Nexora.Core.Git;
using Nexora.Core.Historico;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    static readonly HashSet<string> Podados = new(StringComparer.OrdinalIgnoreCase);
    static readonly TimeSpan IdadeDosPontos = TimeSpan.FromDays(14);

    readonly List<ItemTurno> _turnos = [];
    readonly SemaphoreSlim _gravandoRefs = new(1, 1);
    ItemTurno? _turnoAberto;
    int _proximoTurno = 1;
    bool _desfazendo;
    bool _encerrada;

    public IReadOnlyList<ItemTurno> Turnos => _turnos;

    bool GuardaTurnos => !JanelaVM.Demo && !EhLateral && PontosDeRestauracao.Raiz(Pasta) is not null;

    void Podar(string pasta)
    {
        if (PontosDeRestauracao.Raiz(pasta) is not { } raiz) return;
        lock (Podados)
            if (!Podados.Add(raiz)) return;
        _ = Task.Run(() => PontosDeRestauracao.PodarAsync(raiz, IdadeDosPontos));
    }

    ItemTurno NovoTurno(int numero, ItemUsuario mensagem) => new(numero, mensagem, VerTurno, DesfazerTurnoAsync);

    async Task EnviarComPontoAsync(string texto, IReadOnlyList<Anexo> imagens, ItemUsuario mensagem)
    {
        if (Estado != EstadoChat.PrecisaDeVoce) Estado = EstadoChat.Trabalhando;
        try
        {
            GarantirSessao();
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
        var turno = NovoTurno(_proximoTurno++, mensagem);
        _turnoAberto = turno;
        turno.Marcacao = MarcarAsync(turno, texto);
        await turno.Marcacao;
        if (_encerrada) return;
        Mandar(texto, imagens);
    }

    async Task MarcarAsync(ItemTurno turno, string texto)
    {
        var (pasta, sessao, codex) = (Pasta, SessaoId, !EhClaude);
        Podar(pasta);
        try
        {
            var (commit, transcricao, bytes) = await Task.Run(async () =>
            {
                string? arquivo = null;
                long tamanho = 0;
                if (!codex && sessao is not null && ConversasSalvas.ArquivoDe(sessao, pasta) is { } achado)
                {
                    arquivo = achado;
                    tamanho = new FileInfo(achado).Length;
                }
                var mensagem = PontosDeRestauracao.Mensagem($"nexora: antes do turno {turno.Numero}", new Dictionary<string, string?>
                {
                    ["turno"] = turno.Numero.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["texto"] = PontosDeRestauracao.Impressao(texto),
                    ["transcricao"] = arquivo,
                    ["bytes"] = tamanho.ToString(System.Globalization.CultureInfo.InvariantCulture),
                });
                return (await PontosDeRestauracao.CapturarAsync(pasta, mensagem), arquivo, tamanho);
            }).WaitAsync(TimeSpan.FromSeconds(15));
            if (commit is null) return;
            turno.Antes = commit;
            turno.Transcricao = transcricao;
            turno.Bytes = bytes;
            _turnos.Add(turno);
            GravarRefs();
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
        }
    }

    void FecharTurno()
    {
        if (_turnoAberto is not { } turno) return;
        _turnoAberto = null;
        if (_sessao is Core.Codex.SessaoCodex codex) turno.TurnoCodex = codex.UltimoTurno;
        Adicionar(turno);
        _ = ConcluirTurnoAsync(turno);
    }

    async Task ConcluirTurnoAsync(ItemTurno turno)
    {
        await turno.Marcacao;
        if (turno.Antes is not { } antes)
        {
            Itens.Remove(turno);
            return;
        }
        var pasta = Pasta;
        var mensagem = PontosDeRestauracao.Mensagem($"nexora: depois do turno {turno.Numero}", new Dictionary<string, string?>
        {
            ["turno"] = turno.Numero.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["codex"] = turno.TurnoCodex,
        });
        var (depois, resumo) = await Task.Run(async () =>
        {
            var commit = await PontosDeRestauracao.CapturarAsync(pasta, mensagem, antes);
            return (commit, commit is null ? null : await PontosDeRestauracao.ContarAsync(pasta, antes, commit));
        });
        if (depois is null || resumo is null || !_turnos.Contains(turno))
        {
            Itens.Remove(turno);
            return;
        }
        turno.Depois = depois;
        turno.Resumo = resumo;
        GravarRefs();
        AtualizarTurnos();
    }

    void GravarRefs()
    {
        if (SessaoId is not { } id || !PontosDeRestauracao.IdValido(id)) return;
        var refs = _turnos
            .SelectMany(t => new[] { (t.Numero, PontosDeRestauracao.Antes, t.Antes), (t.Numero, PontosDeRestauracao.Depois, t.Depois) })
            .Where(r => r.Item3 is not null)
            .Select(r => (PontosDeRestauracao.RefDe(id, r.Numero, r.Item2), r.Item3!))
            .ToList();
        if (refs.Count == 0) return;
        var pasta = Pasta;
        _ = Task.Run(async () =>
        {
            await _gravandoRefs.WaitAsync();
            try
            {
                await PontosDeRestauracao.GuardarAsync(pasta, refs);
            }
            finally
            {
                _gravandoRefs.Release();
            }
        });
    }

    void AtualizarTurnos()
    {
        var visiveis = _turnos.Where(t => t.Visivel).ToList();
        for (var i = 0; i < visiveis.Count; i++) visiveis[i].Seguintes = visiveis.Count - 1 - i;
        Painel.DefinirTurnos(visiveis);
    }

    void VerTurno(ItemTurno turno)
    {
        Painel.DefinirTurnos(_turnos.Where(t => t.Visivel));
        Painel.MostrarTurno(turno);
        MostrarAbaCommand.Execute("git");
        Painel.VerAlteracoesCommand.Execute(null);
    }

    async Task DesfazerTurnoAsync(ItemTurno turno)
    {
        turno.Confirmando = false;
        if (Trabalhando || Precisa || _turnoAberto is not null)
        {
            Adicionar(ItemAviso.Erro(Idioma.T("Espere o agente terminar o turno antes de desfazer.")));
            return;
        }
        if (_desfazendo || !Itens.Contains(turno.Mensagem)) return;
        if (JanelaVM.Demo)
        {
            Recortar(turno);
            Adicionar(ItemAviso.Info(Idioma.T("No modo demonstração a conversa volta, mas nenhum arquivo é mexido.")));
            return;
        }
        if (turno.Antes is not { } antes) return;
        _desfazendo = true;
        turno.Desfazendo = true;
        try
        {
            if (await VoltarConversaAsync(turno) is not { } volta) return;
            var pasta = Pasta;
            var guardar = SessaoId is { } atual && PontosDeRestauracao.IdValido(atual) ? PontosDeRestauracao.RefDesfeito(atual) : null;
            var r = await Task.Run(() => PontosDeRestauracao.RestaurarAsync(pasta, antes, guardar));
            if (!r.Ok)
            {
                if (volta.Copia is { } copia)
                    try
                    {
                        File.Delete(copia);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                    }
                Adicionar(ItemAviso.Erro(EhCodex
                    ? Idioma.F("A conversa do Codex voltou, mas os arquivos não: {0}.", r.Mensagem)
                    : !EhClaude ? Idioma.F("Não deu para voltar os arquivos: {0}.", r.Mensagem)
                    : Idioma.F("Não deu para voltar os arquivos: {0}.", r.Mensagem)));
                return;
            }
            if (EhClaude) TrocarDeSessao(volta.Sessao);
            var restante = SessaoId;
            Recortar(turno);
            if (restante is not null) _ = Task.Run(() => PontosDeRestauracao.ApagarAsync(pasta, restante, turno.Numero));
            GravarRefs();
            var mexidos = r.Escritos + r.Apagados;
            if (!DescricaoAgente.VoltaConversa)
                Adicionar(ItemAviso.Info(Idioma.F("O {0} não tem como esquecer a conversa: os arquivos voltaram, mas ele ainda lembra do que fez. Conte a ele o que foi desfeito.", NomeAgente)));
            Adicionar(ItemAviso.Info(mexidos == 0
                ? Idioma.T("Turno desfeito. Nenhum arquivo precisou voltar.")
                : mexidos == 1 ? Idioma.T("Turno desfeito. 1 arquivo voltou a como estava.") : Idioma.F("Turno desfeito. {0} arquivos voltaram a como estavam.", mexidos)));
            _ = Painel.AtualizarAlteracoes();
        }
        finally
        {
            _desfazendo = false;
            turno.Desfazendo = false;
        }
    }

    sealed record Volta(string? Sessao, string? Copia);

    async Task<Volta?> VoltarConversaAsync(ItemTurno turno)
    {
        if (!DescricaoAgente.VoltaConversa) return new Volta(SessaoId, null);
        if (EhCodex)
        {
            if (turno.TurnoCodex is not { } idCodex || SessaoId is null)
            {
                Adicionar(ItemAviso.Erro(Idioma.T("O Codex não informou este turno, então a conversa não tem como voltar. Nenhum arquivo foi alterado.")));
                return null;
            }
            ISessaoAgente sessao;
            try
            {
                sessao = GarantirSessao();
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Adicionar(ItemAviso.Erro(ex.Message));
                return null;
            }
            var erro = sessao is Core.Codex.SessaoCodex codex ? await codex.VoltarAntesDeAsync(idCodex) : Idioma.T("O Codex não abriu a conversa.");
            if (erro is not null)
            {
                Adicionar(ItemAviso.Erro(Idioma.F("O Codex não deixou voltar a conversa ({0}). Nenhum arquivo foi alterado.", erro)));
                return null;
            }
            return new Volta(SessaoId, null);
        }
        if (turno.Bytes <= 0) return new Volta(null, null);
        if (turno.Transcricao is not { } arquivo || !File.Exists(arquivo))
        {
            Adicionar(ItemAviso.Erro(Idioma.T("Não achei o histórico desta conversa para voltar. Nenhum arquivo foi alterado.")));
            return null;
        }
        var novo = Guid.NewGuid().ToString();
        if (await Task.Run(() => Transcricao.CopiarAte(arquivo, turno.Bytes, novo)) is not { } copia)
        {
            Adicionar(ItemAviso.Erro(Idioma.T("Não deu para copiar o histórico desta conversa. Nenhum arquivo foi alterado.")));
            return null;
        }
        return new Volta(novo, copia);
    }

    void TrocarDeSessao(string? nova)
    {
        var antiga = SessaoId;
        if (_sessao is { } sessao)
        {
            _sessao = null;
            EnderecoRemoto = null;
            _ = sessao.DisposeAsync().AsTask();
        }
        Bifurcar = false;
        if (antiga is not null && SessaoOrigem is null) SessaoOrigem = antiga;
        SessaoId = nova;
        if (antiga is null || nova is null || JanelaVM.Isolada) return;
        App.Config.TurnosDesfeitos[antiga] = nova;
        App.Config.Salvar();
    }

    void Recortar(ItemTurno turno)
    {
        var indice = Itens.IndexOf(turno.Mensagem);
        if (indice < 0) return;
        while (Itens.Count > indice) Itens.RemoveAt(Itens.Count - 1);
        _turnos.RemoveAll(t => t.Numero >= turno.Numero);
        _ferramentas.Clear();
        _textoAtual = null;
        PermissaoPendente = null;
        SegredoPendente = Itens.OfType<ItemSegredo>().FirstOrDefault(i => i.Pendente);
        RefazerSaidas();
        AtualizarTurnos();
        if (string.IsNullOrWhiteSpace(Rascunho))
        {
            Rascunho = turno.Mensagem.Texto;
            if (turno.Mensagem.Citacao is { } citacao) ResponderA(citacao.Texto);
        }
        if (Estado == EstadoChat.Erro) Estado = EstadoChat.Pronta;
    }

    void RefazerSaidas()
    {
        Saidas.Clear();
        _ultimaEdicao.Clear();
        foreach (var f in Itens.OfType<ItemFerramenta>()) RegistrarSaida(f);
        AvisarSaidas();
    }

    IReadOnlyList<PontoSalvo>? _pontosDoHistorico;

    async Task CarregarTurnosAsync()
    {
        if (!GuardaTurnos || !EhClaude || SessaoId is not { } id || !PontosDeRestauracao.IdValido(id)) return;
        var pasta = Pasta;
        Podar(pasta);
        var pontos = await Task.Run(() => PontosDeRestauracao.ListarAsync(pasta, id));
        if (pontos.Count == 0) return;
        _proximoTurno = Math.Max(_proximoTurno, pontos.Max(p => p.Turno) + 1);
        _pontosDoHistorico = pontos;
        await EncaixarTurnosAsync();
    }

    void EncaixarTurnos() => _ = EncaixarTurnosAsync();

    async Task EncaixarTurnosAsync()
    {
        if (_pontosDoHistorico is not { } pontos) return;
        var pasta = Pasta;
        var encaixados = _turnos.Where(t => pontos.Any(p => p.Turno == t.Numero)).ToList();
        var menorTurno = encaixados.Count == 0 ? int.MaxValue : encaixados.Min(t => t.Numero);
        var limite = encaixados.Count == 0 ? Itens.Count : encaixados.Select(t => Itens.IndexOf(t.Mensagem)).Where(i => i >= 0).DefaultIfEmpty(Itens.Count).Min();
        var antes = pontos.Where(p => p.Tipo == PontosDeRestauracao.Antes && p.Turno < menorTurno).OrderByDescending(p => p.Turno).ToList();
        var mensagens = Itens.Take(limite).OfType<ItemUsuario>().ToList();
        var achados = new List<ItemTurno>();
        var m = mensagens.Count - 1;
        foreach (var ponto in antes)
        {
            if (!ponto.Dados.TryGetValue("texto", out var impressao)) continue;
            var i = m;
            while (i >= 0 && PontosDeRestauracao.Impressao(mensagens[i].Enviado) != impressao) i--;
            if (i < 0) continue;
            m = i - 1;
            var turno = NovoTurno(ponto.Turno, mensagens[i]);
            turno.Antes = ponto.Commit;
            turno.Depois = pontos.FirstOrDefault(p => p.Turno == ponto.Turno && p.Tipo == PontosDeRestauracao.Depois)?.Commit;
            turno.Transcricao = ponto.Dados.GetValueOrDefault("transcricao");
            turno.Bytes = long.TryParse(ponto.Dados.GetValueOrDefault("bytes"), out var b) ? b : 0;
            if (turno.Depois is not null) achados.Add(turno);
        }
        if (achados.Count == 0) return;
        achados.Reverse();
        foreach (var turno in achados)
        {
            var depoisDaMensagem = Itens.IndexOf(turno.Mensagem) + 1;
            var proxima = Itens.Skip(depoisDaMensagem).FirstOrDefault(i => i is ItemUsuario);
            var lugar = proxima is not null ? Itens.IndexOf(proxima) : Itens.Count > 0 && Itens[^1] is ItemAviso { EhInfo: true } ? Itens.Count - 1 : Itens.Count;
            Itens.Insert(lugar, turno);
            _turnos.Add(turno);
        }
        var resumos = await Task.Run(async () =>
        {
            var lista = new List<ResumoDoTurno?>();
            foreach (var turno in achados) lista.Add(await PontosDeRestauracao.ContarAsync(pasta, turno.Antes!, turno.Depois!));
            return lista;
        });
        for (var i = 0; i < achados.Count; i++)
            if (resumos[i] is { } resumo) achados[i].Resumo = resumo;
        AtualizarTurnos();
    }

    public void AdicionarTurnoDemo(ItemUsuario mensagem, IReadOnlyList<ArquivoDoTurno> arquivos)
    {
        var turno = NovoTurno(_proximoTurno++, mensagem);
        turno.Resumo = new ResumoDoTurno(Pasta, arquivos);
        _turnos.Add(turno);
        Itens.Add(turno);
        AtualizarTurnos();
    }
}
