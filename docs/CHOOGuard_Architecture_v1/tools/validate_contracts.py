"""Design-contract validator, not a simulation or a Unity runtime adapter."""
from __future__ import annotations
from typing import Any

def validate_graph(document: dict[str, Any]) -> None:
    components = document['components']
    ids = [c['id'] for c in components]
    if len(ids) != len(set(ids)):
        raise ValueError('Duplicate component ID')
    deps = {c['id']: c['compileDependsOn'] for c in components}
    state: dict[str, int] = {}
    def visit(node: str) -> None:
        if node not in deps:
            raise ValueError(f'Unknown dependency: {node}')
        if state.get(node) == 1:
            raise ValueError(f'Compile dependency cycle at {node}')
        if state.get(node) == 2:
            return
        state[node] = 1
        for dep in deps[node]:
            visit(dep)
        state[node] = 2
    for node in ids:
        visit(node)
    if deps.get('DOMAIN'):
        raise ValueError('Domain must not depend on technology or application modules')

def _ticks(value: Any) -> int:
    if not isinstance(value, str) or not value.isascii() or not value.isdecimal():
        raise ValueError('Simulation ticks must be unsigned decimal characters in a string')
    n = int(value)
    if n > 9223372036854775807:
        raise ValueError('Simulation ticks exceed signed Int64 range')
    return n

def validate_semantics(kind: str, d: dict[str, Any]) -> None:
    """Selected cross-field invariants. File existence and real model correctness are NOT checked."""
    if kind == 'checkpoint':
        t = _ticks(d['simulationTicks'])
        worker_ids = [w['workerId'] for w in d['workers']]
        if len(worker_ids) != len(set(worker_ids)):
            raise ValueError('Duplicate checkpoint worker')
        if d['status'] == 'COMPLETE':
            if set(worker_ids) != set(d['requiredWorkerIds']):
                raise ValueError('Complete checkpoint must list every required worker exactly once')
            for w in d['workers']:
                if _ticks(w['atTicks']) != t or w['inputHash'] != d['inputHash']:
                    raise ValueError('Checkpoint worker time/input binding mismatch')
                policy = w['restartPolicy']
                if policy == 'NONE':
                    raise ValueError('Non-restartable worker cannot support complete checkpoint')
                if policy == 'EXACT_STATE' and not w.get('stateHash'):
                    raise ValueError('Missing required serialized state')
                if policy == 'REPLAY_FROM_START' and not w.get('replayRecipeHash'):
                    raise ValueError('Missing replay recipe')
                if d['branchCapability'] == 'EXACT_STATE' and policy != 'EXACT_STATE':
                    raise ValueError('Exact branch cannot hide a replay-only worker')
    elif kind == 'field-batch':
        if _ticks(d['time']['fromTicks']) > _ticks(d['time']['toTicks']):
            raise ValueError('Field batch moves backwards in time')
        keys = [(x['quantity'], x['spatialSupportId']) for x in d['fields']]
        if len(keys) != len(set(keys)):
            raise ValueError('Duplicate field/support in one batch')
    elif kind == 'domain-event':
        _ticks(d['simulationTicks'])

def input_match(result: dict[str, Any], expected: dict[str, Any]) -> bool:
    """Contract comparison only; not actual worker dispatch or result publication."""
    keys = ('runId','jobId','workerId','workerEpoch','inputHash','boundaryRevision')
    return all(key in result and key in expected and result[key] == expected[key] for key in keys)
