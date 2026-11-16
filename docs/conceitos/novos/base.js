const P=new URLSearchParams(location.search);const C=P.get('c')||'mesa';const T=P.get('t')||'inicio';
const I={
busca:'<circle cx="11" cy="11" r="7"/><path d="m20 20-4-4"/>',
mais:'<path d="M12 5v14M5 12h14"/>',
pasta:'<path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>',
branch:'<circle cx="6" cy="5" r="2"/><circle cx="6" cy="19" r="2"/><circle cx="18" cy="8" r="2"/><path d="M6 7v10M18 10c0 4-6 3-6 7"/>',
issue:'<circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="1.5"/>',
camadas:'<path d="m12 3 9 5-9 5-9-5z"/><path d="m3 13 9 5 9-5"/>',
orq:'<circle cx="6" cy="6" r="2.5"/><circle cx="18" cy="18" r="2.5"/><circle cx="18" cy="6" r="2.5"/><path d="M8.5 6h7M6 8.5v4a5 5 0 0 0 5 5h4.5"/>',
relogio:'<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
grafico:'<path d="M4 20V10M10 20V4M16 20v-7M22 20H2"/>',
chip:'<rect x="6" y="6" width="12" height="12" rx="2"/><path d="M9 2v4M15 2v4M9 18v4M15 18v4M2 9h4M2 15h4M18 9h4M18 15h4"/>',
paleta:'<circle cx="12" cy="12" r="9"/><circle cx="8" cy="10" r="1"/><circle cx="12" cy="7" r="1"/><circle cx="16" cy="10" r="1"/>',
foco:'<circle cx="12" cy="12" r="3"/><path d="M3 8V5a2 2 0 0 1 2-2h3M16 3h3a2 2 0 0 1 2 2v3M21 16v3a2 2 0 0 1-2 2h-3M8 21H5a2 2 0 0 1-2-2v-3"/>',
terminal:'<path d="m5 8 4 4-4 4M12 16h7"/>',
git:'<circle cx="6" cy="6" r="2"/><circle cx="6" cy="18" r="2"/><circle cx="18" cy="12" r="2"/><path d="M6 8v8M8 6h4a4 4 0 0 1 4 4"/>',
arquivo:'<path d="M14 3H6v18h12V7z"/><path d="M14 3v4h4"/>',
painel:'<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M15 4v16"/>',
seta:'<path d="M12 19V5M5 12l7-7 7 7"/>',
check:'<path d="m5 12 5 5 9-10"/>',
play:'<path d="M7 5v14l11-7z"/>',
replay:'<path d="M3 12a9 9 0 1 0 3-6.7L3 8"/><path d="M3 3v5h5"/>',
sol:'<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>',
anexo:'<path d="m21 11-8.5 8.5a5 5 0 0 1-7-7L14 4a3.5 3.5 0 0 1 5 5l-8.5 8.5a2 2 0 0 1-3-3L15 7"/>',
mic:'<rect x="9" y="3" width="6" height="11" rx="3"/><path d="M5 11a7 7 0 0 0 14 0M12 18v3"/>',
pr:'<circle cx="6" cy="6" r="2"/><circle cx="6" cy="18" r="2"/><circle cx="18" cy="18" r="2"/><path d="M6 8v8M18 16V9a3 3 0 0 0-3-3h-4M13 4l-2 2 2 2"/>',
modelo2:'<rect x="4" y="4" width="16" height="16" rx="3"/><path d="M8 9h8M8 13h5"/>',
};
const ic=(n,s=16)=>`<svg class="ic" viewBox="0 0 24 24" style="width:${s}px;height:${s}px">${I[n]}</svg>`;
const COR={precisa:'var(--laranja)',trab:'var(--ciano)',pronta:'var(--verde)',parada:'var(--txt3)'};
const ROT={precisa:'Precisa de você',trab:'Trabalhando',pronta:'Pronta',parada:'Parada'};
const ORDEM=['precisa','trab','pronta','parada'];

function mascote(cor,estado,px=4){
 const g=['..####..','.######.','##E##E##','########','########','.#.##.#.'];
 let r='';
 g.forEach((l,y)=>[...l].forEach((ch,x)=>{
  if(ch==='#')r+=`<rect x="${x}" y="${y+2}" width="1.02" height="1.02" fill="${cor}"/>`;
  if(ch==='E'){
   if(estado==='parada')r+=`<rect x="${x}" y="${y+2}" width="1.02" height="1.02" fill="${cor}"/><rect x="${x}" y="${y+2.55}" width="1" height=".3" fill="#1a1716"/>`;
   else if(estado==='pronta')r+=`<rect x="${x}" y="${y+2}" width="1.02" height="1.02" fill="${cor}"/><rect x="${x}" y="${y+2.2}" width="1" height=".4" fill="#1a1716"/>`;
   else r+=`<rect x="${x}" y="${y+2}" width="1.02" height="1.02" fill="#1a1716"/>`+(estado==='trab'?`<rect x="${x+.55}" y="${y+2.15}" width=".35" height=".35" fill="#fff"/>`:`<rect x="${x+.1}" y="${y+2.1}" width=".35" height=".35" fill="#fff"/>`);
  }
 }));
 if(estado==='precisa')r+=`<rect x="8.4" y="0" width=".9" height="2.2" fill="#F0A860"/><rect x="8.4" y="2.7" width=".9" height=".8" fill="#F0A860"/>`;
 if(estado==='trab')r+=`<rect x="-1.2" y="1" width=".7" height=".7" fill="#86C5E6"/><rect x="-.4" y="0" width=".5" height=".5" fill="#86C5E6" opacity=".6"/>`;
 if(estado==='parada')r+=`<text x="8.3" y="2" font-size="2.2" fill="#8F8A83" font-family="monospace">z</text>`;
 if(estado==='pronta')r+=`<rect x="3" y="6.2" width="2" height=".35" fill="#1a1716"/>`;
 return `<svg viewBox="-1.5 -0.5 11.5 9" width="${11.5*px}" height="${9*px}" style="shape-rendering:crispEdges;overflow:visible;${estado==='parada'?'opacity:.6':''}">${r}</svg>`;
}
function anel(conteudo,pct,cor,tam=56,esp=3){const rr=tam/2-esp;const c=2*Math.PI*rr;return `<div class="anel" style="width:${tam}px;height:${tam}px"><svg class="r" width="${tam}" height="${tam}"><circle cx="${tam/2}" cy="${tam/2}" r="${rr}" stroke="rgba(255,255,255,.09)" stroke-width="${esp}" fill="none"/><circle cx="${tam/2}" cy="${tam/2}" r="${rr}" stroke="${cor}" stroke-width="${esp}" fill="none" stroke-linecap="round" stroke-dasharray="${c*pct/100} ${c}" transform="rotate(-90 ${tam/2} ${tam/2})"/></svg>${conteudo}</div>`;}

const S=[
 {id:1,proj:'Acme.Shop',sig:'SP',cor:'#5d86e0',branch:'assistente-exemplos-20261106',issue:'#3384',titulo:'"O que posso pedir?" no assistente IA',estado:'precisa',ctx:46,tempo:'esperando há 3 min',ultima:'Quero aplicar a migração no indio-local para testar o popup de exemplos.',cmd:['dotnet ef database update','  --project Acme.Shop.EntityFrameworkCore']},
 {id:2,proj:'Nexora',sig:'N',cor:'#a197c4',branch:'visual-tour-fixes',titulo:'Ajustes do passeio visual',estado:'trab',ctx:62,tempo:'há 8 min',ultima:'Rodando os testes headless do quadro…',ferr:'dotnet test tests/Nexora.Testes',sub:2},
 {id:3,proj:'Acme.Shop-melhorias',sig:'SP',cor:'#5d86e0',branch:'erros-coletores-20261106',issue:'#3383',titulo:'Data inicial maior que a final',estado:'trab',ctx:31,tempo:'há 21 min',ultima:'Achei onde o limite de meses se perdeu: o PR #3201 tirou o Take(3).',ferr:'Grep  "MesesAFrente"'},
 {id:4,proj:'Toca',sig:'T',cor:'#7d8cf5',branch:'main',titulo:'Toca.exe --apps',estado:'pronta',ctx:7,tempo:'há 6 min',ultima:'Testei o --apps: lista os apps sem abrir a janela.',diff:[['Program.cs',18,2],['Apps.cs',52,7],['Ajuda.cs',9,3],['README.md',7,0]]},
 {id:5,proj:'Senna',sig:'S',cor:'#e88a4a',branch:'main',titulo:'Busca por trechos no Second Brain',estado:'pronta',ctx:24,tempo:'há 40 min',ultima:'A busca agora devolve os 5 melhores trechos com a nota de origem.',diff:[['senna_lembrar.py',27,8],['busca.py',4,1]]},
 {id:6,proj:'Smartoca',sig:'Sm',cor:'#e3c14f',branch:'main',titulo:'Nova conversa',estado:'parada',ctx:0,tempo:'ontem',ultima:''},
];
const PROJ=[['Nexora','N','#a197c4',1,'trab'],['Acme.Shop','SP','#5d86e0',2,'precisa'],['Toca','T','#7d8cf5',1,'pronta'],['Senna','S','#e88a4a',1,'pronta'],['Smartoca','Sm','#e3c14f',1,'parada'],['Acme.Shop.App','PA','#3fa3e0',0,''],['Banditboard','B','#8f8a83',0,''],['Portfolio','Pf','#c48ab0',0,'']];
const icp=(sig,cor,t=30,rad=9)=>`<div style="width:${t}px;height:${t}px;border-radius:${rad}px;background:linear-gradient(180deg,${cor},color-mix(in srgb,${cor} 55%,#000));display:grid;place-items:center;font:700 ${t*.4}px/1 var(--ui);color:#fff;box-shadow:inset 0 1px 0 rgba(255,255,255,.3);flex:none">${sig}</div>`;
function diffMini(d){return d.map(([f,m,r])=>{const v=Math.round(5*m/(m+r));return `<div style="display:flex;justify-content:space-between;align-items:center;font-size:11.8px"><span class="mono suave">${f}</span><span style="display:flex;gap:8px;align-items:center"><span style="color:var(--verde)">+${m}</span><span style="color:var(--verm)">−${r}</span><span class="diffbar">${Array.from({length:5},(_,i)=>`<i class="${i<v?'m':'r'}"></i>`).join('')}</span></span></div>`}).join('');}
const medidores=()=>`<div class="medidores"><div class="medidor"><span class="fraco">5h</span> <b>1%</b><div class="trilho"><i style="width:2%"></i></div></div><div class="medidor"><span class="fraco">Semana</span> <b>57%</b><div class="trilho"><i style="width:57%"></i></div></div></div>`;
const janbtns=()=>`<div class="janbtns"><span>—</span><span>▢</span><span>✕</span></div>`;
const mascotesRodape=()=>`<div class="mascotes">${[[60,'#a197c4','trab'],[380,'#5d86e0','precisa'],[760,'#7d8cf5','pronta'],[1100,'#e88a4a','pronta'],[1360,'#e3c14f','parada']].map(([x,c,e])=>`<div style="position:absolute;left:${x}px;bottom:0">${mascote(c,e,2.4)}</div>`).join('')}</div>`;

function conversa(compacta){return `
 <div style="display:flex;flex-direction:column;gap:12px;font-size:13.4px">
  <div class="bolha">Retome o handoff "Handoff: visual-tour-fixes".</div>
  <div class="suave" style="max-width:92%">Li o handoff. Faltavam dois ajustes: o clique <b style="color:var(--txt)">interceptado</b> no filtro do quadro e a foto do modo Lista, que saía cortada em 1440 px.</div>
  <div class="ferr">${ic('arquivo',14)}<b>Read</b><span class="mono fraco">src/Nexora/Views/QuadroView.axaml</span></div>
  <div class="ferr">${ic('arquivo',14)}<b>Edit</b><span class="mono fraco">src/Nexora/Componentes.axaml</span><span style="margin-left:auto;color:var(--verde)">+12</span><span style="color:var(--verm)">−3</span><span class="btn peq">Ver diff</span></div>
  <div class="subag">${mascote('#a197c4','pronta',2.2)}<div style="flex:1"><b>Subagent · Haiku</b> <span class="fraco">passeio do quadro, 3 fotos</span><div class="fraco" style="font-size:12px">Nenhum alerta. O filtro abre no primeiro clique.</div></div><span class="chip">${ic('check',12)} feito</span></div>
  ${compacta?'':`<div class="suave">O filtro estava atrás da camada do popup de visões. Movi o popup para o nível da janela e conferi nas fotos.</div>`}
  <div class="ferr" style="border-color:rgba(134,197,230,.35);background:rgba(134,197,230,.07)"><span class="ponto" style="background:var(--ciano)"></span><b>Bash</b><span class="mono fraco">dotnet test tests/Nexora.Testes</span><span style="margin-left:auto" class="fraco mono">0:42</span></div>
 </div>`;}
const composer=()=>`<div class="composer">
 <div class="fila"><span class="etq">NA FILA</span> Depois roda o passeio do modo Lista de novo<span style="margin-left:auto" class="fraco">Mandar agora · Editar · ✕</span></div>
 <div class="fraco">Responder ao Claude…  <span class="kbd">@</span> arquivo  <span class="kbd">/</span> comandos</div>
 <div class="rod"><span class="fraco">${ic('anexo',15)}</span><span class="fraco">${ic('mic',15)}</span><span class="chip" style="color:var(--txt)">Opus 5.5 · Alto</span><span class="chip">Aceitar edições</span><span class="enviar">${ic('seta',15)}</span></div></div>`;
const cabSessao=(s,tam,extra)=>`<div style="display:flex;gap:14px;align-items:center;padding:15px 22px;border-bottom:1px solid var(--borda2)">${anel(mascote(s.cor,'trab',tam/18),62,'var(--ciano)',tam,3)}
 <div><div style="font-size:17px;font-weight:650">${s.titulo}</div><div class="fraco" style="display:flex;gap:12px;font-size:12px;align-items:center">${ic('pasta',12)} Nexora <span class="mono">${ic('branch',12)} visual-tour-fixes</span><span>contexto 62%</span><span>cache 4 min</span></div></div>
 <div style="margin-left:auto;display:flex;gap:6px" class="suave"><span class="btn peq">${ic('arquivo',13)}</span><span class="btn peq">${ic('git',13)}</span><span class="btn peq">${ic('terminal',13)}</span><span class="btn peq">${ic('replay',13)} Replay</span>${extra||''}</div></div>`;

function ceu(){const cv=document.getElementById('ceu'),x=cv.getContext('2d');let sd=7;const rnd=()=>(sd=(sd*9301+49297)%233280)/233280;
 x.fillStyle='#121110';x.fillRect(0,0,1440,900);
 const g=(cx,cy,r,c)=>{const gr=x.createRadialGradient(cx,cy,0,cx,cy,r);gr.addColorStop(0,c);gr.addColorStop(1,'rgba(0,0,0,0)');x.fillStyle=gr;x.fillRect(0,0,1440,900);};
 g(260,120,700,'rgba(148,139,174,.20)');g(1250,820,650,'rgba(91,84,112,.26)');g(900,420,500,'rgba(93,134,224,.07)');
 for(let i=0;i<(C==='constelacao'?260:130);i++){x.fillStyle=`rgba(255,255,255,${.15+rnd()*.5})`;x.beginPath();x.arc(rnd()*1440,rnd()*900,rnd()*1.2+.2,0,7);x.fill();}
 if(C==='constelacao'){x.strokeStyle='rgba(255,255,255,.035)';for(let i=0;i<40;i++){x.beginPath();x.moveTo(rnd()*1440,rnd()*900);x.lineTo(rnd()*1440,rnd()*900);x.stroke();}}
}
