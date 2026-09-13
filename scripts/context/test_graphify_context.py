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

    def test_document_attribution_and_declared_requirements_remain_scoped(self):
        raw, manifest, tasks, context = self.fixture()
        tasks['items'][0]['context'] = [{'path': 'docs/decision.md', 'readWhen': 'candidate', 'selector': 'Boundary'}]
        tasks['items'][0]['requirements'] = ['requirement.boundary']
        tasks['requirements'] = [{'id': 'requirement.boundary', 'title': 'Boundary', 'issueNumbers': [150]},
                                 {'id': 'requirement.unbound', 'title': 'Unmapped', 'issueNumbers': [None]}]
        context['nodes'] = [{'id': 'decision.boundary', 'title': 'Historical boundary', 'kind': 'decision',
                             'status': 'superseded', 'sources': [{'repoPath': 'docs/decision.md', 'sha256': 'c' * 64}]}]
        graph = assemble(raw, manifest, tasks, context)
        view = scoped_context(graph, 150, 'candidate', 24000)
        self.assertTrue(view['complete'])
        self.assertTrue(any(n['domainKind'] == 'DocumentReference' for n in view['nodes']))
        decision = next(n for n in view['nodes'] if n['domainKind'] == 'Decision')
        self.assertEqual(decision['historicalStatus'], 'superseded')
        self.assertTrue(any(n['id'] == 'requirement.boundary' for n in view['nodes']))
        self.assertEqual(graph['coverage']['unboundRequirements'], ['requirement.unbound'])
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
