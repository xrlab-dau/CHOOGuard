"""Capability-authenticated control surface for the Unity FPS provider menu.

Browser pages only finish OAuth. Native callers get public authorization URLs or
short device user codes; tokens, device codes and PKCE verifiers stay in the helper.
"""
import copy
import base64
import hashlib
import re
import secrets
import urllib.parse
from sidecar import Failure

SCHEMA='native-provider-control-v1'
FLOW_PATH='/native/v1/auth/flows/'
TERMINAL={'completed','cancelled','expired','failed'}


def identifier(value):
    return isinstance(value,str) and re.fullmatch(r'[A-Za-z0-9_.:-]{1,160}',value) is not None


def fields(body,allowed,required=()):
    if not isinstance(body,dict):raise Failure('json_object_required')
    if set(body)-set(allowed) or set(required)-set(body):raise Failure('invalid_native_control_fields')


class NativeControl:
    def __init__(self,provider):
        self.provider=provider;self.flows={}

    def guard(self,generation):
        if not generation:raise Failure('provider_session_header_required',409)
        if not secrets.compare_digest(str(generation),self.provider.generation):raise Failure('provider_session_changed',409)

    def models(self):
        rows=[]
        with self.provider.lock:
            for provider_id,adapter in self.provider.adapters.items():
                for account_id,models in adapter.catalog.items():
                    account=adapter.store.data['accounts'].get(account_id)
                    version=self.provider.catalog_versions.get((provider_id,account_id))
                    if not account or not account.get('access_token') or not version:continue
                    for m in models:
                        rows.append({'provider_id':provider_id,'account_id':account_id,'model_id':m['slug'],
                            'slug':m['slug'],'display_name':m['display_name'],'catalog_version':version,
                            'billing':'user_subscription','capabilities':['initial_world','npc_plan'],
                            'selectable':m.get('selectable',True),'reason':m.get('reason',''),
                            'policy_state':m.get('policy_state','account_catalog_listed'),
                            'subscription_usage':copy.deepcopy(m.get('subscription_usage',{}))})
        return rows

    def envelope(self,flow=None,logout=None):
        with self.provider.lock:
            self.sync()
            value={'schema':SCHEMA,'status':self.provider.status(),'models':self.models()}
            if flow is not None:value['flow']=self.public_flow(flow)
            if logout is not None:value['logout']={k:bool(logout[k]) for k in ('local_session_cleared','remote_revocation_confirmed')}
            return value

    def sync(self):
        now=self.provider.clock()
        for flow in self.flows.values():
            if flow['status'] in TERMINAL:continue
            adapter=self.provider.adapters[flow['provider_id']]
            if now>=flow['expires_at']:
                flow['status']='expired';adapter.pending.pop(flow['oauth_state'],None)
            elif not flow.get('callback_in_progress') and flow['oauth_state'] not in adapter.pending:
                flow['status']='cancelled'

    def public_flow(self,flow,start=False):
        adapter=self.provider.adapters[flow['provider_id']]
        pending=adapter.pending.get(flow['oauth_state'],{})
        delay=max(0,pending.get('next_poll',self.provider.clock())-self.provider.clock())
        result={'flow_id':flow['flow_id'],'provider_id':flow['provider_id'],'status':flow['status'],
            'expires_in_seconds':max(0,int(flow['expires_at']-self.provider.clock())),
            'poll_after_ms':int(delay*1000) if flow['status']=='waiting_for_device' else 1000,
            'error_code':flow.get('error_code'),'account_id':flow.get('completed_account_id')}
        if start:
            for key in ('authorization_url','verification_uri','user_code'):
                if key in flow:result[key]=flow[key]
        return result

    def auth_start(self,body,generation):
        fields(body,{'provider_id','account_id','reconsent'},{'provider_id'})
        provider_id=body['provider_id'];account_id=body.get('account_id')
        if not identifier(provider_id) or account_id is not None and not identifier(account_id):raise Failure('invalid_native_provider_account')
        reconsent=body.get('reconsent',False)
        if type(reconsent) is not bool:raise Failure('explicit_reconsent_choice_required')
        with self.provider.lock:
            self.guard(generation);self.sync()
            if sum(f['status'] not in TERMINAL for f in self.flows.values())>=8:raise Failure('too_many_native_auth_flows',429)
            before_accounts={a['id'] for a in self.provider.status()['accounts']}
        reply=self.provider.auth_start(account_id,reconsent,provider_id=provider_id)
        adapter=self.provider.adapters[provider_id]
        url=reply.get('authorization_url')
        parsed=urllib.parse.urlsplit(url or '')
        expected=urllib.parse.urlsplit(adapter.contract.issuer)
        # Return only the exact provider's HTTPS authorization route, never a redirect.
        expected_path=adapter.contract.authorization_path
        device=reply.get('device_flow',False)
        if parsed.scheme!='https' or parsed.netloc!=expected.netloc or parsed.username or parsed.password or parsed.fragment or parsed.path!=expected_path or device and parsed.query:
            raise Failure('untrusted_provider_authorization_url',502)
        if device:
            oauth_state=reply.get('state');user_code=reply.get('user_code')
            if not isinstance(user_code,str) or not re.fullmatch(r'[A-Za-z0-9 -]{1,128}',user_code):raise Failure('invalid_device_user_code',502)
        else:
            query=urllib.parse.parse_qs(parsed.query)
            if len(query.get('state',[]))!=1:raise Failure('invalid_provider_authorization_state',502)
            oauth_state=query['state'][0]
        if not isinstance(oauth_state,str) or oauth_state not in adapter.pending:raise Failure('invalid_provider_authorization_state',502)
        if not device:
            pending=adapter.pending[oauth_state]
            expected_challenge=base64.urlsafe_b64encode(hashlib.sha256(pending['verifier'].encode()).digest()).rstrip(b'=').decode()
            expected_fields={'redirect_uri':adapter.redirect_uri,'response_type':'code','code_challenge_method':'S256','code_challenge':expected_challenge}
            if any(query.get(k)!=[v] for k,v in expected_fields.items()):
                adapter.pending.pop(oauth_state,None);raise Failure('untrusted_provider_authorization_url',502)
        with self.provider.lock:
            # Do not launch a late UI authorization request against an older session.
            if generation!=self.provider.generation:
                adapter.pending.pop(oauth_state,None);raise Failure('provider_session_changed',409)
            flow_id=secrets.token_urlsafe(32)
            flow={'flow_id':flow_id,'provider_id':provider_id,'account_id':account_id,
                'oauth_state':oauth_state,'expires_at':adapter.pending[oauth_state]['expires'],
                'status':'waiting_for_device' if device else 'waiting_for_browser',
                'before_accounts':before_accounts,'error_code':None}
            if device:flow.update(verification_uri=url,user_code=user_code)
            else:flow['authorization_url']=url
            self.flows[flow_id]=flow
            value=self.envelope()
            value['flow']=self.public_flow(flow,start=True)
            return value

    def flow(self,flow_id,generation,cancel=False):
        with self.provider.lock:
            self.guard(generation);self.sync()
            flow=self.flows.get(flow_id)
            if not flow:raise Failure('native_auth_flow_not_found',404)
            if cancel and flow['status'] not in TERMINAL:
                self.provider.adapters[flow['provider_id']].pending.pop(flow['oauth_state'],None)
                flow['status']='cancelled'
            should_poll=not cancel and flow['status']=='waiting_for_device'
        if should_poll:
            try:reply=self.provider.poll_device(flow['provider_id'],flow['oauth_state'])
            except Failure as failure:
                with self.provider.lock:
                    if flow['status']=='cancelled':return self.envelope(flow=flow)
                    flow['status']='expired' if failure.code=='invalid_or_expired_oauth_state' else 'cancelled' if failure.code=='authorization_cancelled' else 'failed'
                    flow['error_code']=failure.code
            else:
                with self.provider.lock:
                    if not reply.get('pending'):
                        flow['status']='completed';self.completed_account(flow)
        return self.envelope(flow=flow)

    def before_callback(self,state):
        with self.provider.lock:
            flow=next((f for f in self.flows.values() if f['oauth_state']==state),None)
            if flow and flow['status'] not in TERMINAL:flow['callback_in_progress']=True
            return flow is not None

    def completed_account(self,flow):
        if flow.get('account_id'):flow['completed_account_id']=flow['account_id'];return
        added=[a['id'] for a in self.provider.status()['accounts'] if a['provider_id']==flow['provider_id'] and a['id'] not in flow['before_accounts'] and a['signed_in']]
        if len(added)==1:flow['completed_account_id']=added[0]

    def after_callback(self,state,failure=None):
        with self.provider.lock:
            flow=next((f for f in self.flows.values() if f['oauth_state']==state and f.get('callback_in_progress')),None)
            if not flow:return
            flow['callback_in_progress']=False
            flow['status']=('expired' if failure and failure.code=='invalid_or_expired_oauth_state' and self.provider.clock()>=flow['expires_at'] else
                'cancelled' if failure and failure.code=='authorization_cancelled' else 'failed' if failure else 'completed')
            flow['error_code']=failure.code if failure else None
            if not failure:self.completed_account(flow)

    def dispatch(self,method,path,body,generation):
        if method=='GET' and path=='/native/v1/status':return self.envelope()
        if path.startswith(FLOW_PATH) and method in ('GET','DELETE'):
            flow_id=path[len(FLOW_PATH):]
            if not re.fullmatch(r'[A-Za-z0-9_-]{43}',flow_id):raise Failure('native_auth_flow_not_found',404)
            if body is not None:fields(body,set())
            return self.flow(flow_id,generation,cancel=method=='DELETE')
        if method=='POST' and path=='/native/v1/auth/start':return self.auth_start(body,generation)
        # Session/consent changes commit under the same lock as their generation CAS.
        with self.provider.lock:
            self.guard(generation)
            if method=='POST' and path=='/native/v1/catalog':fields(body,set())
            elif method=='POST' and path=='/native/v1/selection':
                fields(body,{'provider_id','account_id','model_id','catalog_version'},{'provider_id','account_id','model_id','catalog_version'})
                if not identifier(body['provider_id']) or not identifier(body['account_id']) or not isinstance(body['model_id'],str) or not re.fullmatch(r'[0-9a-f]{64}',str(body['catalog_version'])):raise Failure('invalid_native_selection')
                self.provider.session({'provider_id':body['provider_id'],'account_id':body['account_id'],
                    'model':body['model_id'],'catalog_version':body['catalog_version'],'cloud_enabled':False})
                return self.envelope()
            elif method=='DELETE' and path=='/native/v1/selection':
                fields(body or {},set());self.provider.clear_selection();return self.envelope()
            elif method=='POST' and path=='/native/v1/consent':
                fields(body,{'subscription_enabled','initial_world_allowance','resume_after_usage_review'},{'subscription_enabled'})
                if type(body['subscription_enabled']) is not bool:raise Failure('explicit_subscription_choice_required')
                if not self.provider.selection():
                    if body['subscription_enabled'] or len(body)>1:raise Failure('select_account_model_first',403)
                    return self.envelope()
                if 'resume_after_usage_review' in body and type(body['resume_after_usage_review']) is not bool:raise Failure('explicit_usage_review_required')
                settings={k:v for k,v in body.items() if k!='subscription_enabled'}
                settings['cloud_enabled']=body['subscription_enabled'];self.provider.session(settings)
                return self.envelope()
            elif method=='POST' and path=='/native/v1/logout':
                fields(body,{'account_id'},{'account_id'})
                if not identifier(body['account_id']):raise Failure('invalid_native_provider_account')
                result=self.provider.logout(body['account_id']);return self.envelope(logout=result)
            else:raise Failure('native_control_route_not_found',404)
        # Explicit catalog refresh may perform model-list requests, never inference.
        self.provider.models()
        return self.envelope()
