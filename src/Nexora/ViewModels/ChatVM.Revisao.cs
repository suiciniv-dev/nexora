using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Git;
using Nexora.Core.Revisao;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed record SeloRevisao(string Texto, string Tipo, string Dica, string? Correcao)
{
    public bool Ok => Tipo == "ok";
    public bool Falhou => Tipo == "erro";
    public bool Alerta => Tipo == "alerta";
    public bool PodeCorrigir => Correcao is not null;
    public string Icone => Tipo switch { "ok" => "ok", "erro" => "fechar", _ => "alerta" };
}

public sealed partial class ChatVM
{
    public ObservableCollection<SeloRevisao> SelosRevisao { get; } = [];
    public bool TemRevisao => SelosRevisao.Count > 0 && Estado == EstadoChat.Pronta;
    int _revisao;

    public static IReadOnlyList<PassoDaSessao> PassosDe(IEnumerable<ItemChat> itens) =>
        [.. ItemExploracao.Ferramentas(itens).Select(f => new PassoDaSessao(f.Nome,
            f.Entrada.ValueKind == JsonValueKind.Object ? Protocolo.Texto(f.Entrada, "command") : null,
            f.Concluida, f.Erro, ItemFerramenta.EditaArquivo(f.Nome)))];

    public static IReadOnlyList<SeloRevisao> Selos(ResumoDaRevisao r, ConflitoComBase? conflito)
    {
        var selos = new List<SeloRevisao>();
        if (r.Teste is { } t)
            selos.Add(t.Passou
                ? new(r.EditouDepoisDoTeste ? Idioma.T("Testes passaram, mas houve edição depois") : Idioma.T("Testes passaram"), r.EditouDepoisDoTeste ? "alerta" : "ok", t.Comando,
                    r.EditouDepoisDoTeste ? Idioma.F("Depois da última edição os testes não rodaram de novo. Rode `{0}` e me diga o resultado.", t.Comando) : null)
                : new(Idioma.T("Testes falharam"), "erro", t.Comando, Idioma.F("O comando `{0}` falhou. Corrija e rode de novo até passar.", t.Comando)));
        else if (r.Editou)
            selos.Add(new(Idioma.T("Nenhum teste rodou"), "alerta", Idioma.T("O agente editou arquivos e não rodou testes nesta conversa."),
                Idioma.T("Rode os testes do projeto e me diga o resultado; se algum falhar, corrija.")));
        if (r.Build is { } b)
            selos.Add(b.Passou
                ? new(Idioma.T("Build passou"), r.EditouDepoisDoBuild ? "alerta" : "ok", b.Comando, null)
                : new(Idioma.T("Build falhou"), "erro", b.Comando, Idioma.F("O build `{0}` falhou. Corrija os erros e compile de novo.", b.Comando)));
        if (conflito is { } c)
            selos.Add(c.TemConflito
                ? new(Idioma.F("Conflito com {0}", c.Base), "erro", string.Join("\n", c.Arquivos),
                    Idioma.F("Esta branch tem conflito com {0} em: {1}. Traga as mudanças de {0} e resolva os conflitos.", c.Base, string.Join(", ", c.Arquivos)))
                : new(Idioma.F("Sem conflito com {0}", c.Base), "ok", Idioma.T("Conferido localmente com o último fetch."), null));
        return selos;
    }

    async Task AtualizarRevisaoAsync()
    {
        var vez = ++_revisao;
        var resumo = Verificacoes.Resumir(PassosDe(Itens));
        TrocarSelos(Selos(resumo, null));
        if (!resumo.Editou || JanelaVM.Demo) return;
        var pasta = Pasta;
        var preferida = App.Config.BasesDasTarefas.GetValueOrDefault(Config.ChaveDePasta(LocalAtual.Pasta));
        var conflito = await Task.Run(() => Conflitos.VerificarAsync(pasta, preferida));
        if (vez != _revisao || conflito is null) return;
        TrocarSelos(Selos(Verificacoes.Resumir(PassosDe(Itens)), conflito));
    }

    void TrocarSelos(IReadOnlyList<SeloRevisao> selos)
    {
        SelosRevisao.Clear();
        foreach (var selo in selos) SelosRevisao.Add(selo);
        OnPropertyChanged(nameof(TemRevisao));
    }

    [RelayCommand]
    void PedirCorrecao(SeloRevisao? selo)
    {
        if (selo?.Correcao is not { } texto) return;
        InserirNoRascunho(texto);
    }
}
