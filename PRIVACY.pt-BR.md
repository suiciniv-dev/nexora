# Privacidade

O Nexora não coleta telemetria. Ele não tem biblioteca de análise nem de relatório de erros, e nada sobre como você o usa sai do seu computador sem que você ligue uma opção dizendo o que vai e para quem.

Isso é garantido por um teste, não só por esta página: o [`PrivacidadeTestes`](tests/Nexora.Testes/PrivacidadeTestes.cs) reprova o build se o código ganhar uma biblioteca de telemetria, abrir um arquivo de login de agente, ler um identificador de conta das configurações de um agente ou falar com um endereço da internet que não está na lista abaixo.

## O que sai do seu computador

| Quando | Para onde | O quê |
|---|---|---|
| Procurar atualização, quando o Nexora abre | `api.github.com` (o repositório `suiciniv-dev/nexora-releases`) e o download no `github.com` | Uma requisição comum, com `Nexora/<versão>` como identificação do programa |
| Você usa os recursos do GitHub (pull requests, issues, atividade) | GitHub, pelo seu próprio `gh` | O que o `gh` envia com a sua conta |
| Ícones dos repositórios | `github.com/<dono>.png` | Uma requisição comum |
| Notícias no Dashboard, **desligadas por padrão** | Feeds de lançamentos no `github.com`, `github.blog` e `hnrss.org` | Requisições comuns |
| Máquinas na nuvem, **só se você configurar uma** | Seu servidor por SSH, `api.instabox.sh` ou `api.freestyle.sh` | Seu código (num git bundle) e o trabalho do agente nessa máquina |
| Resumo semanal de desempenho, **desligado por padrão** | `nexora.suiciniv.dev/api/desempenho`, que manda por e-mail ao desenvolvedor do Nexora | Números de memória, processador e travadas, a família do sistema, a quantidade de núcleos, o tamanho da RAM e os nomes das funções do próprio Nexora onde a janela parou. Nada de conversas, pedidos, projetos, nomes de arquivos, de usuários ou de computadores |
| O navegador embutido | As páginas que você ou um agente abrem | O que qualquer navegador envia |

Os próprios agentes (Claude Code, Codex, Gemini CLI e outros) falam com os provedores deles nos termos deles, inclusive a telemetria de cada um. O Nexora os executa sem modificação e não muda o que eles enviam.

## O que o Nexora lê no seu computador

- Arquivos de login dos agentes (`~/.claude/.credentials.json`, o item do Claude Code no Keychain do macOS, `~/.codex/auth.json`): o Nexora só confere se eles existem, para mostrar se o agente está com login feito. Ele nunca os abre.
- `~/.claude.json`: só a lista de servidores MCP que você desligou no projeto aberto.
- As transcrições dos agentes (`~/.claude/projects` e as equivalentes de outros agentes), para mostrar o histórico e o uso do plano na sua tela.

## O que o Nexora guarda

Configurações, histórico e o registro local de desempenho (que fica no seu computador e só é enviado se você ligar o resumo semanal) ficam na pasta de dados do Nexora. Os segredos que você entrega a ele (o formulário protegido de segredos, as chaves dos provedores de nuvem e, para as máquinas na nuvem, um `claude setup-token`) ficam criptografados com o DPAPI no Windows ou no Keychain do macOS, no seu computador.
