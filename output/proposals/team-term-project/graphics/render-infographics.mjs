// Renderer artifact generator; reuses installed AntV 0.2.20 without installing anything.
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.dirname(fileURLToPath(import.meta.url));
const tools = '/Users/um-yunsang/.omp/agent/tools/design-graphics/node_modules';
const library = await fs.readFile(path.join(tools, '@antv/infographic/dist/infographic.min.js'), 'utf8');
const iconNames = (await fs.readdir(path.join(root, 'icons'))).filter(n => n.endsWith('.svg'));
const icons = Object.fromEntries(await Promise.all(iconNames.map(async n => [n.replace(/^lucide-/, '').replace(/\.svg$/, ''), await fs.readFile(path.join(root, 'icons', n), 'utf8')])));
const source = await fs.readFile(path.join(root, 'antv-kit.jsx'), 'utf8');
const hSource = source.slice(source.indexOf('function h('), source.indexOf('// Contract palette'));
let helperSource = source.slice(source.indexOf('const C ='), source.indexOf('export {'));
helperSource = helperSource.replace(/const hex =[\s\S]*?(?=const FRAME)/, `Object.assign(C, { nightRaised: '#142025', nightLine: '#809390', nightText: '#E1E8E6', paperDeep: '#E1E8E6', mistDeep: '#809390', inkSoft: '#142025' });\n`);
helperSource = helperSource.replace(/const ICON_DIR =[\s\S]*?(?=const nl)/, `A.registerResourceLoader(async cfg => { const m = /^lucide\\/([a-z0-9-]+)$/.exec(String(cfg.data || '')); return m ? A.loadSVGResource(ICONS[m[1]]) : null; });\n`);
const transpiler = new Bun.Transpiler({ loader: 'jsx', target: 'browser', tsconfig: { compilerOptions: { jsx: 'react', jsxFactory: 'h', jsxFragmentFactory: 'Fragment' } } });
const browserKit = transpiler.transformSync(`const A = window.AntVInfographic; const {jsx, Fragment}=A; ${hSource} ${helperSource}\nwindow.K={A,h,Fragment,C,FRAME,P,disc,rrect,tri,icon,T,L,D,nl,frameAnchor,Group};`);
const structures = [];
for (const file of ['structure-journey-route.jsx', 'structure-renderer-variants.jsx']) {
  const s = (await fs.readFile(path.join(root, file), 'utf8')).replace("import * as K from './antv-kit.jsx';", 'const K=window.K;').replace(/export const name = [^;]+;/g, '');
  structures.push(`(()=>{${transpiler.transformSync(s)}})();`);
}
const files = {3:'slide-03-learning-journey',6:'slide-06-state-loop',7:'slide-07-observation-relation'};
const syntaxes = Object.fromEntries(await Promise.all(Object.entries(files).map(async ([s,f])=>[s,await fs.readFile(path.join(root,'dsl',f+'.infographic'),'utf8')])));
const html = `<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>CHOOGuard · AntV diagram render</title><style>@import url('https://fonts.googleapis.com/css2?family=Noto+Sans+KR:wght@400;700&family=Inter:wght@700&display=swap');html,body{margin:0}#container{width:1280px;height:608px}#export{position:fixed;right:4px;bottom:4px}</style></head><body><div id="container"></div><button id="export">SVG 내보내기</button><script>${library}</script><script>const ICONS=${JSON.stringify(icons)};(()=>{${browserKit}})();${structures.join('\n')}
const syntaxes=${JSON.stringify(syntaxes)}; const slide=Number(new URLSearchParams(location.search).get('slide')||3); document.body.style.background={3:'#E1E8E6',6:'#0C1D26',7:'#F4F2EA'}[slide];
const infographic=new AntVInfographic.Infographic({container:'#container',width:1280,height:608,padding:0,editable:false});window.infographic=infographic;window.slide=slide;
window.ready=new Promise((resolve,reject)=>{infographic.on('loaded',()=>resolve(true));infographic.on('error',e=>reject(e));});
(async()=>{await document.fonts.ready; infographic.render(syntaxes[slide]);})();
document.getElementById('export').onclick=async()=>{const link=document.createElement('a');link.href=await infographic.toDataURL({type:'svg'});link.download='slide-'+slide+'.svg';link.click();};
</script></body></html>`;
await fs.writeFile(path.join(root,'preview','antv-infographics.html'),html);
console.log(JSON.stringify({renderer:'@antv/infographic@0.2.20',html:path.join(root,'preview','antv-infographics.html'),templates:['choo-journey-route','choo-state-circular','choo-observation-dagre'],offlineIcons:iconNames.length}));
