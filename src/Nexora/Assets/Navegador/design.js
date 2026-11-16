(() => {
  if (window.__nexoraDesign) return;

  const enviar = (m) => {
    try { window.chrome.webview.postMessage(JSON.stringify(m)); } catch (e) { }
  };

  const css = `
    :host { all: initial; }
    .caixa { position: fixed; display: none; border: 2px solid var(--cor); background: color-mix(in srgb, var(--cor) 14%, transparent); border-radius: 3px; pointer-events: none; box-sizing: border-box; }
    .rotulo { position: fixed; display: none; max-width: 420px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; background: #1c1928; color: #f1e9dc; font: 11px/1.5 ui-monospace, Consolas, monospace; padding: 2px 7px; border-radius: 5px; pointer-events: none; box-shadow: 0 2px 8px rgba(0,0,0,.35); }
    .contorno { position: fixed; border: 2px dashed var(--cor); border-radius: 3px; pointer-events: none; box-sizing: border-box; }
    .marcador { position: fixed; min-width: 22px; height: 22px; padding: 0 6px; box-sizing: border-box; border-radius: 11px; background: var(--cor); color: #1b1630; font: 700 12px/22px system-ui, "Segoe UI", sans-serif; text-align: center; box-shadow: 0 2px 8px rgba(0,0,0,.4), 0 0 0 2px #fff; pointer-events: auto; cursor: pointer; user-select: none; }
    .dica { position: fixed; left: 50%; bottom: 12px; transform: translateX(-50%); display: none; background: #1c1928; color: #f1e9dc; font: 12.5px/1.4 system-ui, "Segoe UI", sans-serif; padding: 7px 14px; border-radius: 999px; box-shadow: 0 4px 16px rgba(0,0,0,.35); pointer-events: none; white-space: nowrap; }
    .dica b { color: var(--cor); font-weight: 600; }
    .editor { position: fixed; display: none; width: 290px; box-sizing: border-box; background: #1c1928; color: #f1e9dc; border: 1px solid #3a3550; border-radius: 10px; padding: 10px; box-shadow: 0 10px 32px rgba(0,0,0,.45); pointer-events: auto; font: 13px/1.4 system-ui, "Segoe UI", sans-serif; }
    .alvo { font: 11px/1.4 ui-monospace, Consolas, monospace; color: #a59fb8; margin-bottom: 6px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    textarea { display: block; width: 100%; box-sizing: border-box; min-height: 66px; resize: vertical; background: #13111b; color: inherit; border: 1px solid #3a3550; border-radius: 6px; padding: 6px 8px; font: inherit; outline: none; }
    textarea:focus { border-color: var(--cor); }
    .botoes { display: flex; gap: 6px; margin-top: 8px; }
    .botoes .espaco { flex: 1; }
    button { font: 12px system-ui, "Segoe UI", sans-serif; border-radius: 6px; padding: 4px 10px; border: 1px solid #3a3550; background: transparent; color: inherit; cursor: pointer; }
    button:hover { background: #2a2639; }
    button.principal { background: var(--cor); color: #1b1630; border-color: var(--cor); font-weight: 600; }
    button.remover { color: #ff8f8f; }
    button[hidden] { display: none; }
  `;

  let textos = { comentar: "O que mudar aqui?", salvar: "Salvar", cancelar: "Cancelar", remover: "Remover", dica: "Clique num elemento para comentar", sair: "Esc sai" };
  let cor = "#b9a6f2";
  let host = null, raiz = null, caixa, rotulo, dica, editor, campo, alvoTexto, botaoRemover;
  let ligado = false, foto = false, alvo = null, editando = null, proximo = 1, quadro = 0;
  const marcadores = new Map();

  function montar() {
    if (host && host.isConnected) return;
    host = document.createElement("nexora-design");
    host.style.setProperty("all", "initial");
    host.style.setProperty("position", "fixed");
    host.style.setProperty("inset", "0");
    host.style.setProperty("z-index", "2147483647");
    host.style.setProperty("pointer-events", "none");
    raiz = host.attachShadow({ mode: "open" });
    const folha = new CSSStyleSheet();
    folha.replaceSync(css);
    raiz.adoptedStyleSheets = [folha];
    caixa = criar("div", "caixa");
    rotulo = criar("div", "rotulo");
    dica = criar("div", "dica");
    editor = criar("div", "editor");
    alvoTexto = criar("div", "alvo", editor);
    campo = criar("textarea", "", editor);
    const botoes = criar("div", "botoes", editor);
    botaoRemover = criar("button", "remover", botoes);
    criar("div", "espaco", botoes);
    const cancelar = criar("button", "", botoes);
    const salvar = criar("button", "principal", botoes);
    botaoRemover.type = cancelar.type = salvar.type = "button";
    botaoRemover.addEventListener("click", (e) => { parar(e); remover(editando && editando.id, true); fecharEditor(); });
    cancelar.addEventListener("click", (e) => { parar(e); fecharEditor(); });
    salvar.addEventListener("click", (e) => { parar(e); salvarEditor(); });
    campo.addEventListener("keydown", (e) => {
      e.stopPropagation();
      if (e.isComposing || e.keyCode === 229) return;
      if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); salvarEditor(); }
      else if (e.key === "Escape") { e.preventDefault(); fecharEditor(); }
    });
    for (const tipo of ["keyup", "keypress", "input", "beforeinput", "compositionstart", "compositionend"]) campo.addEventListener(tipo, (e) => e.stopPropagation());
    host.style.setProperty("--cor", cor);
    aplicarTextos();
    (document.body || document.documentElement).appendChild(host);
    for (const m of marcadores.values()) raiz.append(m.contorno, m.no);
  }

  function criar(tag, classe, pai) {
    const el = document.createElement(tag);
    if (classe) el.className = classe;
    (pai || raiz).appendChild(el);
    return el;
  }

  function aplicarTextos() {
    if (!raiz) return;
    campo.placeholder = textos.comentar;
    botaoRemover.textContent = textos.remover;
    editor.querySelector(".botoes button:not(.remover):not(.principal)").textContent = textos.cancelar;
    editor.querySelector("button.principal").textContent = textos.salvar;
    dica.textContent = "";
    dica.append(textos.dica + "  ·  ");
    const b = document.createElement("b");
    b.textContent = textos.sair;
    dica.append(b);
  }

  function parar(e) {
    e.preventDefault();
    e.stopPropagation();
    if (e.stopImmediatePropagation) e.stopImmediatePropagation();
  }

  const doHost = (e) => host && e.composedPath().includes(host);

  function elementoEm(e) {
    const caminho = e.composedPath();
    const el = caminho.find((n) => n instanceof Element);
    if (!el || el === document.documentElement) return null;
    return el;
  }

  function unico(s) {
    try { return document.querySelectorAll(s).length === 1; } catch (e) { return false; }
  }

  function seletor(el) {
    if (el.id && unico("#" + CSS.escape(el.id))) return "#" + CSS.escape(el.id);
    for (const atributo of ["data-testid", "data-test", "data-cy", "name", "aria-label"]) {
      const v = el.getAttribute(atributo);
      if (v && v.length < 60) {
        const s = `${el.tagName.toLowerCase()}[${atributo}="${CSS.escape(v)}"]`;
        if (unico(s)) return s;
      }
    }
    const partes = [];
    let atual = el;
    while (atual && atual.nodeType === 1 && atual !== document.documentElement) {
      if (atual !== el && atual.id && unico("#" + CSS.escape(atual.id))) {
        partes.unshift("#" + CSS.escape(atual.id));
        return partes.join(" > ");
      }
      let parte = atual.tagName.toLowerCase();
      const classes = [...atual.classList]
        .filter((c) => /^[a-zA-Z][\w-]{0,40}$/.test(c) && !/^(css|sc|jsx|svelte|astro|ng|v)-/.test(c) && !/\d{3,}/.test(c))
        .slice(0, 2);
      if (classes.length) parte += "." + classes.map((c) => CSS.escape(c)).join(".");
      const pai = atual.parentElement;
      if (pai) {
        const iguais = [...pai.children].filter((f) => f.tagName === atual.tagName);
        const doMesmoJeito = [...pai.children].filter((f) => { try { return f.matches(parte); } catch (e) { return false; } });
        if (iguais.length > 1 && doMesmoJeito.length > 1) parte += `:nth-of-type(${iguais.indexOf(atual) + 1})`;
      }
      partes.unshift(parte);
      const s = partes.join(" > ");
      if (unico(s)) return s;
      atual = pai;
    }
    return partes.join(" > ");
  }

  function resumoHtml(el) {
    const html = el.outerHTML || "";
    if (html.length <= 400) return html;
    const raso = el.cloneNode(false).outerHTML;
    const fim = raso.lastIndexOf("</");
    const abre = fim > 0 ? raso.slice(0, fim) : raso;
    const fecha = fim > 0 ? raso.slice(fim) : "";
    const texto = (el.innerText || "").replace(/\s+/g, " ").trim().slice(0, 120);
    return (abre.length > 380 ? abre.slice(0, 380) + "…>" : abre) + (texto ? texto + "…" : "…") + fecha;
  }

  function descrever(el) {
    const r = el.getBoundingClientRect();
    const texto = el.innerText || el.value || el.getAttribute("aria-label") || el.getAttribute("alt") || el.getAttribute("title") || el.getAttribute("placeholder") || "";
    return {
      seletor: seletor(el),
      tag: el.tagName.toLowerCase(),
      texto: String(texto).slice(0, 400),
      html: resumoHtml(el),
      x: Math.round(r.left + window.scrollX),
      y: Math.round(r.top + window.scrollY),
      largura: Math.round(r.width),
      altura: Math.round(r.height),
      larguraJanela: window.innerWidth,
      alturaJanela: window.innerHeight,
      url: location.href,
    };
  }

  function posicionar(no, r) {
    no.style.left = r.left + "px";
    no.style.top = r.top + "px";
    no.style.width = Math.max(0, r.width) + "px";
    no.style.height = Math.max(0, r.height) + "px";
  }

  function desenharAlvo() {
    if (!raiz) return;
    const mostrar = ligado && !foto && !editando && alvo && alvo.isConnected;
    caixa.style.display = rotulo.style.display = mostrar ? "block" : "none";
    if (!mostrar) return;
    const r = alvo.getBoundingClientRect();
    posicionar(caixa, r);
    const tag = alvo.tagName.toLowerCase();
    const classe = [...alvo.classList].slice(0, 2).map((c) => "." + c).join("");
    rotulo.textContent = `${tag}${alvo.id ? "#" + alvo.id : classe}  ${Math.round(r.width)}×${Math.round(r.height)}`;
    rotulo.style.left = Math.max(4, Math.min(r.left, window.innerWidth - 220)) + "px";
    rotulo.style.top = (r.top > 26 ? r.top - 24 : Math.min(window.innerHeight - 24, r.bottom + 4)) + "px";
  }

  function desenharMarcadores() {
    let n = 1;
    for (const m of marcadores.values()) {
      m.no.textContent = String(n++);
      const vivo = m.el && m.el.isConnected;
      m.no.style.display = m.contorno.style.display = vivo ? "block" : "none";
      if (!vivo) continue;
      const r = m.el.getBoundingClientRect();
      posicionar(m.contorno, r);
      m.no.style.width = m.no.style.height = "";
      m.no.style.left = Math.max(2, Math.min(window.innerWidth - 26, r.left - 10)) + "px";
      m.no.style.top = Math.max(2, Math.min(window.innerHeight - 26, r.top - 10)) + "px";
      m.contorno.style.display = foto || ligado ? "block" : "none";
    }
  }

  function laco() {
    quadro = 0;
    desenharAlvo();
    desenharMarcadores();
    if (editando && editando.el && editando.el.isConnected) posicionarEditor(editando.el);
    if (ligado || marcadores.size > 0) quadro = requestAnimationFrame(laco);
  }

  function animar() {
    if (!quadro) quadro = requestAnimationFrame(laco);
  }

  function posicionarEditor(el) {
    const r = el.getBoundingClientRect();
    const largura = 290, altura = editor.offsetHeight || 140;
    let x = Math.min(Math.max(8, r.left), window.innerWidth - largura - 8);
    let y = r.bottom + 8;
    if (y + altura > window.innerHeight - 8) y = Math.max(8, r.top - altura - 8);
    editor.style.left = x + "px";
    editor.style.top = y + "px";
  }

  function abrirEditor(el, id) {
    montar();
    const existente = id != null ? marcadores.get(id) : null;
    editando = { el, id: existente ? id : null };
    alvoTexto.textContent = seletor(el);
    campo.value = existente ? existente.comentario : "";
    botaoRemover.hidden = !existente;
    editor.style.display = "block";
    posicionarEditor(el);
    desenharAlvo();
    setTimeout(() => campo.focus(), 0);
    animar();
  }

  function fecharEditor() {
    if (!raiz) return;
    editor.style.display = "none";
    editando = null;
    animar();
  }

  function marcar(id, el, comentario) {
    let m = marcadores.get(id);
    if (!m) {
      const no = document.createElement("div");
      no.className = "marcador";
      const contorno = document.createElement("div");
      contorno.className = "contorno";
      raiz.append(contorno, no);
      no.addEventListener("click", (e) => { parar(e); if (m.el && m.el.isConnected) abrirEditor(m.el, id); });
      m = { id, no, contorno };
      marcadores.set(id, m);
    }
    m.el = el;
    m.comentario = comentario;
    m.no.title = comentario;
    proximo = Math.max(proximo, id + 1);
  }

  function salvarEditor() {
    if (!editando) return;
    const comentario = campo.value.trim();
    const { el, id } = editando;
    fecharEditor();
    if (!comentario) return;
    const novo = id == null ? proximo++ : id;
    marcar(novo, el, comentario);
    enviar(Object.assign({ t: "anotacao", id: novo, comentario }, descrever(el)));
    animar();
  }

  function remover(id, avisar) {
    const m = marcadores.get(id);
    if (!m) return;
    m.no.remove();
    m.contorno.remove();
    marcadores.delete(id);
    if (avisar) enviar({ t: "removida", id });
    animar();
  }

  function sair() {
    desligar();
    enviar({ t: "sair" });
  }

  function aoMover(e) {
    if (!ligado || editando) return;
    if (doHost(e)) { if (alvo) { alvo = null; desenharAlvo(); } return; }
    const el = elementoEm(e);
    if (el !== alvo) { alvo = el; desenharAlvo(); }
  }

  function aoClicar(e) {
    if (!ligado || doHost(e)) return;
    parar(e);
    if (e.type !== "click" || editando) return;
    const el = elementoEm(e) || alvo;
    if (el) abrirEditor(el, null);
  }

  function aoTeclar(e) {
    if (!ligado && !editando) return;
    if (e.key !== "Escape") return;
    if (e.isComposing) return;
    parar(e);
    if (editando) fecharEditor();
    else sair();
  }

  function aoSairDaJanela() {
    alvo = null;
    desenharAlvo();
  }

  const bloqueados = ["pointerdown", "mousedown", "pointerup", "mouseup", "click", "dblclick", "auxclick", "contextmenu", "submit"];

  function ligar(opcoes) {
    if (opcoes) {
      if (opcoes.textos) textos = Object.assign(textos, opcoes.textos);
      if (opcoes.cor) cor = opcoes.cor;
    }
    montar();
    host.style.setProperty("--cor", cor);
    aplicarTextos();
    if (!ligado) {
      ligado = true;
      window.addEventListener("pointermove", aoMover, true);
      window.addEventListener("keydown", aoTeclar, true);
      document.addEventListener("mouseleave", aoSairDaJanela, true);
      for (const t of bloqueados) window.addEventListener(t, aoClicar, true);
    }
    dica.style.display = foto ? "none" : "block";
    document.documentElement.style.cursor = "crosshair";
    animar();
    return true;
  }

  function desligar() {
    ligado = false;
    window.removeEventListener("pointermove", aoMover, true);
    window.removeEventListener("keydown", aoTeclar, true);
    document.removeEventListener("mouseleave", aoSairDaJanela, true);
    for (const t of bloqueados) window.removeEventListener(t, aoClicar, true);
    document.documentElement.style.cursor = "";
    alvo = null;
    if (raiz) {
      fecharEditor();
      dica.style.display = "none";
      desenharAlvo();
    }
    animar();
    return true;
  }

  function restaurar(lista) {
    if (!Array.isArray(lista) || lista.length === 0) return 0;
    montar();
    let achados = 0;
    for (const a of lista) {
      let el = null;
      try { el = document.querySelector(a.seletor); } catch (e) { }
      if (!el) continue;
      marcar(a.id, el, a.comentario || "");
      achados++;
    }
    animar();
    return achados;
  }

  function limpar() {
    for (const id of [...marcadores.keys()]) remover(id, false);
    proximo = 1;
    return true;
  }

  function prepararFoto(ligada) {
    foto = !!ligada;
    if (raiz) {
      dica.style.display = !foto && ligado ? "block" : "none";
      editor.style.display = !foto && editando ? "block" : "none";
    }
    desenharAlvo();
    desenharMarcadores();
    return true;
  }

  function anotar(s, comentario) {
    let el = null;
    try { el = document.querySelector(s); } catch (e) { }
    if (!el) return false;
    montar();
    const id = proximo++;
    marcar(id, el, comentario);
    enviar(Object.assign({ t: "anotacao", id, comentario }, descrever(el)));
    animar();
    return true;
  }

  function apontar(s, texto) {
    let el = null;
    try { el = document.querySelector(s); } catch (e) { }
    if (!el) return false;
    if (texto == null) { alvo = el; desenharAlvo(); return true; }
    abrirEditor(el, null);
    campo.value = texto;
    return true;
  }

  window.__nexoraDesign = { ligar, desligar, restaurar, remover: (id) => { remover(id, false); return true; }, limpar, foto: prepararFoto, seletor, anotar, apontar };
})();
