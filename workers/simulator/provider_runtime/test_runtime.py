"""Synthetic local acceptance/lifecycle tests. No end-user credentials or real provider calls."""
import io
import json
import os
from pathlib import Path
import socket
import sys
import tempfile
import unittest
from unittest import mock
from dataclasses import replace
import urllib.parse

import acceptance as a
import graph_codec as g
import local_client as c
import session

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'provider_auth'))
import test_sidecar as fixture
from multi_provider import MultiProvider
from provider_registry import OPENAI, Registry


def request_fixture():
    return {'contract': 'initial-causal-conditions-v1', 'engine': 'synthetic-engine', 'seed': '42', 'basis_digest': 'a' * 64,
            'graph': {'entity_columns': a.ENTITY_COLUMNS, 'entities': [['grid', None, 'cabinet', [0, 0, 0], 0, 0, [], 0, 0, True, None],
                     ['glyph', 'grid', 'mesh', [0, 0, 0], 1, 0, [], 0, 0, False, 'grid']],
                      'tag_sets': [['semantic', 'electrical'], ['decoration']], 'entity_count': 2, 'kernel_semantic_count': 1,
                      'visuals': [['mesh1', 'grid', 'unverified']], 'visual_count': 1},
            'property_catalog': [{'property': key, 'target': target, 'min': minimum, 'max': maximum}
                                 for key, (target, minimum, maximum) in a.RULES.items()]}


def compact_fixture():
    request=request_fixture()
    request['graph']={'encoding':'prefix-column-v1','numeric_scale':10000,
        'group_columns':g.GROUP_COLUMNS,
        'identities':[[0,'@world'],[0,'glyph'],[1,'rid-a'],[5,'b']],
        'entity_groups':[
            {'common':[None,'cabinet',0,0,0,0,True,None],'entity_refs':[2,3],
             'xyz_columns':[[10000,20000],0,[0,-10000]],'state_value_columns':[[1000,1500]]},
            {'common':[2,'mesh',1,1,1,1,False,2],'entity_refs':[1],
             'xyz_columns':[0,0,0],'state_value_columns':[]}],
        'entity_count':3,'kernel_semantic_count':2,'visual_count':1,'visuals':[[1,2,'unverified']],
        'tag_sets':[['semantic','electrical'],['decoration']],'state_schemas':[['wetness'],[]],
        'link_sets':[[3],[]],'conditional_operation_sets':[['isolate_power'],[]]}
    return request


class RuntimeTests(unittest.TestCase):
    def setUp(self):
        self.fixture = fixture.HttpTests('test_http_page_plain_status_csp_and_valid_script')
        self.fixture.setUp()
        self.beta_transport = fixture.MockTransport()
        self.fixture.p = MultiProvider(self.fixture.store,
            transports={'openai-chatgpt': self.fixture.transport, 'mock-beta': self.beta_transport},
            registry=Registry([OPENAI, replace(OPENAI, id='mock-beta', label='Mock Beta')]))
        self.fixture.p.configure(self.fixture.server.server_port)
        self.fixture.server.provider = self.fixture.p
        self.temp = tempfile.TemporaryDirectory(prefix='chooguard-runtime-synthetic-')
        self.directory = Path(self.temp.name)
        self.request = request_fixture()
        self.request_path = self.directory / 'request.json'
        self.request_path.write_bytes(c.canonical(self.request))
        self.report = self.directory / 'report.json'
        self.client = c.LocalClient.load(self.fixture.store.directory)

    def tearDown(self):
        self.fixture.tearDown()
        self.temp.cleanup()

    def enable(self):
        self.fixture.enable()
        self.fixture.p.session({'initial_world_allowance': {'input_tokens': 192000, 'output_tokens': 512}})

    def complete(self, proposal=None):
        proposal = proposal or {'basis_digest': self.request['basis_digest'], 'seed': '42',
                                'changes': [{'entity_id': 'grid', 'property': 'wetness', 'value': .8}]}
        self.fixture.transport.stream = fixture.events([{'type': 'response.completed', 'response': {
            'id': 'synthetic-runtime-completion', 'status': 'completed', 'model': 'second-2026-10-14',
            'output': [{'type': 'message', 'content': [{'type': 'output_text', 'text': json.dumps(proposal)}]}],
            'usage': {'input_tokens': 8, 'output_tokens': 4}}}])

    def run_mode(self, mode):
        return a.execute(mode, self.request_path, self.report, client=self.client, evidence='synthetic')

    def test_local_status_and_open_make_no_provider_calls_and_leak_no_identity(self):
        self.assertFalse(self.client.status()['connected'])
        opener = mock.Mock(return_value=True)
        self.assertTrue(self.client.open_browser(opener))
        self.assertEqual([], self.fixture.transport.calls)
        safe = c.canonical(c.public_status(self.fixture.p.status()))
        for secret in (self.fixture.p.capability, self.fixture.p.bootstrap, self.fixture.p.generation):
            self.assertNotIn(secret.encode(), safe)
        self.assertNotIn(b'account_label', safe)

    def test_missing_login_and_missing_allowance_never_infer(self):
        report = self.run_mode('run-once')
        self.assertFalse(report['provider_request_attempted'])
        self.fixture.enable()
        report = self.run_mode('run-once')
        self.assertEqual('initial_world_allowance_not_enabled', report['error'])
        self.assertFalse(report['provider_request_attempted'])
        self.assertFalse(any(call[1].endswith('/responses') for call in self.fixture.transport.calls))

    def test_ready_inspect_preserves_full_graph_and_allowance(self):
        self.enable()
        before = len(self.fixture.transport.calls)
        report = self.run_mode('inspect')
        self.assertEqual('ready_awaiting_explicit_run_once', report['disposition'])
        self.assertEqual(before, len(self.fixture.transport.calls))
        self.assertEqual(1, self.fixture.p.status()['initial_world']['remaining'])
        body = a.body_for(self.request, self.request_path.read_text(), 'second')
        self.assertEqual(self.request, c.strict_json(body['messages'][1]['content']))

    def test_one_completed_request_validates_consumes_and_exports_safe_native_completion(self):
        self.enable(); self.complete()
        report = self.run_mode('run-once')
        self.assertEqual('completed_contract_passed_pending_native_apply', report['disposition'])
        self.assertFalse(report['live_provider_inference'])
        self.assertTrue(report['completed_response_received'])
        self.assertEqual('second-2026-10-14',report['provider_returned_model'])
        self.assertFalse(report['provider_model_exact_match'])
        self.assertEqual(1, report['change_count'])
        self.assertFalse(report['native_state_apply_verified'])
        response = c.strict_json(Path(report['completion_artifact']).read_bytes())
        self.assertEqual({'prompt_tokens': 8, 'completion_tokens': 4, 'total_tokens': 12}, response['usage'])
        combined = self.report.read_bytes() + Path(report['completion_artifact']).read_bytes()
        for secret in (self.fixture.p.capability, self.fixture.p.bootstrap, self.fixture.p.generation, 'mock@example.invalid', 'FAKE_ACCESS_PRIVATE', 'FAKE_REFRESH_PRIVATE'):
            self.assertNotIn(secret.encode(), combined)
        count = len(self.fixture.transport.calls)
        second = self.run_mode('run-once')
        self.assertFalse(second['provider_request_attempted'])
        self.assertEqual(count, len(self.fixture.transport.calls))

    def test_stale_native_generation_refuses_before_inference(self):
        self.enable(); self.complete()
        original = self.client.request
        def request(method, path, body=None, generation=None, timeout=5):
            if method == 'POST':
                self.fixture.p.session({'model': 'first'})
            return original(method, path, body, generation, timeout)
        self.client.request = request
        report = self.run_mode('run-once')
        self.assertEqual('provider_session_changed', report['error'])
        self.assertFalse(any(call[1].endswith('/responses') for call in self.fixture.transport.calls))

    def test_proposal_rejects_wrong_basis_derived_state_bad_target_and_duplicate(self):
        _, _, entities, _ = a.load_request(self.request_path)
        base = {'basis_digest': self.request['basis_digest'], 'seed': '42', 'changes': []}
        invalid = [dict(base, basis_digest='b' * 64), dict(base, seed=42), dict(base, scenario_id=1),
                   dict(base, changes=[{'entity_id': 'grid', 'property': 'fire', 'value': 1}]),
                   dict(base, changes=[{'entity_id': 'glyph', 'property': 'wetness', 'value': .5}]),
                   dict(base, changes=[{'entity_id': '@world', 'property': 'rain', 'value': 2}]),
                   dict(base, changes=[{'entity_id': 'grid', 'property': 'wetness', 'value': True}]),
                   dict(base, changes=[{'entity_id': 'grid', 'property': 'wetness', 'value': .4}] * 2)]
        for proposal in invalid:
            with self.subTest(proposal=proposal), self.assertRaises(c.LocalFailure):
                a.validate_proposal(c.canonical(proposal), self.request, entities)

    def test_negative_completed_proposal_consumes_attempt_without_success_artifact(self):
        self.enable(); self.complete({'basis_digest': 'b' * 64, 'seed': '42', 'changes': []})
        report = self.run_mode('run-once')
        self.assertEqual('rejected_basis_or_seed', report['error'])
        self.assertFalse(report['live_provider_inference'])
        self.assertTrue(report['completed_response_received'])
        self.assertEqual(8,report['usage']['prompt_tokens'])
        self.assertFalse(report['status_after']['initial_attempt_allowed'])
        self.assertEqual(1, self.fixture.p.status()['usage']['requests'])
        self.assertFalse(self.report.with_suffix('.completion.json').exists())

    def test_endpoint_redirect_userinfo_capability_and_duplicate_json_refused(self):
        descriptor = {'schema': 1, 'endpoint': self.fixture.p.endpoint, 'capability': self.fixture.p.capability,
                      'browser_url': self.fixture.p.endpoint + '/#' + self.fixture.p.bootstrap}
        for endpoint in ('https://api.openai.com', 'http://localhost:18953', 'http://127.0.0.1:18953?x=1', 'http://a@127.0.0.1:18953'):
            with self.subTest(endpoint=endpoint), self.assertRaises(c.LocalFailure):
                c.LocalClient(dict(descriptor, endpoint=endpoint))
        with self.assertRaises(c.LocalFailure):
            c.strict_json('{"seed":"42","seed":"43"}')
        with self.assertRaises(c.LocalFailure):
            self.client.request('POST', '/auth/start', {})

    def test_descriptor_permissions_and_symlink_refused(self):
        target = self.fixture.store.directory / 'connection.json'
        os.chmod(target, 0o644)
        with self.assertRaises(c.LocalFailure):
            c.LocalClient.load(target.parent)
        os.chmod(target, 0o600)
        other = self.fixture.store.directory / 'other.json'
        other.symlink_to(target)
        with self.assertRaises(c.LocalFailure):
            c.private_read(other)

    def test_second_provider_same_model_and_exact_selection_routes_without_key_fallback(self):
        self.enable()
        start = self.fixture.p.auth_start(provider_id='mock-beta')
        query = {key: value[0] for key, value in urllib.parse.parse_qs(urllib.parse.urlsplit(start['authorization_url']).query).items()}
        self.beta_transport.codes['beta-login'] = self.beta_transport.token('oaiapp_beta_mock', query['nonce'])
        self.fixture.p.callback({'state': query['state'], 'code': 'beta-login', 'client_id': 'oaiapp_beta_mock'})
        models = self.fixture.p.models()['models']
        matching = [row for row in models if row['model_id'] == 'second']
        self.assertEqual(2, len(matching))
        first = self.client.status()['selection']
        row = next(row for row in matching if row['provider_id'] == 'mock-beta')
        self.fixture.p.session({'provider_id': 'mock-beta', 'account_id': row['account_id'], 'model': row['model_id'],
            'catalog_version': row['catalog_version'], 'cloud_enabled': True, 'initial_world_allowance': {'input_tokens': 192000, 'output_tokens': 512}})
        selected = self.client.status()['selection']
        self.assertEqual(first['model_id'], selected['model_id'])
        self.assertNotEqual(c.selection_record(first)['selection_fingerprint'], c.selection_record(selected)['selection_fingerprint'])
        proposal = {'basis_digest': self.request['basis_digest'], 'seed': '42', 'changes': []}
        self.beta_transport.stream = fixture.events([{'type': 'response.completed', 'response': {'id': 'beta-completed', 'status': 'completed',
            'model': 'second', 'output': [{'type': 'message', 'content': [{'type': 'output_text', 'text': json.dumps(proposal)}]}],
            'usage': {'input_tokens': 8, 'output_tokens': 4}}}])
        report = self.run_mode('run-once')
        self.assertEqual('completed_contract_passed_pending_native_apply', report['disposition'])
        self.assertEqual('mock-beta', report['status']['selection']['provider_id'])
        self.assertFalse(report['live_provider_inference'])
        self.assertEqual(1, sum(call[1].endswith('/responses') for call in self.beta_transport.calls))
        self.assertEqual(0, sum(call[1].endswith('/responses') for call in self.fixture.transport.calls))
        self.assertNotIn(selected['account_id'], self.report.read_text())
        self.assertNotIn(selected['session_generation'], self.report.read_text())

    def test_unregistered_paid_missing_or_mismatched_selection_refused_before_post(self):
        self.enable()
        status = self.client.status()
        for mutate in (lambda v: v['selection'].__setitem__('billing', 'api_key'),
                       lambda v: v['selection'].__setitem__('provider_id', 'not-registered'),
                       lambda v: v['selection'].__setitem__('model_id', 'different'),
                       lambda v: v['selection'].__setitem__('account_id', ''),
                       lambda v: v['selection'].__setitem__('token', 'not-allowed')):
            invalid = json.loads(json.dumps(status));mutate(invalid)
            with self.subTest(status=invalid), self.assertRaises(c.LocalFailure):
                c.validate_native_status(invalid)
        body = a.body_for(self.request, self.request_path.read_text(), status['model'])
        with self.assertRaises(c.LocalFailure):
            self.client.request('POST', '/v1/chat/completions', body, status['session_generation'])
        self.assertFalse(any(call[1].endswith('/responses') for call in self.fixture.transport.calls))

    def test_legacy_status_is_read_only_and_never_authorizes_inference(self):
        legacy = {'provider': 'openai-chatgpt', 'connected': True, 'cloud_enabled': True, 'model': 'second',
            'session_generation': 'legacy-generation', 'plan_permission': True, 'quota_stopped': False,
            'initial_world': {'remaining': 1, 'allowance': {'input_tokens': 192000, 'output_tokens': 512}},
            'usage': {'requests': 0, 'input_tokens': 0, 'output_tokens': 0}}
        with self.assertRaises(c.LocalFailure):
            c.validate_native_status(legacy)
        observed = c.validate_native_status(legacy, allow_legacy_readonly=True)
        self.assertEqual('legacy-chatgpt-readonly', observed['protocol'])
        self.assertIsNone(observed['selection'])
        body = a.body_for(self.request, self.request_path.read_text(), 'second')
        with self.assertRaises(c.LocalFailure):
            a.ready(observed, body)
        with mock.patch.object(self.client, 'request', return_value=legacy):
            self.assertEqual('legacy-chatgpt-readonly', self.client.status(allow_legacy_readonly=True)['protocol'])
            self.assertIsNone(self.client._selection)

    def test_provider_catalog_model_slash_and_completion_billing_are_guarded(self):
        self.enable();self.complete()
        status = self.client.status();choice = dict(status['selection'], model_id='organization/model:revision')
        self.assertEqual(choice, c.selection_binding(choice, status['providers']))
        body = a.body_for(self.request, self.request_path.read_text(), choice['model_id'], selection=choice)
        self.assertEqual(choice, body['chooguard_selection'])
        body = a.body_for(self.request, self.request_path.read_text(), status['model'], selection=status['selection'])
        reply = self.client.request('POST', '/v1/chat/completions', body, status['session_generation'])
        reply['chooguard']['billing'] = 'api_key'
        _, _, entities, _ = a.load_request(self.request_path)
        with self.assertRaises(c.LocalFailure):
            a.validate_completion(reply, status, body, self.request, entities)

    def test_compact_columns_references_and_actual_local_http_keep_every_entity(self):
        self.request=compact_fixture();self.request_path.write_bytes(c.canonical(self.request))
        decoded,identities=g.decode_graph(self.request['graph'])
        self.assertEqual(['@world','glyph','grid-a','grid-b'],identities)
        self.assertEqual(['grid-a',None,'cabinet',[1,0,0],0,0,[.1],0,0,True,None],decoded['entities'][1])
        self.assertEqual(['grid-b',None,'cabinet',[3,0,-1],0,0,[.25],0,0,True,None],decoded['entities'][2])
        self.assertEqual([['grid-b'],[]],decoded['link_sets'])
        self.assertEqual([['glyph','grid-a','unverified']],decoded['visuals'])
        _,_,entities,_=a.load_request(self.request_path)
        proposal={'basis_digest':self.request['basis_digest'],'seed':'42','changes':[{'entity_ref':2,'property':'wetness','value':.8},{'entity_ref':0,'property':'rain','value':.2}]}
        normalized=a.validate_proposal(c.canonical(proposal),self.request,entities)
        self.assertEqual(['grid-a','@world'],[change['entity_id'] for change in normalized['changes']])
        self.enable();self.complete(proposal)
        report=self.run_mode('run-once')
        self.assertEqual('completed_contract_passed_pending_native_apply',report['disposition'])
        self.assertEqual('prefix-column-v1',report['graph_encoding'])
        sent=json.loads(next(call[3] for call in self.fixture.transport.calls if call[1].endswith('/responses')))
        self.assertEqual(self.request,c.strict_json(sent['input'][1]['content']))

    def test_compact_malformed_prefix_reference_column_count_and_proposals_refused(self):
        base=compact_fixture()
        mutations=[lambda graph:graph['identities'].__setitem__(2,[99,'bad']),
                   lambda graph:graph.__setitem__('numeric_scale',1000),
                   lambda graph:graph['entity_groups'][0]['entity_refs'].__setitem__(0,99),
                   lambda graph:graph['entity_groups'][0]['entity_refs'].__setitem__(1,2),
                   lambda graph:graph['entity_groups'][0]['xyz_columns'].__setitem__(0,[10000]),
                   lambda graph:graph['entity_groups'][0]['xyz_columns'].__setitem__(0,[2**63-1,1]),
                   lambda graph:graph.__setitem__('entity_count',2),
                   lambda graph:graph['visuals'][0].__setitem__(1,99)]
        for mutate in mutations:
            request=json.loads(json.dumps(base));mutate(request['graph'])
            with self.subTest(request=request),self.assertRaises(c.LocalFailure):
                g.decode_graph(request['graph'])
        self.request_path.write_bytes(c.canonical(base));_,_,entities,_=a.load_request(self.request_path)
        invalid=[{'entity_ref':True,'property':'rain','value':.1},
                 {'entity_ref':99,'property':'rain','value':.1},
                 {'entity_ref':1,'property':'wetness','value':.1},
                 {'entity_id':'grid-a','entity_ref':2,'property':'wetness','value':.1}]
        for change in invalid:
            with self.subTest(change=change),self.assertRaises(c.LocalFailure):
                a.validate_proposal(c.canonical({'basis_digest':base['basis_digest'],'seed':'42','changes':[change]}),base,entities)
        with self.assertRaises(c.LocalFailure):
            a.validate_proposal(c.canonical({'basis_digest':base['basis_digest'],'seed':'42','changes':[
                {'entity_id':'grid-a','property':'wetness','value':.1},{'entity_ref':2,'property':'wetness','value':.2}]}),base,entities)

    def test_compact_raw_large_finite_columns_and_empty_world_match_native_contract(self):
        request=compact_fixture()
        request['graph']['entity_groups'][0]['xyz_columns'][0]={'raw':[1e100,-1e100]}
        decoded,_=g.decode_graph(request['graph'])
        self.assertEqual(1e100,decoded['entities'][1][3][0])
        self.assertEqual(-1e100,decoded['entities'][2][3][0])
        for column in ({'raw':[1e100]},{'raw':[1e100,float('inf')]},{'raw':[True,0]},{'raw':[1,2],'scale':10000}):
            request['graph']['entity_groups'][0]['xyz_columns'][0]=column
            with self.subTest(column=column),self.assertRaises(c.LocalFailure):
                g.decode_graph(request['graph'])
        empty=compact_fixture()
        empty['graph'].update(identities=[[0,'@world']],entity_groups=[],entity_count=0,kernel_semantic_count=0,visual_count=0,visuals=[],tag_sets=[],state_schemas=[],link_sets=[],conditional_operation_sets=[])
        self.request_path.write_bytes(c.canonical(empty))
        _,_,entities,_=a.load_request(self.request_path)
        self.assertEqual(0,len(entities))
        proposal={'basis_digest':empty['basis_digest'],'seed':'42','changes':[{'entity_ref':0,'property':'rain','value':.2}]}
        self.assertEqual('@world',a.validate_proposal(c.canonical(proposal),empty,entities)['changes'][0]['entity_id'])


class LifecycleTests(unittest.TestCase):
    def test_start_timeout_never_claims_a_helper_on_the_wrong_port(self):
        for later_failure in (False, True):
            with self.subTest(later_failure=later_failure), tempfile.TemporaryDirectory(prefix='chooguard-start-synthetic-') as directory:
                private = Path(directory) / 'private'
                child = mock.Mock(pid=12345)
                child.poll.return_value = None
                client = mock.Mock(port=18955, endpoint='http://127.0.0.1:18955')
                client.status.side_effect = [{}, c.LocalFailure('unavailable')] if later_failure else [{}]
                clock = [0, 0, 1, 9] if later_failure else [0, 0, 9]
                with mock.patch.object(session, 'status', side_effect=c.LocalFailure('unavailable')), \
                     mock.patch.object(session, 'owned_process', return_value=None), \
                     mock.patch.object(session.subprocess, 'Popen', return_value=child), \
                     mock.patch.object(session.LocalClient, 'load', return_value=client), \
                     mock.patch.object(session, 'process_identity', return_value='synthetic child'), \
                     mock.patch.object(session, 'public_status', return_value={}), \
                     mock.patch.object(session.time, 'monotonic', side_effect=clock), \
                     mock.patch.object(session.time, 'sleep'):
                    with self.assertRaises(c.LocalFailure) as error:
                        session.start(private, 18954)
                self.assertEqual('local_helper_start_timeout', error.exception.code)
                self.assertFalse((private / session.OWNER_FILE).exists())
                child.send_signal.assert_called_once_with(session.signal.SIGTERM)
                child.wait.assert_called_once_with(timeout=4)
                child.kill.assert_not_called()

    def test_start_waits_for_requested_port_before_recording_ownership(self):
        with tempfile.TemporaryDirectory(prefix='chooguard-start-synthetic-') as directory:
            private = Path(directory) / 'private'
            child = mock.Mock(pid=12345)
            child.poll.return_value = None
            wrong = mock.Mock(port=18955, endpoint='http://127.0.0.1:18955')
            ready = mock.Mock(port=18954, endpoint='http://127.0.0.1:18954')
            with mock.patch.object(session, 'status', side_effect=c.LocalFailure('unavailable')), \
                 mock.patch.object(session, 'owned_process', return_value=None), \
                 mock.patch.object(session.subprocess, 'Popen', return_value=child), \
                 mock.patch.object(session.LocalClient, 'load', side_effect=[wrong, ready]), \
                 mock.patch.object(session, 'process_identity', return_value='synthetic child'), \
                 mock.patch.object(session, 'public_status', return_value={}), \
                 mock.patch.object(session.time, 'monotonic', side_effect=[0, 0, 1]), \
                 mock.patch.object(session.time, 'sleep'):
                result = session.start(private, 18954)
            self.assertTrue(result['started_new_process'])
            self.assertEqual('http://127.0.0.1:18954/', result['local_ui'])
            self.assertEqual(12345, c.strict_json((private / session.OWNER_FILE).read_bytes())['pid'])
            child.send_signal.assert_not_called()
            child.wait.assert_not_called()

    def test_actual_scoped_child_start_reopen_stop_retains_mock_registration_and_cleans_descriptor(self):
        with tempfile.TemporaryDirectory(prefix='chooguard-lifecycle-synthetic-') as directory:
            private = Path(directory) / 'private'
            with socket.socket() as sock:
                sock.bind(('127.0.0.1', 0)); port = sock.getsockname()[1]
            started = session.start(private, port)
            try:
                self.assertTrue(started['started_new_process'])
                self.assertFalse(started['status']['connected'])
                self.assertFalse(started['provider_requests_made_by_start'])
                original = (private / 'accounts.json').read_bytes()
                second = session.start(private, port)
                self.assertEqual(started['pid'], second['pid'])
                self.assertFalse(second['started_new_process'])
            finally:
                stopped = session.stop(private)
            self.assertTrue(stopped['descriptor_removed_by_helper'])
            self.assertEqual(original, (private / 'accounts.json').read_bytes())
            self.assertFalse((private / session.OWNER_FILE).exists())

    def test_pid_reuse_or_missing_owner_never_signals_other_process(self):
        with tempfile.TemporaryDirectory(prefix='chooguard-lifecycle-synthetic-') as directory:
            private = Path(directory)
            c.write_json(private / session.OWNER_FILE, {'schema': 1, 'pid': 12345, 'process_identity': 'prior-created-process', 'sidecar_path': str(session.SIDECAR)})
            with mock.patch.object(session, 'process_identity', return_value='different-created-process'), mock.patch.object(session.os, 'kill') as kill:
                with self.assertRaises(c.LocalFailure):
                    session.stop(private)
                kill.assert_not_called()


if __name__ == '__main__':
    import hashlib
    import time
    stream = io.StringIO()
    suite = unittest.defaultTestLoader.loadTestsFromModule(sys.modules[__name__])
    began = time.monotonic()
    paths = [Path(__file__), Path(a.__file__), Path(g.__file__), Path(c.__file__), Path(session.__file__), *sorted(session.SIDECAR.parent.glob('*.py'))]
    hashes = {str(path.relative_to(c.ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}
    result = unittest.TextTestRunner(stream=stream, verbosity=2).run(suite)
    report = {'passed': result.wasSuccessful(), 'test_count': result.testsRun, 'elapsed_seconds': round(time.monotonic() - began, 3),
              'source_hashes': hashes, 'source_changed_during_run': any(hashlib.sha256(path.read_bytes()).hexdigest() != hashes[str(path.relative_to(c.ROOT))] for path in paths),
              'live_provider_login': False, 'live_provider_inference': False, 'existing_cli_credentials_read': False, 'stdout': stream.getvalue()}
    c.write_json(Path(__file__).with_name('test-report.json'), report)
    print(stream.getvalue(), end='')
    sys.exit(0 if result.wasSuccessful() else 1)
