using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

using Nexora.ViewModels;

namespace Nexora.Views;

public static class SolturaDePastas
{
    public static void Ligar(Control alvo, Border realce)
    {
        DragDrop.SetAllowDrop(alvo, true);
        alvo.AddHandler(DragDrop.DragEnterEvent, (_, e) => Passando(e, realce));
        alvo.AddHandler(DragDrop.DragOverEvent, (_, e) => Passando(e, realce));
        alvo.AddHandler(DragDrop.DragLeaveEvent, (_, e) =>
        {
            var ponto = e.GetPosition(alvo);
            if (ponto.X <= 0 || ponto.Y <= 0 || ponto.X >= alvo.Bounds.Width || ponto.Y >= alvo.Bounds.Height) realce.Classes.Set("ativa", false);
        });
        alvo.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            realce.Classes.Set("ativa", false);
            if (!e.DataTransfer.Contains(DataFormat.File)) return;
            var caminhos = Caminhos(e.DataTransfer.TryGetFiles());
            e.Handled = true;
            if (caminhos.Count == 0 || Janela(alvo) is not { } janela) return;
            janela.SoltarPastas(caminhos);
        });
    }

    static void Passando(DragEventArgs e, Border realce)
    {
        var temArquivo = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = temArquivo ? DragDropEffects.Copy : DragDropEffects.None;
        realce.Classes.Set("ativa", temArquivo);
        if (temArquivo) e.Handled = true;
    }

    public static IReadOnlyList<string> Caminhos(IEnumerable<IStorageItem>? itens) =>
        [.. (itens ?? []).Select(i => i.TryGetLocalPath()).OfType<string>()];

    static JanelaVM? Janela(Control alvo) =>
        alvo.DataContext as JanelaVM ?? TopLevel.GetTopLevel(alvo)?.DataContext as JanelaVM;
}
