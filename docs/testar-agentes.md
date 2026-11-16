# Testar os agentes no Nexora

Cada agente roda com o login dele, neste computador. O Nexora só acha o agente se o comando estiver no PATH. Depois de instalar, feche e abra o Nexora, ou use **Configurações › Ver os agentes deste computador › Procurar de novo**.

Os comandos de instalação e login abaixo vêm da documentação de cada agente na data em que este guia foi escrito. Se algum mudar, vale o site oficial.

## Roteiro igual para todos

Faça numa pasta de teste (um repositório git qualquer), nunca num projeto de trabalho.

1. **Escolher**: nova conversa, botão do agente (ao lado do modelo), escolher o agente. A tela vazia deve mostrar o nome dele.
2. **Modelos**: abrir o menu de modelo. A lista deve vir do agente (pode levar alguns segundos na primeira vez). Escolher um modelo diferente do padrão.
3. **Primeira mensagem**: "Liste os arquivos desta pasta e me diga o que é o projeto." Deve aparecer o texto chegando aos poucos e as ferramentas (leitura, busca).
4. **Permissão**: no modo **Perguntar sempre**, pedir "Crie o arquivo teste.txt com a palavra ok." Deve aparecer o pedido de permissão. Testar **Permitir**, depois repetir com **Negar**, e depois **Permitir sempre** (quando o agente oferecer).
5. **Plano**: pedir uma tarefa de vários passos. Se o agente mandar plano, a lista de tarefas aparece.
6. **Interromper**: mandar algo demorado e clicar em **Parar**. O turno deve encerrar com "O turno foi interrompido."
7. **Mensagem no meio do turno**: enquanto ele trabalha, mandar outra mensagem. No Codex ela entra no turno atual; nos outros fica na fila e sai quando o turno acaba.
8. **Comandos de barra**: digitar `/` e ver se a lista do agente aparece (nem todos mandam).
9. **Desfazer turno**: no último turno, **Desfazer**. Os arquivos voltam. No Claude e no Codex a conversa volta junto; nos outros aparece o aviso de que o agente ainda lembra do que fez.
10. **Histórico**: fechar a aba, achar a conversa em **Conversas › Histórico** (a linha mostra o nome do agente), abrir de novo. A conversa deve reaparecer e a próxima mensagem deve continuar a mesma sessão.
11. **Reiniciar**: fechar e abrir o Nexora com a aba aberta. Ela deve voltar.
12. **Erros**: deslogar o agente (ou renomear o executável) e mandar uma mensagem. Deve aparecer um erro claro, sem travar a aba.

Anote o que falhar com o nome do agente, o passo e, se puder, um print.

## Por agente

### Codex (OpenAI)
- Precisa de um plano do ChatGPT que inclua o Codex.
- Instalar: `npm install -g @openai/codex`
- Login: `codex login`
- Extras para testar: modo **Planejar** (o Codex pode fazer perguntas, que aparecem como cartão de pergunta), esforço **Extra alto**, uso do plano no rodapé, retomar uma conversa antiga.

### Gemini CLI (Google)
- Antigo: desde novembro de 2026 o Google recusa contas Google pessoais neste cliente e manda migrar para o Antigravity. Só funciona com chave de API ou Vertex.
- Instalar: `npm install -g @google/gemini-cli`
- Login: rodar `gemini` uma vez no terminal e escolher entrar com o Google. Depois sair com `/quit`.
- O Nexora abre com `gemini --acp`. Versões antigas usavam `--experimental-acp`; se não abrir, atualize com o mesmo comando de instalação.

### Antigravity (Google)
- Precisa do CLI do Antigravity (`agy`), versão 1.5 ou mais nova. As versões 1.1.x não têm o `acp`: atualize com `agy update`.
- Instalar: pelo site antigravity.google (página de download do CLI).
- Login: rodar `agy` uma vez e entrar com a conta Google. No Nexora, se aparecer um aviso com um link, abra o link para autorizar.
- O Nexora abre com `agy acp`.

### GitHub Copilot
- Precisa de um plano do Copilot.
- Instalar: `npm install -g @github/copilot`
- Login: rodar `copilot` e usar `/login`.
- Atenção: o `copilot` que vem com o VS Code é só um atalho e o Nexora ignora. Precisa do CLI instalado pelo npm.

### Cursor
- Precisa de uma conta do Cursor.
- Instalar: seguir cursor.com/cli (instala o `cursor-agent`).
- Login: `cursor-agent login`
- O Nexora abre com `cursor-agent acp`.

### OpenCode
- Usa o provedor e a chave que você configurar (há modelos grátis).
- Instalar: `npm install -g opencode-ai`
- Login: `opencode auth login` e escolher o provedor.
- O Nexora abre com `opencode acp`.

### Kiro (AWS)
- Precisa de uma conta do Kiro.
- Instalar: seguir kiro.dev/cli (instala o `kiro-cli`).
- Login: `kiro-cli login`
- O Nexora abre com `kiro-cli acp --agent-engine=v3`.

### Qwen Code (Alibaba)
- Instalar: `npm install -g @qwen-code/qwen-code`
- Login: rodar `qwen` uma vez e entrar (conta Qwen ou chave de API).
- O Nexora abre com `qwen --acp`.

### Grok (xAI)
- Precisa de conta ou chave da xAI.
- Instalar: `npm install -g @xai-official/grok`
- Login: rodar `grok` uma vez, ou definir `XAI_API_KEY` nas variáveis de ambiente do Windows.
- O Nexora abre com `grok agent stdio`.

### goose (Block)
- Usa o provedor que você configurar.
- Instalar: seguir block.github.io/goose (instala o `goose`).
- Configurar: `goose configure` e escolher o provedor.
- O Nexora abre com `goose acp`.

## No PC do trabalho

O WatchGuard pode barrar programas novos. Se um agente instalar mas não abrir pelo Nexora, teste primeiro no terminal (`gemini --version`, por exemplo). Se nem no terminal abrir, é o antivírus.
