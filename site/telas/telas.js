(() => {
  const EN = document.documentElement.lang === "en";
  const t = (pt, en) => (EN ? en : pt);

  const P = {
    "painel-geral": "M3 3 L10 3 L10 10 L3 10 Z M14 3 L21 3 L21 10 L14 10 Z M14 14 L21 14 L21 21 L14 21 Z M3 14 L10 14 L10 21 L3 21 Z",
    conversa: "M21 15 A2 2 0 0 1 19 17 L7 17 L3 21 L3 5 A2 2 0 0 1 5 3 L19 3 A2 2 0 0 1 21 5 Z",
    camadas: "M12.83 2.18 A2 2 0 0 0 11.17 2.18 L2.6 6.08 A1 1 0 0 0 2.6 7.91 L11.18 11.82 A2 2 0 0 0 12.84 11.82 L21.42 7.92 A1 1 0 0 0 21.42 6.09 Z M2 12 A1 1 0 0 0 2.58 12.91 L11.18 16.82 A2 2 0 0 0 12.83 16.82 L21.41 12.92 A1 1 0 0 0 22 12 M2 17 A1 1 0 0 0 2.58 17.91 L11.18 21.82 A2 2 0 0 0 12.83 21.82 L21.41 17.92 A1 1 0 0 0 22 17",
    issue: "M12 2 A10 10 0 1 0 12 22 A10 10 0 1 0 12 2 M12 11 A1 1 0 1 0 12 13 A1 1 0 1 0 12 11",
    orquestra: "M5 3 L9 3 A2 2 0 0 1 11 5 L11 9 A2 2 0 0 1 9 11 L5 11 A2 2 0 0 1 3 9 L3 5 A2 2 0 0 1 5 3 Z M7 11 L7 15 A2 2 0 0 0 9 17 L13 17 M15 13 L19 13 A2 2 0 0 1 21 15 L21 19 A2 2 0 0 1 19 21 L15 21 A2 2 0 0 1 13 19 L13 15 A2 2 0 0 1 15 13 Z",
    relogio: "M2 12 A10 10 0 1 0 22 12 A10 10 0 1 0 2 12 M12 6 L12 12 L16 14",
    grafico: "M3 3 L3 19 A2 2 0 0 0 5 21 L21 21 M18 17 L18 9 M13 17 L13 5 M8 17 L8 14",
    memoria: "M6 4 L18 4 A2 2 0 0 1 20 6 L20 18 A2 2 0 0 1 18 20 L6 20 A2 2 0 0 1 4 18 L4 6 A2 2 0 0 1 6 4 Z M9 9 L15 9 L15 15 L9 15 Z M9 1.5 L9 4 M15 1.5 L15 4 M9 20 L9 22.5 M15 20 L15 22.5 M20 9 L22.5 9 M20 15 L22.5 15 M1.5 9 L4 9 M1.5 15 L4 15",
    ajuda: "M2 12 A10 10 0 1 0 22 12 A10 10 0 1 0 2 12 M9.1 9 A3 3 0 0 1 14.9 10 C14.9 12 11.9 13 11.9 13 M12 17 L12.01 17",
    ajustes: "M12.22 2 L11.78 2 A2 2 0 0 0 9.78 4 L9.78 4.18 A2 2 0 0 1 8.78 5.91 L8.35 6.16 A2 2 0 0 1 6.35 6.16 L6.2 6.08 A2 2 0 0 0 3.47 6.81 L3.25 7.19 A2 2 0 0 0 3.98 9.92 L4.13 10.02 A2 2 0 0 1 5.13 11.74 L5.13 12.25 A2 2 0 0 1 4.13 13.99 L3.98 14.08 A2 2 0 0 0 3.25 16.81 L3.47 17.19 A2 2 0 0 0 6.2 17.92 L6.35 17.84 A2 2 0 0 1 8.35 17.84 L8.78 18.09 A2 2 0 0 1 9.78 19.82 L9.78 20 A2 2 0 0 0 11.78 22 L12.22 22 A2 2 0 0 0 14.22 20 L14.22 19.82 A2 2 0 0 1 15.22 18.09 L15.65 17.84 A2 2 0 0 1 17.65 17.84 L17.8 17.92 A2 2 0 0 0 20.53 17.19 L20.75 16.8 A2 2 0 0 0 20.02 14.07 L19.87 13.99 A2 2 0 0 1 18.87 12.25 L18.87 11.75 A2 2 0 0 1 19.87 10.01 L20.02 9.92 A2 2 0 0 0 20.75 7.19 L20.53 6.81 A2 2 0 0 0 17.8 6.08 L17.65 6.16 A2 2 0 0 1 15.65 6.16 L15.22 5.91 A2 2 0 0 1 14.22 4.18 L14.22 4 A2 2 0 0 0 12.22 2 Z M12 9 A3 3 0 1 0 12 15 A3 3 0 1 0 12 9",
    busca: "M3 11 A8 8 0 1 0 19 11 A8 8 0 1 0 3 11 M21 21 L16.7 16.7",
    foco: "M3 8 L3 5 A2 2 0 0 1 5 3 L8 3 M16 3 L19 3 A2 2 0 0 1 21 5 L21 8 M21 16 L21 19 A2 2 0 0 1 19 21 L16 21 M8 21 L5 21 A2 2 0 0 1 3 19 L3 16 M9 12 A3 3 0 1 0 15 12 A3 3 0 1 0 9 12",
    maximizar: "M15 3 L21 3 L21 9 M9 21 L3 21 L3 15 M21 3 L14 10 M3 21 L10 14",
    faisca: "M12 3 L12 21 M3 12 L21 12 M6.6 6.6 L17.4 17.4 M17.4 6.6 L6.6 17.4",
    terminal: "M4 17 L10 11 L4 5 M12 19 L20 19",
    pasta: "M20 20 A2 2 0 0 0 22 18 L22 8 A2 2 0 0 0 20 6 L12.1 6 A2 2 0 0 1 10.41 5.1 L9.6 3.9 A2 2 0 0 0 7.93 3 L4 3 A2 2 0 0 0 2 5 L2 18 A2 2 0 0 0 4 20 Z",
    branch: "M6 3 L6 15 M15 6 A3 3 0 1 0 21 6 A3 3 0 1 0 15 6 M3 18 A3 3 0 1 0 9 18 A3 3 0 1 0 3 18 M18 9 A9 9 0 0 1 9 18",
    worktree: "M9 18 A3 3 0 1 0 15 18 A3 3 0 1 0 9 18 M3 6 A3 3 0 1 0 9 6 A3 3 0 1 0 3 6 M15 6 A3 3 0 1 0 21 6 A3 3 0 1 0 15 6 M18 9 L18 11 Q18 12 17 12 L7 12 Q6 12 6 11 L6 9 M12 12 L12 15",
    vinculo: "M10 13 A5 5 0 0 0 17.54 13.54 L20.54 10.54 A5 5 0 0 0 13.47 3.47 L11.75 5.18 M14 11 A5 5 0 0 0 6.46 10.46 L3.46 13.46 A5 5 0 0 0 10.53 20.53 L12.24 18.82",
    abaixo: "M6 9 L12 15 L18 9",
    ok: "M20 6 L9 17 L4 12",
    alerta: "M2 12 A10 10 0 1 0 22 12 A10 10 0 1 0 2 12 M12 8 L12 12 M12 16 L12.01 16",
    raio: "M13 2 L4 14 L12 14 L11 22 L20 10 L12 10 Z",
    clipe: "M13.234 20.252 L21 12.3 M16 6 L7.586 14.586 A2 2 0 0 0 10.414 17.414 L18.828 9 A4 4 0 0 0 13.172 3.343 L4.757 11.757 A6 6 0 0 0 13.243 20.243",
    microfone: "M12 2 A3 3 0 0 0 9 5 L9 12 A3 3 0 0 0 15 12 L15 5 A3 3 0 0 0 12 2 Z M19 10 L19 12 A7 7 0 0 1 5 12 L5 10 M12 19 L12 22",
    subagente: "M12 8 L12 4 L8 4 M6 8 L18 8 A2 2 0 0 1 20 10 L20 18 A2 2 0 0 1 18 20 L6 20 A2 2 0 0 1 4 18 L4 10 A2 2 0 0 1 6 8 Z M2 14 L4 14 M20 14 L22 14 M15 13 L15 15 M9 13 L9 15",
    enviar: "M12 19 L12 5 M5 12 L12 5 L19 12",
    mais: "M12 5 L12 19 M5 12 L19 12",
    externo: "M15 3 L21 3 L21 9 M10 14 L21 3 M18 13 L18 19 A2 2 0 0 1 16 21 L5 21 A2 2 0 0 1 3 19 L3 8 A2 2 0 0 1 5 6 L11 6",
    commit: "M9 12 A3 3 0 1 0 15 12 A3 3 0 1 0 9 12 M3 12 L9 12 M15 12 L21 12",
    lista: "M8 6 L21 6 M8 12 L21 12 M8 18 L21 18 M3 6 L3.01 6 M3 12 L3.01 12 M3 18 L3.01 18",
    quadro: "M5 4 L8 4 A1 1 0 0 1 9 5 L9 19 A1 1 0 0 1 8 20 L5 20 A1 1 0 0 1 4 19 L4 5 A1 1 0 0 1 5 4 Z M11 4 L13 4 A1 1 0 0 1 14 5 L14 13 A1 1 0 0 1 13 14 L11 14 A1 1 0 0 1 10 13 L10 5 A1 1 0 0 1 11 4 Z M17 4 L19 4 A1 1 0 0 1 20 5 L20 16 A1 1 0 0 1 19 17 L17 17 A1 1 0 0 1 16 16 L16 5 A1 1 0 0 1 17 4 Z",
    fixar: "M12 17 L12 22 M9 10.76 A2 2 0 0 1 7.89 12.55 L6.11 13.45 A2 2 0 0 0 5 15.24 L5 16 A1 1 0 0 0 6 17 L18 17 A1 1 0 0 0 19 16 L19 15.24 A2 2 0 0 0 17.89 13.45 L16.11 12.55 A2 2 0 0 1 15 10.76 L15 7 A1 1 0 0 1 16 6 A2 2 0 0 0 18 4 A2 2 0 0 0 16 2 L8 2 A2 2 0 0 0 6 4 A2 2 0 0 0 8 6 A1 1 0 0 1 9 7 Z",
    atualizar: "M3 12 A9 9 0 0 1 12 3 A9.75 9.75 0 0 1 18.74 5.74 L21 8 M21 3 L21 8 L16 8 M21 12 A9 9 0 0 1 12 21 A9.75 9.75 0 0 1 5.26 18.26 L3 16 M8 16 L3 16 L3 21",
  };
  const ic = (n, c = "") => `<svg viewBox="0 0 24 24"${c ? ` class="${c}"` : ""}><path d="${P[n]}"/></svg>`;

  const TRILHO = [["painel-geral", "ao-vivo"], ["conversa", "conversas"], ["camadas", "projetos"], ["issue", "issues"], ["orquestra", "orquestra"], ["relogio", "agendados"], ["grafico", "uso"], ["memoria", "recursos"]];
  function trilho(ativo, painel = false) {
    const item = ([i, id]) => `<span class="${id === ativo || (painel && id === "conversas") ? "ativo" : ""}${id === "ao-vivo" ? " contador" : ""}">${ic(i)}</span>`;
    return `<div class="jt-trilho"><div class="cap">${TRILHO.map(item).join("")}</div><div class="cap fim">${item(["ajuda", "guia"])}${item(["ajustes", "config"])}</div></div>`;
  }

  function lateral(conversaAtiva = true) {
    return `<div class="jt-lat"><div class="tit">${t("Conversas", "Conversations")}<span>${ic("atualizar")}${ic("lista")}</span></div>
    <div class="sec">${t("Em andamento", "In progress")}</div>
    <div class="proj"><s>LO</s>Loja</div>
    <div class="ramo">${ic("branch")}cupom-checkout</div>
    <div class="cv${conversaAtiva ? " ativo" : ""}"><b><i style="--c:var(--t-laranja)"></i>${t("Cupom de desconto no checkout", "Discount coupon at checkout")}</b><small class="pede">${t("esperando há 3 min", "waiting for 3 min")}</small></div>
    <div class="ramo">${ic("branch")}dev</div>
    <div class="cv"><b><i></i>${t("Orquestrador", "Orchestrator")}</b><small>${t("esperar s1, s2, s3 · 9 min", "wait s1, s2, s3 · 9 min")}</small></div>
    <div class="proj"><s>LP</s>Loja-pedidos</div>
    <div class="cv"><b><i style="--c:var(--t-laranja)"></i>${t("Erro na tela de pedidos", "Orders page error")}</b><small class="pede">${t("esperando há 21 s", "waiting for 21 s")}</small></div>
    <div class="proj"><s>AN</s>App.Notas</div>
    <div class="cv"><b><i style="--c:var(--t-verde)"></i>${t("Notas offline", "Offline notes")}</b><small>${t("PR #51 · checks ok · aprovado", "PR #51 · checks ok · approved")}</small></div>
    <div class="sec">${t("Histórico", "History")}</div>
    <div class="hist">${t("Migração do relatório", "Report migration")}<span>${t("há 2 h", "2 h ago")}</span></div></div>`;
  }

  const aba = (texto, ativa, cor) => `<div class="jt-aba${ativa ? "" : " fraca"}">${cor ? `<i style="background:${cor}"></i>` : ""}${texto}${ativa ? "<span>×</span>" : ""}</div>`;
  const topo = (abas, larga) => `<div class="jt-topo"><span class="jt-marca${larga ? "" : " curta"}"><span class="jt-logo">N</span>${larga ? "Nexora" : ""}</span>${abas}${abas ? '<span class="jt-mais">+</span>' : ""}<span class="jt-icones">${ic("busca")}${ic("foco")}${ic("maximizar")}</span></div>`;
  const rodape = () => `<div class="jt-rodape"><span>${ic("faisca", "faisca")}<u><b></b></u>${t("4% · 3h 12m · 35% na semana", "4% · 3h 12m · 35% this week")}</span><span class="codex">${ic("terminal")}${t("Codex 25% · 40% na semana", "Codex 25% · 40% this week")}</span><span class="mem">${ic("memoria")}${t("2,4 GB", "2.4 GB")}</span></div>`;

  function moldura({ ativo = null, recolhida = false, abas, corpo, semTrilho = false, conversaAtiva = false }) {
    const comLateral = !recolhida && !semTrilho;
    return {
      classe: (comLateral ? " com-lista" : "") + (semTrilho ? " sem-trilho" : ""),
      html: `${topo(abas, comLateral || semTrilho)}${semTrilho ? "" : comLateral ? trilho(ativo, true) + lateral(conversaAtiva) : trilho(ativo)}<div class="d-cartao">${corpo}</div>${rodape()}`,
    };
  }

  const ABA_CUPOM = (ativa) => aba(t("Cupom de desconto no ch…", "Discount coupon at ch…"), ativa, "var(--t-laranja)");

  const cartao = (o) => `<div class="d-ct ${o.classe || ""}"><div class="t"><s>${o.sigla}</s>${o.titulo}</div><div class="o">${o.origem}</div>${o.acao ? `<div class="a">${o.acao}</div>` : ""}${o.barra != null ? `<div class="l"><b style="width:${o.barra}%"></b></div>` : ""}${o.pr ? `<div class="pr">${ic("branch")}${o.pr}</div>` : ""}${o.botoes ? `<div class="bts">${o.botoes}</div>` : ""}</div>`;
  const coluna = (nome, cor, itens) => `<div class="d-col"><h4><i style="background:${cor}"></i>${nome}<small>${itens.length}</small></h4>${itens.join("")}</div>`;
  const cab = (titulo, sub, extra = "") => `<div class="d-cab"><div><h3>${titulo}</h3>${sub ? `<p>${sub}</p>` : ""}</div>${extra}</div>`;
  const seg = (itens, ativo) => `<div class="d-seg">${itens.map(([i, n], k) => `<span class="${k === ativo ? "ativo" : ""}">${i ? ic(i) : ""}${n}</span>`).join("")}</div>`;
  const busca = (texto) => `<div class="d-busca">${ic("busca")}${texto}</div>`;

  function conversa(menu) {
    return `<div class="jt-conversa d-conv">
      <div class="jt-cab"><span class="d-av">LO</span><div><b>${t("Cupom de desconto no checkout", "Discount coupon at checkout")}</b><p>${ic("pasta")}Loja ${ic("branch")}<code>cupom-checkout</code><span class="d-ctx"><u><b></b></u>${t("84k de 200k (42%)", "84k of 200k (42%)")}</span></p></div><span class="jt-estado d-pede"><i></i>${t("Precisa de você", "Needs you")}</span></div>
      <div class="jt-fio">
        <div class="jt-bolha">${t("Revise o cupom de desconto do checkout e me diga o que falta para fechar a issue 48.", "Review the checkout discount coupon and tell me what's left to close issue 48.")}</div>
        <div class="jt-linha"><i class="ponto"></i><em>${t("Pensou por 7 s · 2,1 mil tokens", "Thought for 7 s · 2.1k tokens")}</em></div>
        <div class="jt-resposta"><span class="faisca">✻</span><div>
          <p>${t("Olhei a issue e o código do <code>CupomService</code>. Faltam <b>duas coisas</b> antes de fechar a 48:", "I read the issue and the <code>CupomService</code> code. <b>Two things</b> are missing before closing #48:")}</p>
          <ul><li>${t("<b>Cupom vencido:</b> hoje o serviço lança exceção; o certo é devolver uma falha que a tela consiga mostrar.", "<b>Expired coupon:</b> the service throws today; it should return a failure the page can show.")}</li><li>${t("<b>Limite por cliente:</b> o mesmo cupom pode ser usado duas vezes se a pessoa clicar rápido.", "<b>Per-customer limit:</b> the same coupon can be used twice with a quick double click.")}</li></ul>
        </div></div>
        <div class="jt-ferramenta"><i style="background:var(--t-verde)"></i><b>${t("Explorou", "Explored")}</b><span class="d-suave">${t("2 arquivos lidos · 1 busca", "2 files read · 1 search")}</span></div>
        <div class="jt-ferramenta"><i style="background:var(--t-laranja)"></i><b>Bash</b><code>dotnet test tests/Loja.Application.Tests --filter Cupom</code><span class="mm"><ins>42 ${t("passaram", "passed")}</ins></span></div>
        <div class="d-perm"><b>${ic("alerta")}${t("Permitir Bash: aplica a migration", "Allow Bash: apply the migration")}</b><p>${t("O comando altera o banco de dados local.", "The command changes the local database.")}</p><pre>dotnet ef database update --project src/Loja.Infra</pre><div class="bts"><span class="claro">${t("Permitir", "Allow")}</span><span>${t("Não perguntar de novo", "Don't ask again")}</span><span>${t("Negar", "Deny")}</span></div></div>
      </div>
      <div class="jt-caixa">
        <p>${t("Mensagem para o Claude · @ cita um arquivo · / lista comandos", "Message Claude · @ mentions a file · / lists commands")}</p>
        <div class="jt-acoes">${ic("clipe")}${ic("microfone")}<span class="jt-pilula">✻ Claude Code</span><span class="jt-pilula${menu ? " d-aberta" : ""}">Opus 5.5 · ${t("Alto", "High")} ${ic("abaixo")}</span><span class="dica">${t("Enter envia", "Enter sends")}</span><span class="jt-pilula modo">${ic("raio")}${t("Perguntar sempre", "Always ask")}</span><span class="jt-enviar">${ic("enviar")}</span></div>
      </div>
      ${menu ? menuModelo() : ""}
    </div>`;
  }

  function menuModelo() {
    const modelo = (nome, desc, ativo) => `<div class="d-op${ativo ? " ativo" : ""}"><div><b>${nome}</b><small>${desc}</small></div>${ativo ? ic("ok") : ""}</div>`;
    const chave = (nome, desc, ligada) => `<div class="d-op"><div><b>${nome}</b><small>${desc}</small></div><span class="d-chave${ligada ? " ligada" : ""}"><i></i></span></div>`;
    return `<div class="d-menu">
      <h6>${t("Modelo", "Model")}</h6>
      ${modelo("Opus 5.5", t("O mais capaz, para tarefas longas", "Most capable, for long tasks"), true)}
      ${modelo("Sonnet 5.5", t("Rápido e equilibrado", "Fast and balanced"), false)}
      ${modelo("Haiku 5.5", t("O mais rápido, para tarefas simples", "Fastest, for simple tasks"), false)}
      <h6>${t("Esforço", "Effort")}</h6>
      <div class="d-niveis">${[t("Baixo", "Low"), t("Médio", "Medium"), t("Alto", "High"), "Max"].map((n, i) => `<span class="${i === 2 ? "ativo" : ""}">${n}</span>`).join("")}</div>
      <div class="d-sep"></div>
      ${chave(t("Modo rápido", "Fast mode"), t("Respostas mais rápidas no Opus", "Faster output on Opus"), false)}
      ${chave("Ultracode", t("Vários agentes no mesmo pedido", "Several agents on one request"), false)}
    </div>`;
  }

  const TELAS = {
    quadro: () => moldura({
      ativo: "ao-vivo",
      abas: ABA_CUPOM(false),
      corpo: `<div class="d-pag">${cab(t("Ao vivo", "Live now"), t("6 sessões · 2 precisam de você", "6 sessions · 2 need you"), busca(t("Buscar sessões", "Search sessions")) + seg([["quadro", t("Quadro", "Board")], ["painel-geral", t("Mesa", "Desk")]], 0))}
        <div class="d-colunas">
          ${coluna(t("Precisa de você", "Needs you"), "var(--t-laranja)", [cartao({ classe: "pede", sigla: "LO", titulo: t("Migration do cupom", "Coupon migration"), origem: "Loja · cupom-checkout", acao: t("Permitir Bash: dotnet ef database update", "Allow Bash: dotnet ef database update"), botoes: `<span class="claro">${t("Permitir", "Allow")}</span><span>${t("Negar", "Deny")}</span>` }), cartao({ classe: "pede", sigla: "LP", titulo: t("Erro na tela de pedidos", "Orders page error"), origem: "Loja-pedidos · erro-tela", acao: t("Pergunta: manter o filtro por data?", "Question: keep the date filter?"), botoes: `<span class="claro">${t("Responder", "Reply")}</span>` })])}
          ${coluna(t("Trabalhando", "Working"), "var(--t-texto)", [cartao({ sigla: "LO", titulo: t("Cupom de desconto", "Discount coupon"), origem: "Loja · cupom-checkout", acao: "Edit  src/Loja.Web/Cupom.razor", barra: 62 }), cartao({ sigla: "LA", titulo: t("Limite de requisições", "Rate limiting"), origem: "Loja.Api · rate-limit", acao: t("Pensando… 2,1 mil tokens", "Thinking… 2.1k tokens"), barra: 34 })])}
          ${coluna(t("Prontas", "Ready"), "var(--t-verde)", [cartao({ classe: "pronta", sigla: "AN", titulo: t("Notas offline", "Offline notes"), origem: "App.Notas · notas-offline", acao: t("Pronto: o teste novo passou.", "Done: the new test passed."), pr: t("PR #51 · checks ok · aprovado", "PR #51 · checks ok · approved"), botoes: `<span>${t("Ver o PR", "Open PR")}</span>` })])}
          ${coluna(t("Paradas", "Idle"), "var(--t-texto-fraco)", [cartao({ classe: "parada", sigla: "SI", titulo: t("Relatório mensal", "Monthly report"), origem: "Site · relatorio", acao: t("parada há 2 dias", "idle for 2 days") })])}
        </div></div>`,
    }),

    conversa: () => moldura({
      conversaAtiva: true,
      abas: ABA_CUPOM(true),
      corpo: conversa(false),
    }),

    modelo: () => moldura({
      recolhida: true,
      abas: ABA_CUPOM(true),
      corpo: conversa(true),
    }),

    "issues-kanban": () => issues(0),
    "issues-lista": () => issues(1),

    issue: () => moldura({
      ativo: "issues",
      abas: ABA_CUPOM(false),
      corpo: `<div class="d-pag d-issue"><div class="d-issue-texto">
          <p class="d-suave">Loja · #48 · ${t("aberta por vini há 3 dias", "opened by vini 3 days ago")}</p>
          <h3>${t("Cupom de desconto no checkout", "Discount coupon at checkout")}</h3>
          <div class="d-chips"><span class="d-estado-issue">${ic("issue")}In progress ${ic("abaixo")}</span><span class="d-et" style="--c:#7CC8E8">feature</span><span class="d-et" style="--c:#E59AC4">checkout</span></div>
          <div class="d-md"><h5>${t("O que fazer", "What to do")}</h5><ul><li>${t("Aceitar um código de cupom no checkout e mostrar o desconto no total.", "Accept a coupon code at checkout and show the discount in the total.")}</li><li>${t("Cupom vencido mostra uma mensagem, sem erro na tela.", "An expired coupon shows a message, with no error page.")}</li><li>${t("Cada cliente usa o mesmo cupom uma vez só.", "Each customer uses a coupon only once.")}</li></ul><h5>${t("Pronto quando", "Done when")}</h5><ul><li>${t("Os testes do <code>CupomService</code> cobrem os três casos.", "The <code>CupomService</code> tests cover the three cases.")}</li></ul></div>
          <div class="d-coment"><span class="d-av p">AN</span><div><b>ana</b> <span class="d-suave">${t("há 1 dia", "1 day ago")}</span><p>${t("O limite por cliente pode ficar para uma issue separada, se apertar.", "The per-customer limit can go to a separate issue if needed.")}</p></div></div>
          <div class="d-campo">${t("Comentar…", "Comment…")}</div>
        </div><div class="d-issue-lado">
          <span class="d-botao claro">${ic("worktree")}${t("Começar num worktree", "Start in a worktree")}</span>
          <span class="d-botao">${ic("conversa")}${t("Sessão nova", "New session")}</span>
          <span class="d-botao">${ic("vinculo")}${t("Copiar o link", "Copy link")}</span>
          <dl><dt>${t("Responsável", "Assignee")}</dt><dd><span class="d-av p">VI</span>vini</dd><dt>${t("Prioridade", "Priority")}</dt><dd>P1</dd><dt>${t("Tamanho", "Size")}</dt><dd>M</dd><dt>${t("Iteração", "Iteration")}</dt><dd>Sprint 12</dd><dt>PR</dt><dd>${t("nenhum ainda", "none yet")}</dd></dl>
        </div></div>`,
    }),

    agendados: () => moldura({
      ativo: "agendados",
      abas: ABA_CUPOM(false),
      corpo: `<div class="d-pag">${cab(t("Agendados", "Scheduled"), t("Pedidos que rodam sozinhos no horário, cada um numa sessão nova no quadro.", "Requests that run on their own at a set time, each in a new session on the board."))}
        <div class="d-agenda"><div class="d-lista-ag">
          ${agendado(t("Revisar os PRs abertos", "Review the open PRs"), "Loja", t("seg a sex · 08:30", "Mon–Fri · 08:30"), t("próximo: amanhã às 08:30", "next: tomorrow at 08:30"), true)}
          ${agendado(t("Atualizar as dependências", "Update the dependencies"), "Loja.Api", t("segunda · 09:00", "Monday · 09:00"), t("próximo: segunda às 09:00", "next: Monday at 09:00"), true)}
          ${agendado(t("Resumo dos erros da semana", "Weekly error summary"), "App.Notas", t("sexta · 17:00", "Friday · 17:00"), t("pausado", "paused"), false)}
        </div><div class="d-form">
          <h4>${t("Novo agendado", "New schedule")}</h4>
          <label>${t("Projeto", "Project")}</label><div class="d-campo sel">Loja ${ic("abaixo")}</div>
          <label>${t("Pedido", "Request")}</label><div class="d-campo alto">${t("Rode os testes e me diga quais falharam desde ontem.", "Run the tests and tell me which ones failed since yesterday.")}</div>
          <label>${t("Hora e dias", "Time and days")}</label><div class="d-dias"><span class="d-campo hora">18:00</span>${(EN ? ["M", "T", "W", "T", "F", "S", "S"] : ["S", "T", "Q", "Q", "S", "S", "D"]).map((d, i) => `<span class="${i < 5 ? "ativo" : ""}">${d}</span>`).join("")}</div>
          <span class="d-botao claro">${t("Agendar", "Schedule")}</span>
        </div></div></div>`,
    }),

    abertura: () => moldura({
      semTrilho: true,
      abas: "",
      corpo: `<div class="d-abertura"><div class="d-passos">${[t("Agente", "Agent"), t("Conta", "Account"), t("Projetos", "Projects"), "GitHub"].map((n, i) => `<span class="${i < 2 ? "feito" : i === 2 ? "agora" : ""}"><i>${i < 2 ? ic("ok") : i + 1}</i>${n}</span>`).join("")}</div>
        <h3>${t("Seus projetos", "Your projects")}</h3>
        <p class="d-suave">${t("Achamos estes no VS Code, no Cursor e no Windsurf. Marque os que quer trazer.", "We found these in VS Code, Cursor and Windsurf. Check the ones to bring in.")}</p>
        <div class="d-vs">${[["loja.code-workspace", "D:\\Projetos\\Loja", "VS Code", true], ["Loja.Api", "D:\\Projetos\\Loja.Api", "VS Code", true], ["App.Notas", "D:\\Projetos\\App.Notas", "Cursor", true], ["site", "D:\\Projetos\\Site", "Windsurf", false]].map(([n, c, ed, ok]) => `<div class="d-vsl"><span class="d-cx${ok ? " ok" : ""}">${ic("ok")}</span><div><b>${n}</b><small>${c}</small></div><span class="d-ed">${ed}</span></div>`).join("")}</div>
        <div class="d-fim"><span class="d-botao">${t("Pular", "Skip")}</span><span class="d-botao claro">${t("Trazer 3 projetos", "Bring 3 projects")}</span></div></div>`,
    }),
  };

  function agendado(titulo, projeto, quando, proximo, ligado) {
    return `<div class="d-ag${ligado ? "" : " pausado"}"><span class="d-ico">${ic("relogio")}</span><div><b>${titulo}</b><small>${projeto} · ${quando}</small><small class="prox">${proximo}</small></div><span class="d-chave${ligado ? " ligada" : ""}"><i></i></span></div>`;
  }

  function issues(modo) {
    const et = (n, c) => `<span class="d-et" style="--c:${c}">${n}</span>`;
    const F = et("feature", "#7CC8E8"), B = et("bug", "#E2735F"), D = et(t("docs", "docs"), "#C9C6BF"), C = et("checkout", "#E59AC4");
    const ISSUES = [
      ["Todo", "var(--t-texto-fraco)", [[52, t("Exportar pedidos em CSV", "Export orders as CSV"), F, "AN", "P2", "S", ""], [55, t("Senha esquecida não envia e-mail", "Forgot password sends no email"), B, "VI", "P1", "S", ""], [57, t("Guia de deploy no README", "Deploy guide in the README"), D, "", "P3", "XS", ""]]],
      ["In progress", "var(--t-laranja)", [[48, t("Cupom de desconto no checkout", "Discount coupon at checkout"), F + C, "VI", "P1", "M", ""], [50, t("Erro na tela de pedidos", "Orders page error"), B, "AN", "P1", "S", ""]]],
      ["In review", "var(--t-ciano)", [[46, t("Notas offline no app", "Offline notes in the app"), F, "VI", "P2", "L", "#51"]]],
      ["Done", "var(--t-verde)", [[44, t("Login com Google", "Google sign-in"), F, "AN", "P2", "M", "#47"], [41, t("Timeout no relatório", "Report timeout"), B, "VI", "P1", "S", "#45"]]],
    ];
    const av = (s) => (s ? `<span class="d-av p">${s}</span>` : `<span class="d-suave">—</span>`);
    let miolo;
    if (modo === 0) {
      miolo = `<div class="d-colunas">${ISSUES.map(([st, cor, lista]) => coluna(st, cor, lista.map(([n, titulo, ets, quem, pri, tam, pr]) => `<div class="d-ct d-is"><div class="d-n">#${n}</div><div class="t2">${titulo}</div><div class="d-chips">${ets}</div><div class="d-meta">${av(quem)}<span>${pri}</span><span>${tam}</span>${pr ? `<span class="pr">${ic("branch")}${pr}</span>` : ""}</div></div>`))).join("")}</div>`;
    } else {
      miolo = `<div class="d-tabela"><div class="d-th"><span>${t("Título", "Title")}</span><span>${t("Responsável", "Assignee")}</span><span>${t("Etiquetas", "Labels")}</span><span>${t("Prioridade", "Priority")}</span><span>${t("Tamanho", "Size")}</span><span>PR</span></div>${ISSUES.map(([st, cor, lista]) => `<div class="d-grupo"><i style="background:${cor}"></i>${st}<small>${lista.length}</small></div>${lista.map(([n, titulo, ets, quem, pri, tam, pr]) => `<div class="d-tr"><span><em>#${n}</em>${titulo}</span><span>${av(quem)}</span><span class="d-chips">${ets}</span><span>${pri}</span><span>${tam}</span><span>${pr ? `<span class="pr">${ic("branch")}${pr}</span>` : `<span class="d-suave">—</span>`}</span></div>`).join("")}`).join("")}</div>`;
    }
    return moldura({
      ativo: "issues",
      abas: ABA_CUPOM(false),
      corpo: `<div class="d-pag">${cab("Issues", t("Loja · Projeto Loja 2026 · 8 abertas", "Loja · Loja 2026 project · 8 open"), seg([["quadro", "Kanban"], ["lista", t("Lista", "List")]], modo) + busca(t("Filtrar issues", "Filter issues")) + `<span class="d-botao claro">${ic("mais")}${t("Nova issue", "New issue")}</span>`)}${miolo}</div>`,
    });
  }

  let semente = 11;
  const rnd = () => (semente = (semente * 16807) % 2147483647) / 2147483647;
  function ceu() {
    semente = 11;
    const nos = Array.from({ length: 46 }, () => [rnd() * 1280, rnd() * 800, rnd() < 0.25]);
    let linhas = "", pontos = "";
    for (let i = 0; i < nos.length; i++) for (let j = i + 1; j < nos.length; j++) {
      const d = Math.hypot(nos[i][0] - nos[j][0], nos[i][1] - nos[j][1]);
      if (d < 170) linhas += `<line x1="${nos[i][0].toFixed(0)}" y1="${nos[i][1].toFixed(0)}" x2="${nos[j][0].toFixed(0)}" y2="${nos[j][1].toFixed(0)}" stroke-opacity="${((1 - d / 170) * 0.22).toFixed(2)}"/>`;
    }
    for (const [x, y, anel] of nos) pontos += anel ? `<circle cx="${x.toFixed(0)}" cy="${y.toFixed(0)}" r="4.5" fill="none" stroke="#F2F0EC" stroke-opacity=".4" stroke-width="1.5"/>` : `<circle cx="${x.toFixed(0)}" cy="${y.toFixed(0)}" r="2.2" fill="#D9D4E6" fill-opacity=".4"/>`;
    return `<svg class="d-ceu" viewBox="0 0 1280 800" preserveAspectRatio="xMidYMid slice"><g stroke="#948BAE" stroke-width="1">${linhas}</g>${pontos}</svg>`;
  }

  const NOMES = [["Loja-pedidos", "Store-orders"], ["Loja", "Store"], [">LO<", ">ST<"], [">LP<", ">SO<"], ["cupom-checkout", "coupon-checkout"], ["erro-tela", "orders-bug"], ["notas-offline", "offline-notes"], ["App.Notas", "App.Notes"], ["Site · relatorio", "Site · monthly-report"], ["rate-limit", "rate-limit"], ["D:\Projetos", "D:\Projects"], ["Cupom.razor", "Coupon.razor"]];
  const fundo = ceu();
  const caixas = new Set();
  function desenhar(caixa) {
    const fazer = TELAS[caixa.dataset.tela];
    if (!fazer) return;
    const { classe, html } = fazer();
    const texto = EN ? NOMES.reduce((h, [de, para]) => h.split(de).join(para), html) : html;
    caixa.innerHTML = `${fundo}<div class="tela tela-d${classe}" aria-hidden="true">${texto}</div>`;
    encaixar(caixa);
  }
  function encaixar(caixa) {
    const tela = caixa.querySelector(".tela-d");
    if (tela) tela.style.transform = `scale(${caixa.clientWidth / 1280})`;
  }
  const obs = new ResizeObserver((e) => e.forEach((x) => encaixar(x.target)));
  document.querySelectorAll(".desenho[data-tela]").forEach((c) => { desenhar(c); obs.observe(c); caixas.add(c); });
  window.desenharTela = desenhar;
  document.querySelectorAll("[data-lat]").forEach((alvo) => {
    const html = trilho(null, true) + lateral(true);
    const texto = EN ? NOMES.reduce((h, [de, para]) => h.split(de).join(para), html) : html;
    const molde = document.createElement("template");
    molde.innerHTML = texto.trim();
    alvo.replaceWith(...molde.content.childNodes);
  });
})();
