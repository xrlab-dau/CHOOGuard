import copy
from pathlib import Path
import tempfile
import unittest

from graphify_context import assemble, check_graph, safe_relative, scoped_context


class GraphifyContextTests(unittest.TestCase):
    def fixture(self):
        manifest = {'schemaVersion': 1, 'files': [
            {'path': 'src/Voice.cs', 'ref': 'a' * 40, 'sha256': 'b' * 64,
             'corpusPath': 'a/src/Voice.cs'}]}
        raw = {'nodes': [{'id': 'voice', 'label': 'Voice', 'file_type': 'code',
                          'source_file': 'a/src/Voice.cs', 'source_location': 'L2'}], 'edges': []}
        task = {'number': 150, 'title': 'Voice boundary', 'workId': 'FMP-12a-R2', 'kind': 'work_item',
                'outcome': '검증된 경계를 인계한다.', 'context': [{'path': 'src/Voice.cs', 'selector': 'Voice',
                'readWhen': 'candidate', 'proves': '경계', 'limits': '실서비스 수용 아님'}],
                'codePointers': [], 'outputs': [{'id': 'artifact:150:result:candidate',
                'producerPhase': 'candidate', 'path': 'docs/result.json', 'qualification': '검토 후보'}],
                'inputs': [], 'oneOfInputs': [], 'phaseWriteScopes': {}, 'checks': []}
        tasks = {'items': [task], 'references': [], 'requirements': [], 'edges': [], 'snapshot': {}}
        context = {'nodes': [], 'edges': []}
        return raw, manifest, tasks, context

    def test_ast_and_work_phase_connect_without_acceptance(self):
        graph = assemble(*self.fixture())
        check_graph(graph)
        self.assertTrue(any(n['domainKind'] == 'WorkPhase' for n in graph['nodes']))
        source = next(n for n in graph['nodes'] if n['domainKind'] == 'Source')
        self.assertEqual(source['sourceRef'], 'a' * 40)
        self.assertEqual(source['qualification'], 'source-only; acceptance not established')
        view = scoped_context(graph, 150, 'candidate', 12000)
        self.assertTrue(view['complete'])
        self.assertTrue(any(n['domainKind'] == 'CodeSymbol' for n in view['nodes']))
        self.assertEqual(view['authority'], 'context-only; use task_context for execution conditions')

    def test_unknown_ast_source_fails_closed(self):
        raw, manifest, tasks, context = self.fixture()
        raw['nodes'][0]['source_file'] = 'secret.cs'
        with self.assertRaisesRegex(ValueError, 'allowlist'):
            assemble(raw, manifest, tasks, context)

    def test_duplicate_and_dangling_graph_rejected(self):
        graph = assemble(*self.fixture())
        graph['nodes'].append(copy.deepcopy(graph['nodes'][0]))
        with self.assertRaisesRegex(ValueError, 'duplicate'):
            check_graph(graph)
        graph = assemble(*self.fixture())
        graph['edges'].append({'source': 'missing', 'target': graph['nodes'][0]['id'],
                              'relation': 'x', 'confidence': 'EXTRACTED', 'source_file': 'x.md'})
        with self.assertRaisesRegex(ValueError, 'endpoint'):
            check_graph(graph)

    def test_budget_never_claims_truncated_context_complete(self):
        graph = assemble(*self.fixture())
        result = scoped_context(graph, 150, 'candidate', 100)
        self.assertFalse(result['complete'])
        self.assertGreater(result['requiredChars'], 100)
        self.assertIn('next', result)

    def test_phase_context_is_not_injected_into_prepare(self):
        graph = assemble(*self.fixture())
        result = scoped_context(graph, 150, 'prepare', 12000)
        self.assertFalse(any(n['domainKind'] == 'CodeSymbol' for n in result['nodes']))

    def test_two_relations_between_same_nodes_survive(self):
        raw, manifest, tasks, context = self.fixture()
        raw['nodes'].append(dict(raw['nodes'][0], id='caller', label='Caller'))
        raw['edges'] = [dict(source='caller', target='voice', relation=r,
                             confidence='EXTRACTED', source_file='a/src/Voice.cs')
                        for r in ('calls', 'references')]
        graph = assemble(raw, manifest, tasks, context)
        self.assertEqual(len([e for e in graph['edges'] if e['relation'] in {'calls', 'references'}]), 2)

    def test_source_less_external_symbol_is_not_a_verified_source(self):
        raw, manifest, tasks, context = self.fixture()
        raw['nodes'].append({'id': 'external', 'label': 'MonoBehaviour', 'file_type': 'code', 'source_file': ''})
        raw['edges'] = [{'source': 'voice', 'target': 'external', 'relation': 'inherits',
                         'confidence': 'EXTRACTED', 'source_file': 'a/src/Voice.cs'}]
        graph = assemble(raw, manifest, tasks, context)
        external = next(n for n in graph['nodes'] if n['domainKind'] == 'ExternalSymbol')
        self.assertNotIn('sourceRef', external)
        self.assertEqual(external['qualification'], 'unresolved external symbol; definition not extracted')
        check_graph(graph)

    def test_missing_raw_endpoint_is_explicit_unresolved_reference(self):
        raw, manifest, tasks, context = self.fixture()
        raw['edges'] = [{'source': 'voice', 'target': 'ref_missing', 'relation': 'imports_from',
                         'confidence': 'EXTRACTED', 'source_file': 'a/src/Voice.cs'}]
        graph = assemble(raw, manifest, tasks, context)
        self.assertTrue(any(n['domainKind'] == 'UnresolvedReference' for n in graph['nodes']))
        self.assertEqual(graph['coverage']['rawDanglingEdges'], 1)
        check_graph(graph)

    def test_pm_and_historical_source_paths_are_validated(self):
        for path in ('.claude/settings.json', '.agents/config.json', '.memlog/state.json', '.env'):
            for field in ('context', 'codePointers', 'historical'):
                with self.subTest(path=path, field=field):
                    raw, manifest, tasks, context = self.fixture()
                    if field == 'historical':
                        context['nodes'] = [{'id': 'private', 'kind': 'evidence', 'title': 'Private',
                                             'status': 'historical', 'sources': [{'repoPath': path}]}]
                    else:
                        tasks['items'][0][field] = [path] if field == 'codePointers' else [{'path': path}]
                    with self.assertRaises(ValueError):
                        assemble(raw, manifest, tasks, context)

    def requirement(self, code='F-TEST', issues=(150,)):
        return dict(id='requirement.' + code, code=code, kind='requirement', title='Bounded requirement',
                    definition='A bounded requirement.', issueNumbers=list(issues),
                    mappingStatus='mapped' if issues else 'unresolved', limits='Not acceptance',
                    legacySource=dict(pointer='reviews/foundation-map.json#/issues/*/requirements',
                                      availability='unavailable', generatorRecovered=False),
                    evidence=[dict(issue=n, kind='definition_trace_outcome', reason='Outcome agrees',
                                   limits='Planning only', sources=[dict(path='docs/requirements.md', selector='L9',
                                   quote='A bounded requirement.', availability='local_snapshot', ref=None,
                                   digest=dict(algorithm='sha256', value='b' * 64), access='Embedded quote only',
                                   qualification='Definition evidence only', limits='Not acceptance')]) for n in issues])

    def mapped_fixture(self):
        raw, manifest, tasks, context = self.fixture()
        tasks['requirements'] = [self.requirement()]
        tasks['items'][0]['requirements'] = ['requirement.F-TEST']
        tasks['edges'] = [dict(relation='implements', **{'from': 'issue.150', 'to': 'requirement.F-TEST'},
                              reason='Outcome agrees', mappingStatus='mapped', policy=None)]
        return raw, manifest, tasks, context

    def test_explicit_mapping_is_one_edge_and_nonmapping_context_never_counts_as_coverage(self):
        raw, manifest, tasks, context = self.mapped_fixture()
        tasks['requirements'].append(self.requirement('F-UNBOUND', ()))
        tasks['edges'].append(dict(relation='context', **{'from': 'issue.150', 'to': 'requirement.F-UNBOUND'}, reason='Discovery only'))
        graph = assemble(raw, manifest, tasks, context)
        self.assertEqual(len([e for e in graph['edges'] if e['relation'] == 'implements']), 1)
        self.assertFalse(any(e['relation'] == 'addresses_requirement' for e in graph['edges']))
        self.assertEqual(graph['coverage']['requirementMappings'], 1)
        self.assertEqual(graph['coverage']['unboundRequirements'], ['requirement.F-UNBOUND'])
        self.assertFalse(any(n['id'] == 'requirement.F-UNBOUND' for n in scoped_context(graph, 150, 'prepare', 32000)['nodes']))

    def test_shared_requirement_query_scopes_evidence_and_keeps_hold(self):
        raw, manifest, tasks, context = self.mapped_fixture()
        second = copy.deepcopy(tasks['items'][0]); second.update(number=151, outputs=[])
        tasks['items'].append(second); tasks['requirements'] = [self.requirement(issues=(150, 151))]
        tasks['requirements'][0]['mappingStatus'] = 'historical_on_hold'
        for n in (150, 151):
            edge = dict(tasks['edges'][0], **{'from': 'issue.' + str(n)}, mappingStatus='historical_on_hold',
                        policy=dict(path='docs/context/work-orders/policy.json', selector='/goalOverrides/' + str(n)))
            if n == 150:
                tasks['edges'][0] = edge
            else:
                tasks['edges'].append(edge)
        graph = assemble(raw, manifest, tasks, context)
        self.assertEqual(graph['coverage']['heldRequirementMappings'], 2)
        self.assertEqual(graph['coverage']['heldRequirements'], ['requirement.F-TEST'])
        before = copy.deepcopy(graph)
        for n in (150, 151):
            view = scoped_context(graph, n, 'prepare', 32000)
            r = next(n for n in view['nodes'] if n['domainKind'] == 'Requirement')
            self.assertEqual(r['contract']['issueNumbers'], [n])
            self.assertEqual([e['issue'] for e in r['contract']['evidence']], [n])
            self.assertEqual(r['mappingStatus'], 'historical_on_hold')
            self.assertNotIn('issue.' + str(301 - n), [node['id'] for node in view['nodes']])
        self.assertEqual(graph, before)

    def test_requirement_invalid_mapping_cases_match_node_validator(self):
        import subprocess
        from graphify_context import ROOT, requirement_projection
        mutations = {
            'null': lambda t: t['requirements'][0].update(issueNumbers=[None]),
            'duplicate': lambda t: t['requirements'][0].update(issueNumbers=[150, 150]),
            'id': lambda t: t['requirements'][0].update(id='issue.150'),
            'code': lambda t: t['requirements'][0].update(code='F-OTHER'),
            'array-code': lambda t: t['requirements'][0].update(code=['F-TEST']),
            'newline-code': lambda t: (t['requirements'][0].update(code='F-TEST\n', id='requirement.F-TEST\n'), t['items'][0].update(requirements=['requirement.F-TEST\n']), t['edges'][0].update(to='requirement.F-TEST\n')),
            'newline-endpoint': lambda t: t['edges'][0].update(**{'from': 'issue.150\n'}),
            'unicode-endpoint': lambda t: t['edges'][0].update(**{'from': 'issue.1５0'}),
            'newline-digest': lambda t: t['requirements'][0]['evidence'][0]['sources'][0]['digest'].update(value='b' * 64 + '\n'),
            'newline-ref': lambda t: t['requirements'][0]['evidence'][0]['sources'][0].update(availability='qualified_ref', ref='a' * 40 + '\n'),
            'definition': lambda t: t['requirements'][0].update(definition=''),
            'evidence': lambda t: t['requirements'][0].update(evidence=[]),
            'foreign': lambda t: t['requirements'][0]['evidence'][0].update(issue=151),
            'missing-index': lambda t: t['items'][0].update(requirements=[]),
            'extra-index': lambda t: t['items'][0].update(requirements=['requirement.F-OTHER']),
            'duplicate-index': lambda t: t['items'][0].update(requirements=['requirement.F-TEST'] * 2),
            'missing-edge': lambda t: t.update(edges=[]),
            'reverse': lambda t: t['edges'][0].update(**{'from': 'requirement.F-TEST', 'to': 'issue.150'}),
            'extra-field': lambda t: t['edges'][0].update(consumerPhase='candidate'),
            'local-ref': lambda t: t['requirements'][0]['evidence'][0]['sources'][0].update(ref='a' * 40),
            'unpinned': lambda t: t['requirements'][0]['evidence'][0]['sources'][0].update(availability='qualified_ref', ref='develop'),
            'private': lambda t: t['requirements'][0]['evidence'][0]['sources'][0].update(path='.claude/settings.json'),
            'escape': lambda t: t['requirements'][0]['evidence'][0]['sources'][0].update(path='../doc.md'),
            'empty-segment': lambda t: t['requirements'][0]['evidence'][0]['sources'][0].update(path='docs//file.md'),
            'encoded': lambda t: t['requirements'][0]['evidence'][0]['sources'][0].update(path='docs/file%20.md'),
            'fragment': lambda t: t['requirements'][0]['evidence'][0]['sources'][0].update(path='docs/file.md#part'),
            'decomposed': lambda t: t['requirements'][0]['evidence'][0]['sources'][0].update(path='docs/é.md'),
            'duplicate-edge': lambda t: t['edges'].append(copy.deepcopy(t['edges'][0])),
            'missing-quote': lambda t: t['requirements'][0]['evidence'][0]['sources'][0].pop('quote'),
            'missing-hold-policy': lambda t: (t['requirements'][0].update(mappingStatus='historical_on_hold'), t['edges'][0].update(mappingStatus='historical_on_hold')),
            'digest': lambda t: t['requirements'][0]['evidence'][0]['sources'][0].update(digest={'algorithm': 'sha256', 'value': 'bad'}),
            'hold': lambda t: t['edges'][0].update(mappingStatus='historical_on_hold'),
        }
        candidates = []
        for name, mutate in mutations.items():
            raw, manifest, tasks, context = self.mapped_fixture(); mutate(tasks); candidates.append(tasks)
            with self.subTest(name=name + '-projection'), self.assertRaises(ValueError):
                requirement_projection(tasks)
            with self.subTest(name=name), self.assertRaises(ValueError):
                assemble(raw, manifest, tasks, context)
        candidates.insert(0, self.mapped_fixture()[2])
        from graphify_context import encoded
        module = (ROOT / 'scripts/context/work_graph.mjs').as_uri()
        script = f"import {{requirementProjection}} from '{module}';import fs from 'node:fs';console.log(JSON.stringify(JSON.parse(fs.readFileSync(0,'utf8')).map(g=>{{try{{requirementProjection(g);return true}}catch{{return false}}}})));"
        result = subprocess.run(['node', '--input-type=module', '-e', script], input=encoded(candidates), text=True, capture_output=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        import json
        self.assertEqual(json.loads(result.stdout), [True] + [False] * len(mutations))

    def test_requirement_evidence_symlink_escape_is_rejected_without_reading_bytes(self):
        raw, manifest, tasks, context = self.mapped_fixture()
        with tempfile.TemporaryDirectory() as directory, tempfile.TemporaryDirectory() as outside:
            root = Path(directory)
            (root / 'escape').symlink_to(outside, target_is_directory=True)
            tasks['requirements'][0]['evidence'][0]['sources'][0]['path'] = 'escape/doc.md'
            with self.assertRaises(ValueError):
                assemble(raw, manifest, tasks, context, root)

    def test_approved_registry_has_fifteen_requirements_sixteen_mappings_two_holds(self):
        from graphify_context import ROOT, load_graph
        raw, manifest, _, context = self.fixture()
        graph = assemble(raw, manifest, load_graph(ROOT / 'docs/context/work-graph.json'), context)
        self.assertEqual(graph['coverage']['requirementMappings'], 16)
        self.assertEqual(graph['coverage']['heldRequirementMappings'], 2)
        self.assertEqual(graph['coverage']['unboundRequirements'], [])
        self.assertEqual(len(graph['coverage']['heldRequirements']), 2)

    def test_document_attribution_and_declared_requirements_remain_scoped(self):
        raw, manifest, tasks, context = self.fixture()
        tasks['items'][0]['context'] = [{'path': 'docs/decision.md', 'readWhen': 'candidate', 'selector': 'Boundary'}]
        tasks['items'][0]['requirements'] = ['requirement.F-BOUNDARY']
        tasks['requirements'] = [self.requirement('F-BOUNDARY'), self.requirement('F-UNBOUND', ())]
        tasks['edges'] = [dict(relation='implements', **{'from': 'issue.150', 'to': 'requirement.F-BOUNDARY'},
                              reason='Outcome agrees', mappingStatus='mapped', policy=None)]
        context['nodes'] = [{'id': 'decision.boundary', 'title': 'Historical boundary', 'kind': 'decision',
                             'status': 'superseded', 'sources': [{'repoPath': 'docs/decision.md', 'sha256': 'c' * 64}]}]
        graph = assemble(raw, manifest, tasks, context)
        view = scoped_context(graph, 150, 'candidate', 24000)
        self.assertTrue(view['complete'])
        self.assertTrue(any(n['domainKind'] == 'DocumentReference' for n in view['nodes']))
        decision = next(n for n in view['nodes'] if n['domainKind'] == 'Decision')
        self.assertEqual(decision['historicalStatus'], 'superseded')
        self.assertTrue(any(n['id'] == 'requirement.F-BOUNDARY' for n in view['nodes']))
        self.assertEqual(graph['coverage']['unboundRequirements'], ['requirement.F-UNBOUND'])
        prepare = scoped_context(graph, 150, 'prepare', 24000)
        self.assertFalse(any(n['domainKind'] == 'Decision' for n in prepare['nodes']))

    def test_scoped_symbols_retain_adjacent_unresolved_endpoints(self):
        raw, manifest, tasks, context = self.fixture()
        raw['nodes'].append({'id': 'external', 'label': 'MonoBehaviour', 'file_type': 'code', 'source_file': ''})
        raw['edges'] = [dict(source='voice', target=target, relation='references', confidence='EXTRACTED',
                             source_file='a/src/Voice.cs') for target in ('external', 'missing')]
        graph = assemble(raw, manifest, tasks, context)
        for symbol in (None, 'Voice'):
            view = scoped_context(graph, 150, 'candidate', 24000, symbol)
            self.assertTrue(view['complete'])
            kinds = {n['domainKind'] for n in view['nodes']}
            self.assertTrue({'ExternalSymbol', 'UnresolvedReference'} <= kinds)
            self.assertEqual(view['unresolvedNeighbors'], 2)

    def test_explicit_refs_must_both_match_ast_source(self):
        raw, manifest, tasks, context = self.fixture()
        tasks['items'][0]['context'][0].update(sourceRef='a' * 40, ref='c' * 40)
        graph = assemble(raw, manifest, tasks, context)
        self.assertFalse(any(e['relation'] == 'source_candidate' for e in graph['edges']))

    def test_symbol_budget_continuation_keeps_selection(self):
        graph = assemble(*self.fixture())
        view = scoped_context(graph, 150, 'candidate', 100, 'Voice')
        self.assertFalse(view['complete'])
        self.assertIn('--symbol Voice', view['next'])

    def test_paths_block_private_and_symlink_escape(self):
        with tempfile.TemporaryDirectory() as directory, tempfile.TemporaryDirectory() as outside:
            root = Path(directory)
            for unsafe in ('../x', '/home/x', '.env', '.claude/settings.json', 'Library/x', 'docs/evidence/raw.log'):
                with self.assertRaises(ValueError):
                    safe_relative(root, unsafe)
            (root / 'escape').symlink_to(outside, target_is_directory=True)
            with self.assertRaises(ValueError):
                safe_relative(root, 'escape/x.cs')


if __name__ == '__main__':
    unittest.main()
