import test from'node:test';import assert from'node:assert/strict';import{brief,section,renderBody,load}from'./task_context.mjs';
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';import {spawnSync} from 'node:child_process';import * as context from './task_context.mjs';
const order={issue:1,workId:'T-1','@type':'WorkItem',title:'[T-1] Test',directive:'Make the bounded output',executionMode:'member + LLM',assignment:{preassigned:false,existingClaim:null},environment:'neutral',parent:null,children:[],requiredOutputPaths:['docs/test.json'],relations:{predecessors:[{issue:2,consumerPhase:'candidate',producerPhase:'accept',artifact:'artifact:2:multi:colon:accept'}],successors:[],oneOfGroups:[],parallelCandidates:[3],parallelCondition:'actual claims required',conflicts:[]},minimumContext:[],handoff:['actual results']};
const item={number:1,hardPredecessors:order.relations.predecessors,inputs:[],oneOfInputs:[],guardPredicates:[{id:'guard-without-phase',predicate:'deny unknown'}],inputQualifiers:[],phaseWriteScopes:{prepare:[],candidate:[{path:'docs/test.json',physicalBinding:'workspace-file:{workspaceId}:docs/test.json'}],accept:[]},artifactOutputBindings:{candidate:[]},resourceBindings:[],requiredLocks:[],checks:[{command:'test command',phases:['candidate'],expected:'pass or report failure'}],acceptance:'all checks',checkEvidenceMappings:[],stopConditions:['scope conflict'],outputs:[],context:[],codePointers:[],truth:[],board:{status:'old capture'},reviewNotes:[]};
test('brief preserves exact artifact identity and exposes explicit continuation without assigning a member',()=>{const x=brief(order,'candidate');assert.equal(x.predecessors[0].artifact,'artifact:2:multi:colon:accept');assert.equal(x.assignment.preassigned,false);assert.match(x.requiredNextReads.writeBoundary,/--section writes --phase candidate/);});
test('default load reads only one packet, not the canonical graph',()=>{
 const root=fs.mkdtempSync(fileURLToPath(new URL('./.packet-test-',import.meta.url)));
 try{fs.mkdirSync(path.join(root,'docs/context/work-orders'),{recursive:true});fs.writeFileSync(path.join(root,'docs/context/work-orders/001.json'),JSON.stringify(order));assert.deepEqual(load(root,1),{order});}finally{fs.rmSync(root,{recursive:true,force:true});}
});
test('budget output marks completeness and retains all required guards or an explicit continuation',()=>{
 assert.equal(typeof context.budgetResponse,'function');
 const data={guards:Array.from({length:50},(_,n)=>({id:'guard-'+n,predicate:'must verify'}))};
 const small=context.budgetResponse(data,{maxBytes:256,continuation:'node scripts/context/task_context.mjs brief --issue 1 --section inputs --phase candidate'});
 assert.equal(small.complete,false);assert.equal(small.data,undefined);assert.match(small.continuation,/--section inputs/);
 const full=context.budgetResponse(data,{maxBytes:10000});assert.equal(full.complete,true);assert.deepEqual(full.data,data);
});
test('CLI rejects unknown commands, duplicate flags, missing values and invalid budgets',()=>{
 const cli=fileURLToPath(new URL('./task_context.mjs',import.meta.url));
 for(const args of [['wrong','--issue','70'],['brief','--issue','70','--wat'],['brief','--issue','70','--issue','71'],['brief','--issue','70','--phase'],['brief','--issue','70','--max-bytes','NaN'],['brief','--issue','70','--max-items','-1']]){
  const result=spawnSync(process.execPath,[cli,...args],{encoding:'utf8'});assert.equal(result.status,2,JSON.stringify(args));assert.equal(result.stdout,'');
 }
});
test('detailed loads select canonical fields and only relevant source qualifications',()=>{
 const root=fs.mkdtempSync(fileURLToPath(new URL('./.packet-test-',import.meta.url)));
 try{
  fs.mkdirSync(path.join(root,'docs/context/work-orders'),{recursive:true});fs.writeFileSync(path.join(root,'docs/context/work-orders/001.json'),JSON.stringify(order));
  const canonical={...item,context:[{path:'docs/a.md',readWhen:'candidate',availability:'published'}]};
  fs.writeFileSync(path.join(root,'docs/context/work-graph.json'),JSON.stringify({items:[canonical,{number:2,privateIrrelevant:'not returned'}]}));
  fs.writeFileSync(path.join(root,'docs/context/source-availability.json'),JSON.stringify({snapshot:{developSHA:'a'.repeat(40)},files:{'docs/a.md':{develop:true,developGitBlobSha:'b'.repeat(40)},'other.md':{irrelevant:true}}}));
  for(const name of ['inputs','writes','checks','outputs','history']){const loaded=load(root,1,{section:name});assert.ok(loaded.item);assert.equal(loaded.item.privateIrrelevant,undefined);assert.doesNotThrow(()=>section(loaded.order,loaded.item,name,'candidate'));}
  const loaded=load(root,1,{section:'sources'});assert.deepEqual(Object.keys(loaded.availability.files),['docs/a.md']);
  assert.equal(section(loaded.order,loaded.item,'sources','candidate',loaded.availability).qualification[0].ref,'a'.repeat(40));
  assert.deepEqual(load(root,1,{section:'relations'}),{order});
  assert.deepEqual(Object.keys(load(root,1,{body:true}).item),['number','phaseWriteScopes']);
  assert.throws(()=>load(root,0));assert.throws(()=>load(root,1,{section:'unknown'}));assert.throws(()=>load(root,2));
 }finally{fs.rmSync(root,{recursive:true,force:true});}
});
test('budget and section API reject invalid options and never truncate checks',()=>{
 assert.throws(()=>context.budgetResponse({}, {maxBytes:-1}));assert.throws(()=>context.budgetResponse({}, {maxItems:NaN}));
 assert.throws(()=>context.budgetResponse({many:[1,2]}, {maxItems:1}),/continuation/);
 const response=context.budgetResponse({many:[1,2]}, {maxItems:1,continuation:'read full'});assert.equal(response.complete,false);
 assert.throws(()=>section(order,item,'unknown'));assert.throws(()=>section(order,item,'inputs','future'));
 assert.deepEqual(section(order,item,'relations'),order.relations);assert.deepEqual(section(order,item,'outputs'),item.outputs);
 assert.deepEqual(section(order,item,'writes').phaseWriteScopes,item.phaseWriteScopes);assert.equal(section(order,item,'history').historicalOnly,true);
 assert.deepEqual(section(order,item,'checks','candidate').checks,item.checks);
 assert.deepEqual(context.parseArgs(['brief','--issue','1','--section','checks','--phase','candidate','--max-items','2']),{command:'brief',number:1,phase:'candidate',name:'checks',ref:undefined,maxBytes:undefined,maxItems:2});
});
test('CLI returns complete scoped JSON, incomplete budgets, and read-only bodies',()=>{
 const cli=fileURLToPath(new URL('./task_context.mjs',import.meta.url));
 for(const sectionName of [null,'relations','checks','inputs','writes','sources','outputs','history']){
  const args=['brief','--issue','70',...(sectionName?['--section',sectionName]:[])];
  const result=spawnSync(process.execPath,[cli,...args],{encoding:'utf8'});assert.equal(result.status,0,result.stderr);assert.equal(JSON.parse(result.stdout).complete,true);
 }
 const small=spawnSync(process.execPath,[cli,'brief','--issue','70','--section','inputs','--phase','candidate','--max-bytes','1'],{encoding:'utf8'});
 assert.equal(small.status,3);assert.equal(JSON.parse(small.stdout).complete,false);assert.match(JSON.parse(small.stdout).continuation,/--section inputs --phase candidate/);
 const body=spawnSync(process.execPath,[cli,'body','--issue','70','--ref','a'.repeat(40)],{encoding:'utf8'});assert.equal(body.status,0);assert.match(body.stdout,/PM 작업 지시/);
});
test('code-only source qualifications are loaded and phase-filtered without accepting local bytes',()=>{
 const root=fs.mkdtempSync(fileURLToPath(new URL('./.packet-test-',import.meta.url)));
 try{
  const file='src/code.py',ref='a'.repeat(40),q={ref,digest:{algorithm:'sha256',value:'b'.repeat(64)},url:`https://github.com/xrlab-dau/CHOOGuard/blob/${ref}/${file}`,access:'published',qualification:'source-only',limits:'not acceptance'};
  fs.mkdirSync(path.join(root,'docs/context/work-orders'),{recursive:true});fs.writeFileSync(path.join(root,'docs/context/work-orders/001.json'),JSON.stringify(order));
  const canonical={...item,codePointers:[{path:file,symbol:'main',availability:'local_unpublished'},'src/unqualified.py']};
  fs.writeFileSync(path.join(root,'docs/context/work-graph.json'),JSON.stringify({items:[canonical]}));
  fs.writeFileSync(path.join(root,'docs/context/source-availability.json'),JSON.stringify({files:{[file]:{qualifiedRefs:[q]},'other.py':{irrelevant:true}}}));
  const loaded=load(root,1,{section:'sources'});assert.deepEqual(Object.keys(loaded.availability.files),[file]);
  const view=section(loaded.order,loaded.item,'sources','candidate',loaded.availability),s=view.qualification.find(x=>x.path===file);
  assert.equal(s.ref,ref);assert.deepEqual(s.digest,q.digest);assert.equal(s.accepted,false);assert.equal(s.qualificationLimits,q.limits);assert.equal(s.selector,'main');
  assert.equal(view.codePointers[0].ref,ref);assert.equal(view.codePointers[1].ref,null);assert.equal(view.codePointers[1].accepted,false);
  assert.equal(section(loaded.order,loaded.item,'sources','prepare',loaded.availability).codePointers.length,0);
 }finally{fs.rmSync(root,{recursive:true,force:true});}
});

test('input sections never drop a guard lacking a phase shortcut',()=>assert.equal(section(order,item,'inputs','candidate').guards.length,1));
test('write sections return exact scopes and physical binding rather than counts',()=>assert.equal(section(order,item,'writes','candidate').phaseWriteScopes[0].physicalBinding,item.phaseWriteScopes.candidate[0].physicalBinding));
test('body is an executable PM work order with scoped reads and no historical status promoted to current',()=>{const b=renderBody(order,item,'a'.repeat(40));assert.match(b,/PM 작업 지시/);assert.match(b,/--section checks --phase candidate/);assert.match(b,/선배정 없음/);assert.match(b,/#2/);assert.doesNotMatch(b,/old capture/);assert.throws(()=>brief(order,'invalid'));});
