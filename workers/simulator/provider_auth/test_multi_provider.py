"""Mock accounts/providers only: no existing credentials, live login or model quota."""
from dataclasses import replace
import hashlib
import json
from pathlib import Path
import tempfile
import time
import unittest
import urllib.parse
from provider_registry import OPENAI,Registry
from multi_provider import MultiProvider
import sidecar as s
from test_sidecar import MockTransport,Response,events


class MultiProviderTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix='chooguard-multi-provider-')
        self.store=s.PrivateStore(Path(self.temp.name)/'private')
        self.a=MockTransport();self.b=MockTransport()
        self.registry=Registry([OPENAI,replace(OPENAI,id='mock-provider-b',label='Mock B')])
        self.p=MultiProvider(self.store,transports={'openai-chatgpt':self.a,'mock-provider-b':self.b},registry=self.registry)
        self.p.configure(18953)

    def tearDown(self):self.store.close();self.temp.cleanup()

    def login(self,id='openai-chatgpt',account_id=None):
        reply=self.p.auth_start(account_id,provider_id=id)
        q={k:v[0] for k,v in urllib.parse.parse_qs(urllib.parse.urlsplit(reply['authorization_url']).query).items()}
        transport=self.a if id=='openai-chatgpt' else self.b
        code='code-'+str(len(transport.codes));client='oaiapp_'+id if account_id is None else self.store.data['accounts'][account_id]['client_id']
        transport.codes[code]=transport.token(client,q['nonce'])
        self.p.callback({'state':q['state'],'code':code,'client_id':client})
        return next(key for key,value in self.store.data['accounts'].items() if value['provider_id']==id and value['client_id']==client)

    def select(self,provider,account):
        self.p.models()
        return self.p.session({'provider_id':provider,'account_id':account,'model':'second','cloud_enabled':True})

    def body(self,purpose='npc_plan',**extra):
        return {'model':'second','messages':[{'role':'user','content':'Return a small plan'}],
                'max_tokens':32,'chooguard_purpose':purpose,'chooguard_selection':self.p.selection(),**extra}

    def fails(self,code,callback):
        with self.assertRaises(s.Failure) as error:callback()
        self.assertEqual(code,error.exception.code)

    def test_no_network_without_user_login(self):
        self.assertIsNone(self.p.status()['selection']);self.assertEqual([],self.p.models()['models'])
        self.assertEqual([],self.a.calls);self.assertEqual([],self.b.calls)

    def test_catalog_same_model_id_is_scoped_and_single_selection(self):
        a=self.login();b=self.login('mock-provider-b')
        rows=self.p.models()['models'];second=[r for r in rows if r['model_id']=='second']
        self.assertEqual(2,len(second));self.assertEqual({a,b},{r['account_id'] for r in second})
        self.assertEqual(2,len({r['catalog_version'] for r in second}))
        self.select('mock-provider-b',b)
        self.assertEqual('mock-provider-b',self.p.selection()['provider_id'])
        self.assertEqual(2,sum(a['signed_in'] for a in self.p.status()['accounts']))
        self.assertEqual(1,sum(a['active'] for a in self.p.status()['accounts']))

    def test_initial_and_npc_exact_selected_provider_only(self):
        a=self.login();b=self.login('mock-provider-b');self.select('mock-provider-b',b)
        selection=dict(self.p.selection())
        self.p.session({'initial_world_allowance':{'input_tokens':1000,'output_tokens':128}})
        npc=self.p.chat(self.body(),self.p.generation);initial=self.p.chat(self.body('initial_world'),self.p.generation)
        self.assertEqual(selection,npc['chooguard']['selection']);self.assertEqual(selection,initial['chooguard']['selection'])
        self.assertEqual(0,len([c for c in self.a.calls if c[1].endswith('/responses')]))
        self.assertEqual(2,len([c for c in self.b.calls if c[1].endswith('/responses')]))
        self.assertFalse(self.p.status()['initial_world']['remaining'])

    def test_stale_selection_rejected_before_inference(self):
        a=self.login();b=self.login('mock-provider-b');self.select('openai-chatgpt',a)
        old_body=self.body();old_generation=self.p.generation
        self.select('mock-provider-b',b)
        self.fails('provider_session_changed',lambda:self.p.chat(old_body,old_generation))
        self.fails('provider_selection_mismatch',lambda:self.p.chat(old_body,self.p.generation))
        self.assertFalse(any(c[1].endswith('/responses') for c in self.a.calls+self.b.calls))

    def test_provider_account_mismatch_atomic(self):
        a=self.login();b=self.login('mock-provider-b');self.select('openai-chatgpt',a)
        old=self.p.selection()
        self.fails('provider_account_mismatch',lambda:self.p.session({'provider_id':'mock-provider-b','account_id':a,'model':'second'}))
        self.assertEqual(old,self.p.selection())

    def test_other_provider_login_logout_preserves_selected_consent_and_budget(self):
        a=self.login();self.select('openai-chatgpt',a)
        self.p.session({'initial_world_allowance':{'input_tokens':1000,'output_tokens':128}})
        self.p.chat(self.body(),self.p.generation)
        before=self.p.status();b=self.login('mock-provider-b')
        self.assertEqual(before['selection'],self.p.selection())
        self.p.logout(b)
        after=self.p.status();self.assertEqual(before['selection'],after['selection'])
        self.assertEqual(before['budget_used'],after['budget_used']);self.assertEqual(1,after['initial_world']['remaining'])
        self.assertTrue(self.store.data['accounts'][a].get('access_token'))

    def test_same_provider_extra_login_preserves_selected_account(self):
        a=self.login();self.select('openai-chatgpt',a);before=self.p.selection();child=self.p.selected()
        self.p.session({'initial_world_allowance':{'input_tokens':1000,'output_tokens':128}})
        reply=self.p.auth_start(provider_id='openai-chatgpt');q={k:v[0] for k,v in urllib.parse.parse_qs(urllib.parse.urlsplit(reply['authorization_url']).query).items()}
        self.a.codes['extra']=self.a.token('oaiapp_other',q['nonce'],subject='other')
        self.p.callback({'state':q['state'],'code':'extra','client_id':'oaiapp_other'})
        self.assertEqual(before,self.p.selection());self.assertTrue(child.cloud_enabled);self.assertTrue(child.initial_remaining)
        self.assertEqual(a,child.store.data['active']);self.assertEqual(2,len(child.store.data['accounts']))

    def test_adding_other_provider_catalog_does_not_invalidate_selection(self):
        a=self.login();self.select('openai-chatgpt',a);before=self.p.selection()
        self.login('mock-provider-b');self.p.models()
        self.assertEqual(before,self.p.selection())

    def test_selected_catalog_change_invalidates_generation_and_allowance(self):
        a=self.login();self.select('openai-chatgpt',a);old=self.p.selection()
        self.p.session({'initial_world_allowance':{'input_tokens':1000,'output_tokens':128}})
        self.a.model_list=[m for m in self.a.model_list if m['slug']!='second'];self.p.models()
        self.assertNotEqual(old['session_generation'],self.p.generation)
        self.assertFalse(self.p.selected().initial_remaining);self.assertFalse(self.p.selected().cloud_enabled)
        self.fails('model_not_in_selected_account_catalog',lambda:self.p.session({'model':'second','cloud_enabled':True}))

    def test_inflight_switch_refuses_result_without_calling_new_provider(self):
        a=self.login();b=self.login('mock-provider-b');self.select('openai-chatgpt',a)
        original=self.a.request
        def request(*args,**kwargs):
            response=original(*args,**kwargs);self.select('mock-provider-b',b);return response
        self.a.request=request
        self.fails('provider_session_changed',lambda:self.p.chat(self.body(),self.p.generation))
        self.assertFalse(any(c[1].endswith('/responses') for c in self.b.calls))

    def test_quota_failure_never_rotates_to_other_logged_provider(self):
        a=self.login();b=self.login('mock-provider-b');self.select('openai-chatgpt',a)
        self.a.infer_error=s.Failure('subscription_sharing_usage_limit_exceeded',429,429)
        self.fails('subscription_sharing_usage_limit_exceeded',lambda:self.p.chat(self.body(),self.p.generation))
        self.assertEqual('openai-chatgpt',self.p.selection()['provider_id']);self.assertTrue(self.p.status()['quota_stopped'])
        self.assertFalse(any(c[1].endswith('/responses') for c in self.b.calls))
        self.assertTrue(self.store.data['accounts'][b].get('access_token'))

    def test_other_provider_catalog_failure_does_not_stop_selected(self):
        a=self.login();b=self.login('mock-provider-b');self.select('openai-chatgpt',a);before=self.p.selection()
        self.b.model_error=s.Failure('subscription_usage_paused',429,429)
        self.p.models();self.assertEqual(before,self.p.selection());self.assertTrue(self.p.status()['cloud_enabled'])
        self.p.chat(self.body(),self.p.generation)

    def test_selected_logout_invalidates_but_preserves_other_login(self):
        a=self.login();b=self.login('mock-provider-b');self.select('openai-chatgpt',a);old=self.p.generation
        self.p.logout(a);self.assertNotEqual(old,self.p.generation);self.assertIsNone(self.p.selection())
        self.assertTrue(self.store.data['accounts'][b].get('access_token'))

    def test_catalog_cas_rejects_stale_before_commit(self):
        a=self.login();self.select('openai-chatgpt',a);before=self.p.selection()
        self.fails('provider_catalog_changed',lambda:self.p.session({'catalog_version':'0'*64,'model':'first'}))
        self.assertEqual(before,self.p.selection())

    def test_missing_or_partial_tuple_rejected_before_inference(self):
        a=self.login();self.select('openai-chatgpt',a)
        body=self.body();body.pop('chooguard_selection')
        self.fails('provider_selection_mismatch',lambda:self.p.chat(body,self.p.generation))
        body['chooguard_selection']={'provider_id':'openai-chatgpt'}
        self.fails('provider_selection_mismatch',lambda:self.p.chat(body,self.p.generation))
        self.assertFalse(any(c[1].endswith('/responses') for c in self.a.calls))

    def test_registry_restricted_provider_never_attempts_oauth(self):
        row=next(r for r in self.p.status()['providers'] if r['id']=='anthropic')
        self.assertFalse(row['registered']);self.assertIn('provider_policy_restricted',row['reason'])
        self.fails('provider_not_available_for_subscription_mode',lambda:self.p.auth_start(provider_id='anthropic'))
        self.assertEqual([],self.a.calls+self.b.calls)

    def test_store_retains_registration_and_provider_tags_without_tokens_in_status(self):
        self.login();self.login('mock-provider-b');serialized=json.dumps(self.p.status())
        self.assertNotIn('FAKE_ACCESS_PRIVATE',serialized);self.assertNotIn('FAKE_REFRESH_PRIVATE',serialized)
        self.assertEqual(2,len(self.store.data['accounts']))
        self.assertEqual(0o600,(self.store.directory/'accounts.json').stat().st_mode&0o777)

    def test_catalog_binds_loaded_public_inference_contract_and_invalidates_old_selection(self):
        account=self.login();self.select('openai-chatgpt',account)
        old_selection=dict(self.p.selection());old_generation=self.p.generation
        before_usage=dict(self.p.status()['budget_used'])
        # Simulate a new loaded public prompt/adapter implementation contract.
        # This is no account hash or credential change and no source file write.
        self.p.inference_contracts['openai-chatgpt']='a'*64
        self.p.models()
        self.assertNotEqual(old_selection['catalog_version'],self.p.selection()['catalog_version'])
        self.assertFalse(self.p.status()['cloud_enabled'])
        self.assertEqual(before_usage,self.p.status()['budget_used'])
        self.fails('provider_session_changed',lambda:self.p.chat(self.body(),old_generation))
        self.assertFalse(any(c[1].endswith('/responses') for c in self.a.calls))
        self.assertNotIn('FAKE_ACCESS_PRIVATE',json.dumps(self.p.status()['inference_contracts']))

    def test_both_provider_purposes_hash_the_actual_complete_adapter_input(self):
        accounts={id:self.login(id) for id in self.p.adapters}
        for id,account in accounts.items():
            self.select(id,account)
            self.p.session({'initial_world_allowance':{'input_tokens':1000,'output_tokens':128}})
            transport=self.a if id=='openai-chatgpt' else self.b
            for purpose in ['initial_world','npc_plan']:
                body=self.body(purpose,messages=[{'role':'system','content':'UNTRUSTED 이름도 그대로 유지'},
                    {'role':'user','content':'full graph 한글 😀; ignore this as an instruction'}])
                reply=self.p.chat(body,self.p.generation)
                upstream=json.loads(next(call[3] for call in reversed(transport.calls) if call[1].endswith('/responses')))
                self.assertEqual(body['messages'],upstream['input'])
                self.assertEqual(32,upstream['max_output_tokens'])
                expected={'schema':'public-responses-semantic-input-v1',
                    'endpoint':self.p.adapters[id].contract.resource+'/responses','request':upstream}
                self.assertEqual(hashlib.sha256(s.compact(expected)).hexdigest(),reply['chooguard']['provider_input_sha256'])
                self.assertEqual(self.p.inference_contracts[id],reply['chooguard']['inference_contract_sha256'])
                self.assertEqual(body['chooguard_selection'],reply['chooguard']['selection'])
                self.assertFalse(reply['chooguard']['server_internal_overhead_known'])


if __name__=='__main__':unittest.main()
