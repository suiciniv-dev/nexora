using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Nexora.Core.Revisao;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class DiffVM : AbaVM
{
    readonly Action<string>? _abrir;
    readonly IReadOnlyList<LinhaDiff> _brutas;
    LinhaDiffVM? _escrevendo;
    int _inicio = -1;

    public DiffVM(string titulo, string dica, IReadOnlyList<LinhaDiff> linhas, string? arquivo, Action<AbaVM> fechar, Action<AbaVM> selecionar, Action<string>? abrir,
        RevisaoPendenteVM? revisao = null, string? relativo = null, IReadOnlyList<ConversaDoPr>? conversas = null)
        : base(titulo, dica, fechar, selecionar)
    {
        _brutas = linhas;
        Revisao = revisao;
        Linhas = linhas.Select((l, i) => new LinhaDiffVM(l, i, this)).ToList();
        Arquivo = arquivo;
        _abrir = abrir;
        Relativo = relativo ?? (arquivo is not null ? Path.GetFileName(arquivo) : Dica.Split('#')[0]);
        Adicionadas = linhas.Count(l => l.Tipo == TipoLinha.Adicionada);
        Removidas = linhas.Count(l => l.Tipo == TipoLinha.Removida);
        if (revisao is not null)
            foreach (var pendente in revisao.De(dica))
                if (pendente.IndiceFim >= 0 && pendente.IndiceFim < Linhas.Count) Prender(Linhas[pendente.IndiceFim], pendente);
        if (conversas is not null) Pendurar(conversas);
    }

    public List<LinhaDiffVM> Linhas { get; }
    public string? Arquivo { get; }
    public string Relativo { get; }
    public RevisaoPendenteVM? Revisao { get; }
    public bool PodeComentar => Revisao is not null;
    public string Caminho => Arquivo ?? Dica.Split('#')[0];
    public int Adicionadas { get; }
    public int Removidas { get; }
    public string Resumo => $"+{Adicionadas}  −{Removidas}";
    public bool PodeAbrir => Arquivo is not null && File.Exists(Arquivo);
    public bool Vazio => Linhas.Count == 0;
    public int ConversasNoDiff { get; private set; }

    void Pendurar(IReadOnlyList<ConversaDoPr> conversas)
    {
        foreach (var conversa in conversas.Where(c => !c.Desatualizada && c.Linha is not null))
        {
            var linha = Linhas.LastOrDefault(l => l.NumeroNovo == conversa.Linha) ?? Linhas.LastOrDefault(l => l.NumeroNovo is null && l.NumeroAntigo == conversa.Linha);
            if (linha is null) continue;
            linha.Pendurar(new ConversaNaLinhaVM(conversa));
            ConversasNoDiff++;
        }
    }

    static void Prender(LinhaDiffVM linha, ComentarioPendenteVM comentario)
    {
        linha.Comentarios.Add(comentario);
        comentario.AoRemover = () => linha.Comentarios.Remove(comentario);
    }

    public void Comentar(LinhaDiffVM linha, bool estender)
    {
        if (Revisao is null || linha.Cabecalho) return;
        if (!estender && _escrevendo is { } aberto && aberto != linha && !string.IsNullOrWhiteSpace(aberto.Rascunho)) Confirmar(aberto);
        var rascunho = _escrevendo?.Rascunho ?? "";
        if (!estender || _escrevendo is null || _inicio < 0) _inicio = linha.Indice;
        var (de, ate) = (Math.Min(_inicio, linha.Indice), Math.Max(_inicio, linha.Indice));
        if (_escrevendo is not null && _escrevendo != Linhas[ate]) _escrevendo.Escrevendo = false;
        _escrevendo = Linhas[ate];
        _escrevendo.Rascunho = estender ? rascunho : "";
        _escrevendo.FaixaDoRascunho = ComentariosDeRevisao.Comentario(Relativo, _brutas, de, ate, "") is { } previa
            ? ComentariosDeRevisao.Faixa(previa.Inicio, previa.Fim, previa.Antiga) : "";
        _escrevendo.Escrevendo = true;
        Marcar(de, ate);
    }

    void Marcar(int de, int ate)
    {
        foreach (var l in Linhas) l.NaFaixa = l.Indice >= de && l.Indice <= ate && ate > de;
    }

    public void Confirmar(LinhaDiffVM linha)
    {
        if (Revisao is null || linha != _escrevendo || string.IsNullOrWhiteSpace(linha.Rascunho)) return;
        var de = Math.Min(_inicio < 0 ? linha.Indice : _inicio, linha.Indice);
        if (ComentariosDeRevisao.Comentario(Relativo, _brutas, de, linha.Indice, linha.Rascunho) is not { } dados) return;
        var pendente = new ComentarioPendenteVM(dados, Dica, linha.Indice, Revisao);
        Revisao.Adicionar(pendente);
        Prender(linha, pendente);
        Cancelar();
    }

    public void Cancelar()
    {
        if (_escrevendo is not null)
        {
            _escrevendo.Escrevendo = false;
            _escrevendo.Rascunho = "";
        }
        _escrevendo = null;
        _inicio = -1;
        Marcar(-1, -1);
    }

    [RelayCommand]
    void AbrirArquivo()
    {
        if (Arquivo is not null) _abrir?.Invoke(Arquivo);
    }
}
