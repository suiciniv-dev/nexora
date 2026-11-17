#if !MAC
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Web.WebView2.Core;
using Nexora.Core.Texto;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Aplicativo;

public sealed class Apresentacao : Window
{
    const string Anfitriao = "nexora.apresentacao";

    readonly BoasVindasVM _vm;
    readonly WebViewNativo _web;
    bool _concluida;
    bool _instalando;

    public event Action? Falhou;

    public Apresentacao(BoasVindasVM vm, Window? dono)
    {
        _vm = vm;
        Title = Idioma.T("Bem-vindo ao Nexora");
        Icon = dono?.Icon;
        CanResize = false;
        WindowDecorations = WindowDecorations.None;
        ShowInTaskbar = false;
        Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0E0D0C"));
        var tela = dono is null ? null : dono.Screens.ScreenFromWindow(dono) ?? dono.Screens.Primary;
        if (tela is not null)
        {
            var area = tela.WorkingArea;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = area.Position;
            Width = area.Width / tela.Scaling;
            Height = area.Height / tela.Scaling;
        }
        else
        {
            Width = 1280;
            Height = 800;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        _web = new WebViewNativo();
        Content = _web;
        _web.Pronto += Carregar;
        _web.Falhou += _ => Dispatcher.UIThread.Post(() =>
        {
            Falhou?.Invoke();
            Close();
        });
        Closed += (_, _) =>
        {
            if (!_concluida) _vm.Concluir();
        };
    }

    void Carregar(CoreWebView2 web)
    {
        var pasta = Path.Combine(AppContext.BaseDirectory, "Assets", "Apresentacao");
        if (!File.Exists(Path.Combine(pasta, "index.html")))
        {
            Falhou?.Invoke();
            Close();
            return;
        }
        web.SetVirtualHostNameToFolderMapping(Anfitriao, pasta, CoreWebView2HostResourceAccessKind.Allow);
        web.WebMessageReceived += (_, e) =>
        {
            var texto = e.TryGetWebMessageAsString();
            Dispatcher.UIThread.Post(() => _ = ReceberAsync(texto));
        };
        web.Navigate($"https://{Anfitriao}/index.html?lang={Idioma.Codigo}");
    }

    async Task ReceberAsync(string? json)
    {
        if (string.IsNullOrEmpty(json)) return;
        JsonElement m;
        try
        {
            m = JsonDocument.Parse(json).RootElement.Clone();
        }
        catch (JsonException)
        {
            return;
        }
        string? Texto(string nome) => m.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        switch (Texto("type"))
        {
            case "pronto":
                _vm.Verificar();
                await EnviarTudoAsync();
                break;
            case "verificar":
                _vm.Verificar();
                Enviar(Requisitos());
                break;
            case "instalar" when Texto("id") is { } id:
                await InstalarAsync([id]);
                break;
            case "instalarTudo":
                await InstalarAsync(_vm.Requisitos.Where(r => !r.Ok && r.Receita.Instalavel).Select(r => r.Receita.Id).ToList());
                break;
            case "abrir" when Texto("url") is { } url && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase):
                try
                {
                    Sistema.Abrir(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }
                break;
            case "entrar":
                _vm.EntrarCommand.Execute(null);
                break;
            case "verificarLogin":
                _vm.VerificarLoginCommand.Execute(null);
                Enviar(new JsonObject { ["type"] = "login", ["ok"] = _vm.Logado, ["temClaude"] = _vm.Requisitos[0].Ok });
                break;
            case "procurar":
                await EnviarDescobertaAsync();
                break;
            case "importarAchados" when m.TryGetProperty("caminhos", out var lista) && lista.ValueKind == JsonValueKind.Array:
                _vm.ImportarAchados(lista.EnumerateArray().Select(c => c.GetString()).OfType<string>().ToList());
                await EnviarPastasAsync();
                await EnviarDescobertaAsync();
                break;
            case "adicionarRaiz" when Texto("pasta") is { } sugerida && Directory.Exists(sugerida):
                _vm.AdicionarRaiz(sugerida);
                await EnviarPastasAsync();
                await EnviarDescobertaAsync();
                break;
            case "adicionarPasta":
                var pastas = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Idioma.T("Pasta onde ficam seus repositórios") });
                if (pastas.FirstOrDefault()?.TryGetLocalPath() is { } pasta) _vm.AdicionarRaiz(pasta);
                await EnviarPastasAsync();
                await EnviarDescobertaAsync();
                break;
            case "adicionarProjeto":
                var projetos = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Idioma.T("Pasta do projeto") });
                if (projetos.FirstOrDefault()?.TryGetLocalPath() is { } projeto) _vm.AdicionarProjeto(projeto);
                await EnviarPastasAsync();
                await EnviarDescobertaAsync();
                break;
            case "removerPasta" when Texto("pasta") is { } remover:
                _vm.RemoverRaiz(remover);
                await EnviarPastasAsync();
                await EnviarDescobertaAsync();
                break;
            case "importar":
                var arquivos = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = Idioma.T("Importar um .code-workspace"),
                    FileTypeFilter = [new FilePickerFileType(Idioma.T("Workspace do VS Code")) { Patterns = ["*.code-workspace"] }],
                });
                if (arquivos.FirstOrDefault()?.TryGetLocalPath() is { } arquivo) _vm.Importar(arquivo);
                await EnviarPastasAsync();
                await EnviarDescobertaAsync();
                break;
            case "github":
                await EnviarGithubAsync();
                break;
            case "githubInstalar":
                await InstalarGhAsync();
                break;
            case "githubEntrar":
                EntrarNoGh();
                break;
            case "githubAtivar" when Texto("conta") is { } conta:
                await Core.Git.GitHub.TornarAtivaAsync(conta);
                await EnviarGithubAsync();
                break;
            case "tema" when Texto("id") is { } tema:
                _vm.UsarTemaCommand.Execute(tema);
                break;
            case "solido" when m.TryGetProperty("valor", out var v):
                _vm.Solido = v.ValueKind == JsonValueKind.True;
                break;
            case "concluir":
            case "pular":
                _concluida = true;
                _vm.Concluir();
                Close();
                break;
        }
    }

    async Task InstalarAsync(IReadOnlyList<string> ids)
    {
        if (_instalando) return;
        _instalando = true;
        try
        {
            foreach (var id in ids)
            {
                if (_vm.Requisitos.FirstOrDefault(r => r.Receita.Id == id) is not { Ok: false } requisito || !requisito.Receita.Instalavel) continue;
                Enviar(new JsonObject { ["type"] = "instalando", ["id"] = id, ["linha"] = Idioma.T("Começando…") });
                var ultima = DateTime.MinValue;
                var ok = await _vm.InstalarAsync(requisito, linha =>
                {
                    if ((DateTime.UtcNow - ultima).TotalMilliseconds < 120) return;
                    ultima = DateTime.UtcNow;
                    Dispatcher.UIThread.Post(() => Enviar(new JsonObject { ["type"] = "instalando", ["id"] = id, ["linha"] = linha }));
                });
                Enviar(Requisitos());
                Enviar(new JsonObject { ["type"] = "instalado", ["id"] = id, ["ok"] = ok });
            }
        }
        finally
        {
            _instalando = false;
            Enviar(new JsonObject { ["type"] = "login", ["ok"] = _vm.Logado, ["temClaude"] = _vm.Requisitos[0].Ok });
        }
    }

    async Task EnviarGithubAsync()
    {
        Instalacao.AtualizarPath();
        var instalado = Instalacao.NoPath("gh.exe") is not null;
        var contas = instalado ? await Core.Git.GitHub.ContasDoGhAsync() ?? [] : [];
        Enviar(new JsonObject
        {
            ["type"] = "github",
            ["instalado"] = instalado,
            ["instalavel"] = Instalacao.Gh.Instalavel,
            ["comando"] = "winget install GitHub.cli",
            ["contas"] = new JsonArray([.. contas.Select(c => (JsonNode)new JsonObject { ["conta"] = c.Conta, ["ativa"] = c.Ativa })]),
        });
    }

    async Task InstalarGhAsync()
    {
        if (_instalando) return;
        _instalando = true;
        try
        {
            var ultima = DateTime.MinValue;
            await Instalacao.InstalarAsync(Instalacao.Gh, linha =>
            {
                if ((DateTime.UtcNow - ultima).TotalMilliseconds < 120) return;
                ultima = DateTime.UtcNow;
                Dispatcher.UIThread.Post(() => Enviar(new JsonObject { ["type"] = "githubInstalando", ["linha"] = linha }));
            });
        }
        finally
        {
            _instalando = false;
            Enviar(new JsonObject { ["type"] = "githubInstalado" });
            await EnviarGithubAsync();
        }
    }

    static void EntrarNoGh()
    {
        Instalacao.AtualizarPath();
        if (Instalacao.NoPath("gh.exe") is not { } gh) return;
        try
        {
            Sistema.Abrir(new ProcessStartInfo("cmd.exe", $"/k \"\"{gh}\" auth login --web --hostname github.com --git-protocol https --scopes project\"") { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    async Task EnviarTudoAsync()
    {
        Enviar(new JsonObject
        {
            ["type"] = "estado",
            ["tema"] = _vm.TemaEscolhido,
            ["solido"] = _vm.Solido,
            ["versao"] = typeof(Apresentacao).Assembly.GetName().Version?.ToString(3),
            ["passo"] = _vm.Passo,
            ["temas"] = new JsonArray([.. _vm.OutrosTemas.Select(c => (JsonNode)new JsonObject
            {
                ["id"] = c.Id,
                ["nome"] = c.Nome,
                ["l"] = Hex(c.Tema.Cor("Lateral")),
                ["f"] = Hex(c.Tema.Cor("Fundo")),
                ["t"] = Hex(c.Tema.Cor("Texto")),
                ["m"] = Hex(c.Tema.Cor("Marca")),
            })]),
        });
        Enviar(Requisitos());
        Enviar(new JsonObject { ["type"] = "login", ["ok"] = _vm.Logado, ["temClaude"] = _vm.Requisitos[0].Ok });
        await EnviarPastasAsync();
        await EnviarDescobertaAsync();
    }

    static string Hex(Avalonia.Media.Color cor) => $"#{cor.R:X2}{cor.G:X2}{cor.B:X2}";

    JsonObject Requisitos() => new()
    {
        ["type"] = "requisitos",
        ["itens"] = new JsonArray([.. _vm.Requisitos.Select(r => (JsonNode)new JsonObject
        {
            ["id"] = r.Receita.Id,
            ["nome"] = r.Nome,
            ["uso"] = r.Uso,
            ["ok"] = r.Ok,
            ["detalhe"] = r.Detalhe,
            ["comando"] = r.ComoInstalar,
            ["instalavel"] = r.Receita.Instalavel,
            ["site"] = r.Receita.Site,
            ["passos"] = new JsonArray([.. r.Receita.Passos.Select(p => (JsonNode)JsonValue.Create(p))]),
        })]),
    };

    async Task EnviarPastasAsync()
    {
        var texto = await _vm.ContarProjetosAsync();
        Enviar(new JsonObject
        {
            ["type"] = "pastas",
            ["raizes"] = new JsonArray([.. _vm.Raizes.Select(r => (JsonNode)new JsonObject { ["pasta"] = r.Pasta, ["existe"] = r.Existe })]),
            ["texto"] = texto,
        });
    }

    async Task EnviarDescobertaAsync()
    {
        var d = await _vm.ProcurarNoVsCodeAsync();
        Enviar(new JsonObject
        {
            ["type"] = "vscode",
            ["itens"] = new JsonArray([.. d.Novos.Select(a => (JsonNode)new JsonObject
            {
                ["caminho"] = a.Caminho,
                ["nome"] = a.Nome,
                ["workspace"] = a.Workspace,
            })]),
            ["jaNoNexora"] = d.JaNoNexora,
            ["raizes"] = new JsonArray([.. d.Raizes.Select(r => (JsonNode)new JsonObject { ["pasta"] = r.Pasta, ["quantos"] = r.Quantos })]),
        });
    }

    public async Task<bool> FotografarAsync(string caminho)
    {
        if (_web.Web is not { } web) return false;
        await using var arquivo = File.Create(caminho);
        await web.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, arquivo);
        return true;
    }

    void Enviar(JsonObject mensagem)
    {
        try
        {
            _web.Web?.PostWebMessageAsString(mensagem.ToJsonString());
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
        }
    }
}
#endif
