# Nexora — plano

IDE desktop pessoal em volta dos chats do Claude Code. O centro são as sessões trabalhando. Arquivos, editor e git ficam à mão para quando o Vini quiser mexer. O objetivo é largar o VS Code no dia a dia e deixá-lo só para depurar com breakpoint.

Referências: o app do vídeo `Desktop\snapinsta-1793313319515.mp4` (abas, chat, árvore de arquivos, orquestrador em canvas, worktree por agente) e o anywh.sh.

## Decisões

| Assunto | Decisão | Por quê |
|---|---|---|
| Stack | C# .NET 10 + Avalonia 12 | Desktop nativo, sem página web nem WebView. O editor nativo (AvaloniaEdit + TextMate) usa as mesmas gramáticas e temas do VS Code, e o mesmo código roda no Mac depois. |
| Como roda | `dotnet D:\NexoraApp\Nexora.dll` (`UseAppHost=false`) | O WatchGuard do PC do trabalho barra exe novo; o `dotnet.exe` assinado passa, como na Toca. |
| Claude | Um processo `claude` instalado por sessão, em `-p --input-format stream-json --output-format stream-json` | É o que a extensão do VS Code faz. O app nunca lê nem guarda credencial: cada sessão usa o binário oficial sem modificação, com o login dele. |
| Aparência | Minimalista; escuro = Dracula, claro = Alucard (o claro oficial do Dracula) | Pedido do Vini: nada de preto puro. |
| Nome | Nexora, sem nada do Racco/Banditboard/Toca | Pedido do Vini. |
| Linguagens | Qualquer uma (C#, Java, Kotlin, Python, TS…) | O Claude não depende disso, e o editor da fase 3 colore pelas gramáticas TextMate. |
| Nova sessão | Abre na hora uma "Nova conversa" em branco, com o ambiente escolhido dentro dela (ou "Outra pasta") | Pedido do Vini, como a tela nova do Claude Code no VS Code; nada de seletor do Explorer primeiro. |
| Ambiente | Cada `.code-workspace` em `D:\Repositorios` (raiz e primeiro nível): o 1º projeto é o principal, os outros são vinculados e entram na sessão como `--add-dir` | É o jeito que ele já trabalha no VS Code. |
| Nova tarefa | Branch `nome-AAAAMMDD` a partir de `origin/<padrão>` (`--no-track`), worktree irmão `<Repo>-<nome>` e `.code-workspace` com os mesmos vinculados | Padrão das branches e worktrees dele; irmão mantém os caminhos relativos `../` dos projetos. |
| Alvo do app | `net10.0-windows10.0.19041.0` (o núcleo segue `net10.0`) | Avisos nativos do Windows; para o Mac, multi-target depois. |
| Contexto e cache | Lidos do próprio fluxo: `usage` de cada resposta (entrada + cache lido + cache criado + saída) e `modelUsage[].contextWindow` do fim do turno; cache de 1 h ou 5 min pelo `cache_creation` | Nada de chamada extra ao Claude. |
| Visual | Skill ui-ux-pro-max (instalada em `~/.claude/skills`, revisada no commit 28b3b8d) como checklist; ícones Lucide desenhados por um controle próprio, barra de título integrada, transições de 120 ms, contraste ≥ 4,5:1 | Ela serve como checklist e referência de Avalonia 12; a paleta e os padrões de página dela são de site e foram ignorados. |

## Protocolo do `claude` (aprendido em 29/10/2026, Claude Code 2.1.282)

Argumentos: `-p --input-format stream-json --output-format stream-json --verbose --include-partial-messages --permission-prompt-tool stdio`, mais `--resume <id>`, `--model`, `--permission-mode` quando for o caso. `--allow-dangerously-skip-permissions` só entra quando o modo escolhido é "Sem perguntar".

Entrada (uma linha JSON por mensagem):

- mensagem: `{"type":"user","message":{"role":"user","content":[{"type":"text","text":"…"}]},"parent_tool_use_id":null,"session_id":""}`
- controle: `{"type":"control_request","request_id":"…","request":{"subtype":"initialize" | "interrupt" | "set_permission_mode","mode":… | "set_model","model":…}}`
- resposta de permissão: `{"type":"control_response","response":{"subtype":"success","request_id":"<id do pedido>","response":{"behavior":"allow","updatedInput":{…},"updatedPermissions":[…]}}}` ou `{"behavior":"deny","message":"…"}`

Saída:

- `system/init` traz `session_id`, `model`, `cwd`, `permissionMode`; `system/hook_*`, `system/status` e `system/thinking_tokens` vêm antes e durante.
- `stream_event` com `content_block_delta`/`text_delta` é o texto parcial; o pensamento chega vazio.
- `assistant` vem um por bloco (texto ou `tool_use`), com o mesmo `message.id`.
- `user` traz os `tool_result`.
- `control_request` com `subtype: can_use_tool` é o pedido de permissão (`tool_name`, `input`, `description`, `decision_reason`, `permission_suggestions`). `AskUserQuestion` e `ExitPlanMode` também chegam por aqui.
- `result` fecha o turno (`duration_ms`, `total_cost_usd`, `is_error`).
- `rate_limit_event` traz o uso do plano (`unifiedWindows.five_hour/seven_day.utilization`).
- Mensagens de subagente têm `parent_tool_use_id` preenchido.

Histórico: `~/.claude/projects/<pasta>/<sessão>.jsonl`, com `cwd`, `gitBranch`, `ai-title`, `custom-title`, `last-prompt`, mensagens `user`/`assistant` e `isSidechain` para subagente.

## Fases

### 1. Base ✅ (29/10/2026)

- [x] Sessão com o `claude` instalado: streaming, ferramentas, permissões, pergunta do Claude, aprovação de plano, parar, modo e modelo.
- [x] Conversas herdadas do Claude Code: lista com título, pasta, branch e tempo, busca, histórico carregado e continuação com `--resume`.
- [x] Markdown nativo (títulos, listas, código, tabela, citação, código inline).
- [x] Temas Dracula/Alucard com troca na hora; skeleton no carregamento.
- [x] Uso do plano no rodapé, lido da própria saída do Claude Code.
- [x] Processos do `claude` num Job Object: fecham junto com o Nexora, até se ele travar.

Pendências desta fase:

- Resposta de `AskUserQuestion` (`updatedInput.answers`) e "Não perguntar de novo" (`updatedPermissions`) ainda não foram testadas com um pedido real.
- Retomar uma conversa que está aberta no VS Code cria duas sessões no mesmo histórico (avisar na fase 4).
- Colar imagem e anexar arquivo no chat.

### 2. Ambientes e git ✅ (29/10/2026)

- [x] Ambientes lidos dos `.code-workspace` (JSON com comentário e vírgula sobrando), na lateral com a branch de cada um.
- [x] Página do ambiente: branch atual, arquivos alterados, troca de branch (travada se uma sessão trabalha na pasta), worktrees do repositório, projetos vinculados com a branch de cada um, abrir pasta e abrir no VS Code.
- [x] Nova tarefa: cria branch + worktree + workspace, registra o ambiente e abre a sessão (com a descrição como primeira mensagem, se houver).
- [x] Nova sessão sem Explorer: "Nova conversa" com ambiente e "Outra pasta"; vinculados entram como `--add-dir`.
- [x] Acabamento: ícones vetoriais, barra de título integrada, botões redondos de enviar/parar, transições, contraste do texto fraco.
- [x] Testes com git de verdade num repositório temporário (worktree, troca de branch, alterações).

Pendências desta fase:

- Ícone do app: ideias em [ideias-icone.png](ideias-icone.png), esperando a escolha do Vini.
- Raízes dos ambientes só pelo `config.json` (`raizes`); falta tela para adicionar pasta ou workspace avulso.
- Remover worktree pelo app (hoje só cria).
- Trocar branch dos projetos vinculados (hoje só do principal).

### 3. Arquivos ✅ (29/10/2026)

- [x] Painel à direita (Ctrl+B) com a árvore do ambiente (principal e vinculados, pasta por pasta, sem bin/obj/node_modules/.git) e a aba Alterações com o `git status` de todos os projetos.
- [x] Editor AvaloniaEdit + TextMate (Dracula no escuro, QuietLight no claro) em abas, ao lado do chat; Ctrl+S salva; aviso quando o arquivo muda no disco com alteração não salva; arquivo binário ou grande não abre.
- [x] Salvar mantém o encoding original (UTF-8 com ou sem BOM, Windows-1252) e as quebras de linha.
- [x] Diff colorido: do git (clicando numa alteração) e das ferramentas Edit/Write/MultiEdit do Claude ("Ver diff" na linha da ferramenta).
- [x] `@` no campo de mensagem procura arquivos do ambiente; "Citar no chat" (Ctrl+L) manda a seleção com caminho e linhas.

Pendências desta fase:

- IntelliSense e "ir para definição" (LSP do Roslyn).
- Busca de texto em todos os arquivos (Ctrl+Shift+F).
- Criar, renomear e apagar arquivos pela árvore.

### 4. Várias sessões ✅ (29/10/2026)

- [x] Visão geral (Ctrl+0) em grade de widgets, ao vivo: o que cada sessão está fazendo, há quanto tempo, modelo, contexto usado/janela com barra (laranja acima de 80%), quanto falta para o cache expirar (laranja abaixo de 10 min, vermelho expirado), custo, e ações no card (Permitir/Negar, Parar, Abrir).
- [x] Contexto e cache também no cabeçalho de cada chat; conversas herdadas já abrem com o último contexto e o horário do cache.
- [x] Contadores na lateral (trabalhando, precisam de você) e Ctrl+Tab / Ctrl+Shift+Tab para alternar as sessões.
- [x] Sessões fora do Nexora (VS Code, terminal) lidas de `~/.claude/sessions`, com o título da conversa; o Nexora recusa continuar uma conversa que está aberta em outro lugar.
- [x] (30/10) As sessões de fora viram cards na mesma grade, com etiqueta da origem e o andamento lido dos últimos 256 KB do histórico (última atividade, branch, modelo, contexto, cache); "esperando" vem do `status: waiting` que o Claude Code grava. Nexora ou fora, a ordem é a mesma: quem precisa de você, trabalhando, paradas.
- [x] Aviso do Windows e barra de tarefas piscando quando uma sessão pede permissão, termina ou dá erro e a janela não está na frente.

Pendências desta fase:

- Clicar no aviso do Windows ainda não traz o Nexora para a frente (falta o ativador COM).
- O aviso do Windows não foi visto funcionando: o Vini confirma no primeiro uso.

### Extras antes da fase 5 ✅ (29/10/2026)

- [x] `@` usa `git ls-files` (o Acme.Shop.App com 5 vinculados lê ~9 mil arquivos em ~1,4 s; a varredura de pasta passava de 5 min) e mostra só a melhor faixa: `@registros.razor` traz `Registros.razor` e `Registros.razor.cs`.
- [x] Ambientes editáveis: adicionar e tirar pastas vinculadas (regrava o `.code-workspace` mantendo settings e o resto, com tab), criar ambiente novo (gera `<pasta principal>\<nome>.code-workspace`), tirar da lista e mostrar de novo. Workspaces fora de `D:\Repositorios` ficam lembrados no `config.json`.
- [x] Temas: galeria com 8 prontos (Suiciniv e Suiciniv Papel com as cores do portfólio, Dracula, Alucard, Argila, Oceano, Floresta, Neon), criar a partir do atual, editar com seletor de cor e hex, fontes da interface e do código, cores do editor, imagem de fundo com escurecimento, importar e exportar `.json` (`%APPDATA%\Nexora\temas`). Edição aparece ao vivo no app.
- [x] Fundos em ASCII vivo, desenhados em tempo real: estrelas, chuva, código caindo, ondas, aurora, fogueira e vaga-lumes; reagem ao mouse e aceleram quando alguma sessão está trabalhando.
- [x] Nova conversa no estilo do VS Code: "✻ Claude Code" em texto, ícone do Nexora, dicas de teclado, Shift+Tab troca o modo (padrão → aceitar edições → planejar) e cards das conversas recentes do ambiente.

### Temas animados ✅ (29/10/2026)

- [x] Tema **Padrão** com as cores do portfólio (era "Suiciniv"), fundo sólido; **Padrão claro** (era "Suiciniv Papel"). Argila, Oceano, Floresta e Neon e os fundos ASCII feitos à mão saíram.
- [x] 15 temas animados: Claude Code, Cloud Drift, Honu Lagoon, Koi Pond, Jellyfish, Paper Boats, Fireflies, Moonlit, Lareira, Chuva Lo-fi, Galáxia Distante, Jardim Sakura, Aurora, Chuva de Código e Arcade. Os `themes*.js` originais rodam sem alteração no V8 (Microsoft.ClearScript) por uma ponte que imita o canvas 2D e grava as operações; o C# desenha com Skia e faz a parte do `engine.js` que pesa (amostragem da cena, ladrilhos com glifo, lanterna do mouse, rastro, ondas). Reagem ao mouse e ao clique, com onda ao mexer rápido.
- [x] 12 peças do ASCII Magic: Starfield Warp, Spiral Galaxy, Nebula Drift, Northern Lights, Neon City Rain, Retro Synth Sunset, Moonlit Sea, Rainy Window, Snowfall Valley, Underwater Reef, Flow Field e Game of Life, cobrindo a janela sobre `#0d0d0f`.
- [x] Cada fundo vira um tema com interface de vidro (cores com transparência) e véu para escurecer o fundo. Texto do Claude em bolha (com ✻, largura do conteúdo) em todos os temas, a pedido do Vini. Galeria em grupos (Nexora, Temas animados, ASCII Magic, seus temas) com miniatura de cada fundo, sem créditos na interface (pedido dele).
- [x] Fundo **animado**, **parado** (o wallpaper congelado, sem gastar CPU) ou **sólido** (sem wallpaper), escolhido na galeria e guardado no `config.json`.
- [x] Quando alguma sessão trabalha, o fundo reage: ondas nos temas animados, peça mais rápida no ASCII Magic.
- Os arquivos `LICENSE` (MIT) ficam junto dos originais em `src/Nexora/Fundos/*/`: a MIT pede que o aviso acompanhe as cópias.
- Desempenho medido (1280x800, rasterizando em CPU no teste): 7 a 45 ms por quadro; no app o quadro vira `SKPicture` e a GPU desenha. 30 fps com a janela em foco, 12 fora dela, parado quando minimizado. O Jint (interpretador puro) foi testado antes e ficava em 100 a 400 ms nas peças do ASCII Magic.

### Temas animados próprios ✅ (06/11/2026)

- [x] Os temas do Claudinho e as peças do ASCII Magic saíram, junto com o V8 (ClearScript) e os `.js`. No lugar, 8 temas desenhados em C# com Skia, no clima das estrelas que piscam da tela de boas-vindas: Grafo, Grafo claro, Constelação, Carta celeste, Via Láctea, Nebulosa, Pulso e Aurora (`src/Nexora/Fundos/`, base em `MotorNexora.cs`). Dois são claros. Reagem ao mouse e ao clique e se agitam quando o Claude trabalha.
- [x] Temas do usuário e `--tema` com os ids antigos caem no novo mais parecido (`Papeis.Migrar` e `Papeis.TemaAntigo`).
- [x] O fundo base (degradê, brilhos e vinheta) é gravado numa imagem pequena e redesenhado de tempos em tempos; por quadro só vão as estrelas e as linhas, agrupadas por nível de opacidade em poucas chamadas de desenho. Medido no app em 1440x900: 14% (Grafo) a 23% (Aurora) de um núcleo, contra 6% do tema sem animação.
- [x] Cabeçalho da página Temas quebra em duas linhas em janelas estreitas (1000 px) em vez de se sobrepor.

Pendências:

- O mascote pixel do Claude Code (Clawd) não foi copiado: é arte da Anthropic. O tema animado "Claude Code" desenha a estrela do Claude; antes de tornar o Nexora público, rever esse tema.
- Música e sons por tema ficaram de fora, assim como a transição animada entre fundos.

### 5. Orquestrador ✅ (30/10/2026)

- [x] Servidor MCP do próprio app (`Nexora.Core/Orquestra`): HTTP em `localhost` numa porta livre (HttpListener, sem admin), JSON-RPC à mão (`initialize`, `ping`, `tools/list`, `tools/call`), uma chave aleatória por orquestrador no `Authorization: Bearer`. O `claude` recebe o servidor por `--mcp-config` (JSON em linha), as instruções por `--append-system-prompt` e as ferramentas de leitura liberadas por `--allowedTools`.
- [x] Ferramentas: `listar_ambientes`, `listar_sessoes`, `criar_agente` (com `tarefa`, cria branch `<tarefa>-AAAAMMDD` + worktree + workspace pelo mesmo caminho da tela do ambiente; `grupo`, `titulo`, `modelo`), `enviar_mensagem`, `ler_sessao`, `esperar` (até 240 s, abaixo do limite de 5 min do fetch do Node), `responder_permissao` (pergunta do agente: negar com a resposta em `motivo`) e `parar_sessao`. Ler e esperar não pedem confirmação; o resto pede, a menos que o Vini permita para a sessão.
- [x] Tela Orquestrador (Ctrl+Shift+O): escolhe o ambiente, inicia a sessão chefe e mostra o canvas com o orquestrador, os agentes ligados por curvas (cor do estado), grupos e a conversa do chefe ao lado (dá para esconder). Arrastar move nós e grupos; soltar um agente dentro de um grupo junta, fora tira. No nó: Permitir/Negar, mandar mensagem direto ao agente, abrir conversa, novo grupo, remover. Zoom por Ctrl+roda ou − / +.
- [x] Ferramentas MCP aparecem no chat como "Nexora · criar agente", com resumo próprio.
- [x] Testado de ponta a ponta com o `claude` real (haiku, repositório temporário, `--isolada`): o chefe criou o agente num worktree, esperou, respondeu a permissão de escrita, leu a sessão e encerrou (20 s, ~US$ 0,06). Flags de teste: `--chefe <pasta> --orquestrar "<texto>" --modelo N --modo N`; `--demo --orquestra` mostra um canvas de exemplo.

Pendências desta fase:

- O canvas não é salvo: fechar o Nexora ou o orquestrador perde posições e grupos (as sessões dos agentes continuam).
- Um orquestrador por vez.
- O vídeo de referência sumiu da Área de Trabalho e o anywh.sh não tem orquestrador; o desenho do canvas saiu do plano, sem comparar com a referência.
- Remover worktree pelo app continua pendente (o "Remover" do nó só fecha a sessão).

### Visão geral: responder no card e recentes primeiro ✅ (30/10/2026)

- [x] O botão de conversa no card abre o chat ali mesmo, em duas colunas: os últimos 14 itens (mensagens, respostas, ferramentas, permissões com Permitir/Negar) e um campo de resposta (Enter envia; é o mesmo rascunho da aba do chat).
- [x] Sessão de fora (VS Code, terminal) abre lida do fim do histórico; a leitura cresce de 256 KB até 16 MB quando o fim é só resultado grande de ferramenta. A caixa de resposta dela continua a conversa no Nexora com `--resume <id> --fork-session`: uma cópia com todo o histórico e outro id, para não haver dois processos escrevendo no mesmo arquivo; a aba do VS Code fica como estava. O card do Nexora abre no lugar para seguir respondendo. A aba "Conversa trazida do histórico" faz o mesmo quando a conversa ainda está aberta no VS Code (antes recusava). Testado com o claude real (haiku): a cópia ganha outro id e lembra do que veio antes.
- [x] Ordem pelas conversas mais recentes (envio, fim de turno ou pedido de permissão; nas de fora, início do turno ou última atividade), com quem precisa de você no topo. Enquanto algum card está aberto a grade não muda de ordem, para não tirar o card de onde você está digitando.
- Flags de teste: `--expandir` (primeiro chat com conversa) e `--expandir-fora`.

### Abas e várias janelas ✅ (30/10/2026)

- [x] Tudo abre em aba na barra de título (Visão geral, Orquestrador, chats, ambientes, temas), com o ponto de estado nas abas de chat. Fechar a aba não encerra a sessão: quem encerra é o × da lista Abertas.
- [x] Arrastar uma aba para fora da faixa abre uma janela nova no lugar onde ela foi solta; soltar sobre a faixa de outra janela muda a aba de janela. Também pelo botão direito ("Abrir em nova janela", "Voltar para a janela principal"). Na faixa, arrastar reordena; o botão do meio fecha.
- [x] Janelas extras sem a lateral, com o mesmo fundo e tema; a última aba fechada fecha a janela. Ctrl+Tab / Ctrl+Shift+Tab alternam as abas da janela, Ctrl+W fecha, Ctrl+N abre sessão nela. O que for aberto (pela visão geral, pelo canvas) entra na janela em que se está.
- [x] Posição, tamanho e monitor de cada janela, mais as abas fixas (Visão geral, Orquestrador, Temas), ficam no `config.json` e voltam ao abrir o Nexora. Janela salva num monitor que não existe mais abre centralizada.
- Flag de teste: `--destacar` manda a aba atual para uma janela nova; `--foto` salva também as outras janelas (`-2.png`, `-3.png`…).

### 6. Painel Executar ✅ (30/10/2026)

- [x] Aba "Executar · <ambiente>" (botão na página do ambiente e ▷ no cabeçalho do chat) com o que dá para rodar no ambiente e nos vinculados: configurações `coreclr` do `.vscode/launch.json` (nome, cwd, `launchSettingsProfile`, env, `serverReadyAction.uriFormat`), perfis `Project` dos `Properties/launchSettings.json` que o launch.json não cobre, e scripts do `package.json`.
- [x] Porta por worktree: vem do `App:SelfUrl` (ou `Urls`) do `appsettings.Development.json` local do projeto, que é como o Vini já separa os worktrees (Cloud 44315, Cloud-melhorias 44320); dá para trocar no painel e fica lembrada no `config.json`. Porta diferente da do launchSettings vira `--urls` com o mesmo host; scripts npm recebem `PORT`. Porta já ocupada avisa em vez de subir.
- [x] Saída ao vivo no editor do Nexora (até 5 mil linhas, sem cores ANSI), "no ar" quando aparece `Now listening on`, link e "Abrir no navegador" pelo formato do launch.json, Parar, Reiniciar e Limpar. Os processos ficam no Job Object: fecham junto com o Nexora.
- [x] Testado com um projeto web temporário: subiu na porta do worktree pelo `--urls` e o painel achou o endereço. No Acme.Shop-melhorias a descoberta mostra "1. Acme (Backend/API)" na 44320 (o app da Acme não foi executado no teste).
- Flags de teste: `--executar <pasta>` e `--rodar N`.

Pendências desta fase:

- Worktree novo não tem os arquivos fora do git (`appsettings.Development.json` e afins): falta copiar do principal com a porta seguinte ao criar o worktree.
- Configurações de depuração (`blazorwasm`, `compounds`) não rodam pelo painel: continuam no VS Code.

### Comandos de barra e Remote Control ✅ (30/10/2026)

- [x] `/` no começo da mensagem abre a lista dos comandos que o `claude` aceita nesse modo, com dica de argumento e descrição: skills do usuário e de plugins, comandos do projeto e os do Claude Code que funcionam no `-p`. A lista vem da resposta do `initialize` (campo `commands`): da sessão aberta, ou de um `claude` aberto só para isso naquela pasta, sem mensagem e sem gastar token, guardada até o Nexora fechar. O `/remote-control` não está nela: no modo não interativo o Claude Code responde que não está disponível.
- [x] Saída de comando local (`/context`, `/usage`…) aparece no chat: quando o turno termina sem texto do Claude, o `result` vira a resposta.
- [x] Remote Control pelo botão 📱 do cabeçalho do chat: pedido de controle `remote_control` (`enabled`) na sessão aberta, que devolve `session_url`; o Nexora mostra o link e desliga junto quando a sessão fecha. Testado ligando e desligando numa sessão sem mensagem.
- Processos do `claude` abertos pelo Nexora ficam fora da lista de sessões de fora da visão geral.
- Não verificado: se o que é digitado no celular aparece ao vivo como mensagem no chat do Nexora (as respostas do Claude aparecem).

### 7. Worktrees de ponta a ponta ✅ (30/10/2026)

- [x] Worktree novo (tela ou orquestrador) herda do principal os arquivos fora do git: `appsettings.Development.json`, `appsettings.*.local.json`, `*.local.json` (inclui o `.claude/settings.local.json`) e `.env*`, sem `bin/obj`. A porta do `App:SelfUrl` vira a próxima livre de 5 em 5, olhando os outros worktrees e as portas em uso (Cloud 44315, -melhorias 44320, o próximo 44325), em todas as URLs do arquivo; o BOM é mantido. No Acme.Shop real seriam copiados o `.claude/settings.local.json` e o `appsettings.Development.json` do Blazor.
- [x] Encerrar a tarefa: a lixeira na lista de worktrees (e "Encerrar tarefa" quando o PR foi mergeado) pede confirmação, recusa se houver sessão aberta ou execução no painel naquela pasta e se houver alteração sem commit, apaga o `.code-workspace` que o Nexora criou, roda `git worktree remove` e só apaga a branch com `git branch -d` (branch não mergeada fica, com aviso; PR por squash precisa do `-D` manual). O ambiente sai da lista, as abas dele fecham e as portas lembradas são esquecidas.
- [x] PR da branch no topo da página do ambiente, pelo `gh pr view`: número, título, estado (rascunho, aberto, mergeado, fechado), revisão e checks, com "Abrir no GitHub".

### 8. Chat no nível do VS Code ✅ (30/10/2026)

- [x] Imagem no chat: Ctrl+V cola print ou arquivos copiados, arrastar para a caixa de mensagem, ou o clipe. Vai como bloco `image` base64 antes do texto, reduzida para no máximo 1800 px e 3,5 MB (PNG, ou JPEG se passar). Arquivo que não é imagem entra como `@caminho`. Miniaturas na bolha; no histórico, a contagem de imagens. Testado com o claude real (haiku disse a cor de um quadrado).
- [x] Clicar no aviso do Windows abre a sessão que pediu você e traz o Nexora para a frente (`ToastNotification.Activated`, com o app aberto). Não testado com clique de verdade.
- [x] Retomar o orquestrador: os últimos 10 ficam no `config.json` (conversa, pasta, agentes, grupos, posições). Abrir a conversa dele pelo histórico, ou "Retomar" na tela de início, volta com as ferramentas do Nexora e o canvas. Testado com o claude real.

### Editor mais forte ✅ (30/10/2026)

- [x] Busca em todos os arquivos (Ctrl+Shift+F ou guia Busca do painel): `git grep` em cada raiz (respeita o `.gitignore` e pega arquivo novo), leitura direta em pasta sem git, "Aa" e ".*", resultados por arquivo e clique abrindo na linha.
- [x] Árvore: Novo arquivo, Nova pasta, Renomear (F2), Apagar para a Lixeira (Delete), Copiar caminho e Mostrar no Explorer, com o nome editado no próprio item. Recusa renomear ou apagar o que tem alteração não salva no editor.

### Hub de MCP ✅ (04/11/2026)

- [x] Um servidor do `.mcp.json` por pasta de projeto para todas as sessões (`Nexora.Core/Mcp`). O `claude` recebe o mesmo nome por `--mcp-config` em HTTP local e não sobe a cópia stdio dele; os conectores do claude.ai continuam (o `--strict-mcp-config` derruba esses, por isso não é usado). Testado com o claude real: duas sessões, um processo.
- [x] Servidor de navegador (chrome-devtools, playwright, puppeteer…) fica um por sessão. Todos sobem só na primeira chamada: a resposta do `initialize` e das listas fica em cache em `%APPDATA%\Nexora\mcp`. Desligam depois de 10 min parados (`McpOciosoMinutos`) ou 2 min sem sessão. Cada servidor roda num Job Object próprio, que mata a árvore inteira (cmd → npx → node → Edge) e mede a memória.
- [x] Respeita `disabledMcpjsonServers` das settings e do `~/.claude.json`, como o `-p` faz. Desligar: `HubMcp: false` no `config.json` ou `--sem-hub`.
- Medido em 04/11 com os MCPs de um projeto da empresa (3 sessões, sem mensagem): sem o hub 2447 MB (1231 MB de MCPs em 39 processos); com o hub 1292 MB (MCPs parados até alguém usar); depois de uma consulta ao banco, 241 MB num só banco-local. Teste: `NEXORA_MEDIR=1 dotnet test --filter MedicaoTestes`.

### Layout próprio ✅ (04/11/2026)

- [x] Primeiro saiu um layout de IDE (barra de ícones à esquerda); o Vini achou VS Code demais. Ficou o estilo do Claude Desktop, sem copiar: lateral com Nova sessão, Procurar (Ctrl+K), Projetos, Visão geral, Orquestrador, Recursos, Fixadas, Em andamento e Histórico por repositório.
- [x] Painel da sessão à direita (Ctrl+Alt+B) com abas Sessão (ambiente, Remote Control, contexto pelo `get_context_usage`, saídas = arquivos editados), Arquivos, Git e Busca (Ctrl+Shift+E/G/F). Ações rápidas no fim do turno: "Commit e PR em rascunho" e "Só o commit".
- [x] Páginas: projeto (caixa "Nova sessão em…", recentes, instruções CLAUDE.md, branch, pastas, worktrees), Projetos (cartões) e Recursos (memória das sessões e dos MCPs do hub).
- [x] Tema Padrão grafite de vidro (acrílico do Windows 11, `Acrilico` no tema), botão principal branco, cores `Marca`/`MarcaFim` para o brilho da caixa e a aba ativa.
- Flags de teste: `--pagina projetos|recursos`, `--aba arquivos|git|busca|sessao`.

Pendências:

- [x] Ícone N-grafo em grafite (`#4A4553 → #1D1B22`), traço fino, glifo com 36% da largura; o magenta da primeira rodada ficou agressivo. A `Marca` do tema Padrão é o tom médio do mesmo cinza-violeta (`#948BAE → #5B5470`). Fontes em `docs/icone`, gerar com `scripts\gerar-icone.ps1`.
- Terminal integrado (ConPTY) num painel, como no Desktop.
- Agendados (tarefas recorrentes) e navegador embutido.

### 0.2.0: visual próprio, guia e instalador ✅ (04/11/2026)

- [x] O layout estilo Claude Desktop ficou "parecido demais com o Claude Desktop"; ele aprovou a proposta própria (https://claude.ai/artifact/Y1BPe4jXVxd5cwzn7vk52e): barra fina com o logo, busca e abas no topo com "Importar projeto" e "Nova sessão" branco, gaveta de Conversas (Ctrl+B) e o **quadro de sessões** como tela inicial (colunas Precisa de você, Trabalhando, Prontas e Paradas, com permitir/negar e commit no cartão; Lista continua como opção) e o botão com brilho do orquestrador.
- [x] Vidro em todos os temas pelo modo de fundo (`ModoFundo`: animado, vidro, sólido). Os temas animados mostram a animação; em "vidro" (pausa) também ficam de vidro. A transparência é aplicada pelo modo (`Catalogo.EmVidro`), então serve para os temas criados pelo usuário.
- [x] O ícone da janela segue o tema (`IconeDoTema`, desenhado no app com a `Marca` escurecida); `--icones <pasta>` grava o de cada tema.
- [x] Abertura guiada (`BoasVindasVM`, `--boas-vindas`, `--passo N`), só avança por clique: boas-vindas, requisitos (Claude Code, Git, Node, .NET 10, com o comando de instalar), login pelo próprio `claude /login`, pastas de projetos e importar `.code-workspace`, visual e atalhos. Botão de ajuda na barra reabre.
- [x] O `claude` também é achado na extensão do VS Code (e Cursor/Windsurf).
- [x] Repositório privado suiciniv-dev/nexora. A tag `v*` monta pelo Actions o `Nexora-Setup` (Inno, autocontido, exe) e o `Nexora-Portatil` (.zip com `instalar.cmd`, roda pelo `dotnet.exe` para PCs com WatchGuard).

### 0.2.2: apresentação em HTML que configura sozinha ✅ (04/11/2026)

- [x] A abertura guiada virou uma apresentação em HTML como a do Toca: janela sem borda do tamanho da área de trabalho do monitor, WebView2 num `NativeControlHost` (`WebViewNativo`), `Assets/Apresentacao/index.html` num palco de 1600×900 escalado, mensagens JSON com o `Apresentacao.cs`. Sem WebView2, cai na tela nativa de antes.
- [x] Programas que faltam: botão Instalar (winget para Git e Node, `install.ps1` oficial para o Claude Code), "Instalar os N que faltam", progresso ao vivo e o PATH recarregado do registro no fim; passo a passo à mão com o comando e a página oficial.
- [x] Login percebido sozinho (confere a cada 2,5 s no passo da conta).
- [x] Projetos achados no VS Code, Insiders, Cursor e Windsurf (`VsCode.Recentes` lê o `storage.json`): workspaces e pastas com git que ainda não estão no Nexora, com marcar e "Trazer". Pasta solta ganha um `.code-workspace` em `%APPDATA%\Nexora\Projetos`, sem criar nada no repositório. Sugere pasta vigiada onde há 2 ou mais workspaces.
- [x] `--foto` fotografa a apresentação pelo próprio WebView2 (`CapturePreviewAsync`); o `index.html` aberto fora do app usa dados de exemplo (`?passo=N&guia=1&instalando=1&logado=1`).

### 0.2.3: parado de verdade ✅ (04/11/2026)

- [x] CPU com o Nexora parado: as duas animações infinitas (bolinha de "trabalhando" e esqueleto) redesenhavam a 60 quadros por segundo, até minimizado (17% de um núcleo aberto, 6% minimizado). Agora um relógio único (`Pulso`, 8 passos por segundo) só roda com alguma janela do Nexora em foco: 2,6% em foco, 0,7% minimizado, nada sem foco.
- [x] Hub: o Claude Code manda `server/discover` ao conectar, e o hub repassava ao servidor, que subia só para responder "método não existe". Agora o cache guarda `server/discover` e as respostas -32601 junto com as listas (`respostas` no arquivo, com leitura do formato antigo `listas`). Com 3 sessões paradas no projeto com MCPs: o hub fica em 0 processos (antes 24, 948 MB); total 1.337 MB contra 2.534 MB sem o hub. O estado de cada servidor mostra o pedido que o fez subir (`SubiuPor`).

### 0.2.4: janela rápida no quadro ✅ (04/11/2026)

- [x] Clicar num cartão abre a conversa numa janela rápida por cima do quadro (pedido dele): a sessão do Nexora inteira e interativa (`ChatView` com `Compacto`, sem o cabeçalho repetido), a de fora com o trecho recente (60 itens) e a resposta que continua numa cópia. "Abrir a conversa" (ícone de maximizar) leva à aba; Esc, o X ou clicar fora fecham. Responder uma de fora troca a janela para a cópia no Nexora.
- [x] Botões da janela no canto: o Avalonia 12.1.4 media o `PART_FullScreenButton` invisível (Width 45) e deixava 47 px depois do fechar. Estilo em App.axaml zera a largura quando está invisível.

### 0.2.5: quadro pelo teclado, por projeto, uso e GitHub ✅ (04/11/2026)

- [x] Teclado no quadro: as setas andam entre os cartões (pula coluna vazia), Espaço ou Enter abrem a janela rápida e, com ela aberta, as setas passam para o cartão anterior ou o próximo. Esc fecha e devolve o foco ao cartão. Lógica em `NavegacaoQuadro`, com testes.
- [x] Quadro por projeto: "Por projeto" divide em uma faixa por projeto (projeto = dono da pasta), com o filtro de projeto no cabeçalho. `QuadroPorProjeto` no config.
- [x] Página Uso (trilho, ícone de gráfico): o plano vem do `claude -p "/usage" --output-format json --no-session-persistence` (custo zero, como no Banditboard) e a parte de cada projeto por semana vem das transcrições (`UsoLocal`, incremental por offset, cache em `%APPDATA%\Nexora\uso.json`; 5 s na primeira leitura de 8 semanas, 0,04 s depois). A parte pesa as respostas pelo preço do modelo (`PesoDoModelo`) e o "≈ % do plano" = parte × semana do /usage. Sem valores em dólar.
- [x] GitHub, leve: estado do PR no cartão (`gh pr view <branch>`), só para branch que não é a principal, uma consulta por vez, só com o quadro aberto e a janela em foco, guardado por 2 min (10 sem PR, 30 fechado). Na página do projeto, "Começar por uma issue" lista as abertas (`gh issue list`, só ao clicar) e preenche o pedido e o nome da tarefa.

### 0.2.6: issues como o quadro do GitHub ✅ (04/11/2026)

- [x] Duas contas do gh juntas: `GitHub.GhAsync` tenta, por pasta, a conta conhecida, a do ajudante de credencial do repo (`--user`), a dona do remoto, a ativa e as outras, cada uma por `GH_TOKEN` no processo (sem `gh auth switch`); guarda a que deu certo. o trabalho usa acme-dev, os pessoais suiciniv-dev.
- [x] Página Issues (trilho): o quadro do GitHub Projects dos repositórios dos projetos, com as visões do próprio quadro (filtro e agrupamento de cada uma, `items(query:)` no servidor), agrupado por Status com as cores das opções, colunas de responsável, etiquetas, prioridade, tamanho, PRs vinculados e iteração. "Só as minhas" (padrão) soma `assignee:@me`. Sem quadro, ou escolhendo "Issues abertas", vira a lista por repositório (`gh issue list`), sem repetir clones do mesmo repo.
- [x] Popup da issue (Esc, X, clicar fora; ↑ ↓ navegam): corpo em Markdown, troca de Status (`updateProjectV2ItemFieldValue`), e "Começar uma sessão": worktree novo a partir da base do projeto (padrão = `origin/HEAD`, dev nos da empresa e main nos pessoais; a escolhida fica em `BasesDasTarefas` no config), ou na pasta principal, sempre com a issue no pedido.
- [x] Permissão de projetos: sem o escopo `read:project` a página mostra "Dar permissão", que abre o `gh auth refresh --scopes project` para cada conta que precisa (troca e volta a principal sozinho).
- [x] Apresentação: passo 4 opcional do GitHub (instala o gh pelo winget, entra com `--scopes project`, lista as contas e "Usar como principal").
- [x] Workspaces pessoais em `D:\Workspaces` (Banditboard, Toca, Senna, Smartoca, FinancePay, Portfolio, Perfil GitHub, Racco IPTV, Sofa, Rumo a Vice City), vigiados pelo Nexora.

### 0.3.0: Mac, respostas no cartão, arrastar issues e atualização ✅ (04/11/2026)

- [x] Mac (Intel e Apple Silicon): `-r osx-*` troca o TFM para `net10.0` com `MAC` (csproj), ClearScript dos dois chips; WebView2 e o aviso do Windows só no Windows (`#if !MAC`), e no Mac a abertura é a nativa (`MostraBoasVindas`). `Sistema.cs`: Terminal pelo osascript, `open`, VS Code, atalhos Ctrl→⌘ (KeyBindings e textos de dica/tecla, Ctrl+Tab fica ⌃Tab), Porta-chaves só para ver se o login existe (`security find-generic-password`, sem ler o segredo). `Instalacao.PathDoMac` lê o PATH do shell de login (app aberto pelo Finder não herda). Receitas com Homebrew e `curl … install.sh`. Sessões encerradas na saída por `GrupoDeProcessos` (sem job object no Mac). Vidro: AcrylicBlur também no Mac. Barra de título: semáforo à esquerda, trilho abaixo da barra, sem a reserva de 156 px.
- [x] Testado num Mac Intel (macOS 15.8) só com dados de demonstração e um repositório git fictício: quadro, projetos, abertura, issues, vidro. A foto `--foto` da abertura nativa sai errada no Mac, a tela real (screencapture) está certa.
- [x] Actions: job `mac` (macos-latest) monta `Nexora-Mac-AppleSilicon` e `Nexora-Mac-Intel` (.app por `scripts/mac/montar-app.sh`, ícone por sips/iconutil, assinatura ad-hoc de cada arquivo e do bundle, DMG com atalho para Aplicativos) e sobe no release; `publicar-release.ps1` copia os .dmg e cria `/baixar/mac` e `/baixar/mac-intel`.
- [x] Perguntas do Claude (AskUserQuestion) no cartão do quadro e da lista (`PerguntasNoCartao`): escolha única de uma pergunta responde no clique (`OpcaoVM.Rapida`), as outras marcam e "Responder"; "Outra resposta" abre a conversa.
- [x] Issues: arrastar a linha para outro grupo troca o status (arrasto interno com fantasma e grupo aceso, otimista e desfeito se o GitHub recusar); grupos de Status aparecem mesmo vazios para servirem de alvo.
- [x] Atualização: `Atualizacoes` lê o latest do nexora-releases ao abrir (depois de 20 s) e a cada 6 h; botão "Atualizar para X" na barra; Setup (unins000.exe) roda `/VERYSILENT`, portátil roda o `instalar.ps1` do zip na mesma pasta e reabre pelo conhost, Mac troca o .app pelo do DMG. Com sessão trabalhando, pede um segundo clique.
- [x] Site: seção "Por que o Nexora" (VS Code × Claude Code), Windows e Mac, números novos de memória (VS Code 3,1 GB/28 processos, app do Claude 730 MB/9, Nexora 320 MB/1), 12 recursos, só Windows e Mac no topo (portátil virou link no cartão do Windows).

### Sugestões (04/11/2026, para ele escolher)

- [x] (05/11) Ao começar a sessão por uma issue, mover a issue para "In progress" e mostrar o número dela no cartão.
- [x] (05/11) No "Commit e PR" da sessão, pôr "Closes #N" e mover a issue para "In review".
- [x] (05/11) Busca por texto nas issues e campo de comentário no popup.
- [x] (05/11) Fila de permissões: todos os pedidos de todas as sessões numa lista só, com "permitir este comando em todas".
- [x] (05/11) Cartão "Pronta" com o resumo das alterações (+/− por arquivo) e "Ver diff" antes do commit.
- [x] (05/11) Aviso do sistema com Permitir/Negar direto, sem abrir o Nexora.
- Mac: a apresentação em HTML (hoje só no Windows, pelo WebView2) num WKWebView ou no navegador com ponte local; assinatura e notarização se um dia houver conta paga da Apple.
- Modelos de pedido por projeto ("corrigir a issue #", "revisar o PR"), com variáveis.
- Terminal integrado e Agendados (já planejados).
- Inglês no app e no site.

### 9. Depois

- Perfis (contas diferentes do Claude por `CLAUDE_CONFIG_DIR`), Mac, IntelliSense via LSP.

### Visual renovado (09/11/2026, inspirado no AgentOps/Agimo)

- [x] Tema Mosaico (grade de commits em âmbar que acende com o mouse, o clique e quando o Claude trabalha) e Mosaico estático (a mesma grade, desenhada uma vez).
- [x] "Visão geral" virou "Ao vivo" (Live now).
- [x] Lateral: worktrees juntos do projeto principal, sessões separadas por branch, segunda linha com estado e tempo ou PR, paradas há mais de 1 h recolhidas.
- [x] Leituras e buscas seguidas no chat viram um item "Explorou".
- [x] Contador de "precisa de você" no trilho e primeiro pedido no cartão do quadro.

### Pedido de segredo por formulário (12/11/2026, ideia do `request_secret` do T3 Code)

- [x] Servidor MCP `nexora-segredos` (`Nexora.Core/Segredos`, o mesmo `ServidorMcp` do orquestrador com outro nome) entra em toda sessão pelo `--mcp-config`, com `mcp__nexora-segredos__pedir_segredo` liberado no `--allowedTools`. Ferramenta `pedir_segredo(nome, motivo, destino, arquivo?)`: destino `env` grava a chave num `.env*` dentro do projeto só se o `git check-ignore` confirmar que ele é ignorado (rastreado, fora do git ou fora da pasta é recusado com explicação); destino `ambiente` guarda o valor com DPAPI (Windows, `%APPDATA%\Nexora\segredos\*.bin`) ou no Porta-chaves (Mac, `security -i` pela entrada, sem o valor na linha de comando) e o índice sem valor em `segredos.json`.
- [x] A chamada espera até 240 s; sem resposta, o agente recebe "ainda não respondeu" e, chamando de novo com os mesmos dados, volta ao mesmo formulário. O agente recebe só a confirmação ("Guardado em .env como X" ou "recusou"). Codex ganha `tool_timeout_sec = 300` para esse servidor.
- [x] Conversa: cartão com o nome, o motivo, o destino, campo mascarado com olhinho, Salvar e Recusar; a sessão fica em "Precisa de você" e o cartão do Ao vivo mostra o pedido com "Responder na conversa". O valor é apagado do item ao salvar.
- [x] Variáveis guardadas entram no ambiente das sessões (Claude, Codex, ACP), do terminal integrado e do Executar abertos para o projeto (ou subpasta). A sessão que pediu reinicia no fim do turno com `--resume` para já receber a variável.
- [x] Configurações › Segredos: nome, projeto e onde, com apagar (apaga do cofre ou tira a linha do `.env`).
- Testado com o claude real (haiku, `NEXORA_INTEGRACAO=1`, `SegredosIntegracaoTestes`): pediu os dois destinos, o `.jsonl` da conversa tem só a confirmação, e na sessão retomada `echo ${#DEPLOY_TOKEN}` deu o tamanho certo.
- Pendente: Porta-chaves do Mac não testado; Gemini e outros ACP só recebem a ferramenta se aceitarem MCP por HTTP; o segredo de um projeto não passa para os worktrees dele (só a mesma pasta e subpastas).

### Comentários de revisão (12/11/2026, inspirado no T3 Code)

- [x] No diff, o "+" da margem abre uma caixa de comentário na linha; Shift+clique em outra linha estende o intervalo. Os comentários ficam pendentes na conversa ("3 comentários"), dá para editar e apagar, e "Mandar para o agente" manda todos juntos, cada um com arquivo, linhas, trecho e texto.
- [x] Painel de PR: cada comentário (de linha, geral e o texto das revisões) tem caixinha para entrar no envio. "Mandar para o agente" vai para a conversa original ou para uma conversa nova que começa pela passagem do Senna ("Handoff: <branch>"). A nova é o padrão quando a passagem já foi feita, quando a original está parada há mais de 8 h, com o contexto quase cheio ou vazia.
- [x] Conversas resolvidas do PR dobram numa pilha de avatares na margem da linha do diff (agentes com o avatar do Nexora, pessoas com as iniciais); o clique abre. No painel, cada arquivo tem "Ver o diff no PR" (`gh pr diff`, com `git diff origin/<base>...HEAD` de reserva).

### Navegador embutido e modo design (12/11/2026, referência: preview do T3 Code)

- [x] Aba Navegador no painel da conversa (globo, ao lado de PR): `NavegadorView` reaproveita o `WebViewNativo`, o painel alarga para 560 px nessa aba e "Abrir numa janela flutuante" leva a mesma página para uma `JanelaDoNavegador`. Endereço por conversa em `EnderecosDoNavegador` (sessão e pasta). `Enderecos.Normalizar` aceita `5173`, `:3000`, `localhost:5173` e recusa `javascript:`/`file:`; Enter no endereço espera a composição do IME.
- [x] Página que falha esconde o WebView e mostra o motivo (`ErrosDeNavegacao`): servidor local parado aponta para o painel Executar. Servidores no ar (e as portas dos alvos do projeto) vêm de `ServidoresNoAr` do painel Executar.
- [x] Modo design: `Assets/Navegador/design.js` (shadow DOM com `adoptedStyleSheets`, contorno no mouse, clique bloqueado na página, editor com Enter/Shift+Enter/Esc e marcadores numerados que voltam depois de recarregar). "Mandar para o agente" captura a página pelo `CapturePreviewAsync` com os marcadores e manda o texto montado por `Anotacoes.Mensagem`.
- [x] Agente abre o navegador: servidor MCP `nexora-navegador` (o `ServidorMcp` do orquestrador com outro nome) em todas as sessões, ferramenta `abrir_navegador` liberada sem pedir permissão.
- Pendências: no Mac a página abre no navegador do sistema (falta WKWebView); o agente não lê a página nem tira print sozinho; teste ponta a ponta da ferramenta com um Claude de verdade.

### Máquinas na nuvem, fase 1: SSH (12/11/2026)

Decidido com o Vini: o agente pode rodar numa máquina na nuvem para não pesar no PC. O usuário escolhe e o Nexora só sugere; o padrão é Neste PC; projetos da Símix nunca vão para a nuvem.
- [x] `Nexora.Core/Nuvem`: `ssh.exe` com uma chave ed25519 própria do Nexora (`ChavesSsh`, parte privada só neste PC). `ProcessoRemoto` implementa `IProcessoDoAgente`, a mesma interface que `SessaoClaude`, `SessaoCodex` e `SessaoAcp` usam para o processo local.
- [x] Agente solto na máquina (`ScriptRemoto`): lê a entrada por `tail -f` e grava a saída num log. Se a conexão cair, o Nexora reconecta sozinho e relê o log do byte onde parou; o agente continua com o PC desligado.
- [x] Login do Claude: token do `claude setup-token` guardado no cofre deste PC e passado só como `CLAUDE_CODE_OAUTH_TOKEN` no processo remoto, sem ir para disco lá.
- [x] Código: as mudanças locais vão para a máquina ao abrir a conversa (`CodigoNaNuvem`). "Trazer para cá" traz a branch e as mudanças sem commit para um worktree `<branch>-nuvem` e abre uma conversa lá para o commit e o PR.
- [x] Tela: Configurações › Máquinas na nuvem (Conectar minha máquina, linha pública para copiar, token, Testar conexão com o relatório de git, node, claude e codex); chip Neste PC/Nuvem ao lado do agente em cada conversa; "Pode ir para a nuvem" na página do projeto (padrão desligado); selo de nuvem no Ao vivo e na aba; faixa "Reconectar agora". Reabrir uma conversa da nuvem traz o histórico (só Claude).
- [x] Testes: 14 numa máquina falsa (Git Bash numa pasta temporária, `MaquinaFalsa`) e `--demo --nuvem` para as telas.
- Pendente: teste com uma máquina SSH de verdade; Codex e ACP voltarem depois de fechar o Nexora (a hibernação fica desligada na nuvem).
- Fase 2 (InstaBox feito na branch instabox: chave no cofre, criar, pausar, ligar e apagar pela API, SSH com certificado de uma hora, Claude Code instalado na criação; Freestyle feito em 13/11 pela documentação, com SSH por token e SSH_ASKPASS, ainda sem teste com conta de verdade): InstaBox e Freestyle com um clique (cria e pausa a máquina pela chave de API). Fase 3 (feita na branch cloud-run: Executar na nuvem com túnel SSH para o localhost e Abrir no Nexora; sugestão com a memória do PC acima de 85%): Executar na nuvem com a porta no navegador embutido e a sugestão de ir para a nuvem. Depois: modo Automático. Cuidado: o InstaBox não diz quem opera nem tem termos publicados.

### Comando nexora no terminal ✅ (13/11/2026, inspirado no `t3` do T3 Code)

- [x] Canal local por named pipe só do usuário (`CanalLocal`, nome pela pasta de dados), uma linha JSON por pedido.
- [x] `Nexora.dll --cli`: status, abrir, sessoes, iniciar, enviar, ver, esperar, ao-vivo e parar, com `--json`, `-` para ler do pipe, `--espere` e códigos de saída (2 precisa de você, 3 Nexora fechado).
- [x] Botão em Configurações › Avançado que cria o `nexora.cmd` (ou `~/.local/bin/nexora` no Mac) e põe a pasta no PATH.
- Depois: aprovar e negar pelo terminal, `--seguir`, alvos do Executar, máquinas, link `nexora://novo` e um MCP com as mesmas ferramentas.

### Futuro: várias contas do Claude

Só quando houver mais de uma conta. Cada conta é uma pasta de configuração (`CLAUDE_CONFIG_DIR`); o Nexora não lê credencial, só aponta a pasta.
- Cadastro das pastas no guia de primeiros passos e pílula da conta no cabeçalho da sessão.
- Uso de cada conta na página Uso (sessão de 5 h e semana, quando reinicia).
- Escolha automática na sessão nova, com dois modos: Concentrar (gasta a que reinicia antes até acabar) e Espalhar (a de maior folga na semana, para nenhuma sessão parar no meio).
