import { avatarFrame } from 'obav/render.ts';
import { framePlacement } from 'obav/dom/paint.ts';
import { pointerGaze, followStep, followGaze, REST } from 'obav/follow.ts';
import catalogo from '../../src/Nexora/Assets/Avatares/catalogo.json';
import definicoes from 'obav-definicoes';

const MS_HUMOR = 420;
const reduzido = matchMedia('(prefers-reduced-motion: reduce)');
let ponteiro = null;
const vivos = new Set();
let quadro = 0;
let ultimo = 0;

function rgb(cor) {
  const c = cor.replace('#', '');
  return [0, 2, 4].map((i) => parseInt(c.slice(i, i + 2), 16));
}
const css = (c) => `rgb(${c.map((v) => Math.round(v)).join(',')})`;
const misturar = (a, b, t) => a.map((v, i) => v + (b[i] - v) * t);

function corDe(chave) {
  const [grupo, nome] = chave.split(':');
  const fonte = grupo === 'orquestrador' ? catalogo.orquestrador : catalogo[grupo]?.[nome] ?? catalogo.subagentes['*'];
  return { avatar: fonte.avatar, cor: fonte.cor };
}

function pontilhado(tam, cor, cheioNaDiagonal) {
  const tela = document.createElement('canvas');
  tela.width = tela.height = tam * 2;
  const ctx = tela.getContext('2d');
  ctx.fillStyle = cor;
  ctx.fillRect(cheioNaDiagonal ? tam : 0, 0, tam, tam);
  if (cheioNaDiagonal) ctx.fillRect(0, tam, tam, tam);
  return tela;
}

function olho(ctx, marca, borda) {
  if (marca.shape === 'arc') {
    ctx.lineCap = 'round';
    ctx.lineWidth = marca.strokeWidth;
    ctx.strokeStyle = borda;
    ctx.stroke(new Path2D(marca.d));
    return;
  }
  ctx.beginPath();
  ctx.roundRect(marca.x, marca.y, marca.width, marca.height, marca.radius);
  ctx.lineWidth = 2.2;
  ctx.strokeStyle = borda;
  ctx.stroke();
  ctx.fillStyle = '#FFFFFF';
  ctx.fill();
}

function pintar(a, q) {
  const { ctx, lado } = a;
  const base = rgb(a.cor);
  const escuro = css(base.map((v) => v * 0.38));
  const sombra = base.map((v) => v * 0.62);
  const luz = css(base.map((v) => v + (255 - v) * 0.45));
  const s = lado / 100;
  const { matrices } = framePlacement(q);
  const caminhos = new Map(q.paths.map((p) => [p.id, { p2: a.cache.get(p.d) ?? guardar(a, p.d), regra: p.evenOdd ? 'evenodd' : 'nonzero' }]));
  const n = q.shapes.filter((x) => x.kind === 'slice').length;
  ctx.setTransform(1, 0, 0, 1, 0, 0);
  ctx.clearRect(0, 0, lado, lado);
  ctx.lineJoin = 'round';
  const por = (m) => ctx.setTransform(s * m[0], s * m[1], s * m[2], s * m[3], s * m[4], s * m[5]);
  q.shapes.forEach((sh, i) => {
    if (sh.kind !== 'slice') return;
    por(matrices[i]);
    ctx.lineWidth = 5.5;
    ctx.strokeStyle = escuro;
    ctx.stroke(caminhos.get(sh.path).p2);
  });
  let k = 0;
  q.shapes.forEach((sh, i) => {
    por(matrices[i]);
    if (sh.kind !== 'slice') {
      olho(ctx, sh.mark, escuro);
      return;
    }
    const { p2, regra } = caminhos.get(sh.path);
    ctx.fillStyle = css(misturar(sombra, base, Math.pow(n > 1 ? k / (n - 1) : 1, 0.7)));
    ctx.fill(p2, regra);
    k++;
    if (k !== n) return;
    ctx.save();
    ctx.clip(p2, regra);
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    const celula = Math.max(1, Math.round(lado / 48));
    if (!a.padroes || a.padroes.celula !== celula) {
      a.padroes = { celula, luz: ctx.createPattern(pontilhado(celula, luz, false), 'repeat'), sombra: ctx.createPattern(pontilhado(celula, '#000', true), 'repeat') };
    }
    ctx.globalAlpha = 0.7;
    ctx.fillStyle = a.padroes.luz;
    ctx.fillRect(0, 0, lado * 0.62, lado * 0.5);
    ctx.globalAlpha = 0.24;
    ctx.fillStyle = a.padroes.sombra;
    ctx.fillRect(0, lado * 0.58, lado, lado * 0.42);
    ctx.globalAlpha = 1;
    ctx.restore();
  });
}

function guardar(a, d) {
  const p = new Path2D(d);
  a.cache.set(d, p);
  return p;
}

function medir(a) {
  const r = a.el.getBoundingClientRect();
  const lado = Math.max(1, Math.round(r.width * (devicePixelRatio || 1)));
  if (a.canvas.width !== lado) a.canvas.width = a.canvas.height = lado;
  a.lado = lado;
  a.caixa = r;
}

function desenhar(a, agora) {
  const dt = a.antes ? (agora - a.antes) / 1000 : 0;
  a.antes = agora;
  const parado = reduzido.matches;
  const alvo = ponteiro ? pointerGaze(ponteiro, a.caixa, a.origem) : null;
  a.olhar = followStep(a.olhar, alvo, dt, parado).state;
  const humor = a.feliz ? 'happy' : 'idle';
  if (humor !== a.humor) {
    a.de = a.humor;
    a.humor = humor;
    a.mudou = agora;
  }
  const t = parado ? 1 : Math.min(1, (agora - a.mudou) / MS_HUMOR);
  const q = avatarFrame(a.def, {
    color: a.cor,
    time: parado ? 0 : (agora - a.inicio) / 1000,
    motion: a.humor,
    motionBlend: t < 1 && a.de ? { from: a.de, t } : undefined,
    gaze: followGaze(a.olhar),
    reducedMotion: parado,
  });
  pintar(a, q);
}

function laco(agora) {
  quadro = 0;
  if (document.hidden) return;
  const passo = agora - ultimo;
  if (passo >= 1000 / 60 - 2) {
    ultimo = agora;
    for (const a of vivos) desenhar(a, agora);
  }
  if (vivos.size && !reduzido.matches) quadro = requestAnimationFrame(laco);
}

function pedir() {
  if (!quadro && vivos.size) quadro = requestAnimationFrame(laco);
}

function montar(el) {
  const { avatar, cor } = corDe(el.dataset.avatar);
  const def = definicoes[avatar];
  if (!def) return;
  const canvas = document.createElement('canvas');
  canvas.setAttribute('aria-hidden', 'true');
  el.append(canvas);
  const a = {
    el, canvas, ctx: canvas.getContext('2d'), def, cor, cache: new Map(), olhar: REST,
    humor: 'idle', de: null, mudou: 0, inicio: performance.now() - Math.random() * 4000, feliz: false,
    origem: { x: def.eyes.x / 100, y: def.eyes.y / 100 },
  };
  const cartao = el.closest('.bicho') ?? el;
  cartao.addEventListener('pointerenter', () => { a.feliz = true; pedir(); });
  cartao.addEventListener('pointerleave', () => { a.feliz = false; pedir(); });
  cartao.addEventListener('focusin', () => { a.feliz = true; pedir(); });
  cartao.addEventListener('focusout', () => { a.feliz = false; pedir(); });
  medir(a);
  desenhar(a, performance.now());
  return a;
}

const todos = [...document.querySelectorAll('[data-avatar]')].map(montar).filter(Boolean);
const porEl = new Map(todos.map((a) => [a.el, a]));
const observador = new IntersectionObserver((entradas) => {
  for (const e of entradas) {
    const a = porEl.get(e.target);
    if (!a) continue;
    if (e.isIntersecting) {
      medir(a);
      a.antes = 0;
      vivos.add(a);
    } else vivos.delete(a);
  }
  pedir();
});
for (const a of todos) observador.observe(a.el);
addEventListener('pointermove', (e) => {
  ponteiro = { x: e.clientX, y: e.clientY };
  if (reduzido.matches) for (const a of vivos) desenhar(a, performance.now());
}, { passive: true });
document.documentElement.addEventListener('pointerleave', () => { ponteiro = null; pedir(); });
addEventListener('scroll', () => { for (const a of vivos) a.caixa = a.el.getBoundingClientRect(); }, { passive: true });
addEventListener('resize', () => { for (const a of todos) medir(a); for (const a of todos) desenhar(a, performance.now()); });
document.addEventListener('visibilitychange', pedir);
reduzido.addEventListener('change', () => { for (const a of todos) desenhar(a, performance.now()); pedir(); });
