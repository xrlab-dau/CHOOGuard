// Run in the actual loaded AntV browser page. Returns raw renderer export,
// normalized vector composition, transparent shape-only SVG and exact native text boxes.
window.exportNativeLayout = async () => {
  const rawURL = await infographic.toDataURL({ type: 'svg' });
  const originalSvg = decodeURIComponent(rawURL.slice(rawURL.indexOf(',') + 1));
  const live = document.querySelector('#container svg');
  const rootBox = live.getBoundingClientRect();
  const matrix = live.getScreenCTM();
  const labels = [...live.querySelectorAll('foreignObject,text')].map((n, i) => {
    const fo = n.tagName === 'foreignObject';
    const content = fo ? n.firstElementChild : n;
    const style = getComputedStyle(content);
    const r = n.getBoundingClientRect();
    const m = n.getScreenCTM();
    const scale = Math.hypot(m.a, m.b);
    const text = fo ? content.innerHTML.replace(/<br\s*\/?\s*>/g, '\n').replace(/<[^>]+>/g, '') : n.textContent;
    return { id: `slide-${slide}-label-${i + 1}`, text, x: r.x - rootBox.x, y: r.y - rootBox.y, width: r.width, height: r.height, fontSize: Number.parseFloat(style.fontSize) * scale, weight: style.fontWeight === 'bold' ? 700 : Number(style.fontWeight) || 400, fontFamily: 'Noto Sans KR', fontStyle: style.fontWeight === 'bold' || Number(style.fontWeight) >= 700 ? 'Bold' : 'Regular', color: fo ? style.color : style.fill, align: (n.getAttribute('data-horizontal-align') || 'LEFT').toLowerCase(), verticalAlign: (n.getAttribute('data-vertical-align') || 'TOP').toLowerCase(), lineHeight: fo ? Number.parseFloat(style.lineHeight) * scale : Number.parseFloat(style.fontSize) * scale * Number(n.getAttribute('line-height') || 1.3), source: 'actual browser DOM measurement' };
  });
  const exported = new DOMParser().parseFromString(originalSvg, 'image/svg+xml').documentElement;
  const ns = 'http://www.w3.org/2000/svg';
  const output = document.createElementNS(ns, 'svg');
  output.setAttribute('xmlns', ns); output.setAttribute('width', '1280'); output.setAttribute('height', '608'); output.setAttribute('viewBox', '0 0 1280 608');
  output.setAttribute('role', 'img'); output.setAttribute('aria-labelledby', `slide-${slide}-title slide-${slide}-desc`);
  const title = document.createElementNS(ns, 'title'); title.id=`slide-${slide}-title`; title.textContent={3:'훈련 경험의 연결',6:'현장 상태와 JEV·코드의 피드백',7:'개인 관찰과 정보에 따른 행동'}[slide]; output.append(title);
  const desc = document.createElementNS(ns, 'desc'); desc.id=`slide-${slide}-desc`; desc.textContent={3:'기본 안내에서 상황 적용을 거쳐 행동 기록을 돌아본다.',6:'특정 사람과 물건의 현재 상태에서 원자 전이 후보를 만들고 JEV 판단과 코드의 시간 추첨·조건 재확인을 거쳐 갱신한 상태를 다시 판단한다.',7:'각 승객은 직접 관찰한 위험, 받은 안내 방송, 자신의 현재 위치와 경로를 바탕으로 판단하고 행동한다.'}[slide]; output.append(desc);
  const defs = document.createElementNS(ns, 'defs');
  exported.querySelectorAll('defs').forEach(d=>[...d.children].forEach(n=>defs.append(n.cloneNode(true))));
  output.append(defs);
  const group = document.createElementNS(ns, 'g'); group.setAttribute('transform', `matrix(${matrix.a} ${matrix.b} ${matrix.c} ${matrix.d} ${matrix.e-rootBox.x} ${matrix.f-rootBox.y})`);
  [...exported.children].filter(n => !['defs','style','title','desc'].includes(n.tagName)).forEach(n=>group.append(n.cloneNode(true)));
  group.querySelectorAll('text,foreignObject,style,image').forEach(n=>n.remove());
  // Export is transparent: no canvas paint. The renderer's local grouping fields remain.
  group.querySelectorAll('[data-element-type="background"]').forEach(n=>n.remove());
  output.append(group);
  // Expand installed Lucide symbol uses into editable vector paths. No external href.
  output.querySelectorAll('use').forEach((u,i)=>{
    const href=u.getAttribute('href')||u.getAttribute('xlink:href')||'';
    const symbol=href.startsWith('#')?[...defs.querySelectorAll('symbol')].find(n=>n.id===href.slice(1)):null;
    if(!symbol) throw new Error(`Unresolved vector pictogram: ${href}`);
    const vb=(symbol.getAttribute('viewBox')||'0 0 24 24').split(/\s+/).map(Number);
    const g=document.createElementNS(ns,'g');
    g.setAttribute('transform',`translate(${u.getAttribute('x')||0} ${u.getAttribute('y')||0}) scale(${Number(u.getAttribute('width'))/vb[2]} ${Number(u.getAttribute('height'))/vb[3]})`);
    const color=slide===6&&i===2?'#142025':u.getAttribute('fill')||'#142025';
    g.setAttribute('color',color);g.setAttribute('fill',color);
    [...symbol.children].forEach(n=>g.append(n.cloneNode(true)));
    g.querySelectorAll('[stroke],[fill]').forEach(n=>{for(const key of ['stroke','fill'])if(n.getAttribute(key)==='currentColor')n.setAttribute(key,color);});
    u.replaceWith(g);
  });
  defs.querySelectorAll('symbol').forEach(n=>n.remove());
  let finalLabels = labels;
  const supplementalVectors = [];
  if (slide === 6) {
    const box = (text,x,y,width,height,size=28,bold=false,align='left') => ({text,x,y,width,height,fontSize:size,weight:bold?700:400,fontStyle:bold?'Bold':'Regular',fontFamily:'Noto Sans KR',color:'#F4F2EA',align,verticalAlign:'top',lineHeight:size*1.25,source:'native editorial placement around actual AntV circular geometry'});
    finalLabels = [box('현재 상태',876,44,376,40,32,true),box('공간·설비·승객',876,92,376,40),box('원자 전이 후보',888,264,368,40,32,true),box('특정 사람·물건의\n가능한 다음 변화',888,312,368,72),box('JEV 판단',440,476,400,40,32,true,'center'),box('곧 일어날 가능성·정도',440,524,400,72,28,false,'center'),box('코드 적용',20,308,396,40,32,true,'right'),box('시간에 따라 추첨\n적용 전 조건 재확인',20,356,396,72,28,false,'right'),box('새 현장 상태',28,60,372,40,32,true,'right'),box('다음 판단에 반영',28,108,372,40,28,false,'right'),box('상태 변화\n다시 판단',548,244,184,76,28,false,'center')];
    // sequence-circular has unconnected sectors. This explicitly closes last -> first.
    const feedback=document.createElementNS(ns,'path'); feedback.setAttribute('d','M 574 72 A 196 196 0 0 1 706 72'); feedback.setAttribute('fill','none');feedback.setAttribute('stroke','#D0F266');feedback.setAttribute('stroke-width','4');feedback.setAttribute('stroke-linecap','round');output.append(feedback);
    const head=document.createElementNS(ns,'path');head.setAttribute('d','M 702 64 L 717 76 L 699 78 Z');head.setAttribute('fill','#D0F266');output.append(head);
    supplementalVectors.push({role:'updated state -> current state feedback link',geometry:'circular arc + native vector arrowhead',reason:'AntV sequence-circular renders sectors but no explicit last-to-first feedback relation',path:'M 574 72 A 196 196 0 0 1 706 72'});
  }
  if (slide === 7) {
    finalLabels.push({text:'개인의 관찰·수신 범위만 반영',x:280,y:548,width:620,height:40,fontSize:28,weight:400,fontStyle:'Regular',fontFamily:'Noto Sans KR',color:'#52635F',align:'center',verticalAlign:'top',lineHeight:35,source:'native scope boundary annotation'});
  }
  finalLabels.forEach((l,i)=>{
    l.id ||= `slide-${slide}-native-${i+1}`;
    const g=document.createElementNS(ns,'g');
    const lines=l.text.split('\n');
    const total=lines.length*l.lineHeight;
    const top=l.y+(l.verticalAlign==='middle'?(l.height-total)/2:0);
    lines.forEach((line,j)=>{const t=document.createElementNS(ns,'text');t.setAttribute('x',l.align==='center'?l.x+l.width/2:l.align==='right'?l.x+l.width:l.x);t.setAttribute('y',top+j*l.lineHeight);t.setAttribute('dominant-baseline','text-before-edge');t.setAttribute('font-family',l.fontFamily);t.setAttribute('font-size',l.fontSize);t.setAttribute('font-weight',l.weight);t.setAttribute('fill',l.color);t.setAttribute('text-anchor',l.align==='center'?'middle':l.align==='right'?'end':'start');t.textContent=line;g.append(t);});
    output.append(g);
  });
  // All references retained for import must point to local vector definitions.
  output.querySelectorAll('[href],[*|href]').forEach(n=>{for(const a of [...n.attributes])if(a.localName==='href'&&!a.value.startsWith('#'))n.removeAttributeNode(a);});
  const composedSvg=new XMLSerializer().serializeToString(output);
  const shape=output.cloneNode(true);shape.querySelectorAll('text,foreignObject,style').forEach(n=>n.remove());
  return {slide,originalSvg,composedSvg,shapeSvg:new XMLSerializer().serializeToString(shape),labels:finalLabels,supplementalVectors,rendererViewBox:live.getAttribute('viewBox'),fontLoaded:document.fonts.check('28px "Noto Sans KR"')};
};
