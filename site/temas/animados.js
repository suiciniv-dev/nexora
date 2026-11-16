const TAU = Math.PI * 2;
const VELOCIDADE_DA_ONDA = 440;
const VIDA_DA_ONDA = 2;
const RAIO_DO_MOUSE = 190;
const VIDA_DA_CADENTE = 1.1;
const ESCALA_DO_PANO = 0.25;
const NIVEIS = 32;
const LARGURAS = [1.35, 1.9, 2.7];
const PARALAXE = [0.45, 0.7, 1];

const limitar = (v) => (v < 0 ? 0 : v > 1 ? 1 : v);
const entre = (v, min, max) => (v < min ? min : v > max ? max : v);
const distancia = (dx, dy) => Math.sqrt(dx * dx + dy * dy);

function sorteio(semente) {
  let a = semente >>> 0;
  const f = () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
  f.inteiro = (n) => Math.floor(f() * n);
  return f;
}

function C(hex, alfa = 1) {
  const h = hex.slice(1);
  return { r: parseInt(h.slice(0, 2), 16), g: parseInt(h.slice(2, 4), 16), b: parseInt(h.slice(4, 6), 16), a: alfa };
}
const rgba = (c, mult = 1) => `rgba(${c.r},${c.g},${c.b},${limitar(c.a * mult).toFixed(3)})`;
const comAlfa = (c, a) => ({ r: c.r, g: c.g, b: c.b, a: limitar(a) * c.a });

const paleta = (escuro, fundo, fundoFim, luz, sombra, vinheta, linha, no, anel, destaque) => ({ escuro, fundo, fundoFim, luz, sombra, vinheta, linha, no, anel, destaque });

const PALETAS = {
  grafo: paleta(true, C("#0E0D0C"), C("#0A090B"), C("#948BAE", 0.22), C("#5B5470", 0.28), C("#000000", 0.55), C("#948BAE"), C("#D9D4E6"), C("#F2F0EC"), C("#BDB4DA")),
  "grafo-claro": paleta(false, C("#F6F4F1"), C("#ECE9F1"), C("#948BAE", 0.26), C("#BDB4DA", 0.4), C("#3A3548", 0.1), C("#5B5470"), C("#4A4460"), C("#3A3548"), C("#7A6FA0")),
  constelacao: paleta(true, C("#0E1024"), C("#06060E"), C("#8F96D8", 0.2), C("#5B5470", 0.3), C("#000000", 0.5), C("#A9B6F2"), C("#E4E6FF"), C("#F4F2FF"), C("#A9B6F2")),
  "carta-celeste": paleta(false, C("#F6F3EA"), C("#EDE8DC"), C("#A9B6F2", 0.24), C("#BDB4DA", 0.32), C("#3A3548", 0.12), C("#4A4E86"), C("#2A2E52"), C("#2A2E52"), C("#4A4E86")),
  "via-lactea": paleta(true, C("#0C0B13"), C("#07070B"), C("#948BAE", 0.18), C("#5B5470", 0.26), C("#000000", 0.55), C("#BDB4DA"), C("#EEE8F6"), C("#F2F0EC"), C("#D9C7F0")),
  nebulosa: paleta(true, C("#0E0B12"), C("#08070B"), C("#E59AC4", 0.12), C("#5B5470", 0.3), C("#000000", 0.55), C("#948BAE"), C("#F0E6F2"), C("#F2F0EC"), C("#E59AC4")),
  pulso: paleta(true, C("#0B0D12"), C("#08090C"), C("#948BAE", 0.2), C("#86C5E6", 0.08), C("#000000", 0.55), C("#948BAE"), C("#E0ECF4"), C("#F2F0EC"), C("#86C5E6")),
  aurora: paleta(true, C("#0A0F14"), C("#07090B"), C("#948BAE", 0.14), C("#5B5470", 0.24), C("#000000", 0.5), C("#9FB8C8"), C("#E6F2EE"), C("#F2F0EC"), C("#7BCB9F")),
};

class Lote {
  constructor() {
    this.niveis = Array.from({ length: NIVEIS + 1 }, () => []);
  }

  static nivel(a) {
    return a <= 0 ? 0 : a >= 1 ? NIVEIS : Math.round(a * NIVEIS);
  }

  linha(x1, y1, x2, y2, a) {
    const n = Lote.nivel(a);
    if (n) this.niveis[n].push(x1, y1, x2, y2);
  }

  ponto(x, y, a) {
    const n = Lote.nivel(a);
    if (n) this.niveis[n].push(x, y);
  }

  tracar(ctx, cor, largura) {
    ctx.lineWidth = largura;
    for (let n = 1; n <= NIVEIS; n++) {
      const l = this.niveis[n];
      if (!l.length) continue;
      ctx.strokeStyle = rgba(cor, n / NIVEIS);
      ctx.beginPath();
      for (let i = 0; i < l.length; i += 4) {
        ctx.moveTo(l[i], l[i + 1]);
        ctx.lineTo(l[i + 2], l[i + 3]);
      }
      ctx.stroke();
      l.length = 0;
    }
  }

  pintar(ctx, cor, diametro) {
    const r = diametro / 2;
    for (let n = 1; n <= NIVEIS; n++) {
      const l = this.niveis[n];
      if (!l.length) continue;
      ctx.fillStyle = rgba(cor, n / NIVEIS);
      ctx.beginPath();
      for (let i = 0; i < l.length; i += 2) {
        ctx.moveTo(l[i] + r, l[i + 1]);
        ctx.arc(l[i], l[i + 1], r, 0, TAU);
      }
      ctx.fill();
      l.length = 0;
    }
  }
}

function novaEstrela(sorte, x, y, camada = null) {
  let c = camada;
  if (c === null) {
    const v = sorte();
    c = v < 0.68 ? 0 : v < 0.92 ? 1 : 2;
  }
  const r = sorte();
  return {
    x, y, camada: c,
    brilho: c === 0 ? 0.42 + 0.3 * r : c === 1 ? 0.6 + 0.3 * r : 0.8 + 0.2 * r,
    fase: sorte() * TAU,
    freq: 0.45 + 1.9 * sorte(),
  };
}

function cintilar(e, t) {
  const s = 0.5 + 0.5 * Math.sin((t * e.freq) % TAU + e.fase);
  return e.brilho * (0.25 + 0.75 * s * s);
}

function brilho(c, cx, cy, rx, ry, giro, cor, corte) {
  c.save();
  c.translate(cx, cy);
  if (giro) c.rotate(giro);
  c.scale(1, ry / rx);
  const g = c.createRadialGradient(0, 0, 0, 0, 0, rx);
  g.addColorStop(0, rgba(cor));
  g.addColorStop(corte, rgba(cor, 0));
  c.fillStyle = g;
  c.fillRect(-rx, -rx, rx * 2, rx * 2);
  c.restore();
}

class Motor {
  constructor(p, semente) {
    this.P = p;
    this.semente = semente;
    this.sorte = sorteio(semente);
    this.W = 0;
    this.H = 0;
    this.wAntes = 0;
    this.hAntes = 0;
    this.atividade = 0;
    this.trabalhando = false;
    this.mouse = null;
    this.agora = 0;
    this.ondas = [];
    this.ceu = [new Lote(), new Lote(), new Lote()];
    this.halos = new Lote();
    this.proximaCadente = NaN;
    this.cadente = null;
    this.cadentes = null;
    this.intervaloDoPano = Infinity;
    this.panoTela = null;
    this.panoEm = NaN;
    this.fps = 24;
  }

  clique(x, y, t) {
    if (this.ondas.length >= 5) this.ondas.shift();
    this.ondas.push({ x, y, t });
    if (this.W > 0) this.aoClicar(x, y);
  }

  agito(trabalhando) {
    this.trabalhando = trabalhando;
  }

  quadro(ctx, largura, altura, t, dt, mouse) {
    if (largura !== this.W || altura !== this.H || !this.panoTela) {
      this.wAntes = this.W;
      this.hAntes = this.H;
      this.W = largura;
      this.H = altura;
      this.medir();
      this.panoEm = NaN;
    }
    this.agora = t;
    this.mouse = mouse;
    const passo = entre(dt, 0, 0.1);
    this.atividade += ((this.trabalhando ? 1 : 0) - this.atividade) * Math.min(1, passo * 0.8);
    this.ondas = this.ondas.filter((o) => t - o.t <= VIDA_DA_ONDA && o.t - t <= 1);
    this.avancar(passo);
    if (Number.isNaN(this.panoEm) || t - this.panoEm >= this.intervaloDoPano || t < this.panoEm) {
      this.gravarPano();
      this.panoEm = t;
    }
    ctx.globalAlpha = 1;
    ctx.globalCompositeOperation = "source-over";
    ctx.imageSmoothingEnabled = true;
    ctx.drawImage(this.panoTela, 0, 0, this.W, this.H);
    this.desenhar(ctx, t);
    this.desenharCadente(ctx, t);
    this.desenharOndas(ctx);
  }

  medir() {}
  avancar() {}
  desenhar() {}
  aoClicar() {}
  pano() {}

  perto(x, y) {
    const m = this.mouse;
    if (!m) return 0;
    const dx = x - m.x, dy = y - m.y, d2 = dx * dx + dy * dy;
    if (d2 >= RAIO_DO_MOUSE * RAIO_DO_MOUSE) return 0;
    const k = 1 - Math.sqrt(d2) / RAIO_DO_MOUSE;
    return k * k * (3 - 2 * k);
  }

  onda(x, y) {
    let forca = 0;
    for (const o of this.ondas) {
      const idade = this.agora - o.t;
      if (idade < 0) continue;
      const k = 1 - Math.abs(distancia(x - o.x, y - o.y) - idade * VELOCIDADE_DA_ONDA) / 56;
      if (k > 0) forca = Math.max(forca, k * (1 - idade / VIDA_DA_ONDA));
    }
    return forca;
  }

  realce(x, y) {
    return Math.max(this.perto(x, y), this.onda(x, y));
  }

  presenca(x, y, margem = 20) {
    return limitar(Math.min(Math.min(x + margem, this.W + margem - x), Math.min(y + margem, this.H + margem - y)) / 40);
  }

  fresca(salto) {
    return sorteio(this.semente * 7919 + salto);
  }

  semear(antigas, quantas) {
    const kx = this.wAntes > 0 ? this.W / this.wAntes : 1;
    const ky = this.hAntes > 0 ? this.H / this.hAntes : 1;
    const novas = new Array(quantas);
    for (let i = 0; i < quantas; i++) {
      if (i < antigas.length) novas[i] = { ...antigas[i], x: antigas[i].x * kx, y: antigas[i].y * ky };
      else {
        const x = this.sorte() * this.W;
        const y = this.sorte() * this.H;
        novas[i] = novaEstrela(this.sorte, x, y);
      }
    }
    return novas;
  }

  derivar(estrelas, dx, dy) {
    const { W, H } = this;
    for (const e of estrelas) {
      const k = PARALAXE[e.camada];
      e.x += dx * k;
      e.y += dy * k;
      if (e.x < -4) e.x += W + 8;
      else if (e.x > W + 4) e.x -= W + 8;
      if (e.y < -4) e.y += H + 8;
      else if (e.y > H + 4) e.y -= H + 8;
    }
  }

  desenharEstrelas(ctx, estrelas, t, halo = 1) {
    for (const e of estrelas) {
      const a = Math.min(1, cintilar(e, t) + 0.55 * this.realce(e.x, e.y));
      this.ceu[e.camada].ponto(e.x, e.y, a);
      if (e.camada === 2) this.halos.ponto(e.x, e.y, a);
    }
    this.halos.pintar(ctx, comAlfa(this.P.destaque, (this.P.escuro ? 0.16 : 0.07) * halo), 9);
    for (let c = 0; c < 3; c++) this.ceu[c].pintar(ctx, this.P.no, LARGURAS[c]);
  }

  anel(ctx, x, y, raio, cor, alfa, largura = 1.4) {
    if (alfa <= 0) return;
    ctx.lineWidth = largura;
    ctx.strokeStyle = rgba(comAlfa(cor, alfa));
    ctx.beginPath();
    ctx.arc(x, y, raio, 0, TAU);
    ctx.stroke();
  }

  ligarBrilhantes(lote, brilhantes, estrelas, alcance, forca, margem) {
    for (let i = 0; i < brilhantes.length; i++) {
      const a = estrelas[brilhantes[i]];
      for (let j = i + 1; j < brilhantes.length; j++) {
        const b = estrelas[brilhantes[j]];
        const dx = a.x - b.x, dy = a.y - b.y;
        if (Math.abs(dx) > alcance || Math.abs(dy) > alcance) continue;
        const d = distancia(dx, dy);
        if (d > alcance) continue;
        let alfa = (1 - d / alcance) * forca;
        if (margem !== null) alfa *= Math.min(this.presenca(a.x, a.y, margem), this.presenca(b.x, b.y, margem));
        alfa *= 1 + 1.5 * this.realce((a.x + b.x) / 2, (a.y + b.y) / 2);
        lote.linha(a.x, a.y, b.x, b.y, alfa);
      }
    }
  }

  desenharCadente(ctx, t) {
    const intervalo = this.cadentes;
    if (!intervalo) return;
    if (Number.isNaN(this.proximaCadente) || this.proximaCadente - t > intervalo[1] * 2) this.proximaCadente = t + intervalo[0] * (0.3 + this.sorte());
    if (!this.cadente && t >= this.proximaCadente) {
      const angulo = 0.38 + this.sorte() * 0.4;
      const lado = this.sorte.inteiro(2) === 0 ? -1 : 1;
      this.cadente = { x: this.W * (0.2 + 0.6 * this.sorte()), y: this.H * 0.32 * this.sorte(), dx: lado * Math.cos(angulo), dy: Math.sin(angulo), t };
      this.proximaCadente = t + (intervalo[0] + this.sorte() * (intervalo[1] - intervalo[0])) / (1 + 1.5 * this.atividade);
    }
    const c = this.cadente;
    if (!c) return;
    const idade = t - c.t;
    if (idade < 0 || idade > VIDA_DA_CADENTE) {
      this.cadente = null;
      return;
    }
    const andou = idade * 640;
    const cauda = Math.min(andou, 160);
    const alfa = Math.sin((Math.PI * idade) / VIDA_DA_CADENTE);
    const hx = c.x + c.dx * andou, hy = c.y + c.dy * andou, tx = hx - c.dx * cauda, ty = hy - c.dy * cauda;
    const g = ctx.createLinearGradient(tx, ty, hx, hy);
    g.addColorStop(0, rgba(this.P.anel, 0));
    g.addColorStop(1, rgba(this.P.anel, alfa * 0.85));
    ctx.strokeStyle = g;
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.moveTo(tx, ty);
    ctx.lineTo(hx, hy);
    ctx.stroke();
    ctx.fillStyle = rgba(this.P.anel, alfa);
    ctx.beginPath();
    ctx.arc(hx, hy, 1.3, 0, TAU);
    ctx.fill();
  }

  desenharOndas(ctx) {
    for (const o of this.ondas) {
      const idade = this.agora - o.t;
      if (idade < 0) continue;
      this.anel(ctx, o.x, o.y, idade * VELOCIDADE_DA_ONDA, this.P.destaque, (1 - idade / VIDA_DA_ONDA) * (this.P.escuro ? 0.28 : 0.36), 1.2);
    }
  }

  gravarPano() {
    const { W, H, P } = this;
    const pw = Math.max(8, Math.ceil(W * ESCALA_DO_PANO)), ph = Math.max(8, Math.ceil(H * ESCALA_DO_PANO));
    if (!this.panoTela) this.panoTela = document.createElement("canvas");
    if (this.panoTela.width !== pw || this.panoTela.height !== ph) {
      this.panoTela.width = pw;
      this.panoTela.height = ph;
    }
    const c = this.panoTela.getContext("2d");
    c.setTransform(pw / W, 0, 0, ph / H, 0, 0);
    c.globalAlpha = 1;
    c.globalCompositeOperation = "source-over";
    const vertical = c.createLinearGradient(0, 0, 0, H);
    vertical.addColorStop(0, rgba(P.fundo));
    vertical.addColorStop(1, rgba(P.fundoFim));
    c.fillStyle = vertical;
    c.fillRect(0, 0, W, H);
    brilho(c, W * 0.8, H * 0.06, W * 0.55, H * 0.5, 0, P.luz, 0.7);
    brilho(c, W * 0.08, H, W * 0.6, H * 0.55, 0, P.sombra, 0.7);
    this.pano(c);
    c.save();
    c.translate(W / 2, H / 2);
    c.scale(1, (H * 0.85) / (W * 0.95));
    const vinheta = c.createRadialGradient(0, 0, 0, 0, 0, W * 0.95);
    vinheta.addColorStop(0, rgba(P.vinheta, 0));
    vinheta.addColorStop(0.45, rgba(P.vinheta, 0));
    vinheta.addColorStop(1, rgba(P.vinheta));
    c.fillStyle = vinheta;
    c.fillRect(-W, -W, W * 2, W * 2);
    c.restore();
  }
}

class Grafo extends Motor {
  constructor(p) {
    super(p, 7);
    this.alcance = 170;
    this.nos = [];
    this.estrelas = [];
    this.linhas = new Lote();
    this.pontos = new Lote();
    this.cadentes = p.escuro ? [28, 60] : null;
  }

  medir() {
    const { W, H, sorte } = this;
    const n = entre(Math.round((W * H) / 22000), 14, 130);
    const kx = this.wAntes > 0 ? W / this.wAntes : 1;
    const ky = this.hAntes > 0 ? H / this.hAntes : 1;
    const antigos = this.nos;
    this.nos = [];
    for (let i = 0; i < n; i++) {
      if (i < antigos.length) this.nos.push({ ...antigos[i], x: antigos[i].x * kx, y: antigos[i].y * ky });
      else this.nos.push({ x: sorte() * W, y: sorte() * H, vx: (sorte() - 0.5) * 9, vy: (sorte() - 0.5) * 9, fase: sorte() * TAU, anel: sorte() < 0.28 });
    }
    this.estrelas = this.semear(this.estrelas, entre(Math.floor((W * H) / 7000), 20, 400));
  }

  avancar(dt) {
    const { W, H } = this;
    const ritmo = dt * (1 + 2.2 * this.atividade);
    for (const n of this.nos) {
      n.x += n.vx * ritmo;
      n.y += n.vy * ritmo;
      if (n.x < -20) n.x = W + 20;
      else if (n.x > W + 20) n.x = -20;
      if (n.y < -20) n.y = H + 20;
      else if (n.y > H + 20) n.y = -20;
    }
    this.derivar(this.estrelas, -1.6 * ritmo, -0.5 * ritmo);
  }

  desenhar(ctx, t) {
    const { P, nos, alcance } = this;
    this.desenharEstrelas(ctx, this.estrelas, t, 0.6);
    const forca = (P.escuro ? 0.4 : 0.5) + 0.1 * this.atividade;
    for (let i = 0; i < nos.length; i++) {
      const a = nos[i];
      for (let j = i + 1; j < nos.length; j++) {
        const b = nos[j];
        const dx = a.x - b.x, dy = a.y - b.y;
        if (Math.abs(dx) > alcance || Math.abs(dy) > alcance) continue;
        const d = distancia(dx, dy);
        if (d > alcance) continue;
        let alfa = (1 - d / alcance) * forca * Math.min(this.presenca(a.x, a.y), this.presenca(b.x, b.y));
        alfa *= 1 + 1.6 * this.realce((a.x + b.x) / 2, (a.y + b.y) / 2);
        this.linhas.linha(a.x, a.y, b.x, b.y, alfa);
      }
    }
    this.linhas.tracar(ctx, P.linha, 1);
    const tempo = t % 3600;
    for (const n of nos) {
      const b = 0.78 + 0.22 * Math.sin(tempo * 0.9 + n.fase);
      const alfa = Math.min(1, (0.6 + 0.4 * this.realce(n.x, n.y)) * b);
      if (n.anel) this.anel(ctx, n.x, n.y, 4.5, P.anel, alfa, 1.6);
      else this.pontos.ponto(n.x, n.y, alfa);
    }
    this.pontos.pintar(ctx, P.no, 4.4);
  }
}

class Constelacao extends Motor {
  constructor(p, carta) {
    super(p, 11);
    this.carta = carta;
    this.cadentes = carta ? null : [9, 20];
    this.linhas = new Lote();
    this.giro = [0, 0, 0];
    this.estrelas = [];
    this.raio = [];
    this.angulo = [];
    this.ligacoes = [];
    this.ligada = [];
  }

  geometria() {
    const { W, H } = this;
    const polo = { x: -0.2 * W, y: 1.9 * H };
    this.polo = polo;
    this.raioMinimo = distancia(entre(polo.x, 0, W) - polo.x, entre(polo.y, 0, H) - polo.y);
    const cantos = [[0, 0], [W, 0], [0, H], [W, H]];
    this.raioMaximo = Math.max(...cantos.map((c) => distancia(c[0] - polo.x, c[1] - polo.y))) + 10;
    const angulos = cantos.map((c) => Math.atan2(c[1] - polo.y, c[0] - polo.x));
    this.inicio = Math.min(...angulos) - 0.02;
    this.faixa = Math.max(...angulos) + 0.02 - this.inicio;
  }

  medir() {
    this.geometria();
    const { raioMinimo, raioMaximo, faixa } = this;
    const area = 0.5 * faixa * (raioMaximo * raioMaximo - raioMinimo * raioMinimo);
    const quantas = [entre(Math.floor(area / 4200), 40, 900), entre(Math.floor(area / 13000), 14, 300), entre(Math.floor(area / 30000), 10, 140)];
    const total = quantas[0] + quantas[1] + quantas[2];
    this.estrelas = [];
    this.raio = new Array(total);
    this.angulo = new Array(total);
    let k = 0;
    for (let c = 0; c < 3; c++) {
      const sorte = this.fresca(c);
      for (let i = 0; i < quantas[c]; i++, k++) {
        this.estrelas.push(novaEstrela(sorte, 0, 0, c));
        this.raio[k] = Math.sqrt(raioMinimo * raioMinimo + sorte() * (raioMaximo * raioMaximo - raioMinimo * raioMinimo));
        this.angulo[k] = sorte() * faixa;
      }
    }
    this.posicionar();
    this.ligar(total - quantas[2], total);
  }

  ligar(de, ate) {
    const { estrelas } = this;
    const pares = [];
    for (let i = de; i < ate; i++)
      for (let j = i + 1; j < ate; j++) {
        const d = distancia(estrelas[i].x - estrelas[j].x, estrelas[i].y - estrelas[j].y);
        if (d < 220) pares.push([i, j, d]);
      }
    const grau = new Array(estrelas.length).fill(0);
    const sorte = this.fresca(9);
    const fora = estrelas.map(() => sorte() < 0.2);
    const limite = estrelas.map(() => (sorte() < 0.18 ? 3 : 2));
    const grupo = estrelas.map((_, i) => i);
    const raiz = (i) => {
      while (grupo[i] !== i) i = grupo[i] = grupo[grupo[i]];
      return i;
    };
    this.ligacoes = [];
    pares.sort((p, q) => p[2] - q[2]);
    for (const [a, b] of pares) {
      if (fora[a] || fora[b] || grau[a] >= limite[a] || grau[b] >= limite[b]) continue;
      const ra = raiz(a), rb = raiz(b);
      if (ra === rb) continue;
      grupo[ra] = rb;
      grau[a]++;
      grau[b]++;
      this.ligacoes.push([a, b]);
    }
    this.ligada = grau.map((g) => g > 0);
  }

  posicionar() {
    const { polo, faixa, inicio } = this;
    for (let i = 0; i < this.estrelas.length; i++) {
      const e = this.estrelas[i];
      let a = this.angulo[i] + (this.giro[e.camada] % faixa);
      if (a >= faixa) a -= faixa;
      a += inicio;
      e.x = polo.x + this.raio[i] * Math.cos(a);
      e.y = polo.y + this.raio[i] * Math.sin(a);
    }
  }

  avancar(dt) {
    const ritmo = dt * (1 + 1.5 * this.atividade);
    const giro = [0.003, 0.0042, 0.0056];
    for (let c = 0; c < 3; c++) this.giro[c] += giro[c] * ritmo;
    this.posicionar();
  }

  grade(ctx) {
    const { polo, raioMinimo, raioMaximo, inicio, faixa } = this;
    ctx.strokeStyle = rgba(comAlfa(this.P.linha, 0.1));
    ctx.lineWidth = 1;
    for (let r = Math.ceil(raioMinimo / 150) * 150; r < raioMaximo; r += 150) {
      ctx.beginPath();
      ctx.arc(polo.x, polo.y, r, 0, TAU);
      ctx.stroke();
    }
    ctx.setLineDash([3, 6]);
    const giro = this.giro[2] % 0.12;
    ctx.beginPath();
    for (let a = Math.floor(inicio / 0.12) * 0.12 + giro; a < inicio + faixa; a += 0.12) {
      ctx.moveTo(polo.x + raioMinimo * Math.cos(a), polo.y + raioMinimo * Math.sin(a));
      ctx.lineTo(polo.x + raioMaximo * Math.cos(a), polo.y + raioMaximo * Math.sin(a));
    }
    ctx.stroke();
    ctx.setLineDash([]);
  }

  desenhar(ctx, t) {
    const { carta, estrelas, W, H } = this;
    if (carta) this.grade(ctx);
    const forca = (carta ? 0.42 : 0.26) + 0.08 * this.atividade;
    for (const [a, b] of this.ligacoes) {
      const ea = estrelas[a], eb = estrelas[b];
      if (distancia(ea.x - eb.x, ea.y - eb.y) > 230) continue;
      this.linhas.linha(ea.x, ea.y, eb.x, eb.y, forca * (1 + 1.4 * this.realce((ea.x + eb.x) / 2, (ea.y + eb.y) / 2)));
    }
    this.linhas.tracar(ctx, this.P.linha, carta ? 1.1 : 1);
    this.desenharEstrelas(ctx, estrelas, t, carta ? 0 : 1);
    for (let i = 0; i < estrelas.length; i++) {
      if (!this.ligada[i]) continue;
      const e = estrelas[i];
      if (e.x < -10 || e.y < -10 || e.x > W + 10 || e.y > H + 10) continue;
      this.anel(ctx, e.x, e.y, carta ? 4.2 : 3.8, this.P.anel, (carta ? 0.55 : 0.32) + 0.5 * this.realce(e.x, e.y), 1.1);
    }
  }
}

class ViaLactea extends Motor {
  constructor(p) {
    super(p, 23);
    this.cadentes = [16, 34];
    this.linhas = new Lote();
    this.estrelas = [];
    this.s = [];
    this.o = [];
    this.brilhantes = [];
    this.campo = 0;
    this.deriva = 0;
  }

  geometria() {
    const { W, H } = this;
    this.centro = { x: W / 2, y: H / 2 };
    this.giro = Math.atan2(-0.62 * H, W);
    this.u = { x: Math.cos(this.giro), y: Math.sin(this.giro) };
    this.n = { x: -this.u.y, y: this.u.x };
    this.comprimento = (W * Math.abs(this.u.x) + H * Math.abs(this.u.y)) / 2 + 30;
    this.largura = (W * Math.abs(this.n.x) + H * Math.abs(this.n.y)) / 2 + 30;
    this.sigma = 0.12 * Math.min(W, H) + 26;
  }

  curva(s) {
    return 0.05 * this.comprimento * Math.sin((s / this.comprimento) * 1.6);
  }

  gauss(sorte) {
    const u1 = 1 - sorte();
    const u2 = sorte();
    return Math.sqrt(-2 * Math.log(u1)) * Math.cos(TAU * u2);
  }

  medir() {
    const { W, H } = this;
    this.geometria();
    this.campo = entre(Math.floor((W * H) / 9000), 20, 300);
    const total = this.campo + entre(Math.floor((W * H) / 1100), 160, 2000);
    this.estrelas = [];
    this.s = new Array(total);
    this.o = new Array(total);
    const noCampo = this.fresca(0), naFaixa = this.fresca(1);
    for (let i = 0; i < total; i++) {
      const campo = i < this.campo;
      const sorte = campo ? noCampo : naFaixa;
      const v = sorte();
      const camada = campo ? (v < 0.6 ? 0 : v < 0.9 ? 1 : 2) : v < 0.8 ? 0 : v < 0.97 ? 1 : 2;
      this.estrelas.push(novaEstrela(sorte, 0, 0, camada));
      this.s[i] = sorte() * 2 - 1;
      this.o[i] = campo ? sorte() * 2 - 1 : entre(this.gauss(sorte) * 0.5, -1, 1);
    }
    this.brilhantes = [];
    this.estrelas.forEach((e, i) => { if (e.camada === 2) this.brilhantes.push(i); });
    this.posicionar();
  }

  posicionar() {
    const { centro, u, n, comprimento, largura, sigma, campo } = this;
    const volta = 2 * comprimento;
    for (let i = 0; i < this.estrelas.length; i++) {
      const e = this.estrelas[i];
      const s = ((this.s[i] * comprimento + this.deriva * (0.45 + 0.28 * e.camada) + comprimento) % volta) - comprimento;
      const o = this.o[i] * (i < campo ? largura : 2 * sigma) + this.curva(s);
      e.x = centro.x + u.x * s + n.x * o;
      e.y = centro.y + u.y * s + n.y * o;
    }
  }

  avancar(dt) {
    this.deriva += dt * 6 * (1 + 2 * this.atividade);
    this.posicionar();
  }

  pano(c) {
    this.geometria();
    const { centro, u, n, comprimento, sigma, giro, P } = this;
    brilho(c, centro.x, centro.y, comprimento * 1.1, sigma * 1.9, giro, C("#948BAE", 0.16), 1);
    brilho(c, centro.x + u.x * comprimento * 0.2, centro.y + u.y * comprimento * 0.2, comprimento * 0.7, sigma * 0.8, giro, comAlfa(P.destaque, 0.09), 1);
    brilho(c, centro.x + n.x * sigma * 0.18, centro.y + n.y * sigma * 0.18, comprimento * 0.75, sigma * 0.2, giro, comAlfa(P.fundo, 0.5), 1);
  }

  desenhar(ctx, t) {
    this.ligarBrilhantes(this.linhas, this.brilhantes, this.estrelas, 95, 0.3 + 0.1 * this.atividade, null);
    this.linhas.tracar(ctx, this.P.linha, 1);
    this.desenharEstrelas(ctx, this.estrelas, t);
  }
}

const NUVENS = [
  { cor: C("#E59AC4", 0.24), x: 0.26, y: 0.34, raio: 0.42, achatado: 0.62, fx: 0.031, fy: 0.026, fase: 0 },
  { cor: C("#948BAE", 0.28), x: 0.74, y: 0.28, raio: 0.5, achatado: 0.7, fx: 0.024, fy: 0.037, fase: 1.7 },
  { cor: C("#86C5E6", 0.17), x: 0.62, y: 0.8, raio: 0.38, achatado: 0.66, fx: 0.041, fy: 0.029, fase: 3.1 },
  { cor: C("#5B5470", 0.34), x: 0.14, y: 0.82, raio: 0.46, achatado: 0.74, fx: 0.022, fy: 0.044, fase: 4.4 },
];

class Nebulosa extends Motor {
  constructor(p) {
    super(p, 29);
    this.cadentes = [30, 60];
    this.intervaloDoPano = 0.4;
    this.linhas = new Lote();
    this.estrelas = [];
    this.brilhantes = [];
    this.relogio = 0;
  }

  medir() {
    this.estrelas = this.semear(this.estrelas, entre(Math.floor((this.W * this.H) / 4500), 40, 600));
    this.brilhantes = [];
    this.estrelas.forEach((e, i) => { if (e.camada >= 1) this.brilhantes.push(i); });
  }

  avancar(dt) {
    const ritmo = dt * (1 + 2 * this.atividade);
    this.relogio += ritmo;
    this.derivar(this.estrelas, -2.4 * ritmo, 0.9 * ritmo);
  }

  pano(c) {
    const { W, H } = this;
    const lado = Math.max(W, H);
    const tempo = this.relogio % 100000;
    for (const n of NUVENS) {
      const x = (n.x + 0.1 * Math.sin(tempo * n.fx + n.fase)) * W;
      const y = (n.y + 0.08 * Math.cos(tempo * n.fy + n.fase * 1.3)) * H;
      const raio = n.raio * lado * (1 + 0.06 * Math.sin(tempo * 0.05 + n.fase));
      brilho(c, x, y, raio, raio * n.achatado, 0.4 * Math.sin(tempo * 0.013 + n.fase), comAlfa(n.cor, 1 + 0.25 * this.atividade), 1);
    }
  }

  desenhar(ctx, t) {
    this.ligarBrilhantes(this.linhas, this.brilhantes, this.estrelas, 150, 0.26 + 0.08 * this.atividade, 4);
    this.linhas.tracar(ctx, this.P.linha, 1);
    this.desenharEstrelas(ctx, this.estrelas, t);
  }
}

class Pulso extends Motor {
  constructor(p) {
    super(p, 17);
    this.celula = 150;
    this.linhas = new Lote();
    this.rastro = new Lote();
    this.cabecas = new Lote();
    this.brilhos = new Lote();
    this.pontos = new Lote();
    this.sinais = [];
    this.fracao = [];
    this.nos = [];
    this.flash = [];
    this.vizinhos = [];
    this.arestas = [];
    this.fundo = [];
    this.cols = 0;
    this.rows = 0;
    this.acumulado = 0;
    this.relogio = 0;
  }

  medir() {
    const { W, H, celula } = this;
    this.fundo = this.semear(this.fundo, entre(Math.floor((W * H) / 8000), 20, 360));
    const cols = Math.max(2, Math.round(W / celula)), rows = Math.max(2, Math.round(H / celula));
    if (cols === this.cols && rows === this.rows) {
      this.posicionar();
      return;
    }
    this.cols = cols;
    this.rows = rows;
    const sorte = this.fresca(0);
    const n = cols * rows;
    this.fracao = [];
    this.nos = [];
    for (let j = 0; j < rows; j++)
      for (let i = 0; i < cols; i++) {
        this.fracao.push({ x: (i + 0.18 + 0.64 * sorte()) / cols, y: (j + 0.18 + 0.64 * sorte()) / rows });
        this.nos.push(novaEstrela(sorte, 0, 0, 2));
      }
    this.flash = new Array(n).fill(0);
    this.posicionar();
    this.vizinhos = Array.from({ length: n }, () => []);
    const vistas = new Set();
    this.arestas = [];
    const limite = 2.2 * Math.max(W / cols, H / rows);
    const nos = this.nos;
    for (let a = 0; a < n; a++) {
      const proximos = [];
      for (let b = 0; b < n; b++) if (b !== a) proximos.push([b, distancia(nos[a].x - nos[b].x, nos[a].y - nos[b].y)]);
      proximos.sort((p, q) => p[1] - q[1]);
      for (const [b, d] of proximos.slice(0, 2)) {
        const chave = Math.min(a, b) * 100000 + Math.max(a, b);
        if (d > limite || vistas.has(chave)) continue;
        vistas.add(chave);
        this.arestas.push([Math.min(a, b), Math.max(a, b)]);
        this.vizinhos[a].push(b);
        this.vizinhos[b].push(a);
      }
    }
    this.sinais = [];
    for (let i = 0; i < 6; i++) this.lancar(sorte.inteiro(n), sorte());
  }

  posicionar() {
    const tempo = this.relogio % 100000;
    for (let i = 0; i < this.nos.length; i++) {
      const e = this.nos[i];
      e.x = this.fracao[i].x * this.W + 5 * Math.sin(tempo * 0.31 + e.fase);
      e.y = this.fracao[i].y * this.H + 5 * Math.cos(tempo * 0.27 + e.fase * 1.7);
    }
  }

  lancar(de, andado = 0) {
    const vizinhos = this.vizinhos[de];
    if (!vizinhos.length) return;
    this.sinais.push({ de, para: vizinhos[this.sorte.inteiro(vizinhos.length)], andado, velocidade: 110 + 70 * this.sorte() });
    this.flash[de] = Math.max(this.flash[de], 0.5);
  }

  aoClicar(x, y) {
    if (!this.nos.length) return;
    let perto = 0, menor = Infinity;
    this.nos.forEach((e, i) => {
      const d = distancia(e.x - x, e.y - y);
      if (d < menor) { menor = d; perto = i; }
    });
    this.flash[perto] = 1;
    for (const v of this.vizinhos[perto]) this.sinais.push({ de: perto, para: v, andado: 0, velocidade: 170 + 40 * this.sorte() });
  }

  avancar(dt) {
    const { nos, sinais, vizinhos, flash } = this;
    this.relogio += dt;
    this.derivar(this.fundo, -1.2 * dt, 0.4 * dt);
    this.posicionar();
    for (let i = 0; i < flash.length; i++) flash[i] = Math.max(0, flash[i] - dt * 1.3);
    const limite = 30 + 40 * this.atividade;
    this.acumulado += dt * (0.7 + 3.3 * this.atividade);
    while (this.acumulado >= 1) {
      this.acumulado -= 1;
      if (sinais.length < limite && nos.length > 0) this.lancar(this.sorte.inteiro(nos.length));
    }
    const ritmo = 1 + 0.9 * this.atividade;
    for (let i = sinais.length - 1; i >= 0; i--) {
      const s = sinais[i];
      const comprimento = Math.max(1, distancia(nos[s.para].x - nos[s.de].x, nos[s.para].y - nos[s.de].y));
      s.andado += (s.velocidade * ritmo * dt) / comprimento;
      if (s.andado < 1) continue;
      flash[s.para] = 1;
      const opcoes = vizinhos[s.para].filter((v) => v !== s.de);
      if (!opcoes.length || this.sorte() < 0.25 - 0.12 * this.atividade || sinais.length > limite) {
        sinais.splice(i, 1);
        continue;
      }
      s.de = s.para;
      s.para = opcoes[this.sorte.inteiro(opcoes.length)];
      s.andado = 0;
    }
  }

  desenhar(ctx, t) {
    const { P, nos, flash } = this;
    this.desenharEstrelas(ctx, this.fundo, t, 0.5);
    for (const [a, b] of this.arestas) {
      const na = nos[a], nb = nos[b];
      let alfa = 0.13 + 0.32 * Math.max(flash[a], flash[b]) + 0.06 * this.atividade;
      alfa *= 1 + 1.4 * this.realce((na.x + nb.x) / 2, (na.y + nb.y) / 2);
      this.linhas.linha(na.x, na.y, nb.x, nb.y, alfa);
    }
    this.linhas.tracar(ctx, P.linha, 1);
    for (const s of this.sinais) {
      const de = nos[s.de], para = nos[s.para];
      const dx = para.x - de.x, dy = para.y - de.y, comprimento = Math.max(1, distancia(dx, dy));
      const x = de.x + dx * s.andado, y = de.y + dy * s.andado;
      this.brilhos.ponto(x, y, 0.22);
      this.cabecas.ponto(x, y, 0.95);
      for (let k = 1; k <= 6; k++) {
        const atras = s.andado - (k * 6) / comprimento;
        if (atras < 0) break;
        this.rastro.ponto(de.x + dx * atras, de.y + dy * atras, 0.7 * (1 - k / 7));
      }
    }
    this.brilhos.pintar(ctx, P.destaque, 12);
    this.rastro.pintar(ctx, P.destaque, 2.2);
    this.cabecas.pintar(ctx, P.anel, 3.2);
    for (let i = 0; i < nos.length; i++) {
      const e = nos[i];
      this.pontos.ponto(e.x, e.y, Math.min(1, 0.35 + 0.5 * cintilar(e, t) + flash[i] * 0.4 + 0.4 * this.realce(e.x, e.y)));
      if (flash[i] > 0.02) this.anel(ctx, e.x, e.y, 4 + 18 * (1 - flash[i]), P.destaque, flash[i] * 0.6, 1.2);
    }
    this.pontos.pintar(ctx, P.no, 4.2);
  }
}

const CORTINAS = [
  { cor: C("#7BCB9F"), altura: 0.3, amplitude: 0.055, tamanho: 0.3, forca: 0.32, fase: 0 },
  { cor: C("#86C5E6"), altura: 0.21, amplitude: 0.045, tamanho: 0.22, forca: 0.21, fase: 2.3 },
  { cor: C("#948BAE"), altura: 0.4, amplitude: 0.05, tamanho: 0.2, forca: 0.25, fase: 4.1 },
];

let feixes = null;
function criarFeixes() {
  const base = document.createElement("canvas");
  base.width = 64;
  base.height = 256;
  const g = base.getContext("2d");
  const vertical = g.createLinearGradient(0, 0, 0, 256);
  for (const [p, a] of [[0, 0], [0.3, 40], [0.66, 150], [0.84, 255], [1, 0]]) vertical.addColorStop(p, `rgba(255,255,255,${a / 255})`);
  g.fillStyle = vertical;
  g.fillRect(0, 0, 64, 256);
  g.globalCompositeOperation = "destination-in";
  const horizontal = g.createLinearGradient(0, 0, 64, 0);
  for (const [p, a] of [[0, 0], [0.22, 90], [0.5, 255], [0.78, 90], [1, 0]]) horizontal.addColorStop(p, `rgba(255,255,255,${a / 255})`);
  g.fillStyle = horizontal;
  g.fillRect(0, 0, 64, 256);
  return CORTINAS.map((c) => {
    const tinta = document.createElement("canvas");
    tinta.width = 64;
    tinta.height = 256;
    const t = tinta.getContext("2d");
    t.drawImage(base, 0, 0);
    t.globalCompositeOperation = "source-in";
    t.fillStyle = rgba(c.cor);
    t.fillRect(0, 0, 64, 256);
    return tinta;
  });
}

class Aurora extends Motor {
  constructor(p) {
    super(p, 31);
    this.cadentes = [24, 50];
    this.intervaloDoPano = 0.14;
    this.linhas = new Lote();
    this.estrelas = [];
    this.brilhantes = [];
    this.relogio = 0;
    feixes ??= criarFeixes();
  }

  medir() {
    this.estrelas = this.semear(this.estrelas, entre(Math.floor((this.W * this.H) / 5500), 30, 500));
    this.brilhantes = [];
    this.estrelas.forEach((e, i) => { if (e.camada === 2) this.brilhantes.push(i); });
  }

  avancar(dt) {
    const ritmo = dt * (1 + 1.6 * this.atividade);
    this.relogio += ritmo;
    this.derivar(this.estrelas, -1.4 * ritmo, 0.3 * ritmo);
  }

  desenhar(ctx, t) {
    this.ligarBrilhantes(this.linhas, this.brilhantes, this.estrelas, 150, 0.26, 4);
    this.linhas.tracar(ctx, this.P.linha, 1);
    this.desenharEstrelas(ctx, this.estrelas, t);
  }

  pano(c) {
    const { W, H } = this;
    const passo = 12;
    const tempo = this.relogio % 100000;
    c.globalCompositeOperation = "screen";
    c.imageSmoothingQuality = "high";
    CORTINAS.forEach((cortina, i) => {
      for (let x = -passo * 2; x < W + passo * 2; x += passo) {
        const onda = Math.sin(x * 0.0042 + tempo * 0.11 + cortina.fase) + 0.5 * Math.sin(x * 0.011 - tempo * 0.17 + cortina.fase * 1.7);
        const baseY = H * (cortina.altura + cortina.amplitude * onda);
        const altura = H * cortina.tamanho * (0.7 + 0.3 * Math.sin(x * 0.006 + tempo * 0.08 + cortina.fase));
        const raio = 0.5 + 0.5 * Math.sin(x * 0.013 + tempo * 0.35 + cortina.fase) * Math.sin(x * 0.0047 - tempo * 0.16);
        const forca = cortina.forca * (0.45 + 0.55 * raio) * (1 + 0.35 * this.atividade) * (0.75 + 0.25 * this.perto(x, baseY) * 2);
        c.globalAlpha = limitar(forca / 2);
        const largura = passo * 10;
        c.drawImage(feixes[i], x - largura / 2, baseY - altura * 0.84, largura, altura);
      }
    });
    c.globalAlpha = 1;
    c.globalCompositeOperation = "source-over";
  }
}

const CRIADORES = {
  grafo: () => new Grafo(PALETAS.grafo),
  "grafo-claro": () => new Grafo(PALETAS["grafo-claro"]),
  constelacao: () => new Constelacao(PALETAS.constelacao, false),
  "carta-celeste": () => new Constelacao(PALETAS["carta-celeste"], true),
  "via-lactea": () => new ViaLactea(PALETAS["via-lactea"]),
  nebulosa: () => new Nebulosa(PALETAS.nebulosa),
  pulso: () => new Pulso(PALETAS.pulso),
  aurora: () => new Aurora(PALETAS.aurora),
};

export function criarFundo(id) {
  const criar = CRIADORES[id];
  if (!criar) throw new Error(`Fundo ${id} não existe.`);
  return criar();
}
