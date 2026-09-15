import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import vm from 'node:vm';
import {test} from 'node:test';

const generator = fileURLToPath(new URL('./render_pm_map.mjs', import.meta.url));
function packet(issue, predecessors = []) {
  return {issue, '@type': 'WorkItem', title: 'Synthetic task '+issue, directive: 'Synthetic scope',
    assignment: {existingClaim: null}, requiredOutputPaths: [], minimumContext: [], children: [],
    relations: {predecessors, successors: [], parallelCandidates: [], conflicts: []}};
}
function generate(packets) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'chooguard-map-'));
  try {
    fs.mkdirSync(path.join(root, 'scripts/context'), {recursive: true});
    fs.mkdirSync(path.join(root, 'docs/context/work-orders'), {recursive: true});
    fs.copyFileSync(generator, path.join(root, 'scripts/context/render_pm_map.mjs'));
    for (const p of packets) fs.writeFileSync(path.join(root, 'docs/context/work-orders',
      String(p.issue).padStart(3,'0')+'.json'), JSON.stringify(p));
    execFileSync(process.execPath, [path.join(root, 'scripts/context/render_pm_map.mjs')]);
    return fs.readFileSync(path.join(root, 'docs/context/index.html'), 'utf8');
  } finally { fs.rmSync(root, {recursive: true, force: true}); }
}
function scriptOf(html) { return html.match(/<script>([\s\S]*)<\/script>/)[1]; }
function dom() {
  const all=[];
  function element(tag) {
    const e={tag, children:[], textContent:'', value:'', append(...c){this.children.push(...c);},
      replaceChildren(...c){this.children=c;}};
    all.push(e);return e;
  }
  const ids=new Map(['#list','#detail','#search','#kind'].map(id=>[id,element('div')]));
  return {all, document:{createElement:element, createTextNode:text=>({textContent:text}),
    querySelector:id=>ids.get(id)}};
}
test('generated inline JavaScript parses independently of generator syntax', ()=> {
  assert.doesNotThrow(()=>new vm.Script(scriptOf(generate([packet(46)]))));
});
test('task and dependency counts are calculated from the supplied packets', ()=> {
  const html=generate([packet(46,[{issue:21,consumerPhase:'candidate',producerPhase:'candidate',artifact:'schema'}]),packet(99)]);
  assert.match(html,/2개 작업 · 단계 의존성 1개/);
  assert.doesNotMatch(html,/101개 작업|381개/);
});
test('detail renders three separate copyable commands for the selected issue', ()=> {
  const {all,document}=dom();
  vm.runInNewContext(scriptOf(generate([packet(46)])), {document,window:{},location:{hash:'#46'}});
  const commands=all.find(e=>e.tag==='pre'&&e.textContent.includes('--section inputs'))?.textContent;
  assert.deepEqual(commands?.split('\n'), ['inputs','writes','checks'].map(s=>
    'node scripts/context/task_context.mjs brief --issue 46 --section '+s+' --phase candidate'));
});
test('packet text cannot terminate the inline script', ()=> {
  const p=packet(46);p.title='</script><script>throw new Error("injected")</script>';
  const html=generate([p]);
  assert.equal((html.match(/<script>/g)||[]).length,1);
  assert.doesNotThrow(()=>new vm.Script(scriptOf(html)));
});
