# Nexora: templates de tarefa e prompts das novidades

## 1. O padrão das tuas conversas

Li as primeiras mensagens de cerca de 50 conversas do Acme.Shop e do Acme.Shop-melhorias, de 08/10 a 06/11. Quase todas caem em cinco formatos.

| Formato | Frequência | Como começa |
|---|---|---|
| Issue nova | cerca de metade | "Bom dia claude, issue: {link}" + texto da issue com @arquivos + fecho |
| Reviews do PR | 6 | "Handoff: {nome}, foi solicitado alguns reviews" + links + "não responder o reviewer" |
| Continuar handoff | 7 | "Handoff: {nome}, podemos dar andamento" |
| Duas issues no mesmo PR | 2 | "temos 2 issues que vamos fazer no mesmo PR" |
| Relatório de PRs | 2 | "Analise meus PRs dos últimos N dias e gere uma lista para apresentação" |

O fecho da issue nova varia pouco. Aparece quase sempre "pode criar a branch, mas não committar, só depois do meu ok". Muda só o que vem antes:

- **Fazer direto:** "Analisar e já ajustar."
- **Investigar primeiro:** "Analisar e tentar simular. Daí me passar, depois do meu ok ajustar."
- **Levantamento:** "Fazer primeiro um levantamento das mudanças, análises e fases."

Duas coisas tu sempre acrescentas à mão no texto da issue. A primeira são os @arquivos com "acho que é esse". A segunda é o contexto que só tu sabes, como "tem algo na memória" ou "tenho um PR aberto que pode interferir". Por isso o template não deve enviar sozinho: ele monta o rascunho e deixa o cursor no texto da issue.

## 2. Os templates propostos

Variáveis: `{saudacao}` (Bom dia, Boa tarde, Boa noite), `{url}`, `{numero}`, `{titulo}`, `{corpo}` (texto da issue), `{handoff}` (nome do handoff da branch), `{links}` (um por linha), `{dias}`.

### Issue nova

```
{saudacao} claude, issue: {url}

{corpo}

{fecho}
Pode criar a branch, mas não committar. Só depois do meu ok. Qualquer coisa me perguntar.
```

O `{fecho}` é uma escolha de três botões no próprio rascunho: "Analisar e já ajustar", "Analisar e me passar antes de ajustar" e "Fazer primeiro um levantamento".

### Reviews do PR

```
Handoff: {handoff}, foi solicitado alguns reviews.

{links}

Não é pra responder o reviewer, apenas fazer. Sem committar, só depois do meu ok.
```

### Continuar handoff

```
Handoff: {handoff}, podemos dar andamento. O que está pendente? Me passa, depois dou o ok para seguirmos.
```

### Duas issues no mesmo PR

```
{saudacao} claude, temos 2 issues que vamos fazer no mesmo PR.

{url}
{corpo}

{url2}
{corpo2}

Pode criar a branch, mas não committar. Só depois do meu ok. Qualquer coisa me perguntar.
```

### Relatório de PRs

```
Analise meus PRs dos últimos {dias} dias e gere uma lista para apresentação, ordenada do maior impacto para o menor, em markdown.
Separar em Novos e Melhorias/Ajustes, só com os tópicos e um emoji no início. Títulos com ###### e tópicos com -.
Em cada item: título do PR, link do PR e link da tela do sistema no tenant de demonstração (https://demo.acme.example/painel).
```

### O que já existe e o que falta

O botão Nova sessão da página de Issues já monta "{saudacao} claude, issue: {url}" e o texto da issue em tópicos. Faltam três coisas:

- **O fecho** com o "pode criar a branch, mas não committar".
- **Manter o recuo da issue.** Hoje a função que monta o rascunho tira o recuo de cada linha, e os sub-tópicos viram tópicos soltos. As tuas issues usam muito sub-tópico.
- **Escolher o template por projeto**, com o "Issue nova" como padrão nos projetos da empresa.

## 3. Prompts para rodar em outra sessão

Cada prompt é para uma sessão nova do Claude Code em `D:\Nexora`. Todos trazem as regras do projeto para o modelo mais barato não errar. Sugestão de modelo ao lado de cada um.

### Regras comuns (já vão dentro de cada prompt)

```
Regras do projeto, leia o CLAUDE.md antes:
- Responda em português do Brasil. Sem comentários no código.
- Componentes visuais novos vão para src/Nexora/Componentes.axaml.
- Todo texto visível passa por Idioma.T ou Idioma.F e ganha a tradução em src/Nexora/Traducao/Ingles.*.cs. Os testes de tradução falham se faltar.
- Rode `dotnet test tests/Nexora.Testes` no fim, tudo tem que passar.
- Para ver a tela, use scripts\passeio.ps1 ou o Nexora demo isolado com --foto. Nunca feche o Nexora que está aberto, nem clique ou digite no PC.
- Crie uma branch, mas não faça commit sem o meu ok.
```

### A. Templates de tarefa (Sonnet 5.5, esforço médio)

```
Quero templates de tarefa no Nexora. Um template é um texto de pedido salvo, com variáveis, que monta o rascunho da sessão sem enviar.

Leia antes:
- docs/conceitos/novos/novidades.md, seções 1 e 2: os cinco templates, as variáveis e o que falta.
- src/Nexora/ViewModels/IssuesVM.cs: Rascunho, TopicosDoCorpo, Saudacao, NovaSessao e ComecarEmWorktree.
- tests/Nexora.Testes/RascunhoDaIssueTestes.cs.
- src/Nexora/Config.cs, para guardar os templates.

O que fazer:
1. Um modelo de template com nome, texto e projetos onde vale (vazio é todos). Guardar na Config. Já vir com os cinco da seção 2 na primeira vez.
2. Trocar o Rascunho fixo da página de Issues pelo template "Issue nova" do projeto, com as variáveis preenchidas.
3. TopicosDoCorpo passa a manter o recuo dos sub-tópicos da issue. Ajuste os testes do rascunho.
4. No campo de mensagem de uma sessão nova, um botão "Templates" lista os templates do projeto. Escolher um põe o texto no rascunho, e o cursor vai para o primeiro trecho a completar.
5. No "Issue nova", três botões trocam o fecho: "Analisar e já ajustar", "Analisar e me passar antes de ajustar" e "Fazer primeiro um levantamento".
6. Uma tela simples para criar, editar, duplicar e apagar templates. Pode ficar dentro de Projetos, num item "Templates".
7. Testes do preenchimento das variáveis e do recuo. Um passeio do botão Templates com foto.

Antes de codar, me mostre o plano em tópicos e espere o meu ok.
```

### B. Modo foco (Sonnet 5.5, esforço médio)

```
Quero um "Modo foco" no Nexora. Ele mostra só as sessões que precisam de mim, uma por vez, em tela cheia, para eu despachar rápido.

Leia antes:
- src/Nexora/ViewModels/FilaDePermissoes.cs e a faixa de permissões do quadro.
- src/Nexora/ViewModels/NavegacaoQuadro.cs, que já passa de cartão com as setas.
- src/Nexora/ViewModels/VisaoGeralVM.cs e CartaoVM.cs.
- docs/conceitos/novos/mesa-inicio.png, para o visual (vidro, borda âmbar, mascote).

O que fazer:
1. Atalho F no quadro e Ctrl+Shift+F em qualquer tela. Esc sai.
2. Uma camada de vidro sobre a janela, com um cartão grande da sessão da vez: projeto, branch, issue, a última fala do Claude e o pedido (permissão, pergunta com opções ou plano).
3. Teclas: Enter ou P permite, N nega, T permite em todas, setas passam para a próxima. Para pergunta, 1 a 9 escolhem a opção. Para plano, A aprova e C continua planejando. O, ou clicar, abre a conversa.
4. Embaixo, uma fileira dos próximos pedidos e um contador "3 de 5".
5. Quando a fila esvazia, mostra "Nada esperando por você" e sai sozinho depois de 2 segundos.
6. Pedidos novos entram no fim da fila enquanto o modo está aberto.
7. Testes da ordem da fila e das teclas. Um passeio com foto do modo aberto, no demo.

Antes de codar, me mostre o plano em tópicos e espere o meu ok.
```

### C. Resumo do dia (Sonnet 5.5, esforço alto)

```
Quero um "Resumo do dia" no Nexora: uma tela com o que cada sessão fez hoje, os PRs abertos e o uso do plano, e que dá para copiar como texto.

Leia antes:
- src/Nexora.Core/Historico (ConversasSalvas, Transcricao, Andamento), para saber o que cada sessão fez.
- src/Nexora.Core/Uso/UsoLocal.cs e src/Nexora/ViewModels/UsoVM.cs, para o uso por projeto.
- src/Nexora.Core/Git, para os PRs das branches.
- src/Nexora/ViewModels/AgendadosVM.cs e src/Nexora.Core/Agenda/Agendamento.cs.
- docs/conceitos/novos/rio-inicio.png, painel "Hoje", para o visual.

O que fazer:
1. Montar o resumo sem gastar tokens: sessões do dia por projeto, título, branch, issue, arquivos alterados com +/- e o estado do PR.
2. Números do topo: sessões, PRs, arquivos e linhas. Embaixo, o uso do dia por projeto.
3. Botão "Escrever com o Claude", que abre uma sessão com Haiku e pede um parágrafo por projeto. Só roda quando eu clicar.
4. Botões "Copiar em markdown" e "Copiar para o Slack". O markdown segue o meu formato: títulos com ###### e tópicos com -, um emoji no início.
5. Escolher o dia, com hoje como padrão e as setas para os dias anteriores.
6. Abrir pela página Uso e pela busca (Ctrl+K "resumo").
7. Opcional: um agendado "Resumo do dia" às 18:00 que só gera a tela e manda a notificação.
8. Testes da montagem com conversas de exemplo. Um passeio com foto no demo.

Antes de codar, me mostre o plano em tópicos e espere o meu ok.
```

### D. Replay de sessão (Opus 5.5, esforço alto)

```
Quero um "Replay" nas conversas do Nexora: voltar no tempo de uma sessão com um controle deslizante e ver como os arquivos estavam em cada passo.

Leia antes:
- src/Nexora.Core/Historico/Transcricao.cs e src/Nexora.Core/Claude/Eventos.cs, para os eventos do JSONL.
- src/Nexora/ViewModels/ChatVM.cs e ChatVM.Revisao.cs, e src/Nexora/Views/DiffView.axaml.
- src/Nexora/ViewModels/ChatVM.Detalhes.cs, aba de arquivos editados pela sessão.
- docs/conceitos/novos/constelacao-sessao.png, cartão "Replay da sessão", para o visual.

O que fazer:
1. Montar a linha do tempo a partir do JSONL: um marco por mensagem minha, por edição (Edit, Write, MultiEdit) e por comando. Cor por tipo.
2. Um botão Replay no cabeçalho da conversa abre uma faixa com o deslizante, play, pausa e a velocidade.
3. Ao mover, a conversa rola até o passo e o painel lateral mostra os arquivos como estavam ali. Reconstruir o arquivo aplicando as edições do JSONL ao contrário a partir do estado atual. Se uma edição não bater, mostrar só o diff daquele passo e avisar.
4. Clicar num marco de edição abre o diff daquele passo.
5. Funciona também em conversas do histórico e de fora do Nexora, só leitura.
6. Testes da reconstrução com um JSONL de exemplo, incluindo o caso em que o arquivo mudou fora da sessão.

Antes de codar, me mostre o plano em tópicos e espere o meu ok.
```

## 4. Ordem que eu seguiria

1. Templates, porque tu usas todo dia e é o mais barato.
2. Modo foco, porque reaproveita a fila de permissões e a navegação que já existem.
3. Resumo do dia, que depois serve de base para o painel "Hoje" do conceito 2.
4. Replay, o mais trabalhoso por causa da reconstrução dos arquivos.
