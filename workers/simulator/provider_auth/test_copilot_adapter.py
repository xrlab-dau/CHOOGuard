"""Official pinned SDK signatures + mock device/SDK flows, never live Copilot calls."""
import inspect
import json
from pathlib import Path
from types import SimpleNamespace as NS
import tempfile
import unittest
import urllib.parse
import sidecar as s
from copilot_adapter import CopilotProvider,SdkRuntime,contract


class DeviceTransport:
    def __init__(self):
        self.calls=[];self.reply={'error':'authorization_pending'};self.identity={'id':123,'login':'mock-user'};self.hook=None
    def json(self,method,url,headers=None,body=None):
        self.calls.append((method,url,headers,body))
        if url.endswith('/device/code'):return {'device_code':'DEVICE_SECRET','user_code':'ABCD-EFGH','verification_uri':'https://github.com/login/device','expires_in':900,'interval':5}
        if url.endswith('/access_token'):
            if self.hook:self.hook()
            return self.reply
        if url.endswith('/user'):return self.identity
        raise AssertionError(url)


class FakeSession:
    def __init__(self,client):self.client=client;self.handler=None;self.aborted=False
    def on(self,handler):self.handler=handler
    async def send_and_wait(self,prompt,**kwargs):
        self.client.prompt=prompt;self.client.send_kwargs=kwargs
        for event in self.client.events:self.handler(event)
        return NS(data=NS(content='{"steps":[]}'))
    async def disconnect(self):self.client.disconnected=True
    async def abort(self):self.aborted=True


class FakeClient:
    def __init__(self,**kwargs):
        self.kwargs=kwargs;self.session_kwargs=None;self.stopped=False;self.disconnected=False
        self.events=[NS(type='assistant.usage',data=NS(input_tokens=8,output_tokens=4,model='model/same',is_auto=False,is_byok=False))]
        self.catalog=[{'id':'model/same','name':'Same','policy':{'state':'enabled'},'capabilities':{}},
                      {'id':'blocked','name':'Blocked','policy':{'state':'disabled'},'capabilities':{}},
                      {'id':'auto','name':'Auto','policy':{'state':'enabled'},'capabilities':{}}]
    async def start(self):pass
    async def stop(self):self.stopped=True
    async def list_models(self):return self.catalog
    async def create_session(self,**kwargs):self.session_kwargs=kwargs;return FakeSession(self)


class CopilotTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix='chooguard-device-mock-')
        self.store=s.PrivateStore(Path(self.temp.name)/'private')
        self.transport=DeviceTransport();self.now=1000
        self.p=CopilotProvider(self.store,transport=self.transport,clock=lambda:self.now,contract=contract('OWN_APP_PUBLIC_CLIENT'))
    def tearDown(self):self.store.close();self.temp.cleanup()
    def fails(self,code,fn):
        with self.assertRaises(s.Failure) as e:fn()
        self.assertEqual(code,e.exception.code)
    def login(self,extra=None):
        result=self.p.auth_start();self.now+=5
        self.transport.reply={'access_token':'APP_OWN_FAKE_TOKEN','token_type':'bearer','scope':'read:user',**(extra or {})}
        done=self.p.poll_device(result['state']);return done['account_id']

    def test_public_client_device_flow_no_secret_and_identity_binding(self):
        start=self.p.auth_start();self.assertTrue(start['device_flow']);self.assertNotIn('DEVICE_SECRET',json.dumps(start))
        form=urllib.parse.parse_qs(self.transport.calls[-1][3].decode());self.assertEqual(['OWN_APP_PUBLIC_CLIENT'],form['client_id']);self.assertNotIn('client_secret',form)
        self.assertTrue(self.p.poll_device(start['state'])['pending']);self.assertEqual(1,len(self.transport.calls))
        self.now+=5;self.transport.reply={'access_token':'APP_OWN_FAKE_TOKEN','token_type':'bearer','scope':'read:user'}
        done=self.p.poll_device(start['state']);self.assertEqual('123',self.store.data['accounts'][done['account_id']]['subject'])
        self.assertEqual('https://api.github.com/user',self.transport.calls[-1][1]);self.assertFalse(self.p.cloud_enabled)

    def test_slow_down_interval_and_expiry(self):
        start=self.p.auth_start();self.now+=5;self.transport.reply={'error':'slow_down'}
        self.assertEqual(10,self.p.poll_device(start['state'])['interval'])
        self.now+=901;self.fails('invalid_or_expired_oauth_state',lambda:self.p.poll_device(start['state']))

    def test_cancel_during_poll_does_not_resurrect_tokens(self):
        start=self.p.auth_start();self.now+=5;self.transport.reply={'access_token':'APP_OWN_FAKE_TOKEN','token_type':'bearer','scope':'read:user'}
        self.transport.hook=lambda:self.p.pending.clear()
        self.fails('provider_session_changed',lambda:self.p.poll_device(start['state']))
        self.assertEqual({},self.store.data['accounts'])

    def test_refresh_public_client_identity_and_logout_race(self):
        a=self.login({'expires_in':61,'refresh_token':'APP_REFRESH'})
        self.now+=3;self.transport.reply={'access_token':'APP_REFRESHED','token_type':'bearer','expires_in':3600,'refresh_token':'APP_REFRESH2'}
        self.assertEqual('APP_REFRESHED',self.p.access(a))
        form=urllib.parse.parse_qs(self.transport.calls[-2][3].decode());self.assertNotIn('client_secret',form);self.assertEqual(['refresh_token'],form['grant_type'])
        self.store.data['accounts'][a]['expires_at']=self.now
        self.transport.hook=lambda:self.p.logout(a)
        self.fails('session_changed_during_refresh',lambda:self.p.access(a));self.assertNotIn('access_token',self.store.data['accounts'][a])

    def test_refresh_rejects_different_user(self):
        a=self.login({'expires_in':61,'refresh_token':'APP_REFRESH'});self.now+=3
        self.transport.reply={'access_token':'APP_REFRESHED','token_type':'bearer','expires_in':3600,'refresh_token':'APP_REFRESH2'};self.transport.identity={'id':456,'login':'different'}
        self.fails('returning_account_identity_mismatch',lambda:self.p.access(a));self.assertNotIn('access_token',self.store.data['accounts'][a])

    def test_logout_local_clears_without_embedded_app_secret(self):
        a=self.login();before=len(self.transport.calls);result=self.p.logout(a)
        self.assertFalse(result['remote_revocation_confirmed']);self.assertTrue(result['local_session_cleared'])
        self.assertEqual(before,len(self.transport.calls));self.assertNotIn('access_token',self.store.data['accounts'][a])

    def runtime(self):
        self.clients=[]
        def factory(**kwargs):
            client=FakeClient(**kwargs);self.clients.append(client);return client
        return SdkRuntime(self.store.directory/'sdk',client_factory=factory)

    def test_pinned_sdk_signatures_support_actual_sandbox_options(self):
        from copilot import CopilotClient
        from copilot.session import CopilotSession
        client=inspect.signature(CopilotClient).parameters;session=inspect.signature(CopilotClient.create_session).parameters
        for key in ['github_token','use_logged_in_user','mode','base_directory','env']:self.assertIn(key,client)
        for key in ['available_tools','tools','enable_config_discovery','enable_skills','enable_file_hooks','enable_host_git_operations','model_capabilities','on_permission_request']:self.assertIn(key,session)
        self.assertIn('prompt',inspect.signature(CopilotSession.send_and_wait).parameters)

    def test_sdk_catalog_policy_disabled_and_auto_never_selectable(self):
        runtime=self.runtime();models=runtime.models('APP_ONLY_TOKEN');self.assertTrue(models[0]['selectable'])
        self.assertFalse(models[1]['selectable']);self.assertFalse(models[2]['selectable'])
        self.assertEqual('APP_ONLY_TOKEN',self.clients[-1].kwargs['github_token']);self.assertFalse(self.clients[-1].kwargs['use_logged_in_user']);self.assertTrue(self.clients[-1].stopped)

    def test_tool_free_single_exact_model_and_usage(self):
        runtime=self.runtime();out=runtime.infer('APP_ONLY_TOKEN',{'model':'model/same','input':[{'role':'user','content':'Return JSON'}],'max_output_tokens':64})
        client=self.clients[-1];options=client.session_kwargs
        self.assertEqual('empty',client.kwargs['mode']);self.assertEqual('model/same',options['model'])
        self.assertEqual([],options['tools']);self.assertEqual([],options['available_tools']);self.assertEqual({},options['mcp_servers'])
        for key in ['enable_config_discovery','enable_skills','enable_file_hooks','enable_host_git_operations','enable_session_store','enable_github_cache_store']:self.assertFalse(options[key])
        self.assertEqual(64,options['model_capabilities'].limits.max_output_tokens)
        self.assertEqual('reject',getattr(options['on_permission_request'](None,None).kind,'value',options['on_permission_request'](None,None).kind))
        self.assertNotIn('GITHUB_TOKEN',client.kwargs['env']);self.assertNotIn('OPENAI_API_KEY',client.kwargs['env']);self.assertNotIn('HOME',client.kwargs['env']);self.assertEqual(self.store.directory/'sdk',Path(client.kwargs['base_directory']))
        self.assertIsInstance(client.prompt,str);self.assertEqual({'input_tokens':8,'output_tokens':4},out[1]);self.assertTrue(client.stopped);self.assertTrue(client.disconnected)
        semantic=SdkRuntime.semantic_input({'model':'model/same','input':[{'role':'user','content':'Return JSON'}],'max_output_tokens':64})
        self.assertEqual(client.prompt,semantic['prompt'])
        self.assertEqual(options['system_message'],semantic['system_message'])
        self.assertEqual(options['model'],semantic['model'])
        self.assertEqual(options['model_capabilities'].limits.max_output_tokens,semantic['max_output_tokens'])
        self.assertFalse(semantic['sdk_internal_overhead_known'])
        self.assertNotIn('APP_ONLY_TOKEN',json.dumps(semantic))

    def test_missing_usage_tool_event_and_auto_billing_refused(self):
        for events,code in [([], 'provider_usage_missing'),([NS(type='tool.execution_start',data=NS())],'unexpected_sdk_tool_event'),
                            ([NS(type='assistant.usage',data=NS(input_tokens=8,output_tokens=4,is_auto=True,is_byok=False))],'unexpected_sdk_billing_or_auto_routing')]:
            def factory(**kwargs):
                client=FakeClient(**kwargs);client.events=events;return client
            runtime=SdkRuntime(self.store.directory/'sdk',client_factory=factory)
            self.fails(code,lambda:runtime.infer('APP_ONLY_TOKEN',{'model':'model/same','input':[],'max_output_tokens':64}))

    def test_missing_or_mismatched_usage_model_refused(self):
        for reported in [None,'different-model']:
            def factory(**kwargs):
                client=FakeClient(**kwargs);client.events[0].data.model=reported;return client
            runtime=SdkRuntime(self.store.directory/'sdk',client_factory=factory)
            self.fails('unexpected_sdk_usage_model',lambda:runtime.infer('APP_ONLY_TOKEN',{'model':'model/same','input':[],'max_output_tokens':64}))

    def test_device_login_does_not_restore_changed_play_consent_or_logout(self):
        from multi_provider import MultiProvider
        from provider_registry import Registry
        a='aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
        self.store.data['accounts'][a]={'provider_id':'github-copilot','client_id':'OWN_APP_PUBLIC_CLIENT',
            'subject':'123','label':'Old A','access_token':'OLD_A_TOKEN','scopes':['read:user']}
        self.store.data['active']=a;self.store.save()
        class Runtime:
            def models(self,token):return [{'slug':'model/same','display_name':'Same','selectable':True},
                                          {'slug':'model/other','display_name':'Other','selectable':True}]
        p=MultiProvider(self.store,registry=Registry([self.p.contract]),transports={'github-copilot':self.transport},
            clock=lambda:self.now,adapter_factories={'github-copilot':lambda *args,**kwargs:CopilotProvider(*args,runtime=Runtime(),**kwargs)})
        p.configure(18954);p.models();p.session({'model':'model/same','cloud_enabled':True})
        start=p.auth_start(provider_id='github-copilot');self.now+=5
        self.transport.reply={'access_token':'NEW_B_TOKEN','token_type':'bearer','scope':'read:user'}
        after={}
        def switch():
            p.session({'model':'model/other','cloud_enabled':False});after['selection']=p.selection()
        self.transport.hook=switch
        p.poll_device('github-copilot',start['state'])
        self.assertEqual(after['selection'],p.selection());self.assertFalse(p.status()['cloud_enabled'])
        self.transport.hook=None;p.session({'cloud_enabled':True})
        start=p.auth_start(provider_id='github-copilot');self.now+=5
        self.transport.hook=lambda:p.logout(a)
        p.poll_device('github-copilot',start['state'])
        self.assertIsNone(p.selection());self.assertNotIn('access_token',self.store.data['accounts'][a])
        self.assertFalse(p.status()['cloud_enabled'])

    def test_real_sdk_exact_pinned_constructor_without_start_or_download(self):
        runtime=SdkRuntime(self.store.directory/'sdk')
        client=runtime.client('FAKE_APP_TOKEN')
        self.assertEqual('empty',client._options.mode)
        self.assertFalse(client._options.use_logged_in_user)
        self.assertIsNone(client._client)


if __name__=='__main__':unittest.main()
