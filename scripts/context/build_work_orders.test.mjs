import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {fileURLToPath} from 'node:url';
// The legacy module performs writes on import; do not import it until the pure API exists.
const builder=/export function buildWorkOrders/.test(fs.readFileSync(new URL('./build_work_orders.mjs',import.meta.url),'utf8'))?await import('./build_work_orders.mjs'):{};
const root=fileURLToPath(new URL('../../',import.meta.url));
const read=p=>JSON.parse(fs.readFileSync(root+p,'utf8'));
const item=n=>({number:n,kind:'work_item',parent:null,hardPredecessors:[],inputs:[],oneOfInputs:[],outputs:[],requiredLocks:[],writeScope:[],phaseWriteScopes:{prepare:[],candidate:[],accept:[]}});

test('builder exposes an import-safe pure build without changing inputs',()=>{
 assert.equal(typeof builder.buildWorkOrders,'function');
 const g=read('docs/context/work-graph.json'),a=read('docs/context/source-availability.json'),p=read('docs/context/work-orders/policy.json');
 const before=JSON.stringify([g,a,p]);
 const result=builder.buildWorkOrders(g,a,p);
 assert.equal(result.orders.length,g.items.length);
 assert.match(result.index.scope,new RegExp(String(g.items.length)));
 assert.equal(JSON.stringify([g,a,p]),before);
 assert.deepEqual(result,builder.buildWorkOrders(g,a,p));
 const original=g.items.find(x=>x.oneOfInputs.some(q=>q.alternatives.some(z=>z.additionalInputs?.length)));
 assert.deepEqual(result.orders.find(x=>x.issue===original.number).relations.oneOfGroups,original.oneOfInputs);
});

test('requirement packets and ontology retain exactly sixteen evidence-scoped mappings',()=>{
 const g=read('docs/context/work-graph.json'),a=read('docs/context/source-availability.json'),p=read('docs/context/work-orders/policy.json'),before=JSON.stringify([g,a,p]);
 const result=builder.buildWorkOrders(g,a,p),nodes=result.index['@graph'],ids=new Set(nodes.map(n=>n['@id']));
 const pairs=g.edges.filter(e=>e.relation==='implements').map(e=>[e.from,e.to]).sort();assert.equal(pairs.length,16);
 assert.equal(nodes.filter(n=>n['@type']==='Requirement').length,15);
 assert.deepEqual(nodes.filter(n=>n.relation==='implements').map(n=>[n.from.replace('work:','issue.'),n.to.replace('requirement:','')]).sort(),pairs);
 assert.deepEqual(result.orders.flatMap(o=>o.requirements.map(r=>['issue.'+o.issue,r.id])).sort(),pairs);
 for(const n of nodes.filter(n=>n.relation==='implements')){assert.ok(ids.has(n.from)&&ids.has(n.to));assert.equal(n['@type'],'Relationship');}
 for(const o of result.orders){assert.match(o.readMore.requirements,/--section requirements/);for(const r of o.requirements){assert.ok(r.definition);assert.ok(r.evidence.length);assert.ok(r.evidence.every(e=>e.issue===o.issue));}}
 assert.equal(result.orders.filter(o=>o.requirements.length).length,15);
 assert.equal(result.orders.flatMap(o=>o.requirements).filter(r=>r.mappingStatus==='historical_on_hold').length,2);
 assert.equal(result.ontology['@context'].implements['@type'],'@id');assert.equal(result.ontology['@context'].issueNumbers['@type'],'http://www.w3.org/2001/XMLSchema#integer');
 assert.equal(JSON.stringify([g,a,p]),before);assert.deepEqual(result,builder.buildWorkOrders(g,a,p));
});

test('candidate relations include OR alternatives and transitive extra-input guards',()=>{
 assert.equal(typeof builder.candidateRelations,'function');
 const [a,b,c,d,e]=[1,2,3,4,5].map(item);
 c.outputs=[{id:'artifact:3:contract:candidate',producerPhase:'candidate'}];
 a.oneOfInputs=[{artifact:'or:1:source',consumerPhase:'candidate',alternatives:[{issue:2,artifact:'artifact:2:source:accept',producerPhase:'accept',additionalInputs:[{artifact:'artifact:3:contract:candidate',consumerPhase:'candidate'}]}]}];
 c.inputs=[{fromIssue:4,artifact:'artifact:4:upstream:candidate',consumerPhase:'candidate',producerPhase:'candidate'}];
 const graph={items:[a,b,c,d,e],references:[],edges:[]};
 const rel=builder.candidateRelations(graph).get(1);
 assert.deepEqual(rel.parallelCandidates,[5]);
 assert.equal(rel.authorization,false);
 assert.deepEqual(rel.conditional, [2,3,4]);
});

test('candidate-only conflicts exclude a pair even when acceptance is independent',()=>{
 assert.equal(typeof builder.candidateRelations,'function');
 const [a,b]=[1,2].map(item);
 // Legacy scopes remain supported by conflictReasons; current candidate scopes are isolated.
 for(const x of [a,b]){delete x.phaseWriteScopes;x.writeScope=[{path:'shared.json',mode:'exclusive',phases:['candidate']}];}
 const rel=builder.candidateRelations({items:[a,b],edges:[],references:[]}).get(1);
 assert.deepEqual(rel.parallelCandidates,[]);
 assert.match(rel.conflicts[0].reasons.join(' '),/path:shared.json/);
});

test('source qualification supports exact non-develop refs without promoting local bytes or acceptance',()=>{
 assert.equal(typeof builder.enrichSource,'function');
 const q={ref:'a'.repeat(40),digest:{algorithm:'sha256',value:'b'.repeat(64)},url:'https://github.com/xrlab-dau/CHOOGuard/blob/'+ 'a'.repeat(40)+'/docs/new.md',access:'repository read access',qualification:'reachable source only',limits:'not acceptance'};
 const a={files:{'docs/new.md':{availability:'local_unpublished',qualifiedRefs:[q]}}};
 const s=builder.enrichSource({path:'docs/new.md',availability:'local_unpublished',readWhen:'prepare'},a);
 assert.equal(s.ref,q.ref);assert.deepEqual(s.digest,q.digest);assert.equal(s.declaredAvailability,'local_unpublished');
 assert.equal(s.availability,'qualified_ref');assert.equal(s.qualification,q.qualification);assert.equal(s.accepted,false);
 const ambiguous=structuredClone(a);ambiguous.files['docs/new.md'].qualifiedRefs.push({...q,ref:'c'.repeat(40),url:q.url.replace('a'.repeat(40),'c'.repeat(40))});
 assert.equal(builder.enrichSource({path:'docs/new.md'},ambiguous).ref,null);
});

test('explicit sourceRef and ref must agree with qualified and captured develop references',()=>{
 const ref='a'.repeat(40),other='c'.repeat(40),path='docs/new.md';
 const q={ref,digest:{algorithm:'sha256',value:'b'.repeat(64)},url:`https://github.com/xrlab-dau/CHOOGuard/blob/${ref}/${path}`,access:'repository read access',qualification:'source only',limits:'not acceptance'};
 const qualified={files:{[path]:{availability:'local_unpublished',qualifiedRefs:[q]}}};
 const develop={snapshot:{developSHA:ref},files:{[path]:{availability:'published',develop:true,developGitBlobSha:'d'.repeat(40)}}};
 for(const availability of [qualified,develop]){
  for(const declared of [{sourceRef:ref},{ref},{sourceRef:ref,ref}])assert.equal(builder.enrichSource({path,...declared},availability).ref,ref);
  for(const declared of [{sourceRef:other},{ref:other},{sourceRef:other,ref},{sourceRef:ref,ref:other}]){
   const source=builder.enrichSource({path,...declared},availability);assert.equal(source.ref,null);assert.equal(source.digest,null);assert.notEqual(source.availability,'qualified_ref');assert.notEqual(source.availability,'published');
  }
 }
});

test('phase-specific ordering does not turn future acceptance into candidate dependency',()=>{
 const [a,b,c]=[1,2,3].map(item);
 a.inputs=[{fromIssue:2,consumerPhase:'accept',producerPhase:'accept'}];
 a.oneOfInputs=[{artifact:'or:1:final',consumerPhase:'accept',alternatives:[{issue:3,artifact:'artifact:3:final:accept',producerPhase:'accept'}]}];
 const relations=builder.candidateRelations({items:[a,b,c],references:[],edges:[]});assert.deepEqual(relations.get(1).parallelCandidates,[2,3]);
 a.oneOfInputs[0].consumerPhase='candidate';a.oneOfInputs[0].alternatives[0].guards=[{issue:2,artifact:'artifact:2:guard:candidate',producerPhase:'candidate'}];
 assert.deepEqual(builder.candidateRelations({items:[a,b,c],references:[],edges:[]}).get(1).conditional,[2,3]);
});

test('ontology source nodes retain exact qualification limits, not only a digest',()=>{
 const g=read('docs/context/work-graph.json'),a=read('docs/context/source-availability.json'),p=read('docs/context/work-orders/policy.json');
 const c=g.items[0].context[0];
 const q={ref:'c'.repeat(40),digest:{algorithm:'sha256',value:'d'.repeat(64)},url:`https://github.com/xrlab-dau/CHOOGuard/blob/${'c'.repeat(40)}/${c.path}`,access:'repository read access',qualification:'source only',limits:'not accepted or a live access check'};
 a.files[c.path]={availability:'local_unpublished',qualifiedRefs:[q]};
 const result=builder.buildWorkOrders(g,a,p),source=result.index['@graph'].find(x=>x['@type']==='Source'&&x.path===c.path&&x.ref===q.ref);
 assert.equal(source.qualification,q.qualification);assert.equal(source.qualificationLimits,q.limits);assert.equal(source.accepted,false);assert.deepEqual(source.qualifiedRefs,[q]);
});

test('code pointers participate in source ontology and declared prepare context',()=>{
 const g=read('docs/context/work-graph.json'),a=read('docs/context/source-availability.json'),p=read('docs/context/work-orders/policy.json');
 const x=g.items.find(x=>x.number===91),file='scripts/dev/foundation_load_metrics.py';
 const c=x.codePointers.find(c=>c.path===file);c.readWhen='prepare';
 const result=builder.buildWorkOrders(g,a,p),order=result.orders.find(x=>x.issue===91),s=order.minimumContext.find(x=>x.path===file);
 assert.ok(s);assert.equal(s.accepted,false);assert.equal(s.availability,'qualified_ref');assert.equal(s.selector,c.symbol);
 const work=result.index['@graph'].find(x=>x['@id']==='work:91');
 const selected=work.contextSelections.find(x=>x.selector===c.symbol);assert.ok(selected);
 assert.ok(result.index['@graph'].some(x=>x['@id']===selected.usesSource&&x.path===file&&x.ref===s.ref&&x.accepted===false));
});

test('relationship projection preserves all edge phase and guard metadata',()=>{
 assert.equal(typeof builder.relationshipNode,'function');
 const edge={relation:'context',from:'issue.1',to:'issue.2',consumerPhase:'candidate',producerPhase:'accept',guard:{predicate:'selected'},artifact:'artifact:2:exact:accept',reason:'scoped'};
 const result=builder.relationshipNode(edge,1,s=>s.replace('issue.','work:'));
 assert.deepEqual(result,{...edge,'@id':'cg:relation/1','@type':'Relationship',from:'work:1',to:'work:2'});
});
