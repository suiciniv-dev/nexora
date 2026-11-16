# Running agents locally and on cloud machines

Every conversation chooses where its agent runs: **This PC** or a **cloud machine**. Locally, the agent CLI is a child process of Nexora. On a cloud machine, it runs on a Linux box over SSH and keeps working with your PC off.

Cloud machines currently support Claude Code only. Codex and ACP agents always run locally.

## Local runs

The agent process starts in the project folder (or the task's worktree) with the user's environment. On Windows it goes into a job object, so ending the session also ends everything it started. Closing the app ends local agents.

## Cloud machines

### Providers

Set up in **Settings › Cloud machines**.

| Provider | How it connects | Creates machines | Pause | Notes |
|---|---|---|---|---|
| Your machine over SSH | Nexora generates an ed25519 key; you paste the public line into `~/.ssh/authorized_keys` on the server | no | no | Any Linux server you can reach |
| InstaBox | API key; Nexora creates the machine and connects | yes | yes | Pausing keeps only the disk |
| Freestyle | API key; the token is used as the SSH password | yes | yes | Written from the documentation, not yet tested with a real account |

Providers implement `IProvedorDeMaquinas` (`src/Nexora.Core/Nuvem/Maquinas.cs`): create, resume, pause, publish a port and read the quota. Adding a provider means implementing that interface.

The private SSH key stays on this PC, in a folder only your user can read. Nexora uses the system `ssh` and `ssh-keygen` (OpenSSH on Windows), with its own known-hosts file per machine.

### Turning it on for a project

1. Add a machine in **Settings › Cloud machines** and test the connection.
2. Optional: paste the token from `claude setup-token` in **Claude login on the machine**, so Claude on the machine uses your plan without logging in there.
3. On the project page, turn on **Can go to the cloud**.
4. When starting a conversation, pick the machine next to the agent.

When this PC's memory is above 85%, a new conversation suggests the cloud.

### What happens when a conversation starts

The tab shows each step:

1. **Connecting**: resumes the machine if it was paused and opens SSH.
2. **Syncing the code** (`CodigoNaNuvem.cs`): the current branch is packed as a `git bundle`, sent, and checked out on the machine. Uncommitted changes already on the machine are stashed first (`nexora: before sync <date>`).
3. **Starting the agent**: installs Claude Code if it is missing, then starts it with the same arguments as a local session (see [agents.md](agents.md)).
4. **Running**.

### Detached runs

The agent does not depend on the SSH connection. `ScriptRemoto.cs` installs a small shell script that:

- creates a private folder per run (`chmod 700`) with `entrada` (input), `saida` (output) and `erros` (errors) files;
- starts the agent under `setsid`, reading from `entrada` through `tail -f` and writing to `saida`;
- receives environment variables (like the Claude token) on stdin and exports them to the agent process only, without writing them to disk;
- answers `iniciar`, `ler` (read output from an offset) and `parar`.

Nexora appends messages to `entrada` and reads `saida` from where it stopped. If the connection drops, the tab shows **Disconnected** and keeps retrying; nothing is lost, because the agent keeps writing to the file.

### Keeping the machine awake

While a cloud conversation is **working** and Nexora is open, Nexora tells InstaBox the machine is busy (a 10-minute lease renewed every 4 minutes). When the turn ends, the lease is not renewed and the provider's own idle rules apply.

### Ending a cloud conversation

Closing the tab or the app only disconnects. The agent process on the machine is not stopped: it stays there, idle, waiting for the next message, so the conversation can continue later. To stop it, pause or delete the machine in **Settings › Cloud machines**.

Keep this in mind for billing: an InstaBox or Freestyle machine that is not paused keeps costing even with the agent idle.

### Bringing the work back

**Bring it here** fetches the branch and the uncommitted changes from the machine into a worktree on this PC, and opens a conversation there for the commit and the pull request.

### Run targets on the machine

**Run** can also start a project's run targets on the cloud machine. When the provider can publish ports, the server's port is forwarded to `localhost` on this PC. Stopping a run target stops it on the machine.

## Files involved

| File | Role |
|---|---|
| `Nuvem/ServicoDaNuvem.cs` | Machines, provider keys, the Claude token, active runs (`nuvem.json`) |
| `Nuvem/Transportes.cs` | SSH transport and the SSH provider |
| `Nuvem/InstaBox.cs`, `Nuvem/Freestyle.cs` | Provider API clients |
| `Nuvem/ChavesSsh.cs` | Key generation |
| `Nuvem/CodigoNaNuvem.cs` | Sending and bringing back code |
| `Nuvem/ScriptRemoto.cs`, `Nuvem/ProcessoRemoto.cs` | The remote script and the process that follows it |
| `Nuvem/ExecucaoNaNuvem.cs` | Run targets on the machine |
