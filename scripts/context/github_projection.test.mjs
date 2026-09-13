import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
const moduleURL=new URL('./github_projection.mjs',import.meta.url);
const projection=fs.existsSync(moduleURL)?await import(moduleURL):{};
const ref='a'.repeat(40);
const order={issue:149,workId:'CTX-149','@type':'WorkItem',title:'[CTX-149] New context',directive:'Read only the task',executionMode:'member + LLM',assignment:{preassigned:false,existingClaim:null},environment:'neutral',parent:null,children:[],requiredOutputPaths:['docs/context.json'],relations:{predecessors:[],successors:[],oneOfGroups:[],parallelCandidates:[],parallelCondition:'claims required',conflicts:[]},minimumContext:[],handoff:[]};
const item={number:149,title:'Original task',phaseWriteScopes:{candidate:[{path:'docs/context.json'}]}};
const graph={items:[item]};
const snapshot={issues:[{number:149,id:'ISSUE_149',title:'Original task',body:'Human context\n\n<details><summary>Old history</summary>\n\nFailed run kept exactly.\n\n</details>\n',updatedAt:'2026-09-13T00:00:00Z',assignees:[{login:'existing-owner'}],comments:[{id:'comment1',body:'Existing start'}]}]};
const apply=(plan,before)=>{const after=structuredClone(before);for(const op of plan.operations)after.issues.find(x=>x.number===op.issue)[op.field]=op.after;return after;};

test('projection is pure and dry-run, keeps history and only plans explicitly managed fields',()=>{
 assert.equal(typeof projection.planProjection,'function');
 const before=JSON.stringify([graph,order,snapshot]);
 const plan=projection.planProjection(graph,[order],snapshot,{ref});
 assert.equal(plan.dryRun,true);assert.equal(plan.authorization,false);assert.equal(plan.operations.length,2);
 const body=plan.operations.find(x=>x.field==='body');assert.ok(body.after.includes(snapshot.issues[0].body));assert.match(body.after,/PM 작업 지시/);
 for(const op of plan.operations){assert.ok(['title','body'].includes(op.field));assert.equal(typeof op.before,'string');assert.equal(typeof op.after,'string');assert.ok(op.reason);}
 assert.equal(JSON.stringify([graph,order,snapshot]),before);
 assert.equal(projection.planProjection(graph,[order],apply(plan,snapshot),{ref}).operations.length,0);
});

test('preconditions reject concurrent issue drift before apply',()=>{
 assert.equal(typeof projection.checkPreconditions,'function');
 const plan=projection.planProjection(graph,[order],snapshot,{ref});
 assert.equal(projection.checkPreconditions(plan,snapshot).valid,true);
 for(const field of ['body','title','updatedAt','assignees','comments']){
  const changed=structuredClone(snapshot);changed.issues[0][field]=typeof changed.issues[0][field]==='string'?'changed':[];
  assert.equal(projection.checkPreconditions(plan,changed).valid,false,field);
 }
});

test('human edits inside managed body or title are not overwritten when replanning',()=>{
 assert.equal(typeof projection.planProjection,'function');
 const first=projection.planProjection(graph,[order],snapshot,{ref});const changed=apply(first,snapshot);
 changed.issues[0].body=changed.issues[0].body.replace('Read only the task','Human changed instruction');
 changed.issues[0].title='Human changed title';
 const plan=projection.planProjection(graph,[order],changed,{ref});
 assert.equal(plan.operations.length,0);assert.ok(plan.conflicts.some(x=>x.field==='body'));assert.ok(plan.conflicts.some(x=>x.field==='title'));
});

test('unmanaged history edits survive an update to generated instructions',()=>{
 assert.equal(typeof projection.planProjection,'function');
 const first=projection.planProjection(graph,[order],snapshot,{ref});const changed=apply(first,snapshot);
 changed.issues[0].body+='\nHuman follow-up, outside managed content.\n';
 const plan=projection.planProjection(graph,[{...order,directive:'Updated bounded task'}],changed,{ref});
 const body=plan.operations.find(x=>x.field==='body');assert.ok(body.after.endsWith('Human follow-up, outside managed content.\n'));
 assert.ok(body.after.includes(snapshot.issues[0].body));
});

test('receipt check binds exact plan and verifies resulting managed and preserved fields',()=>{
 assert.equal(typeof projection.checkApplyReceipt,'function');
 const plan=projection.planProjection(graph,[order],snapshot,{ref}),after=apply(plan,snapshot);
 const receipt={planDigest:plan.planDigest,ref,operations:plan.operations.map(x=>({issue:x.issue,field:x.field,before:x.before,after:x.after,status:'applied'}))};
 assert.equal(projection.checkApplyReceipt(plan,receipt,after).valid,true);
 assert.equal(projection.checkApplyReceipt(plan,{...receipt,planDigest:'wrong'},after).valid,false);
 assert.equal(projection.checkApplyReceipt(plan,{...receipt,operations:receipt.operations.slice(1)},after).valid,false);
 after.issues[0].assignees=[];assert.equal(projection.checkApplyReceipt(plan,receipt,after).valid,false);
});

test('malformed plans and receipts fail closed without throwing',()=>{
 assert.equal(projection.checkPreconditions(null,snapshot).valid,false);
 assert.equal(projection.checkApplyReceipt(null,null,snapshot).valid,false);
 const plan=projection.planProjection(graph,[order],snapshot,{ref}),after=apply(plan,snapshot);
 const tampered=structuredClone(plan);tampered.operations[0].after='changed';assert.equal(projection.checkPreconditions(tampered,snapshot).valid,false);
 assert.equal(projection.checkApplyReceipt(plan,{planDigest:plan.planDigest,ref,operations:[null,null]},after).valid,false);
});

test('projection rejects missing or duplicate snapshots and mutable refs without remote actions',()=>{
 assert.equal(typeof projection.planProjection,'function');
 assert.throws(()=>projection.planProjection(graph,[order],snapshot,{ref:'develop'}),/ref/);
 assert.throws(()=>projection.planProjection(graph,[order],{issues:[]},{ref}),/snapshot/);
 assert.throws(()=>projection.planProjection(graph,[order],{issues:[...snapshot.issues,...snapshot.issues]},{ref}),/Duplicate/);
});
