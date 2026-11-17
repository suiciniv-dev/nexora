# Architecture

Nexora is a .NET 10 desktop app built with [Avalonia 12](https://avaloniaui.net/). It does not talk to any model API itself: every conversation is a process of an official agent CLI (Claude Code, Codex, Gemini CLI…), and Nexora drives that process through the CLI's own machine-readable protocol.

```
┌──────────────────────────── Nexora (one process) ────────────────────────────┐
│                                                                              │
│  src/Nexora  (UI)                         src/Nexora.Core  (no UI)           │
│  Views (.axaml) ── ViewModels ───────────▶ ISessaoAgente                     │
│                    (MVVM, CommunityToolkit)  ├─ SessaoClaude  ── stream-json ─┼─▶ claude -p
│                                              ├─ SessaoCodex   ── JSON-RPC ────┼─▶ codex app-server
│                                              └─ SessaoAcp     ── ACP ─────────┼─▶ gemini --acp, …
│                                                                              │
│  Orchestrator MCP server  (http://localhost:<port>/mcp)  ◀── agents call it  │
│  MCP hub                  (http://localhost:<port>/mcp/<key>)                │
│  Local channel (named pipe, current user only)  ◀── `nexora` command         │
└──────────────────────────────────────────────────────────────────────────────┘
          │ ssh (optional)
          ▼
   Cloud machine: agent runs detached, survives disconnects
```

## Projects

| Project | Role |
|---|---|
| `src/Nexora.Core` | Everything that does not need a window: agent sessions and protocols, history, git and GitHub, cloud machines, secrets, MCP servers, scheduling, performance probes. Targets plain `net10.0`, so it is testable without a UI. |
| `src/Nexora` | The Avalonia app: views (`Views/*.axaml`), view models (`ViewModels`), themes and animated backgrounds (`Temas`, `Fundos`), translations (`Traducao`), and app services (config, updates, windows, the `nexora` command entry point). On Windows it targets `net10.0-windows10.0.19041.0` for WebView2 and the pseudo console. |
| `tests/Nexora.Testes` | xUnit tests. Core tests plus headless UI tests (`Tela/`) that render real views without a display. |
| `tools/ClaudeSimulado` | A fake `claude` that replays protocol events, used by tests. |

The code is written in Portuguese (identifiers, UI source strings). English UI text comes from `src/Nexora/Traducao`. See [CONTRIBUTING.md](../CONTRIBUTING.md).

## Code layout in `src/Nexora`

Each first-level folder is a layer, and its namespace is `Nexora.<Folder>`. Subfolders only group files of one feature and keep the layer's namespace. `ViewModels` and `Views` use the same feature folders (`Chat`, `Janela`, `Conversas`, `Ambientes`, `Issues`, `PullRequests`, `Painel`, `Editor`, `Dashboard`, `AoVivo`, `Orquestra`, `Navegador`, `Nuvem`, `Executar`, `Agenda`, `Uso`, `Desempenho`, `Temas`, `Configuracoes`, `Inicio`), so a view and its view model sit in folders of the same name. Chat row view models live in `ViewModels/Chat/Itens`.

| Folder | Holds | May depend on |
|---|---|---|
| root | `Program` and `App` only | everything |
| `Aplicativo` | Startup, the `nexora` command entry, window coordination and anything that wires views to view models | everything |
| `Views` | Windows and views (`.axaml` with code-behind), and controls or helpers tied to a view model | view models, controls, services, models |
| `Controles` | Controls drawn in code that know no view model (`MarkdownTexto`, `Icone`, charts, backgrounds) | services, models |
| `ViewModels` | View models only: `*VM` classes, chat row view models (`Chat/Itens/Item*`) and the contracts between view models (`IAnfitriao`) | services, models, `Nexora.Core` |
| `Modelos` | Plain enums and records the view models share. No Avalonia, no view models | `Nexora.Core` |
| `Servicos` | App services that know no view model or view (config, system, images, updates) | models, `Nexora.Core` |
| `Desempenho` | Responsiveness monitor and frame measurement | everything (diagnostics) |
| `Temas`, `Fundos`, `Traducao` | Themes, animated backgrounds, translations | services |

Rules:

- One top-level type per file, named after the type.
- A control that needs a view model type is part of a view: it goes to `Views`, not `Controles`.
- `Modelos`, `Servicos` and `Controles` never reference `Nexora.ViewModels` or `Nexora.Views`.
- `Nexora.Modelos`, `Nexora.Servicos`, `Nexora.Controles`, `Nexora.Aplicativo` and `Nexora.Desempenho` are global usings (`Nexora.csproj`). XAML uses the prefixes `vm`, `v`, `c` (controls), `m` (models) and `l` (translation).

## Performance rules

The UI thread only lays out and draws. These rules come from measured stalls:

- No disk, network or process work on the UI thread: no `Process.Start`, `File.*`, fsync or git reads in property getters, bindings or event handlers. Use `Task.Run` and post the result back. File writes from UI code go through `ArquivoSeguro.GravarDepois`.
- Streaming updates are incremental: change only what changed. `MarkdownTexto` keeps finished blocks and updates the last one in place; never rebuild a whole control tree per delta.
- Hidden views do no work for content nobody sees; they catch up when shown.
- Memory is bounded: caches have a size limit and history loads in pages. A chat view keeps the last 40 rows mounted (up to 60), three tab views stay mounted, and the Live board keeps two peeks.
- Global styles cost every control of the targeted type, even controls without the class: each one gets a style instance and an activator per matching style. Style classed boxes with `c|Caixa.classe` (`<c:Caixa Classes="…">`) and classed buttons with `c|Botao.classe` (`<c:Botao Classes="…">`, which keeps the Fluent button theme); never add `Border.classe` or `Button.classe` styles, since both types are in every template. A control that should look like a classed button reports `Botao` as its style key.
- Allocation budgets in `tests/Nexora.Testes/Tela/OrcamentoTestes.cs` fail when a hot path allocates far more than it should. They count bytes on the test thread, not milliseconds.

## Core process

Garbage collection pauses the whole process, so heavy reads in the app process pause the UI even when they run off the UI thread. Work moves to a second process step by step, behind `INucleo` (`src/Nexora.Core/Servidor`):

- The pipe name comes from the user, the data folder and the build (the module version id of `Nexora.dll`), see `ServidorDoNucleo.NomeDoCano`. On start the app connects to a server already listening on that name, and only when there is none it starts one (`Aplicativo/ProcessoDoNucleo.cs`). On Windows the server runs as `dotnet Nexora.dll --servidor <pipe>`, because endpoint protection may block `Nexora.exe` from starting another `Nexora.exe`; the exe is the fallback, and a failed start is written to `erros.log`. The server is not added to the app's job object, so it outlives the app. Messages are one JSON object per line with request ids, so requests run in parallel, on a pipe restricted to the current user.
- `App.Nucleo` starts as `NucleoLocal` (in process) and switches to `NucleoRemoto` once the pipe connects. Every remote call falls back to the local implementation when the server is missing, answers with an error or does not answer within 60 seconds; if the pipe breaks, the app stays local for the rest of the session. The server serves several apps at once, a second server for the same name gives up (exit code 2), and it exits after 30 seconds with no app connected and nothing busy (`ocupado`, which live sessions will feed).
- Agent sessions run in the server: `ChatVM` creates a `SessaoRemota` (`src/Nexora.Core/Servidor`), which forwards every call over the pipe to `SessoesDoServidor`, and the server builds the real `SessaoClaude`, `SessaoCodex` or `SessaoAcp` with `FabricaDeSessoes`. Cloud sessions stay in the app, since they already run detached on the remote machine. Each server session numbers its events and keeps the last 5000 (`RegistroDeEventos`); the server pushes them to every subscribed app as `{"sessao", "numero", "evento"}` lines, and calls on one session are handled in the order they arrive.
- Closing the app ends idle sessions as before. A session that is working or waiting for a permission is released instead (`SessaoRemota.Soltar`) and keeps running in the server; a released session that goes idle for 3 minutes is ended there. On start, the app lists the live sessions with no app attached and reopens each as a tab (`JanelaVM.ReabrirSessoesVivas`, `ChatVM.DeSessaoViva`): it loads the history from the agent's transcript, subscribes from the current event number, and shows pending permission requests and the working state that the server keeps.
- MCP: the hub lives in the server (`SessoesDoServidor.Hub`) and the app reaches it through `HubRemoto`, so the hub addresses handed to an agent stay valid while the agent outlives the app. The MCP servers that need the window (orchestrator, built-in browser, secrets) stay in the app behind a bridge in the server (`PonteMcp`): `ServidorMcp` registers each owner with the bridge and gives the agent the bridge address, the bridge forwards each call to the app's current port with the app's secret and the owner in `X-Nexora-Dono`, and answers "Nexora is closed" while no app is connected. Owners are `<app run>:<tab code>`, and a reopened tab maps the old owner to its new code (`ServidorMcp.Apelidar`), so tool calls from an agent started before the restart reach the right tab; a reopened orchestrator is reattached to its canvas.
- Demo and isolated windows, tests, and `"NucleoNoProprioProcesso": true` in the settings keep everything in process.
- Moved so far: the saved chat list, usage reading, the session tails read by the Live board, every `git` and `gh` command (`ComandoGit.Nucleo`), and the history pages: the server scans the file backwards and returns only the chosen lines (`TrechoBruto`), which the app parses into events. A command that reached the server is never run again locally if its answer is lost, since commands have side effects; the app reports the loss instead.

### Plan: agent sessions in the core process

Decision (17/11/2026): agents keep running when the app closes or updates, and tabs reconnect to them when it opens again. Steps, each shippable on its own:

1. Events cross the pipe: `JsonDoNucleo` serializes every `EventoClaude` subtype with its type name as discriminator, found by reflection; `EventosNoCanoTestes` fails when a new event type has no round-trip sample. Done.
2. Server lifetime: a fixed pipe name per user, data folder and build, so a new app finds the running server; the server leaves the app's job object and exits only when no app is connected and no session is alive for a while. The build in the name replaces a version handshake: after an update the new app starts its own server, an idle old one exits on its own and a busy one stays until its sessions end. Done. Open for step 4: a busy server keeps the install folder's files open, so the Windows installer (`/CLOSEAPPLICATIONS`) would close it; the server will have to run from a copy of the build, or the update will wait for its sessions.
3. Server-to-app messages: events carry a per-session sequence number, and the server keeps a bounded log per session so a reconnecting tab asks for everything after its last number. Done.
4. `SessaoRemota : ISessaoAgente` forwards calls to a session registry in the server. `PedirAsync` takes the control request body (`JsonObject`) instead of a function, so it crosses the process. Done.
5. The orchestrator MCP server and the MCP hub move to the server, so their localhost addresses stay valid while agents outlive the app. Done: the hub moved; the orchestrator, browser and secrets servers sit behind the server's MCP bridge.
6. On start, the app lists live sessions and reattaches their tabs; the performance monitor gets the agent process ids from the server; cloud (SSH) sessions follow. Done: `SessaoRemota` registers the agent's process id in `SessaoClaude.PidsDoNexora`, which the monitor and the Live board read, and cloud sessions already survive on their machine.

## Agent sessions

All agents implement `ISessaoAgente` (`src/Nexora.Core/Claude/ISessaoAgente.cs`): start, send a message (with images), allow or deny a permission request, interrupt, change mode or model, and raise `Evento` for everything the agent emits. The UI only sees these events, so it does not care which CLI is behind a tab.

`CatalogoAgentes` (`src/Nexora.Core/Provedores/CatalogoAgentes.cs`) lists every supported agent with its executable names, arguments, install command and protocol:

| Protocol | Class | How it runs | Notes |
|---|---|---|---|
| Claude | `SessaoClaude` | `claude -p --input-format stream-json --output-format stream-json --permission-prompt-tool stdio …` | Full support: resume and fork, model and effort, permission modes, MCP config, remote control. |
| Codex | `SessaoCodex` | `codex app-server` | Resume, model and effort; messages sent mid-turn join the current turn. |
| ACP | `SessaoAcp` | The agent's ACP mode over stdio (for example `gemini --acp`) | Gemini CLI, Antigravity, Copilot, Cursor, OpenCode, Kiro, Qwen Code, Grok, goose. |

Permission requests arrive as events. Nexora shows them in the tab and on the live board, and answers with `Permitir` / `Negar`. Subagent requests carry the subagent that asked.

`ProcessoDoAgente` and `GrupoDeProcessos` own the OS processes. On Windows each agent goes into a job object, so ending a session also ends the processes it started.

## History

Nexora does not keep its own copy of conversations. It reads what the CLIs already store (for Claude Code, `~/.claude/projects/*.jsonl`) through `Historico/` and turns it into the same events a live session emits. Opening an old conversation and sending a message resumes it in the CLI (`--resume`).

## Environments and worktrees

An environment is a repository or a VS Code `.code-workspace` with linked folders (`Ambientes/`). A task can get its own branch and git worktree, created from the project's base branch, so several agents can work on the same repository without stepping on each other. `Git/` wraps `git` and `gh` for status, diffs, restore points, conflicts, issues boards, pull requests and checks.

## Orchestrator

`Orquestra/ServidorMcp.cs` is a small MCP server on `http://localhost:<port>/mcp`, started by the app. The orchestrator conversation gets it as an MCP server, with these tools (`Orquestra/Ferramentas.cs`):

`listar_ambientes`, `listar_sessoes`, `criar_agente`, `enviar_mensagem`, `ler_sessao`, `esperar`, `responder_permissao`, `parar_sessao`.

So the orchestrator is just another agent session that controls the others through tools. Each new agent it creates shows up as a normal tab and on the live board.

## MCP hub

Agent CLIs usually start their own copy of every MCP server a project uses. With several sessions open this multiplies memory use. `Mcp/HubMcp.cs` starts each MCP server once and exposes it to all sessions at `http://localhost:<port>/mcp/<key>`, caching list responses (`tools/list`, `prompts/list`, …). Servers start on first use.

## The `nexora` command

The same executable is the terminal command: `Nexora --cli …` runs `Cli.Rodar` and exits without opening a window (`src/Nexora/Program.cs`). It talks to the running app through a named pipe restricted to the current user (`Cli/CanalLocal.cs`). Commands answer in text or `--json`, and the exit code tells whether a session needs attention (`CodigosDaCli`).

## Cloud machines

`Nuvem/` lets a conversation run on a Linux machine instead of this PC:

- **Providers**: your own server over SSH (`ProvedorSsh`), InstaBox (`InstaBox.cs`) and Freestyle (`Freestyle.cs`). The provider creates or wakes the machine, and Nexora installs the agent CLI if it is missing.
- **Transport**: the system `ssh` (`TransporteSsh`), with a per-machine key and known-hosts file. For Freestyle, the token is passed as the SSH password through `SSH_ASKPASS`, pointing back to the Nexora executable.
- **Code sync** (`CodigoNaNuvem.cs`): the current branch is sent as a `git bundle`; uncommitted remote changes are stashed first. **Bring it here** does the reverse into a local worktree.
- **Detached runs** (`ScriptRemoto.cs`, `ProcessoRemoto.cs`): the agent runs on the machine under `setsid`, reading from an input file and writing to output files. If the connection drops, Nexora reconnects and continues reading where it stopped, and the agent keeps working with this PC off.
- **Login**: the Claude token from `claude setup-token` is stored locally as a secret and passed to the remote agent as an environment variable.

## Secrets

`Segredos/` stores values with Windows DPAPI (current user) or the macOS Keychain. When an agent needs a password or key, it asks through a form in the chat; the value goes to a `.env` file in the project or to an environment variable of the agent, and never goes through the conversation text.

## Other modules

| Folder | What it does |
|---|---|
| `Executar/` | Finds run targets in a project (launch profiles, npm scripts…) and runs them, locally or on a cloud machine with the port forwarded to `localhost`. |
| `Terminal/` | Windows pseudo console for the built-in terminal. |
| `Navegador/` | Built-in browser helpers (WebView2): addresses, navigation errors, and design-mode annotations sent to the agent. |
| `Revisao/` | Diff line comments and PR review comments sent to the agent. |
| `Agenda/` | Scheduled prompts. |
| `Uso/` | Plan and usage limits, read from local agent data. |
| `Desempenho/` | UI responsiveness probes: when the window freezes, it records what was running. |
| `Avatares/` | C# port of the open-bot-avatar renderer (see `THIRD-PARTY-NOTICES.md`). |
| `Senna/` | Optional integration with a local handoff store; inactive when it is not installed. |

## Data on disk

| What | Where |
|---|---|
| Settings | `%APPDATA%\Nexora\config.json` (Windows), `~/Library/Application Support/Nexora/config.json` (macOS) |
| Secrets | Encrypted with DPAPI next to the settings (`segredos/`), or in the macOS Keychain |
| Conversations | Wherever each agent CLI keeps them; Nexora only reads them |
| Live agent sessions | In the core process (`dotnet Nexora.dll --servidor …`) while they run; nothing is written to disk |

## Updates

On start, the app checks the latest release of [`suiciniv-dev/nexora-releases`](https://github.com/suiciniv-dev/nexora-releases) (`src/Nexora/Atualizacoes.cs`). `--sem-atualizacao` hides the update notice.
