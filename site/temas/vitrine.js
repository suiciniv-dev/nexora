const FUNDOS = [
  ["grafo", "Grafo", "Graph", "nós e estrelas à deriva", "nodes and stars drifting", true, "#BDB4DA", "#0E0D0C"],
  ["grafo-claro", "Grafo claro", "Light graph", "nós à deriva no papel", "nodes drifting on paper", false, "#5B5470", "#F6F4F1"],
  ["constelacao", "Constelação", "Constellation", "céu que gira devagar", "a slowly turning sky", true, "#A9B6F2", "#0C0D1E"],
  ["carta-celeste", "Carta celeste", "Star chart", "constelações a tinta", "constellations in ink", false, "#4A4E86", "#F6F3EA"],
  ["via-lactea", "Via Láctea", "Milky Way", "faixa de poeira estelar", "a band of stardust", true, "#CDBFEA", "#0C0B13"],
  ["nebulosa", "Nebulosa", "Nebula", "nuvens de cor à deriva", "clouds of color drifting", true, "#E59AC4", "#0E0B12"],
  ["pulso", "Pulso", "Pulse", "sinais entre estrelas", "signals between stars", true, "#86C5E6", "#0B0D12"],
  ["aurora", "Aurora", "Aurora", "cortinas de luz no céu", "curtains of light in the sky", true, "#7BCB9F", "#0A0F14"],
];

const CHAVES = ["fundo", "lateral", "superficie", "superficieAlta", "borda", "texto", "textoSuave", "textoFraco",
  "acento", "acentoHover", "sobreAcento", "rosa", "verde", "laranja", "vermelho", "ciano",
  "codigoFundo", "codigoTexto", "bolhaUsuario", "marca", "marcaFim"];

const NEXORA = [
  ["padrao", "Padrão", "Default", true, ["#181716", "#121110", "#24221F", "#2E2B28", "#24FFFFFF", "#F2F0EC", "#C4C0B9", "#9C978F",
    "#F2F0EC", "#FFFFFF", "#161514", "#E59AC4", "#7BCB9F", "#F0A860", "#E8806C", "#86C5E6", "#0F0E0D", "#E6C38F", "#332F2C", "#948BAE", "#5B5470"]],
  ["padrao-claro", "Padrão claro", "Default light", false, ["#F7F6F4", "#EFEDEA", "#FFFFFF", "#E9E6E2", "#26000000", "#1C1B1A", "#45423E", "#615D58",
    "#1C1B1A", "#000000", "#FFFFFF", "#A3477A", "#2F7D4F", "#A35E00", "#B8402F", "#1F6F9E", "#EFEDE9", "#8A4B12", "#ECE9E4", "#5B5470", "#3A3548"]],
  ["dracula", "Dracula", "Dracula", true, ["#282A36", "#21222C", "#2F3140", "#44475A", "#383A4C", "#F8F8F2", "#B9BDD6", "#8B93BF",
    "#BD93F9", "#CBA6FB", "#282A36", "#FF79C6", "#50FA7B", "#FFB86C", "#FF5555", "#8BE9FD", "#21222C", "#FF79C6", "#3A3C4E"]],
  ["alucard", "Alucard", "Alucard", false, ["#FFFBEB", "#F4EEDA", "#F8F3E1", "#E9E3CD", "#E0D9C0", "#1F1F1F", "#4F4B69", "#6C6690",
    "#644AC9", "#7560D3", "#FFFBEB", "#A3144D", "#14710A", "#A34D14", "#CB3A2A", "#036A96", "#F3EDD7", "#A3144D", "#E8E4F4"]],
];

const EN = document.documentElement.lang === "en";
const TEXTOS = EN
  ? {
      local: "en-US", decimal: ".", trabalhando: "Working", pronta: "Ready", animados: "Animated",
      pensando: (n) => `Thinking… · ${n} tokens`, pensou: (mil) => `Thought for 14 s · ${mil}k tokens`,
    }
  : {
      local: "pt-BR", decimal: ",", trabalhando: "Trabalhando", pronta: "Pronta", animados: "Animados",
      pensando: (n) => `Pensando… · ${n} tokens`, pensou: (mil) => `Pensou por 14 s · ${mil} mil tokens`,
    };

const GRUPOS = [
  { nome: "Nexora", ids: NEXORA.map((t) => t[0]) },
  { nome: TEXTOS.animados, ids: FUNDOS.map((f) => f[0]) },
];

const cor = (hex) => {
  const h = hex.slice(1);
  if (h.length === 8) return { a: parseInt(h.slice(0, 2), 16) / 255, r: parseInt(h.slice(2, 4), 16), g: parseInt(h.slice(4, 6), 16), b: parseInt(h.slice(6, 8), 16) };
  return { a: 1, r: parseInt(h.slice(0, 2), 16), g: parseInt(h.slice(2, 4), 16), b: parseInt(h.slice(4, 6), 16) };
};
const misturar = (x, y, k) => ({ r: Math.round(x.r + (y.r - x.r) * k), g: Math.round(x.g + (y.g - x.g) * k), b: Math.round(x.b + (y.b - x.b) * k), a: 1 });
const com = (c, a) => ({ ...c, a });
const luminancia = (c) => (0.299 * c.r + 0.587 * c.g + 0.114 * c.b) / 255;
const css = (c) => (c.a >= 1 ? `rgb(${c.r},${c.g},${c.b})` : `rgba(${c.r},${c.g},${c.b},${c.a.toFixed(3)})`);
const PRETO = cor("#000000"), BRANCO = cor("#FFFFFF");

function doFundo([, , , , , escuro, acentoHex, baseHex]) {
  const base = cor(baseHex), acento = cor(acentoHex);
  if (escuro) {
    const vidro = misturar(base, cor("#121110"), 0.55);
    return [
      misturar(vidro, PRETO, 0.25), com(vidro, 0.88), com(misturar(vidro, BRANCO, 0.05), 0.9), com(misturar(vidro, BRANCO, 0.11), 0.94),
      cor("#1FFFFFFF"), cor("#ECEBE7"), cor("#C9C6BF"), cor("#9C998F"),
      acento, misturar(acento, BRANCO, 0.18), luminancia(acento) > 0.55 ? cor("#141312") : BRANCO,
      cor("#E59AC4"), cor("#6CC79A"), cor("#F0A860"), cor("#E2735F"), cor("#7CC8E8"),
      com(misturar(vidro, PRETO, 0.35), 0.92), misturar(acento, BRANCO, 0.25), com(misturar(vidro, BRANCO, 0.14), 0.92),
      cor("#948BAE"), cor("#5B5470"),
    ];
  }
  const tinta = cor("#1E1B26");
  return [
    base, com(misturar(base, BRANCO, 0.75), 0.88), com(misturar(base, BRANCO, 0.85), 0.9), com(misturar(base, BRANCO, 0.95), 0.95),
    com(tinta, 0.14), cor("#1C1B1A"), cor("#45423E"), cor("#615D58"),
    acento, misturar(acento, PRETO, 0.12), BRANCO,
    cor("#A3477A"), cor("#2F7D4F"), cor("#A35E00"), cor("#B8402F"), cor("#1F6F9E"),
    com(misturar(base, tinta, 0.07), 0.94), misturar(acento, PRETO, 0.15), com(misturar(acento, BRANCO, 0.85), 0.92),
    cor("#5B5470"), cor("#3A3548"),
  ];
}

const TEMAS = new Map();
for (const [id, pt, en, escuro, hexes] of NEXORA) {
  const cores = hexes.map(cor);
  while (cores.length < CHAVES.length) cores.push(cores[8]);
  TEMAS.set(id, { id, nome: EN ? en : pt, ideia: "", escuro, cores, fundo: null, amostra: [hexes[0], hexes[19] ?? hexes[8]] });
}
for (const f of FUNDOS) {
  TEMAS.set(f[0], { id: f[0], nome: EN ? f[2] : f[1], ideia: EN ? f[4] : f[3], escuro: f[5], cores: doFundo(f), fundo: f[0], amostra: [f[7], f[6]] });
}

const janela = document.getElementById("janelaTema");
const tela = janela.querySelector(".tela");
const canvas = janela.querySelector("canvas");
const chips = document.getElementById("chipsTemas");
const abas = document.getElementById("abasTemas");
const parado = matchMedia("(prefers-reduced-motion: reduce)").matches;

const pedido = new URLSearchParams(location.search).get("tema");
let escolhido = TEMAS.has(pedido) ? pedido : "grafo";
let grupo = GRUPOS.findIndex((g) => g.ids.includes(escolhido));
let criarFundo = null, ctx = null, atual = null;
let bruto = null, mouse = null, ultimo = 0, rodando = false, visivel = false, fade = null;
let W = 0, H = 0, bw = 1280, bh = 800, esc = 1, inicioTurno = 0, trabalhando = true;

function montarAbas() {
  abas.innerHTML = "";
  GRUPOS.forEach((g, i) => {
    const b = document.createElement("button");
    b.type = "button";
    b.textContent = `${g.nome} `;
    const n = document.createElement("small");
    n.textContent = g.ids.length;
    b.appendChild(n);
    b.className = i === grupo ? "ativo" : "";
    b.setAttribute("aria-pressed", i === grupo);
    b.onclick = () => { grupo = i; montarAbas(); montarChips(); };
    abas.appendChild(b);
  });
}

function montarChips() {
  chips.innerHTML = "";
  for (const id of GRUPOS[grupo].ids) {
    const t = TEMAS.get(id);
    const b = document.createElement("button");
    b.type = "button";
    b.className = "chip-tema" + (id === escolhido ? " ativo" : "");
    b.setAttribute("aria-pressed", id === escolhido);
    if (t.ideia) b.title = t.ideia;
    const i = document.createElement("i");
    i.style.setProperty("--b", t.amostra[0]);
    i.style.setProperty("--a", t.amostra[1]);
    b.appendChild(i);
    b.appendChild(document.createTextNode(t.nome));
    b.onclick = () => usar(id);
    chips.appendChild(b);
  }
  const ativo = chips.querySelector(".ativo");
  if (ativo && chips.scrollWidth > chips.clientWidth)
    chips.scrollLeft += ativo.getBoundingClientRect().left - chips.getBoundingClientRect().left - 20;
}

function aplicarCores(tema) {
  CHAVES.forEach((k, i) => janela.style.setProperty(`--t-${k.replace(/[A-Z]/g, (m) => "-" + m.toLowerCase())}`, css(tema.cores[i])));
  const f = tema.cores[0];
  janela.style.setProperty("--t-fundo-cheio", `rgb(${f.r},${f.g},${f.b})`);
  janela.classList.toggle("claro", !tema.escuro);
}

function medir() {
  const largura = janela.clientWidth;
  const compacta = largura < 700;
  janela.classList.toggle("compacta", compacta);
  bw = compacta ? 520 : 1280;
  bh = compacta ? 720 : 800;
  janela.style.aspectRatio = `${bw} / ${bh}`;
  esc = largura / bw;
  tela.style.transform = `scale(${esc})`;
  W = largura;
  H = janela.clientHeight;
  if (!ctx) return;
  const dpr = Math.min(window.devicePixelRatio || 1, 1.5);
  const cw = Math.max(1, Math.round(W * dpr)), ch = Math.max(1, Math.round(H * dpr));
  if (canvas.width !== cw || canvas.height !== ch) {
    canvas.width = cw;
    canvas.height = ch;
  }
  if (parado || !rodando) desenharUmaVez();
}

function fotografar() {
  if (!canvas.width) return null;
  const img = document.createElement("canvas");
  img.width = canvas.width;
  img.height = canvas.height;
  img.getContext("2d").drawImage(canvas, 0, 0);
  return img;
}

function usar(id) {
  escolhido = id;
  montarChips();
  const tema = TEMAS.get(id);
  aplicarCores(tema);
  janela.classList.toggle("estatico", !tema.fundo);
  if (!ctx) return;
  const agora = performance.now() / 1000;
  fade = parado || !atual ? null : { img: fotografar(), t: agora };
  mouse = null;
  atual = { tema, motor: tema.fundo ? criarFundo(tema.fundo) : null };
  if (parado || !rodando) desenharUmaVez();
}

function pintarNexora(tema) {
  const [fundo, , , , , , , , acento] = tema.cores;
  const marca = tema.cores[19];
  ctx.globalAlpha = 1;
  ctx.globalCompositeOperation = "source-over";
  ctx.fillStyle = css({ ...fundo, a: 1 });
  ctx.fillRect(0, 0, bw, bh);
  const bolha = (x, y, r, c, a) => {
    const g = ctx.createRadialGradient(x, y, 0, x, y, r);
    g.addColorStop(0, `rgba(${c.r},${c.g},${c.b},${a})`);
    g.addColorStop(1, `rgba(${c.r},${c.g},${c.b},0)`);
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, bw, bh);
  };
  bolha(bw * 0.82, bh * 0.02, bw * 0.55, marca, tema.escuro ? 0.32 : 0.22);
  bolha(bw * 0.05, bh * 0.95, bw * 0.5, acento, tema.escuro ? 0.14 : 0.12);
}

function desenhar(t, dt, ponteiro) {
  ctx.setTransform(canvas.width / bw, 0, 0, canvas.height / bh, 0, 0);
  if (atual.motor) {
    atual.motor.agito(trabalhando);
    atual.motor.quadro(ctx, bw, bh, t, dt, ponteiro ? { x: ponteiro.x / esc, y: ponteiro.y / esc } : null);
  } else pintarNexora(atual.tema);
}

function pintar(t, dt) {
  if (!atual || !W) return;
  if (bruto) {
    if (!mouse) mouse = { x: bruto.x, y: bruto.y };
    const k = Math.min(1, dt * 14);
    mouse = { x: mouse.x + (bruto.x - mouse.x) * k, y: mouse.y + (bruto.y - mouse.y) * k };
  } else mouse = null;
  desenhar(t, dt, mouse);
  if (fade && fade.img) {
    const a = 1 - (t - fade.t) / 0.55;
    if (a <= 0) fade = null;
    else {
      ctx.setTransform(1, 0, 0, 1, 0, 0);
      ctx.globalAlpha = a;
      ctx.drawImage(fade.img, 0, 0);
      ctx.globalAlpha = 1;
    }
  }
}

function desenharUmaVez() {
  if (!ctx || !atual || !W) return;
  desenhar(parado ? 6 : performance.now() / 1000, 1 / 30, null);
}

const fmt = new Intl.NumberFormat(TEXTOS.local);
const pensando = janela.querySelector("[data-pensando]");
const pilula = janela.querySelector("[data-estado]");
let ultimoTexto = "";
function turno(agora) {
  const s = agora - inicioTurno;
  const eraTrabalhando = trabalhando;
  trabalhando = s < 14;
  if (s > 22) { inicioTurno = agora; trabalhando = true; }
  if (eraTrabalhando !== trabalhando) janela.classList.toggle("pronta", !trabalhando);
  const tokens = Math.round(Math.min(s, 14) * 187 + (s > 0.4 ? 40 : 0));
  const texto = trabalhando ? TEXTOS.pensando(fmt.format(tokens)) : TEXTOS.pensou((tokens / 1000).toFixed(1).replace(".", TEXTOS.decimal));
  if (texto !== ultimoTexto) { pensando.textContent = texto; ultimoTexto = texto; }
  if (pilula) pilula.textContent = trabalhando ? TEXTOS.trabalhando : TEXTOS.pronta;
}

function quadro(ms) {
  if (!rodando) return;
  requestAnimationFrame(quadro);
  const t = ms / 1000;
  if (!atual) return;
  const fps = atual.motor ? atual.motor.fps : 30;
  if (t - ultimo < 1 / fps - 0.004) return;
  const dt = ultimo ? Math.min(0.1, t - ultimo) : 1 / 30;
  ultimo = t;
  turno(t);
  pintar(t, dt);
}

function ligar() {
  const deve = visivel && !document.hidden && !parado && ctx;
  if (deve && !rodando) {
    rodando = true;
    ultimo = 0;
    if (!inicioTurno) inicioTurno = performance.now() / 1000;
    requestAnimationFrame(quadro);
  } else if (!deve) rodando = false;
}

janela.addEventListener("pointermove", (e) => {
  const r = janela.getBoundingClientRect();
  bruto = { x: e.clientX - r.left, y: e.clientY - r.top };
});
janela.addEventListener("pointerleave", () => { bruto = null; });
janela.addEventListener("click", (e) => {
  if (!ctx || !atual?.motor || e.target.closest("button")) return;
  const r = janela.getBoundingClientRect();
  atual.motor.clique((e.clientX - r.left) / esc, (e.clientY - r.top) / esc, performance.now() / 1000);
});

montarAbas();
montarChips();
aplicarCores(TEMAS.get(escolhido));
janela.classList.toggle("estatico", !TEMAS.get(escolhido).fundo);
requestAnimationFrame(() => requestAnimationFrame(() => janela.classList.add("anima")));
new ResizeObserver(medir).observe(janela);
document.addEventListener("visibilitychange", ligar);

let carregando = null;
async function carregar() {
  if (carregando) return carregando;
  carregando = (async () => {
    ({ criarFundo } = await import("./animados.js"));
    ctx = canvas.getContext("2d");
    medir();
    usar(escolhido);
    ligar();
  })();
  return carregando;
}

new IntersectionObserver((entradas) => {
  for (const e of entradas) {
    visivel = e.isIntersecting;
    if (visivel) carregar().then(ligar);
    else ligar();
  }
}, { rootMargin: "200px" }).observe(janela);
