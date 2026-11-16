import { test, beforeEach } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { onRequestPost, onRequest, validar, montarEmail } from '../../functions/api/desempenho.js';

const estatistica = (media, maximo, p95) => ({ media, maximo, p95 });

const dia = (data, extras = {}) => ({
  data,
  amostras: 2400,
  ramMb: estatistica(900, 1800, 1500),
  ramNexoraMb: estatistica(300, 400, 380),
  cpu: estatistica(4.5, 40, 12),
  cpuNexora: estatistica(0.8, 6, 2),
  atrasoMs: { media: 12, maximo: 640, p95: 30 },
  travadas: 2,
  divisaoMb: { nexora: 300, sessoes: 400, mcps: 90, builds: 60, outros: 50 },
  heapMaximoMb: 160,
  privadaMaximaMb: 420,
  handlesMaximo: 2100,
  abasMaximo: 6,
  trabalhandoMaximo: 3,
  custoMedioMs: 2.1,
  momentos: [
    { hora: '10:40', tipo: 'travada', valor: 640, ramMb: 1200, cpu: 30.5, abas: 5, trabalhando: 3 },
    { hora: '15:20', tipo: 'ram', valor: 1800, ramMb: 1800, cpu: 9.7, abas: 6, trabalhando: 1 },
    { hora: '11:05', tipo: 'cpu', valor: 40, ramMb: 1500, cpu: 40, abas: 4, trabalhando: 4 },
  ],
  ...extras,
});

const relatorio = (extras = {}) => ({
  formato: 1,
  versao: '0.4.4',
  sistema: 'windows',
  nucleos: 16,
  memoriaDoPcMb: 32768,
  geradoEm: '2026-11-09T09:00',
  dias: [dia('2026-11-03'), dia('2026-11-04', { travadas: 0, momentos: [] })],
  ...extras,
});

const AMBIENTE = { RESEND_KEY: 'chave-de-teste', RELATORIO_PARA: 'vini@exemplo.com' };

let chamadas;
let guardados;
let falhaDoResend;

beforeEach(() => {
  chamadas = [];
  guardados = new Set();
  falhaDoResend = false;
  globalThis.fetch = async (url, opcoes) => {
    chamadas.push({ url, opcoes, corpo: JSON.parse(opcoes.body) });
    return falhaDoResend ? new Response('{}', { status: 422 }) : new Response('{"id":"x"}', { status: 200 });
  };
  globalThis.caches = {
    default: {
      match: async (pedido) => (guardados.has(pedido.url) ? new Response('1') : undefined),
      put: async (pedido) => guardados.add(pedido.url),
    },
  };
});

const enviar = (corpo, env = AMBIENTE, cabecalhos = {}) =>
  onRequestPost({
    request: new Request('https://nexora.suiciniv.dev/api/desempenho', {
      method: 'POST',
      headers: { 'content-type': 'application/json', ...cabecalhos },
      body: typeof corpo === 'string' ? corpo : JSON.stringify(corpo),
    }),
    env,
  });

test('envia um e-mail so para o destino configurado', async () => {
  const resposta = await enviar(relatorio());
  assert.equal(resposta.status, 200);
  assert.equal(chamadas.length, 1);
  const { url, opcoes, corpo } = chamadas[0];
  assert.equal(url, 'https://api.resend.com/emails');
  assert.equal(opcoes.headers.authorization, 'Bearer chave-de-teste');
  assert.deepEqual(corpo.to, ['vini@exemplo.com']);
  assert.match(corpo.from, /resend\.dev/);
  assert.match(corpo.subject, /Nexora 0\.4\.4: desempenho de 03\/11 a 04\/11 \(2 travadas\)/);
  assert.match(corpo.text, /RAM total \(Nexora \+ sessões \+ MCPs \+ builds\): média 900 MB, pico 1,8 GB/);
  assert.match(corpo.text, /interface travada por 640 ms/);
  assert.match(corpo.html, /<table/);
});

test('o remetente pode vir do ambiente', async () => {
  await enviar(relatorio(), { ...AMBIENTE, RELATORIO_DE: 'Nexora <nexora@suiciniv.dev>' });
  assert.equal(chamadas[0].corpo.from, 'Nexora <nexora@suiciniv.dev>');
});

test('sem chave ou destino responde 503 e nao chama o Resend', async () => {
  assert.equal((await enviar(relatorio(), { RELATORIO_PARA: 'vini@exemplo.com' })).status, 503);
  assert.equal((await enviar(relatorio(), { RESEND_KEY: 'x' })).status, 503);
  assert.equal((await enviar(relatorio(), { ...AMBIENTE, RELATORIO_PARA: 'a@b.com, c@d.com' })).status, 503);
  assert.equal(chamadas.length, 0);
});

test('recusa corpo grande, quebrado e fora do formato', async () => {
  assert.equal((await enviar('x'.repeat(20000))).status, 413);
  assert.equal((await enviar('{quebrado')).status, 400);
  assert.equal((await enviar(relatorio({ formato: 2 }))).status, 400);
  assert.equal((await enviar(relatorio({ sistema: '<script>' }))).status, 400);
  assert.equal((await enviar(relatorio({ versao: '1.0 <b>' }))).status, 400);
  assert.equal((await enviar(relatorio({ dias: [] }))).status, 400);
  assert.equal((await enviar(relatorio({ dias: Array.from({ length: 15 }, (_, i) => dia(`2026-10-${10 + i}`)) }))).status, 400);
  assert.equal((await enviar(relatorio({ dias: [dia('2026-11-03', { travadas: -1 })] }))).status, 400);
  assert.equal((await enviar(relatorio({ dias: [dia('2026-11-03', { travadas: '2' })] }))).status, 400);
  assert.equal((await enviar(relatorio({ dias: [dia('<img>')] }))).status, 400);
  assert.equal((await enviar(relatorio({ dias: [dia('2026-11-03', { momentos: [{ hora: '10:40', tipo: 'x' }] })] }))).status, 400);
  assert.equal(chamadas.length, 0);
});

test('so repassa campos conhecidos para o e-mail', async () => {
  const sujo = relatorio();
  sujo.dias[0].intruso = '<script>alert(1)</script>';
  sujo.intruso = 'Loja secreta';
  await enviar(sujo);
  const { html, text } = chamadas[0].corpo;
  assert.ok(!html.includes('script') && !text.includes('script'));
  assert.ok(!html.includes('secreta') && !text.includes('secreta'));
});

test('segura o segundo envio seguido com 429', async () => {
  assert.equal((await enviar(relatorio())).status, 200);
  const segundo = await enviar(relatorio());
  assert.equal(segundo.status, 429);
  assert.equal(segundo.headers.get('retry-after'), '600');
  assert.equal(chamadas.length, 1);
});

test('falha do Resend vira 502 e nao trava o proximo envio', async () => {
  falhaDoResend = true;
  assert.equal((await enviar(relatorio())).status, 502);
  falhaDoResend = false;
  assert.equal((await enviar(relatorio())).status, 200);
});

test('outros metodos respondem 405', async () => {
  const resposta = onRequest();
  assert.equal(resposta.status, 405);
  assert.equal(resposta.headers.get('allow'), 'POST');
});

test('o e-mail traz resumo, tabela por dia e momentos', () => {
  const email = montarEmail(validar(relatorio()));
  assert.match(email.texto, /Por dia/);
  assert.match(email.texto, /03\/11 \|/);
  assert.match(email.texto, /Momentos mais pesados/);
  assert.match(email.texto, /03\/11 às 15:20: pico de RAM de 1,8 GB/);
  assert.match(email.texto, /RAM média por parte: Nexora 300 MB, sessões 400 MB/);
  assert.equal((email.html.match(/<tr>/g) ?? []).length, 3);
});

test('o e-mail separa a RAM só do Nexora e explica a travada pelo GC', () => {
  const travada = { hora: '07:07', tipo: 'travada', valor: 2720, ramMb: 552, cpu: 1.4, abas: 1, trabalhando: 0, nexoraMb: 552, pausaGcMs: 1800, gen2: 2, desdeAbrirS: 300, abaNovaHaS: 90 };
  const email = montarEmail(validar(relatorio({ dias: [dia('2026-11-03', { momentos: [travada] })] })));
  assert.match(email.texto, /RAM só do Nexora: média 300 MB, pico 400 MB/);
  assert.match(email.texto, /interface travada por 2\.720 ms \(.*GC 1\.800 ms, 5 min depois de abrir o app, última aba nova há 2 min/);
  assert.match(montarEmail(validar(relatorio())).texto, /pico de RAM de 1,8 GB \(1 abas|pico de RAM de 1,8 GB \(6 abas/);
});

const causa = (nome, vezes, maiorMs, ponta = 'System.IO.File.ReadAllText') => ({ causa: nome, ponta, vezes, maiorMs });

test('o e-mail junta as causas das travadas de todos os dias', () => {
  const email = montarEmail(validar(relatorio({
    dias: [
      dia('2026-11-03', { causas: [causa('Nexora.ViewModels.ChatVM.Aplicar', 3, 900), causa('Nexora.Views.DiffView.Montar', 1, 3496, 'Nexora.Views.DiffView.Montar')] }),
      dia('2026-11-04', { causas: [causa('Nexora.ViewModels.ChatVM.Aplicar', 4, 1200, 'System.Threading.Monitor.Wait')] }),
    ],
  })));
  assert.match(email.texto, /Onde a interface parou\nNexora\.ViewModels\.ChatVM\.Aplicar \(7 vezes, até 1\.200 ms; parada em System\.Threading\.Monitor\.Wait\)\nNexora\.Views\.DiffView\.Montar \(1 vez, até 3\.496 ms\)/);
  assert.match(email.html, /<code>Nexora\.ViewModels\.ChatVM\.Aplicar<\/code>/);
  assert.doesNotMatch(montarEmail(validar(relatorio())).texto, /Onde a interface parou/);
});

test('causa so passa com nome de codigo', async () => {
  for (const nome of ['D:\\Projetos\\Loja', 'Loja secreta.x', '<img src=x>.y', 'SemPonto', `Nexora.${'a'.repeat(160)}`]) {
    assert.equal((await enviar(relatorio({ dias: [dia('2026-11-03', { causas: [causa(nome, 1, 400)] })] }))).status, 400, nome);
  }
  assert.equal((await enviar(relatorio({ dias: [dia('2026-11-03', { causas: [causa('Nexora.A.B', 1, 400, 'C:\\x.y')] })] }))).status, 400);
  assert.equal((await enviar(relatorio({ dias: [dia('2026-11-03', { causas: Array.from({ length: 6 }, (_, i) => causa(`Nexora.A.M${i}`, 1, 400)) })] }))).status, 400);
  assert.equal(chamadas.length, 0);
});

test('aceita o resumo montado pelo Nexora quando o teste do C# manda um', { skip: !process.env.NEXORA_RESUMO }, async () => {
  const resumo = JSON.parse(readFileSync(process.env.NEXORA_RESUMO, 'utf8'));
  const resposta = await enviar(resumo);
  assert.equal(resposta.status, 200);
  assert.equal(chamadas.length, 1);
  assert.match(chamadas[0].corpo.text, /Onde a interface parou\nNexora\.ViewModels\.ChatVM\.Aplicar \(2 vezes/);
});
