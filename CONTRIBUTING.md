# Contributing to Nexora

Thanks for wanting to help. Issues, ideas and pull requests are welcome, in English or Portuguese.

## Before you start

- Look for an existing issue first. For anything bigger than a small fix, open an issue to talk about it before writing code, so we agree on the direction.
- Small, focused pull requests are easier to review than large ones.
- Never put a secret in an issue, a test or a screenshot. Security problems go through [SECURITY.md](SECURITY.md), not a public issue.

## Setup

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and, to use the app for real, at least one supported agent CLI installed and logged in.

```powershell
git clone https://github.com/suiciniv-dev/nexora.git
cd nexora
dotnet run --project src/Nexora
dotnet test tests/Nexora.Testes
```

`--demo --isolada` opens the app with sample data and without touching your real settings. [docs/desenvolvimento.md](docs/desenvolvimento.md) lists the other command-line options (Portuguese).

## Project layout

| Path | What is there |
|---|---|
| `src/Nexora` | The Avalonia app: views, view models, themes, translations |
| `src/Nexora.Core` | Logic without UI: agent sessions, providers, cloud machines, secrets, git and GitHub |
| `tests/Nexora.Testes` | Unit and headless UI tests (xUnit) |
| `tools/ClaudeSimulado` | A fake agent used by tests |
| `site` | The project website |
| `scripts` | Build, publish and screenshot scripts |
| `docs` | Plans, notes and agent test guides |

## Conventions

- **Language.** Identifiers and UI source strings are in Portuguese. Every user-visible string goes through `Idioma.T(...)` (or `{l:T '...'}` in XAML), and its English text goes in `src/Nexora/Traducao/Ingles.*.cs`. The Portuguese text is the key, so keep both in sync. A new string without a translation will show up untranslated in English.
- **No code comments.** Prefer clear names and small methods.
- **New visual components** go in `src/Nexora/Componentes.axaml`.
- **Tests.** Add or update a test with every behavior change. The suite runs headless, so it works without a display or an installed agent. A few UI tests can be flaky under load; if one fails on your machine, run it alone before assuming you broke it.
- **Do not simulate clicks or keystrokes on a real desktop** from scripts. For visual checks use `scripts/passeio.ps1` (a scripted headless tour that saves screenshots) or `--demo --foto`.

## Commits and pull requests

- Branch from `main`.
- Write commit messages in English, in the imperative, with a short first line (for example `Show the portable build as a button`).
- In the pull request, say what changed and why, and how you tested it. Add a screenshot for visible changes.
- Make sure `dotnet test tests/Nexora.Testes` passes before asking for review.

## Adding or testing an agent

Agents are described in `src/Nexora.Core/Provedores/CatalogoAgentes.cs`. The checklist in [docs/testar-agentes.md](docs/testar-agentes.md) shows what to verify for each one: model list, permissions, interrupt, history, restart and error handling.

## License

By contributing you agree that your work is released under the [MIT License](LICENSE).
