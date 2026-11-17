using System.Diagnostics;
using System.Text.Json;
using Avalonia.Threading;
using Nexora.Core.Navegador;
using Nexora.Core.Orquestra;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Aplicativo;

static class NavegadorDoApp
{
    public static ServidorMcp? Servidor { get; private set; }

    public static void LigarServidor(JanelaVM janela, string[] args)
    {
        if (Servidor is not null || args.Contains("--demo") || args.Contains("--sem-hub")) return;
        Servidor = new ServidorMcp(FerramentasDoNavegador.Todas, (dono, ferramenta, argumentos, _) =>
            FerramentasDoNavegador.ExecutarAsync(ferramenta, argumentos, url => Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (janela.Abertas.FirstOrDefault(c => c.Codigo == dono) is not { } chat) return false;
                chat.AbrirNavegador(url);
                return true;
            }).GetTask()), FerramentasDoNavegador.Servidor);
    }

    public static void Ligar()
    {
        NavegadorVM.Flutuar = JanelaDoNavegador.Abrir;
        NavegadorVM.AbrirFora = url =>
        {
            try
            {
                Sistema.Abrir(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
            }
        };
    }

    public static async Task AbrirAsync(JanelaVM janela, string url, string[] args)
    {
        ChatVM? chat;
        while ((chat = janela.Atual ?? janela.Abertas.FirstOrDefault()) is null) await Task.Delay(50);
        if (janela.Atual != chat) janela.Mostrar(chat);
        chat.AbrirNavegador(url);
        var navegador = chat.Navegador;
        if (args.Contains("--flutuar")) navegador.DestacarCommand.Execute(null);
        if (!args.Contains("--design") && Argumento(args, "--anotar") is null && Argumento(args, "--roteiro-js") is null) return;
        for (var i = 0; i < 200 && (navegador.Nativo is null || navegador.Carregando || navegador.Atual is null); i++) await Task.Delay(50);
        if (navegador.Nativo is not NavegadorView vista) return;
        await navegador.AlternarDesignCommand.ExecuteAsync(null);
        foreach (var par in (Argumento(args, "--anotar") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            if (par.Split('|', 2) is [var seletor, var comentario])
                await vista.RodarParaTesteAsync($"window.__nexoraDesign.anotar({JsonSerializer.Serialize(seletor)}, {JsonSerializer.Serialize(comentario)})");
        await Task.Delay(150);
        if (Argumento(args, "--apontar") is { } apontar)
        {
            var partes = apontar.Split('|', 2);
            var texto = partes.Length > 1 ? JsonSerializer.Serialize(partes[1]) : "null";
            await vista.RodarParaTesteAsync($"window.__nexoraDesign.apontar({JsonSerializer.Serialize(partes[0])}, {texto})");
        }
        if (Argumento(args, "--roteiro-js") is { } arquivo && File.Exists(arquivo))
        {
            await vista.RodarParaTesteAsync(await File.ReadAllTextAsync(arquivo));
            await Task.Delay(800);
            var resultado = await vista.RodarParaTesteAsync("window.__resultado");
            if (Argumento(args, "--roteiro-saida") is { } saida)
                await File.WriteAllLinesAsync(saida,
                [
                    $"js={resultado}",
                    $"modoDesign={navegador.ModoDesign}",
                    $"anotacoes={string.Join(" | ", navegador.Anotacoes.Select(a => $"{a.Numero}:{a.Seletor}:{a.Comentario}"))}",
                    $"titulo={navegador.Titulo}",
                ]);
        }
        if (Argumento(args, "--salvar-print") is { } destino && await vista.CapturarAsync() is { } print) await File.WriteAllBytesAsync(destino, print);
        if (args.Contains("--mandar")) await navegador.MandarParaAgenteCommand.ExecuteAsync(null);
    }

    static string? Argumento(string[] args, string nome)
    {
        var i = Array.IndexOf(args, nome);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
