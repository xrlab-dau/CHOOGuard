"""Actual localhost native-control HTTP with synthetic provider accounts/transports."""
from dataclasses import replace
import http.client
import json
from pathlib import Path
import tempfile
import threading
import time
import unittest
import urllib.parse
from provider_registry import OPENAI,Registry
from multi_provider import MultiProvider
from copilot_adapter import CopilotProvider,contract as copilot_contract
from test_sidecar import MockTransport
from test_copilot_adapter import DeviceTransport
import sidecar as s


class NativeControlTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix='chooguard-native-control-')
        self.store=s.PrivateStore(Path(self.temp.name)/'private')
        self.a=MockTransport();self.b=MockTransport();self.device=DeviceTransport();self.now=time.time()
        self.registry=Registry([OPENAI,replace(OPENAI,id='mock-beta',label='Mock B'),copilot_contract('OWN_APP_PUBLIC_CLIENT')])
        class Runtime:
            def models(self,token):return [{'slug':'second','display_name':'Second','selectable':True}]
        self.p=MultiProvider(self.store,transports={'openai-chatgpt':self.a,'mock-beta':self.b,'github-copilot':self.device},
            registry=self.registry,clock=lambda:self.now,
            adapter_factories={'github-copilot':lambda *a,**kw:CopilotProvider(*a,runtime=Runtime(),**kw)})
        self.server=s.Server(('127.0.0.1',0),self.p)
        self.thread=threading.Thread(target=self.server.serve_forever,daemon=True);self.thread.start()
    def tearDown(self):
        self.server.shutdown();self.server.server_close();self.thread.join(timeout=2)
        self.store.close();self.temp.cleanup()

    def request(self,method,path,body=None,headers=None,native=True,generation=True):
        request_headers={'Authorization':'Bearer '+self.p.capability} if native else {}
        if generation:request_headers['X-ChooGuard-Session']=self.p.generation
        raw=None
        if body is not None:raw=json.dumps(body).encode();request_headers['Content-Type']='application/json'
        request_headers.update(headers or {})
        client=http.client.HTTPConnection('127.0.0.1',self.server.server_port,timeout=3)
        try:
            client.request(method,path,body=raw,headers=request_headers);reply=client.getresponse();data=reply.read()
            return reply.status,json.loads(data) if reply.getheader('Content-Type','').startswith('application/json') else data.decode()
        finally:client.close()

    def native(self,method,path,body=None,**kwargs):
        status,value=self.request(method,'/native/v1'+path,body,**kwargs)
        self.assertEqual(200,status,value);self.assertEqual('native-provider-control-v1',value['schema']);return value

    def start(self,provider='openai-chatgpt',**kwargs):return self.native('POST','/auth/start',{'provider_id':provider,**kwargs})['flow']

    def complete(self,flow,transport=None,client='OWN_MOCK_PUBLIC'):
        transport=transport or self.a
        query={k:v[0] for k,v in urllib.parse.parse_qs(urllib.parse.urlsplit(flow['authorization_url']).query).items()}
        code='code-'+str(len(transport.codes));transport.codes[code]=transport.token(client,query['nonce'])
        callback='/auth/callback?'+urllib.parse.urlencode({'state':query['state'],'code':code,'client_id':client})
        status,page=self.request('GET',callback,native=False,generation=False)
        self.assertEqual(200,status);self.assertIn('게임으로 돌아가',page);self.assertNotIn('id="provider"',page)
        return self.native('GET','/auth/flows/'+flow['flow_id'])['flow']['account_id']

    def ready(self,provider='openai-chatgpt',transport=None):
        account=self.complete(self.start(provider),transport=transport,client='OWN_'+provider)
        catalog=self.native('POST','/catalog',{})
        row=next(m for m in catalog['models'] if m['provider_id']==provider and m['model_id']=='second')
        return self.native('POST','/selection',{k:row[k] for k in ('provider_id','account_id','model_id','catalog_version')}),account

    def test_status_is_cached_readonly_and_tokens_never_returned(self):
        value=self.native('GET','/status',generation=False)
        self.assertEqual([],value['models']);self.assertIsNone(value['status']['selection'])
        self.assertEqual([],self.a.calls+self.b.calls+self.device.calls)
        flow=self.start();text=json.dumps(self.native('GET','/status'))
        pending=next(iter(self.p.adapters['openai-chatgpt'].pending.values()))
        self.assertNotIn(pending['verifier'],text);self.assertNotIn('oauth_state',text)
        self.assertNotIn('device_code',json.dumps(flow));self.assertEqual('waiting_for_browser',flow['status'])

    def test_cookie_and_bad_native_capability_cannot_control(self):
        for method,path,body in [('GET','/status',None),('POST','/auth/start',{'provider_id':'openai-chatgpt'}),('DELETE','/selection',{})]:
            status,error=self.request(method,'/native/v1'+path,body,native=False,headers={'Cookie':'chooguard_ui='+self.p.ui_cookie})
            self.assertEqual(401,status);self.assertEqual('local_capability_required',error['error']['code'])
        self.assertEqual([],self.a.calls+self.b.calls)

    def test_host_origin_cross_site_and_nonloopback_boundary(self):
        for headers in [{'Host':'localhost:'+str(self.server.server_port)}, {'Origin':'https://external.invalid'}, {'Sec-Fetch-Site':'cross-site'}]:
            status,error=self.request('GET','/native/v1/status',headers=headers)
            self.assertEqual(403,status)
        handler=s.Handler.__new__(s.Handler);handler.client_address=('192.0.2.8',123);handler.server=self.server;handler.headers={};handler.command='GET';handler.path='/native/v1/status'
        with self.assertRaises(s.Failure) as e:handler.boundary()
        self.assertEqual('loopback_host_required',e.exception.code)

    def test_mutation_requires_current_generation_before_provider_request(self):
        for headers,generation in [({},False),({'X-ChooGuard-Session':'old-generation'},True)]:
            status,error=self.request('POST','/native/v1/auth/start',{'provider_id':'openai-chatgpt'},headers=headers,generation=generation)
            self.assertEqual(409,status)
        self.assertEqual([],self.a.calls)

    def test_two_provider_login_union_single_selection_and_explicit_consent(self):
        a=self.complete(self.start());b=self.complete(self.start('mock-beta'),transport=self.b,client='OWN_B')
        result=self.native('POST','/catalog',{})
        twins=[m for m in result['models'] if m['model_id']=='second'];self.assertEqual({a,b},{m['account_id'] for m in twins})
        row=next(m for m in twins if m['provider_id']=='mock-beta')
        selected=self.native('POST','/selection',{k:row[k] for k in ('provider_id','account_id','model_id','catalog_version')})
        self.assertEqual('mock-beta',selected['status']['selection']['provider_id']);self.assertFalse(selected['status']['cloud_enabled'])
        consent=self.native('POST','/consent',{'subscription_enabled':True})
        self.assertTrue(consent['status']['cloud_enabled']);self.assertEqual(0,consent['status']['initial_world']['remaining'])
        armed=self.native('POST','/consent',{'subscription_enabled':True,'initial_world_allowance':{'input_tokens':1000,'output_tokens':128}})
        self.assertEqual(1,armed['status']['initial_world']['remaining'])
        self.assertFalse(any(c[1].endswith('/responses') for c in self.a.calls+self.b.calls))

    def test_status_and_catalog_do_not_consume_allowance_or_infer(self):
        self.ready();self.native('POST','/consent',{'subscription_enabled':True,'initial_world_allowance':{'input_tokens':1000,'output_tokens':128}})
        for method,path,body in [('GET','/status',None),('POST','/catalog',{})]:
            value=self.native(method,path,body);self.assertEqual(1,value['status']['initial_world']['remaining']);self.assertEqual(0,value['status']['usage']['requests'])
        self.assertFalse(any(c[1].endswith('/responses') for c in self.a.calls))

    def test_catalog_cas_and_account_provider_mismatch_atomic(self):
        selected,account=self.ready();before=selected['status']['selection']
        invalid={'provider_id':'openai-chatgpt','account_id':account,'model_id':'second','catalog_version':'0'*64}
        status,error=self.request('POST','/native/v1/selection',invalid);self.assertEqual(409,status)
        invalid['provider_id']='mock-beta'
        status,error=self.request('POST','/native/v1/selection',invalid);self.assertEqual(400,status)
        self.assertEqual(before,self.p.selection())

    def test_clear_selection_and_nonselected_logout_preserve_other_logins(self):
        selected,a=self.ready();b=self.complete(self.start('mock-beta'),transport=self.b,client='OWN_B')
        self.native('POST','/consent',{'subscription_enabled':True,'initial_world_allowance':{'input_tokens':1000,'output_tokens':128}})
        before=dict(self.p.selection());value=self.native('POST','/logout',{'account_id':b})
        self.assertEqual(before,value['status']['selection']);self.assertTrue(value['status']['cloud_enabled']);self.assertEqual(1,value['status']['initial_world']['remaining'])
        value=self.native('DELETE','/selection',{})
        self.assertIsNone(value['status']['selection']);self.assertFalse(value['status']['cloud_enabled']);self.assertTrue(self.store.data['accounts'][a].get('access_token'))

    def test_opaque_flow_cancel_expiry_and_other_flow_preservation(self):
        first=self.start();second=self.start('mock-beta')
        self.assertNotEqual(first['flow_id'],urllib.parse.parse_qs(urllib.parse.urlsplit(first['authorization_url']).query)['state'][0])
        cancelled=self.native('DELETE','/auth/flows/'+first['flow_id'],{})['flow'];self.assertEqual('cancelled',cancelled['status'])
        self.assertEqual('waiting_for_browser',self.native('GET','/auth/flows/'+second['flow_id'])['flow']['status'])
        self.now+=301;self.assertEqual('expired',self.native('GET','/auth/flows/'+second['flow_id'])['flow']['status'])
        status,error=self.request('GET','/native/v1/auth/flows/'+'x'*43);self.assertEqual(404,status)

    def test_native_browser_callback_completed_and_no_settings_html(self):
        flow=self.start();account=self.complete(flow)
        value=self.native('GET','/auth/flows/'+flow['flow_id'])
        self.assertEqual('completed',value['flow']['status']);self.assertEqual(account,value['flow']['account_id'])
        self.assertNotIn('authorization_url',value['flow'])
        self.assertNotIn('FAKE_ACCESS_PRIVATE',json.dumps(value));self.assertNotIn('FAKE_REFRESH_PRIVATE',json.dumps(value))

    def test_cancelled_browser_callback_never_exchanges_or_shows_settings(self):
        flow=self.start();self.native('DELETE','/auth/flows/'+flow['flow_id'],{})
        query={k:v[0] for k,v in urllib.parse.parse_qs(urllib.parse.urlsplit(flow['authorization_url']).query).items()}
        before=len(self.a.calls)
        status,page=self.request('GET','/auth/callback?'+urllib.parse.urlencode({'state':query['state'],'code':'LATE_UNUSED','client_id':'OWN_UNUSED'}),native=False,generation=False)
        self.assertEqual(200,status);self.assertIn('게임으로 돌아가',page);self.assertNotIn('id="provider"',page)
        self.assertEqual(before,len(self.a.calls));self.assertEqual('cancelled',self.native('GET','/auth/flows/'+flow['flow_id'])['flow']['status'])

    def test_native_redirect_query_and_transfer_encoding_rejected(self):
        original=self.p.auth_start
        def tamper(*a,**kw):
            value=original(*a,**kw);url=urllib.parse.urlsplit(value['authorization_url'])
            query=urllib.parse.parse_qs(url.query);query['redirect_uri']=['https://external.invalid/callback']
            value['authorization_url']=urllib.parse.urlunsplit((url.scheme,url.netloc,url.path,urllib.parse.urlencode({k:v[0] for k,v in query.items()}),''));return value
        self.p.auth_start=tamper
        status,error=self.request('POST','/native/v1/auth/start',{'provider_id':'openai-chatgpt'})
        self.assertEqual(502,status);self.assertEqual('untrusted_provider_authorization_url',error['error']['code'])
        status,error=self.request('DELETE','/native/v1/selection',headers={'Transfer-Encoding':'chunked'})
        self.assertEqual(400,status);self.assertEqual('transfer_encoding_not_supported',error['error']['code'])

    def test_device_flow_only_user_code_uri_and_interval_polling(self):
        flow=self.start('github-copilot');self.assertEqual('waiting_for_device',flow['status']);self.assertEqual('https://github.com/login/device',flow['verification_uri'])
        self.assertNotIn('DEVICE_SECRET',json.dumps(flow));self.assertNotIn('state',flow);self.assertNotIn('authorization_url',flow)
        count=len(self.device.calls);self.native('GET','/auth/flows/'+flow['flow_id']);self.assertEqual(count,len(self.device.calls))
        self.now+=5;self.device.reply={'access_token':'OWN_DEVICE_FAKE','token_type':'bearer','scope':'read:user'}
        value=self.native('GET','/auth/flows/'+flow['flow_id'])
        self.assertEqual('completed',value['flow']['status']);self.assertNotIn('OWN_DEVICE_FAKE',json.dumps(value))
        self.assertFalse(value['status']['cloud_enabled']);self.assertIsNone(value['status']['selection'])

    def test_device_flow_cancel_during_poll_never_resurrects_tokens(self):
        flow=self.start('github-copilot');self.now+=5
        self.device.reply={'access_token':'OWN_DEVICE_FAKE','token_type':'bearer','scope':'read:user'}
        self.device.hook=lambda:self.server.native_control.flow(flow['flow_id'],self.p.generation,cancel=True)
        value=self.native('GET','/auth/flows/'+flow['flow_id'])
        self.assertEqual('cancelled',value['flow']['status']);self.assertEqual({},self.p.adapters['github-copilot'].store.data['accounts'])

    def test_url_validation_and_stale_auth_start_fail_closed(self):
        original=self.p.auth_start
        for bad in ['https://external.invalid/authorize','http://auth.openai.com/api/accounts/authorize','https://auth.openai.com/logout']:
            self.p.auth_start=lambda *a,**kw:{**original(*a,**kw),'authorization_url':bad}
            status,error=self.request('POST','/native/v1/auth/start',{'provider_id':'openai-chatgpt'})
            self.assertEqual(502,status);self.assertEqual('untrusted_provider_authorization_url',error['error']['code'])
            self.p.adapters['openai-chatgpt'].pending.clear()
        def stale(*a,**kw):
            value=original(*a,**kw);self.p.rotate();return value
        self.p.auth_start=stale
        status,error=self.request('POST','/native/v1/auth/start',{'provider_id':'openai-chatgpt'})
        self.assertEqual(409,status);self.assertEqual({},self.p.adapters['openai-chatgpt'].pending)

    def test_explicit_fields_no_api_key_or_default_provider_fallback(self):
        for body in [{},{'provider_id':'anthropic'},{'provider_id':'openai-chatgpt','api_key':'FORBIDDEN'}, {'provider_id':'openai-chatgpt','reconsent':'true'}]:
            status,error=self.request('POST','/native/v1/auth/start',body)
            self.assertGreaterEqual(status,400)
        self.assertEqual([],self.a.calls+self.b.calls+self.device.calls)
        status,error=self.request('POST','/native/v1/consent',{'subscription_enabled':True});self.assertEqual(403,status)


if __name__=='__main__':unittest.main()
