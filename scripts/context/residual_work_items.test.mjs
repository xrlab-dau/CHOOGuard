import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {validateGraph, inputProjections} from './work_graph.mjs';

const load = path => JSON.parse(readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8'));
const graph = load('docs/context/work-graph.json');
const availability = load('docs/context/source-availability.json');
const policy = load('docs/context/work-orders/policy.json');
const ref = 'bdcb714849fcdaa2d7d9f7f431b661a26eb3090a';
const item = number => graph.items.find(value => value.number === number);
const hash = value => createHash('sha256').update(JSON.stringify(value)).digest('hex');
const requires = (consumer, producer, artifact, consumerPhase, producerPhase) => {
  const inputs = inputProjections(graph, consumer)[consumerPhase].hard;
  assert.ok(inputs.some(input => input.issue === producer && input.artifact === artifact && input.producerPhase === producerPhase), `${consumer}@${consumerPhase} needs ${producer}@${producerPhase} ${artifact}`);
};

test('four real residual issues extend the canonical corpus without inventing acceptance', () => {
  assert.equal(graph.items.length, 105);
  assert.equal(graph.reviewScope.openIssueNumbers.length, 105);
  const result = validateGraph(graph);
  assert.equal(result.valid, true, result.errors.join('\n'));
  for (const number of [149, 150, 151, 152]) {
    const value = item(number);
    assert.ok(value.truth.some(entry => entry.source === `https://github.com/xrlab-dau/CHOOGuard/issues/${number}`));
    assert.equal(value.assigneePolicy, 'unassigned_until_claim');
    assert.ok(value.outputs.every(output => output.proposed && output.wholeIssueAcceptance === false));
    assert.ok(policy.goalOverrides[number]);
    assert.ok(policy.domains[number]);
  }
});

test('causal contract precedes scheduler implementation while executed corpus gates real rehearsal', () => {
  requires(150, 149, 'artifact:149:load-causal-contract:candidate', 'candidate', 'candidate');
  requires(150, 149, 'artifact:149:load-causal-corpus:candidate', 'candidate', 'candidate');
  requires(143, 150, 'artifact:150:engine-metrics-binding:candidate', 'candidate', 'candidate');
  requires(150, 149, 'artifact:149:load-causal-corpus:accept', 'accept', 'accept');
  requires(140, 149, 'artifact:149:load-causal-corpus:accept', 'candidate', 'accept');
  requires(140, 150, 'artifact:150:engine-metrics-run:accept', 'candidate', 'accept');
  assert.ok(!item(149).hardPredecessors.some(input => input.issue === 140));
  assert.ok(!item(149).inputs.some(input => input.fromIssue === 140));
  assert.ok(!item(149).oneOfInputs.some(group => group.alternatives.some(input => input.issue === 140)));
});

test('scheduler implementation does not wait for binaries but real protocol execution requires runnable payload', () => {
  requires(150, 143, 'artifact:143:fmp13a-runnable-build-payload:candidate', 'accept', 'candidate');
  assert.ok(!item(150).hardPredecessors.some(input => input.issue === 143 && input.consumerPhase === 'candidate'));
  for (const phase of ['candidate', 'accept']) {
    const bindings = item(143).artifactOutputBindings[phase];
    const runnable = bindings.find(binding => binding.artifact === `artifact:143:fmp13a-runnable-build-payload:${phase}`);
    const attempt = bindings.find(binding => binding.artifact === `artifact:143:fmp13a-evidence:${phase}`);
    assert.notEqual(runnable.path, attempt.path);
    assert.equal(attempt.bindingKind, 'diagnostic_context_output_not_runnable');
    assert.equal(runnable.receiptStorage.appendOnly, true);
    assert.equal(runnable.receiptStorage.completeRunRecordRequired, true);
    assert.ok(!item(150).hardPredecessors.some(input => input.artifact === attempt.artifact));
  }
});

test('P0 staging ownership gates live voice and service delivery without treating old test totals as proof', () => {
  requires(100, 151, 'artifact:151:staging-ownership-proof:accept', 'candidate', 'accept');
  requires(143, 151, 'artifact:151:staging-ownership-proof:accept', 'candidate', 'accept');
  assert.match(item(151).outcome, /P0/);
  const contract = JSON.stringify(item(151));
  assert.match(contract, /24/);
  assert.match(contract, /89/);
  assert.match(contract, /쓰기.*게시|credential.*publication/);
  assert.ok(!item(151).truth.some(entry => entry.level === 'currently_verified'));
});

test('70 skipped diagnosis gates integration while retaining tests and physical Editor isolation', () => {
  requires(91, 152, 'artifact:152:fixture-discovery-proof:accept', 'accept', 'accept');
  requires(140, 152, 'artifact:152:fixture-discovery-proof:accept', 'candidate', 'accept');
  const value = item(152);
  assert.match(JSON.stringify(value.stopConditions), /삭제/);
  assert.match(JSON.stringify(value.stopConditions), /skip/);
  const unity = value.resourceBindings.find(resource => resource.template === 'unity-editor-v1');
  assert.ok(unity.identityKeys.includes('editor-process:{hostProcessNamespace}:{editorPid}:{editorProcessStart}'));
  assert.match(unity.rule, /Different checkouts\/Editors\/outputs can run concurrently/);
  assert.ok(!value.requiredLocks.some(lock => /global.*unity|unity.*global/i.test(lock)));
  for (const number of [150, 152]) {
    const owner = item(number);
    const roots = owner.resourceBindings.find(resource => resource.template === 'unity-editor-v1').generatedOutputRoots;
    for (const phase of ['candidate', 'accept']) {
      for (const binding of owner.artifactOutputBindings[phase].filter(output => output.pathKind === 'immutable_receipt_index')) {
        assert.equal(binding.receiptStorage.mode, 'separate_immutable_raw_records');
        assert.ok(roots.some(root => binding.receiptStorage.rawScopePath === `${root}**`));
        assert.notEqual(binding.receiptStorage.rawLockId, binding.lockId);
        assert.ok(owner.phaseWriteScopes[phase].some(scope => scope.physicalBinding === binding.receiptStorage.rawLockId));
      }
    }
  }
});

test('PR153 path-qualified publication remains source-only and preserves captured availability', () => {
  const expected = {
    'scripts/dev/run_foundation_load.py': '753815024f028dc17baaa2a08121a5c61a9cc4bbe5a0d959d37694b58f94c92f',
    'services/livekit/configure_local.py': '1922ec3508f6794addae0b0834d4770e338d13c43dac44a4a630a872bdcfdf3f',
    'Packages/com.xrlab.chooguard.foundation/Multiplayer/Runtime/RuntimeMetricAccumulator.cs': '115e480b758a4d084c5b0ae3c283fd49e1297d11e354de457164627c3103f2bb',
    'Packages/com.xrlab.chooguard.foundation/Tests/Editor/CrowdLegacyFixture.cs': '4493963a421b27da4f5701baa8d0fc7c6dc57c43d86b1a66926f76197e1526e3',
  };
  for (const [path, digest] of Object.entries(expected)) {
    const entry = availability.files[path];
    const qualified = entry.qualifiedRefs.find(source => source.ref === ref);
    assert.deepEqual(qualified.digest, {algorithm: 'sha256', value: digest});
    assert.equal(qualified.url, `https://github.com/xrlab-dau/CHOOGuard/blob/${ref}/${path}`);
    assert.equal(qualified.access, 'published');
    assert.equal(qualified.qualification, 'source-only');
    assert.match(qualified.limits, /compile.*미검증/);
    assert.equal(qualified.accepted, undefined);
  }
  assert.equal(availability.files['scripts/dev/run_foundation_load.py'].availability, 'local_unpublished');
  for (const value of [149, 150, 151, 152].map(item)) {
    requires(value.number, 120, 'artifact:120:source-only-access-manifest:candidate', 'candidate', 'candidate');
    for (const context of value.context.filter(source => source.availability === 'published' && source.sourceRef === ref)) {
      assert.ok(availability.files[context.path]?.qualifiedRefs.some(source => source.ref === ref));
    }
  }
});

test('the original claim, snapshot, historical references and failed review lineage are unchanged', () => {
  assert.equal(hash(item(70)), '8ecf3378676b9a9716d2102f5aedfc15aeb82b38cdbb03e1198678b00a14ee6f');
  assert.equal(hash(graph.snapshot), '4deb90495c965060a82e446ee9663b8105fbe37462e77c411f4862081b522bea');
  assert.equal(hash(graph.references), '1de1cdcd6d466f6f373c9b7c7599d88aff1ee9f24c3ac2fab8c1b252a7a14e86');
  assert.equal(hash(graph.review), '2af8ff9668c1a6df772f6d8969844cf59ebe4637812c636bf1e38408c8965c4f');
  assert.equal(policy.claims['70'].holder, 'Adrianaline');
});

test('current external-generation and automated review execution are paused, not retroactively accepted', () => {
  for (const number of [106, 107]) {
    const value = item(number);
    assert.match(value.outcome, /보류|중단/);
    assert.match(JSON.stringify(value.stopConditions), /AAA\/REWORK/);
    assert.ok(value.guardPredicates.some(guard => guard.kind === 'pm_execution_resumption' && guard.state === 'unverified'));
    assert.match(policy.goalOverrides[number], /보류|중단/);
    assert.equal(value.board.capturedProjectStatus.value, 'In progress');
  }
});
