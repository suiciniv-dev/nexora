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
- Memory is bounded: caches have a size limit and history loads in pages.

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

## Updates

On start, the app checks the latest release of [`suiciniv-dev/nexora-releases`](https://github.com/suiciniv-dev/nexora-releases) (`src/Nexora/Atualizacoes.cs`). `--sem-atualizacao` hides the update notice.
