import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import {renderBody} from './task_context.mjs';

const canonical=x=>JSON.stringify(x===null||typeof x!=='object'?x:Array.isArray(x)?x.map(v=>JSON.parse(canonical(v))):Object.fromEntries(Object.keys(x).sort().map(k=>[k,JSON.parse(canonical(x[k]))])));
const hash=x=>createHash('sha256').update(x).digest('hex');
const digest=x=>hash(canonical(x));
const same=(a,b)=>canonical(a)===canonical(b);
const PREFIX='<!-- chooguard:work-order:';
const END='<!-- /chooguard:work-order -->';
const START=/<!-- chooguard:work-order:v1 issue=(\d+) body=([a-f0-9]{64}) title=([a-f0-9]{64}) -->\n/g;
const preserved=x=>Object.fromEntries(Object.entries(x).filter(([key])=>!['body','title','updatedAt'].includes(key)));

function issueMap(snapshot){
 if(!Array.isArray(snapshot?.issues))throw Error('Expected snapshot.issues array');
 const rows=new Map();
 for(const issue of snapshot.issues){
  if(!Number.isSafeInteger(issue?.number)||issue.number<1||typeof issue.title!=='string'||typeof issue.body!=='string'||typeof issue.updatedAt!=='string'||!Number.isFinite(Date.parse(issue.updatedAt))||!Array.isArray(issue.assignees)||!Array.isArray(issue.comments))throw Error('Incomplete issue snapshot: number/title/body/updatedAt/assignees/comments required');
  if(rows.has(issue.number))throw Error('Duplicate snapshot issue #'+issue.number);
  rows.set(issue.number,issue);
 }
 return rows;
}
function managed(body,number){
 const starts=[...body.matchAll(START)];
 if(!body.includes(PREFIX)&&!body.includes(END))return null;
 if(starts.length!==1||body.split(PREFIX).length!==2||body.split(END).length!==2)throw Error('Malformed or duplicate managed body markers');
 const start=starts[0],contentStart=start.index+start[0].length,end=body.indexOf(END);
 if(Number(start[1])!==number||end<contentStart)throw Error('Managed body issue/marker mismatch');
 const content=body.slice(contentStart,end);
 if(hash(content)!==start[2])throw Error('Human edits in managed body; preserve and reconcile explicitly');
 return {start:start.index,end:end+END.length,content,titleHash:start[3]};
}
function wrap(number,content,title){
 if(content.includes(PREFIX)||content.includes(END))throw Error('Generated instructions contain reserved markers');
 return `${PREFIX}v1 issue=${number} body=${hash(content)} title=${hash(title)} -->\n${content}${END}`;
}
function planPayload(plan){const {planDigest,...payload}=plan;return payload;}
function planErrors(plan){
 try{
  if(!plan||!Array.isArray(plan.operations)||!Array.isArray(plan.preconditions)||!Array.isArray(plan.conflicts)||plan.dryRun!==true||plan.authorization!==false)return ['Malformed dry-run plan'];
  if(!plan.operations.every(x=>x&&Number.isSafeInteger(x.issue)&&['title','body'].includes(x.field)&&typeof x.before==='string'&&typeof x.after==='string'&&typeof x.reason==='string')||!plan.preconditions.every(x=>x&&Number.isSafeInteger(x.issue)&&x.before&&x.snapshotDigest===digest(x.before)))return ['Malformed operation/precondition'];
  if(new Set(plan.operations.map(x=>`${x.issue}/${x.field}`)).size!==plan.operations.length||new Set(plan.preconditions.map(x=>x.issue)).size!==plan.preconditions.length||plan.operations.some(x=>!plan.preconditions.some(c=>c.issue===x.issue&&c.before[x.field]===x.before)))return ['Duplicate or unbound operation/precondition'];
  if(plan.planDigest!==digest(planPayload(plan)))return ['Plan digest mismatch'];
  return plan.conflicts.length?['Unresolved projection conflicts; replan after explicit reconciliation']:[];
 }catch{return ['Malformed dry-run plan'];}
}

// Snapshot and canonical contract are caller-provided data, not remote authority.
// This module has no GitHub client, mutation, lease or scheduler.
export function planProjection(graph,orders,snapshot,{ref}={}){
 if(!/^[a-f0-9]{40}$/.test(ref??''))throw Error('Projection ref must be an immutable 40-character commit SHA');
 if(!Array.isArray(graph?.items)||!Array.isArray(orders))throw Error('Expected graph.items and orders arrays');
 const rows=issueMap(snapshot),items=new Map(graph.items.map(x=>[x.number,x]));
 if(items.size!==graph.items.length)throw Error('Duplicate canonical issue');
 const operations=[],preconditions=[],conflicts=[],seen=new Set();
 for(const order of [...orders].sort((a,b)=>a.issue-b.issue)){
  if(seen.has(order.issue))throw Error('Duplicate work order #'+order.issue);seen.add(order.issue);
  const item=items.get(order.issue),current=rows.get(order.issue);
  if(!item||!current)throw Error('Missing canonical item or snapshot for #'+order.issue);
  preconditions.push({issue:order.issue,snapshotDigest:digest(current),before:structuredClone(current)});
  let block;
  try{block=managed(current.body,order.issue);}catch(e){conflicts.push({issue:order.issue,field:'body',reason:e.message});
   if(current.title!==item.title&&current.title!==order.title)conflicts.push({issue:order.issue,field:'title',reason:'Human title differs from canonical and desired title'});
   continue;
  }
  const titleOwned=block?hash(current.title)===block.titleHash:current.title===item.title||current.title===order.title;
  if(!titleOwned){conflicts.push({issue:order.issue,field:'title',reason:'Human title differs from last managed title; preserve and reconcile explicitly'});continue;}
  const content=renderBody(order,item,ref),next=wrap(order.issue,content,order.title);
  // Initial adoption preserves every original byte, including nested historical details.
  const body=block?current.body.slice(0,block.start)+next+current.body.slice(block.end):next+(current.body?'\n\n<details><summary>이전 본문 원문 (현재 지시 아님)</summary>\n\n'+current.body+'\n</details>\n':'\n');
  for(const [field,after,reason] of [['title',order.title,'Synchronize the canonical PM work-order title'],['body',body,'Refresh only managed instructions; preserve the original body, history and human text']]){
   if(current[field]!==after)operations.push({issue:order.issue,field,before:current[field],after,reason});
  }
 }
 const plan={schemaVersion:1,dryRun:true,authorization:false,ref,operations,conflicts,preconditions,preservedFields:['comments','assignees','state','labels','project fields','all non-managed issue fields'],scope:'Pure issue title/body projection only. A fresh precondition check and explicit caller-approved remote apply are separate; no atomic compare-and-swap or distributed lock is claimed.'};
 return {...plan,planDigest:digest(plan)};
}

export function checkPreconditions(plan,snapshot){
 const errors=planErrors(plan);
 try{const rows=issueMap(snapshot);for(const condition of plan.preconditions??[]){const current=rows.get(condition.issue);if(!current||digest(current)!==condition.snapshotDigest)errors.push(`Concurrent snapshot drift for #${condition.issue}; read again and replan, never force`);}}catch(e){errors.push(e.message);}
 return {valid:errors.length===0,errors,authorization:false,scope:'Read-only drift check; not remote write permission or an atomic lock'};
}

export function checkApplyReceipt(plan,receipt,afterSnapshot){
 const errors=planErrors(plan);
 if(errors.length)return {valid:false,errors,authorization:false};
 if(receipt?.planDigest!==plan?.planDigest||receipt?.ref!==plan?.ref)errors.push('Receipt plan/ref mismatch');
 const expected=plan.operations??[],actual=receipt?.operations;
 if(!Array.isArray(actual)||actual.length!==expected.length||actual.some(x=>!x||typeof x!=='object'))errors.push('Receipt operation count/shape mismatch');
 else{
  const keys=actual.map(x=>`${x.issue}/${x.field}`);
  if(new Set(keys).size!==keys.length)errors.push('Duplicate receipt operation');
  for(const op of expected){const row=actual.find(x=>x.issue===op.issue&&x.field===op.field);if(!row||row.status!=='applied'||row.before!==op.before||row.after!==op.after)errors.push(`Receipt operation mismatch for #${op.issue} ${op.field}`);}
 }
 try{
  const rows=issueMap(afterSnapshot);
  for(const condition of plan.preconditions??[]){
   const after=rows.get(condition.issue),before=condition.before;
   if(!after){errors.push('Missing resulting snapshot #'+condition.issue);continue;}
   for(const field of ['title','body']){const op=expected.find(x=>x.issue===condition.issue&&x.field===field);if(after[field]!== (op?op.after:before[field]))errors.push(`Post-apply mismatch #${condition.issue} ${field}`);}
   if(!same(preserved(after),preserved(before)))errors.push(`Preserved fields changed for #${condition.issue}; investigate, do not restore over concurrent human changes`);
  }
 }catch(e){errors.push(e.message);}
 return {valid:errors.length===0,errors,authorization:false,scope:'Receipt/snapshot consistency only; caller evidence, not proof of remote execution or product acceptance'};
}

const self=fileURLToPath(import.meta.url);
if(process.argv[1]&&path.resolve(process.argv[1])===self){
 try{
  const args=process.argv.slice(2),allowed=new Set(['--graph','--orders','--snapshot','--ref']),values={};
  for(let i=0;i<args.length;i+=2){const key=args[i],value=args[i+1];if(!allowed.has(key)||Object.hasOwn(values,key)||!value||value.startsWith('--'))throw Error('Use --graph <json> --orders <json-array> --snapshot <json> --ref <SHA>; dry-run only');values[key]=value;}
  if(Object.keys(values).length!==4)throw Error('All graph/orders/snapshot/ref flags are required; remote writes are not supported');
  const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
  console.log(JSON.stringify(planProjection(read(values['--graph']),read(values['--orders']),read(values['--snapshot']),{ref:values['--ref']}),null,2));
 }catch(e){console.error(e.message);process.exitCode=2;}
}
