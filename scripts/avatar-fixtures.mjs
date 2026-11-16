import { readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const repo = process.env.AVATAR_REPO;
if (!repo) {
  console.error('Defina AVATAR_REPO com a pasta do open-bot-avatar (git clone https://github.com/DominikScholz/open-bot-avatar).');
  process.exit(2);
}
const saida = resolve(process.argv[2] ?? 'tests/Nexora.Testes/Avatares/fixtures.json');
const src = join(repo, 'packages', 'core', 'src');
const carregar = (arquivo) => import(pathToFileURL(join(src, arquivo)).href);
const { avatarFrame, liveFrame } = await carregar('render.ts');
const { validateAvatar } = await carregar('validate.ts');
const { hashString } = await carregar('motion.ts');
const { getExample } = await carregar('examples.ts');

const galeria = ['cat', 'book', 'star', 'magnifier', 'brain', 'cloud', 'rocket', 'ghost'].map((id) =>
  JSON.parse(readFileSync(join(repo, 'gallery', `${id}.json`), 'utf8')),
);
const definicoes = [...galeria, getExample('mug'), getExample('barbell')];

const casos = [
  { time: 0 },
  { time: 1.37 },
  { time: 2.1, motion: 'thinking' },
  { time: 0.5, motion: 'happy' },
  { time: 3, gaze: { x: 0.6, y: -0.3 } },
  { time: 4.4, gaze: { x: -0.8, y: 0.5, weight: 0.4 } },
  { time: 5, reducedMotion: true },
  { time: 1.1, motion: 'happy', motionBlend: { from: 'idle', t: 0.4 } },
  { time: 2.6, motion: 'idle', motionBlend: { from: { motion: 'thinking', blend: { from: 'happy', t: 0.5 } }, t: 0.3 } },
  { time: 0.8, depth: 3 },
  { time: 1.9, view: { yaw: 1.2, pitch: 0.1 } },
  { time: 2.2, size: 48, vivo: true },
  { time: 6.3, color: '#7C8CF8', eyeColor: '#fff' },
];

const matriz = (m) => m.map((v) => +v.toFixed(9));
const resultado = { avatares: [] };
for (const def of definicoes) {
  const validado = validateAvatar(def);
  if (!validado.ok) throw new Error(`${def.id}: ${validado.errors.join('; ')}`);
  const item = { definicao: def, normalizado: validado.value.body.path, casos: [] };
  for (const caso of casos) {
    const { vivo, ...opcoes } = caso;
    const quadro = vivo ? liveFrame(def, opcoes) : avatarFrame(def, opcoes);
    item.casos.push({
      opcoes: caso,
      cor: quadro.color,
      tinta: quadro.ink,
      raiz: matriz(quadro.root),
      caminhos: quadro.paths.map((p) => ({ id: p.id, hash: hashString(p.d), tamanho: p.d.length, parOuImpar: p.evenOdd, caixa: [p.box.x, p.box.y, p.box.w, p.box.h] })),
      formas: quadro.shapes.map((s) =>
        s.kind === 'slice'
          ? { olho: false, chave: s.key, caminho: s.path, matriz: matriz(s.matrix) }
          : { olho: true, chave: s.key, matriz: matriz(s.matrix), marca: s.mark },
      ),
    });
  }
  resultado.avatares.push(item);
}
writeFileSync(saida, JSON.stringify(resultado));
console.log(`fixtures: ${definicoes.length} avatares x ${casos.length} casos em ${saida}`);
