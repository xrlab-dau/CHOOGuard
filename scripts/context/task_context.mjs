import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {enrichSource} from './build_work_orders.mjs';
export const SECTION_FIELDS={relations:[],inputs:['number','hardPredecessors','inputs','oneOfInputs','guardPredicates','inputQualifiers'],writes:['number','phaseWriteScopes','artifactOutputBindings','resourceBindings','requiredLocks'],checks:['checks','acceptance','checkEvidenceMappings','stopConditions'],outputs:['outputs'],sources:['context','codePointers'],history:['truth','board','reviewNotes']};
export const PHASE_NAMES={prepare:'준비',candidate:'구현·검증',accept:'완료 판정'};
const uniq=a=>[...new Set(a)];
export function brief(order,phase='prepare'){
 if(!Object.hasOwn(PHASE_NAMES,phase))throw Error('Unknown phase');
 return {issue:order.issue,workId:order.workId,domainCode:order.domainCode,type:order['@type'],title:order.title,pmInstruction:order.directive,orchestrator:'PM',executor:order.executionMode,assignment:order.assignment,environment:order.environment,phase,parent:order.parent,children:order.children,requiredOutputPaths:order.requiredOutputPaths,predecessors:order.relations.predecessors.filter(x=>x.consumerPhase===phase),oneOfGroups:order.relations.oneOfGroups.filter(x=>x.consumerPhase===phase),successorIssues:uniq(order.relations.successors.map(x=>x.issue)),parallelPreview:order.relations.parallelCandidates.slice(0,8),parallelCount:order.relations.parallelCandidates.length,parallelCondition:order.relations.parallelCondition,conflictIssues:order.relations.conflicts.map(x=>x.issue),readFirst:order.minimumContext,requiredNextReads:{beforeEditing:`node scripts/context/task_context.mjs brief --issue ${order.issue} --section inputs --phase ${phase}`,writeBoundary:`node scripts/context/task_context.mjs brief --issue ${order.issue} --section writes --phase ${phase}`,beforeCompletion:`node scripts/context/task_context.mjs brief --issue ${order.issue} --section checks --phase ${phase}`,allRelations:`node scripts/context/task_context.mjs brief --issue ${order.issue} --section relations`},handoff:order.handoff};
}
export function section(order,item,name,phase,availability={files:{}}){
 const chosen=v=>!phase?v:v.filter(x=>{const p=x.phases??[x.consumerPhase??x.producerPhase??x.phase].filter(Boolean);return !p.length||p.includes(phase)});
 if(phase&&!Object.hasOwn(PHASE_NAMES,phase))throw Error('Unknown phase');
 if(name==='relations')return order.relations;
 if(name==='inputs')return {hard:chosen(item.hardPredecessors),declared:chosen(item.inputs),oneOf:chosen(item.oneOfInputs),guards:chosen(item.guardPredicates),qualifiers:item.inputQualifiers,rule:'Only the selected alternative activates its guards and extra inputs. A producer issue being open is not equivalent to its candidate artifact being unavailable.'};
 if(name==='writes')return {issue:item.number,phase:phase??'all',phaseWriteScopes:phase?item.phaseWriteScopes[phase]:item.phaseWriteScopes,artifactOutputBindings:phase?item.artifactOutputBindings[phase]:item.artifactOutputBindings,resources:phase?item.resourceBindings.filter(x=>x.phases.includes(phase)):item.resourceBindings,requiredLocks:item.requiredLocks,rule:'These are boundaries, not acquired locks. Record the actual branch, paths and shared mutable-resource claim before editing. Do not touch another current claim.'};
 if(name==='checks')return {checks:chosen(item.checks),acceptance:item.acceptance,checkEvidenceMappings:item.checkEvidenceMappings,stopConditions:item.stopConditions,rule:'Read and execute all required checks for the selected phase. Report failed/unrun cases honestly. No unlimited review loop or automatic closure.'};
 if(name==='outputs')return chosen(item.outputs);
 if(name==='sources')return {sources:item.context.filter(x=>!phase||x.readWhen===phase||Array.isArray(x.readWhen)&&x.readWhen.includes(phase)),codePointers:item.codePointers,qualification:item.context.filter(x=>!phase||x.readWhen===phase||Array.isArray(x.readWhen)&&x.readWhen.includes(phase)).map(x=>enrichSource(x,availability)),rule:'Published references remain tied to their captured ref. For local_unpublished sources use the exact qualified provider/path/ref/digest/access, never path presence alone.'};
 if(name==='history')return {historicalOnly:true,truth:item.truth,capturedBoard:item.board,reviewNotes:item.reviewNotes,existingClaim:order.assignment.existingClaim,rule:'History is evidence, not a current assignment, environment restriction, completed test, or automatic instruction.'};
 throw Error('Unknown section: '+name);
}
const link=n=>`[#${n}](https://github.com/xrlab-dau/CHOOGuard/issues/${n})`;
const safe=s=>String(s??'').replaceAll('|','\\|').replace(/\r?\n/g,' ');
const grouped=rows=>Object.entries(PHASE_NAMES).map(([phase,label])=>{const entries=uniq(rows.filter(x=>x.consumerPhase===phase).map(x=>`${x.issue}@${x.producerPhase}`));return `${label}: ${entries.length?entries.map(s=>{const [n,p]=s.split('@');return link(Number(n))+'['+PHASE_NAMES[p]+']'}).join(', '):'이슈 완료를 기다리는 필수 선행 없음'}`}).join('<br>');
export function renderBody(order,item,ref='{{CONTEXT_REF}}'){
 const base=`https://github.com/xrlab-dau/CHOOGuard/blob/${ref}/`,packet=`docs/context/work-orders/${String(order.issue).padStart(3,'0')}.json`,pre=grouped(order.relations.predecessors),succ=uniq(order.relations.successors.map(x=>x.issue)),parallel=order.relations.parallelCandidates.slice(0,8),conflicts=order.relations.conflicts.map(x=>x.issue),paths=uniq((item.phaseWriteScopes.candidate??[]).map(x=>x.path));
 const outputRows=order.requiredOutputPaths.map(p=>`| \`${safe(p)}\` | 정확한 자격·검증 조건은 아래 outputs/checks 조회 |`).join('\n');
 const sources=order.minimumContext.length?order.minimumContext.map(s=>`- [\`${s.path}\`](${s.url}) · ${safe(s.selector)}\n  - 읽는 이유: ${safe(s.why)}${s.limits?` / 한계: ${safe(s.limits)}`:''}`).join('\n'):'- 현재 공개된 준비 자료만으로 범위·테스트 설계를 작성하세요. 미게시 코드가 필요한 구현은 해당 선행 소스 전달 후 진행합니다.';
 const claim=order.assignment.existingClaim;
 return `# PM 작업 지시: ${order.title.replace(/^\[[^\]]+\]\s*/,'')}\n\n> **${order.workId} · ${order['@type']==='Goal'?'통합 목표':'실행 작업'}** | PM이 지시·우선순위·인계를 조율하고, 팀원과 팀원의 LLM이 수행합니다.\n> 개인 선배정 없음. 학교/개인 PC·장소·고사양 보유를 배정 조건으로 사용하지 않습니다.\n${claim?`\n**기존 착수 보존:** [@${claim.holder}의 착수 기록](${claim.source}). ${claim.scope.map(x=>'\`'+x+'\`').join(', ')}는 기존 작업과 중복 편집하지 않습니다. 완료 판정은 별도입니다.\n`:''}\n## 1. 수행할 일\n\n${order.directive}\n\n${order['@type']==='Goal'?'PM은 아래 하위 산출물을 통합·판정하세요. 하위 작업을 이 이슈에서 중복 구현하거나 사람을 선배정하지 않습니다.':'아래 산출물을 실제로 만들거나 검증하고 결과를 반환하세요. 계획서·파일 존재·과거 PASS만으로 완료 처리하지 않습니다.'}\n\n| 반환 산출물 | 완료 기준 읽기 |\n|---|---|\n${outputRows}\n\n## 2. LLM이 먼저 읽을 최소 문맥\n\n1. [이 이슈의 context packet](${base+packet})\n2. [공통 PM 오케스트레이션 계약](${base}docs/context/orchestration-contract.md)\n3. 아래 자료의 지정 부분만 읽으세요. 전체 저장소·전체 그래프를 먼저 읽지 않습니다.\n\n${sources}\n\n컨텍스트 커밋과 실제 구현 소스의 기준 SHA는 다를 수 있습니다. 작업 코드 기준선은 입력 manifest에서 확인하세요. 상세 문맥은 이슈/단계로 잘라 조회합니다.\n\n\`\`\`sh\nnode scripts/context/task_context.mjs brief --issue ${order.issue}\nnode scripts/context/task_context.mjs brief --issue ${order.issue} --section inputs --phase candidate\nnode scripts/context/task_context.mjs brief --issue ${order.issue} --section writes --phase candidate\nnode scripts/context/task_context.mjs brief --issue ${order.issue} --section checks --phase candidate\n\`\`\`\n\n## 3. 진행 순서\n\n- [ ] **준비:** 목표·산출물·선행 입력을 확인하고 변경 범위와 검증 방법을 정리합니다. 없는 입력은 구체적으로 기록하되 독립적인 준비 작업까지 전역 차단하지 않습니다.\n- [ ] **착수:** 실제 시작할 때만 담당자/LLM 세션·브랜치·정확한 경로·공유 리소스를 claim합니다. 먼저 입력과 쓰기 범위 전체를 조회하세요.\n- [ ] **구현·검증:** 해당 단계의 선행 산출물을 받은 뒤 허용 범위에서 위 작업을 수행합니다. 후보 전달과 전체 이슈 완료를 구분하세요.\n- [ ] **완료 제안:** checks/outputs의 필수 항목을 전부 확인하고 실행·실패·미실행 근거를 남깁니다. PM이 인계와 다음 단계를 결정합니다. 무제한 AI 재검수는 하지 않습니다.\n\n## 4. 선행·후행·병렬·충돌\n\n| 관계 | PM과 실행 LLM의 판단 기준 |\n|---|---|\n| 상위 목표 | ${order.parent?link(order.parent):'최상위'} |\n| 하위 실행 작업 | ${(order.children??[]).length?order.children.map(link).join(', '):'직접 하위 없음'} |\n| 선행 | ${pre} |\n| 후행 | ${succ.length?succ.map(link).join(', '):'직접 후행 없음; PM에 결과 반환'} |\n| 조건부 병렬 후보 | ${parallel.length?parallel.map(link).join(', '):'보장된 후보 없음'}${order.relations.parallelCandidates.length>8?' · 전체 목록은 packet/relations':''} |\n| 직접 쓰기 충돌 | ${conflicts.length?conflicts.map(link).join(', '):'현재 선언 경로의 직접 충돌 없음'} |\n\n**병렬은 자동 승인이나 사전 배정이 아닙니다.** 선행 입력 충족, 실제 경로 비중복, 별도 checkout, 공유 Editor/출력/가변 리소스의 비경합을 확인한 경우에만 병렬 진행합니다. GitHub의 이슈 단위 blocked-by 대신 위 단계별 관계와 [온톨로지 등록부](${base}docs/context/work-orders/index.jsonld)를 사용합니다.\n${order.relations.oneOfGroups.length?'\n**택1 입력 있음:** inputs 조회의 선택지·guard·추가 입력을 확인하고 하나의 자격 있는 경로만 선택하세요. 모든 선택지를 동시에 선행으로 묶지 않습니다.\n':''}\n## 5. 수정 경계와 반환\n\n${paths.map(p=>'- `'+p+'`').join('\n')}\n\n위 목록 밖 수정, 타인의 활성 claim, 공통 설정·민감자료·유료/외부 공개 행동이 필요하면 그 부분만 PM에게 요청하세요. 상세 물리적 binding·lock·산출물 경계는 writes 조회가 기준입니다. 특정 OS/HMD는 해당 시험 조건으로만 기록하며 모든 작업의 환경 제한으로 확대하지 않습니다.\n\n**반환 형식:** \`Work ID / phase / branch·commit / 변경 경로 / 입력·출력 ref와 hash / 실행한 검사와 결과 / 실패·미실행·실제 blocker / 후행 전달 대상 / claim 해제\`. 공개 반환에는 비밀·개인 경로·제한 자료를 넣지 않습니다.\n\n<details><summary>이전 상태·시험·검토 이력</summary>\n\n이전 기록은 삭제하거나 새 PASS로 바꾸지 않습니다. 현재 배정·환경 제한·자동 작업 명령으로 재사용하지도 않습니다. \`node scripts/context/task_context.mjs brief --issue ${order.issue} --section history\`와 기존 댓글에서 필요한 이력만 확인하세요.\n\n</details>\n`;
}
export function load(root,number,{section:name=null,body=false}={}){
 if(!Number.isSafeInteger(number)||number<1)throw Error('Use --issue <number>');
 if(name&&!Object.hasOwn(SECTION_FIELDS,name))throw Error('Unknown section: '+name);
 const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
 const order=read('docs/context/work-orders/'+String(number).padStart(3,'0')+'.json');
 if(order.issue!==number)throw Error('Unknown issue');
 if(!body&&(!name||name==='relations'))return {order};
 // Canonical JSON is parsed only on a detailed read; unrelated items/fields never leave load().
 const item=read('docs/context/work-graph.json').items.find(x=>x.number===number);
 if(!item)throw Error('Unknown canonical issue');
 const fields=body?['number','phaseWriteScopes']:SECTION_FIELDS[name];
 const selected=Object.fromEntries(fields.filter(k=>Object.hasOwn(item,k)).map(k=>[k,item[k]]));
 if(name==='sources'){
  const source=read('docs/context/source-availability.json');
  return {order,item:selected,availability:{snapshot:source.snapshot,files:Object.fromEntries(item.context.filter(c=>Object.hasOwn(source.files,c.path)).map(c=>[c.path,source.files[c.path]]))}};
 }
 return {order,item:selected};
}

export function budgetResponse(data,{maxBytes,maxItems,continuation=null}={}){
 if(maxBytes!==undefined&&(!Number.isSafeInteger(maxBytes)||maxBytes<1))throw Error('Invalid --max-bytes');
 if(maxItems!==undefined&&(!Number.isSafeInteger(maxItems)||maxItems<1))throw Error('Invalid --max-items');
 const requiredBytes=Buffer.byteLength(JSON.stringify(data),'utf8');
 const width=x=>Array.isArray(x)?Math.max(x.length,...x.map(width)):x&&typeof x==='object'?Math.max(0,...Object.values(x).map(width)):0;
 const largestList=width(data),complete=(maxBytes===undefined||requiredBytes<=maxBytes)&&(maxItems===undefined||largestList<=maxItems);
 if(!complete&&!continuation)throw Error('Budget exceeded; an explicit continuation is required');
 return {complete,authorization:false,requiredBytes,largestList,...(complete?{data}:{reason:'Required conditions exceed the requested budget. Nothing is partially truncated; retrieve the complete scoped result before acting.',continuation})};
}

export function parseArgs(args){
 const [command,...rest]=args;
 if(!['brief','body'].includes(command))throw Error('Use brief or body --issue <number>');
 const allowed=new Set(['--issue','--phase','--section','--ref','--max-bytes','--max-items']),values={};
 for(let i=0;i<rest.length;i+=2){const flag=rest[i],value=rest[i+1];if(!allowed.has(flag)||Object.hasOwn(values,flag)||!value||value.startsWith('--'))throw Error('Unknown, repeated or incomplete flag: '+flag);values[flag]=value;}
 const positive=(key,required=false)=>{const value=values[key];if(value===undefined&&!required)return undefined;if(!/^[1-9]\d*$/.test(value??'')||!Number.isSafeInteger(Number(value)))throw Error('Invalid '+key);return Number(value);};
 const number=positive('--issue',true),phase=values['--phase']??null,name=values['--section']??null;
 if(phase&&!Object.hasOwn(PHASE_NAMES,phase))throw Error('Unknown phase');
 if(name&&!Object.hasOwn(SECTION_FIELDS,name))throw Error('Unknown section: '+name);
 if(command==='body'&&(phase||name||values['--max-bytes']||values['--max-items']))throw Error('body accepts only --issue and --ref');
 if(command==='brief'&&values['--ref'])throw Error('--ref is only supported for body');
 if(values['--ref']&&!/^[a-f0-9]{40}$/.test(values['--ref']))throw Error('--ref requires an immutable commit SHA');
 return {command,number,phase,name,ref:values['--ref'],maxBytes:positive('--max-bytes'),maxItems:positive('--max-items')};
}
const self=fileURLToPath(import.meta.url);
if(process.argv[1]&&path.resolve(process.argv[1])===self){
 try{
  const options=parseArgs(process.argv.slice(2)),{command,number,phase,name,ref,maxBytes,maxItems}=options;
  const {order,item,availability}=load(path.resolve(path.dirname(self),'../..'),number,{section:name,body:command==='body'});
  if(command==='body')console.log(renderBody(order,item,ref));
  else{
   const data=name?section(order,item,name,phase,availability):brief(order,phase??'prepare');
   const continuation=`node scripts/context/task_context.mjs brief --issue ${number}${name?' --section '+name:''}${phase?' --phase '+phase:''}`;
   const result=budgetResponse(data,{maxBytes,maxItems,continuation});
   // Preserve the established JSON shape for unbudgeted consumers, with explicit completeness.
   console.log(JSON.stringify(maxBytes!==undefined||maxItems!==undefined?result:Array.isArray(data)?result:{...data,complete:true,authorization:false},null,2));
   if(!result.complete)process.exitCode=3;
  }
 }catch(e){console.error(e.message);process.exitCode=2;}
}
