# Building, testing and releasing

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows 10/11 or macOS. On Windows the app targets `net10.0-windows10.0.19041.0` (WebView2, pseudo console); elsewhere it targets `net10.0`.
- For the Windows installer: [Inno Setup 6](https://jrsoftware.org/isinfo.php). For the Mac DMGs: a Mac with `hdiutil`.

## Build and run

```powershell
dotnet build Nexora.slnx
dotnet run --project src/Nexora
dotnet run --project src/Nexora -- --demo --isolada     # sample data, no agent needed
```

`scripts/testar.ps1` runs the tests and a Release build of the app, and reports which step failed.

## Tests

```powershell
dotnet test tests/Nexora.Testes
```

The suite (about 700 tests) runs headless: UI tests render real Avalonia views without a display, and agent sessions are driven by `tools/ClaudeSimulado`, a fake `claude` that speaks the stream-json protocol. No agent, login or network is needed.

| Folder | Covers |
|---|---|
| `tests/Nexora.Testes` (root) | Core logic: Claude protocol, history, git, GitHub parsing, secrets, orchestrator, MCP hub |
| `Acp/`, `Codex/`, `Provedores/` | The other agent protocols and the agent catalog |
| `Cli/` | The `nexora` command and its local channel |
| `Nuvem/` | Cloud machines, with a fake machine and fake provider APIs |
| `Tela/` | Headless UI: layouts, keyboard, accessibility, responsiveness, screenshots, the visual tour |
| `Avatares/` | Avatar renderer against fixtures |

A few UI timing tests can be flaky on a busy machine. If one fails, run it alone before assuming a regression:

```powershell
dotnet test tests/Nexora.Testes --filter "FullyQualifiedName~NameOfTheTest"
```

### Opt-in tests and variables

These are skipped unless the variable is set:

| Variable | Effect |
|---|---|
| `NEXORA_INTEGRACAO=1` | Runs a real conversation with Claude Code on Haiku (`--filter Integracao`). Needs Claude Code installed and logged in. |
| `NEXORA_ROTEIRO` | Script for the visual tour (see below) |
| `NEXORA_FOTOS` | Folder where screenshot tests and the tour save PNGs |
| `NEXORA_IDIOMA` | Language of the tour (`pt` or `en`) |
| `NEXORA_CONVERSA_LONGA` | Number of turns in the tour's long sample conversation |
| `NEXORA_FUNDOS_PASTA` | Folder for animated background snapshots |
| `NEXORA_MEDIR=1`, `NEXORA_MEDIR_WORKTREE` | Performance measurements against a real repository |

## Visual tour

`scripts/passeio.ps1` runs the demo app headless and follows a plain-text script of steps (`clicar`, `direito`, `passar`, `rolar`, `digitar`, `tecla`, `pagina`, `tamanho`, `foto`). At each `foto` it saves the screenshot, a copy with every clickable element numbered, and the list of those elements. `log.txt` records the response time of every step and flags slow steps, freezes, clicks that hit another element and clicks with no effect.

```powershell
scripts\passeio.ps1 -Roteiro tour.txt -Saida fotos
```

Use this, or `--demo --foto`, to check visual changes. Do not script real mouse or keyboard input on a desktop.

## Publishing locally

```powershell
dotnet publish src/Nexora -c Release -r win-x64 --self-contained false -o <folder>
dotnet <folder>\Nexora.dll
```

`scripts/publicar.ps1` does this into the maintainer's install folder. It refuses to run while Nexora is open. The core process (`dotnet Nexora.dll --servidor …`) counts as open: it stays up about 30 seconds after the last window closes, so wait for it before publishing.

## Releases

Pushing a tag `vX.Y.Z` runs `.github/workflows/instalador.yml`:

1. **Windows** (`windows-latest`): runs the tests, publishes a self-contained `win-x64` build and a framework-dependent one, builds `Nexora-Setup-<version>.exe` with Inno Setup (`instalador/Nexora.iss`) and zips `Nexora-Portatil-<version>.zip` (`instalador/portatil`), then creates the GitHub release.
2. **macOS** (`macos-latest`): publishes for Apple Silicon and Intel, wraps each in an `.app` with `scripts/mac/montar-app.sh`, builds the DMGs with `hdiutil` and uploads them to the release.

The version comes from `<Version>` in `src/Nexora/Nexora.csproj`, and the release notes from `notas/vX.Y.Z.md`. `scripts/publicar-release.ps1` copies the files to [`suiciniv-dev/nexora-releases`](https://github.com/suiciniv-dev/nexora-releases), which is what the app's updater and the website's download links use.

Builds are not code-signed.
