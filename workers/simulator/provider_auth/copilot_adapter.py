"""Own OAuth App device login + tool-free GitHub Copilot SDK subscription adapter.

No CLI credentials, PAT, API key, external tools, model auto-router or BYOK provider.
A public client ID from CHOOGuard's own device-enabled OAuth App is required.
"""
import asyncio
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import secrets
import stat
import tempfile
import time
import urllib.parse
import uuid
from provider_registry import SubscriptionContract
from sidecar import Provider, Failure, Transport, compact

SDK_COMMIT='810c483ae426344c2a5fee61025f9e3bd457cca9'

SDK_DISTRIBUTION='1.0.22rc1'
SDK_RUNTIME_VERSION='1.0.98-1'
MODEL_ONLY_SYSTEM_MESSAGE='You are a model-only CHOOGuard simulation planner. Return only the requested JSON. Do not use tools, change model, call subagents, or access files.'
MODEL_PROMPT_INSTRUCTION='Return exactly the JSON requested by these messages.'
SDK_SOURCE_HASHES={'__init__.py': '4781a18defdec777a2f8d6ed02e6bb84258ce9d2c52b19032a315569c6ebb96a', 'client.py': 'd56c93c7967f21511bb7cc95c66840e85b2fb79631d91e91baad9b41930221a1', 'session.py': 'f779d18a1ef7418a984ecbc97532bcc100cdefe52be6987fc0a5e197d3e1d3ed', 'generated/session_events.py': 'd10bb99b9a76389180da37fb3497d7db2656e4a59e67caf402211be8dc82e321', '_sdk_protocol_version.py': '6775e49bd5cd1ce8f3698a7a25b9801a33a46f0ea84345cf7038752af8013e1e', '_cli_download.py': '7251e4e74751ef87f1a66a3b7c410d7c3caf0714f9f5f7743eaf73fb84cbdd52', '_cli_version.py': '1626313381a147b8eed6ed6aee16c10ac93ee2eed30915df25781c01d371c9a8'}

def verify_sdk_pin():
    # Refuse a global/mutable package even if its Python API happens to look compatible.
    import importlib
    expected=Path(__file__).with_name('.sdk-deps').resolve()/'copilot'
    try:
        module=importlib.import_module('copilot')
    except ImportError:raise Failure('copilot_sdk_not_installed',503) from None
    if Path(module.__file__).resolve()!=expected/'__init__.py':raise Failure('copilot_sdk_pin_mismatch',503)
    for relative,digest in SDK_SOURCE_HASHES.items():
        path=expected/relative
        if not path.is_file() or path.is_symlink() or hashlib.sha256(path.read_bytes()).hexdigest()!=digest:
            raise Failure('copilot_sdk_pin_mismatch',503)
    return module


def contract(client_id):
    if not isinstance(client_id,str) or not 1<=len(client_id)<=200 or not all(c.isalnum() or c in '._-' for c in client_id):
        raise Failure('own_github_oauth_app_client_id_required')
    return SubscriptionContract(id='github-copilot',label='GitHub Copilot',issuer='https://github.com',
        resource='https://api.github.com',scopes='read:user',required_scope='read:user',
        authorization_path='/login/device',token_path='/login/oauth/access_token',
        usage_url='https://github.com/settings/copilot',client_id=client_id,dynamic_registration=False,
        evidence_url='https://docs.github.com/en/copilot/how-tos/copilot-sdk/setup/github-oauth')


class SdkRuntime:
    def __init__(self,directory,client_factory=None):
        self.directory=Path(directory);self.client_factory=client_factory

    @staticmethod
    def available():return importlib.util.find_spec('copilot') is not None

    def client(self,token):
        if not self.client_factory:
            try:
                CopilotClient=verify_sdk_pin().CopilotClient
            except ImportError:raise Failure('copilot_sdk_not_installed',503) from None
            factory=CopilotClient
        else:factory=self.client_factory
        self.directory.mkdir(parents=True,exist_ok=True,mode=0o700)
        if self.directory.is_symlink():raise Failure('unsafe_sdk_directory')
        os.chmod(self.directory,0o700)
        # Explicit clean env: no inherited GitHub/other CLI auth or provider API keys.
        env={'PATH':os.environ.get('PATH','/usr/bin:/bin'),
             'XDG_CONFIG_HOME':str(self.directory/'config'),
             'COPILOT_SKIP_CLI_DOWNLOAD':'1'}
        connection={}
        if not self.client_factory:
            from copilot.client import RuntimeConnection
            from copilot._cli_version import get_runtime_platform
            pair=Path(__file__).with_name('.sdk-runtime')/'prebuilds'/get_runtime_platform()
            runtime=pair/('copilot-runtime.exe' if os.name=='nt' else 'copilot-runtime')
            if not runtime.is_file() or not (pair/'runtime.node').is_file():raise Failure('copilot_runtime_not_installed',503)
            if runtime.is_symlink() or (pair/'runtime.node').is_symlink():raise Failure('unsafe_sdk_runtime')
            receipt=json.loads(Path(__file__).with_name('sdk-install-receipt.json').read_text())
            if receipt.get('distribution_version')!=SDK_DISTRIBUTION or receipt.get('runtime_version')!=SDK_RUNTIME_VERSION:raise Failure('copilot_sdk_pin_mismatch',503)
            for name,digest in receipt.get('runtime_files',{}).items():
                file=Path(__file__).with_name('.sdk-runtime')/name
                if not file.is_file() or hashlib.sha256(file.read_bytes()).hexdigest()!=digest:raise Failure('copilot_runtime_pin_mismatch',503)
            if not receipt.get('runtime_files'):raise Failure('copilot_runtime_not_verified',503)
            connection['connection']=RuntimeConnection.for_stdio(path=str(runtime.resolve()))
        try:
            return factory(**connection,github_token=token,use_logged_in_user=False,mode='empty',
                base_directory=str(self.directory),working_directory=str(self.directory),env=env,
                log_level='error',enable_remote_sessions=False,builtin_plugin_directories=[])
        except TypeError:raise Failure('copilot_sdk_contract_version_mismatch',503) from None

    def models(self,token):return asyncio.run(self._models(token))

    async def _models(self,token):
        client=self.client(token)
        try:
            await client.start()
            rows=await client.list_models()
            result=[]
            for model in rows:
                value=model.to_dict() if hasattr(model,'to_dict') else model
                if not isinstance(value,dict):raise Failure('invalid_model_catalog',502)
                model_id=value.get('id');name=value.get('name')
                if not isinstance(model_id,str) or not model_id or not isinstance(name,str):raise Failure('invalid_model_catalog',502)
                policy=value.get('policy') or {}
                enabled=policy.get('state')=='enabled'
                # "auto" delegates model selection to the harness, contrary to the user's choice.
                reason='automatic_model_routing_not_supported' if model_id.lower()=='auto' else '' if enabled else 'account_model_policy_not_enabled'
                result.append({'slug':model_id,'display_name':name,'selectable':not reason,'reason':reason,
                    'policy_state':policy.get('state','unknown'),'billing_evidence':'copilot_user_subscription',
                    'capabilities':value.get('capabilities',{}),'subscription_usage':value.get('billing',{})})
            return result
        except Failure:raise
        except Exception:raise Failure('copilot_catalog_unavailable',503) from None
        finally:
            try:await client.stop()
            except Exception:pass

    def infer(self,token,upstream):return asyncio.run(self._infer(token,upstream))

    @staticmethod
    def semantic_input(upstream):
        return {'schema':'copilot-sdk-semantic-input-v1','model':upstream['model'],
            'prompt':compact({'messages':upstream['input'],'instruction':MODEL_PROMPT_INSTRUCTION}).decode(),
            'system_message':{'mode':'replace','content':MODEL_ONLY_SYSTEM_MESSAGE},
            'max_output_tokens':upstream['max_output_tokens'],'tools_allowed':[],
            'sdk_commit':SDK_COMMIT,'sdk_distribution':SDK_DISTRIBUTION,
            'runtime_version':SDK_RUNTIME_VERSION,'sdk_internal_overhead_known':False}

    async def _infer(self,token,upstream):
        client=self.client(token);session=None;usage=[];violations=[]
        try:
            from copilot.session import ModelCapabilitiesOverride,ModelLimitsOverride
            from copilot.rpc import PermissionDecisionReject
            def deny(*_):return PermissionDecisionReject(feedback='CHOOGuard model-only session: tools are disabled.')
            await client.start()
            session=await client.create_session(model=upstream['model'],tools=[],available_tools=[],
                on_permission_request=deny,streaming=False,mcp_servers={},custom_agents=[],commands=[],
                enable_config_discovery=False,skip_custom_instructions=True,
                enable_skills=False,enable_file_hooks=False,enable_host_git_operations=False,
                enable_session_store=False,enable_github_cache_store=False,
                memory={'enabled':False},infinite_sessions={'enabled':False},
                model_capabilities=ModelCapabilitiesOverride(limits=ModelLimitsOverride(
                    max_output_tokens=upstream['max_output_tokens'])),
                system_message={'mode':'replace','content':MODEL_ONLY_SYSTEM_MESSAGE})
            def observe(event):
                type=getattr(event.type,'value',event.type);data=event.data
                if type=='assistant.usage':usage.append(data)
                if type in ('tool.execution_start','subagent.started'):
                    violations.append('unexpected_sdk_tool_event')
                if type=='session.model_change' and getattr(data,'new_model',None)!=upstream['model']:
                    violations.append('unexpected_sdk_model_change')
            session.on(observe)
            prompt=self.semantic_input(upstream)['prompt']
            try:reply=await session.send_and_wait(prompt,timeout=45)
            except TimeoutError:
                await session.abort();raise Failure('provider_stream_timeout',504) from None
            if violations:raise Failure(violations[0],502)
            if not reply or not isinstance(getattr(reply.data,'content',None),str):raise Failure('provider_response_text_missing',502)
            if not usage:raise Failure('provider_usage_missing',502)
            in_tokens=out_tokens=0
            for item in usage:
                if getattr(item,'is_auto',False) or getattr(item,'is_byok',False):raise Failure('unexpected_sdk_billing_or_auto_routing',502)
                if getattr(item,'model',None)!=upstream['model']:raise Failure('unexpected_sdk_usage_model',502)
                for key in ('input_tokens','output_tokens'):
                    v=getattr(item,key,None)
                    if isinstance(v,bool) or not isinstance(v,int) or v<0:raise Failure('provider_usage_invalid',502)
                in_tokens+=item.input_tokens;out_tokens+=item.output_tokens
            return reply.data.content,{'input_tokens':in_tokens,'output_tokens':out_tokens},str(uuid.uuid4()),getattr(usage[-1],'model',None)
        except Failure:raise
        except TypeError:raise Failure('copilot_sdk_contract_version_mismatch',503) from None
        except Exception:raise Failure('copilot_inference_unavailable',503) from None
        finally:
            if session:
                try:await session.disconnect()
                except Exception:pass
            try:await client.stop()
            except Exception:pass


class CopilotProvider(Provider):
    def __init__(self,store,transport=None,clock=time.time,contract=None,runtime=None):
        super().__init__(store,transport or Transport(('github.com','api.github.com')),clock,contract)
        self.runtime=runtime or SdkRuntime(store.directory/'copilot-sdk')

    def inference_input_context(self,upstream):return SdkRuntime.semantic_input(upstream)

    def auth_start(self,account_id=None,reconsent=False):
        with self.lock:
            if account_id and account_id not in self.store.data['accounts']:raise Failure('unknown_account')
            self.pending={k:v for k,v in self.pending.items() if v['expires']>self.clock()}
            if len(self.pending)>=4:raise Failure('too_many_auth_attempts',429)
        fields={'client_id':self.contract.client_id,'scope':'read:user'}
        reply=self.transport.json('POST','https://github.com/login/device/code',
            {'Content-Type':'application/x-www-form-urlencoded','Accept':'application/json'},urllib.parse.urlencode(fields).encode())
        for key in ('device_code','user_code','verification_uri'):
            if not isinstance(reply.get(key),str) or not reply[key]:raise Failure('invalid_device_authorization_response',502)
        if reply['verification_uri']!='https://github.com/login/device':raise Failure('untrusted_device_verification_uri',502)
        expires=reply.get('expires_in');interval=reply.get('interval',5)
        if isinstance(expires,bool) or not isinstance(expires,int) or not 1<=expires<=1800 or isinstance(interval,bool) or not isinstance(interval,int) or not 1<=interval<=60:raise Failure('invalid_device_authorization_response',502)
        state=secrets.token_urlsafe(32)
        with self.lock:self.pending[state]={'expires':self.clock()+expires,'device_code':reply['device_code'],
            'account_id':account_id,'interval':interval,'next_poll':self.clock()+interval}
        return {'provider_id':self.contract.id,'device_flow':True,'state':state,
            'authorization_url':reply['verification_uri'],'user_code':reply['user_code'],
            'expires_in':expires,'interval':interval}

    def poll_device(self,state,activate=True):
        with self.lock:
            pending=self.pending.get(state)
            if not pending or pending['expires']<=self.clock():
                self.pending.pop(state,None);raise Failure('invalid_or_expired_oauth_state')
            if self.clock()<pending['next_poll']:return {'pending':True,'interval':pending['interval']}
            pending['next_poll']=self.clock()+pending['interval']
        fields={'client_id':self.contract.client_id,'device_code':pending['device_code'],
            'grant_type':'urn:ietf:params:oauth:grant-type:device_code'}
        reply=self.transport.json('POST','https://github.com/login/oauth/access_token',
            {'Content-Type':'application/x-www-form-urlencoded','Accept':'application/json'},urllib.parse.urlencode(fields).encode())
        error=reply.get('error')
        if error in ('authorization_pending','slow_down'):
            with self.lock:
                if error=='slow_down':pending['interval']=min(60,pending['interval']+5);pending['next_poll']=self.clock()+pending['interval']
            return {'pending':True,'interval':pending['interval']}
        if error:
            with self.lock:self.pending.pop(state,None)
            raise Failure('authorization_cancelled' if error=='access_denied' else 'device_authorization_failed',403)
        token=reply.get('access_token')
        if not isinstance(token,str) or not token or len(token)>32768 or reply.get('token_type','').lower()!='bearer':raise Failure('invalid_token_response',502)
        scopes=reply.get('scope','').replace(',',' ').split()
        if 'read:user' not in scopes:raise Failure('github_identity_scope_required',403)
        identity=self.transport.json('GET','https://api.github.com/user',{'Authorization':'Bearer '+token,'Accept':'application/vnd.github+json','X-GitHub-Api-Version':'2022-11-28'})
        subject=identity.get('id');label=identity.get('login')
        if isinstance(subject,bool) or not isinstance(subject,int) or subject<=0 or not isinstance(label,str):raise Failure('invalid_github_identity',502)
        with self.lock:
            # Cancellation/logout while polling must never resurrect a token.
            if self.pending.get(state) is not pending:raise Failure('provider_session_changed',409)
            account_id=pending['account_id'] or str(uuid.uuid4())
            old=self.store.data['accounts'].get(account_id)
            if old and old.get('subject')!=str(subject):raise Failure('returning_account_identity_mismatch')
            token_fields=self._token_fields(reply)
            self.store.data['accounts'][account_id]={'provider_id':self.contract.id,'client_id':self.contract.client_id,
                'subject':str(subject),'label':label+' · GitHub Copilot','registration_label':self.contract.client_id,
                'access_token':token,'scopes':scopes,'saved_at':self.clock(),'token_type':'Bearer',**token_fields}
            self.pending.pop(state,None);self.catalog.pop(account_id,None)
            if activate:
                self.store.data['active']=account_id;self.selected_model='';self.cloud_enabled=False;self.rotate()
            self.store.save()
            return {'pending':False,'account_id':account_id}

    def callback(self,query):raise Failure('device_flow_has_no_browser_code_callback',400)

    def _token_fields(self,reply):
        fields={}
        expiry=reply.get('expires_in')
        if expiry is not None:
            if isinstance(expiry,bool) or not isinstance(expiry,int) or not 1<=expiry<=31536000:raise Failure('invalid_token_expiry',502)
            fields['expires_at']=self.clock()+expiry
        refresh=reply.get('refresh_token')
        if refresh is not None:
            if not isinstance(refresh,str) or not refresh or len(refresh)>32768:raise Failure('invalid_token_response',502)
            fields['refresh_token']=refresh
        return fields

    def access(self,account_id):
        with self.refresh_lock:
            with self.lock:
                current=self.store.data['accounts'].get(account_id)
                if not current or not current.get('access_token'):raise Failure('sign_in_required',401)
                account=dict(current)
                if 'expires_at' not in account or account['expires_at']-self.clock()>60:return account['access_token']
                if not account.get('refresh_token'):raise Failure('github_reauthorization_required',401)
            fields={'client_id':self.contract.client_id,'grant_type':'refresh_token','refresh_token':account['refresh_token']}
            try:
                reply=self.transport.json('POST','https://github.com/login/oauth/access_token',
                    {'Content-Type':'application/x-www-form-urlencoded','Accept':'application/json'},urllib.parse.urlencode(fields).encode())
                if reply.get('error'):raise Failure('github_refresh_rejected',401)
                token=reply.get('access_token')
                if not isinstance(token,str) or not token or reply.get('token_type','').lower()!='bearer':raise Failure('invalid_token_response',502)
                identity=self.transport.json('GET','https://api.github.com/user',{'Authorization':'Bearer '+token,'Accept':'application/vnd.github+json'})
                if str(identity.get('id'))!=account['subject']:raise Failure('returning_account_identity_mismatch',401)
                replacement={**account,**self._token_fields(reply),'access_token':token,'saved_at':self.clock()}
            except Failure as failure:
                with self.lock:
                    current=self.store.data['accounts'].get(account_id)
                    if not current or current.get('refresh_token')!=account.get('refresh_token'):raise Failure('session_changed_during_refresh',409) from None
                    if failure.status==401:
                        self.clear_tokens(current);self.store.save()
                        if self.store.data.get('active')==account_id:self.cloud_enabled=False;self.rotate()
                raise
            with self.lock:
                current=self.store.data['accounts'].get(account_id)
                if not current or current.get('refresh_token')!=account.get('refresh_token'):raise Failure('session_changed_during_refresh',409)
                self.store.data['accounts'][account_id]=replacement;self.store.save()
            return token

    def models_for(self,account_id):
        with self.lock:generation=self.generation
        try:
            rows=self.runtime.models(self.access(account_id))
            if len({m['slug'] for m in rows})!=len(rows):raise Failure('invalid_model_catalog',502)
            with self.lock:
                if generation!=self.generation:raise Failure('provider_session_changed',409)
                self.catalog[account_id]=rows
            return {'models':rows,'session_generation':generation}
        except Failure as error:self.record_failure(error,account_id);raise

    def session(self,body):
        model=body.get('model')
        if model:
            account_id=body.get('account_id',self.store.data.get('active'))
            row=next((m for m in self.catalog.get(account_id,[]) if m['slug']==model),None)
            if not row or not row.get('selectable'):raise Failure('account_model_not_available_for_subscription',403)
        return super().session(body)

    def infer(self,access,upstream):return self.runtime.infer(access,upstream)

    def logout(self,account_id=None):
        with self.lock:
            account_id=account_id or self.store.data.get('active');account=self.store.data['accounts'].get(account_id)
            if not account:raise Failure('unknown_account')
            if self.store.data.get('active')==account_id:self.cloud_enabled=False;self.selected_model='';self.rotate()
            self.pending={k:v for k,v in self.pending.items() if v.get('account_id')!=account_id}
            self.clear_tokens(account);self.catalog.pop(account_id,None);self.store.save()
        # Revoking a classic OAuth token through GitHub's application API requires
        # an app secret. This public-client app clears locally; user can revoke in GitHub.
        return {**self.status(),'remote_revocation_confirmed':False,'local_session_cleared':True}
