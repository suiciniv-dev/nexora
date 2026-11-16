# Security Policy

## Reporting a vulnerability

Please do not open a public issue for a security problem.

Use GitHub's private reporting: go to the **Security** tab of this repository and choose **Report a vulnerability**. Include what you found, the steps to reproduce it, the version, and your operating system.

You can expect an answer within a few days. This is a small project maintained by one person, so please be patient. Once a fix is released, we are happy to credit you if you want.

## Supported versions

Only the latest release receives security fixes.

## What matters most here

Nexora runs coding agents that can read and change files and run commands on your machine or on a cloud machine, so these areas are the most sensitive:

- Permission requests: anything that lets an agent run a command or write a file without the permission the user chose.
- Secrets: values saved through the protected form are stored with Windows DPAPI or the macOS Keychain. Anything that leaks them into the chat, logs, config files or the agent's context is a vulnerability.
- Cloud machines: SSH keys, API keys and tokens for InstaBox, Freestyle or `claude setup-token`.
- The `nexora` command and its local connection to the running app.
- The update and installer flow.

## Out of scope

- Problems in the agent CLIs themselves (Claude Code, Codex and others). Report those to their maintainers.
- Findings that need an attacker who already controls the user's account or machine.
- The release builds are not code-signed; this is known.
