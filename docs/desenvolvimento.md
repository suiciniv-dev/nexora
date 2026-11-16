# Desenvolvimento do Nexora

IDE desktop para trabalhar com várias sessões do Claude Code: chats, conversas herdadas do histórico, ambientes e worktrees, arquivos com editor e diff, e uma visão geral ao vivo de todas as sessões. O orquestrador vem na fase 5. O plano completo está em [plano.md](plano.md).

Precisa do Claude Code instalado e logado (`claude` no PATH ou em `~/.local/bin`). O Nexora não lê nem guarda credencial: cada sessão é um processo do `claude` oficial.

## Rodar

```powershell
dotnet run --project src/Nexora                          # desenvolvimento
dotnet publish src/Nexora -c Release -o D:\NexoraApp     # publicar
dotnet D:\NexoraApp\Nexora.dll                           # abrir o publicado
```

O atalho da Área de Trabalho abre por `conhost.exe --headless dotnet.exe D:\NexoraApp\Nexora.dll`, sem janela de console.

## Testes

```powershell
dotnet test tests/Nexora.Testes
$env:NEXORA_INTEGRACAO = "1"; dotnet test tests/Nexora.Testes --filter Integracao   # conversa real no Haiku
```

## Opções para testar sem clicar

| Opção | Faz |
|---|---|
| `--demo` | enche a janela com uma conversa de exemplo |
| `--foto arquivo.png [--espera ms]` | grava a janela num PNG e fecha |
| `--abrir N` | abre a conversa N da lista do histórico |
| `--ambiente Nome` | abre a página do ambiente |
| `--nova` | abre uma "Nova conversa" em branco |
| `--pasta D:\x --enviar "texto" [--modo N] [--modelo N]` | abre uma sessão nova e manda a mensagem |
| `--raiz D:\x --ambiente Nome --tarefa "nome"` | procura ambientes só em `D:\x` e cria uma nova tarefa (worktree) |
| `--pasta D:\x --arquivo D:\x\a.cs --painel --alteracoes` | abre um arquivo no editor, o painel e o diff da primeira alteração |
| `--pasta D:\x --mencao termo` | escreve `@termo` no campo de mensagem e abre a lista |
| `--pasta D:\x --terminal` | abre a sessão com o terminal integrado aberto (Windows) |
| `--visao` | abre a visão geral |
| `--temas` / `--editar-tema` | abre a galeria de temas / o editor a partir do tema atual |
| `--tema id` | usa um tema: `padrao`, `padrao-claro`, `dracula`, `alucard`, um tema animado (`grafo`, `grafo-claro`, `constelacao`, `carta-celeste`, `via-lactea`, `nebulosa`, `pulso`, `aurora`) ou um seu; os ids dos temas animados antigos caem no novo mais parecido |
| `--fundo animado\|parado\|solido` | força o modo do fundo |
| `--novo-ambiente` | abre a página de novo ambiente |
| `--isolada` | sessões sem os hooks e configurações do usuário (`--setting-sources project,local`) e sem gravar o `config.json`; ligado sozinho com `--pasta` |
