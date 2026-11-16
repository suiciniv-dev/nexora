const LIMITE_DO_CORPO = 16384;
const INTERVALO_MINIMO = 600;
const MAXIMO_DE_DIAS = 14;
const MAXIMO_DE_MOMENTOS = 12;
const MAXIMO_DE_CAUSAS = 5;
const NOME_DE_CODIGO = /^(?=.{1,160}$)[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)+$/;
const REMETENTE_PADRAO = 'Nexora <onboarding@resend.dev>';
const ENDERECO_DO_RESEND = 'https://api.resend.com/emails';
const PARTES = ['Nexora', 'sessões', 'MCPs', 'builds', 'outros'];
const SISTEMAS = ['windows', 'mac', 'linux', 'outro'];
const TIPOS = ['ram', 'cpu', 'travada'];

class Invalido extends Error {}

function numero(valor, maximo = 1e9) {
  if (typeof valor !== 'number' || !Number.isFinite(valor) || valor < 0 || valor > maximo) throw new Invalido('numero');
  return valor;
}

function texto(valor, formato) {
  if (typeof valor !== 'string' || !formato.test(valor)) throw new Invalido('texto');
  return valor;
}

function objeto(valor) {
  if (valor === null || typeof valor !== 'object' || Array.isArray(valor)) throw new Invalido('objeto');
  return valor;
}

function lista(valor, maximo) {
  if (!Array.isArray(valor) || valor.length > maximo) throw new Invalido('lista');
  return valor;
}

function opcional(valor) {
  return valor === undefined ? undefined : numero(valor);
}

function estatistica(valor) {
  const e = objeto(valor);
  return { media: numero(e.media), maximo: numero(e.maximo), p95: numero(e.p95) };
}

function momento(valor) {
  const m = objeto(valor);
  const tipo = texto(m.tipo, /^(ram|cpu|travada)$/);
  return {
    hora: texto(m.hora, /^\d{2}:\d{2}$/),
    tipo,
    valor: numero(m.valor),
    ramMb: numero(m.ramMb),
    cpu: numero(m.cpu, 10000),
    abas: numero(m.abas, 100000),
    trabalhando: numero(m.trabalhando, 100000),
    nexoraMb: opcional(m.nexoraMb),
    pausaGcMs: opcional(m.pausaGcMs),
    desdeAbrirS: opcional(m.desdeAbrirS),
    abaNovaHaS: opcional(m.abaNovaHaS),
  };
}

function causa(valor) {
  const c = objeto(valor);
  return {
    causa: texto(c.causa, NOME_DE_CODIGO),
    ponta: texto(c.ponta, NOME_DE_CODIGO),
    vezes: numero(c.vezes, 100000),
    maiorMs: numero(c.maiorMs, 3600000),
  };
}

function dia(valor) {
  const d = objeto(valor);
  const divisao = objeto(d.divisaoMb);
  return {
    data: texto(d.data, /^\d{4}-\d{2}-\d{2}$/),
    amostras: numero(d.amostras, 100000),
    ramMb: estatistica(d.ramMb),
    ramNexoraMb: estatistica(d.ramNexoraMb),
    cpu: estatistica(d.cpu),
    cpuNexora: estatistica(d.cpuNexora),
    atrasoMs: estatistica(d.atrasoMs),
    travadas: numero(d.travadas, 100000),
    divisaoMb: {
      nexora: numero(divisao.nexora),
      sessoes: numero(divisao.sessoes),
      mcps: numero(divisao.mcps),
      builds: numero(divisao.builds),
      outros: numero(divisao.outros),
    },
    heapMaximoMb: numero(d.heapMaximoMb),
    privadaMaximaMb: numero(d.privadaMaximaMb),
    handlesMaximo: numero(d.handlesMaximo),
    abasMaximo: numero(d.abasMaximo, 100000),
    trabalhandoMaximo: numero(d.trabalhandoMaximo, 100000),
    custoMedioMs: numero(d.custoMedioMs, 100000),
    momentos: lista(d.momentos, MAXIMO_DE_MOMENTOS).map(momento),
    causas: d.causas === undefined ? [] : lista(d.causas, MAXIMO_DE_CAUSAS).map(causa),
  };
}

export function validar(corpo) {
  const c = objeto(corpo);
  if (c.formato !== 1) throw new Invalido('formato');
  const sistema = texto(c.sistema, /^[a-z]{1,8}$/);
  if (!SISTEMAS.includes(sistema)) throw new Invalido('sistema');
  const dias = lista(c.dias, MAXIMO_DE_DIAS).map(dia);
  if (dias.length === 0) throw new Invalido('dias');
  return {
    versao: texto(c.versao, /^[0-9.]{1,16}$/),
    sistema,
    nucleos: numero(c.nucleos, 100000),
    memoriaDoPcMb: numero(c.memoriaDoPcMb),
    geradoEm: texto(c.geradoEm, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/),
    dias,
  };
}

const inteiro = (n) => Math.round(n).toLocaleString('pt-BR');
const decimal = (n) => n.toLocaleString('pt-BR', { maximumFractionDigits: 1 });
const pct = (n) => `${decimal(n)}%`;
const mb = (n) => (n >= 1024 ? `${decimal(n / 1024)} GB` : `${inteiro(n)} MB`);
const duracao = (s) => (s >= 3600 ? `${decimal(s / 3600)} h` : s >= 60 ? `${Math.round(s / 60)} min` : `${inteiro(s)} s`);
const dataCurta = (iso) => `${iso.slice(8, 10)}/${iso.slice(5, 7)}`;
const horaDe = (iso) => `${iso.slice(8, 10)}/${iso.slice(5, 7)} ${iso.slice(11, 16)}`;

function descreverMomento(data, m) {
  const quando = `${dataCurta(data)} às ${m.hora}`;
  const contexto = `${inteiro(m.abas)} abas, ${inteiro(m.trabalhando)} trabalhando, RAM ${mb(m.ramMb)}, CPU ${pct(m.cpu)}`;
  const o = m.tipo === 'ram' ? `pico de RAM de ${mb(m.valor)}` : m.tipo === 'cpu' ? `CPU em ${pct(m.valor)}` : `interface travada por ${inteiro(m.valor)} ms`;
  const detalhes = [];
  if (m.tipo === 'ram' && m.nexoraMb !== undefined) detalhes.push(`Nexora ${mb(m.nexoraMb)}`);
  if (m.tipo === 'travada') {
    if (m.pausaGcMs !== undefined) detalhes.push(`GC ${inteiro(m.pausaGcMs)} ms`);
    if (m.desdeAbrirS !== undefined) detalhes.push(`${duracao(m.desdeAbrirS)} depois de abrir o app`);
    if (m.abaNovaHaS !== undefined) detalhes.push(`última aba nova há ${duracao(m.abaNovaHaS)}`);
  }
  return { quando, o, contexto: detalhes.length > 0 ? `${contexto}; ${detalhes.join(', ')}` : contexto };
}

function juntarCausas(dias) {
  const porCausa = new Map();
  for (const c of dias.flatMap((d) => d.causas)) {
    const atual = porCausa.get(c.causa);
    if (!atual) porCausa.set(c.causa, { causa: c.causa, ponta: c.ponta, vezes: c.vezes, maiorMs: c.maiorMs, pontaVezes: c.vezes });
    else {
      atual.vezes += c.vezes;
      atual.maiorMs = Math.max(atual.maiorMs, c.maiorMs);
      if (c.vezes > atual.pontaVezes) {
        atual.ponta = c.ponta;
        atual.pontaVezes = c.vezes;
      }
    }
  }
  return [...porCausa.values()]
    .sort((a, b) => b.vezes - a.vezes || b.maiorMs - a.maiorMs || a.causa.localeCompare(b.causa))
    .slice(0, MAXIMO_DE_CAUSAS)
    .map(({ causa, ponta, vezes, maiorMs }) => ({ causa, ponta, vezes, maiorMs }));
}

function descreverCausa(c) {
  const quantas = `${inteiro(c.vezes)} ${c.vezes === 1 ? 'vez' : 'vezes'}, até ${inteiro(c.maiorMs)} ms`;
  return { causa: c.causa, detalhe: c.ponta === c.causa ? quantas : `${quantas}; parada em ${c.ponta}` };
}

export function resumir(relatorio) {
  const dias = relatorio.dias;
  const amostras = dias.reduce((s, d) => s + d.amostras, 0);
  const ponderada = (pegar) => (amostras ? dias.reduce((s, d) => s + pegar(d) * d.amostras, 0) / amostras : 0);
  const maior = (pegar) => dias.reduce((m, d) => Math.max(m, pegar(d)), 0);
  const primeiro = dias[0].data;
  const ultimo = dias[dias.length - 1].data;
  const todos = dias.flatMap((d) => d.momentos.map((m) => ({ data: d.data, ...m })));
  const momentos = TIPOS
    .flatMap((tipo) => todos.filter((m) => m.tipo === tipo).sort((a, b) => b.valor - a.valor).slice(0, 2))
    .sort((a, b) => `${b.data} ${b.hora}`.localeCompare(`${a.data} ${a.hora}`))
    .map((m) => descreverMomento(m.data, m));
  return {
    versao: relatorio.versao,
    sistema: relatorio.sistema,
    computador: `${relatorio.nucleos} núcleos, ${mb(relatorio.memoriaDoPcMb)} de RAM`,
    geradoEm: horaDe(relatorio.geradoEm),
    periodo: primeiro === ultimo ? dataCurta(primeiro) : `${dataCurta(primeiro)} a ${dataCurta(ultimo)}`,
    quantosDias: dias.length,
    ramPico: maior((d) => d.ramMb.maximo),
    ramMedia: ponderada((d) => d.ramMb.media),
    ramNexoraMedia: ponderada((d) => d.ramNexoraMb.media),
    ramNexoraPico: maior((d) => d.ramNexoraMb.maximo),
    cpuMedia: ponderada((d) => d.cpu.media),
    cpuP95: maior((d) => d.cpu.p95),
    travadas: dias.reduce((s, d) => s + d.travadas, 0),
    atrasoMaximo: maior((d) => d.atrasoMs.maximo),
    handles: maior((d) => d.handlesMaximo),
    abas: maior((d) => d.abasMaximo),
    custo: ponderada((d) => d.custoMedioMs),
    divisao: ['nexora', 'sessoes', 'mcps', 'builds', 'outros'].map((parte) => ponderada((d) => d.divisaoMb[parte])),
    dias: dias.map((d) => ({
      data: dataCurta(d.data),
      amostras: inteiro(d.amostras),
      ramMedia: mb(d.ramMb.media),
      ramPico: mb(d.ramMb.maximo),
      cpuMedia: pct(d.cpu.media),
      cpuP95: pct(d.cpu.p95),
      travadas: inteiro(d.travadas),
      atraso: `${inteiro(d.atrasoMs.maximo)} ms`,
      abas: inteiro(d.abasMaximo),
    })),
    momentos,
    causas: juntarCausas(dias).map(descreverCausa),
  };
}

const COLUNAS = [
  ['Dia', 'data'],
  ['Medições', 'amostras'],
  ['RAM média', 'ramMedia'],
  ['RAM pico', 'ramPico'],
  ['CPU média', 'cpuMedia'],
  ['CPU p95', 'cpuP95'],
  ['Travadas', 'travadas'],
  ['Maior atraso', 'atraso'],
  ['Abas', 'abas'],
];

export function montarEmail(relatorio) {
  const r = resumir(relatorio);
  const assunto = `Nexora ${r.versao}: desempenho de ${r.periodo}${r.travadas > 0 ? ` (${r.travadas} travadas)` : ''}`;
  const linhas = [
    `Desempenho do Nexora ${r.versao} (${r.sistema}), ${r.periodo}.`,
    `Computador: ${r.computador}. Gerado em ${r.geradoEm}.`,
    '',
    `RAM total (Nexora + sessões + MCPs + builds): média ${mb(r.ramMedia)}, pico ${mb(r.ramPico)}`,
    `RAM só do Nexora: média ${mb(r.ramNexoraMedia)}, pico ${mb(r.ramNexoraPico)}`,
    `CPU: média ${pct(r.cpuMedia)}, p95 máximo ${pct(r.cpuP95)}`,
    `Interface: ${r.travadas} travadas acima de 250 ms, maior atraso ${inteiro(r.atrasoMaximo)} ms`,
    `Handles no máximo ${inteiro(r.handles)}, até ${inteiro(r.abas)} abas, medição custa ${decimal(r.custo)} ms`,
    `RAM média por parte: ${r.divisao.map((v, i) => `${PARTES[i]} ${mb(v)}`).join(', ')}`,
    '',
    'Por dia',
    ...r.dias.map((d) => COLUNAS.map(([titulo, chave]) => `${titulo} ${d[chave]}`).join(' | ')),
  ];
  if (r.momentos.length > 0) linhas.push('', 'Momentos mais pesados', ...r.momentos.map((m) => `${m.quando}: ${m.o} (${m.contexto})`));
  if (r.causas.length > 0) linhas.push('', 'Onde a interface parou', ...r.causas.map((c) => `${c.causa} (${c.detalhe})`));
  const celula = (v, cabecalho) => `<${cabecalho ? 'th' : 'td'} style="padding:4px 10px;text-align:${cabecalho ? 'left' : 'right'};border-bottom:1px solid #e5e5e5;white-space:nowrap">${v}</${cabecalho ? 'th' : 'td'}>`;
  const html = [
    '<div style="font-family:Segoe UI,Arial,sans-serif;color:#1c1c1c;font-size:14px;line-height:1.5">',
    `<h2 style="margin:0 0 4px">Desempenho do Nexora ${r.versao}</h2>`,
    `<p style="margin:0 0 16px;color:#666">${r.sistema}, ${r.computador}. ${r.periodo}, gerado em ${r.geradoEm}.</p>`,
    '<ul style="padding-left:18px;margin:0 0 16px">',
    `<li>RAM total (Nexora + sessões + MCPs + builds): média <b>${mb(r.ramMedia)}</b>, pico <b>${mb(r.ramPico)}</b></li>`,
    `<li>RAM só do Nexora: média <b>${mb(r.ramNexoraMedia)}</b>, pico <b>${mb(r.ramNexoraPico)}</b></li>`,
    `<li>CPU: média <b>${pct(r.cpuMedia)}</b>, p95 máximo <b>${pct(r.cpuP95)}</b></li>`,
    `<li>Interface: <b>${r.travadas}</b> travadas acima de 250 ms, maior atraso <b>${inteiro(r.atrasoMaximo)} ms</b></li>`,
    `<li>RAM média por parte: ${r.divisao.map((v, i) => `${PARTES[i]} ${mb(v)}`).join(', ')}</li>`,
    `<li>Handles no máximo ${inteiro(r.handles)}, até ${inteiro(r.abas)} abas, a medição custa ${decimal(r.custo)} ms</li>`,
    '</ul>',
    '<table style="border-collapse:collapse;font-size:13px"><tr>',
    ...COLUNAS.map(([titulo]) => celula(titulo, true)),
    '</tr>',
    ...r.dias.map((d) => `<tr>${COLUNAS.map(([, chave]) => celula(d[chave], false)).join('')}</tr>`),
    '</table>',
  ];
  if (r.momentos.length > 0) {
    html.push('<h3 style="margin:20px 0 6px">Momentos mais pesados</h3><ul style="padding-left:18px;margin:0">');
    for (const m of r.momentos) html.push(`<li><b>${m.quando}</b>: ${m.o}<br><span style="color:#666">${m.contexto}</span></li>`);
    html.push('</ul>');
  }
  if (r.causas.length > 0) {
    html.push('<h3 style="margin:20px 0 6px">Onde a interface parou</h3><ul style="padding-left:18px;margin:0">');
    for (const c of r.causas) html.push(`<li><code>${c.causa}</code><br><span style="color:#666">${c.detalhe}</span></li>`);
    html.push('</ul>');
  }
  html.push('</div>');
  return { assunto, texto: linhas.join('\n'), html: html.join('') };
}

function resposta(status, corpo, extras = {}) {
  return new Response(JSON.stringify(corpo), {
    status,
    headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store', ...extras },
  });
}

const DESTINO = /^[^\s@,;<>]+@[^\s@,;<>]+\.[^\s@,;<>]+$/;

export async function onRequestPost({ request, env }) {
  const chave = (env.RESEND_KEY || '').trim();
  const destino = (env.RELATORIO_PARA || '').trim();
  if (!chave || !DESTINO.test(destino)) return resposta(503, { erro: 'nao-configurado' });

  const declarado = Number(request.headers.get('content-length') || 0);
  if (declarado > LIMITE_DO_CORPO) return resposta(413, { erro: 'grande-demais' });
  const bruto = await request.text();
  if (bruto.length > LIMITE_DO_CORPO) return resposta(413, { erro: 'grande-demais' });

  let relatorio;
  try {
    relatorio = validar(JSON.parse(bruto));
  } catch (erro) {
    if (erro instanceof Invalido || erro instanceof SyntaxError) return resposta(400, { erro: 'invalido' });
    throw erro;
  }

  const limite = typeof caches !== 'undefined' ? caches.default : null;
  const chaveDoLimite = new Request(new URL('/api/desempenho/limite', request.url).toString());
  if (limite && (await limite.match(chaveDoLimite))) return resposta(429, { erro: 'muitos-envios' }, { 'retry-after': String(INTERVALO_MINIMO) });

  const email = montarEmail(relatorio);
  let enviado;
  try {
    enviado = await fetch(ENDERECO_DO_RESEND, {
      method: 'POST',
      headers: { authorization: `Bearer ${chave}`, 'content-type': 'application/json' },
      body: JSON.stringify({
        from: (env.RELATORIO_DE || '').trim() || REMETENTE_PADRAO,
        to: [destino],
        subject: email.assunto,
        html: email.html,
        text: email.texto,
      }),
    });
  } catch {
    return resposta(502, { erro: 'falha-no-envio' });
  }
  if (!enviado.ok) return resposta(502, { erro: 'falha-no-envio' });

  if (limite) await limite.put(chaveDoLimite, new Response('1', { headers: { 'cache-control': `max-age=${INTERVALO_MINIMO}` } }));
  return resposta(200, { ok: true });
}

export function onRequest() {
  return resposta(405, { erro: 'metodo' }, { allow: 'POST' });
}
