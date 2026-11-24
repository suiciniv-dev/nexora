# Privacy

Nexora collects no telemetry. It has no analytics or crash reporting library, and nothing about how you use it leaves your computer unless you turn on an option that says what goes and to whom.

This is enforced by a test, not only by this page: [`PrivacidadeTestes`](tests/Nexora.Testes/PrivacidadeTestes.cs) fails the build if the code gains a telemetry library, opens an agent login file, reads an account identifier from an agent's settings, or contacts an internet address that is not listed below.

## What leaves your computer

| When | Where | What |
|---|---|---|
| Checking for updates, when Nexora starts | `api.github.com` (the `suiciniv-dev/nexora-releases` repository) and the release download on `github.com` | A normal web request with `Nexora/<version>` as the user agent |
| You use the GitHub features (pull requests, issues, activity) | GitHub, through your own `gh` | What `gh` sends with your account |
| Repository icons | `github.com/<owner>.png` | A normal web request |
| News on the Dashboard, **off by default** | Release feeds on `github.com`, `github.blog` and `hnrss.org` | Normal web requests |
| Cloud machines, **only if you set one up** | Your own server over SSH, `api.instabox.sh` or `api.freestyle.sh` | Your code (as a git bundle) and the agent's work on that machine |
| Weekly performance summary, **off by default** | `nexora.suiciniv.dev/api/desempenho`, which emails it to Nexora's developer | Memory, CPU and freeze numbers, the operating system family, core count, RAM size, and the names of Nexora's own code functions where the window stalled. No conversations, prompts, projects, file names, user names or machine names |
| The built-in browser | The pages you or an agent open | What any browser sends |

The agents themselves (Claude Code, Codex, Gemini CLI and others) talk to their providers under their own terms, including their own telemetry. Nexora starts them unmodified and does not change what they send.

## What Nexora reads on your computer

- Agent login files (`~/.claude/.credentials.json`, the Claude Code item in the macOS Keychain, `~/.codex/auth.json`): Nexora only checks that they exist, to tell you whether an agent is signed in. It never opens them.
- `~/.claude.json`: only the list of MCP servers you turned off for the project that is open.
- Agent transcripts (`~/.claude/projects` and the equivalents of other agents), to show history and plan usage on your screen.

## What Nexora stores

Settings, history and the local performance log (kept on your computer, and sent only if you turn on the weekly summary) stay in Nexora's data folder. Secrets you hand to it (the protected secret form, cloud provider keys and, for cloud machines, a `claude setup-token`) are encrypted with DPAPI on Windows or the Keychain on macOS, on your computer.
