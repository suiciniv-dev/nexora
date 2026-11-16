# Configuration, credentials and environment variables

## Settings

Settings are saved in `config.json`:

| System | Path |
|---|---|
| Windows | `%APPDATA%\Nexora\config.json` |
| macOS | `~/Library/Application Support/Nexora/config.json` |

Almost everything is set from the app (**Settings**). `--isolada` runs without reading or writing this file, which is what tests and screenshots use.

## Credentials

Nexora keeps as few credentials as it can. Each kind lives in a different place:

| Credential | Where it lives | Who uses it |
|---|---|---|
| Agent logins (Claude, Codex, Gemini…) | Wherever each CLI keeps it (for example `~/.claude`, `~/.codex/auth.json`) | Only the agent CLI. Nexora checks whether you are logged in but does not read the token. |
| GitHub | The `gh` CLI's own login | Nexora runs `gh`. With several accounts logged in, it picks the right one per repository by passing `GH_TOKEN` to that one `gh` call, without `gh auth switch`, and remembers which account worked. |
| Secrets an agent asks for | Windows DPAPI (current user) or the macOS Keychain | Written to the project's `.env` file or set as an environment variable on the agent, as the user chooses. The value never goes into the chat. |
| Cloud provider API keys (InstaBox, Freestyle) | DPAPI / Keychain | Nexora's calls to the provider API |
| Claude token for cloud machines (`claude setup-token`) | DPAPI / Keychain | Passed as `CLAUDE_CODE_OAUTH_TOKEN` to the agent process on the machine, through stdin; never written to the machine's disk |
| SSH keys for cloud machines | Private key in a folder only your user can read | The system `ssh` |

There is no secret in the repository, in `config.json` or in the logs. If you find one, please report it as described in [SECURITY.md](../SECURITY.md).

## Environment variables

### Read by the app

| Variable | Effect |
|---|---|
| `PATH`, `PATHEXT` | Where Nexora looks for agent CLIs, `git`, `gh` and `ssh` |
| `ANTHROPIC_MODEL` | Default Claude model when none is chosen, the same as in Claude Code |
| `ANTHROPIC_API_KEY`, `OPENAI_API_KEY` | Only checked to tell, on the welcome screen, whether Claude Code or Codex is already logged in |
| `CODEX_HOME` | Where Codex keeps its data and history |
| `GH_CONFIG_DIR`, `XDG_CONFIG_HOME` | Where the `gh` CLI keeps its accounts |
| `SHELL` | On macOS, the login shell Nexora asks for the full `PATH` (apps opened from Finder start with a minimal one) |
| `NEXORA_CANAL` | Name of the local channel the `nexora` command connects to. Only needed to point the command at a second, isolated Nexora. |
| `NEXORA_ARVORE` | With `1` and `--foto`, also writes the visual tree next to the screenshot (debugging layout) |

`NEXORA_SSH_SENHA` and `SSH_ASKPASS_REQUIRE` are set by Nexora itself when it calls `ssh` for Freestyle, so `ssh` can ask Nexora for the token as a password. Do not set them yourself.

### Used by tests

See [building.md](building.md).

## Command-line options

The app accepts options to open a specific screen, use sample data, take a screenshot and exit, or run isolated. They are listed in [desenvolvimento.md](desenvolvimento.md) (Portuguese). The most useful:

| Option | Does |
|---|---|
| `--demo` | Fills the window with sample conversations; needs no agent |
| `--isolada` | Sessions without the user's hooks and settings, and no writes to `config.json` |
| `--foto file.png [--espera ms]` | Saves the window to a PNG and exits |
| `--janela 1440x900` | Window size |
| `--sem-atualizacao` | Hides the update notice |

`Nexora --cli <command>` is the `nexora` terminal command. Install it from **Settings › Advanced** and run `nexora ajuda` for the list of commands.
