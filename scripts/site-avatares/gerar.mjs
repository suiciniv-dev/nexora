import { execFileSync } from 'node:child_process';
import { readdirSync, readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const aqui = dirname(fileURLToPath(import.meta.url));
const raiz = resolve(aqui, '..', '..');
const repo = process.env.AVATAR_REPO;
if (!repo || !existsSync(join(repo, 'packages', 'core', 'src', 'render.ts'))) {
  console.error('Defina AVATAR_REPO com o checkout do open-bot-avatar.');
  process.exit(2);
}
const pasta = join(raiz, 'src', 'Nexora', 'Assets', 'Avatares');
const definicoes = Object.fromEntries(
  readdirSync(pasta).filter((f) => f.endsWith('.json') && f !== 'catalogo.json').map((f) => {
    const d = JSON.parse(readFileSync(join(pasta, f), 'utf8'));
    return [d.id, d];
  }),
);
const temporario = join(aqui, '.definicoes.json');
writeFileSync(temporario, JSON.stringify(definicoes));
const esbuild = process.env.ESBUILD ?? 'esbuild';
const saida = join(raiz, 'site', 'avatares', 'avatares.js');
mkdirSync(dirname(saida), { recursive: true });
execFileSync(esbuild, [
  join(aqui, 'entrada.js'),
  '--bundle', '--minify', '--format=iife', '--target=es2020', '--legal-comments=none',
  `--alias:obav=${join(repo, 'packages', 'core', 'src')}`,
  `--alias:obav-definicoes=${temporario}`,
  `--outfile=${saida}`,
  '--banner:js=/*! open-bot-avatar (c) Dominik Scholz, MIT. https://github.com/DominikScholz/open-bot-avatar */',
], { stdio: 'inherit' });
