using System.Windows.Input;
using Nexora.Core.Texto;
using Nexora.ViewModels;

namespace Nexora.Aplicativo;

public sealed record AtalhoDoApp(string Grupo, string Tecla, string Descricao, string[] Gestos, Func<JanelaVM, ICommand?>? Comando = null, object? Parametro = null, string Icone = "")
{
    public string TeclaExibida => CatalogoDeAtalhos.Exibir(Tecla);
    public bool NaBusca => Comando is not null && Icone.Length > 0;
}

public sealed record GrupoDeAtalhos(string Nome, IReadOnlyList<AtalhoDoApp> Itens);

public static class CatalogoDeAtalhos
{
    public const string TeclaDosAtalhos = "F1";

    public static IReadOnlyList<AtalhoDoApp> Todos()
    {
        var geral = Idioma.T("Geral");
        var navegar = Idioma.T("Páginas e painéis");
        var abas = Idioma.T("Abas e sessões");
        var conversa = Idioma.T("Na conversa");
        var aoVivo = Idioma.T("Ao vivo");
        var foco = Idioma.T("Modo foco");
        var grande = Idioma.T("Tela grande");
        var arquivos = Idioma.T("Arquivos");
        return
        [
            new(geral, "Ctrl+K", Idioma.T("Procurar conversas, projetos e comandos"), ["Ctrl+K", "Ctrl+Space"], vm => vm.ProcurarCommand),
            new(geral, TeclaDosAtalhos, Idioma.T("Ver todos os atalhos de teclado"), [TeclaDosAtalhos], vm => vm.MostrarAtalhosCommand, Icone: "teclado"),
            new(geral, "Ctrl+,", Idioma.T("Configurações"), ["Ctrl+OemComma"], vm => vm.MostrarConfiguracoesCommand, Icone: "ajustes"),
            new(geral, "Ctrl+J", Idioma.T("Modo foco: um pedido de cada vez"), ["Ctrl+J"], vm => vm.AbrirFocoCommand, Icone: "foco"),
            new(geral, "F11", Idioma.T("Tela cheia"), ["F11"], vm => vm.AlternarTelaCheiaCommand, Icone: "maximizar"),
            new(geral, "Ctrl+=", Idioma.T("Aumentar o texto"), ["Ctrl+OemPlus", "Ctrl+Shift+OemPlus", "Ctrl+Add"], vm => vm.AumentarTextoCommand),
            new(geral, "Ctrl+-", Idioma.T("Diminuir o texto"), ["Ctrl+OemMinus", "Ctrl+Subtract"], vm => vm.DiminuirTextoCommand),
            new(geral, "Esc", Idioma.T("Fechar o menu, a busca ou o painel aberto"), []),

            new(navegar, "Ctrl+0", Idioma.T("Dashboard"), ["Ctrl+D0"], vm => vm.MostrarDashboardCommand, Icone: "dashboard"),
            new(navegar, "Ctrl+Shift+0", Idioma.T("Ao vivo: todas as sessões"), ["Ctrl+Shift+D0"], vm => vm.MostrarVisaoGeralCommand, Icone: "quadro"),
            new(navegar, "Ctrl+Shift+O", Idioma.T("Orquestrador"), ["Ctrl+Shift+O"], vm => vm.MostrarOrquestraCommand, Icone: "orquestra"),
            new(navegar, "Ctrl+B", Idioma.T("Mostrar ou esconder a lateral de conversas"), ["Ctrl+B", "Ctrl+Shift+B"], vm => vm.AlternarLateralCommand, Icone: "lateral"),
            new(navegar, "Ctrl+Alt+B", Idioma.T("Painel da sessão: ambiente, contexto, arquivos e git"), ["Ctrl+Alt+B"], vm => vm.AlternarDetalhesCommand, Icone: "painel-direito"),
            new(navegar, "Ctrl+Shift+E", Idioma.T("Arquivos da sessão"), ["Ctrl+Shift+E"], vm => vm.AbrirPainelDaSessaoCommand, "arquivos", "arquivos"),
            new(navegar, "Ctrl+Shift+F", Idioma.T("Buscar nos arquivos da sessão"), ["Ctrl+Shift+F"], vm => vm.AbrirPainelDaSessaoCommand, "busca", "busca"),
            new(navegar, "Ctrl+Shift+G", Idioma.T("Git da sessão: alterações e commits"), ["Ctrl+Shift+G"], vm => vm.AbrirPainelDaSessaoCommand, "git", "branch"),

            new(abas, "Ctrl+N", Idioma.T("Nova sessão no projeto da aba atual"), ["Ctrl+N"], vm => vm.NovaSessaoCommand, Icone: "mais"),
            new(abas, "Ctrl+\\", Idioma.T("Nova sessão ao lado, dividindo a tela"), ["Ctrl+OemBackslash", "Ctrl+OemPipe"], vm => vm.Principal.NovaSessaoAoLadoCommand, Icone: "dividir"),
            new(abas, "Ctrl+Shift+\\", Idioma.T("Ligar ou desligar a tela dividida (split view)"), ["Ctrl+Shift+OemBackslash", "Ctrl+Shift+OemPipe"], vm => vm.Principal.AlternarSplitCommand, Icone: "dividir"),
            new(abas, "Ctrl+Alt+L", Idioma.T("Pergunta rápida ao lado, sem atrapalhar a conversa"), ["Ctrl+Alt+L"], vm => vm.PerguntaLateralCommand, Icone: "conversa"),
            new(abas, "Ctrl+Tab", Idioma.T("Próxima aba"), ["Ctrl+Tab"], vm => vm.Principal.ProximaCommand),
            new(abas, "Ctrl+Shift+Tab", Idioma.T("Aba anterior"), ["Ctrl+Shift+Tab"], vm => vm.Principal.AnteriorCommand),
            new(abas, "Ctrl+W", Idioma.T("Fechar a aba"), ["Ctrl+W"], vm => vm.Principal.FecharAtualCommand),

            new(conversa, "Enter", Idioma.T("Enviar a mensagem"), []),
            new(conversa, "Shift+Enter", Idioma.T("Quebrar a linha"), []),
            new(conversa, "Alt+M", Idioma.T("Trocar o modo de permissão"), []),
            new(conversa, "Ctrl+V", Idioma.T("Colar texto ou um print"), []),
            new(conversa, "Ctrl+`", Idioma.T("Abrir ou fechar o terminal da sessão"), []),
            new(conversa, "/", Idioma.T("No começo da mensagem, lista os comandos"), []),
            new(conversa, "@", Idioma.T("Citar um arquivo do projeto"), []),

            new(aoVivo, "← → ↑ ↓", Idioma.T("Andar entre os cartões"), []),
            new(aoVivo, "F", Idioma.T("Abrir o modo foco"), []),

            new(foco, "Enter / P", Idioma.T("Permitir"), []),
            new(foco, "N", Idioma.T("Negar"), []),
            new(foco, "S", Idioma.T("Permitir sempre, sem perguntar de novo"), []),
            new(foco, "T", Idioma.T("Permitir em todas as sessões que pedem o mesmo"), []),
            new(foco, "1–9", Idioma.T("Escolher a resposta de uma pergunta"), []),
            new(foco, "O", Idioma.T("Abrir a conversa"), []),
            new(foco, "Tab", Idioma.T("Próximo pedido"), []),

            new(grande, "← → ↑ ↓", Idioma.T("Andar entre os cartões"), []),
            new(grande, "Tab", Idioma.T("Trocar de fileira"), []),
            new(grande, "Enter", Idioma.T("Abrir o que está selecionado"), []),
            new(grande, "P / N", Idioma.T("Permitir ou negar"), []),
            new(grande, "Esc", Idioma.T("Voltar"), []),

            new(arquivos, "Ctrl+S", Idioma.T("Salvar o arquivo aberto"), []),
            new(arquivos, "Ctrl+L", Idioma.T("Citar o trecho selecionado na conversa"), []),
            new(arquivos, "F2", Idioma.T("Renomear o arquivo selecionado"), []),
            new(arquivos, "Delete", Idioma.T("Mandar o arquivo selecionado para a Lixeira"), []),
        ];
    }

    public static IReadOnlyList<GrupoDeAtalhos> Agrupados() =>
        Todos().GroupBy(a => a.Grupo).Select(g => new GrupoDeAtalhos(g.Key, g.ToList())).ToList();

    public static string Exibir(string tecla) => Exibir(tecla, Sistema.Mac);

    public static string Exibir(string tecla, bool mac)
    {
        if (!mac || !tecla.Contains('+')) return tecla;
        var partes = tecla.Split('+');
        var mods = partes[..^1];
        var final = partes[^1].Length == 0 ? "+" : partes[^1];
        var ctrl = mods.Contains("Ctrl");
        var comando = ctrl && final is not ("Tab" or "Space");
        return (ctrl && !comando ? "⌃" : "") + (mods.Contains("Alt") ? "⌥" : "") + (mods.Contains("Shift") ? "⇧" : "") + (comando ? "⌘" : "") + final;
    }
}

public sealed class ComandoAdiado(Func<ICommand?> alvo) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parametro) => alvo()?.CanExecute(parametro) == true;

    public void Execute(object? parametro)
    {
        if (alvo() is { } comando && comando.CanExecute(parametro)) comando.Execute(parametro);
    }
}
