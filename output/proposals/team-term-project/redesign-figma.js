// The action driver prepends D, R, L, GUARD, HASHES, SELECTED and the exact fingerprint function from the live guard metadata.
if(figma.fileKey!==D.figma.fileKey||figma.editorType!=='slides')throw new Error('Wrong file/editor');
const page=await figma.getNodeByIdAsync('0:1');await figma.setCurrentPageAsync(page);
const slides=figma.getSlideGrid().flat();
if(slides.length!==13)throw new Error('Expected the existing thirteen slides');
for(let i=0;i<13;i++){
  const current=slides[i],guard=GUARD.slides[i];
  if(current.id!==guard.id||current.width!==1920||current.height!==1080)throw new Error('Slide order/dimension changed');
  if(SELECTED.includes(i)){
    const actual=current.children.map(n=>n.id).sort(),expected=[...guard.childIds].sort();
    if(JSON.stringify(actual)!==JSON.stringify(expected))throw new Error('Unowned intervening edit on '+current.id);
    if(fingerprint(current)!==guard.fingerprint)throw new Error('Intervening content/geometry edit on '+current.id);
  }
}
for(const family of ['Noto Sans KR','Inter'])for(const style of ['Black','Bold','Regular'])await figma.loadFontAsync({family,style});
const P=R.palette,S=R.nativeTypeSizes;
for(const k of ['ink','night','paper','mist','accent','accentInk','muted','mutedOnNight','lineLight','lineDark'])if(!/^#[0-9a-f]{6}$/i.test(P[k]))throw new Error('Missing color '+k);
const preservedDiagrams=new Map();
for(const number of [6,7])if(SELECTED.includes(number-1)){const guard=GUARD.slides[number-1];if(guard.antVVectorIds.length!==1||guard.nativeAntVLabelLayerIds.length!==1)throw new Error('Expected one actual AntV composition');const vectors=await figma.getNodeByIdAsync(guard.antVVectorIds[0]),labels=await figma.getNodeByIdAsync(guard.nativeAntVLabelLayerIds[0]);if(vectors.parent.id!==guard.childIds[0]||labels.parent.id!==guard.childIds[0])throw new Error('AntV ownership changed');preservedDiagrams.set(number,{vectors,labels});}
const created=[],removed=[],textRecords=[],imageRecords=[],diagramRecords=[];
const rgb=h=>({r:parseInt(h.slice(1,3),16)/255,g:parseInt(h.slice(3,5),16)/255,b:parseInt(h.slice(5,7),16)/255});
const solid=h=>({type:'SOLID',color:rgb(h)});
function F(p,name,x,y,w,h,fill=null){const n=figma.createFrame();p.appendChild(n);n.name=name;n.resize(w,h);n.x=x;n.y=y;n.fills=fill?[solid(fill)]:[];n.clipsContent=false;created.push(n.id);return n;}
function box(p,name,x,y,w,h,fill=null,stroke=null,radius=0){const n=figma.createRectangle();p.appendChild(n);n.name=name;n.resize(w,h);n.x=x;n.y=y;n.fills=fill?[solid(fill)]:[];if(stroke){n.strokes=[solid(stroke)];n.strokeWeight=2;}n.cornerRadius=radius;created.push(n.id);return n;}
function circle(p,name,x,y,d,fill=null,stroke=null,weight=2){const n=figma.createEllipse();p.appendChild(n);n.name=name;n.resize(d,d);n.x=x;n.y=y;n.fills=fill?[solid(fill)]:[];if(stroke){n.strokes=[solid(stroke)];n.strokeWeight=weight;}created.push(n.id);return n;}
function units(s){let n=0;for(const c of s){if(/[가-힣一-龥]/.test(c))n+=1;else if(/[A-Z0-9]/.test(c))n+=.7;else if(/\s/.test(c))n+=.28;else if(/[a-z]/.test(c))n+=.54;else n+=.5;}return n;}
function T(p,name,value,x,y,w,size=S.body,color=P.ink,style='Regular',h=null,align='LEFT',tracking=0,lineHeightPx=null,fontFamily=null,verticalAlign='TOP'){
  const role=Object.values(R.typography.roles).find(r=>r.sizePx===size),leading=lineHeightPx||(role?role.lineHeightPx:Math.ceil(size*1.32));
  const n=figma.createText();p.appendChild(n);n.name=name;n.fontName={family:fontFamily||(/[가-힣]/.test(value)?'Noto Sans KR':'Inter'),style};n.fontSize=size;n.lineHeight={unit:'PIXELS',value:leading};n.letterSpacing={unit:'PERCENT',value:tracking};n.fills=[solid(color)];n.textAutoResize='NONE';n.textAlignVertical=verticalAlign;
  const lines=value.split('\n').reduce((sum,line)=>sum+Math.max(1,Math.ceil(units(line)*size/w)),0);
  const height=h||Math.ceil((lines+0.25)*leading);n.resize(w,height);n.characters=value;n.x=x;n.y=y;n.textAlignHorizontal=align;
  created.push(n.id);textRecords.push({id:n.id,name,text:value,width:w,height,fontSize:size});return n;
}
function line(p,name,x,y,data,color,weight=3,dash=null){const n=figma.createVector();p.appendChild(n);n.name=name;n.vectorPaths=[{windingRule:'NONZERO',data}];n.x=x;n.y=y;n.fills=[];n.strokes=[solid(color)];n.strokeWeight=weight;if(dash)n.dashPattern=dash;created.push(n.id);return n;}
function arrow(p,name,x,y,w,color,weight=3){return line(p,name,x,y,`M 0 8 L ${w} 8 M ${w-10} 0 L ${w} 8 L ${w-10} 16`,color,weight);}
const iconPaths={
  eye:'<path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z"/><circle cx="12" cy="12" r="3"/>',
  book:'<path d="M3 4h6c2 0 3 1 3 3v14c0-2-1-3-3-3H3V4Zm18 0h-6c-2 0-3 1-3 3v14c0-2 1-3 3-3h6V4Z"/>',
  person:'<circle cx="12" cy="5" r="3"/><path d="M5 22v-5a7 7 0 0 1 14 0v5M8 22v-5m8 5v-5"/>',
  report:'<path d="M6 3h12v18H6V3ZM9 7h6M9 11h6M9 15h4"/>',
  radio:'<path d="M7 8h10v13H7V8ZM8 8V3M10 12h4M10 16h4M10 18h4"/><path d="M12 2a6 6 0 0 1 6 6M12 5a3 3 0 0 1 3 3"/>',
  map:'<path d="m2 5 6-2 8 2 6-2v16l-6 2-8-2-6 2V5ZM8 3v16M16 5v16"/>',
  layers:'<path d="m2 7 10-5 10 5-10 5-10-5Zm0 5 10 5 10-5M2 17l10 5 10-5"/>',
  headset:'<path d="M3 7h18v10h-6l-3-3-3 3H3V7ZM1 9v6M23 9v6"/><circle cx="7" cy="11" r="1"/><circle cx="17" cy="11" r="1"/>',
  tool:'<path d="m15 3 4 4-5 5-3-3 4-6Zm-5 7 4 4-7 7-4-4 7-7ZM3 3l7 7"/>',
  route:'<circle cx="5" cy="5" r="3"/><circle cx="19" cy="19" r="3"/><path d="M8 5h7a4 4 0 0 1 0 8H9a4 4 0 0 0 0 8h7"/>',
  brain:'<path d="M12 4C8 0 3 4 5 8 0 9 1 15 5 16c-1 5 4 8 7 4V4Zm0 0c4-4 9 0 7 4 5 1 4 7 0 8 1 5-4 8-7 4V4ZM5 8l3 2M5 16l4-2M19 8l-3 2M19 16l-4-2"/>'
};
function icon(p,kind,x,y,size,color){const path=iconPaths[kind];if(!path)throw new Error('Unknown pictogram '+kind);const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="${color}" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">${path}</svg>`;const n=figma.createNodeFromSvg(svg);p.appendChild(n);n.name='Native pictogram · '+kind;n.x=x;n.y=y;created.push(n.id);return n;}
function photo(p,key,x,y,w,h,radius=0,scaleMode='FILL'){if(!HASHES[key])throw new Error('Missing uploaded photo '+key);const n=box(p,'Development image · '+key,x,y,w,h,null,null,radius);n.fills=[{type:'IMAGE',imageHash:HASHES[key],scaleMode}];imageRecords.push({id:n.id,key,imageHash:HASHES[key],width:w,height:h,scaleMode});return n;}
function chip(p,label,x,y,w,fill,fg,size=24){const f=F(p,'Information chip · '+label,x,y,w,48,fill);f.layoutMode='HORIZONTAL';f.paddingLeft=16;f.paddingRight=16;f.paddingTop=6;f.paddingBottom=6;f.counterAxisAlignItems='CENTER';const t=T(f,'Chip text',label,0,0,w-32,size,fg,'Bold',36);t.layoutSizingHorizontal='FIXED';t.layoutSizingVertical='FIXED';return f;}
function theme(ground){const dark=ground==='night';return{background:P[ground],fg:dark?P.paper:P.ink,muted:dark?P.mutedOnNight:P.muted,line:dark?P.lineDark:P.lineLight,emphasis:dark?P.accent:P.accentInk,dark};}
function chrome(root,s,plan,th){
  const chapter=plan.chapter,variant=R.headerFooter.variants[plan.ground],ch=chapter===null?null:R.headerFooter.chapters[chapter],code=ch?ch.index:'REF';
  const badge=F(root,'Chapter index',72,40,112,64,P[variant.indexFill]);
  T(badge,'Chapter index text',code,0,0,112,ch?56:40,P[variant.indexText],'Black',64,'CENTER',-2);
  T(root,'Korean section',ch?ch.korean:'참고자료',208,40,376,28,th.fg,'Bold',40);
  T(root,'English section',ch?ch.english:'REFERENCES',608,48,768,24,th.muted,'Regular',32,'LEFT',4);
  T(root,'Product wordmark','CHOOGuard',1512,48,336,28,th.fg,'Bold',40,'RIGHT',-2);
  T(root,'Editorial headline',plan.headline,72,128,1760,S.headline,th.fg,'Bold',176,'LEFT',-1);
  line(root,'Six-topic narrative route',88,992,'M 0 0 L 1040 0',P[variant.connector],4);
  for(let k=0;k<6;k++){
    const active=k===chapter,cx=88+k*208,d=active?24:14;
    circle(root,'Route station '+L.chapters[k],cx-d/2,992-d/2,d,active?P[variant.activeNode]:th.background,active?th.fg:P[variant.inactiveNode],4);
    T(root,'Route topic label',L.chapters[k],72+k*208,1008,176,24,active?P[variant.activeLabel]:th.muted,active?'Bold':'Regular',32);
    if(active)box(root,'Current chapter underline',72+k*208,1044,80,4,P[variant.activeLabel]);
  }
  T(root,'Chapter readout',ch?ch.index+'  '+ch.korean:'참고자료',1320,1000,328,28,th.fg,'Bold',40);
  T(root,'Editorial folio',String(s.number).padStart(2,'0')+' / 13',1696,1000,152,28,th.fg,'Bold',40,'RIGHT');
}
function renderedDiagram(root,number){
  const source=preservedDiagrams.get(number);if(!source)throw new Error('Missing guarded actual AntV composition');
  const vectors=source.vectors.clone(),labels=source.labels.clone();root.appendChild(vectors);root.appendChild(labels);vectors.x=568;vectors.y=328;labels.x=568;labels.y=328;created.push(vectors.id,labels.id);
  diagramRecords.push({slide:number,vectorNodeId:vectors.id,labelFrameId:labels.id,template:vectors.name.replace('AntV vectors · ',''),labelCount:labels.findAll(n=>n.type==='TEXT').length,renderer:'Actual AntV native composition retained',sourceVectorNodeId:source.vectors.id});
}
function leftNarrative(root,heading,body,detail,th){T(root,'Graphic thesis',heading,72,352,408,48,th.fg,'Black',192);T(root,'Graphic explanation',body,72,576,408,32,th.fg,'Regular',176);if(detail)T(root,'Graphic boundary',detail,72,808,408,26,th.muted,'Regular',144);}
for(const i of SELECTED){
  const slide=slides[i],s=D.slides[i],plan=L.pages[i],th=theme(plan.ground),c=s.content;
  const oldChildren=[...slide.children];
  const root=F(slide,'Station Field Atlas · '+plan.family,0,0,1920,1080,th.background);
  if(s.number!==1)chrome(root,s,plan,th);
  switch(s.number){
    case 1:
      box(root,'Cover lime ground plane',1344,0,576,1080,P.accent);
      T(root,'Cover publication','XR-LAB / TERM PROJECT',72,64,1120,26,P.paper,'Bold',44,'LEFT',6);
      T(root,'Cover wordmark','CHOOGuard',72,208,1088,S.display,P.paper,'Black',184,'LEFT',-3);
      T(root,'Cover thesis',s.claim,72,448,752,56,P.paper,'Black',192,'LEFT',-1);
      T(root,'Cover product description','부산역·KTX\n비상대응훈련 VR 프로그램',72,720,640,32,P.mutedOnNight,'Regular',112);
      photo(root,'concept',880,352,1000,616);
      chip(root,'설명용 개념 그림',1456,944,376,P.accent,P.ink);
      T(root,'Cover authorship','CHOOGuard / XR-Lab',72,1008,1056,24,P.mutedOnNight,'Regular',40);
      T(root,'Cover folio','01 / 13',1584,1008,248,28,P.ink,'Bold',40,'RIGHT');
      break;
    case 2:{
      const panel=F(root,'Reality constraint',72,344,536,584,P.night);
      T(panel,'Constraint label',c.problem.title,40,40,456,28,P.accent,'Bold',48);
      T(panel,'Constraint thesis',c.problem.body,40,136,456,40,P.paper,'Black',208);
      T(panel,'Project context',c.context,40,416,456,30,P.mutedOnNight,'Regular',120);
      const xs=[696,1096,1496];
      icon(root,'eye',xs[0]+92,392,160,P.ink);
      line(root,'Judgment diamond',xs[1]+80,376,'M 96 0 L 192 96 L 96 192 L 0 96 Z',P.accentInk,4);
      circle(root,'Judgment focal point',xs[1]+148,444,56,P.accent);
      icon(root,'report',xs[2]+96,392,160,P.ink);
      arrow(root,'Observation to principle',992,472,64,P.lineLight,3);
      arrow(root,'Principle to result',1392,472,64,P.lineLight,3);
      c.goal.steps.forEach((v,k)=>T(root,'Application mode',v,xs[k],608,352,38,k===1?P.accentInk:P.ink,'Black',128,'CENTER'));
      T(root,'Script-to-reality gap',c.gap,696,816,1128,30,P.muted,'Regular',104,'CENTER');
      break;}
    case 3:{
      leftNarrative(root,c.thesis,c.body,c.boundary,th);
      const twin=F(root,'Digital-twin space pillar',568,344,488,592,P.paper);
      T(twin,'Twin pillar label',c.pillars[0].title,24,24,440,32,P.ink,'Black',64);
      photo(twin,'world',24,112,440,247.7,8,'FIT');
      T(twin,'Twin conditions',c.pillars[0].detail,24,392,440,36,P.ink,'Bold',104);
      T(twin,'Twin image provenance','현재 부산역 공간 캡처',24,536,440,24,P.muted,'Regular',40);
      const dynamics=F(root,'State-based situation pillar',1104,344,744,376,P.night);
      T(dynamics,'Dynamic pillar label',c.pillars[1].title,32,24,680,36,P.paper,'Black',72);
      icon(dynamics,'person',32,128,88,P.accent);
      T(dynamics,'Dynamic pillar meaning',c.pillars[1].detail,152,128,560,32,P.paper,'Regular',136);
      T(dynamics,'State interaction rule','승객과 사용자의 행동이 다음 조건을 바꾼다.',32,296,680,28,P.mutedOnNight,'Regular',64);
      line(root,'Two pillars to experience',1056,680,'M 0 0 L 24 0 Q 32 0 32 8 L 32 96 Q 32 104 40 104 L 48 104 M 40 96 L 48 104 L 40 112',P.accentInk,4);
      line(root,'Situation to experience',1464,720,'M 0 0 L 0 24 M -8 16 L 0 24 L 8 16',P.accentInk,4);
      const experience=F(root,'Direct response experience',1104,752,744,184,P.accent);
      T(experience,'Experience output',c.experience,32,24,680,40,P.ink,'Black',64);
      T(experience,'Experience cause',c.experienceDetail,32,104,680,28,P.ink,'Regular',72);
      break;}
    case 4:
      chip(root,'현재 개발 화면',72,336,248,P.accent,P.ink);
      T(root,'Twin construction thesis',c.thesis,72,448,272,36,P.paper,'Black',248);
      T(root,'Twin capture boundary',c.boundary,72,800,272,28,P.mutedOnNight,'Regular',112);
      photo(root,'play',376,328,1120,481.25,8);
      T(root,'Play catalog caption',c.captions.play,376,832,1120,30,P.paper,'Bold',56);
      T(root,'Twin fidelity boundary',c.note,376,904,1120,26,P.mutedOnNight,'Regular',48);
      photo(root,'world',1528,328,320,180.1,8);T(root,'Station caption',c.captions.world,1528,520,320,24,P.paper,'Regular',64);
      photo(root,'door',1528,600,320,140,8,'FIT');T(root,'Door caption',c.captions.door,1528,752,320,24,P.paper,'Regular',40);
      photo(root,'cabin',1528,808,320,132,8,'FIT');T(root,'Cabin caption',c.captions.cabin,1528,952,320,24,P.paper,'Regular',32);
      break;
    case 5:{
      chip(root,'공간 제작 과정',72,336,456,P.ink,P.paper);
      const slabs=c.layers.map((v,k)=>({...v,y:[384,536,688][k],fill:[P.paper,P.ink,P.night][k],fg:k===0?P.ink:P.paper}));
      slabs.forEach((v,k)=>{line(root,'Layer perspective',72,v.y,'M 0 28 L 32 0 L 888 0 L 856 28 L 0 28',P.lineLight,2);const f=F(root,'Twin construction layer '+k,72,v.y+32,856,112,v.fill);T(f,'Layer construction',v.label,32,16,792,34,v.fg,'Black',64);T(f,'Layer method',v.sub,32,76,792,24,k===0?P.muted:P.mutedOnNight,'Regular',40);});
      arrow(root,'Current foundation to planned XR',968,560,112,P.accentInk,3);
      const xr=F(root,'Planned XR development',1144,384,704,504,P.night);
      chip(xr,'VR 개발·확인 목표',32,32,344,P.accent,P.ink);
      icon(xr,'headset',32,128,88,P.accent);
      T(xr,'XR headline','XR 입력과 공간 UI',152,132,520,42,P.paper,'Black',96);
      T(xr,'XR libraries','XR Interaction Toolkit · XR Origin\nXR Interaction Simulator',32,272,640,30,P.paper,'Regular',112);
      T(xr,'Physical-device boundary','HMD·컨트롤러 조작과 사용성 확인',32,400,640,26,P.mutedOnNight,'Regular',80);
      T(root,'Required integrated functions',c.requirements,72,864,856,24,P.muted,'Bold',40);
      T(root,'Current runtime environment',c.shared,72,920,1776,26,P.muted,'Regular',48);
      break;}
    case 6:
      leftNarrative(root,'사람·물건의\n변화를 찾는다.', 'JEV가 가능성을 판단한다.\n코드가 시간에 따라\n변화를 추첨한다.', '적용 전 조건을 확인한다.\n바뀐 상태를 다시 판단한다.',th);
      renderedDiagram(root,6);
      break;
    case 7:
      leftNarrative(root,'승객은 각자의\n정보로 움직인다.', '위치·벽·층·문에 따라\n보고 듣는 정보가 다르다.\n안내에 따라 행동이 바뀐다.', '승객의 이동과 안내가\n다음 상태를 바꾼다.',th);
      renderedDiagram(root,7);
      break;
    case 8:{
      T(root,'Entropy concept boundary',c.qualifier,72,304,1776,24,P.mutedOnNight,'Regular',40);
      const positions=[{x:72,y:352},{x:1224,y:352},{x:72,y:656},{x:1224,y:656}],kinds=['map','eye','tool','report'];
      line(root,'State coupling left top',632,456,'M 0 0 L 120 0 Q 128 0 128 8 L 128 96 Q 128 104 136 104 L 192 104 M 184 96 L 192 104 L 184 112',P.lineDark,3);
      line(root,'State coupling right top',1096,456,'M 0 104 L 56 104 Q 64 104 64 96 L 64 8 Q 64 0 72 0 L 96 0 M 88 -8 L 96 0 L 88 8',P.lineDark,3);
      line(root,'State coupling left bottom',632,664,'M 0 96 L 120 96 Q 128 96 128 88 L 128 8 Q 128 0 136 0 L 192 0 M 184 -8 L 192 0 L 184 8',P.lineDark,3);
      line(root,'State coupling right bottom',1096,664,'M 0 0 L 56 0 Q 64 0 64 8 L 64 88 Q 64 96 72 96 L 96 96 M 88 88 L 96 96 L 88 104',P.lineDark,3);
      circle(root,'Updated world state core',824,472,272,P.paper);
      icon(root,'person',920,508,80,P.accentInk);
      T(root,'Updated state name',c.core,848,608,224,36,P.ink,'Black',104,'CENTER');
      c.rows.forEach((v,k)=>{const p=positions[k];icon(root,kinds[k],p.x,p.y,56,P.accent);T(root,'Diversity factor',v[0],p.x+80,p.y,496,38,P.paper,'Black',64);T(root,'Factor condition',v[1],p.x,p.y+88,552,32,P.paper,'Regular',80);T(root,'Factor effect',v[2],p.x,p.y+184,552,26,P.mutedOnNight,'Regular',64);});
      T(root,'Emergent diversity thesis',c.closing,72,928,1776,28,P.accent,'Bold',40);
      break;}
    case 9:{
      chip(root,'TARGET · 완성 목표',72,320,360,P.accent,P.ink);
      chip(root,c.worldUI.mode,464,320,672,P.ink,P.paper);
      photo(root,'play',72,392,1264,543.1,8);
      const context=F(root,'Spatial observation panel',104,424,512,144,P.night);
      T(context,'Observation title',c.worldUI.context,24,24,464,28,P.paper,'Bold',64);
      T(context,'Observation fields','위치 · 상태 · 안내 정보',24,96,464,24,P.mutedOnNight,'Regular',40);
      line(root,'Target selection ray',756,692,'M 0 176 L 316 0',P.accent,5);
      circle(root,'Target selection focus',1060,680,24,P.night,P.accent,4);
      chip(root,c.worldUI.action,736,600,568,P.accent,P.ink,30);
      c.worldUI.actions.forEach((v,k)=>{const x=104+k*400;const action=F(root,'Target spatial action '+v,x,848,376,56,P.night);icon(action,['radio','tool','route'][k],16,12,32,P.accent);T(action,'Target action label',v,64,8,296,28,P.paper,'Bold',40);});
      const slate=F(root,'Target action-record slate',1416,336,432,616,P.paper);
      T(slate,'Record title',c.recordUI.title,24,24,384,36,P.ink,'Black',72);
      T(slate,'Record fields','수행 순서 · 상태 변화\n관찰·전달 정보',24,112,384,24,P.muted,'Regular',80);
      const detail=['본 상황·들은 정보','사용한 도구·조치','사람·시설의 상태 변화','조치 뒤에 다시 본 상황'];
      c.recordUI.steps.forEach((v,k)=>{T(slate,'Record stage',v,24,216+k*72,384,28,P.ink,'Bold',48);T(slate,'Recorded content',detail[k],24,256+k*72,384,24,P.muted,'Regular',40);});
      box(slate,'Restart action',24,544,384,48,P.ink,null,8);T(slate,'Restart label',c.recordUI.button,32,552,368,26,P.paper,'Bold',40,'CENTER');
      break;}
    case 10:{
      chip(root,c.label,1584,344,264,P.ink,P.paper);
      line(root,'Implementation dependency track',272,432,'M 0 0 L 1344 0',P.lineLight,4);
      c.weeks.forEach((v,k)=>{const x=72+k*448;circle(root,'Proposed milestone',x+184,416,32,P.paper,P.accentInk,4);T(root,'Week typography',v.week,x,336,392,56,P.accentInk,'Black',80);T(root,'Week implementation',v.work,x,488,392,30,P.ink,'Bold',176);T(root,'Artifact label','산출물',x,696,392,24,P.muted,'Regular',40);T(root,'Week artifact',v.output,x,744,392,30,P.ink,'Black',104);T(root,'Completion check',v.check,x,856,392,26,P.muted,'Regular',112);});
      break;}
    case 11:{
      ['LEARN','INTEGRATE','BETA','DELIVER'].forEach((v,k)=>T(root,'Release phase typography',v,72,344+k*144,576,64,P.ink,'Black',96,'LEFT',-2));
      T(root,'Proposed schedule status','8주 제안 일정 · W5–W8',72,920,576,28,P.accentInk,'Bold',64);
      line(root,'Release runway track',704,368,'M 0 0 L 0 536',P.accentInk,4);
      c.weeks.forEach((v,k)=>{const y=344+k*144;circle(root,'Release checkpoint',688,y+24,32,P.accent,P.ink,4);T(root,'Release week',v.week,752,y,104,40,P.ink,'Black',64);T(root,'Release work',v.work,880,y,968,30,P.ink,'Bold',80);T(root,'Release artifact',v.output,880,y+76,448,26,P.ink,'Regular',64);T(root,'Release check',v.check,1360,y+76,488,24,P.accentInk,'Regular',80);});
      T(root,'Final submission components',c.deliverables.join(' · '),752,920,1096,24,P.ink,'Bold',48);
      break;}
    case 12:{
      const terms=c.terms;
      c.rows.forEach((v,k)=>{const x=72+k*608;T(root,'Meaningful outcome display',terms[k],x,368,560,80,k===0?P.accentInk:P.ink,'Black',136);T(root,'Outcome category',v[0],x,512,560,26,P.muted,'Bold',64);T(root,'Expected experience',v[1],x,608,560,32,P.ink,'Regular',144);line(root,'Outcome to observation',x,792,'M 0 0 L 0 56 M -8 46 L 0 56 L 8 46',P.lineLight,3);T(root,'Observation category','관찰할 내용',x+32,776,528,24,P.muted,'Regular',48);T(root,'Evidence to collect',v[2],x+32,824,528,30,P.ink,'Bold',128);});
      break;}
    case 13:{
      const references=F(root,'Technical-library index',72,352,1776,448);references.layoutMode='HORIZONTAL';references.itemSpacing=48;
      c.references.forEach((v,k)=>{const f=F(references,'Linked reference '+(k+1),0,0,560,448);f.layoutMode='VERTICAL';f.itemSpacing=24;f.paddingTop=8;T(f,'Reference number',String(k+1).padStart(2,'0'),0,0,560,48,P.accent,'Black',72);const title=T(f,'Reference title',v.title,0,0,560,32,P.paper,'Black',136);title.layoutSizingVertical='FIXED';const desc=T(f,'Reference purpose',v.detail,0,0,560,28,P.mutedOnNight,'Regular',96);desc.layoutSizingVertical='FIXED';const link=T(f,'Reference link',v.linkLabel,0,0,560,26,P.accent,'Bold',48);link.setRangeHyperlink(0,link.characters.length,{type:'URL',value:v.url});link.layoutSizingVertical='FIXED';});
      T(root,'Project environment source',c.projectSource,72,848,1776,26,P.mutedOnNight,'Regular',64);
      T(root,'Media attribution',c.mediaSource,72,920,1776,26,P.mutedOnNight,'Regular',64);
      break;}
  }
  slide.fills=[solid(th.background)];slide.speakerNotes=s.claim+'\n\n'+s.notes;
  for(const n of oldChildren){removed.push(n.id);n.remove();}
}
const nativeEvidence=[],boundsIssues=[];
for(const i of SELECTED){
  const s=slides[i],b=s.absoluteBoundingBox,all=s.findAll(()=>true),texts=all.filter(n=>n.type==='TEXT');
  for(const n of texts){const r=n.absoluteBoundingBox;if(r&&b&&(r.x<b.x-1||r.y<b.y-1||r.x+r.width>b.x+b.width+1||r.y+r.height>b.y+b.height+1))boundsIssues.push({slide:i+1,id:n.id,text:n.characters,bounds:r});}
  nativeEvidence.push({id:s.id,slide:i+1,layoutFamily:L.pages[i].family,ground:L.pages[i].ground,nativeTextCount:texts.length,nativeVectorCount:all.filter(n=>n.type==='VECTOR').length,frameCount:all.filter(n=>n.type==='FRAME').length,childIds:s.children.map(n=>n.id),headline:L.pages[i].headline,notes:s.speakerNotes});
}
return{fileKey:figma.fileKey,pageId:page.id,slideCount:slides.length,rewrittenSlides:SELECTED.map(i=>i+1),guardReadBack:'Selected children and actual FNV1a subtree fingerprints matched before any mutation',nativeEvidence,imageRecords,diagramRecords,boundsIssues,nativeShapeDiagrams:SELECTED.filter(i=>i===2||i===7).map(i=>i+1),url:'https://www.figma.com/slides/'+figma.fileKey};
