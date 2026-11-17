using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nexora.Servicos;

public sealed class Config
{
    static readonly JsonSerializerOptions Indentado = new() { WriteIndented = true };

    public string Tema { get; set; } = "sistema";
    public string ModoFundo { get; set; } = "solido";
    public double Zoom { get; set; } = 1;
    public string Fonte { get; set; } = Aparencia.FonteDoSistema;
    public string Animacoes { get; set; } = Aparencia.AnimacoesDoSistema;
    public List<string> Raizes { get; set; } = [@"D:\Repositorios"];
    public string? UltimaPasta { get; set; }
    public List<string>? FeedsDoDashboard { get; set; }
    public string? ProjetoDoDashboard { get; set; }
    public bool DesenhoPeloProcessador { get; set; }
    public List<string> Workspaces { get; set; } = [];
    public List<string> Ocultos { get; set; } = [];
    public bool LateralVisivel { get; set; } = true;
    public bool HubMcp { get; set; } = true;
    public bool PainelDireitoAberto { get; set; }
    public bool Quadro { get; set; } = true;
    public bool Mesa { get; set; } = true;
    public List<Core.Texto.TemplateDeTarefa>? Templates { get; set; }
    public string TemplateDaIssue { get; set; } = Core.Texto.Templates.IdIssue;
    public List<string>? TemplatesPadraoVistos { get; set; }
    public bool QuadroPorProjeto { get; set; }
    public bool BoasVindasFeitas { get; set; }
    public List<string> Fixadas { get; set; } = [];
    public List<string> ProjetosFechados { get; set; } = [];
    public int McpOciosoMinutos { get; set; } = 10;
    public Dictionary<string, int> Portas { get; set; } = [];
    public Dictionary<string, string> EnderecosDoNavegador { get; set; } = [];
    public string AgentePadrao { get; set; } = "claude";
    public bool AgenteEscolhido { get; set; }
    public string? ModeloChefe { get; set; } = "opus";
    public string? EsforcoChefe { get; set; } = "high";
    public Dictionary<string, Nexora.Modelos.OrquestraSalva> Orquestradores { get; set; } = [];
    public JanelaSalva? Principal { get; set; }
    public List<JanelaSalva> Janelas { get; set; } = [];
    public Dictionary<string, string> BasesDasTarefas { get; set; } = [];
    public string? QuadroGitHub { get; set; }
    public Dictionary<string, string> VisoesGitHub { get; set; } = [];
    public bool IssuesEmLista { get; set; }
    public Dictionary<string, Core.Git.IssueDaSessao> IssuesDasSessoes { get; set; } = [];
    public Dictionary<string, string> OrigensDasSessoes { get; set; } = [];
    public Dictionary<string, string> TurnosDesfeitos { get; set; } = [];
    public string? UltimoModelo { get; set; }
    public Dictionary<string, string> ModeloPorAgente { get; set; } = [];
    public string? UltimoEsforco { get; set; }
    public int UltimoModo { get; set; }
    public bool UltimoRapido { get; set; }
    public bool UltimoUltracode { get; set; }
    public bool AbrirPeloExe { get; set; }
    public string Idioma { get; set; } = "auto";
    public List<string> ConversasFechadas { get; set; } = [];
    public Dictionary<string, DateTime> SessoesEscondidas { get; set; } = [];
    public List<string> SessoesDoNexora { get; set; } = [];
    public bool HibernarSessoes { get; set; } = true;
    public string ModeloDosSubagentes { get; set; } = Nexora.Servicos.VagasDeSubagentes.Sonnet;
    public int SubagentesAoMesmoTempo { get; set; } = 2;
    public string VigiaChecks { get; set; } = Core.Git.VigiaDePr.Avisar;
    public string VigiaConflitos { get; set; } = Core.Git.VigiaDePr.Avisar;
    public string VigiaRevisoes { get; set; } = Core.Git.VigiaDePr.Avisar;
    public Dictionary<string, DateTime> VigiaVistos { get; set; } = [];
    public List<string> VigiasPausadas { get; set; } = [];
    public List<string> ConversasCompletas { get; set; } = [];
    public string? AbaDetalhe { get; set; }
    public List<string> PastasAbertas { get; set; } = [];
    public List<Core.Agenda.Agendamento> Agendamentos { get; set; } = [];
    public double AlturaTerminal { get; set; } = 260;
    public List<string> ProjetosNaNuvem { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RelatorioPorEmail { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RelatorioDia { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RelatorioHora { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? RelatorioEnviadoEm { get; set; }

    public static string ChaveDePasta(string pasta) =>
        OperatingSystem.IsWindows() ? pasta.Replace('/', '\\').TrimEnd('\\').ToLowerInvariant() : pasta.TrimEnd('/').ToLowerInvariant();

    public static string CaminhoPadrao =>
        Path.Combine(global::Nexora.Core.DadosDoNexora.Pasta, "config.json");

    readonly object _trava = new();
    string _caminho = CaminhoPadrao;

    [JsonIgnore]
    public Core.Arquivos.OrigemDaLeitura Origem { get; private set; } = Core.Arquivos.OrigemDaLeitura.Novo;

    [JsonIgnore]
    public string? Guardado { get; private set; }

    [JsonIgnore]
    public bool Recuperada => Origem is Core.Arquivos.OrigemDaLeitura.Copia or Core.Arquivos.OrigemDaLeitura.Perdido;

    public static Config Carregar() => Carregar(CaminhoPadrao);

    public static Config Carregar(string caminho)
    {
        var lida = Core.Arquivos.ArquivoSeguro.LerJson(caminho, () => new Config());
        var config = lida.Valor;
        config._caminho = caminho;
        config.Origem = lida.Origem;
        config.Guardado = lida.Guardado;
        return config;
    }

    public void Salvar()
    {
        if (Origem == Core.Arquivos.OrigemDaLeitura.Ilegivel) return;
        try
        {
            string texto;
            lock (_trava) texto = JsonSerializer.Serialize(this, Indentado);
            Core.Arquivos.ArquivoSeguro.GravarDepois(_caminho, texto, manterCopia: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
        }
    }
}
