import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import {conflictReasons,validateGraph,requirementProjection,inputProjections,conditionalNeighborhood,PHASES} from './work_graph.mjs';
const hash=x=>createHash('sha256').update(x).digest('hex');
const num=s=>Number(String(s).replace(/^issue\./,''));
const text=x=>typeof x==='string'&&x.trim().length>0;

// 문자열 code pointer도 candidate 문맥으로 정규화하고 같은 출처 자격 검사를 적용한다.
export function normalizeSource(c){const value=typeof c==='string'?{path:c}:c;return {...value,selector:value.selector??value.symbol,readWhen:value.readWhen??'candidate'};}
export function sourceSelections(item){return [...(item.context??[]),...(item.codePointers??[])].map(normalizeSource);}
export function sourceInPhase(c,phase){return !phase||c.readWhen==='all'||c.readWhen===phase||Array.isArray(c.readWhen)&&c.readWhen.includes(phase);}

// Qualification describes these remote bytes only, never local bytes, acceptance or permission.
export function enrichSource(c,a){
 c=normalizeSource(c);
 const s=a.files?.[c.path]??{},declared=c.availability??s.availability??'unknown';
 const qualifiedRefs=structuredClone(s.qualifiedRefs??[]);
 const valid=q=>/^[a-f0-9]{40}$/.test(q?.ref)&&q?.digest?.algorithm==='sha256'&&/^[a-f0-9]{64}$/.test(q.digest.value)&&text(q.access)&&text(q.qualification)&&text(q.limits)&&q.url===`https://github.com/xrlab-dau/CHOOGuard/blob/${q.ref}/${c.path}`;
 const matchesDeclaredRef=ref=>[c.sourceRef,c.ref].every(expected=>expected===undefined||expected===null||expected===ref);
 const matches=qualifiedRefs.filter(q=>valid(q)&&matchesDeclaredRef(q.ref));
 const selected=matches.length===1?matches[0]:null;
 const published=!qualifiedRefs.length&&declared==='published'&&s.develop===true&&/^[a-f0-9]{40}$/.test(s.developGitBlobSha)&&/^[a-f0-9]{40}$/.test(a.snapshot?.developSHA)&&matchesDeclaredRef(a.snapshot.developSHA);
 return {path:c.path,selector:c.selector,readWhen:c.readWhen,why:c.proves,limits:c.limits,declaredAvailability:declared,availability:selected?'qualified_ref':published?'published':declared==='published'?'unverified':declared,provider:selected?'qualified-source-manifest':published?'git-at-captured-ref':'qualified-source-manifest',ref:selected?.ref??(published?a.snapshot.developSHA:null),digest:selected?.digest??(published?{algorithm:'git-blob-sha1',value:s.developGitBlobSha}:null),url:selected?.url??(published?`https://github.com/xrlab-dau/CHOOGuard/blob/${a.snapshot.developSHA}/${c.path}`:null),access:selected?.access??(published?'repository read access':'Do not read unqualified local bytes. Obtain exact path/ref/digest/access from the applicable input before this consuming phase.'),qualification:selected?.qualification??null,qualificationLimits:selected?.limits??null,qualifiedRefs,accepted:false};
}

// OR-union reachability is conservative: it excludes possible ordering, not an AND prerequisite.
export function candidateRelations(g){
 const byId=new Map(g.items.map(x=>[x.number,x])),hard=new Map(),possible=new Map();
 const add=(map,c,cp,p,pp)=>{const key=`${c}@${cp}`;if(!map.has(key))map.set(key,new Set());map.get(key).add(`${p}@${pp}`);};
 const both=(...args)=>{add(hard,...args);add(possible,...args);};
 const artifact=new Map([...g.items,...(g.references??[])].flatMap(x=>(x.outputs??[]).map(o=>[o.id,{issue:x.number,producerPhase:o.producerPhase}])));
 for(const x of g.items){
  both(x.number,'candidate',x.number,'prepare');both(x.number,'accept',x.number,'candidate');
  for(const [phase,p] of Object.entries(inputProjections(g,x.number))){
   for(const d of p.hard)both(x.number,phase,d.issue,d.producerPhase);
   for(const d of p.declared)if(Number.isInteger(d.fromIssue))both(x.number,phase,d.fromIssue,d.producerPhase);
  }
  for(const group of conditionalNeighborhood(g,x.number).asConsumer)for(const alternative of group.alternatives){
   const selected=alternative.selectedAlternative;add(possible,x.number,group.consumerPhase,selected.issue,selected.producerPhase);
   for(const guard of alternative.guards)add(possible,x.number,guard.consumerPhase??group.consumerPhase,guard.issue,guard.producerPhase);
   for(const input of alternative.additionalInputs){const producer=artifact.get(input.artifact);if(!producer)throw Error('Unresolved conditional artifact: '+input.artifact);add(possible,x.number,input.consumerPhase??group.consumerPhase,producer.issue,producer.producerPhase);}
  }
 }
 for(const e of g.edges??[])if(e.relation==='requires')both(num(e.from),e.consumerPhase,num(e.to),e.producerPhase);
 const closure=map=>{const memo=new Map();return start=>{if(!memo.has(start)){const seen=new Set(),todo=[...(map.get(start)??[])];while(todo.length){const n=todo.pop();if(seen.has(n))continue;seen.add(n);todo.push(...(map.get(n)??[]));}memo.set(start,seen);}return memo.get(start);};};
 const hardReach=closure(hard),possibleReach=closure(possible);
 const related=(reach,x,y)=>reach(`${x}@candidate`).has(`${y}@candidate`)||reach(`${y}@candidate`).has(`${x}@candidate`);
 const parentOf=(x,y)=>{const seen=new Set();let at=x.parent;while(at){if(at===y.number)return true;if(seen.has(at))throw Error('Parent cycle');seen.add(at);at=byId.get(at)?.parent;}return false;};
 const result=new Map();
 for(const x of g.items){const parallelCandidates=[],conditional=[],conflicts=[];
  for(const y of g.items){if(x.number===y.number)continue;
   const candidate=conflictReasons(x,y,{phaseA:'candidate',phaseB:'candidate'}),accept=conflictReasons(x,y,{phaseA:'accept',phaseB:'accept'});
   if(candidate.length||accept.length)conflicts.push({issue:y.number,reasons:[...new Set([...candidate,...accept])],phases:{candidate,accept}});
   if(x.kind!=='work_item'||y.kind!=='work_item'||candidate.length||parentOf(x,y)||parentOf(y,x))continue;
   if(related(hardReach,x.number,y.number))continue;
   if(related(possibleReach,x.number,y.number)||possibleReach(`${x.number}@candidate`).has(`${x.number}@candidate`)||possibleReach(`${y.number}@candidate`).has(`${y.number}@candidate`))conditional.push(y.number);
   else parallelCandidates.push(y.number);
  }
  parallelCandidates.sort((u,v)=>Number(byId.get(v)?.parent===x.parent)-Number(byId.get(u)?.parent===x.parent)||Math.abs(u-x.number)-Math.abs(v-x.number)||u-v);
  result.set(x.number,{parallelCandidates,conditional,conflicts,phase:'candidate',authorization:false});
 }
 return result;
}
export function relationshipNode(e,index,iri){return {...structuredClone(e),'@id':'cg:relation/'+index,'@type':'Relationship',from:iri(e.from),to:iri(e.to)};}

export function buildWorkOrders(g,a,policy){
 g=structuredClone(g);a=structuredClone(a);policy=structuredClone(policy);
const valid=validateGraph(g);if(!valid.valid)throw Error(valid.errors.join('; '));
const byId=new Map(g.items.map(x=>[x.number,x])),phases=PHASES,relations=candidateRelations(g),requirements=requirementProjection(g);
const cleanTitle=x=>'['+x.workId+'] '+(policy.titleOverrides?.[x.number]??x.title.replace(/^\[[^\]]+\]\s*/,''));
const source=c=>enrichSource(c,a);
const orders=[];const sourceNodes=new Map(),resourceNodes=new Map();
const sourceId=s=>'source:'+hash(JSON.stringify([s.path,s.ref,s.digest,s.availability,s.qualifiedRefs])).slice(0,20);
const resourceId=w=>'resource:'+hash(w.physicalBinding??w.path).slice(0,20);
for(const x of g.items){
 const predecessors=structuredClone(x.hardPredecessors),successors=structuredClone(x.hardSuccessors);
 const {conflicts,parallelCandidates:parallel,conditional}=relations.get(x.number);
 const sources=sourceSelections(x).map(source),readable=c=>['published','qualified_ref'].includes(c.availability),firstRead=sources.filter(c=>readable(c)&&sourceInPhase(c,'prepare')).slice(0,4);
 const outputPaths=[...new Set(x.outputs.map(o=>o.path))];
 const p={schemaVersion:1,'@context':'./ontology.jsonld','@id':'work:'+x.number,'@type':x.kind==='goal'?'Goal':'WorkItem',issue:x.number,workId:x.workId,domainCode:policy.domains[x.number]??'XR',title:cleanTitle(x),orchestrator:'role:pm',executionMode:x.kind==='goal'?'PM aggregates child deliverables; members and their LLMs execute child work.':'A member and their LLM execute this bounded task.',assignment:{policy:policy.assignment,preassigned:false,existingClaim:policy.claims[x.number]??null},environment:policy.environment,directive:policy.goalOverrides[x.number]??x.outcome,parent:x.parent??null,children:g.items.filter(y=>y.parent===x.number).map(y=>y.number),requiredOutputPaths:outputPaths,flow:{prepare:'Read this packet and only its current-phase sources. Identify missing inputs, plan exact changes and tests. Do not claim another member’s work.',candidate:'After candidate-phase inputs are qualified, claim exact paths/branch and perform the implementation or verification requested here.',accept:'Run every required check, preserve failed/unrun cases, return evidence. PM evaluates handoff; a document or checkbox alone is not completion.'},relations:{predecessors,successors,oneOfGroups:structuredClone(x.oneOfInputs),parallelCandidates:parallel,conditionalCandidates:conditional,parallelPhase:'candidate',authorization:false,parallelCondition:'Candidate-phase plan only. Required inputs must be available, actual branches/checkouts and mutable resources must be separate, and exact write claims must not overlap. This is not a live lease or an assignment.',conflicts},minimumContext:firstRead,readMore:{sources:'node scripts/context/task_context.mjs brief --issue '+x.number+' --section sources --phase prepare',inputs:'node scripts/context/task_context.mjs brief --issue '+x.number+' --section inputs --phase candidate',writes:'node scripts/context/task_context.mjs brief --issue '+x.number+' --section writes --phase candidate',checks:'node scripts/context/task_context.mjs brief --issue '+x.number+' --section checks --phase candidate',history:'node scripts/context/task_context.mjs brief --issue '+x.number+' --section history'},handoff:['workId and phase','branch/commit and changed paths','input and output refs/digests','checks actually run and their result','failed/unrun items and current blockers','successor handoff and claim release'],technicalContract:{path:'docs/context/work-graph.json',selector:'items[number='+x.number+']',scope:'Exact phase inputs, artifact qualifications, write/resource contracts, checks and evidence. Historical capture/review fields are not current assignments or a mandatory review loop.'},sourceSnapshot:{capturedAt:g.snapshot.capturedAt,liveState:'Read GitHub Project at actual start; this packet grants no live lease or blanket external-action permission.'}};
 p.requirements=requirements.byIssue[x.number];
 p.readMore.requirements='node scripts/context/task_context.mjs brief --issue '+x.number+' --section requirements';
 orders.push(p);
 for(const s of sources){const id=sourceId(s);sourceNodes.set(id,{'@id':id,'@type':'Source',path:s.path,provider:s.provider,ref:s.ref,digest:s.digest,url:s.url,availability:s.availability,declaredAvailability:s.declaredAvailability,access:s.access,qualification:s.qualification,qualificationLimits:s.qualificationLimits,qualifiedRefs:s.qualifiedRefs,accepted:false});}
 for(const phase of phases)for(const w of x.phaseWriteScopes[phase]??[]){const id=resourceId(w);resourceNodes.set(id,{'@id':id,'@type':'Resource',path:w.path,physicalBinding:w.physicalBinding,allocation:'bound only at actual claim; no machine/person preassignment'});}
}
const ontology={'@context':{'@vocab':'https://github.com/xrlab-dau/CHOOGuard#',domain:'https://github.com/xrlab-dau/CHOOGuard#domain/',classifiedAs:{'@id':'cg:classifiedAs','@type':'@id'},requirement:'https://github.com/xrlab-dau/CHOOGuard#requirement/',usesSource:{'@id':'cg:usesSource','@type':'@id'},resources:{'@id':'cg:resources','@type':'@id'},requiresOneOf:{'@id':'cg:requiresOneOf','@type':'@id'},from:{'@id':'cg:from','@type':'@id'},to:{'@id':'cg:to','@type':'@id'},cg:'https://github.com/xrlab-dau/CHOOGuard#',work:'https://github.com/xrlab-dau/CHOOGuard#work/',phase:'https://github.com/xrlab-dau/CHOOGuard#phase/',artifact:'https://github.com/xrlab-dau/CHOOGuard#artifact/',source:'https://github.com/xrlab-dau/CHOOGuard#source/',resource:'https://github.com/xrlab-dau/CHOOGuard#resource/',role:'https://github.com/xrlab-dau/CHOOGuard#role/',Goal:'cg:Goal',WorkItem:'cg:WorkItem',WorkPhase:'cg:WorkPhase',Artifact:'cg:Artifact',Source:'cg:Source',Resource:'cg:Resource',Role:'cg:Role',Dependency:'cg:Dependency',partOf:{'@id':'cg:partOf','@type':'@id'},orchestrator:{'@id':'cg:orchestrator','@type':'@id'},producer:{'@id':'cg:producer','@type':'@id'},consumer:{'@id':'cg:consumer','@type':'@id'},usesArtifact:{'@id':'cg:usesArtifact','@type':'@id'},produces:{'@id':'cg:produces','@type':'@id'},parallelWith:{'@id':'cg:parallelWith','@type':'@id'},conflictsWith:{'@id':'cg:conflictsWith','@type':'@id'}},meaning:'Project-specific LLM-readable ontology, not a claimed universal LLM standard. Dependencies bind producer/consumer phases and artifact identity; contains/partOf is not execution order.'};
Object.assign(ontology['@context'],{Requirement:'cg:Requirement',Relationship:'cg:Relationship',implements:{'@id':'cg:implements','@type':'@id'},issueNumbers:{'@id':'cg:issueNumbers','@type':'http://www.w3.org/2001/XMLSchema#integer'},definition:'cg:definition',mappingStatus:'cg:mappingStatus',evidence:{'@id':'cg:evidence','@type':'@json'},legacySource:{'@id':'cg:legacySource','@type':'@json'},policy:{'@id':'cg:policy','@type':'@json'}});
ontology.meaning+=' implements is an evidence-backed planning association, never a prerequisite, implementation receipt, acceptance or authority to resume a held task.';
const graph=[{'@id':'role:pm','@type':'Role',label:'PM issues work, selects priorities and resolves decisions; no executor preassignment.'},{'@id':'role:executor','@type':'Role',label:'A member with their LLM; actual owner is recorded only at claim.'}];
for(const code of ['PM','XR','MAP','UX','QA'])graph.push({'@id':'domain:'+code,'@type':'Domain',code,meaning:'functional classification, not a member assignment'});
for(const p of orders){const x=byId.get(p.issue);graph.push({'@id':p['@id'],'@type':p['@type'],workId:p.workId,title:p.title,classifiedAs:'domain:'+p.domainCode,packet:String(p.issue).padStart(3,'0')+'.json',implements:p.requirements.map(r=>'requirement:'+r.id),orchestrator:'role:pm',...(p.parent?{partOf:'work:'+p.parent}:{}),parallelWith:p.relations.parallelCandidates.map(n=>'work:'+n),conflictsWith:p.relations.conflicts.map(z=>'work:'+z.issue),contextSelections:sourceSelections(x).map(c=>{const s=source(c);return {usesSource:sourceId(s),selector:s.selector,readWhen:s.readWhen,why:s.why,limits:s.limits}})});for(const phase of phases)graph.push({'@id':'phase:'+p.issue+'/'+phase,'@type':'WorkPhase',partOf:p['@id'],phase,produces:x.outputs.filter(o=>o.producerPhase===phase).map(o=>o.id),resources:(x.phaseWriteScopes[phase]??[]).map(resourceId),requiresOneOf:x.oneOfInputs.filter(o=>o.consumerPhase===phase).map(o=>o.artifact)});}
for(const x of g.items)for(const o of x.outputs)graph.push({'@id':o.id,'@type':'Artifact',producer:'phase:'+x.number+'/'+o.producerPhase,path:o.path,qualification:o.qualification,digestRequirements:o.digestRequirements});
let count=0;for(const e of g.edges.filter(x=>x.relation==='requires'))graph.push({...structuredClone(e),'@id':'cg:dependency/'+(++count),'@type':'Dependency',from:'work:'+num(e.from),to:'work:'+num(e.to),producer:'phase:'+num(e.to)+'/'+e.producerPhase,consumer:'phase:'+num(e.from)+'/'+e.consumerPhase,usesArtifact:e.artifact,reason:e.reason});
for(const r of g.references){graph.push({'@id':'work:'+r.number,'@type':'HistoricalReference',title:r.title,state:r.state,limits:r.limits});for(const o of r.outputs??[])graph.push({'@id':o.id,'@type':'HistoricalArtifact',producer:'work:'+r.number,...o,historicalOnly:true});}
for(const x of g.items)for(const group of x.oneOfInputs){graph.push({'@id':group.artifact,'@type':'ConditionalArtifact',consumer:'phase:'+x.number+'/'+group.consumerPhase,resolution:group.resolution,alternatives:group.alternatives.map(z=>({...z,producer:'phase:'+z.issue+'/'+z.producerPhase,usesArtifact:z.artifact}))});for(const z of group.alternatives)if(!byId.has(z.issue)&&!graph.some(n=>n['@id']==='phase:'+z.issue+'/'+z.producerPhase))graph.push({'@id':'phase:'+z.issue+'/'+z.producerPhase,'@type':'HistoricalPhase',partOf:'work:'+z.issue,phase:z.producerPhase,historicalOnly:true});}
const ids=new Map(g.requirements.map(r=>[r.id,'requirement:'+r.id]));for(const r of g.requirements)graph.push({'@id':ids.get(r.id),'@type':'Requirement',code:r.code,title:r.title,definition:r.definition,issueNumbers:requirements.byRequirement[r.id],mappingStatus:r.mappingStatus,evidence:structuredClone(r.evidence),legacySource:structuredClone(r.legacySource),limits:r.limits});
const iri=s=>/^issue\.\d+$/.test(s)?'work:'+num(s):(ids.get(s)??s);
let rel=0;for(const e of g.edges.filter(e=>!['requires','contains'].includes(e.relation)))graph.push(relationshipNode(e,++rel,iri));
graph.push(...sourceNodes.values(),...resourceNodes.values());
const index={'@context':'./ontology.jsonld',scope:orders.length+' open work instructions; phase-specific dependency ontology; not acceptance or live ownership','@graph':graph};
const readme='# LLM 작업 문맥 레지스트리\n\n현재 지시: [PM 오케스트레이션 계약](../orchestration-contract.md). 이 레지스트리는 계획·지시·실제 실행·증거를 구별합니다.\n\n- `NNN.json`: 해당 이슈의 실행 지시와 최소 문맥\n- `ontology.jsonld`: 프로젝트 온톨로지와 관계 방향\n- `index.jsonld`: Goal/WorkItem/Phase/Artifact/Source/Resource/Role/Requirement 등록\n- `requirements`: 해당 작업의 정의·근거·한계만 보존하며 `--section requirements`는 packet만 읽습니다. 공유 요구의 다른 이슈 근거는 반환하지 않습니다.\n- `implements`: 계획상 근거 매핑이며 선행·수용·실행 권한이 아닙니다. `historical_on_hold`는 #106/#107 정책 보류를 유지합니다.\n- 미게시 명세는 `local_snapshot`, `ref:null`과 정확한 hash·내장 인용으로 구분합니다. `legacySource` 원생성기와 foundation-map은 미복구입니다.\n- 상세 입력·쓰기·검증은 `task_context.mjs --section`으로 해당 이슈/단계만 읽습니다.\n\n| 이슈 | PM 작업 지시 | 문맥 |\n|---|---|---|\n'+orders.map(p=>`| [#${p.issue}](https://github.com/xrlab-dau/CHOOGuard/issues/${p.issue}) | ${p.title} | [packet](${String(p.issue).padStart(3,'0')}.json) |`).join('\n')+'\n';
const summary={workOrders:orders.length,phaseDependencies:count,artifacts:g.items.reduce((s,x)=>s+x.outputs.length,0),sources:sourceNodes.size,resources:resourceNodes.size,parallelDirected:orders.reduce((s,x)=>s+x.relations.parallelCandidates.length,0),conflictDirected:orders.reduce((s,x)=>s+x.relations.conflicts.length,0)};
return {orders,ontology,index,readme,summary};
}

const self=fileURLToPath(import.meta.url);
if(process.argv[1]&&path.resolve(process.argv[1])===self){
 try{
  if(process.argv.length>2)throw Error('build_work_orders.mjs accepts no flags');
  const root=path.resolve(path.dirname(self),'../..'),read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
  const result=buildWorkOrders(read('docs/context/work-graph.json'),read('docs/context/source-availability.json'),read('docs/context/work-orders/policy.json'));
  const put=(p,x)=>{const f=path.join(root,'docs/context/work-orders',p);fs.mkdirSync(path.dirname(f),{recursive:true});fs.writeFileSync(f,typeof x==='string'?x:JSON.stringify(x,null,2)+'\n');};
  for(const order of result.orders)put(String(order.issue).padStart(3,'0')+'.json',order);
  put('ontology.jsonld',result.ontology);put('index.jsonld',result.index);put('README.md',result.readme);
  console.log(JSON.stringify(result.summary,null,2));
 }catch(e){console.error(e.message);process.exitCode=2;}
}
