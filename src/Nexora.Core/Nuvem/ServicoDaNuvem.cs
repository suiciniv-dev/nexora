using System.Text.Json;
using Nexora.Core.Segredos;

namespace Nexora.Core.Nuvem;

public sealed record ExecucaoSalva(
    string Execucao,
    string Maquina,
    string PastaRemota,
    string PastaLocal,
    string Branch,
    string Agente,
    string? SessaoId,
    DateTime Criada);

public sealed class ServicoDaNuvem
{
    public const string VariavelDoClaude = "CLAUDE_CODE_OAUTH_TOKEN";
    const string IdDoToken = "nuvem-claude-token";
    const string IdDaChaveInstaBox = "nuvem-instabox-chave";
    const string IdDaChaveFreestyle = "nuvem-freestyle-chave";

    sealed class Dados
    {
        public List<MaquinaNaNuvem> Maquinas { get; set; } = [];
        public List<ExecucaoSalva> Execucoes { get; set; } = [];
    }

    readonly string? _arquivo;
    readonly IArmazemDeValores? _armazem;
    readonly object _trava = new();
    readonly Dictionary<string, IMaquinaRemota> _abertas = [];
    Dados _dados;

    public ServicoDaNuvem(string? pasta, IArmazemDeValores? armazem, string? pastaDeTeste = null, HttpMessageHandler? manipuladorInstaBox = null, Uri? enderecoInstaBox = null, HttpMessageHandler? manipuladorFreestyle = null, Uri? enderecoFreestyle = null)
    {
        Pasta = pasta ?? Path.Combine(Path.GetTempPath(), "nexora-nuvem-" + Environment.ProcessId);
        _arquivo = pasta is null ? null : Path.Combine(pasta, "nuvem.json");
        _armazem = armazem;
        Ssh = new ProvedorSsh(Pasta);
        Teste = new ProvedorDeTeste(pastaDeTeste ?? Path.Combine(Pasta, "maquinas-de-teste"));
        InstaBox = new ProvedorInstaBox(Pasta, Ssh.Chave, new ClienteInstaBox(() => LerSegredo(IdDaChaveInstaBox), manipuladorInstaBox, enderecoInstaBox));
        Freestyle = new ProvedorFreestyle(Pasta, Ssh.Chave, new ClienteFreestyle(() => LerSegredo(IdDaChaveFreestyle), manipuladorFreestyle, enderecoFreestyle));
        _dados = Carregar();
    }

    public string Pasta { get; }
    public ProvedorSsh Ssh { get; }
    public ProvedorDeTeste Teste { get; }
    public ProvedorInstaBox InstaBox { get; }
    public ProvedorFreestyle Freestyle { get; }
    public bool GuardaToken => _armazem is not null;

    public event Action? Mudou;

    public IReadOnlyList<MaquinaNaNuvem> Maquinas
    {
        get
        {
            lock (_trava) return [.. _dados.Maquinas];
        }
    }

    public MaquinaNaNuvem? Maquina(string? id)
    {
        lock (_trava) return _dados.Maquinas.FirstOrDefault(m => m.Id == id);
    }

    public MaquinaNaNuvem Salvar(MaquinaNaNuvem maquina)
    {
        lock (_trava)
        {
            var i = _dados.Maquinas.FindIndex(m => m.Id == maquina.Id);
            if (i >= 0) _dados.Maquinas[i] = maquina;
            else _dados.Maquinas.Add(maquina);
            _abertas.Remove(maquina.Id);
            Gravar();
        }
        Mudou?.Invoke();
        return maquina;
    }

    public void Apagar(string id)
    {
        lock (_trava)
        {
            _dados.Maquinas.RemoveAll(m => m.Id == id);
            _dados.Execucoes.RemoveAll(e => e.Maquina == id);
            _abertas.Remove(id);
            Gravar();
        }
        Mudou?.Invoke();
    }

    public IProvedorDeMaquinas Provedor(MaquinaNaNuvem maquina) => maquina.Tipo switch
    {
        TiposDeMaquina.Teste => Teste,
        TiposDeMaquina.InstaBox => InstaBox,
        TiposDeMaquina.Freestyle => Freestyle,
        _ => Ssh,
    };

    public bool TemChaveInstaBox => LerSegredo(IdDaChaveInstaBox) is { Length: > 0 };

    public void GuardarChaveInstaBox(string chave) => GuardarChave(IdDaChaveInstaBox, chave);

    public void ApagarChaveInstaBox() => ApagarChave(IdDaChaveInstaBox);

    public bool TemChaveFreestyle => LerSegredo(IdDaChaveFreestyle) is { Length: > 0 };

    public void GuardarChaveFreestyle(string chave) => GuardarChave(IdDaChaveFreestyle, chave);

    public void ApagarChaveFreestyle() => ApagarChave(IdDaChaveFreestyle);

    void GuardarChave(string id, string chave)
    {
        if (_armazem is null) throw new PlatformNotSupportedException(Texto.Idioma.T("Este sistema não tem onde guardar o segredo de forma protegida."));
        chave = chave.Trim();
        if (chave.Length == 0 || chave.Any(char.IsWhiteSpace)) throw new ArgumentException(Texto.Idioma.T("A chave tem que ser uma linha só, sem espaços."));
        _armazem.Gravar(id, chave);
        Mudou?.Invoke();
    }

    void ApagarChave(string id)
    {
        _armazem?.Apagar(id);
        Mudou?.Invoke();
    }

    public async Task<(MaquinaNaNuvem Maquina, RelatorioDaMaquina Relatorio)> CriarNoInstaBoxAsync(string nome, string? modelo, Action<string>? progresso = null, CancellationToken cancelamento = default)
    {
        progresso?.Invoke(Texto.Idioma.T("Criando a máquina no InstaBox…"));
        return await PrepararCriadaAsync(Salvar(await InstaBox.CriarAsync(nome, modelo, cancelamento).ConfigureAwait(false)), progresso, cancelamento).ConfigureAwait(false);
    }

    public async Task<(MaquinaNaNuvem Maquina, RelatorioDaMaquina Relatorio)> CriarNaFreestyleAsync(string nome, Action<string>? progresso = null, CancellationToken cancelamento = default)
    {
        progresso?.Invoke(Texto.Idioma.T("Criando a máquina na Freestyle…"));
        return await PrepararCriadaAsync(Salvar(await Freestyle.CriarAsync(nome, cancelamento).ConfigureAwait(false)), progresso, cancelamento).ConfigureAwait(false);
    }

    async Task<(MaquinaNaNuvem Maquina, RelatorioDaMaquina Relatorio)> PrepararCriadaAsync(MaquinaNaNuvem maquina, Action<string>? progresso, CancellationToken cancelamento)
    {
        progresso?.Invoke(Texto.Idioma.T("Conectando pelo SSH…"));
        var relatorio = await TestarAsync(maquina, cancelamento).ConfigureAwait(false);
        if (relatorio.Conectou && !relatorio.Tem("claude"))
        {
            progresso?.Invoke(Texto.Idioma.T("Instalando o Claude Code na máquina…"));
            await Abrir(Maquina(maquina.Id) ?? maquina).RodarAsync(InstalarClaude, null, cancelamento).ConfigureAwait(false);
            relatorio = await TestarAsync(Maquina(maquina.Id) ?? maquina, cancelamento).ConfigureAwait(false);
        }
        return (Maquina(maquina.Id) ?? maquina, relatorio);
    }

    public const string InstalarClaude = "command -v claude >/dev/null 2>&1 || [ -x \"$HOME/.local/bin/claude\" ] || curl -fsSL https://claude.ai/install.sh | bash";

    public Task PausarAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default)
    {
        lock (_trava) _abertas.Remove(maquina.Id);
        return Provedor(maquina).PausarAsync(maquina, cancelamento);
    }

    public AvisoDeTrabalho? ManterAcordada(MaquinaNaNuvem maquina, string tarefa) =>
        maquina.Tipo == TiposDeMaquina.InstaBox && maquina.IdNoProvedor is { } id && TemChaveInstaBox
            ? new AvisoDeTrabalho(InstaBox.Cliente, id, "nexora-" + tarefa)
            : null;

    public Task RetomarAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default) => Provedor(maquina).RetomarAsync(maquina, cancelamento);

    public async Task ApagarComAMaquinaAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default)
    {
        if (maquina.Tipo == TiposDeMaquina.InstaBox) await InstaBox.ApagarAsync(maquina, cancelamento).ConfigureAwait(false);
        if (maquina.Tipo == TiposDeMaquina.Freestyle) await Freestyle.ApagarAsync(maquina, cancelamento).ConfigureAwait(false);
        Apagar(maquina.Id);
    }

    string? LerSegredo(string id)
    {
        try
        {
            return _armazem?.Ler(id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or FormatException)
        {
            return null;
        }
    }

    public IMaquinaRemota Abrir(MaquinaNaNuvem maquina)
    {
        lock (_trava)
        {
            if (_abertas.TryGetValue(maquina.Id, out var aberta) && aberta.Dados == maquina) return aberta;
            var nova = Provedor(maquina).Abrir(maquina);
            _abertas[maquina.Id] = nova;
            return nova;
        }
    }

    public Task<string> ChavePublicaAsync(CancellationToken cancelamento = default) => ChavesSsh.GarantirAsync(Ssh.Chave, null, cancelamento);

    public string? ChavePublicaPronta => ChavesSsh.LinhaPublica(Ssh.Chave);

    public async Task<RelatorioDaMaquina> TestarAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default)
    {
        if (maquina.Tipo != TiposDeMaquina.Teste) await ChavePublicaAsync(cancelamento).ConfigureAwait(false);
        var relatorio = await Abrir(maquina).TestarAsync(cancelamento).ConfigureAwait(false);
        if (relatorio.Conectou)
            Salvar(maquina with { Home = relatorio.Home, TestadaEm = DateTime.Now, Resumo = Resumir(relatorio) });
        return relatorio;
    }

    public static string Resumir(RelatorioDaMaquina relatorio)
    {
        List<string> partes = [];
        if (!string.IsNullOrWhiteSpace(relatorio.Sistema)) partes.Add(relatorio.Sistema.Split(' ')[0]);
        foreach (var f in new[] { "git", "node", "claude", "codex" })
            if (relatorio.Tem(f)) partes.Add(f);
        return string.Join(" · ", partes);
    }

    public bool TemTokenDoClaude
    {
        get
        {
            try
            {
                return _armazem?.Ler(IdDoToken) is { Length: > 0 };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or FormatException)
            {
                return false;
            }
        }
    }

    public void GuardarTokenDoClaude(string token)
    {
        if (_armazem is null) throw new PlatformNotSupportedException(Texto.Idioma.T("Este sistema não tem onde guardar o segredo de forma protegida."));
        token = token.Trim();
        if (token.Length == 0 || token.Contains('\n') || token.Contains(' ')) throw new ArgumentException(Texto.Idioma.T("O token tem que ser uma linha só, sem espaços."));
        _armazem.Gravar(IdDoToken, token);
        Mudou?.Invoke();
    }

    public void ApagarTokenDoClaude()
    {
        _armazem?.Apagar(IdDoToken);
        Mudou?.Invoke();
    }

    public IReadOnlyDictionary<string, string> AmbienteDoAgente(string agente, IReadOnlyDictionary<string, string>? doProjeto = null)
    {
        var ambiente = new Dictionary<string, string>(doProjeto ?? new Dictionary<string, string>(), StringComparer.Ordinal);
        if (agente == Claude.Agentes.Claude)
            try
            {
                if (_armazem?.Ler(IdDoToken) is { Length: > 0 } token) ambiente[VariavelDoClaude] = token;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or FormatException)
            {
            }
        return ambiente;
    }

    public ExecucaoSalva? Execucao(string? sessaoId)
    {
        if (string.IsNullOrEmpty(sessaoId)) return null;
        lock (_trava) return _dados.Execucoes.LastOrDefault(e => e.SessaoId == sessaoId);
    }

    public ExecucaoSalva? ExecucaoPorId(string execucao)
    {
        lock (_trava) return _dados.Execucoes.FirstOrDefault(e => e.Execucao == execucao);
    }

    public IReadOnlyList<ExecucaoSalva> Execucoes
    {
        get
        {
            lock (_trava) return [.. _dados.Execucoes];
        }
    }

    public void Lembrar(ExecucaoSalva execucao)
    {
        lock (_trava)
        {
            _dados.Execucoes.RemoveAll(e => e.Execucao == execucao.Execucao);
            _dados.Execucoes.Add(execucao);
            if (_dados.Execucoes.Count > 200) _dados.Execucoes.RemoveRange(0, _dados.Execucoes.Count - 200);
            Gravar();
        }
    }

    public void Esquecer(string execucao)
    {
        lock (_trava)
        {
            if (_dados.Execucoes.RemoveAll(e => e.Execucao == execucao) > 0) Gravar();
        }
    }

    public static string NovaExecucao() => DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..6];

    public Arquivos.OrigemDaLeitura Origem { get; private set; } = Arquivos.OrigemDaLeitura.Novo;

    public string? Guardado { get; private set; }

    Dados Carregar()
    {
        if (_arquivo is null) return new Dados();
        var lida = Arquivos.ArquivoSeguro.LerJson(_arquivo, () => new Dados());
        Origem = lida.Origem;
        Guardado = lida.Guardado;
        return lida.Valor;
    }

    void Gravar()
    {
        if (_arquivo is null || Origem == Arquivos.OrigemDaLeitura.Ilegivel) return;
        try
        {
            Arquivos.ArquivoSeguro.Gravar(_arquivo, JsonSerializer.Serialize(_dados, new JsonSerializerOptions { WriteIndented = true }), manterCopia: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
