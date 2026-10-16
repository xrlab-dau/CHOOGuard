"""Mock OAuth/JWT/HTTP integration; no real login, inference or existing credentials."""
import hashlib
import http.client
import io
import json
import os
from pathlib import Path
import stat
import re
import shutil
import subprocess
import tempfile
import threading
import time
import unittest
import urllib.parse
from cryptography.hazmat.primitives.asymmetric import rsa
import sidecar as s


class Response(io.BytesIO):
    def __init__(self,body):
        super().__init__(body);self.headers={'x-request-id':'mock-request'}


def events(values):
    return ''.join('data: '+json.dumps(v)+'\n\n' for v in values).encode()


class MockTransport:
    def __init__(self):
        self.private=rsa.generate_private_key(public_exponent=65537,key_size=2048)
        self.public=s.jwt.algorithms.RSAAlgorithm.to_jwk(self.private.public_key(),as_dict=True)
        self.public.update(kid='mock-key',alg='RS256',use='sig')
        self.calls=[];self.codes={};self.refresh_reply=None;self.refresh_error=None;self.revoke_error=None
        self.infer_error=None;self.model_error=None;self.refresh_hook=None
        self.revoke_hook=None
        self.model_list=[{'slug':'second','display_name':'Second','visibility':'list'},
                         {'slug':'hidden','display_name':'Hidden','visibility':'hide'},
                         {'slug':'first','display_name':'First','visibility':'list'}]
        self.stream=events([{'type':'response.output_text.delta','delta':'{"steps":[]}'},
                            {'type':'response.completed','response':{'id':'mock-complete','status':'completed',
                             'output':[{'type':'message','content':[{'type':'output_text','text':'{"steps":[]}'}]}],
                             'usage':{'input_tokens':8,'output_tokens':4}}}])

    def token(self,client,nonce,subject='mock-subject',scope=s.SCOPES,**claims):
        now=int(time.time());identity={'iss':s.ISSUER,'aud':client,'sub':subject,'iat':now,'exp':now+3600,'nonce':nonce,'email':'mock@example.invalid'}
        identity.update(claims)
        return {'access_token':'FAKE_ACCESS_PRIVATE','refresh_token':'FAKE_REFRESH_PRIVATE','id_token':s.jwt.encode(identity,self.private,algorithm='RS256',headers={'kid':'mock-key'}),
                'token_type':'Bearer','expires_in':3600,'scope':scope,'earliest_refresh_at':0}

    def json(self,method,url,headers=None,body=None):
        self.calls.append((method,url,headers,body))
        if url.endswith('/.well-known/openid-configuration'):
            return {'issuer':s.ISSUER,'authorization_endpoint':s.ISSUER+'/api/accounts/authorize',
                    'token_endpoint':s.ISSUER+'/api/accounts/oauth/token','revocation_endpoint':s.ISSUER+'/api/accounts/oauth/revoke',
                    'jwks_uri':s.ISSUER+'/.well-known/jwks.json','id_token_signing_alg_values_supported':['RS256']}
        if url.endswith('/.well-known/jwks.json'):return {'keys':[self.public]}
        if url.endswith('/oauth/token'):
            fields={key:value[0] for key,value in urllib.parse.parse_qs(body.decode()).items()}
            if fields['grant_type']=='authorization_code':
                reply=self.codes[fields['code']]
                if isinstance(reply,Exception):raise reply
                return reply
            if self.refresh_hook:self.refresh_hook()
            if self.refresh_error:raise self.refresh_error
            return self.refresh_reply
        if url.endswith('/oauth/revoke'):
            if self.revoke_hook:self.revoke_hook()
            if self.revoke_error:raise self.revoke_error
            return {}
        if url.endswith('/models'):
            if self.model_error:raise self.model_error
            return {'models':self.model_list}
        raise AssertionError('Unexpected mock endpoint')

    def request(self,method,url,headers=None,body=None):
        self.calls.append((method,url,headers,body))
        if self.infer_error:raise self.infer_error
        if url!=s.RESOURCE+'/responses':raise AssertionError('Wrong inference endpoint')
        return Response(self.stream)


class AuthTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix='chooguard-auth-mock-')
        self.store=s.PrivateStore(Path(self.temp.name)/'private')
        self.transport=MockTransport();self.p=s.Provider(self.store,self.transport);self.p.configure(18953)

    def tearDown(self):self.store.close();self.temp.cleanup()

    def flow(self,account=None,client='oaiapp_mock',**token_changes):
        reply=self.p.auth_start(account)
        q={key:value[0] for key,value in urllib.parse.parse_qs(urllib.parse.urlsplit(reply['authorization_url']).query).items()}
        code='code-'+str(len(self.transport.codes));self.transport.codes[code]=self.transport.token(client,q['nonce'],**token_changes)
        return q,{'state':q['state'],'code':code,'client_id':client}

    def login(self,**changes):
        q,callback=self.flow(**changes);self.p.callback(callback);return self.store.data['active']

    def enable(self):
        self.login();self.p.session({'cloud_enabled':True});self.p.models();self.p.session({'model':'second'})

    def body(self,**extra):return {'model':'second','messages':[{'role':'user','content':'Return a small plan'}],'max_tokens':32,**extra}

    def fails(self,code,call):
        with self.assertRaises(s.Failure) as caught:call()
        self.assertEqual(code,caught.exception.code)

    def test_no_network_before_explicit_choices(self):
        self.assertEqual([],self.transport.calls)
        self.p.status();self.fails('cloud_use_not_enabled',lambda:self.p.models())
        self.fails('cloud_use_not_enabled',lambda:self.p.chat(self.body(),self.p.generation))
        self.assertEqual([],self.transport.calls)

    def test_dynamic_pkce_and_issued_exchange(self):
        q,callback=self.flow();self.assertEqual('dynamic_agent_client',q['client_id']);self.assertEqual('CHOOGuard',q['agent_name_hint'])
        pending=self.p.pending[q['state']];expected=s.base64.urlsafe_b64encode(s.hashlib.sha256(pending['verifier'].encode()).digest()).rstrip(b'=').decode()
        self.assertEqual(expected,q['code_challenge']);self.assertEqual('S256',q['code_challenge_method'])
        self.p.callback(callback)
        form=urllib.parse.parse_qs(next(x[3] for x in self.transport.calls if x[1].endswith('/oauth/token')).decode())
        self.assertEqual(['oaiapp_mock'],form['client_id']);self.assertEqual([q['redirect_uri']],form['redirect_uri']);self.assertNotIn('client_secret',form)
        self.assertFalse(self.p.cloud_enabled)

    def test_state_mismatch(self):
        q,callback=self.flow();callback['state']='wrong';before=len(self.transport.calls)
        self.fails('invalid_or_expired_oauth_state',lambda:self.p.callback(callback));self.assertEqual(before,len(self.transport.calls))

    def test_state_single_use(self):
        q,callback=self.flow();self.p.callback(callback)
        self.fails('invalid_or_expired_oauth_state',lambda:self.p.callback(callback))

    def test_state_expiry(self):
        q,callback=self.flow();self.p.pending[q['state']]['expires']=time.time()-1
        self.fails('invalid_or_expired_oauth_state',lambda:self.p.callback(callback))

    def test_cancel_does_not_exchange(self):
        q,callback=self.flow();before=len(self.transport.calls)
        self.fails('authorization_cancelled',lambda:self.p.callback({'state':q['state'],'error':'access_denied'}))
        self.assertEqual(before,len(self.transport.calls));self.assertFalse(self.p.account())

    def test_callback_requires_issued_client(self):
        q,callback=self.flow();callback.pop('client_id')
        self.fails('issued_client_id_missing',lambda:self.p.callback(callback))

    def test_id_signature_rejection(self):
        q,callback=self.flow();reply=self.transport.codes[callback['code']]
        claims={'iss':s.ISSUER,'aud':'oaiapp_mock','sub':'mock-subject','iat':int(time.time()),'exp':int(time.time())+3600,'nonce':q['nonce']}
        other=rsa.generate_private_key(public_exponent=65537,key_size=2048)
        reply['id_token']=s.jwt.encode(claims,other,algorithm='RS256',headers={'kid':'mock-key'})
        self.fails('invalid_id_token',lambda:self.p.callback(callback));self.assertIsNone(self.p.account())

    def test_id_audience_rejection(self):
        q,callback=self.flow(aud='wrong-client');self.fails('invalid_id_token',lambda:self.p.callback(callback))

    def test_id_issuer_rejection(self):
        q,callback=self.flow(iss='https://untrusted.invalid');self.fails('invalid_id_token',lambda:self.p.callback(callback))

    def test_id_expiry_rejection(self):
        q,callback=self.flow(exp=int(time.time())-120);self.fails('invalid_id_token',lambda:self.p.callback(callback))

    def test_id_nonce_rejection(self):
        q,callback=self.flow();reply=self.transport.codes[callback['code']]
        reply.update(self.transport.token('oaiapp_mock','wrong'))
        self.fails('id_token_nonce_mismatch',lambda:self.p.callback(callback))

    def test_id_algorithm_confusion_rejection(self):
        q,callback=self.flow();self.transport.codes[callback['code']]['id_token']=s.jwt.encode({'sub':'x'},'mock-secret-at-least-32-characters-long',algorithm='HS256',headers={'kid':'mock-key'})
        self.fails('invalid_id_token_algorithm',lambda:self.p.callback(callback))

    def test_returning_client_and_subject_binding(self):
        account=self.login();q,callback=self.flow(account,subject='other-subject')
        self.assertEqual('oaiapp_mock',q['client_id']);self.assertNotIn('agent_name_hint',q);self.assertIn('id_token_hint',q)
        self.fails('returning_account_identity_mismatch',lambda:self.p.callback(callback))
        self.assertEqual('mock-subject',self.p.account()['subject'])

    def test_returning_callback_cannot_replace_client(self):
        account=self.login();q,callback=self.flow(account);callback['client_id']='oaiapp_other'
        self.fails('callback_client_id_mismatch',lambda:self.p.callback(callback))

    def test_returning_callback_client_may_be_absent(self):
        account=self.login();q,callback=self.flow(account);callback.pop('client_id');self.p.callback(callback)
        self.assertEqual(account,self.store.data['active'])

    def test_same_email_registrations_isolated(self):
        first=self.login();second=self.login(client='oaiapp_other')
        self.assertNotEqual(first,second);self.assertEqual(2,len(self.store.data['accounts']))
        self.assertNotEqual(self.store.data['accounts'][first]['label'],self.store.data['accounts'][second]['label'])

    def test_missing_plan_scope_retains_login_but_disables_cloud(self):
        self.login(scope='openid email profile');self.assertTrue(self.p.status()['connected']);self.assertFalse(self.p.status()['plan_permission'])
        self.fails('chatgpt_plan_permission_required',lambda:self.p.session({'cloud_enabled':True}))

    def test_private_storage_and_descriptor_no_provider_tokens(self):
        self.login()
        for name in ('accounts.json','connection.json'):self.assertEqual(0o600,stat.S_IMODE((self.store.directory/name).stat().st_mode))
        descriptor=json.loads((self.store.directory/'connection.json').read_text())
        self.assertEqual(1,descriptor['schema']);self.assertNotIn('FAKE_ACCESS_PRIVATE',json.dumps(descriptor));self.assertNotIn('FAKE_REFRESH_PRIVATE',json.dumps(descriptor))
        self.assertNotEqual(self.p.capability,self.p.bootstrap);self.assertEqual(0o700,stat.S_IMODE(self.store.directory.stat().st_mode))

    def test_store_lock_prevents_refresh_process_race(self):
        self.fails('another_provider_process_is_running',lambda:s.PrivateStore(self.store.directory))

    def test_symlink_private_file_rejected(self):
        target=self.store.directory/'unexpected';target.symlink_to('/dev/null')
        self.fails('unsafe_private_file_permissions',lambda:self.store.write('unexpected',{}))

    def test_refresh_rotates_atomically_without_scope(self):
        account=self.login();old=self.p.account();old['expires_at']=0
        self.transport.refresh_reply={**self.transport.token('oaiapp_mock','refresh'),'access_token':'FAKE_ACCESS_ROTATED','refresh_token':'FAKE_REFRESH_ROTATED'}
        self.assertEqual('FAKE_ACCESS_ROTATED',self.p.access(account));saved=json.loads((self.store.directory/'accounts.json').read_text())['accounts'][account]
        self.assertEqual('FAKE_REFRESH_ROTATED',saved['refresh_token'])
        form=urllib.parse.parse_qs([x[3] for x in self.transport.calls if x[1].endswith('/oauth/token')][-1].decode())
        self.assertNotIn('scope',form);self.assertEqual([s.RESOURCE],form['resource']);self.assertEqual(['oaiapp_mock'],form['client_id'])

    def test_refresh_serialized_threads(self):
        account=self.login();self.p.account()['expires_at']=0
        self.transport.refresh_reply=self.transport.token('oaiapp_mock','refresh');result=[]
        threads=[threading.Thread(target=lambda:result.append(self.p.access(account))) for _ in range(4)]
        for thread in threads:thread.start()
        for thread in threads:thread.join()
        self.assertEqual(4,len(result))
        refresh_calls=[x for x in self.transport.calls if x[3] and b'grant_type=refresh_token' in x[3]];self.assertEqual(1,len(refresh_calls))

    def test_terminal_refresh_clears_only_tokens(self):
        account=self.login();self.p.account()['expires_at']=0;self.transport.refresh_error=s.Failure('invalid_grant',400,400)
        self.fails('invalid_grant',lambda:self.p.access(account));self.assertFalse(self.p.status()['connected'])
        self.assertEqual('oaiapp_mock',self.store.data['accounts'][account]['client_id'])

    def test_temporary_refresh_keeps_credentials(self):
        account=self.login();self.p.account()['expires_at']=0;self.transport.refresh_error=s.Failure('provider_network_unavailable',503)
        self.fails('provider_network_unavailable',lambda:self.p.access(account));self.assertTrue(self.p.account().get('refresh_token'))

    def test_refresh_cannot_resurrect_logout(self):
        account=self.login();self.p.account()['expires_at']=0
        self.transport.refresh_reply=self.transport.token('oaiapp_mock','refresh');self.transport.refresh_hook=lambda:self.p.logout(account)
        self.fails('session_changed_during_refresh',lambda:self.p.access(account));self.assertFalse(self.p.account().get('access_token'))

    def test_late_terminal_refresh_cannot_clear_new_authorization(self):
        account=self.login();self.p.account()['expires_at']=0
        def reauthorize():
            q,callback=self.flow(account);self.transport.codes[callback['code']].update(access_token='FAKE_NEW_ACCESS',refresh_token='FAKE_NEW_REFRESH')
            self.p.callback(callback)
        self.transport.refresh_hook=reauthorize;self.transport.refresh_error=s.Failure('invalid_grant',400,400)
        self.fails('session_changed_during_refresh',lambda:self.p.access(account))
        self.assertEqual('FAKE_NEW_REFRESH',self.p.account()['refresh_token']);self.assertTrue(self.p.status()['connected'])

    def test_late_logout_cannot_clear_new_authorization(self):
        account=self.login()
        def reauthorize():
            q,callback=self.flow(account);self.transport.codes[callback['code']].update(access_token='FAKE_NEW_ACCESS',refresh_token='FAKE_NEW_REFRESH')
            self.p.callback(callback)
        self.transport.revoke_hook=reauthorize;result=self.p.logout()
        self.assertTrue(result['remote_revocation_confirmed']);self.assertFalse(result['local_session_cleared'])
        self.assertEqual('FAKE_NEW_REFRESH',self.p.account()['refresh_token']);self.assertTrue(self.p.status()['connected'])

    def test_logout_revokes_and_retains_registration(self):
        account=self.login();generation=self.p.generation;result=self.p.logout()
        self.assertTrue(result['remote_revocation_confirmed']);self.assertNotEqual(generation,self.p.generation)
        self.assertFalse(self.p.account().get('refresh_token'));self.assertEqual('oaiapp_mock',self.p.account()['client_id'])
        form=urllib.parse.parse_qs([x[3] for x in self.transport.calls if x[1].endswith('/oauth/revoke')][-1].decode())
        self.assertEqual(['refresh_token'],form['token_type_hint'])

    def test_logout_reports_unconfirmed_remote_revocation(self):
        self.login();self.transport.revoke_error=s.Failure('provider_network_unavailable',503)
        result=self.p.logout();self.assertFalse(result['remote_revocation_confirmed']);self.assertFalse(self.p.account().get('access_token'))
        self.assertEqual('remote_revocation_not_confirmed',result['error'])

    def test_model_catalog_visibility_and_account_order(self):
        self.login();self.p.session({'cloud_enabled':True});result=self.p.models()
        self.assertEqual(['second','first'],[m['slug'] for m in result['models']])
        self.fails('model_not_in_selected_account_catalog',lambda:self.p.session({'model':'hidden'}))

    def test_bridge_completed_responses_protocol(self):
        self.enable();result=self.p.chat(self.body(),self.p.generation)
        self.assertEqual('{"steps":[]}',result['choices'][0]['message']['content']);self.assertEqual(12,result['usage']['total_tokens'])
        call=[x for x in self.transport.calls if x[1].endswith('/responses')][-1];body=json.loads(call[3])
        self.assertFalse(body['store']);self.assertTrue(body['stream']);self.assertNotIn('api_key',body);self.assertNotIn('prompt',body)
        self.assertEqual(8,self.p.status()['usage']['input_tokens'])

    def test_exact_csharp_fields_seed_and_json_format(self):
        self.enable();body=self.body(temperature=.7,seed=20261013,response_format={'type':'json_object'})
        result=self.p.chat(body,self.p.generation)
        upstream=json.loads([x[3] for x in self.transport.calls if x[1].endswith('/responses')][-1])
        self.assertNotIn('seed',upstream);self.assertNotIn('response_format',upstream);self.assertNotIn('temperature',upstream)
        self.assertEqual(body['messages'],upstream['input']);self.assertEqual(20261013,result['chooguard']['requested_seed'])
        self.fails('invalid_source_seed',lambda:self.p.chat(self.body(seed=-1),self.p.generation))
        self.fails('unsupported_response_format',lambda:self.p.chat(self.body(response_format={'type':'json_schema'}),self.p.generation))

    def test_model_switch_during_refresh_stops_before_inference(self):
        self.enable();self.p.account()['expires_at']=0;generation=self.p.generation
        self.transport.refresh_reply=self.transport.token('oaiapp_mock','refresh')
        self.transport.refresh_hook=lambda:self.p.session({'model':'first'})
        self.fails('provider_session_changed',lambda:self.p.chat(self.body(),generation))
        self.assertFalse(any(x[1].endswith('/responses') for x in self.transport.calls))

    def test_json_format_requires_completed_json_object(self):
        self.enable();self.transport.stream=events([{'type':'response.completed','response':{'status':'completed',
            'output':[{'type':'message','content':[{'type':'output_text','text':'plain prose'}]}],
            'usage':{'input_tokens':8,'output_tokens':4}}}])
        self.fails('provider_json_object_missing',lambda:self.p.chat(self.body(response_format={'type':'json_object'}),self.p.generation))
        self.assertEqual(8,self.p.usage['input_tokens']);self.assertEqual(0,self.p.failed_reserved['input_tokens'])

    def test_late_quota_failure_discards_partial_and_stops(self):
        self.enable();self.transport.stream=events([{'type':'response.output_text.delta','delta':'partial secret plan'},
            {'type':'response.failed','response':{'error':{'code':'subscription_sharing_usage_limit_exceeded'}}}])
        self.fails('subscription_sharing_usage_limit_exceeded',lambda:self.p.chat(self.body(),self.p.generation))
        before=len(self.transport.calls);self.assertTrue(self.p.status()['quota_stopped']);self.assertFalse(self.p.cloud_enabled)
        self.fails('subscription_usage_paused',lambda:self.p.chat(self.body(),self.p.generation));self.assertEqual(before,len(self.transport.calls))
        self.assertEqual(0,self.p.status()['usage']['input_tokens']);self.assertGreater(self.p.status()['failed_reserved_tokens']['input_tokens'],0)

    def test_late_unavailable_is_not_success(self):
        self.enable();self.transport.stream=events([{'type':'response.failed','response':{'error':{'code':'subscription_sharing_usage_unavailable'}}}])
        self.fails('subscription_sharing_usage_unavailable',lambda:self.p.chat(self.body(),self.p.generation));self.assertFalse(self.p.status()['quota_stopped'])

    def test_incomplete_and_truncated_streams_rejected(self):
        self.enable()
        for stream,code in [(events([{'type':'response.output_text.delta','delta':'partial'}]),'provider_stream_truncated'),
                            (events([{'type':'response.incomplete','response':{}}]),'response_incomplete')]:
            self.transport.stream=stream;self.fails(code,lambda:self.p.chat(self.body(),self.p.generation))

    def test_http_401_preserves_credentials_and_stops_cloud(self):
        self.enable();self.transport.infer_error=s.upstream_failure(401,{'detail':'do not echo sensitive remote text'},'mock-request')
        self.fails('upstream_admission_error',lambda:self.p.chat(self.body(),self.p.generation))
        self.assertFalse(self.p.cloud_enabled);self.assertTrue(self.p.account().get('refresh_token'))

    def test_structured_error_metadata_without_remote_diagnostic_text(self):
        failure=s.upstream_failure(400,{'error':{'code':'subscription_sharing_unsupported_capability','param':'input[0].type','message':'sensitive-remote-diagnostic'}},'mock-request-id')
        value=failure.public()['error'];self.assertEqual(400,value['upstream_status']);self.assertEqual('input[0].type',value['param'])
        self.assertEqual('error_object',value['body_shape']);self.assertEqual('mock-request-id',value['request_id'])
        self.assertNotIn('sensitive-remote-diagnostic',json.dumps(value))

    def test_temporary_provider_backoff_stops_immediate_network_retry(self):
        self.enable();self.transport.infer_error=s.Failure('subscription_sharing_usage_unavailable',503)
        self.fails('subscription_sharing_usage_unavailable',lambda:self.p.chat(self.body(),self.p.generation));before=len(self.transport.calls)
        self.fails('provider_retry_later',lambda:self.p.chat(self.body(),self.p.generation));self.assertEqual(before,len(self.transport.calls))
        self.assertTrue(self.p.account().get('refresh_token'));self.assertGreater(self.p.status()['retry_after_seconds'],0)

    def test_http_429_no_billing_or_account_bypass(self):
        first=self.login();second=self.login(client='oaiapp_other');self.p.session({'cloud_enabled':True});self.p.models();self.p.session({'model':'second'})
        self.transport.infer_error=s.upstream_failure(429,{'error':{'code':'subscription_sharing_usage_limit_exceeded'}})
        self.fails('subscription_sharing_usage_limit_exceeded',lambda:self.p.chat(self.body(),self.p.generation))
        self.fails('subscription_usage_paused',lambda:self.p.session({'account_id':first,'cloud_enabled':True}))
        self.fails('usage_review_must_match_stopped_account',lambda:self.p.session({'account_id':first,'resume_after_usage_review':True}))
        self.assertEqual(second,self.store.data['active'])

    def test_generation_change_rejects_before_network(self):
        self.enable();old=self.p.generation;self.p.session({'model':'first'});before=len(self.transport.calls)
        self.fails('provider_session_changed',lambda:self.p.chat(self.body(model='first'),old));self.assertEqual(before,len(self.transport.calls))
        newer=self.p.generation;self.p.session({'cloud_enabled':False});self.assertNotEqual(newer,self.p.generation)

    def test_local_budget_exhaustion_does_not_negative_reservation(self):
        self.enable();self.p.session({'limits':{'requests':1,'input_tokens':1000,'output_tokens':100}})
        self.p.chat(self.body(),self.p.generation);before=len(self.transport.calls)
        self.fails('local_session_budget_exceeded',lambda:self.p.chat(self.body(),self.p.generation))
        self.assertEqual(before,len(self.transport.calls));self.assertEqual({'requests':0,'input_tokens':0,'output_tokens':0},self.p.reserved)

    def test_initial_wholeworld_requires_explicit_separate_allowance(self):
        self.enable();body=self.body(chooguard_purpose='initial_world',max_tokens=1024,messages=[{'role':'user','content':'x'*30000}])
        self.fails('initial_world_allowance_not_enabled',lambda:self.p.chat(body,self.p.generation))
        self.p.session({'initial_world_allowance':{'input_tokens':128000,'output_tokens':2048}})
        self.p.chat(body,self.p.generation);self.assertEqual(0,self.p.budget_used['requests']);self.assertEqual(1,self.p.initial_budget_used['requests'])
        self.fails('initial_world_allowance_not_enabled',lambda:self.p.chat(body,self.p.generation))

    def test_initial_allowance_requires_selected_model_and_follows_session(self):
        self.login();self.p.session({'cloud_enabled':True})
        before=len(self.transport.calls)
        allowance={'input_tokens':192000,'output_tokens':512}
        self.fails('select_account_model_first',lambda:self.p.session({'initial_world_allowance':allowance}))
        self.assertEqual(before,len(self.transport.calls));self.assertFalse(self.p.initial_remaining)
        self.p.models();self.p.session({'model':'second','initial_world_allowance':allowance})
        self.assertTrue(self.p.initial_remaining)
        self.p.session({'model':'first'});self.assertFalse(self.p.initial_remaining)
        self.p.session({'initial_world_allowance':allowance});self.assertTrue(self.p.initial_remaining)
        self.login();self.assertFalse(self.p.initial_remaining)
        self.assertFalse(self.p.cloud_enabled)

    def test_live_station_initial_allowance_is_bounded_and_single_use(self):
        self.enable();ordinary_limits=dict(self.p.limits)
        self.fails('invalid_initial_allowance',lambda:self.p.session({'initial_world_allowance':{'input_tokens':s.MAX_INITIAL_INPUT_UNITS+1,'output_tokens':512}}))
        self.p.session({'initial_world_allowance':{'input_tokens':s.MAX_INITIAL_INPUT_UNITS,'output_tokens':512}})
        body=self.body(chooguard_purpose='initial_world',max_tokens=512,messages=[{'role':'user','content':'x'*766419}])
        self.p.chat(body,self.p.generation)
        self.assertEqual(1,self.p.initial_budget_used['requests'])
        self.assertEqual(0,self.p.budget_used['requests']);self.assertEqual(ordinary_limits,self.p.limits)
        before=len(self.transport.calls)
        self.fails('initial_world_allowance_not_enabled',lambda:self.p.chat(body,self.p.generation))
        self.assertEqual(before,len(self.transport.calls))

    def test_initial_allowance_explicit_arm_after_cloud_rotation(self):
        self.enable();self.p.session({'cloud_enabled':False})
        self.p.session({'cloud_enabled':True,'initial_world_allowance':{'input_tokens':192000,'output_tokens':512}})
        self.assertTrue(self.p.initial_remaining)
        self.p.session({'cloud_enabled':False});self.assertFalse(self.p.initial_remaining)

    def test_completed_provider_model_is_distinct_from_selected_slug(self):
        self.enable()
        self.transport.stream=events([{'type':'response.completed','response':{'status':'completed','model':'second-2026-10-14',
            'output':[{'type':'message','content':[{'type':'output_text','text':'{"steps":[]}'}]}],
            'usage':{'input_tokens':8,'output_tokens':4}}}])
        reply=self.p.chat(self.body(),self.p.generation)
        self.assertEqual('second',reply['model'])
        self.assertEqual('second-2026-10-14',reply['chooguard']['provider_model'])

    def test_request_not_silently_truncated_or_key_fallback(self):
        self.enable();self.fails('unsupported_chat_request_field',lambda:self.p.chat(self.body(api_key='never-used'),self.p.generation))
        self.fails('local_input_budget_exceeded',lambda:self.p.chat(self.body(messages=[{'role':'user','content':'x'*13000}]),self.p.generation))
        self.assertFalse(any(x[1].endswith('/responses') for x in self.transport.calls))


class HttpTests(AuthTests):
    # Only the methods declared below are loaded by runner; inherited tests run once in AuthTests.
    def setUp(self):
        super().setUp();self.server=s.Server(('127.0.0.1',0),self.p)
        self.thread=threading.Thread(target=self.server.serve_forever,daemon=True);self.thread.start()

    def tearDown(self):
        self.server.shutdown();self.server.server_close();self.thread.join();super().tearDown()

    def request(self,method,path,value=None,headers=None,raw=None):
        connection=http.client.HTTPConnection('127.0.0.1',self.server.server_port,timeout=5)
        if raw is not None:body=raw
        else:body=json.dumps(value).encode() if value is not None else None
        h={'Content-Type':'application/json',**(headers or {})}
        connection.request(method,path,body=body,headers=h);response=connection.getresponse();data=response.read();status=response.status;fields=dict(response.getheaders());connection.close()
        return status,json.loads(data) if fields.get('Content-Type','').startswith('application/json') else data.decode(),fields

    def native(self):return {'Authorization':'Bearer '+self.p.capability,'X-ChooGuard-Session':self.p.generation}

    def test_http_complete_live_station_payload_passes_without_truncation(self):
        self.enable();self.p.session({'initial_world_allowance':{'input_tokens':s.MAX_INITIAL_INPUT_UNITS,'output_tokens':512}})
        content='x'*766419
        body=self.body(chooguard_purpose='initial_world',max_tokens=512,messages=[{'role':'user','content':content}])
        status,result,_=self.request('POST','/v1/chat/completions',body,self.native())
        self.assertEqual(200,status,result)
        sent=json.loads(next(call[3] for call in self.transport.calls if call[1]==s.RESOURCE+'/responses'))
        self.assertEqual(content,sent['input'][0]['content'])
        self.assertEqual(413,self.request('POST','/session',raw=b' '*524289,headers={'Cookie':'chooguard_ui='+self.p.ui_cookie,'Origin':self.p.endpoint})[0])

    def test_http_capability_host_origin_json_and_generation(self):
        self.assertEqual(401,self.request('GET','/status')[0]);self.assertEqual(200,self.request('GET','/status',headers=self.native())[0])
        self.assertEqual(403,self.request('GET','/status',headers={**self.native(),'Host':'evil.invalid'})[0])
        self.assertEqual(403,self.request('GET','/status',headers={**self.native(),'Origin':'https://evil.invalid'})[0])
        self.assertEqual(400,self.request('POST','/v1/chat/completions',headers=self.native(),raw=b'{broken')[0])
        self.enable();headers=self.native();result=self.request('POST','/v1/chat/completions',self.body(),headers)
        self.assertEqual(200,result[0]);self.assertEqual('{"steps":[]}',result[1]['choices'][0]['message']['content'])
        self.p.session({'model':'first'});before=len(self.transport.calls)
        self.assertEqual(409,self.request('POST','/v1/chat/completions',self.body(model='first'),headers)[0]);self.assertEqual(before,len(self.transport.calls))
        self.assertNotIn('FAKE_ACCESS_PRIVATE',json.dumps(result));self.assertNotIn('FAKE_REFRESH_PRIVATE',json.dumps(result))

    def test_http_browser_bootstrap_cookie_and_ui_only_mutations(self):
        origin={'Origin':self.p.endpoint};status,value,headers=self.request('POST','/ui/bootstrap',{'bootstrap':self.p.bootstrap},origin)
        self.assertEqual(200,status);cookie=headers['Set-Cookie'];self.assertIn('HttpOnly',cookie);self.assertIn('SameSite=Strict',cookie)
        self.assertEqual(401,self.request('POST','/ui/bootstrap',{'bootstrap':self.p.bootstrap},origin)[0])
        self.assertEqual(200,self.request('POST','/ui/bootstrap',{'bootstrap':self.p.bootstrap},{**origin,'Cookie':cookie.split(';')[0]})[0])
        self.assertEqual(401,self.request('POST','/auth/start',{},self.native())[0])
        status,result,_=self.request('POST','/auth/start',{}, {**origin,'Cookie':cookie.split(';')[0]})
        self.assertEqual(200,status);self.assertIn('dynamic_agent_client',result['authorization_url'])
        self.assertEqual(403,self.request('POST','/session',{'cloud_enabled':True},{'Cookie':cookie.split(';')[0]})[0])

    def test_http_cross_site_callback_is_state_guarded(self):
        q,callback=self.flow();path='/auth/callback?'+urllib.parse.urlencode(callback)
        status,_,headers=self.request('GET',path,headers={'Sec-Fetch-Site':'cross-site'})
        self.assertEqual(303,status);self.assertEqual('/',headers['Location']);self.assertTrue(self.p.status()['connected'])
        self.assertEqual(400,self.request('GET',path,headers={'Sec-Fetch-Site':'cross-site'})[0])

    def test_http_page_plain_status_csp_and_valid_script(self):
        status,html,headers=self.request('GET','/')
        self.assertEqual(200,status);self.assertNotIn('JSON.stringify(current',html)
        self.assertIn('Reported usage:',html);self.assertIn('Ordinary session remaining:',html)
        self.assertNotIn(self.p.capability,html);self.assertNotIn(self.p.bootstrap,html)
        self.assertIn("frame-ancestors 'none'",headers['Content-Security-Policy'])
        node=shutil.which('node')
        if node:
            script=re.search(r'<script nonce="[^"]+">(.*?)</script>',html,re.S).group(1)
            result=subprocess.run([node,'--check'],input=script,text=True,capture_output=True)
            self.assertEqual(0,result.returncode,result.stderr)
        self.assertEqual([],self.transport.calls)

    def test_http_old_cookie_rejected_then_stable_app_cookie_survives_restart(self):
        # Reproduce the previous implementation's exact failure before proving the persistence fix.
        old_cookie=self.p.ui_cookie
        self.p.ui_cookie='old-process-invalidated-this-cookie'
        before=self.p.status()
        status,reply,_=self.request('POST','/session',{'cloud_enabled':True},headers={
            'Cookie':'chooguard_ui='+old_cookie,'Origin':self.p.endpoint})
        self.assertEqual(401,status);self.assertEqual('browser_session_required',reply['error']['code'])
        self.assertEqual(before['cloud_enabled'],self.p.cloud_enabled)
        self.assertEqual('/session',self.p.status()['browser_diagnostics']['last']['route'])
        self.assertEqual(401,self.p.status()['browser_diagnostics']['last']['http_status'])
        # Simulated clean process restart reloads exactly the app-owned browser capability.
        replacement=s.Provider(self.store,self.transport)
        self.assertEqual(old_cookie,replacement.ui_cookie)
        self.server.provider=replacement;self.p=replacement;self.p.configure(self.server.server_port)
        status,reply,_=self.request('GET','/status',headers={'Cookie':'chooguard_ui='+old_cookie})
        self.assertEqual(200,status)
        self.assertEqual([],self.transport.calls)
        private=self.store.directory/'browser-session.json'
        self.assertEqual(0o600,stat.S_IMODE(private.stat().st_mode))
        descriptor=json.loads((self.store.directory/'connection.json').read_text())
        self.assertNotIn(old_cookie,json.dumps(descriptor))

    def test_http_server_confirms_browser_choices_and_redacted_action_metadata(self):
        self.login();cookie=self.p.ui_cookie
        headers={'Cookie':'chooguard_ui='+cookie,'Origin':self.p.endpoint}
        self.assertEqual(200,self.request('POST','/session',{'cloud_enabled':True},headers=headers)[0])
        self.assertEqual(200,self.request('GET','/models',headers={'Cookie':'chooguard_ui='+cookie})[0])
        self.assertEqual(200,self.request('POST','/session',{'model':'second'},headers=headers)[0])
        status,reply,_=self.request('POST','/session',{'initial_world_allowance':{'input_tokens':192000,'output_tokens':512}},headers=headers)
        self.assertEqual(200,status);self.assertTrue(reply['cloud_enabled']);self.assertEqual('second',reply['model']);self.assertEqual(1,reply['initial_world']['remaining'])
        diagnostic=self.p.status()['browser_diagnostics']
        self.assertEqual(4,diagnostic['successes']);self.assertEqual(0,diagnostic['failures'])
        self.assertEqual({'method','route','http_status','code','at','setting_names'},set(diagnostic['last']))
        self.assertEqual(['initial_world_allowance'],diagnostic['last_action']['setting_names'])
        for value in (cookie,self.p.capability,self.p.bootstrap,'mock@example.invalid','FAKE_ACCESS_PRIVATE'):
            self.assertNotIn(value,json.dumps(diagnostic))
        self.assertEqual(0,self.p.usage['requests'])


def run():
    suite=unittest.TestSuite()
    suite.addTests(unittest.defaultTestLoader.loadTestsFromTestCase(AuthTests))
    for name in sorted(HttpTests.__dict__):
        if name.startswith('test_'):suite.addTest(HttpTests(name))
    root=Path(__file__).resolve().parents[3];files=[Path(__file__),Path(s.__file__),Path(s.__file__).with_name('browser_ui.py')]
    hashes={str(path.relative_to(root)):hashlib.sha256(path.read_bytes()).hexdigest() for path in files}
    stream=io.StringIO();began=time.monotonic();result=unittest.TextTestRunner(stream=stream,verbosity=2).run(suite)
    report={'exit_code':0 if result.wasSuccessful() else 1,'test_count':result.testsRun,'elapsed_seconds':round(time.monotonic()-began,4),
            'source_hashes':hashes,'source_changed_during_run':any(hashlib.sha256(path.read_bytes()).hexdigest()!=hashes[str(path.relative_to(root))] for path in files),
            'live_provider_login':False,'live_provider_inference':False,'existing_cli_credentials_read':False,
            'scenarios':[name for name in sorted(AuthTests.__dict__) if name.startswith('test_')]+[name for name in sorted(HttpTests.__dict__) if name.startswith('test_')],
            'stdout':stream.getvalue()}
    Path(__file__).with_name('test-report.json').write_text(json.dumps(report,indent=2)+'\n')
    print(stream.getvalue(),end='');return report['exit_code']


if __name__=='__main__':raise SystemExit(run())
