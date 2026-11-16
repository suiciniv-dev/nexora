# Agents and sessions

Nexora runs each conversation as a process of the agent's official CLI. It never calls a model API and never sees your agent login: the CLI uses its own credentials, exactly as it would in a terminal.

## Supported agents

| Agent | Command Nexora looks for | Arguments | Protocol | Install |
|---|---|---|---|---|
| Claude Code | `claude` | (see below) | Claude stream-json | `npm install -g @anthropic-ai/claude-code` |
| Codex | `codex` | `app-server` | Codex JSON-RPC | `npm install -g @openai/codex` |
| Gemini CLI | `gemini` | `--acp` | ACP | `npm install -g @google/gemini-cli` |
| Antigravity | `agy` | `acp` | ACP | antigravity.google/download |
| GitHub Copilot | `copilot` | `--acp` | ACP | `npm install -g @github/copilot` |
| Cursor | `cursor-agent` | `acp` | ACP | cursor.com/cli |
| OpenCode | `opencode` | `acp` | ACP | `npm install -g opencode-ai` |
| Kiro | `kiro-cli` | `acp --agent-engine=v3` | ACP | kiro.dev/cli |
| Qwen Code | `qwen` | `--acp` | ACP | `npm install -g @qwen-code/qwen-code` |
| Grok | `grok` | `agent stdio` | ACP | `npm install -g @xai-official/grok` |
| goose | `goose` | `acp` | ACP | block.github.io/goose |

The list lives in `src/Nexora.Core/Provedores/CatalogoAgentes.cs`. Nexora finds an agent by looking for its command on `PATH` (Claude Code is also found in `~/.local/bin`). After installing an agent, restart Nexora or use **Settings › See the agents on this computer** to search again.

## What each protocol supports

| | Claude Code | Codex | ACP agents |
|---|---|---|---|
| Streaming text and tool calls | yes | yes | yes |
| Permission requests (allow / deny) | yes | yes | yes |
| Model choice | yes | yes | when the agent lists models |
| Effort level | yes | yes | no |
| Resume a past conversation | yes | yes | no |
| Undo a turn and rewind the conversation | yes | yes | files only; the agent still remembers |
| Message sent while the agent works | queued until the turn ends | joins the current turn | queued until the turn ends |
| Subagents shown with their own permission requests | yes | no | no |
| Remote Control | yes | no | no |
| Cloud machines | yes | not yet | not yet |

## How a Claude Code session starts

`SessaoClaude` runs `claude` in print mode with a two-way JSON stream on stdin and stdout:

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio
       [--resume <id> [--fork-session]] [--model <m>] [--effort <e>]
       [--permission-mode <mode>] [--add-dir <dir>]… [--mcp-config <file>]
       [--append-system-prompt <text>] [--allowedTools <list>] [--settings <file>]
```

- `--permission-prompt-tool stdio` makes Claude Code ask Nexora, over the same stream, before running a tool that needs permission. Nexora shows the request and answers with the user's decision.
- `--include-partial-messages` gives the text token by token.
- `--add-dir` passes the other folders of a workspace.
- `--mcp-config` adds the orchestrator server and the MCP hub (see [architecture.md](architecture.md)).
- With `--isolada`, Nexora adds `--setting-sources project,local`, so the session ignores the user's own hooks and settings. Tests and screenshots use this.

Mode and model can change mid-session through control messages on the stream, without restarting the process.

## Codex

`SessaoCodex` runs `codex app-server` and speaks its JSON-RPC protocol: threads, turns, item events and approval requests. Conversations come back from Codex's own history.

## ACP agents

`SessaoAcp` speaks the [Agent Client Protocol](https://agentclientprotocol.com) over stdio: it creates a session, sends prompts, receives updates (text, tool calls, plans) and answers `session/request_permission`. ACP agents differ a lot in what they report, so some features (model list, slash commands, plans) only appear when the agent sends them.

## Adding an agent

If the agent speaks ACP over stdio, adding it is usually one entry in `CatalogoAgentes.Todos`: an id, a display name, the company, `ProtocoloAgente.Acp`, the executable names, the arguments that start ACP mode, the install hint and a short description. Add an icon in `site/img/agentes` if you want it on the website.

Then go through the checklist in [testar-agentes.md](testar-agentes.md) (Portuguese): choosing the agent, model list, first message, permissions, plan, interrupt, message mid-turn, slash commands, undo, history, restart and errors.

## Session states

Every tab has one of these states, which also drive the live board:

| State | Meaning |
|---|---|
| Working | The agent is running a turn. |
| Needs you | A permission request, a question or a secret request is waiting. |
| Ready | The turn ended. |
| Error / stopped | The process ended or failed. The next message starts it again and resumes the conversation when the agent supports it. |

## Testing without an agent

`tools/ClaudeSimulado` is a fake `claude` that answers on the stream-json protocol. Tests use it to drive real sessions without a network or a login. `--demo` fills the app with sample conversations and needs no agent at all.
