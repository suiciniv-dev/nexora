using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public enum EstadoSubagente { Rodando, SegundoPlano, Terminou, Erro, Parado }

public enum EsperaDoSubagente { Nenhuma, Modelo, Vaga }

public sealed partial class ItemSubagente : ObservableObject
{
    const int MaiorResultado = 20000;

    public ItemSubagente(string id, JsonElement entrada, DateTime? inicio = null)
    {
        Id = id;
        Tipo = Protocolo.Texto(entrada, "subagent_type") is { Length: > 0 } tipo ? tipo : "general-purpose";
        Descricao = Protocolo.Texto(entrada, "description") is { Length: > 0 } descricao ? Resumos.UmaLinha(descricao, 140) : Idioma.T("Subagent");
        Pedido = Protocolo.Texto(entrada, "prompt") is { Length: > 0 } pedido ? Resumos.Limitar(pedido.Trim(), 4000) : null;
        ModeloPedido = CartaoVM.NomeDoModelo(Protocolo.Texto(entrada, "model"));
        Inicio = inicio ?? DateTime.Now;
        Ferramentas.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(QuantasFerramentas));
            OnPropertyChanged(nameof(TextoFerramentas));
            OnPropertyChanged(nameof(TemFerramentas));
            OnPropertyChanged(nameof(SoContagem));
        };
    }

    public string Id { get; }
    public string? AgenteId { get; set; }
    public string Tipo { get; }
    public string Descricao { get; }
    public string? Pedido { get; }
    public bool TemPedido => Pedido is not null;
    public bool TemModelo => ModeloPedido is { Length: > 0 };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemModelo))]
    public partial string? ModeloPedido { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EscolhendoModelo), nameof(NaFila), nameof(EstadoTexto), nameof(MostraAtividade))]
    public partial EsperaDoSubagente Espera { get; set; }

    public bool EscolhendoModelo => Espera == EsperaDoSubagente.Modelo && Rodando;
    public bool NaFila => Espera == EsperaDoSubagente.Vaga && Rodando;

    public event Action<string?>? ModeloEscolhido;

    [RelayCommand]
    void EscolherModelo(string? valor)
    {
        if (!EscolhendoModelo) return;
        Espera = EsperaDoSubagente.Nenhuma;
        ModeloEscolhido?.Invoke(string.IsNullOrEmpty(valor) ? null : valor);
    }
    public ObservableCollection<ItemFerramenta> Ferramentas { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Rodando), nameof(Ok), nameof(Humor), nameof(Falhou), nameof(Parou), nameof(EstadoTexto), nameof(TempoTexto), nameof(MostraAtividade), nameof(MostraPrevia), nameof(EscolhendoModelo), nameof(NaFila))]
    public partial EstadoSubagente Estado { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QuantasFerramentas), nameof(TextoFerramentas), nameof(SoContagem))]
    public partial int? FerramentasInformadas { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraAtividade))]
    public partial string? Atividade { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemResultado), nameof(MostraAtividade), nameof(MostraPrevia), nameof(Previa))]
    public partial string? Resultado { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TempoTexto))]
    public partial long? DuracaoMs { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemTokens), nameof(TokensTexto))]
    public partial long? Tokens { get; set; }

    [ObservableProperty]
    public partial bool Aberto { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TempoTexto))]
    public partial DateTime Inicio { get; set; }

    public bool Rodando => Estado is EstadoSubagente.Rodando or EstadoSubagente.SegundoPlano;
    public Core.Avatares.HumorAvatar Humor => Rodando ? Core.Avatares.HumorAvatar.Pensando : Core.Avatares.HumorAvatar.Parado;
    public bool Ok => Estado == EstadoSubagente.Terminou;
    public bool Falhou => Estado == EstadoSubagente.Erro;
    public bool Parou => Estado == EstadoSubagente.Parado;
    public bool TemFerramentas => Ferramentas.Count > 0;
    public bool SoContagem => Ferramentas.Count == 0 && QuantasFerramentas > 0;
    public bool TemTokens => Tokens is > 0;
    public string TokensTexto => Tokens is { } t ? TokensCurtos(t) : "";
    public bool TemResultado => !string.IsNullOrWhiteSpace(Resultado);
    public int QuantasFerramentas => Math.Max(Ferramentas.Count, FerramentasInformadas ?? 0);

    public static string TokensCurtos(long tokens) => tokens >= 1000 ? $"{tokens / 1000.0:0.#}k tokens" : $"{tokens} tokens";

    public string TextoFerramentas => QuantasFerramentas switch
    {
        0 => Idioma.T("nenhuma ferramenta"),
        1 => Idioma.T("1 ferramenta"),
        var n => Idioma.F("{0} ferramentas", n),
    };

    public string TempoTexto => Tempo.Duracao(Math.Max(1000, DuracaoMs ?? (long)(DateTime.Now - Inicio).TotalMilliseconds));

    public string EstadoTexto => Estado switch
    {
        _ when EscolhendoModelo => Idioma.T("esperando você escolher o modelo"),
        _ when NaFila => Idioma.T("na fila"),
        EstadoSubagente.Rodando => Idioma.T("rodando"),
        EstadoSubagente.SegundoPlano => Idioma.T("em segundo plano"),
        EstadoSubagente.Terminou => Idioma.T("terminou"),
        EstadoSubagente.Erro => Idioma.T("deu erro"),
        _ => Idioma.T("parou"),
    };

    public bool MostraAtividade => (Rodando || !TemResultado) && Espera == EsperaDoSubagente.Nenhuma && !string.IsNullOrWhiteSpace(Atividade);
    public bool MostraPrevia => !Rodando && TemResultado;
    public string? Previa => TemResultado ? Resumos.SemMarcas(Resumos.UmaLinha(Resultado!, 220)) : null;

    public void Tique()
    {
        if (!Rodando) return;
        OnPropertyChanged(nameof(TempoTexto));
    }

    public void Concluir(EstadoSubagente estado, string? resultado, int? ferramentas = null, long? duracaoMs = null, long? tokens = null)
    {
        if (tokens is > 0) Tokens = tokens;
        if (ferramentas is { } n) FerramentasInformadas = n;
        if (!string.IsNullOrWhiteSpace(resultado)) Resultado = Resumos.Limitar(resultado.Trim(), MaiorResultado);
        if (!Rodando) return;
        DuracaoMs = duracaoMs ?? (long)(DateTime.Now - Inicio).TotalMilliseconds;
        foreach (var solta in Ferramentas.Where(f => f.Rodando)) solta.Concluir("", false);
        Estado = estado;
    }

    [RelayCommand]
    void Alternar() => Aberto = !Aberto;

    [RelayCommand]
    void Abrir() => Aberto = true;
}

public sealed partial class ItemSubagentes : ItemChat
{
    public ObservableCollection<ItemSubagente> Agentes { get; } = [];

    public void Adicionar(ItemSubagente agente)
    {
        Agentes.Add(agente);
        agente.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ItemSubagente.Estado)) Avisar();
        };
        Avisar();
    }

    public bool Varios => Agentes.Count > 1;
    public bool AlgumRodando => Agentes.Any(a => a.Rodando);
    public string Titulo => Idioma.F("{0} subagents em paralelo", Agentes.Count);

    public string Resumo
    {
        get
        {
            var rodando = Agentes.Count(a => a.Rodando);
            var prontos = Agentes.Count(a => a.Ok);
            var erros = Agentes.Count(a => a.Falhou);
            var parados = Agentes.Count(a => a.Parou);
            return string.Join("  ·  ", new[]
            {
                rodando > 0 ? Idioma.F("{0} rodando", rodando) : null,
                prontos switch { 0 => null, 1 => Idioma.T("1 terminou"), _ => Idioma.F("{0} terminaram", prontos) },
                erros switch { 0 => null, 1 => Idioma.T("1 deu erro"), _ => Idioma.F("{0} deram erro", erros) },
                parados switch { 0 => null, 1 => Idioma.T("1 parou"), _ => Idioma.F("{0} pararam", parados) },
            }.OfType<string>());
        }
    }

    void Avisar()
    {
        OnPropertyChanged(nameof(Varios));
        OnPropertyChanged(nameof(AlgumRodando));
        OnPropertyChanged(nameof(Titulo));
        OnPropertyChanged(nameof(Resumo));
    }
}
