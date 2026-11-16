# Nexora: o que é e o que faz (versão 0.5.x, novembro de 2026)

## 1. Em uma frase
O Nexora é um app de desktop grátis (Windows e Mac) que junta num lugar só todas as sessões do **Claude Code**, o agente de programação da Anthropic que roda no terminal. Ele não substitui o editor de código: é o painel de controle dos agentes. Mostra quem está trabalhando, quem precisa de você e quem terminou. Também liga cada sessão a um projeto, a um worktree do git, a uma issue do GitHub e ao PR.

**Para quem é:** quem usa o Claude Code todo dia, com várias sessões ao mesmo tempo em vários repositórios, e se perde entre terminais e janelas do VS Code.

**O que precisa estar instalado:** Claude Code, git e, para as funções do GitHub, o GitHub CLI (`gh`) com login feito.

## 2. Ideias centrais
- **Sessão:** uma conversa com o Claude Code numa pasta. Cada sessão é um processo `claude` de verdade, e o Nexora mostra a conversa numa aba.
- **Projeto (ou ambiente):** uma pasta principal mais as pastas vinculadas, normalmente vindas de um `.code-workspace` do VS Code. O Nexora acha os projetos nas "pastas de projetos" escolhidas no primeiro uso.
- **Worktree / tarefa:** uma cópia de trabalho do repositório numa branch própria, para várias sessões mexerem no mesmo repositório sem conflito.
- **Estados de uma sessão:** **Nova**, **Trabalhando**, **Precisa de você** (pedido de permissão ou pergunta do Claude), **Pronta** e **Erro**.
- **Sessões de fora:** conversas iniciadas no VS Code ou no terminal também aparecem. Responder a uma delas pelo Nexora cria uma cópia com todo o histórico, e a original fica intacta.

## 3. A janela
- **Trilho lateral** (ícones à esquerda), de cima para baixo: **Ao vivo** (Ctrl+0), **Conversas** (Ctrl+B), **Projetos**, **Orquestrador** (Ctrl+Shift+O), **Recursos**, **Issues**, **Agendados** e **Uso**. Embaixo ficam o **Guia de primeiros passos** e os **Temas**.
- **Barra do topo:**
  - busca **Procurar** (Ctrl+K);
  - abas, uma por sessão ou página. Dá para arrastar, fechar com Ctrl+W ou com o clique do meio, e passar com Ctrl+Tab;
  - arrastar uma aba para fora abre uma janela separada;
  - **+** abre uma nova sessão (Ctrl+N);
  - medidores do plano do Claude (sessão de 5 h e semana);
  - botão **Atualizar para X.Y.Z** quando há versão nova.
- **Lista de Conversas** (lateral): **Fixadas**, **Em andamento** (agrupadas por projeto, com ponto de estado) e **Histórico** por projeto.
- No Mac, os atalhos com Ctrl usam ⌘.

## 4. Ao vivo (a tela inicial)
- **Kanban com quatro colunas:** **Precisa de você**, **Trabalhando**, **Prontas** e **Paradas**. Também há um modo **Lista**, um filtro por projeto e a opção **Por projeto** (uma faixa por projeto).
- **Cada cartão mostra:**
  - projeto, branch e o número da issue;
  - há quanto tempo está trabalhando ou esperando;
  - o uso de contexto em %;
  - quantos subagents estão rodando;
  - o PR, com os checks e a revisão.
- **Ações direto no cartão:**
  - **Permitir** e **Negar** permissões;
  - responder perguntas do Claude, com as opções em botões;
  - quando a sessão fica pronta: ver os arquivos alterados, **Ver diff**, **Commit e PR** e **Só o commit**.
- **Fila de permissões:** uma faixa no topo junta os pedidos de todas as sessões, com **Permitir**, **Em todas** (libera o mesmo pedido em todas as sessões até fechar o app) e **Negar**.
- **Janela rápida:** clicar no cartão abre uma prévia da conversa. As setas passam para o cartão vizinho, e **Abrir a conversa** leva a conversa para uma aba.
- **Notificações do sistema** avisam quando uma sessão fora da tela precisa de você, termina ou dá erro. No Windows, a notificação tem os botões Permitir e Negar.

## 5. A conversa (aba de sessão)
- **Cabeçalho:** pasta, branch, contexto usado e o tempo que resta do cache do prompt. Tem atalhos para:
  - **Arquivos do projeto** (Ctrl+Shift+E);
  - **Alterações do git** (Ctrl+Shift+G);
  - **Terminal** (Ctrl+`, só no Windows);
  - **Detalhes da sessão**;
  - **Executar o projeto**;
  - **Remote Control**, para continuar a conversa pelo celular ou pelo claude.ai.
- **Mensagens:**
  - Markdown formatado e links que abrem o arquivo na linha certa;
  - o "pensamento" do modelo pode ser expandido;
  - cada ferramenta usada aparece como um item que se abre, e as edições têm **Ver diff**;
  - cada subagent tem um cartão próprio (pedido, ferramentas e resultado).
- **Comentar o diff:** o "+" na margem de uma linha abre uma caixa de comentário (Shift+clique em outra linha pega o intervalo). Os comentários ficam pendentes ("3 comentários"), podem ser editados e apagados, e **Mandar para o agente** manda todos de uma vez, com arquivo, linhas e o trecho do código.
- **Comentários do PR no chat:** no painel do PR, marque os comentários e use **Mandar para o agente**. O destino é a **Conversa original** ou uma **Conversa nova com a passagem**, que começa retomando o handoff da branch no Senna; a nova é sugerida quando a original está parada, cheia ou já teve passagem. Conversas resolvidas viram avatares na margem da linha no diff do PR.
- **Permissões e perguntas dentro da conversa:**
  - **Permitir**, **Não perguntar de novo** e **Negar**;
  - para um plano: **Aprovar o plano** ou **Continuar planejando**;
  - **pedido de segredo**: o agente usa a ferramenta `pedir_segredo` e aparece um formulário com campo de senha, **Salvar** e **Recusar**. O valor vai para um `.env` ignorado pelo git ou para o cofre do sistema (variável de ambiente das sessões, do terminal e do Executar do projeto); o agente recebe só a confirmação. A lista fica em Configurações › Segredos.
- **Campo de mensagem:**
  - Enter envia e Shift+Enter quebra a linha;
  - **@** cita um arquivo do projeto e **/** lista os comandos do Claude Code;
  - anexos por botão, Ctrl+V ou arrastando;
  - **Ditar por voz** (Windows).
- **Escolhas da sessão:**
  - pílula de **modelo**, com o esforço (Baixo a Máximo), **Modo rápido** e **Ultracode**;
  - **modo de permissão** (Shift+Tab): Padrão, Perguntar sempre, Aceitar edições, Automático, Planejar e Sem perguntar.
- **Fila:** o que você manda enquanto o Claude trabalha fica **NA FILA** e sai quando ele termina. Dá para mandar agora, editar ou tirar da fila.
- **Aviso de compactar:** aparece quando o contexto passa de 80%.
- **No fim de uma sessão que editou arquivos:**
  - **Commit e PR em rascunho** ou **Só o commit**;
  - se a sessão veio de uma issue, o PR leva "Closes #N";
  - o status da issue no quadro anda sozinho: em andamento ao começar, revisão quando o PR abre.
- **Painel Detalhes da sessão** (à direita), com quatro abas:
  - **Sessão**: troca de branch, pastas vinculadas, MCPs, a divisão do contexto e os arquivos que a sessão editou;
  - **Arquivos**: árvore com criar, renomear e apagar (vai para a Lixeira);
  - **Git**: alterações e diffs;
  - **Busca**: em todos os arquivos, com opção de regex;
  - **Navegador** (ícone de globo, Windows): barra de endereço, voltar, avançar e recarregar, atalho para os servidores do painel Executar e a opção de abrir numa janela flutuante. Cada conversa lembra o endereço. Página que não abre mostra o motivo (servidor parado, endereço inexistente, certificado). No **Modo design**, o mouse contorna os elementos, o clique abre um comentário e cada comentário vira um marcador numerado; **Mandar para o agente** manda para a conversa o seletor, o texto, o HTML resumido, o tamanho e a posição de cada elemento, mais um print da página com os marcadores. Esc sai do modo. O agente também pode abrir uma página nele (ferramenta MCP `abrir_navegador`).
- **Editor embutido** (leve): abas de arquivos e diffs, realce de sintaxe, **Salvar** (Ctrl+S) e **Citar no chat** (Ctrl+L), que manda a seleção para o campo de mensagem.
- **Terminal integrado** (Windows): PowerShell embaixo da conversa.

## 6. Projetos
- **Página Projetos:** um cartão por projeto, com branch, sessões abertas e a última conversa. Botões **Sessão**, **Abrir a pasta**, **Abrir no VS Code**, **Excluir projeto…** e **Novo projeto**.
- **Página de um projeto:**
  - campo para começar uma sessão;
  - **Nova tarefa em worktree**: nome, branch base e o botão **Criar worktree e abrir sessão**;
  - **Começar por uma issue**;
  - o PR da branch, com **Encerrar tarefa** quando o PR já foi mergeado;
  - conversas recentes;
  - lista de worktrees, para abrir ou remover;
  - os CLAUDE.md do projeto, a troca de branch e as pastas vinculadas.
- **Executar:** acha os alvos do `launch.json`, do `launchSettings.json` e do `package.json`. Cada alvo tem Executar, Reiniciar e Parar, porta, abrir no navegador e a saída ao vivo.
- **Arrastar pastas:** soltar uma ou mais pastas na lateral ou na página Projetos adiciona cada uma como projeto. Pasta que já é projeto só abre ou fica destacada; arquivos são ignorados com um aviso curto.
- **Excluir projeto:** escolhe se apaga o `.code-workspace`, a pasta (e a branch, se for worktree) e as conversas. Tudo vai para a Lixeira.

## 7. Issues (GitHub)
- **Fonte:** um quadro do **GitHub Projects**, com as mesmas visões do quadro, ou a **Lista simples** (issues abertas por repositório).
- **Modos Kanban e Lista.** No Kanban, arrastar um cartão para outra coluna muda o status no GitHub.
- **Filtros:** campo **Buscar nas issues** e o interruptor **Minhas e sem responsável**.
- **Colunas:** responsável, etiquetas, prioridade, tamanho, PRs, iteração e data de atualização.
- **Abrir uma issue** mostra:
  - descrição e comentários, com a opção de comentar sem sair do app;
  - troca de status;
  - **Worktree novo** (a partir de uma branch base);
  - **Nova sessão**: já traz um rascunho com o link e o texto da issue em tópicos, sem enviar;
  - **Abrir no GitHub** e **Copiar link**.
- **Duas contas do GitHub ao mesmo tempo** (por exemplo, a da empresa e a pessoal): cada repositório usa a conta que tem acesso.
- **Ao abrir, a página mostra na hora o último estado salvo** e atualiza por trás.
- Se faltar a permissão de projetos do GitHub, aparece **Dar permissão**.

## 8. Orquestrador
- Para uma tarefa grande demais para uma sessão só. Você conversa com um agente "chefe", que divide o trabalho e abre um agente por parte, cada um no seu worktree.
- **Canvas** com o chefe e os agentes ligados por linhas:
  - dá para arrastar os nós e agrupá-los;
  - cada nó aceita mensagem direta e mostra os pedidos de permissão.
- O chefe usa ferramentas do Nexora (criar agente, mandar mensagem, ler e esperar sessões). Ler e esperar não pedem confirmação; as outras pedem, a menos que você permita para a sessão toda.
- Dá para encerrar o chefe sem fechar os agentes e retomar um orquestrador anterior.
- O orquestrador tem um avatar de polvo (no topo da tela, no nó do chefe e no cartão do Ao vivo), que só se mexe enquanto ele trabalha e respeita a opção de reduzir movimento.

## 9. Agendados
- Pedidos que rodam sozinhos: nome, projeto, hora, dias da semana e o texto do pedido.
- Cada item tem **Rodar agora**, um interruptor para ligar e desligar, e mostra a próxima execução e a última.
- Cada execução abre uma sessão nova, que aparece no quadro.
- Só roda com o Nexora aberto. Se ele estava fechado na hora, roda ao abrir, no mesmo dia.

## 10. Uso
- Quanto do plano do Claude já foi usado na sessão de 5 h e na semana, e quando cada um reinicia. A leitura não gasta tokens.
- A parte de cada projeto no uso da semana, os modelos usados e o histórico das últimas 8 semanas.
- Lê também o uso do Codex (`~/.codex/sessions`) e do Gemini CLI (`~/.gemini/tmp`). Filtros por agente e por modelo mudam os totais e os gráficos; cada filtro só aparece quando há mais de uma opção.

## 11. Recursos
- **Desempenho:** memória e processador do Nexora, das sessões e dos MCPs, os momentos mais pesados e gráficos de 14 dias. Os dados ficam só no computador.
- **MCPs compartilhados:** um "hub" roda cada servidor MCP do projeto uma vez só para todas as sessões, liga na primeira chamada e desliga depois de 10 min parado.
- **Processos de build do .NET:** o botão **Liberar** encerra os processos de build parados (Windows).

## 12. Aparência, idioma e primeiro uso
- **Temas:**
  - fixos: Padrão, Padrão claro, Dracula e Alucard;
  - animados: Grafo, Constelação, Via Láctea, Nebulosa, Aurora e outros, que reagem ao mouse e se agitam quando o Claude trabalha;
  - dá para criar, editar (cores, fontes, imagem de fundo), duplicar, importar e exportar.
- **Fundo:** Vidro com animação, Vidro sem animação ou **Sólido e leve**. O vidro real precisa de Windows 11 ou Mac. No Windows, trocar entre sólido e vidro pede para reiniciar o app; as abas voltam sozinhas.
- **Idioma:** Português ou English.
- **Guia de primeiros passos:**
  - abre sozinho na primeira vez;
  - confere e instala o que falta (no Windows, pelo winget);
  - ajuda a entrar no Claude e configurar as contas do `gh`;
  - pede as pastas de projetos e o visual.
- **Atualizações:** o app procura versão nova sozinho. O botão do topo baixa, fecha, instala e reabre; se houver sessão trabalhando, pede confirmação antes.

## 12b. Máquinas na nuvem
- **Para quê:** o agente roda numa máquina sua com SSH (uma VPS, a Oracle grátis, outro PC ou um Mac) em vez de gastar o processador e a memória deste PC. Este PC só controla, e o agente continua lá mesmo com ele desligado.
- **Como conectar** (Configurações › Máquinas na nuvem › **Conectar minha máquina (SSH)…**):
  1. Preencha o endereço (host), o usuário e a porta.
  2. O Nexora cria uma chave SSH própria. Copie a linha pública e cole no `~/.ssh/authorized_keys` desse usuário na máquina.
  3. Rode `claude setup-token` neste PC e cole o token em **Token do Claude**. Ele fica no cofre deste PC e só vai para a máquina como variável de ambiente do processo do Claude.
  4. Clique em **Testar conexão e salvar**. A máquina precisa de `git` e `claude`; o teste mostra também se há `node` e `codex`.
- **InstaBox** (Configurações › Máquinas na nuvem › InstaBox):
  1. Crie uma chave de API no painel do InstaBox com `machines:read`, `machines:write`, `machines:delete` e `images:read`, e cole no campo do InstaBox. Ela fica no cofre deste PC.
  2. **Criar máquina no InstaBox…**: escolha o nome e o modelo (o plano grátis só cria de alguns, como `agent-browser` e `ci`) e clique em **Criar e conectar**. O Nexora cria a máquina, entra pelo SSH e instala o Claude Code se faltar.
  3. O SSH usa um certificado de uma hora que o Nexora pede ao InstaBox com a chave pública dele; não há nada para colar na máquina.
  4. **Pausar** guarda a memória e só cobra o disco. Uma conversa na nuvem liga a máquina sozinha; **Ligar** liga na mão. A lixeira apaga a máquina no InstaBox, com os arquivos dela.
  - O InstaBox não diz quem opera o serviço nem publica termos de uso. No plano grátis, cada execução vai até 2 h e a máquina pausa depois de 30 min parada.
- **Freestyle** (Configurações › Máquinas na nuvem › Freestyle): cole a chave de API (sai em dash.freestyle.sh) e use **Criar máquina na Freestyle…**. O Nexora cria a VM (Ubuntu com git, node e Claude Code) com saída liberada para a internet, entra pelo SSH da Freestyle (`beta-ssh.freestyle.sh`) com um token de uma hora como senha, entregue ao `ssh` pelo próprio Nexora (`SSH_ASKPASS`), sem gravar em disco, e pausa e liga pela API. O plano grátis tem limite de horas por mês e pede cartão. Ainda não foi testado com uma conta de verdade.
- **Usar:** na página do projeto, ligue **Pode ir para a nuvem** (desligado por padrão). Na conversa nova, troque o chip **Neste PC** ao lado do agente pela máquina. A conversa ganha o selo de nuvem na aba e no Ao vivo.
- **Executar na nuvem:** na página Executar, o seletor ao lado de **Atualizar** troca **Neste PC** pela máquina (só em projetos com **Pode ir para a nuvem**). O Nexora roda o alvo na pasta da branch na máquina: se já existe código lá (de uma conversa na nuvem), usa ele; se não, envia o deste PC. Quando o servidor imprime o endereço, a porta chega a este PC por um túnel SSH e abre no `localhost` (**Abrir no Nexora** usa o navegador embutido). Parar encerra o grupo de processos inteiro na máquina. No InstaBox, a máquina fica acordada enquanto o alvo roda.
- **Sugestão:** numa conversa nova de um projeto liberado, se o PC passar de 85% de memória em uso, uma faixa oferece **Rodar em <máquina>**. O X mantém neste PC.
- **Queda de conexão:** a faixa avisa e o Nexora tenta de novo sozinho; **Reconectar agora** força. Nada se perde, porque o agente grava tudo num log na máquina.
- **Trazer para cá:** traz a branch e as mudanças da máquina para um worktree `<branch>-nuvem` neste PC e abre uma conversa lá para o commit e o PR.
- **Limites:** "Minha máquina (SSH)", InstaBox e Freestyle. O histórico ao reabrir só volta em conversas do Claude.

## 12c. Comando nexora no terminal
- **Instalar:** Configurações › Avançado › **Comando nexora no terminal**. No Windows cria `%USERPROFILE%\.local\bin\nexora.cmd` (roda o `Nexora.dll` pelo `dotnet`) e põe a pasta no PATH do usuário; no Mac cria `~/.local/bin/nexora` e a linha do PATH no `~/.zprofile`. **Remover** apaga o arquivo.
- **Como fala com o app:** por um canal local (named pipe) que só o mesmo usuário abre. Sem o Nexora aberto, o comando avisa e sai com código 3.
- **Comandos:** `nexora status`, `nexora abrir [pasta]`, `nexora sessoes [--projeto P]`, `nexora iniciar <projeto> <mensagem|-> [--agente] [--modelo] [--espere]`, `nexora enviar <sessao> <mensagem|-> [--espere]`, `nexora ver <sessao> [--mensagens N]`, `nexora esperar <sessao>`, `nexora ao-vivo`, `nexora parar <sessao>` e `nexora ajuda`. `--json` em todos.
- **Projeto:** `.` é a pasta atual, um caminho ou o nome do projeto. `-` no lugar da mensagem lê do pipe.
- **Saída:** 0 ok, 1 erro, 2 precisa de você (permissão ou pergunta), 3 Nexora fechado, 64 uso errado. `--espere` imprime a resposta final do agente.
- Aprovar permissões continua só pela janela.

## 13. Limites e coisas a saber
- Não é uma IDE completa: o editor é leve. A ideia é revisar e citar trechos, e o código pesado continua no VS Code, que abre com um clique.
- Depende do Claude Code instalado e logado. As funções do GitHub dependem do `gh`.
- **Só no Windows:** terminal integrado, navegador embutido com modo design, ditado, notificações com botões e o Liberar do .NET. No Mac, o navegador abre a página no navegador do sistema.
- Os agendados só rodam com o app aberto.
- Não há uma tela única de "Configurações": as opções ficam em Temas, no guia de primeiros passos e em cada tela.
