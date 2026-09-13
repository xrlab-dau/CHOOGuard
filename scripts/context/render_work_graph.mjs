#!/usr/bin/env node
/**
 * Dependency-free, read-only CHOOGuard work-graph renderer.
 * The canonical graph is validated before any output directory or file is touched.
 */
import { promises as fs } from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { validateGraph } from './work_graph.mjs';

const issueId = number => `issue.${number}`;
const asArray = value => Array.isArray(value) ? value : [];
const safeJson = value => JSON.stringify(value).replace(/[<>&\u2028\u2029]/g, ch => ({'<':'\\u003c','>':'\\u003e','&':'\\u0026','\u2028':'\\u2028','\u2029':'\\u2029'}[ch]));
const jsonText = value => JSON.stringify(value, null, 2);

export function issueNumber(value) {
  const n = value?.number;
  return Number.isSafeInteger(n) ? n : NaN;
}

const boardStatus = item => item.board?.status ?? '미기록';
const boardPhase = item => item.board?.phase ?? '미기록';
const availabilityOf = value => value?.availability ?? '미기록';
const displayValue = value => value === null ? 'null' : value === undefined ? '미기록' : String(value);

export function normalizeNodes(graph) {
  if (!graph || !Array.isArray(graph.items)) return [];
  return graph.items.map((raw, index) => ({
    raw,
    index,
    number: raw.number,
    id: issueId(raw.number),
    workId: raw.workId,
    canonicalWorkId: raw.workId,
    title: raw.title,
    kind: raw.kind,
    parent: raw.parent,
    outcome: raw.outcome,
    nonGoals: asArray(raw.nonGoals),
    truth: asArray(raw.truth),
    context: asArray(raw.context),
    codePointers: asArray(raw.codePointers),
    inputs: asArray(raw.inputs),
    inputQualifiers: asArray(raw.inputQualifiers),
    outputs: asArray(raw.outputs),
    writeScope: asArray(raw.writeScope),
    phaseWriteScopes: raw.phaseWriteScopes ?? {},
    artifactOutputBindings: raw.artifactOutputBindings ?? {},
    checks: asArray(raw.checks),
    checkEvidenceMappings: asArray(raw.checkEvidenceMappings),
    acceptance: asArray(raw.acceptance),
    stopConditions: asArray(raw.stopConditions),
    handoff: raw.handoff,
    reviewNotes: raw.reviewNotes,
    hardPredecessors: asArray(raw.hardPredecessors),
    hardSuccessors: asArray(raw.hardSuccessors),
    oneOfInputs: asArray(raw.oneOfInputs),
    guardPredicates: asArray(raw.guardPredicates),
    relationshipDeclarations: raw.relationshipDeclarations ?? null,
    requiredLocks: asArray(raw.requiredLocks),
    resourceBindings: asArray(raw.resourceBindings),
    board: raw.board,
    status: boardStatus(raw),
    phase: boardPhase(raw),
    capturedProjectStatus: raw.board?.capturedProjectStatus ?? null,
    assigneePolicy: raw.assigneePolicy,
    claimEvidence: raw.claimEvidence,
    authorityBlock: raw.authorityBlock,
    related: asArray(raw.related),
    requirements: asArray(raw.requirements),
    eligibility: 'not projected: authenticated current frontier unavailable',
    reviewStatus: { state: 'LEDGER_NOT_PROVIDED', scope: 'lifecycle is resolved externally from the exact body hash and review ledger' },
  }));
}

export function normalizeReferences(graph) {
  if (!graph || !Array.isArray(graph.references)) return [];
  return graph.references.map(raw => ({
    raw,
    number: raw.number,
    id: `reference.${raw.number}`,
    workId: raw.workId,
    title: raw.title,
    kind: raw.kind,
    state: raw.state,
    stateReason: raw.stateReason,
    closedAt: raw.closedAt,
    url: raw.url,
    outputs: asArray(raw.outputs),
    limits: raw.limits,
    qualification: raw.qualification,
    absorbedHistoricalContext: raw.absorbedHistoricalContext ?? null,
    sourceRef: raw.sourceRef,
    observedAt: raw.observedAt,
    historical: true,
  }));
}

function endpointInfo(value) {
  const id = String(value ?? '');
  const issue = /^issue\.(\d+)$/.exec(id);
  if (issue) return { id, kind: 'issue', number: Number(issue[1]), label: '#'+issue[1] };
  const reference = /^reference\.(\d+)$/.exec(id);
  if (reference) return { id, kind: 'reference', number: Number(reference[1]), label: 'Historical #'+reference[1] };
  return { id, kind: 'external', number: null, label: id || 'unidentified endpoint' };
}
export function normalizeEdges(graph) {
  if (!graph || !Array.isArray(graph.edges)) return [];
  return graph.edges.map((raw, index) => {
    const from = endpointInfo(raw.from), to = endpointInfo(raw.to);
    return {
      raw,
      index,
      from: raw.from,
      to: raw.to,
      fromInfo: from,
      toInfo: to,
      fromNumber: from.kind === 'issue' ? from.number : null,
      toNumber: to.kind === 'issue' ? to.number : null,
      relation: raw.relation,
      artifact: raw.artifact,
      consumerPhase: raw.consumerPhase,
      producerPhase: raw.producerPhase,
      fromPhase: raw.fromPhase,
      toPhase: raw.toPhase,
      purpose: raw.purpose,
      reason: raw.reason,
    };
  });
}

export function renderedMap(files) {
  const map = new Map();
  for (const file of files) {
    const match = /^(\d+)-/.exec(file);
    if (match) map.set(Number(match[1]), file);
  }
  return map;
}

export function pickChange(manifest, number) {
  if (!manifest) return null;
  const list = Array.isArray(manifest) ? manifest : asArray(manifest.issues ?? manifest.changes ?? manifest.items);
  return list.find(item => issueNumber(item) === number) ?? null;
}

export function projectLedgerReview(graph, ledger) {
  const expectedRaw=asArray(graph?.reviewScope?.openIssueNumbers);
  const expectedNumbers=[...new Set(expectedRaw.filter(Number.isSafeInteger))].sort((a,b)=>a-b);
  const itemNumbers=[...new Set(asArray(graph?.items).map(issueNumber).filter(Number.isSafeInteger))].sort((a,b)=>a-b);
  const anomalies={canonical:[],scope:[],missing:[],extras:[],duplicateIdentities:[],keyMismatches:[],malformed:[]};
  if (expectedRaw.length !== 101 || expectedNumbers.length !== 101 || expectedNumbers.length !== expectedRaw.length) anomalies.canonical.push('canonical reviewScope must contain exactly 101 unique integer issue IDs');
  if (itemNumbers.length !== expectedNumbers.length || itemNumbers.some((number,index)=>number!==expectedNumbers[index])) anomalies.canonical.push('canonical items do not exactly match reviewScope');
  const base={expectedNumbers,records:new Map(),rawRecords:[],anomalies,counts:{AAA_PLAN_PASS:0,FAILED:0,AWAITING_REVIEW:0,STALE:0,PENDING:0,UNKNOWN:0},ledgerPresent:Boolean(ledger),valid:false};
  if (!ledger) return {...base,status:'ABSENT'};
  if (!ledger || typeof ledger !== 'object' || Array.isArray(ledger)) { anomalies.malformed.push('ledger must be an object'); return {...base,status:'INVALID'}; }
  const declared=ledger.scope?.issueNumbers;
  if (!Array.isArray(declared)) anomalies.scope.push('ledger.scope.issueNumbers is missing');
  else {
    const normalized=[...new Set(declared.filter(Number.isSafeInteger))].sort((a,b)=>a-b);
    if (declared.length !== expectedNumbers.length || normalized.length !== expectedNumbers.length || normalized.some((number,index)=>number!==expectedNumbers[index])) anomalies.scope.push('ledger.scope.issueNumbers does not exactly match canonical reviewScope');
  }
  const issueMap=ledger.issues;
  if (!issueMap || typeof issueMap !== 'object' || Array.isArray(issueMap)) { anomalies.malformed.push('ledger.issues must be the actual number-keyed object map'); return {...base,status:'INVALID'}; }
  const expected=new Set(expectedNumbers), identities=new Set();
  for (const [key,record] of Object.entries(issueMap)) {
    base.rawRecords.push({key,record});
    const number=issueNumber(record);
    if (!record || typeof record !== 'object' || !Number.isSafeInteger(number)) { anomalies.malformed.push(`record ${key} has no integer number`); continue; }
    if (key !== String(number)) anomalies.keyMismatches.push(`${key}!=${number}`);
    if (identities.has(number)) anomalies.duplicateIdentities.push(number); else identities.add(number);
    if (!expected.has(number)) anomalies.extras.push(number);
    if (key === String(number) && expected.has(number) && !base.records.has(number)) base.records.set(number,record);
  }
  for (const number of expectedNumbers) if (!base.records.has(number)) anomalies.missing.push(number);
  const invalid=Object.values(anomalies).some(values=>values.length>0);
  if (invalid) return {...base,status:'INVALID'};
  for (const record of base.records.values()) {
    if (record.state === 'PASSED' && record.verdict === 'AAA_PLAN_PASS') base.counts.AAA_PLAN_PASS++;
    else if (record.state === 'FAILED') base.counts.FAILED++;
    else if (record.state === 'AWAITING_REVIEW') base.counts.AWAITING_REVIEW++;
    else if (record.state === 'STALE') base.counts.STALE++;
    else if (record.state === 'PENDING') base.counts.PENDING++;
    else base.counts.UNKNOWN++;
  }
  return {...base,status:'VALID',valid:true};
}

export function formatLedgerReviewCoverage(projection) {
  if (projection.status === 'ABSENT') return 'PENDING 0/101 current planning-body reviews (ledger absent; no records are treated as reviewed); topology review is not an issue grade';
  const anomalies=Object.entries(projection.anomalies).flatMap(([kind,values])=>values.map(value=>`${kind}:${value}`));
  if (!projection.valid) return `BLOCKED invalid ledger projection (${anomalies.join(', ')}); no current planning-body coverage or pass claim is displayed`;
  const counts=projection.counts, reviewedCompleted=counts.AAA_PLAN_PASS+counts.FAILED;
  const breakdown=`AAA_PLAN_PASS/planning-only: ${counts.AAA_PLAN_PASS}; FAILED: ${counts.FAILED}; AWAITING_REVIEW: ${counts.AWAITING_REVIEW}; STALE: ${counts.STALE}; PENDING: ${counts.PENDING}; UNKNOWN: ${counts.UNKNOWN}`;
  if (counts.AAA_PLAN_PASS === 101 && reviewedCompleted === 101 && counts.FAILED === 0 && counts.AWAITING_REVIEW === 0 && counts.STALE === 0 && counts.PENDING === 0 && counts.UNKNOWN === 0) return `COMPLETED PLANNING BODY REVIEW 101/101 (${breakdown}); planning-only body review, not corpus review, product acceptance, or publication`;
  return `PENDING ${reviewedCompleted}/101 current planning-body reviews (${breakdown}); topology review is not an issue grade`;
}

export function pickLedger(projection, number) {
  return projection?.valid ? projection.records.get(number) ?? null : null;
}

function validationError(graph, label = 'canonical graph') {
  const result = validateGraph(graph);
  if (!result.valid) {
    const error = new Error(`${label} validation failed: ${result.errors.join('; ')}`);
    error.code = 'INVALID_GRAPH';
    error.validation = result;
    throw error;
  }
  return result;
}

function lineDiff(before, after) {
  const a = String(before ?? '').split(/\r?\n/);
  const b = String(after ?? '').split(/\r?\n/);
  const n = a.length, m = b.length;
  const dp = Array.from({ length: n + 1 }, () => new Uint32Array(m + 1));
  for (let i = n - 1; i >= 0; i--) for (let j = m - 1; j >= 0; j--) dp[i][j] = a[i] === b[j] ? dp[i + 1][j + 1] + 1 : Math.max(dp[i + 1][j], dp[i][j + 1]);
  const out = [];
  let i = 0, j = 0;
  while (i < n || j < m) {
    if (i < n && j < m && a[i] === b[j]) { out.push({ type: 'same', line: i + 1, text: a[i] }); i++; j++; }
    else if (j < m && (i === n || dp[i][j + 1] >= dp[i + 1][j])) { out.push({ type: 'added', line: j + 1, text: b[j] }); j++; }
    else { out.push({ type: 'removed', line: i + 1, text: a[i] }); i++; }
  }
  return out;
}

function sourceAvailability(graph) {
  return {
    state: 'captured snapshot only',
    entries: asArray(graph?.items).flatMap(item => item.context.map(context => ({
      issue: item.number,
      path: context.path,
      availability: context.availability,
      readWhen: context.readWhen,
    }))),
  };
}

function normalizeReviewStatus(projection, number) {
  if (projection.status === 'ABSENT') return { state: 'LEDGER_NOT_PROVIDED', scope: 'lifecycle is not projected; resolve it from the exact body hash and review ledger', source: 'no issue-review-ledger.json supplied to this viewer' };
  if (!projection.valid) return { state: 'LEDGER_SCOPE_INVALID', scope: 'ledger identity/scope anomalies block current review projection; not a body-review grade', source: 'issue-review-ledger.json', raw:projection.rawRecords, anomalies:projection.anomalies };
  const entry = pickLedger(projection, number);
  if (!entry) return { state: 'LEDGER_SCOPE_INVALID', scope: 'validated ledger projection unexpectedly lacks this canonical issue', source: 'issue-review-ledger.json', anomalies:projection.anomalies };
  const value = entry.state ?? entry.status ?? entry.verdict ?? entry.decision ?? 'RECORDED';
  return { state: String(value), scope: 'recorded review metadata; not a product or issue acceptance grade', source: 'issue-review-ledger.json', raw: entry };
}

export function packGraph(graph, { privateApproval = false, original = new Map(), rendered = new Map(), ledger = null, ledgerProjection = null, manifest = null, renderedContents = new Map(), capture = {} } = {}) {
  const baseNodes = normalizeNodes(graph);
  const reviewProjection=ledgerProjection ?? projectLedgerReview(graph,ledger);
  const resourceContractCounts = { total: baseNodes.reduce((sum, n) => sum + n.resourceBindings.length, 0), workspace: baseNodes.reduce((sum, n) => sum + n.resourceBindings.filter(x => x.template === 'workspace-files-v1').length, 0), unity: baseNodes.reduce((sum, n) => sum + n.resourceBindings.filter(x => x.template === 'unity-editor-v1').length, 0), measurement: baseNodes.reduce((sum, n) => sum + n.resourceBindings.filter(x => x.template === 'measurement-run-v1').length, 0), phaseScopes: baseNodes.reduce((sum, n) => sum + Object.values(n.phaseWriteScopes).reduce((inner, list) => inner + asArray(list).length, 0), 0), outputBindings: baseNodes.reduce((sum, n) => sum + Object.values(n.artifactOutputBindings).reduce((inner, list) => inner + asArray(list).length, 0), 0) };
  const nodes = baseNodes.map(node => {
    const originalIssue = original.get(node.number) ?? null;
    const proposedBody = renderedContents.get(node.number) ?? null;
    return {
      ...node,
      reviewStatus: normalizeReviewStatus(reviewProjection, node.number),
      original: privateApproval && originalIssue ? originalIssue : undefined,
      rendered: privateApproval ? proposedBody : undefined,
      diff: privateApproval && originalIssue ? lineDiff(originalIssue.body, proposedBody ?? '') : undefined,
      change: privateApproval ? pickChange(manifest, node.number) : undefined,
      renderedFile: rendered.get(node.number) ?? null,
    };
  });
  return {
    privateApproval,
    reviewProjection:{status:reviewProjection.status,valid:reviewProjection.valid,counts:reviewProjection.counts,anomalies:reviewProjection.anomalies},
    project: graph.project,
    generatedAt: graph.generatedAt,
    snapshot: graph.snapshot,
    reviewScope: graph.reviewScope,
    requirements: graph.requirements,
    references: normalizeReferences(graph),
    nodes,
    edges: normalizeEdges(graph),
    ontology: graph.ontology,
    limitations: graph.limitations,
    review: graph.review,
    sourceAvailability: sourceAvailability(graph),
    resourceContractCounts,
    conformance: {
      status: capture.conformance?.status ?? 'NOT_PROJECTED',
      source: capture.conformance?.source ?? 'no captured compiler-conformance input supplied',
      scope: capture.conformance?.scope ?? 'no conformance verdict supplied; not inferred from graph structure',
      graphStructure: 'VALID: validateGraph passed',
      bodyReviews: capture.bodyReviewSummary ?? 'LEDGER_NOT_PROVIDED: lifecycle is not projected',
      producerEmission: capture.conformance?.producerEmission ?? 'NOT_PROJECTED: producer emission status not supplied',
    },
    capture: {
      ...capture,
      graphSource: capture.graphSource ?? 'package/docs/context/work-graph.json',
      reviewStatus: capture.bodyReviewSummary ?? (ledger ? 'ledger metadata included; exact body-hash matching still applies' : 'LEDGER_NOT_PROVIDED: lifecycle is not projected by this public viewer'),
      changeManifestPresent: Boolean(manifest),
    },
    notice: privateApproval
      ? '사용자 전용 미리보기입니다. 동결 원본/제안 Markdown/라인 diff를 읽기 전용으로 보여줍니다. Compiler conformance는 별도로 캡처된 검증 입력만 표시하며 graph structure VALID와 동일한 의미가 아닙니다. change-manifest.json이 없으므로 완료된 mutation diff를 주장하지 않습니다. GitHub·AAA·게시·권한·현재 Native frontier는 이 뷰어가 검증하지 않습니다.'
      : '이 HTML은 canonical work graph의 phase-neutral planning/governance projection입니다. Lifecycle verdict/status는 내장하지 않으며 정확한 body hash와 별도 review ledger로만 판정합니다. graph structure는 validateGraph 결과로만 표시되고 compiler conformance·producer emission은 별도 입력 없이는 NOT_PROJECTED입니다. live Native eligibility, implementation, product, safety, performance, certification, external action, publication, AAA 또는 GitHub/source write 권한을 주장하지 않습니다.',
  };
}

const HTML = String.raw;

export function renderReviewHtml(data) {
  const payload = safeJson(data);
  return HTML`<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>CHOOGuard read-only context review</title>
<style>
:root{--ink:#18232d;--muted:#607180;--line:#d6e0e8;--bg:#f3f6f8;--panel:#fff;--navy:#102b40;--blue:#0b679f;--green:#176b49;--red:#a72d39;--orange:#8a5300;--purple:#6940a5;--gray:#546574}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font:14px/1.5 system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif}header{padding:22px 28px 18px;background:var(--navy);color:white;border-bottom:4px solid #2b86b7}h1{font-size:23px;margin:0 0 5px}h2{font-size:18px;margin:0 0 12px}h3{font-size:14px;margin:16px 0 7px}.sub{color:#c9dae6}.notice{margin:15px 28px;padding:12px 14px;border-left:4px solid var(--orange);background:#fff8e8}.toolbar{display:grid;grid-template-columns:2fr 1fr 1fr 1fr 1fr 1fr;gap:8px;padding:0 28px 14px}.toolbar input,.toolbar select{width:100%;padding:9px;border:1px solid #b7c6d1;border-radius:4px;background:#fff}.layout{display:grid;grid-template-columns:370px minmax(0,1fr);gap:14px;padding:0 28px 28px}.panel{background:var(--panel);border:1px solid var(--line);border-radius:7px;padding:15px}.list{max-height:calc(100vh - 285px);overflow:auto}.entry{width:100%;display:block;text-align:left;background:white;border:0;border-bottom:1px solid var(--line);padding:10px 11px;cursor:pointer;color:var(--ink)}.entry:hover,.entry.active{background:#eaf4fa}.entry strong{display:block}.meta,.badges{display:flex;gap:5px;flex-wrap:wrap;margin-top:5px}.badge{display:inline-block;border:1px solid #b6c7d4;border-radius:11px;padding:1px 7px;font-size:11px;color:#29485d;background:#f8fbfd}.badge.pending{color:#7a5100;border-color:#d7bc7a;background:#fff8e9}.badge.blocked{color:#922632;border-color:#e1abb2;background:#fff1f2}.badge.ready{color:#155e9b;border-color:#9ec4df;background:#eef7fd}.badge.in_progress{color:#5d3d90;border-color:#c4afe6;background:#f6f0ff}.badge.historical{color:#5c6570;border-color:#bfc7cd;background:#f1f3f5}.badge.null{color:#6d4e13;border-color:#dec791;background:#fffaf0}.counts{display:flex;gap:8px;flex-wrap:wrap;margin-top:9px}.count{padding:4px 8px;border-radius:4px;background:#e9f1f6;color:#22445b;font-size:12px}.status-grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:7px;margin-top:10px}.status-card{display:flex;flex-direction:column;gap:2px;padding:7px 9px;border:1px solid rgba(255,255,255,.3);border-radius:5px;background:rgba(255,255,255,.1);font-size:11px}.status-card strong{font-size:10px;text-transform:uppercase;letter-spacing:.03em}.status-card.fail{background:#7b252d}.status-card.valid{background:#175f44}.status-card.pending{background:#76500b}.status-card.draft{background:#4b3a68}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}.wide{grid-column:1/-1}.card{border:1px solid var(--line);border-radius:6px;padding:12px;background:white;min-width:0}.label{font-size:11px;color:var(--muted);font-weight:700;letter-spacing:.03em;text-transform:uppercase;margin-bottom:5px}.value{white-space:pre-wrap;overflow-wrap:anywhere}.empty{color:var(--muted);font-style:italic}.table-wrap{overflow:auto}.matrix{width:100%;border-collapse:collapse;min-width:640px}.matrix th,.matrix td{border-bottom:1px solid var(--line);padding:6px 7px;text-align:left;vertical-align:top}.matrix th{font-size:11px;color:var(--muted);white-space:nowrap}.matrix code,code{font:12px ui-monospace,SFMono-Regular,Menlo,monospace;overflow-wrap:anywhere}.pills{display:flex;gap:5px;flex-wrap:wrap}.pill{padding:3px 7px;background:#edf3f7;border-radius:4px;font-size:12px}.graph{width:100%;height:290px;border:1px solid var(--line);border-radius:5px;background:#fbfdff;display:block}.legend{display:flex;gap:10px;flex-wrap:wrap;color:var(--muted);font-size:12px;margin-top:7px}.legend i{display:inline-block;width:18px;border-top:3px solid var(--blue);vertical-align:middle}.legend i.req{border-color:var(--blue)}.legend i.contains{border-color:var(--gray)}.legend i.context{border-color:var(--purple);border-top-style:dashed}.legend i.conflict{border-color:var(--red)}.legend i.historical{border-color:#707981;border-top-style:dotted}.diff{max-height:380px;overflow:auto;border:1px solid var(--line);padding:8px;background:#fafcfd}.diffline{display:block;white-space:pre-wrap;font:12px/1.45 ui-monospace,SFMono-Regular,Menlo,monospace}.diffline.added{background:#eaf8ef;color:#145c37}.diffline.removed{background:#fff0f1;color:#8f2631}.diffline.same{color:#566976}.diffmark{display:inline-block;width:18px;font-weight:700}.columns{display:grid;grid-template-columns:1fr 1fr;gap:10px}.raw{max-height:460px;overflow:auto;background:#101820;color:#d9e6ee;padding:10px;border-radius:4px;font:12px/1.45 ui-monospace,SFMono-Regular,Menlo,monospace;white-space:pre-wrap}.details-nav{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:12px}.details-nav button{border:1px solid var(--line);background:#f7fafc;border-radius:4px;padding:6px 9px;cursor:pointer}.details-nav button.active{background:#dceef8;border-color:#77abc5}@media(max-width:1000px){.toolbar{grid-template-columns:1fr 1fr}.layout{grid-template-columns:1fr;padding:0 14px 20px}.toolbar{padding:0 14px 12px}header{padding:18px 14px}.notice{margin:12px 14px}.list{max-height:310px}.grid,.columns,.status-grid{grid-template-columns:1fr 1fr}}
</style></head><body>
<header><h1>CHOOGuard work graph</h1><div class="sub">read-only phase-qualified context, typed relationships, captured provenance, and bounded historical references</div><div class="counts" id="counts"></div><div class="status-grid" id="statusGrid"></div></header>
<div class="notice" id="notice"></div>
<section class="toolbar"><input id="search" placeholder="issue, canonical work ID, title, artifact, path"><select id="status"><option value="">all captured statuses</option></select><select id="phase"><option value="">all planned phases</option></select><select id="relation"><option value="">all typed relations</option></select><select id="requirement"><option value="">all requirements</option></select><select id="entryType"><option value="">active and historical</option><option value="active">active items</option><option value="historical">historical references</option></select></section>
<main class="layout"><aside class="panel"><h2 id="listTitle">Work items</h2><div class="list" id="entryList"></div></aside><section class="panel" id="detail"></section></main>
<script>const DATA=${payload};
const $=id=>document.getElementById(id);const arr=v=>Array.isArray(v)?v:[];const text=(el,v)=>{el.textContent=v==null?'':String(v)};const jsonText=v=>{try{return JSON.stringify(v,null,2)}catch{return String(v)}};const value=v=>v===null?'null':v===undefined?'not recorded':String(v);let selected={type:'active',id:DATA.nodes[0]?.number??null};
function badge(v,extra=''){const el=document.createElement('span');const cls=String(v??'null').toLowerCase().replace(/[^a-z0-9_]+/g,'_');el.className='badge '+cls+' '+extra;text(el,value(v));return el}
function code(v){const el=document.createElement('code');text(el,value(v));return el}
function card(label,body,wide=false){const c=document.createElement('section');c.className='card'+(wide?' wide':'');const l=document.createElement('div');l.className='label';text(l,label);c.append(l,body);return c}
function empty(parent,msg='not recorded'){const e=document.createElement('div');e.className='empty';text(e,msg);parent.append(e)}
function list(parent,items,render){if(!arr(items).length){empty(parent);return}const ul=document.createElement('ul');for(const item of items){const li=document.createElement('li');const rendered=render(item);if(rendered instanceof Node)li.append(rendered);else text(li,rendered);ul.append(li)}parent.append(ul)}
function table(parent,headers,rows){if(!rows.length){empty(parent);return}const wrap=document.createElement('div');wrap.className='table-wrap';const t=document.createElement('table');t.className='matrix';const tr=document.createElement('tr');for(const h of headers){const th=document.createElement('th');text(th,h);tr.append(th)}t.append(tr);for(const row of rows){const r=document.createElement('tr');for(const cell of row){const td=document.createElement('td');if(cell instanceof Node)td.append(cell);else text(td,cell);r.append(td)}t.append(r)}wrap.append(t);parent.append(wrap)}
function allSearch(n){const e=neighbors(n);return [n.number,n.workId,n.title,n.outcome,n.reviewStatus?.state,...n.requirements,...n.requiredLocks,...n.outputs.map(x=>x.id),...n.inputs.map(x=>x.id),...n.context.map(x=>x.path),...e.map(x=>[x.from,x.to,x.relation,x.artifact,x.reason].join(' '))].join(' ').toLowerCase()}
function optionValues(id,values){for(const v of [...new Set(values.filter(v=>v!==null&&v!==undefined&&v!=='').map(String))].sort()){const o=document.createElement('option');o.value=v;text(o,v);$(id).append(o)}}
function statusCard(label,body,cls=''){const c=document.createElement('div');c.className='status-card '+cls;const strong=document.createElement('strong');text(strong,label);const span=document.createElement('span');text(span,body);c.append(strong,span);return c}
function statusClass(v){const s=String(v||'').toUpperCase();return s.includes('FAIL')?'fail':s.includes('PASS')||s.includes('VALID')?'valid':s.includes('DRAFT')?'draft':'pending'}
function renderStatusGrid(){const box=$('statusGrid');box.replaceChildren();box.append(statusCard('Compiler/body conformance',DATA.conformance.status+' | '+DATA.conformance.source,statusClass(DATA.conformance.status)),statusCard('Graph structure',DATA.conformance.graphStructure+' | not overall pass',statusClass(DATA.conformance.graphStructure)),statusCard('Body review coverage',DATA.conformance.bodyReviews,statusClass(DATA.conformance.bodyReviews)),statusCard('Producer emission',DATA.conformance.producerEmission,statusClass(DATA.conformance.producerEmission)))}
function filtered(){const q=$('search').value.trim().toLowerCase(),st=$('status').value,ph=$('phase').value,rel=$('relation').value,req=$('requirement').value,type=$('entryType').value;const active=DATA.nodes.filter(n=>(!q||allSearch(n).includes(q))&&(!st||String(n.status)===st)&&(!ph||String(n.phase)===ph)&&(!req||n.requirements.includes(req))&&(!rel||DATA.edges.some(e=>(e.fromNumber===n.number||e.toNumber===n.number)&&e.relation===rel))).map(n=>({type:'active',id:n.number,data:n}));const refs=DATA.references.filter(r=>!q||[r.number,r.workId,r.title,r.state,r.limits,r.qualification].join(' ').toLowerCase().includes(q)).map(r=>({type:'historical',id:r.number,data:r}));return type==='active'?active:type==='historical'?refs:[...active,...refs]}
function entryButton(entry){const d=entry.data,b=document.createElement('button');b.className='entry'+(selected.type===entry.type&&selected.id===entry.id?' active':'');b.onclick=()=>{selected={type:entry.type,id:entry.id};render()};const strong=document.createElement('strong');text(strong,(entry.type==='historical'?'Historical #':'#')+d.number+' '+d.title);b.append(strong);const meta=document.createElement('div');meta.className='meta';if(entry.type==='historical'){meta.append(badge('historical'),badge(d.state),badge(d.stateReason));}else{meta.append(badge('work '+d.workId),badge('captured '+d.status),badge('phase '+d.phase),badge(d.reviewStatus?.state,'pending'));}b.append(meta);return b}
function endpointMatches(e,n){const keys=new Set(['issue.'+n.number,'reference.'+n.number]);return keys.has(e.from)||keys.has(e.to)}
function neighbors(n){return DATA.edges.filter(e=>endpointMatches(e,n))}
function endpointRecord(id){const info=/^issue\.(\d+)$/.exec(String(id||''));if(info){const number=Number(info[1]);return DATA.nodes.find(x=>x.number===number)||DATA.references.find(x=>x.number===number)||{number,workId:id,title:'Unresolved endpoint',historical:true,id};}const ref=/^reference\.(\d+)$/.exec(String(id||''));if(ref){const number=Number(ref[1]);return DATA.references.find(x=>x.number===number)||{number,workId:id,title:'Unresolved historical endpoint',historical:true,id};}return {number:null,workId:id,title:'External endpoint',historical:true,id};}
function branchGuards(group,alt){return [...arr(alt.guards).map(g=>({...g,source:'guard'})),...arr(alt.additionalInputs).map(g=>({...g,source:'additional input'}))]}
function graphCanvas(n){const canvas=document.createElement('canvas');canvas.className='graph';canvas.width=1040;canvas.height=300;const ctx=canvas.getContext('2d'),es=neighbors(n);const allIds=[n.id,...new Set(es.flatMap(e=>[e.from,e.to]).filter(id=>id!==n.id))];const shownIds=allIds.slice(0,28);const shown=new Set(shownIds);const center={x:520,y:150},pos=new Map([[n.id,center]]);shownIds.slice(1).forEach((id,i)=>{const a=-Math.PI/2+(Math.PI*2*i/Math.max(shownIds.length-1,1));pos.set(id,{x:center.x+405*Math.cos(a),y:center.y+105*Math.sin(a)})});const colors={requires:'#0b679f',contains:'#546574',context:'#6940a5',conflicts_with:'#a72d39',parallel_with:'#176b49',verifies:'#8a5300',implements:'#8a5300',supersedes:'#8a5300'};const symmetric=new Set(['conflicts_with','parallel_with']);const arrow=(a,b,col)=>{const angle=Math.atan2(b.y-a.y,b.x-a.x),tip=12;ctx.fillStyle=col;ctx.beginPath();ctx.moveTo(b.x,b.y);ctx.lineTo(b.x-tip*Math.cos(angle-.45),b.y-tip*Math.sin(angle-.45));ctx.lineTo(b.x-tip*Math.cos(angle+.45),b.y-tip*Math.sin(angle+.45));ctx.closePath();ctx.fill()};for(const e of es){const a=pos.get(e.from),b=pos.get(e.to);if(!a||!b||!shown.has(e.from)||!shown.has(e.to))continue;const col=colors[e.relation]||'#607180';ctx.strokeStyle=col;ctx.lineWidth=e.relation==='requires'?2:1.5;ctx.setLineDash(e.relation==='context'?[6,4]:e.relation==='conflicts_with'?[3,3]:[]);ctx.beginPath();ctx.moveTo(a.x,a.y);ctx.lineTo(b.x,b.y);ctx.stroke();ctx.setLineDash([]);if(!symmetric.has(e.relation))arrow(a,b,col);ctx.fillStyle=col;ctx.font='11px system-ui';ctx.fillText(e.relation,(a.x+b.x)/2,(a.y+b.y)/2)}for(const id of shownIds){const p=pos.get(id),node=endpointRecord(id);if(!p||!node)continue;const active=node.historical!==true&&node.number===n.number;ctx.fillStyle=active?'#0b679f':node.historical?'#f1f3f5':'#edf4f8';ctx.strokeStyle='#34566b';ctx.lineWidth=1;ctx.beginPath();ctx.roundRect(p.x-75,p.y-20,150,40,5);ctx.fill();ctx.stroke();ctx.fillStyle=active?'white':'#18232d';ctx.textAlign='center';ctx.font='12px system-ui';ctx.fillText(node.number===null?String(node.id).slice(0,22):(node.historical?'Historical #':'#')+node.number,p.x,p.y-2);ctx.fillText(String(node.workId||node.title||'').slice(0,20),p.x,p.y+14)}ctx.textAlign='left';return {canvas,shown:shownIds.length,total:allIds.length,omitted:Math.max(0,allIds.length-shownIds.length)}}
function graphSection(n){const box=document.createElement('div'),drawn=graphCanvas(n);box.append(drawn.canvas);const caption=document.createElement('p');text(caption,'Canvas shows '+drawn.shown+' of '+drawn.total+' connected endpoint entries; omitted from canvas: '+drawn.omitted+'. Full canonical typed-edge table is below. Canvas uses canonical root edges only; oneOf alternatives and branch guards remain separate conditional coverage and are not converted into hard dependencies.');box.append(caption);const legend=document.createElement('div');legend.className='legend';for(const [label,cls] of [['requires: stored consumer → producer','req'],['contains: directed','contains'],['context: directed','context'],['conflicts_with: symmetric','conflict']]){const span=document.createElement('span'),i=document.createElement('i');i.className=cls;span.append(i,document.createTextNode(' '+label));legend.append(span)}box.append(legend);return box}
function renderTruth(n){const box=document.createElement('div');table(box,['level','claim','source'],n.truth.map(x=>[x.level,x.text,x.source]));return box}
function claim70Card(){const box=document.createElement('div'),n=DATA.nodes.find(x=>x.number===70);if(!n){empty(box,'#70 is not present in the canonical graph.');return box}const p=document.createElement('p');text(p,'#70 is preserved as captured claim/context evidence. Its truth levels and sources are shown below; this is not a body-review grade or implementation acceptance.');box.append(p);table(box,['level','claim','source'],n.truth.map(x=>[x.level,x.text,x.source]));return box}
function renderContext(n){const box=document.createElement('div');table(box,['path','availability','readWhen','selector','proves','limits'],n.context.map(x=>[code(x.path),x.availability,x.readWhen,x.selector,x.proves,x.limits]));return box}
function renderInputs(n){const box=document.createElement('div');table(box,['id / canonical artifact','from issue','consumer phase','producer phase','qualification','expected source/digest/scope'],n.inputs.map(x=>[code(x.id),value(x.fromIssue),x.consumerPhase,value(x.producerPhase),x.qualification,[x.expectedSourceRef,x.expectedDigest,x.expectedScope].filter(Boolean).join(' | ')||'not pinned']));return box}
function renderInputQualifiers(n){const box=document.createElement('div');if(!n.inputQualifiers.length){empty(box,'No optional inputQualifier declaration in this canonical item.');return box}table(box,['input ID','qualification','exact artifact/phase targets','use','not required for','global phase input'],n.inputQualifiers.flatMap(q=>q.targets.map(t=>[code(q.inputId),q.qualification,code(t.artifact+' @ '+t.phase),t.use,q.notRequiredFor.join('\n'),'false'])));return box}
function renderRelationships(n){const box=document.createElement('div'),d=n.relationshipDeclarations;if(!d){empty(box,'No optional relationshipDeclarations block in this canonical item; only emitted typed graph edges are authoritative.');return box}for(const relation of ['parallel_with','conflicts_with']){const h=document.createElement('h3');text(h,relation);box.append(h);const block=d[relation];table(box,['evaluated peer','from phase','to phase','static evaluation basis'],block.evaluatedPairs.map(x=>[code('#'+x.issue),x.fromPhase,x.toPhase,x.reason]));if(block.items.length)table(box,['emitted peer relation','from phase','to phase','reason'],block.items.map(x=>[code('#'+x.issue),x.fromPhase,x.toPhase,x.reason]));else{const p=document.createElement('p');text(p,'Guaranteed static pair: none. Reasoned none: '+block.reasonedNone);box.append(p)}}return box}
function renderCheckMappings(n){const box=document.createElement('div');if(!n.checkEvidenceMappings.length){empty(box,'No optional phase-exact checkEvidenceMappings declaration in this canonical item.');return box}const rows=[];for(const m of n.checkEvidenceMappings)for(const phase of ['candidate','accept'])rows.push([m.rawCheckEvidence,phase,code(m[phase].artifact),code(m[phase].indexPath),code(m[phase].rawReceiptDirectory)]);table(box,['raw check evidence','phase','artifact','stable index','immutable raw receipt directory'],rows);return box}
function renderOutputs(n){const box=document.createElement('div');table(box,['artifact ID','phase','path','qualification / scope','digest requirements','acceptance / limitation'],n.outputs.map(x=>[code(x.id),x.producerPhase,x.path,[x.qualification,x.scope].filter(Boolean).join('\n'),jsonText(x.digestRequirements),[x.acceptance,x.wholeIssueAcceptance===false?'wholeIssueAcceptance=false':''].filter(Boolean).join('\n')]));return box}
function renderDeps(n,field){const box=document.createElement('div');table(box,['other issue','artifact ID','consumer phase','producer phase','reason','access guard'],n[field].map(x=>[code('#'+x.issue),code(x.artifact),x.consumerPhase,x.producerPhase,x.reason,x.accessGuard?jsonText(x.accessGuard):'none']));return box}
function renderEdges(n){const box=document.createElement('div');table(box,['from','to','typed relation','artifact / purpose','consumer phase','producer phase','reason'],neighbors(n).map(e=>[code(e.from),code(e.to),e.relation,e.artifact||e.purpose||'not applicable',e.consumerPhase||e.fromPhase||'',e.producerPhase||e.toPhase||'',e.reason]));return box}
function renderOR(n){const box=document.createElement('div');if(!n.oneOfInputs.length){empty(box);return box}for(const group of n.oneOfInputs){const h=document.createElement('h3');text(h,group.artifact+' | consumer phase '+group.consumerPhase);box.append(h);const rows=[];for(const alt of group.alternatives){const guardLines=branchGuards(group,alt).map(g=>g.source+': '+jsonText(g));rows.push([code('#'+alt.issue),code(alt.artifact),alt.producerPhase,guardLines.join('\n')||'none']);}table(box,['alternative issue','alternative artifact','producer phase','branch guards / additional inputs'],rows);const p=document.createElement('p');text(p,'Resolution: '+group.resolution);box.append(p)}return box}
function renderPredicates(n){const box=document.createElement('div');table(box,['predicate ID','kind','consumer phase','state','required','native issue edge'],n.guardPredicates.map(p=>[code(p.id),p.kind,p.consumerPhase,p.state,p.required,String(p.nativeIssueEdge)]));return box}
function renderWriterContext(){const box=document.createElement('div');const p=document.createElement('p');text(p,'writerContext is required by the canonical live-state contract, but no live writerContext was supplied to this read-only graph viewer. Authentication, permission, and live physical-alias claims are therefore not asserted.');box.append(p);table(box,['claim boundary','value'],[['writerContext required','yes, live-state schema requirement'],['authentication','not supplied / not asserted'],['permission','not supplied / not asserted'],['live physical alias claim','not supplied / not asserted'],['lease acquisition','not performed by this viewer']]);return box}
function renderResourceContracts(n){const box=document.createElement('div');const rows=n.resourceBindings.map(c=>[code(c.template),c.phases.join(', '),code(c.requiredFields?.join(', ')||'not recorded'),c.pathsFrom||'not applicable',c.keyExpansion||'not applicable',c.failure||'not recorded',c.generatedOutputRoots?.join(', ')||'not applicable',c.identityKeys?.join(', ')||'not applicable',c.liveValues===null?'null':c.liveValues===undefined?'not applicable':jsonText(c.liveValues),c.rule||'not applicable']);table(box,['template','phases','actual required fields','paths from','key expansion','failure rule','generated output roots','identity keys','live values','rule'],rows);return box}
function renderLiveResourceBoundary(n){const box=document.createElement('div');const templates=[['workspace-files-v1','workspaceId, realCheckoutPath, volumeIdentity, branchRef, baseRef, selectedWritePaths, generatedClosure, leaseOwner, fencingToken','real checkout, volume, branch, base, selected paths, generated closure, owner and fence are not supplied until a fresh caller provides them'],['unity-editor-v1','workspaceId, editorInstanceId, editorPid, editorProcessStart, generatedOutputRoots, projectSettingsPaths, hostProcessNamespace','real checkout/Editor/process/output identity values are not supplied until a fresh caller provides them'],['measurement-run-v1','runId, outputRoot, hostResourceNamespace, processGroup, ports, devices, serviceRooms, leaseOwner, fencingToken','real output root/process/network/device/service identities are not supplied until a fresh caller provides them']];table(box,['live contract','required caller fields','current viewer state'],templates);const p=document.createElement('p');text(p,'These are caller-supplied evidence fields only. This viewer does not resolve paths or volumes, inspect branches/processes/ports/devices, acquire leases, authenticate owners, or grant permission.');box.append(p);return box}
function renderWrites(n){const box=document.createElement('div');const phases=['prepare','candidate','accept'];const phaseRows=[];for(const phase of phases)for(const w of arr(n.phaseWriteScopes?.[phase]))phaseRows.push([phase,code(w.path),w.mode,w.physicalBinding||'not recorded',String(w.canonicalWriteAllowed),w.requiresRegistration===undefined?'not recorded':String(w.requiresRegistration),w.originalMode||w.reason||'']);const h0a=document.createElement('h3');text(h0a,'Current phaseWriteScopes (authoritative)');box.append(h0a);table(box,['phase','path','mode','physical binding','canonical write allowed','registration','qualification'],phaseRows);const h0=document.createElement('h3');text(h0,'Artifact output bindings (authoritative)');box.append(h0);const bindingRows=[];for(const phase of ['candidate','accept'])for(const b of arr(n.artifactOutputBindings?.[phase]))bindingRows.push([phase,code(b.artifact),code(b.path),b.pathKind||'not recorded',code(b.scopePath),b.scopeMode||'not recorded',code(b.lockId),b.rawReceiptPolicy||'null',b.receiptStorage?jsonText(b.receiptStorage):'not declared',String(b.canonicalWriteAllowed)]);table(box,['phase','artifact','path','path kind','scope path','scope mode','lock ID','raw receipt policy','receipt storage','canonical write allowed'],bindingRows);const h1=document.createElement('h3');text(h1,'Legacy writeScope (compatibility only, not write authority)');box.append(h1);table(box,['path','mode','phases','lock'],n.writeScope.map(w=>[code(w.path),w.mode,w.phases.join(', '),w.lock||'none']));const h=document.createElement('h3');text(h,'Required locks');box.append(h);const p=document.createElement('div');p.className='pills';for(const x of n.requiredLocks){const s=document.createElement('span');s.className='pill';text(s,x);p.append(s)}box.append(p);const rh=document.createElement('h3');text(rh,'Full resource contract declarations');box.append(rh,renderResourceContracts(n));const rb=document.createElement('h3');text(rb,'Live resource evidence boundary');box.append(rb,renderLiveResourceBoundary(n));const wh=document.createElement('h3');text(wh,'Writer context boundary');box.append(wh,renderWriterContext());return box}
function renderCaptured(n){const box=document.createElement('div');const b=n.board;table(box,['field','captured value','provenance'],[['canonical work ID',n.workId,'work graph item.workId'],['captured baseline work ID',n.original?.workId??'not available in baseline capture',n.original?'baseline/issues.json':'no captured workId field; not invented'],['issue number',n.number,'canonical item.number'],['captured board status',value(b.status),b.reason],['planned phase',value(b.phase),'planned phase context; not a recorded/live phase'],['status observed at',n.capturedProjectStatus?.observedAt||'null',n.capturedProjectStatus?.source||'not recorded'],['source issue updated at',n.capturedProjectStatus?.sourceIssueUpdatedAt||'null','separate issue metadata, not a transition'],['current eligibility','not projected','authenticated current frontier unavailable']]);return box}
function reviewBlock(n){const box=document.createElement('div');const h=document.createElement('div');h.className='badges';h.append(badge(n.reviewStatus?.state,'pending'));const s=document.createElement('span');s.className='badge';text(s,n.reviewStatus?.scope);h.append(s);box.append(h);if(n.reviewStatus?.raw){const pre=document.createElement('pre');pre.className='raw';text(pre,jsonText(n.reviewStatus.raw));box.append(pre)}return box}
function privateCompare(n){const box=document.createElement('div');const note=document.createElement('p');text(note,DATA.capture.changeManifestPresent?'change-manifest present; inspect recorded metadata below.':'change-manifest.json absent. No completed mutation diff is asserted.');box.append(note);const cols=document.createElement('div');cols.className='columns';for(const [label,body] of [['Captured baseline body',n.original?('#'+n.original.number+' '+n.original.title+'\n\n'+n.original.body):'baseline body unavailable'],['Proposed rendered Markdown',n.rendered??'rendered body unavailable']]){const p=document.createElement('pre');p.className='raw';text(p,body);const c=document.createElement('div');const h=document.createElement('h3');text(h,label);c.append(h,p);cols.append(c)}box.append(cols);const dh=document.createElement('h3');text(dh,'Line-level diff: baseline body versus rendered Markdown');box.append(dh);const diff=document.createElement('div');diff.className='diff';if(!n.diff){empty(diff,'diff unavailable');}else for(const row of n.diff){const line=document.createElement('span');line.className='diffline '+row.type;const mark=document.createElement('span');mark.className='diffmark';text(mark,row.type==='added'?'+':row.type==='removed'?'-':' ');line.append(mark,document.createTextNode(String(row.line).padStart(4,' ')+' '+row.text));diff.append(line)}box.append(diff);if(n.change){const mh=document.createElement('h3');text(mh,'Recorded change metadata');box.append(mh);const pre=document.createElement('pre');pre.className='raw';text(pre,jsonText(n.change));box.append(pre)}return box}
function detailActive(n){const root=$('detail');root.replaceChildren();if(!n){empty(root,'No active work item matches the current filters.');return}const h=document.createElement('h2');text(h,'#'+n.number+' '+n.title);root.append(h);const badges=document.createElement('div');badges.className='badges';badges.append(badge('canonical '+n.workId),badge('kind '+n.kind),badge('captured '+n.status),badge('planned phase '+n.phase),badge('eligibility not projected'),badge(n.reviewStatus?.state,'pending'));root.append(badges);const grid=document.createElement('div');grid.className='grid';const out=document.createElement('div');out.className='value';text(out,n.outcome);grid.append(card('Outcome, not acceptance',out));const ng=document.createElement('div');list(ng,n.nonGoals,x=>x);grid.append(card('Non-goals',ng));grid.append(card('Typed local graph, direct neighbors and phases',graphSection(n),true));grid.append(card('Captured status and time',renderCaptured(n),true));grid.append(card('Explicit captured claim #70',claim70Card(),true));grid.append(card('Truth claims with source and limits',renderTruth(n),true));grid.append(card('Context / source availability / proofs / limits',renderContext(n),true));grid.append(card('Current inputs, exact artifact identity and phase',renderInputs(n),true));grid.append(card('Input qualifiers and bounded authority',renderInputQualifiers(n),true));grid.append(card('Relationship declarations',renderRelationships(n),true));grid.append(card('Outputs, qualification, digest requirements, acceptance and limits',renderOutputs(n),true));grid.append(card('Hard predecessors',renderDeps(n,'hardPredecessors'),true));grid.append(card('Hard successors',renderDeps(n,'hardSuccessors'),true));grid.append(card('oneOf alternatives, branch guards and additional inputs',renderOR(n),true));grid.append(card('Non-artifact guard predicates',renderPredicates(n),true));grid.append(card('Write scope, locks and resources',renderWrites(n),true));grid.append(card('Phase-exact check evidence mappings',renderCheckMappings(n),true));const checks=document.createElement('div');list(checks,n.checks,x=>jsonText(x));grid.append(card('Checks, expected/negative/evidence',checks));const ac=document.createElement('div');list(ac,n.acceptance,x=>x);grid.append(card('Acceptance statements, not completed claims',ac));const stop=document.createElement('div');list(stop,n.stopConditions,x=>x);grid.append(card('Stop conditions',stop));const hand=document.createElement('div');text(hand,jsonText(n.handoff));grid.append(card('Handoff',hand));const notes=document.createElement('div');text(notes,jsonText(n.reviewNotes));grid.append(card('Review notes',notes));const req=document.createElement('div');const p=req.querySelector;const pills=document.createElement('div');pills.className='pills';for(const x of n.requirements){const s=document.createElement('span');s.className='pill';text(s,x);pills.append(s)}if(!n.requirements.length)empty(req);else req.append(pills);grid.append(card('Requirement links',req));const rel=document.createElement('div');list(rel,n.related,x=>'#'+x);grid.append(card('Related work IDs',rel));grid.append(card('Direct canonical typed edges',renderEdges(n),true));grid.append(card('Local graph neighborhood',graphSection(n),true));grid.append(card('Review status',reviewBlock(n)));if(DATA.privateApproval)grid.append(card('Private original/proposed bodies and line-level diff',privateCompare(n),true));const raw=document.createElement('pre');raw.className='raw';text(raw,jsonText(n.raw));grid.append(card('Raw canonical item JSON (supplementary)',raw,true));root.append(grid)}
function renderHistoricalContext(r){const box=document.createElement('div'),ctx=r.absorbedHistoricalContext;if(!ctx){empty(box,'No absorbedHistoricalContext record supplied.');return box}table(box,['field','captured value','meaning'],[['closure disposition',ctx.closureDisposition,'human-readable typed closure'],['closure comment',ctx.closureCommentUrl,'captured closure source'],['data rejected',String(ctx.dataRejected),'schema const false; not data rejection'],['accepted as completed',String(ctx.acceptedAsCompleted),'schema const false; not completed producer'],['read only',String(ctx.readOnly),'not executable'],['executable',String(ctx.executable),'must remain false'],['current output',String(ctx.currentOutput),'must remain false'],['native issue edge',String(ctx.nativeIssueEdge),'must remain false'],['source issue updated at',ctx.sourceIssueUpdatedAt,'separate from observation time'],['transferred fire scope','#'+ctx.transferredRemainingScope?.fire13Region,'remaining scope transfer'],['transferred crowd scope','#'+ctx.transferredRemainingScope?.crowd13Region,'remaining scope transfer']]);const h=document.createElement('h3');text(h,'Existing laboratory scope');box.append(h);table(box,['field','value'],Object.entries(ctx.existingLaboratoryScope||{}).map(([k,v])=>[k,String(v)]));const h2=document.createElement('h3');text(h2,'Attributed context artifacts and non-executable consumers');box.append(h2);const rows=[];for(const a of arr(ctx.contextArtifacts)){for(const c of arr(a.contextConsumers))rows.push([code(a.artifact),a.path||'not recorded',a.role||'not recorded',a.producerKind||'not recorded',String(c.issue),c.readWhen||'not recorded',String(c.acceptanceInput),String(c.nativeIssueEdge)]);if(!a.contextConsumers?.length)rows.push([code(a.artifact),a.path||'not recorded',a.role||'not recorded',a.producerKind||'not recorded','none','not recorded','not recorded','not recorded'])}table(box,['artifact attribution','path','role','producer kind','consumer issue','readWhen','acceptance input','native issue edge'],rows);return box}
function renderHistoricalOutputs(r){const box=document.createElement('div');const rows=[];for(const o of r.outputs){const h=o.historicalSnapshot;rows.push([code(o.id),o.path,o.producerPhase,o.sourceRef||'not recorded',o.outputDigest||'not recorded',h?.testedInputCodeRef||'not recorded',h?.reportPublicationRef||o.sourceRef||'not recorded',h?.readOnly===undefined?'not recorded':String(h.readOnly),h?.executable===undefined?'not recorded':String(h.executable)])}table(box,['artifact','path','phase','report publication SHA','output digest','tested-code SHA','report publication ref','read only','executable'],rows);if(!rows.length)empty(box,'No executable historical output; this reference is context-only.');return box}
function detailReference(r){const root=$('detail');root.replaceChildren();if(!r){empty(root,'No historical reference matches the current filters.');return}const h=document.createElement('h2');text(h,'Historical reference #'+r.number+' '+r.title);root.append(h);const badges=document.createElement('div');badges.className='badges';badges.append(badge('historical read-only'),badge(r.state),badge(r.stateReason));root.append(badges);const grid=document.createElement('div');grid.className='grid';const body=document.createElement('div');text(body,jsonText({workId:r.workId,state:r.state,stateReason:r.stateReason,closedAt:r.closedAt,observedAt:r.observedAt,url:r.url}));grid.append(card('Captured identity and status',body));const limits=document.createElement('div');text(limits,(r.qualification||'')+'\n\n'+(r.limits||''));grid.append(card('Qualification and limits',limits));grid.append(card('Typed historical context, closure, transfer, and attribution',renderHistoricalContext(r),true));grid.append(card('#69 report publication versus tested-code SHA',renderHistoricalOutputs(r),true));const outputs=document.createElement('div');list(outputs,r.outputs,x=>jsonText(x));grid.append(card('Historical outputs, not executable current inputs',outputs));const raw=document.createElement('pre');raw.className='raw';text(raw,jsonText(r.raw));grid.append(card('Raw historical reference JSON',raw,true));root.append(grid)}
function render(){const shown=filtered(),box=$('entryList');box.replaceChildren();for(const e of shown)box.append(entryButton(e));if(!shown.some(e=>e.type===selected.type&&e.id===selected.id))selected=shown[0]??{type:'active',id:null};const node=selected.type==='active'?DATA.nodes.find(n=>n.number===selected.id):DATA.references.find(r=>r.number===selected.id);selected.type==='active'?detailActive(node):detailReference(node);text($('listTitle'),(selected.type==='active'?'Work items':'Historical references')+' ('+shown.length+' shown)');text($('counts'),'active '+DATA.nodes.length+' | typed edges '+DATA.edges.length+' | requirements '+DATA.requirements.length+' | historical references '+DATA.references.length+' | resources '+DATA.resourceContractCounts.total+' (workspace '+DATA.resourceContractCounts.workspace+', Unity '+DATA.resourceContractCounts.unity+', measurement '+DATA.resourceContractCounts.measurement+') | phase scopes '+DATA.resourceContractCounts.phaseScopes+' | output bindings '+DATA.resourceContractCounts.outputBindings+' | '+DATA.capture.reviewStatus+' | no live Native frontier');}
function init(){optionValues('status',DATA.nodes.map(n=>n.status));optionValues('phase',DATA.nodes.map(n=>n.phase));optionValues('relation',DATA.edges.map(e=>e.relation));optionValues('requirement',DATA.requirements.map(r=>r.id).concat(DATA.nodes.flatMap(n=>n.requirements)));for(const id of ['search','status','phase','relation','requirement','entryType'])$(id).addEventListener('input',render);text($('notice'),DATA.notice);renderStatusGrid();render()}init();</script></body></html>`;
}

const PACKAGE_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const defaults = { graph: path.join(PACKAGE_ROOT, 'docs/context/work-graph.json'), out: path.join(PACKAGE_ROOT, 'docs/context/index.html') };
function usage(){return ['Usage: node package/scripts/context/render_work_graph.mjs [--graph <path>] [--out <path>]','','The canonical graph is validated before any output directory or file is touched.','The renderer reads exact work-graph schema fields and writes self-contained file://-safe HTML.'].join('\n');}
function parse(argv){const args={...defaults};for(let i=0;i<argv.length;i++){const token=argv[i];if(token==='--help'||token==='-h')return {help:true};if(token==='--graph'||token==='--out'){const val=argv[++i];if(!val||val.startsWith('--'))throw new Error('missing value for '+token);args[token.slice(2)]=path.resolve(process.cwd(),val);continue}throw new Error('unknown argument: '+token)}return args;}
const exists = file => fs.access(file).then(()=>true).catch(()=>false);

export async function renderProductionGraph({graph, out}) {
  if (!(await exists(graph))) { const e = new Error('graph input is missing: '+graph); e.code='MISSING_GRAPH'; throw e; }
  const parsed = JSON.parse(await fs.readFile(graph,'utf8'));
  validationError(parsed);
  const nodes = normalizeNodes(parsed);
  const references = normalizeReferences(parsed);
  const data = packGraph(parsed, { capture: { capturedAt: parsed.snapshot?.capturedAt ?? parsed.generatedAt } });
  await fs.mkdir(path.dirname(out), { recursive: true });
  await fs.writeFile(out, renderReviewHtml(data), 'utf8');
  return { status:'generated', graph, out, items:nodes.length, edges:data.edges.length, historicalReferences:references.length, validation:'passed' };
}

async function main(){let args;try{args=parse(process.argv.slice(2));}catch(error){console.error(JSON.stringify({status:'invalid_arguments',message:error.message},null,2));process.exitCode=64;return;}if(args.help){console.log(usage());return;}try{console.log(JSON.stringify(await renderProductionGraph(args),null,2));}catch(error){const status=error.code==='MISSING_GRAPH'?'waiting_for_graph':'failed_validation';console.error(JSON.stringify({status,message:error.message,graph:args.graph,out:args.out,partialOutput:false},null,2));process.exitCode=error.code==='MISSING_GRAPH'?2:3;}}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href)main();
