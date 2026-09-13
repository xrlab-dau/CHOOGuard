#!/usr/bin/env python3
"""고정 소스 AST와 명시적 PM 관계를 결합한다. 실행·수용 권한은 부여하지 않는다."""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shlex
import subprocess

from context_graph import PRIVATE_TEXT, digest, load_json, repo_path

ROOT = Path(__file__).resolve().parents[2]
BASE = 'docs/context/graphify'
PHASES = ('prepare', 'candidate', 'accept')
AUTHORITY = 'context-only; use task_context for execution conditions'
CODE_EXTENSIONS = {'.cs', '.py', '.js', '.mjs'}


def safe_relative(root, relative):
    target = repo_path(root, relative)
    parts = PurePosixPath(relative).parts
    private = {'.claude', '.agents', '.remember', '.memlog', 'node_modules', 'Builds', 'Build', '__pycache__'}
    if set(parts) & private or target.suffix.lower() in {'.log', '.ulf', '.pem', '.key'}:
        raise ValueError('Private/generated source is prohibited: ' + relative)
    return target


def stable_id(kind, value):
    return kind + ':' + hashlib.sha256(value.encode()).hexdigest()[:24]


def encoded(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':'), allow_nan=False)


def load_graph(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError('duplicate JSON key: ' + key)
            result[key] = value
        return result

    def nonfinite(value):
        raise ValueError('Invalid JSON number: ' + value)

    with Path(path).open('rb') as stream:
        raw = stream.read(16_000_001)
    if len(raw) > 16_000_000:
        raise ValueError('Graph exceeds 16 MB limit')
    return json.loads(raw, object_pairs_hook=unique, parse_constant=nonfinite)


def manifest_entries(manifest, root=ROOT):
    result = {}
    for entry in manifest['files']:
        for field in ('path', 'corpusPath'):
            safe_relative(root, entry[field])
        if not re.fullmatch('[0-9a-f]{40}', entry['ref']) or not re.fullmatch('[0-9a-f]{64}', entry['sha256']):
            raise ValueError('Invalid source ref/digest')
        if PurePosixPath(entry['path']).suffix not in CODE_EXTENSIONS:
            raise ValueError('Code allowlist extensions only')
        if entry['corpusPath'] in result:
            raise ValueError('duplicate corpus path')
        result[entry['corpusPath']] = entry
    return result


def assemble(raw, manifest, tasks, context, root=ROOT):
    entries = manifest_entries(manifest, root)
    nodes, edges, by_id, source_ids, symbols = [], [], {}, {}, {}
    task_path = 'docs/context/work-graph.json'

    def node(identity, label, kind, path=task_path, **extra):
        candidate = dict(id=identity, label=label, file_type='concept', source_file=path,
                         domainKind=kind, **extra)
        if identity in by_id:
            if by_id[identity] != candidate:
                raise ValueError('duplicate inconsistent node: ' + identity)
            return identity
        nodes.append(candidate)
        by_id[identity] = candidate
        return identity

    def edge(source, target, relation, path=task_path, **extra):
        edges.append(dict(source=source, target=target, relation=relation,
                          confidence='EXTRACTED', source_file=path, **extra))

    for entry in entries.values():
        sid = stable_id('source', entry['ref'] + ':' + entry['path'])
        source_ids[entry['corpusPath']] = sid
        node(sid, entry['path'], 'Source', entry['path'], sourceRef=entry['ref'],
             sha256=entry['sha256'], qualification='source-only; acceptance not established')
    raw_ids = {}
    for item in raw.get('nodes', []):
        source = item['source_file']
        if item['id'] in raw_ids:
            raise ValueError('duplicate AST id: ' + item['id'])
        if source == '':
            identity = stable_id('external', item['id'])
            raw_ids[item['id']] = identity
            node(identity, item['label'], 'ExternalSymbol', BASE + '/source-manifest.json',
                 qualification='unresolved external symbol; definition not extracted')
            continue
        if source not in entries:
            raise ValueError('AST source outside allowlist: ' + source)
        sid = source_ids[source]
        identity = stable_id('code', sid + ':' + item['id'])
        raw_ids[item['id']] = identity
        entry = entries[source]
        node(identity, item['label'], 'CodeSymbol', entry['path'],
             source_location=item.get('source_location', ''), sourceRef=entry['ref'])
        by_id[identity]['file_type'] = 'code'
        symbols.setdefault(sid, []).append(identity)
        edge(sid, identity, 'defines', entry['path'])
    raw_dangling = 0
    original_ids = set(raw_ids)
    for item in raw.get('edges', raw.get('links', [])):
        if item['source_file'] not in entries:
            raise ValueError('AST edge outside allowlist')
        if item['source'] not in original_ids or item['target'] not in original_ids:
            raw_dangling += 1
        for endpoint in (item['source'], item['target']):
            if endpoint not in raw_ids:
                identity = stable_id('unresolved', endpoint)
                raw_ids[endpoint] = identity
                node(identity, endpoint, 'UnresolvedReference', BASE + '/source-manifest.json',
                     qualification='raw AST endpoint without definition; not a source or acceptance proof')
        edge(raw_ids[item['source']], raw_ids[item['target']], item['relation'],
             entries[item['source_file']]['path'], origin='ast',
             source_location=item.get('source_location', ''),
             extractionConfidence=item['confidence'])
        edges[-1]['confidence'] = item['confidence']

    source_by_path = {}
    for entry in entries.values():
        source_by_path.setdefault(entry['path'], []).append(source_ids[entry['corpusPath']])
    items = tasks['items']
    for item in items + tasks.get('references', []):
        wid = 'issue.' + str(item['number'])
        node(wid, item['title'], 'WorkItem' if item in items else 'HistoricalReference',
             issue=item['number'], workId=item.get('workId'), workKind=item.get('kind'))
    requirement_ids = {r['id'] for r in tasks.get('requirements', [])}
    for item in tasks.get('requirements', []):
        node(item['id'], item['title'], 'Requirement', qualification=item.get('limits'),
             contract=item, mappingStatus='unbound; no declared work association')
        for number in item.get('issueNumbers', []):
            if type(number) is int and 'issue.' + str(number) in by_id:
                edge('issue.' + str(number), item['id'], 'addresses_requirement',
                     qualification='declared planning coverage; not implementation or acceptance')
    for item in items:
        for requirement in item.get('requirements', []):
            if requirement not in requirement_ids:
                raise ValueError('Unknown declared requirement: ' + str(requirement))
            edge('issue.' + str(item['number']), requirement, 'addresses_requirement',
                 qualification='declared planning coverage; not implementation or acceptance')

    def document(path):
        safe_relative(root, path)
        identity = stable_id('document', path)
        return node(identity, path, 'DocumentReference', targetPath=path,
                    qualification='declared path only; same path does not establish identical bytes, freshness or access')

    def artifact(identity, label=None):
        if identity not in by_id:
            node(identity, label or identity, 'Artifact', qualification='declared contract; receipt not assessed')
        return identity

    for item in items:
        issue = item['number']
        wid = 'issue.' + str(issue)
        for phase in PHASES:
            pid = f'phase:{issue}:{phase}'
            node(pid, f'#{issue} {phase}', 'WorkPhase', issue=issue, phase=phase,
                 packet=f'docs/context/work-orders/{issue:03}.json')
            edge(wid, pid, 'has_phase')
        for previous, following in zip(PHASES, PHASES[1:]):
            edge(f'phase:{issue}:{following}', f'phase:{issue}:{previous}', 'phase_requires')
        for output in item.get('outputs', []):
            aid = artifact(output['id'])
            phase = output.get('producerPhase', 'candidate')
            edge(f'phase:{issue}:{phase}', aid, 'produces', contract=output)
        for inp in item.get('inputs', []):
            aid = artifact(inp['id'])
            phase = inp.get('consumerPhase', 'candidate')
            edge(f'phase:{issue}:{phase}', aid, 'requires_artifact', contract=inp)
        for index, group in enumerate(item.get('oneOfInputs', [])):
            gid = f'choice:{issue}:{index}'
            node(gid, group.get('id', gid), 'InputChoice', contract=group)
            phase = group.get('consumerPhase', 'candidate')
            edge(f'phase:{issue}:{phase}', gid, 'one_of', qualification='selected branch only; not AND')
        for field, kind in (('inputQualifiers', 'Constraint'), ('guardPredicates', 'Constraint'),
                            ('phaseWriteScopes', 'WriteContract'), ('artifactOutputBindings', 'ArtifactBinding'),
                            ('checks', 'VerificationContract'), ('resourceBindings', 'CapabilityRequirement')):
            if item.get(field):
                identity = f'contract:{issue}:{field}'
                node(identity, f'#{issue} {field}', kind, selector=f'/items/{issue}/{field}',
                     lookup='item.number; not array index')
                edge(wid, identity, 'contract_reference')
        for ref in item.get('context', []) + item.get('codePointers', []):
            if isinstance(ref, str):
                ref = {'path': ref, 'readWhen': 'candidate'}
            path = ref.get('path')
            if not path:
                continue
            safe_relative(root, path)
            rid = stable_id('pointer', str(issue) + encoded(ref))
            phase = ref.get('readWhen', 'candidate')
            node(rid, path + '#' + ref.get('selector', ref.get('symbol', '')), 'ContextPointer',
                 targetPath=path, contract=ref, readWhen=phase)
            phase_list = PHASES if phase == 'all' else phase if isinstance(phase, list) else [phase]
            for p in phase_list:
                if p not in PHASES:
                    raise ValueError('Unknown readWhen: ' + str(p))
                edge(f'phase:{issue}:{p}', rid, 'reads')
            edge(rid, document(path), 'points_to_document',
                 qualification='path-level discovery only; verify exact ref/digest before reading')
            for sid in source_by_path.get(path, []):
                if any(ref.get(key) is not None and by_id[sid]['sourceRef'] != ref[key]
                       for key in ('sourceRef', 'ref')):
                    continue
                edge(rid, sid, 'source_candidate', qualification='verify ref/digest against task packet')
    for item in tasks.get('edges', []):
        if item['from'] not in by_id or item['to'] not in by_id:
            raise ValueError('PM edge endpoint missing')
        edge(item['from'], item['to'], item['relation'], contract=item,
             authority='PM declaration; not an execution receipt')

    context_path = 'docs/context/project-context.json'
    for item in context.get('nodes', []):
        identity = 'context:' + item['id']
        kind = {'decision': 'Decision', 'policy': 'Constraint', 'evidence': 'Evidence'}.get(item['kind'], 'ContextRecord')
        node(identity, item['title'], kind, context_path, historicalStatus=item['status'],
             selector='/nodes[id=' + item['id'] + ']', qualification='freshness and acceptance not assessed')
    for item in context.get('edges', []):
        edge('context:' + item['from'], 'context:' + item['to'], item['relation'], context_path)
    for item in context.get('nodes', []):
        for ref in item.get('sources', []):
            path = ref.get('repoPath')
            if not path:
                continue
            edge('context:' + item['id'], document(path), 'attributed_to_document', context_path,
                 contract=ref, qualification='historical attribution; path match is not digest equality or current authority')
            for sid in source_by_path.get(path, []):
                edge('context:' + item['id'], sid, 'source_candidate', context_path,
                     contract=ref, qualification='historical attribution; compare stored digest before reuse')
    bound_requirements = {e['source'] for e in edges if e['source'] in requirement_ids}
    bound_requirements.update(e['target'] for e in edges if e['target'] in requirement_ids)
    for identity in bound_requirements:
        by_id[identity]['mappingStatus'] = 'declared association; acceptance not assessed'

    # 같은 노드 쌍의 다중 의미와 계약 차이를 유지한다.
    edges = [json.loads(text) for text in sorted({encoded(e) for e in edges})]
    graph = dict(nodes=sorted(nodes, key=lambda n: n['id']), edges=edges, hyperedges=[],
                 authority=AUTHORITY, schemaVersion=1,
                 provenance={'tool': 'graphifyy', 'version': '0.9.61', 'mode': 'code-only; no-cluster',
                             'manifestSha256': hashlib.sha256(encoded(manifest).encode()).hexdigest(),
                             'workGraphSha256': hashlib.sha256(encoded(tasks).encode()).hexdigest(),
                             'projectContextSha256': hashlib.sha256(encoded(context).encode()).hexdigest()},
                 coverage={'allowlistedFiles': len(entries), 'extractedFiles': len(symbols),
                           'filesWithoutSymbols': sorted(e['path'] for p, e in entries.items() if source_ids[p] not in symbols),
                           'workItems': len(items), 'astNodes': len(original_ids), 'rawDanglingEdges': raw_dangling,
                           'unboundRequirements': sorted(requirement_ids - bound_requirements),
                           'nodeKinds': dict(sorted(Counter(n['domainKind'] for n in nodes).items()))})
    check_graph(graph)
    return graph


def check_graph(graph):
    ids = set()
    for node in graph['nodes']:
        if node['id'] in ids:
            raise ValueError('duplicate node id')
        ids.add(node['id'])
        if not all(k in node for k in ('id', 'label', 'file_type', 'source_file', 'domainKind')):
            raise ValueError('Incomplete node')
    for edge in graph['edges']:
        if edge['source'] not in ids or edge['target'] not in ids:
            raise ValueError('Missing edge endpoint')
        if not all(k in edge for k in ('relation', 'confidence', 'source_file')):
            raise ValueError('Incomplete edge')
    if PRIVATE_TEXT.search(encoded(graph)):
        raise ValueError('Private absolute path or credential in graph')
    return {'nodes': len(ids), 'edges': len(graph['edges'])}


def scoped_context(graph, issue, phase, max_chars=16000, symbol=None):
    if phase not in PHASES or max_chars < 1:
        raise ValueError('Invalid phase/budget')
    pid = f'phase:{issue}:{phase}'
    by_id = {n['id']: n for n in graph['nodes']}
    if pid not in by_id:
        raise ValueError('Unknown issue')
    wid = 'issue.' + str(issue)
    selected = {pid, wid}
    # 위상 노드의 직접 문맥만 확장한다. PM 선행작업의 문맥까지 재귀로 읽지 않는다.
    direct = [e for e in graph['edges'] if e['source'] == pid]
    selected.update(e['target'] for e in direct)
    pointers = {e['target'] for e in direct if e['relation'] == 'reads'}
    for e in graph['edges']:
        if e['source'] in pointers and e['relation'] in {'source_candidate', 'points_to_document'}:
            selected.add(e['target'])
        if e['source'] == wid and by_id[e['target']]['domainKind'] == 'Requirement':
            selected.add(e['target'])
        if e['target'] == wid and by_id[e['source']]['domainKind'] == 'Requirement':
            selected.add(e['source'])
    documents = {i for i in selected if by_id[i]['domainKind'] == 'DocumentReference'}
    for e in graph['edges']:
        if e['target'] in documents and e['relation'] == 'attributed_to_document':
            selected.add(e['source'])
    source_ids = {i for i in selected if by_id[i]['domainKind'] == 'Source'}
    available_symbols = {e['target'] for e in graph['edges'] if e['source'] in source_ids and e['relation'] == 'defines'}
    if symbol:
        matches = {i for i in available_symbols if symbol.casefold() in by_id[i]['label'].casefold()}
        if not matches:
            raise ValueError('Symbol not found in this phase context')
        selected.update(matches)
    else:
        selected.update(available_symbols)
    selected_symbols = selected & available_symbols
    unresolved = set()
    for e in graph['edges']:
        if e.get('origin') != 'ast':
            continue
        for at, neighbor in ((e['source'], e['target']), (e['target'], e['source'])):
            if at in selected_symbols and by_id[neighbor]['domainKind'] in {'ExternalSymbol', 'UnresolvedReference'}:
                unresolved.add(neighbor)
    selected.update(unresolved)
    result = dict(issue=issue, phase=phase, authority=AUTHORITY, complete=True,
                  symbolFilter=symbol, scope='explicit symbol selection' if symbol else 'phase source symbols',
                  unresolvedNeighbors=len(unresolved),
                  scopeLimits='Direct document attributions and unresolved AST neighbors only; no recursive call graph or predecessor context. Path attribution is not source qualification.',
                  nodes=[n for n in graph['nodes'] if n['id'] in selected],
                  edges=[e for e in graph['edges'] if e['source'] in selected and e['target'] in selected])
    required = len(encoded(result))
    if required > max_chars:
        return dict(issue=issue, phase=phase, authority=AUTHORITY, complete=False,
                    requiredChars=required, nodes=[], edges=[],
                    next=f'query --issue {issue} --phase {phase} --max-chars {required + 256}' +
                         (f' --symbol {shlex.quote(symbol)}' if symbol else ''),
                    taskContext=f'node scripts/context/task_context.mjs brief --issue {issue} --phase {phase}')
    result['requiredChars'] = required
    return result


def materialize(root, manifest, destination):
    entries = manifest_entries(manifest, root)
    destination = Path(destination)
    if destination.exists():
        raise ValueError('Use a fresh corpus directory')
    payloads = []
    for entry in entries.values():
        safe_relative(root, entry['path'])
        spec = entry['ref'] + ':' + entry['path']
        mode = subprocess.check_output(['git', '-C', str(root), 'ls-tree', entry['ref'], '--', entry['path']], text=True)
        if not mode.startswith('100644 blob ') and not mode.startswith('100755 blob '):
            raise ValueError('Only regular tracked Git blobs are allowed: ' + entry['path'])
        payload = subprocess.check_output(['git', '-C', str(root), 'show', spec])
        if len(payload) > 2_000_000 or hashlib.sha256(payload).hexdigest() != entry['sha256']:
            raise ValueError('Source size/digest mismatch: ' + entry['path'])
        payloads.append((entry, payload))
    destination.mkdir(parents=True)
    for entry, payload in payloads:
        target = safe_relative(destination, entry['corpusPath'])
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(payload)
    return {'materialized': len(payloads)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    sub = parser.add_subparsers(dest='command', required=True)
    corpus = sub.add_parser('corpus')
    corpus.add_argument('--destination', type=Path, required=True)
    extract = sub.add_parser('extract')
    extract.add_argument('--corpus', type=Path, required=True)
    extract.add_argument('--output', type=Path, required=True)
    build = sub.add_parser('build')
    build.add_argument('--raw', type=Path, required=True)
    query = sub.add_parser('query')
    query.add_argument('--issue', type=int, required=True)
    query.add_argument('--phase', choices=PHASES, default='prepare')
    query.add_argument('--max-chars', type=int, default=16000)
    query.add_argument('--symbol', help='현재 phase의 소스에서 명시적으로 선택할 심벌 이름')
    sub.add_parser('validate')
    args = parser.parse_args()
    root = args.root.resolve()
    if args.command == 'corpus':
        result = materialize(root, load_json(root / BASE / 'source-manifest.json'), args.destination)
    elif args.command == 'extract':
        if args.output.exists():
            raise ValueError('Use a fresh extraction output')
        from importlib.metadata import version
        if version('graphifyy') != '0.9.61':
            raise ValueError('Pinned Graphify version required')
        manifest = load_json(root / BASE / 'source-manifest.json')
        entries = manifest_entries(manifest, root)
        actual = {p.relative_to(args.corpus).as_posix() for p in args.corpus.rglob('*') if p.is_file()}
        if actual != set(entries):
            raise ValueError('Corpus differs from allowlist')
        for path, entry in entries.items():
            if digest(safe_relative(args.corpus, path)) != entry['sha256']:
                raise ValueError('Corpus digest mismatch')
        env = dict(os.environ, GRAPHIFY_QUERY_LOG_DISABLE='1')
        subprocess.run(['graphify', 'extract', str(args.corpus), '--code-only', '--no-cluster',
                        '--max-workers', '2', '--output', str(args.output)], env=env, check=True)
        return
    elif args.command == 'build':
        graph = assemble(load_graph(args.raw), load_json(root / BASE / 'source-manifest.json'),
                         load_json(root / 'docs/context/work-graph.json'),
                         load_json(root / 'docs/context/project-context.json'), root)
        from graphify.validate import assert_valid
        assert_valid(graph)
        (root / BASE / 'graph.json').write_text(json.dumps(graph, ensure_ascii=False, indent=2) + '\n')
        result = dict(check_graph(graph), **graph['coverage'])
        (root / BASE / 'coverage.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    else:
        graph = load_graph(root / BASE / 'graph.json')
        if args.command == 'query':
            result = scoped_context(graph, args.issue, args.phase, args.max_chars, args.symbol)
        else:
            from graphify.validate import assert_valid
            assert_valid(graph)
            result = check_graph(graph)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
