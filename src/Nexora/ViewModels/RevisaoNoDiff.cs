using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Nexora.Core.Revisao;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ComentarioPendenteVM : ObservableObject
{
    readonly RevisaoPendenteVM _dono;

    public ComentarioPendenteVM(ComentarioDeLinha dados, string origem, int indiceFim, RevisaoPendenteVM dono)
    {
        Dados = dados;
        Origem = origem;
        IndiceFim = indiceFim;
        _dono = dono;
        Texto = dados.Texto;
    }

    public ComentarioDeLinha Dados { get; private set; }
    public string Origem { get; }
    public int IndiceFim { get; }
    public string Arquivo => Dados.Arquivo;
    public string Nome => Path.GetFileName(Dados.Arquivo);
    public string Faixa => ComentariosDeRevisao.Faixa(Dados.Inicio, Dados.Fim, Dados.Antiga);
    public string Onde => $"{Nome} · {Faixa}";
    public Action? AoRemover { get; set; }

    [ObservableProperty]
    public partial string Texto { get; set; }

    partial void OnTextoChanged(string value) => Dados = Dados with { Texto = value.Trim() };

    [ObservableProperty]
    public partial bool Editando { get; set; }

    [ObservableProperty]
    public partial string Edicao { get; set; } = "";

    [RelayCommand]
    void Editar()
    {
        Edicao = Texto;
        Editando = true;
    }

    [RelayCommand]
    void SalvarEdicao()
    {
        if (string.IsNullOrWhiteSpace(Edicao)) return;
        Texto = Edicao.Trim();
        Editando = false;
    }

    [RelayCommand]
    void CancelarEdicao() => Editando = false;

    [RelayCommand]
    void Apagar() => _dono.Remover(this);
}

public sealed partial class RevisaoPendenteVM : ObservableObject
{
    readonly Action<string> _mandar;

    public RevisaoPendenteVM(Action<string> mandar)
    {
        _mandar = mandar;
        Itens.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TemItens));
            OnPropertyChanged(nameof(TextoQuantidade));
            MandarCommand.NotifyCanExecuteChanged();
            if (Itens.Count == 0) ListaAberta = false;
        };
    }

    public ObservableCollection<ComentarioPendenteVM> Itens { get; } = [];
    public bool TemItens => Itens.Count > 0;
    public string TextoQuantidade => Itens.Count == 1 ? Idioma.T("1 comentário") : Idioma.F("{0} comentários", Itens.Count);

    [ObservableProperty]
    public partial bool ListaAberta { get; set; }

    public IEnumerable<ComentarioPendenteVM> De(string origem) => Itens.Where(i => i.Origem == origem);

    public void Adicionar(ComentarioPendenteVM comentario) => Itens.Add(comentario);

    public void Remover(ComentarioPendenteVM comentario)
    {
        if (!Itens.Remove(comentario)) return;
        comentario.AoRemover?.Invoke();
    }

    public string Pedido() => ComentariosDeRevisao.PedidoDoDiff([.. Itens.Select(i => i.Dados)]);

    [RelayCommand(CanExecute = nameof(TemItens))]
    void Mandar()
    {
        if (Itens.Count == 0) return;
        var texto = Pedido();
        Descartar();
        _mandar(texto);
    }

    [RelayCommand]
    void Descartar()
    {
        foreach (var item in Itens.ToList()) Remover(item);
        ListaAberta = false;
    }

    [RelayCommand]
    void AlternarLista() => ListaAberta = !ListaAberta && TemItens;
}

public sealed record RespostaNaLinhaVM(string Autor, string Corpo);

public sealed partial class ConversaNaLinhaVM : ObservableObject
{
    public ConversaNaLinhaVM(ConversaDoPr dados)
    {
        Dados = dados;
        Autor = dados.Primeiro?.Autor ?? "";
        Agente = ComentariosDeRevisao.AgenteDoLogin(Autor);
        Iniciais = ComentariosDeRevisao.Iniciais(Autor);
        Corpo = PainelPrVM.Resumir(dados.Primeiro?.Corpo ?? "", 600);
        Respostas = [.. dados.Comentarios.Skip(1).Select(c => new RespostaNaLinhaVM(c.Autor, PainelPrVM.Resumir(c.Corpo, 400)))];
    }

    public ConversaDoPr Dados { get; }
    public string Autor { get; }
    public string? Agente { get; }
    public bool EhAgente => Agente is not null;
    public bool EhPessoa => Agente is null;
    public string Iniciais { get; }
    public string Corpo { get; }
    public IReadOnlyList<RespostaNaLinhaVM> Respostas { get; }
    public bool TemRespostas => Respostas.Count > 0;
    public bool Resolvida => Dados.Resolvida;
    public string Situacao => Dados.Resolvida ? Idioma.T("resolvida") : Idioma.T("aberta");
    public string Quando => Dados.UltimaEm is { } q ? Core.Texto.Tempo.Relativo(q.LocalDateTime) : "";
    public string Dica => $"{Autor}: {Core.Claude.Resumos.UmaLinha(Dados.Primeiro?.Corpo ?? "", 120)}";
    public bool TemLink => Dados.Primeiro?.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true;

    [RelayCommand]
    void Abrir()
    {
        if (TemLink) PainelPrVM.AbrirLink(Dados.Primeiro!.Url);
    }
}
