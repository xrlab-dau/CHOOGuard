#!/usr/bin/env python3
"""CHOOGuard per-user ChatGPT subscription OAuth. No API-key billing fallback."""
import argparse
import base64
import fcntl
import hashlib
import http.cookies
import json
import os
from pathlib import Path
import re
import secrets
import signal
import stat
import sys
import tempfile
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from provider_registry import OPENAI

sys.modules.setdefault('sidecar',sys.modules[__name__])
sys.path.insert(0, str(Path(__file__).with_name('.deps')))
sys.path.insert(0, str(Path(__file__).with_name('.sdk-deps')))
try:
    import jwt
except ImportError:
    raise SystemExit('Install provider_auth/requirements.txt in a task-local virtual environment.')

ISSUER = 'https://auth.openai.com'
RESOURCE = 'https://api.openai.com/v1'
SCOPES = 'openid profile email offline_access resource.invoke chatgpt.tokens.use.direct'
DIRECT_SCOPE = 'chatgpt.tokens.use.direct'
USAGE_URL = 'https://chatgpt.com/settings/usage'
TERMINAL_REFRESH = {'invalid_grant','invalid_refresh_token','token_expired','refresh_token_expired',
                    'refresh_token_invalidated','refresh_token_reused'}
DEFAULT_BUDGET = {'requests':12,'input_tokens':12000,'output_tokens':3000}
MAX_BUDGET = {'requests':24,'input_tokens':24000,'output_tokens':6000}
MAX_INITIAL_INPUT_UNITS = 1048576  # One complete live-station request; UTF-8 reservation units.


def compact(value):
    return json.dumps(value, ensure_ascii=False, separators=(',',':')).encode()


def safe_code(value, fallback='upstream_error'):
    return value if isinstance(value,str) and re.fullmatch(r'[A-Za-z0-9_.:-]{1,100}',value) else fallback


class Failure(Exception):
    def __init__(self, code, status=400, upstream_status=None, request_id=None, shape=None,param=None):
        self.code = safe_code(code); self.status = status
        self.upstream_status = upstream_status
        self.request_id = safe_code(request_id,'') or None
        self.shape = shape
        self.param=param if isinstance(param,str) and re.fullmatch(r'[A-Za-z0-9_.\[\]-]{1,128}',param) else None
        super().__init__(self.code)

    def public(self):
        error={'code':self.code,'type':'chooguard_provider_error'}
        if self.upstream_status is not None:error['upstream_status']=self.upstream_status
        if self.request_id:error['request_id']=self.request_id
        if self.shape:error['body_shape']=self.shape
        if self.param:error['param']=self.param
        return {'error':error}


def upstream_failure(status, payload, request_id=None):
    code='upstream_admission_error'
    param=None
    shape='non_json'
    if isinstance(payload,dict):
        shape='error_object' if isinstance(payload.get('error'),dict) else 'detail_object' if 'detail' in payload else 'json_object'
        if isinstance(payload.get('error'),dict):code=safe_code(payload['error'].get('code'));param=payload['error'].get('param')
        elif isinstance(payload.get('error'),str):code=safe_code(payload['error'])
    return Failure(code,status,status,request_id,shape,param)


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Transport:
    """Verified TLS, fixed official origins, no redirect credential forwarding/retries."""
    def __init__(self, origins=('auth.openai.com','api.openai.com')):
        self.origins=tuple(origins)
        self.opener=urllib.request.build_opener(NoRedirect())

    def request(self, method, url, headers=None, body=None):
        parsed=urllib.parse.urlsplit(url)
        if parsed.scheme!='https' or parsed.netloc not in self.origins or parsed.username or parsed.password:
            raise Failure('untrusted_provider_endpoint')
        try:
            return self.opener.open(urllib.request.Request(url,data=body,headers=headers or {},method=method),timeout=20)
        except urllib.error.HTTPError as error:
            raw=error.read(262144)
            try:payload=json.loads(raw)
            except (ValueError,UnicodeError):payload=None
            raise upstream_failure(error.code,payload,error.headers.get('x-request-id')) from None
        except (OSError,urllib.error.URLError):
            raise Failure('provider_network_unavailable',503) from None

    def json(self, method, url, headers=None, body=None):
        response=self.request(method,url,headers,body)
        try:
            raw=response.read(2097153)
            if len(raw)>2097152:raise Failure('provider_body_too_large',502)
            value=json.loads(raw) if raw else {}
            if not isinstance(value,dict):raise Failure('provider_json_object_required',502)
            return value
        except (ValueError,UnicodeError):raise Failure('provider_invalid_json',502) from None
        finally:response.close()


class PrivateStore:
    """Only this app's protected files; never reads CLI/keychain/environment credentials."""
    def __init__(self, directory, lock=True):
        self.directory=Path(directory).expanduser()
        repo=Path(__file__).resolve().parents[3]
        if self.directory.resolve().is_relative_to(repo):raise Failure('credential_path_inside_repository')
        self.directory.mkdir(parents=True,exist_ok=True,mode=0o700)
        info=self.directory.lstat()
        if stat.S_ISLNK(info.st_mode) or info.st_uid!=os.getuid():raise Failure('unsafe_credential_directory')
        os.chmod(self.directory,0o700)
        self.lock_file=None
        if lock:
            fd=os.open(self.directory/'sidecar.lock',os.O_RDWR|os.O_CREAT|os.O_NOFOLLOW,0o600)
            self.lock_file=os.fdopen(fd,'a+')
            try:fcntl.flock(fd,fcntl.LOCK_EX|fcntl.LOCK_NB)
            except BlockingIOError:raise Failure('another_provider_process_is_running',409) from None
        path=self.directory/'accounts.json'
        if path.exists():
            self.check(path);self.data=json.loads(path.read_text())
            if self.data.get('schema')!=1:raise Failure('unsupported_credential_schema')
        else:
            self.data={'schema':1,'ext_agent_host_id':'urn:uuid:'+str(uuid.uuid4()),'accounts':{},'active':None}
            self.save()

    @staticmethod
    def check(path):
        info=path.lstat()
        if not stat.S_ISREG(info.st_mode) or info.st_uid!=os.getuid() or info.st_mode&0o077:
            raise Failure('unsafe_private_file_permissions')

    def write(self,name,value):
        target=self.directory/name
        if target.exists() or target.is_symlink():self.check(target)
        fd,temp=tempfile.mkstemp(prefix='.atomic-',dir=self.directory)
        try:
            os.fchmod(fd,0o600)
            with os.fdopen(fd,'wb') as out:out.write(compact(value));out.flush();os.fsync(out.fileno())
            os.replace(temp,target)
        finally:
            if os.path.exists(temp):os.unlink(temp)

    def save(self):self.write('accounts.json',self.data)

    def close(self):
        if self.lock_file:self.lock_file.close();self.lock_file=None


class Provider:
    def __init__(self,store,transport=None,clock=time.time,contract=OPENAI):
        self.contract=contract;self.account_scoped_quota=False
        self.store=store;self.transport=transport or Transport(contract.origins);self.clock=clock
        self.lock=threading.RLock();self.refresh_lock=threading.Lock();self.request_lock=threading.Lock()
        self.metadata=None;self.jwks=None;self.jwks_at=0
        self.pending={};self.catalog={};self.selected_model=''
        self.cloud_enabled=False;self.generation=secrets.token_urlsafe(24)
        self.usage={'requests':0,'input_tokens':0,'output_tokens':0}
        self.budget_used={'requests':0,'input_tokens':0,'output_tokens':0}
        self.failed_reserved={'input_tokens':0,'output_tokens':0}
        self.initial_allowance=None;self.initial_remaining=False
        self.initial_budget_used={'requests':0,'input_tokens':0,'output_tokens':0}
        self.initial_reserved={'requests':0,'input_tokens':0,'output_tokens':0}
        self.reserved={'requests':0,'input_tokens':0,'output_tokens':0}
        self.limits=dict(DEFAULT_BUDGET);self.last_error=None
        self.quota=self.store.data.get('quota_stop')
        self.quotas=dict(self.store.data.get('quota_stops',{}))
        self.backoff_until=0;self.temporary_failures=0
        self.capability=secrets.token_urlsafe(32);self.bootstrap=secrets.token_urlsafe(32)
        self.bootstrap_used=False;self.ui_cookie=self.browser_cookie()
        self.browser_diagnostics={'events':0,'successes':0,'failures':0,'last':None,'last_action':None}
        self.endpoint=None;self.redirect_uri=None

    def browser_cookie(self):
        # App-local browser capability only. It survives helper restart, never enters native descriptors.
        path=self.store.directory/'browser-session.json'
        if path.exists() or path.is_symlink():
            self.store.check(path)
            if path.stat().st_size>16384:raise Failure('invalid_private_browser_session')
            value=json.loads(path.read_text())
            token=value.get('cookie') if isinstance(value,dict) and value.get('schema')==1 else None
            if not isinstance(token,str) or not re.fullmatch(r'[A-Za-z0-9_-]{43}',token):raise Failure('invalid_private_browser_session')
            return token
        token=secrets.token_urlsafe(32)
        self.store.write('browser-session.json',{'schema':1,'cookie':token})
        return token

    def configure(self,port):
        self.endpoint='http://127.0.0.1:'+str(port);self.redirect_uri=self.endpoint+'/auth/callback'
        self.store.write('connection.json',{'schema':1,'endpoint':self.endpoint,'capability':self.capability,
                                          'browser_url':self.endpoint+'/#'+self.bootstrap})

    def rotate(self):
        self.generation=secrets.token_urlsafe(24)
        # One-shot consent belongs to this exact account/model/cloud session.
        self.initial_remaining=False

    def quota_for(self,account_id):
        return self.quotas.get(account_id) or (self.quota if self.quota and self.quota.get('account_id')==account_id else None)

    def callback_origins(self):return (self.contract.issuer,)

    def account(self):return self.store.data['accounts'].get(self.store.data.get('active'))

    def status(self):
        with self.lock:
            account=self.account();connected=bool(account and account.get('access_token'))
            return {'provider':self.contract.id,'connected':connected,'cloud_enabled':self.cloud_enabled,
                    'account_label':account.get('label','') if account else '', 'model':self.selected_model,
                    'session_generation':self.generation,'usage':dict(self.usage),'reserved':dict(self.reserved),
                    'budget_used':dict(self.budget_used),'failed_reserved_tokens':dict(self.failed_reserved),
                    'initial_world':{'allowance':self.initial_allowance,'remaining':int(self.initial_remaining),
                                     'budget_used':dict(self.initial_budget_used),'reserved':dict(self.initial_reserved)},
                    'limits':dict(self.limits),'plan_permission':bool(account and self.contract.required_scope in account.get('scopes',[])),
                    'error':self.last_error,'quota_stopped':bool(self.quota_for(self.store.data.get('active'))),'usage_url':self.contract.usage_url,
                    'retry_after_seconds':max(0,int(self.backoff_until-self.clock())),
                    'browser_diagnostics':{**self.browser_diagnostics,
                        'last':dict(self.browser_diagnostics['last']) if self.browser_diagnostics['last'] else None,
                        'last_action':dict(self.browser_diagnostics['last_action']) if self.browser_diagnostics['last_action'] else None},
                    'accounts':[{'id':key,'label':value['label'],'signed_in':bool(value.get('access_token')),
                                 'active':key==self.store.data.get('active')} for key,value in self.store.data['accounts'].items()]}

    def discover(self):
        if self.metadata is None:
            m=self.transport.json('GET',self.contract.issuer+'/.well-known/openid-configuration')
            if m.get('issuer')!=self.contract.issuer:raise Failure('oidc_issuer_mismatch')
            expected={'authorization_endpoint':self.contract.issuer+self.contract.authorization_path,'token_endpoint':self.contract.issuer+self.contract.token_path}
            if any(m.get(k)!=v for k,v in expected.items()):raise Failure('oidc_endpoint_mismatch')
            for key in ('jwks_uri','revocation_endpoint'):
                uri=urllib.parse.urlsplit(m.get(key,''))
                if uri.scheme!='https' or uri.netloc!=urllib.parse.urlsplit(self.contract.issuer).netloc or uri.query or uri.fragment:raise Failure('oidc_endpoint_mismatch')
            if 'RS256' not in m.get('id_token_signing_alg_values_supported',[]):raise Failure('oidc_algorithm_unavailable')
            self.metadata=m
        return self.metadata

    def verify_id(self,token,client_id,nonce=None,subject=None):
        if not isinstance(token,str) or len(token)>32768:raise Failure('invalid_id_token')
        try:header=jwt.get_unverified_header(token)
        except jwt.PyJWTError:raise Failure('invalid_id_token') from None
        if header.get('alg')!='RS256' or not isinstance(header.get('kid'),str):raise Failure('invalid_id_token_algorithm')
        metadata=self.discover()
        def key():
            if self.jwks is None or self.clock()-self.jwks_at>3600:
                self.jwks=self.transport.json('GET',metadata['jwks_uri']);self.jwks_at=self.clock()
            matches=[k for k in self.jwks.get('keys',[]) if k.get('kid')==header['kid'] and k.get('kty')=='RSA' and k.get('use','sig')=='sig' and k.get('alg','RS256')=='RS256']
            if len(matches)!=1:raise Failure('id_token_key_unavailable')
            signing=jwt.PyJWK.from_dict(matches[0],algorithm='RS256').key
            if signing.key_size<2048:raise Failure('id_token_key_too_small')
            return signing
        try:
            try:signing=key()
            except Failure:
                self.jwks=None;signing=key()  # One bounded refresh for published key rotation.
            claims=jwt.decode(token,signing,algorithms=['RS256'],issuer=self.contract.issuer,audience=client_id,leeway=30,
                              options={'require':['iss','aud','sub','exp','iat']})
            if nonce is not None and (not isinstance(claims.get('nonce'),str) or not secrets.compare_digest(claims['nonce'],nonce)):
                raise Failure('id_token_nonce_mismatch')
            if not isinstance(claims['sub'],str) or not claims['sub']:raise Failure('id_token_subject_missing')
            if subject is not None and claims['sub']!=subject:raise Failure('returning_account_identity_mismatch')
            if isinstance(claims['aud'],list) and len(claims['aud'])>1 and claims.get('azp')!=client_id:raise Failure('id_token_authorized_party_mismatch')
            return claims
        except (jwt.PyJWTError,ValueError,KeyError,TypeError):raise Failure('invalid_id_token') from None

    def auth_start(self,account_id=None,reconsent=False):
        with self.lock:
            self.discover()
            account=None
            if account_id:
                account=self.store.data['accounts'].get(account_id)
                if account is None:raise Failure('unknown_account')
            verifier=secrets.token_urlsafe(64);state=secrets.token_urlsafe(32);nonce=secrets.token_urlsafe(32)
            self.pending={key:value for key,value in self.pending.items() if value['expires']>self.clock()}
            if len(self.pending)>=4:raise Failure('too_many_auth_attempts',429)
            client=account['client_id'] if account else self.contract.client_id
            self.pending[state]={'expires':self.clock()+300,'verifier':verifier,'nonce':nonce,'client_id':client,
                                 'account_id':account_id,'redirect_uri':self.redirect_uri}
            query={'client_id':client,'ext_agent_host_id':self.store.data['ext_agent_host_id'],'response_type':'code',
                   'redirect_uri':self.redirect_uri,'scope':self.contract.scopes,'resource':self.contract.resource,'state':state,'nonce':nonce,
                   'code_challenge_method':'S256','code_challenge':base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).rstrip(b'=').decode()}
            if account:
                if account.get('id_token'):query['id_token_hint']=account['id_token']
                if account.get('email'):query['login_hint']=account['email']
            else:query['agent_name_hint']='CHOOGuard'
            if reconsent:query['prompt']='consent'  # force_reconsent deployment not assumed.
            return {'authorization_url':self.metadata['authorization_endpoint']+'?'+urllib.parse.urlencode(query), 'expires_in':300}

    def clear_tokens(self,account):
        for key in ('access_token','refresh_token','id_token','expires_at','earliest_refresh_at','scopes'):
            account.pop(key,None)

    def token_record(self,reply,account,nonce=None,initial=False):
        for field in ('access_token','refresh_token'):
            if not isinstance(reply.get(field),str) or not reply[field] or len(reply[field])>32768:raise Failure('invalid_token_response')
        if reply.get('token_type','').lower()!='bearer':raise Failure('unsupported_token_type')
        expiry=reply.get('expires_in')
        if isinstance(expiry,bool) or not isinstance(expiry,(int,float)) or not 0<expiry<=86400:raise Failure('invalid_token_expiry')
        claims=None
        if initial or reply.get('id_token'):
            claims=self.verify_id(reply.get('id_token'),account['client_id'],nonce,account.get('subject'))
        scope=reply.get('scope')
        if scope is None and not initial:scope=' '.join(account.get('scopes',[]))
        if not isinstance(scope,str):raise Failure('invalid_granted_scopes')
        replacement=dict(account)
        if claims:
            replacement.update(subject=claims['sub'],issuer=self.contract.issuer,email=claims.get('email',''))
            replacement['id_token']=reply['id_token']
            replacement['label']=(claims.get('email') or self.contract.label+' account')+' · '+account['registration_label']
        replacement.update(access_token=reply['access_token'],refresh_token=reply['refresh_token'],token_type='Bearer',
                           scopes=scope.split(),expires_at=self.clock()+expiry,saved_at=self.clock())
        earliest=reply.get('earliest_refresh_at',0)
        if not isinstance(earliest,(int,float)) or isinstance(earliest,bool):raise Failure('invalid_refresh_window')
        replacement['earliest_refresh_at']=earliest
        return replacement

    def callback(self,query):
        with self.lock:
            state=query.get('state','');pending=self.pending.pop(state,None)
            if pending is None or pending['expires']<=self.clock():raise Failure('invalid_or_expired_oauth_state')
            if query.get('error'):
                self.last_error='authorization_cancelled' if query['error']=='access_denied' else 'authorization_failed'
                raise Failure(self.last_error)
            issued=query.get('client_id')
            if pending['client_id']=='dynamic_agent_client' and self.contract.dynamic_registration:
                if not isinstance(issued,str) or not issued or issued=='dynamic_agent_client' or len(issued)>200:raise Failure('issued_client_id_missing')
                account_id=str(uuid.uuid4());registration=secrets.token_hex(3)
                account={'client_id':issued,'registration_label':registration,'label':'Registration '+registration,
                         'ext_agent_host_id':self.store.data['ext_agent_host_id']}
                # Retain only registration mapping on invalid_grant; no unverified identity becomes active.
                self.store.data['accounts'][account_id]=account;self.store.save()
            else:
                account_id=pending['account_id'] or str(uuid.uuid4())
                account=self.store.data['accounts'].get(account_id) or {'client_id':pending['client_id'],'registration_label':secrets.token_hex(3),'label':self.contract.label+' registration'}
                if issued is not None and issued!=account['client_id']:raise Failure('callback_client_id_mismatch')
            code=query.get('code')
            if not isinstance(code,str) or not code or len(code)>4096:raise Failure('authorization_code_missing')
            fields={'grant_type':'authorization_code','client_id':account['client_id'],'code':code,
                    'code_verifier':pending['verifier'],'redirect_uri':pending['redirect_uri'],'resource':self.contract.resource}
            reply=self.transport.json('POST',self.discover()['token_endpoint'],{'Content-Type':'application/x-www-form-urlencoded'},urllib.parse.urlencode(fields).encode())
            replacement=self.token_record(reply,account,pending['nonce'],True)
            self.store.data['accounts'][account_id]=replacement;self.store.data['active']=account_id;self.store.save()
            self.cloud_enabled=False;self.selected_model='';self.catalog.pop(account_id,None);self.last_error=None;self.rotate()
            return self.status()

    def access(self,account_id):
        with self.refresh_lock:
            with self.lock:
                current=self.store.data['accounts'].get(account_id)
                account=dict(current) if current else None
                if not account or not account.get('access_token'):raise Failure('sign_in_required',401)
                if self.contract.required_scope not in account.get('scopes',[]):raise Failure('chatgpt_plan_permission_required',403)
                if account['expires_at']-self.clock()>60:return account['access_token']
                if self.clock()<account.get('earliest_refresh_at',0):raise Failure('refresh_not_available_yet',503)
                fields={'grant_type':'refresh_token','client_id':account['client_id'],'refresh_token':account['refresh_token'],'resource':self.contract.resource}
            try:
                reply=self.transport.json('POST',self.discover()['token_endpoint'],{'Content-Type':'application/x-www-form-urlencoded'},urllib.parse.urlencode(fields).encode())
                replacement=self.token_record(reply,account)
            except Failure as failure:
                with self.lock:
                    if failure.code in TERMINAL_REFRESH:
                        current=self.store.data['accounts'].get(account_id)
                        if not current or current.get('refresh_token')!=account.get('refresh_token'):
                            raise Failure('session_changed_during_refresh',409) from None
                        self.clear_tokens(current);self.store.save()
                        if self.store.data.get('active')==account_id:self.cloud_enabled=False;self.rotate()
                    if self.store.data.get('active')==account_id:self.last_error=failure.code
                raise
            with self.lock:
                # Logout/account reauthorization may complete during refresh. Never resurrect old credentials.
                current=self.store.data['accounts'].get(account_id)
                if not current or current.get('refresh_token')!=account.get('refresh_token'):
                    raise Failure('session_changed_during_refresh',409)
                self.store.data['accounts'][account_id]=replacement;self.store.save()
                if self.contract.required_scope not in replacement['scopes']:
                    self.cloud_enabled=False;self.rotate();raise Failure('chatgpt_plan_permission_required',403)
                return replacement['access_token']

    def session(self,body):
        with self.lock:
            if not isinstance(body,dict):raise Failure('json_object_required')
            allowed={'account_id','model','cloud_enabled','limits','resume_after_usage_review','initial_world_allowance'}
            if set(body)-allowed:raise Failure('unsupported_session_setting')
            account_id=body.get('account_id',self.store.data.get('active'))
            account=self.store.data['accounts'].get(account_id)
            changed=account_id!=self.store.data.get('active')
            model=body.get('model','' if changed else self.selected_model)
            if not isinstance(model,str):raise Failure('invalid_model')
            if model and model not in [m['slug'] for m in self.catalog.get(account_id,[])]:raise Failure('model_not_in_selected_account_catalog')
            enabled=body.get('cloud_enabled',False if changed else self.cloud_enabled)
            if not isinstance(enabled,bool):raise Failure('explicit_cloud_choice_required')
            resume_review=body.get('resume_after_usage_review',False)
            if not isinstance(resume_review,bool):raise Failure('explicit_usage_review_required')
            quota=self.quota;account_quota=self.quota_for(account_id)
            if resume_review:
                if not account_quota:raise Failure('usage_review_must_match_stopped_account',403)
                if quota and quota.get('account_id')==account_id:quota=None
                account_quota=None
            if enabled:
                if (quota and not self.account_scoped_quota) or account_quota:raise Failure('subscription_usage_paused',429)
                if not account or not account.get('access_token'):raise Failure('sign_in_required',401)
                if self.contract.required_scope not in account.get('scopes',[]):raise Failure('chatgpt_plan_permission_required',403)
            limits=self.limits
            if 'limits' in body:
                limits=body['limits']
                if not isinstance(limits,dict) or set(limits)!=set(MAX_BUDGET):raise Failure('complete_budget_limits_required')
                for key,value in limits.items():
                    if isinstance(value,bool) or not isinstance(value,int) or not 1<=value<=MAX_BUDGET[key]:raise Failure('invalid_budget_limit')
                limits=dict(limits)
            allowance=None
            if 'initial_world_allowance' in body:
                allowance=body['initial_world_allowance']
                if not enabled:raise Failure('enable_cloud_before_initial_allowance',403)
                if not model:raise Failure('select_account_model_first',403)
                if not isinstance(allowance,dict) or set(allowance)!={'input_tokens','output_tokens'}:raise Failure('complete_initial_allowance_required')
                for key,maximum in (('input_tokens',MAX_INITIAL_INPUT_UNITS),('output_tokens',4096)):
                    if isinstance(allowance[key],bool) or not isinstance(allowance[key],int) or not 1<=allowance[key]<=maximum:raise Failure('invalid_initial_allowance')
            # Rejected settings must not clear a usage stop or partially replace limits.
            if resume_review:
                self.quotas.pop(account_id,None);self.store.data['quota_stops']=self.quotas
                self.quota=quota
                if quota is None:self.store.data.pop('quota_stop',None)
            self.limits=limits
            if changed or model!=self.selected_model or enabled!=self.cloud_enabled:self.rotate()
            if allowance is not None:self.initial_allowance=dict(allowance);self.initial_remaining=True
            self.store.data['active']=account_id;self.selected_model=model;self.cloud_enabled=enabled;self.last_error=None;self.store.save()
            return self.status()

    def guard(self,generation=None):
        if generation is not None and not secrets.compare_digest(str(generation),self.generation):raise Failure('provider_session_changed',409)
        if (self.quota and not self.account_scoped_quota) or self.quota_for(self.store.data.get('active')):raise Failure('subscription_usage_paused',429)
        if self.clock()<self.backoff_until:raise Failure('provider_retry_later',503)
        if not self.cloud_enabled:raise Failure('cloud_use_not_enabled',403)
        if not self.account() or not self.account().get('access_token'):raise Failure('sign_in_required',401)

    def record_failure(self,failure,account_id):
        with self.lock:
            if account_id!=self.store.data.get('active') and failure.upstream_status!=429 and failure.code!='subscription_sharing_usage_limit_exceeded':return
            self.last_error=failure.code
            if failure.upstream_status==429 or failure.code=='subscription_sharing_usage_limit_exceeded':
                self.quota={'account_id':account_id,'at':self.clock(),'code':failure.code};self.quotas[account_id]=self.quota;self.store.data['quota_stop']=self.quota;self.store.data['quota_stops']=self.quotas;self.store.save()
                if account_id==self.store.data.get('active'):self.cloud_enabled=False;self.rotate()
            elif failure.status==401:
                self.cloud_enabled=False;self.rotate()  # Preserve credentials unless revocation/terminal refresh confirms unusability.
            elif failure.code in ('subscription_sharing_user_not_eligible','chatpass_v2_scope_not_authorized','chatpass_v2_invalid_authorization_context'):
                self.cloud_enabled=False;self.rotate()
            elif failure.code in ('subscription_sharing_unsupported_capability','subscription_sharing_route_not_supported'):
                self.cloud_enabled=False;self.rotate()
            elif failure.status==503 and failure.code!='provider_retry_later':
                self.temporary_failures+=1;self.backoff_until=self.clock()+min(120,15*2**min(3,self.temporary_failures-1))

    def models(self):
        with self.lock:self.guard();account_id=self.store.data['active']
        return self.models_for(account_id)

    def models_for(self,account_id):
        with self.lock:
            if self.quota_for(account_id):raise Failure('subscription_usage_paused',429)
            generation=self.generation
        try:
            access=self.access(account_id)
            reply=self.transport.json('GET',self.contract.resource+'/models',{'Authorization':'Bearer '+access})
            if not isinstance(reply.get('models'),list):raise Failure('invalid_model_catalog',502)
            result=[];seen=set()
            for model in reply['models']:
                if not isinstance(model,dict):raise Failure('invalid_model_catalog',502)
                if model.get('visibility')!='list':continue
                slug=model.get('slug');label=model.get('display_name')
                if not isinstance(slug,str) or not slug or len(slug)>512 or any(ord(c)<32 for c in slug) or not isinstance(label,str) or slug in seen:raise Failure('invalid_model_catalog',502)
                seen.add(slug);result.append({'slug':slug,'display_name':label})
            with self.lock:
                if generation!=self.generation:raise Failure('provider_session_changed',409)
                self.catalog[account_id]=result
            return {'models':result,'session_generation':self.generation}
        except Failure as failure:self.record_failure(failure,account_id);raise

    def logout(self,account_id=None):
        with self.lock:
            account_id=account_id or self.store.data.get('active');account=self.store.data['accounts'].get(account_id)
            if not account:raise Failure('unknown_account')
            if self.store.data.get('active')==account_id:
                self.cloud_enabled=False;self.selected_model='';self.rotate()
            self.pending={k:v for k,v in self.pending.items() if v.get('account_id')!=account_id}
            refresh=account.get('refresh_token');client=account['client_id']
        confirmed=not refresh
        if refresh:
            for attempt in range(2):
                try:
                    body=urllib.parse.urlencode({'token':refresh,'token_type_hint':'refresh_token','client_id':client}).encode()
                    self.transport.json('POST',self.discover()['revocation_endpoint'],{'Content-Type':'application/x-www-form-urlencoded'},body);confirmed=True;break
                except Failure as failure:
                    if attempt==0 and (failure.status>=500):time.sleep(.15);continue
                    break
        with self.lock:
            current=self.store.data['accounts'][account_id]
            cleared=current.get('refresh_token')==refresh
            if cleared:self.clear_tokens(current);self.catalog.pop(account_id,None);self.store.save()
            self.last_error=('session_changed_during_logout' if not cleared else None if confirmed else 'remote_revocation_not_confirmed')
        return {**self.status(),'remote_revocation_confirmed':confirmed,'local_session_cleared':cleared}

    @staticmethod
    def read_sse(response):
        """Never return partial text: response.completed is the sole success signal."""
        data=[];text=[];bytes_read=0;started=time.monotonic();completed=None
        try:
            while True:
                if time.monotonic()-started>45:raise Failure('provider_stream_timeout',504)
                raw=response.readline(65537)
                if not raw:break
                bytes_read+=len(raw)
                if len(raw)>65536 or bytes_read>4194304:raise Failure('provider_stream_too_large',502)
                try:line=raw.decode('utf-8').rstrip('\r\n')
                except UnicodeError:raise Failure('invalid_provider_stream',502) from None
                if line.startswith('data:'):data.append(line[5:].lstrip());continue
                if line or not data:continue
                payload='\n'.join(data);data=[]
                if payload=='[DONE]':continue
                try:event=json.loads(payload)
                except ValueError:raise Failure('invalid_provider_event',502) from None
                if not isinstance(event,dict):raise Failure('invalid_provider_event',502)
                kind=event.get('type')
                if kind=='response.output_text.delta':
                    delta=event.get('delta');
                    if not isinstance(delta,str):raise Failure('invalid_provider_delta',502)
                    text.append(delta)
                elif kind in ('response.failed','response.incomplete','error'):
                    response_error=event.get('response') or event
                    if not isinstance(response_error,dict):raise Failure('invalid_provider_event',502)
                    error=response_error.get('error') or event.get('error') or {}
                    code=safe_code(error.get('code') if isinstance(error,dict) else None,'response_incomplete' if kind=='response.incomplete' else 'response_failed')
                    status=429 if code=='subscription_sharing_usage_limit_exceeded' else 503 if code=='subscription_sharing_usage_unavailable' else 502
                    raise Failure(code,status,request_id=response.headers.get('x-request-id'),shape='sse_'+kind,param=error.get('param') if isinstance(error,dict) else None)
                elif kind=='response.completed':
                    completed=event.get('response')
                    if not isinstance(completed,dict) or completed.get('status')!='completed':raise Failure('invalid_completed_response',502)
                    break
            if completed is None:raise Failure('provider_stream_truncated',502)
            final=[]
            output=completed.get('output',[])
            if not isinstance(output,list):raise Failure('invalid_provider_output',502)
            for item in output:
                if not isinstance(item,dict):raise Failure('invalid_provider_output',502)
                if item.get('type')=='message':
                    parts=item.get('content',[])
                    if not isinstance(parts,list) or any(not isinstance(c,dict) for c in parts):raise Failure('invalid_provider_output',502)
                    final.extend(c.get('text','') for c in parts if c.get('type')=='output_text')
            if any(not isinstance(value,str) for value in final):raise Failure('invalid_provider_output',502)
            content=''.join(final) if final else ''.join(text)
            if not content:raise Failure('provider_response_text_missing',502)
            usage=completed.get('usage')
            if not isinstance(usage,dict):raise Failure('provider_usage_missing',502)
            for key in ('input_tokens','output_tokens'):
                if isinstance(usage.get(key),bool) or not isinstance(usage.get(key),int) or usage[key]<0:raise Failure('provider_usage_invalid',502)
            returned_model=completed.get('model')
            if returned_model is not None and (not isinstance(returned_model,str) or not re.fullmatch(r'[A-Za-z0-9_.:-]{1,160}',returned_model)):
                raise Failure('invalid_provider_model',502)
            return content,usage,completed.get('id',''),returned_model
        except (OSError,TimeoutError):raise Failure('provider_stream_interrupted',502) from None
        finally:response.close()

    def inference_input_context(self,upstream):
        # This is the complete Responses request assembled by this adapter.
        # Server-internal framing and reasoning overhead remain unknown.
        return {'schema':'public-responses-semantic-input-v1',
                'endpoint':self.contract.resource+'/responses','request':upstream}

    def infer(self,access,upstream):
        response=self.transport.request('POST',self.contract.resource+'/responses',{'Authorization':'Bearer '+access,'Content-Type':'application/json'},compact(upstream))
        return self.read_sse(response)

    def chat(self,body,generation):
        if not self.request_lock.acquire(blocking=False):raise Failure('provider_request_already_running',409)
        reservation=None;account_id=None;settled=False;used_target=None;reserved_target=None
        try:
            with self.lock:
                self.guard(generation);account_id=self.store.data['active']
                if not self.selected_model:raise Failure('select_account_model_first')
                if not isinstance(body,dict) or set(body)-{'model','messages','max_tokens','stream','temperature','chooguard_purpose','response_format','seed'}:raise Failure('unsupported_chat_request_field')
                if body.get('stream',False) is not False:raise Failure('local_bridge_returns_completed_json_only')
                if body.get('model')!=self.selected_model:raise Failure('model_does_not_match_browser_selection',409)
                captured_model=self.selected_model
                response_format=body.get('response_format')
                if response_format is not None and response_format!={'type':'json_object'}:raise Failure('unsupported_response_format')
                requested_seed=body.get('seed')
                if requested_seed is not None and (isinstance(requested_seed,bool) or not isinstance(requested_seed,int) or not 0<=requested_seed<=2147483646):raise Failure('invalid_source_seed')
                temperature=body.get('temperature')
                if temperature is not None and (isinstance(temperature,bool) or not isinstance(temperature,(int,float)) or not 0<=temperature<=2):raise Failure('invalid_temperature')
                messages=body.get('messages')
                if not isinstance(messages,list) or not 1<=len(messages)<=32:raise Failure('invalid_chat_messages')
                for message in messages:
                    if not isinstance(message,dict) or set(message)!={'role','content'} or message['role'] not in ('system','developer','user','assistant') or not isinstance(message['content'],str):raise Failure('text_messages_only')
                purpose=body.get('chooguard_purpose','npc_plan')
                if purpose not in ('npc_plan','initial_world'):raise Failure('unsupported_generation_purpose')
                initial=purpose=='initial_world'
                if initial and not self.initial_remaining:raise Failure('initial_world_allowance_not_enabled',403)
                amount=body.get('max_tokens',2048 if initial else 256)
                maximum=self.initial_allowance['output_tokens'] if initial else 512
                if isinstance(amount,bool) or not isinstance(amount,int) or not 1<=amount<=maximum:raise Failure('invalid_output_token_limit')
                # Conservative application admission estimate, kept distinct from provider usage.
                estimate=sum(len(m['content'].encode('utf-8'))+32 for m in messages)+64
                input_limit=self.initial_allowance['input_tokens'] if initial else self.limits['input_tokens']
                if estimate>input_limit:raise Failure('local_input_budget_exceeded',429)
                candidate={'requests':1,'input_tokens':estimate,'output_tokens':amount}
                if self.usage['requests']>=MAX_BUDGET['requests']:raise Failure('local_total_request_budget_exceeded',429)
                if not initial and any(self.budget_used[key]+self.reserved[key]+candidate[key]>self.limits[key] for key in candidate):raise Failure('local_session_budget_exceeded',429)
                reservation=candidate
                used_target=self.initial_budget_used if initial else self.budget_used
                reserved_target=self.initial_reserved if initial else self.reserved
                if initial:self.initial_remaining=False
                for key in reservation:reserved_target[key]+=reservation[key]
                self.usage['requests']+=1
                used_target['requests']+=1
            access=self.access(account_id)
            with self.lock:
                self.guard(generation)
                if self.store.data.get('active')!=account_id or self.selected_model!=captured_model:raise Failure('provider_session_changed',409)
            upstream={'model':captured_model,'input':messages,'store':False,'stream':True,'max_output_tokens':amount}
            model_input_context=self.inference_input_context(upstream)
            model_input_sha256=hashlib.sha256(compact(model_input_context)).hexdigest()
            content,usage,response_id,returned_model=self.infer(access,upstream)
            with self.lock:
                self.usage['input_tokens']+=usage['input_tokens'];self.usage['output_tokens']+=usage['output_tokens']
                used_target['input_tokens']+=usage['input_tokens'];used_target['output_tokens']+=usage['output_tokens'];settled=True
                if usage['input_tokens']>estimate or usage['output_tokens']>amount:
                    self.cloud_enabled=False;self.rotate();raise Failure('provider_usage_exceeded_reservation',429)
                self.guard(generation)
                self.last_error=None
                self.temporary_failures=0;self.backoff_until=0
            if response_format is not None:
                try:object_result=json.loads(content)
                except ValueError:raise Failure('provider_json_object_missing',502) from None
                if not isinstance(object_result,dict):raise Failure('provider_json_object_missing',502)
            return {'id':safe_code(response_id,'chooguard-completed'),'object':'chat.completion','model':upstream['model'],
                    'choices':[{'index':0,'message':{'role':'assistant','content':content},'finish_reason':'stop'}],
                    'usage':{'prompt_tokens':usage['input_tokens'],'completion_tokens':usage['output_tokens'],'total_tokens':usage['input_tokens']+usage['output_tokens']},
                    'chooguard':{'session_generation':generation,'billing':'chatgpt_plan_only','purpose':purpose,
                                 'requested_seed':requested_seed,'provider_model':returned_model,
                                 'provider_input_sha256':model_input_sha256,
                                 'provider_input_contract':model_input_context['schema'],
                                 'server_internal_overhead_known':False}}
        except Failure as failure:
            if account_id:self.record_failure(failure,account_id)
            raise
        finally:
            if reservation:
                with self.lock:
                    for key in reservation:reserved_target[key]-=reservation[key]
                    # Missing usage on failed/partial streams stays conservatively charged.
                    if not settled:
                        for key in self.failed_reserved:
                            used_target[key]+=reservation[key];self.failed_reserved[key]+=reservation[key]
            self.request_lock.release()


class Server(ThreadingHTTPServer):
    daemon_threads=True
    def __init__(self,address,provider):
        self.provider=provider;super().__init__(address,Handler);provider.configure(self.server_port)
        from native_control import NativeControl
        self.native_control=NativeControl(provider) if hasattr(provider,'registry') else None


class Handler(BaseHTTPRequestHandler):
    server_version='CHOOGuardProvider/1'
    def log_message(self,*args):pass  # OAuth callback queries and headers must never enter logs.

    @property
    def provider(self):return self.server.provider

    def reply(self,status,value,cookie=None,html=False):
        path=urllib.parse.urlsplit(self.path).path
        native=secrets.compare_digest(self.headers.get('Authorization',''),'Bearer '+self.provider.capability)
        if not native and path in ('/status','/models','/session','/ui/bootstrap','/auth/start','/auth/cancel','/auth/logout'):
            error=value.get('error') if isinstance(value,dict) else None
            code=safe_code(error.get('code'),'request_failed') if isinstance(error,dict) else None
            with self.provider.lock:
                diagnostic=self.provider.browser_diagnostics
                diagnostic['events']+=1;diagnostic['successes' if status<400 else 'failures']+=1
                event={'method':self.command,'route':path,'http_status':status,'code':code,'at':self.provider.clock()}
                if path=='/session':event['setting_names']=getattr(self,'ui_setting_names',[])
                diagnostic['last']=event
                if path!='/status':diagnostic['last_action']=event
        raw=value.encode() if html else compact(value)
        self.send_response(status);self.send_header('Content-Type','text/html; charset=utf-8' if html else 'application/json')
        self.send_header('Content-Length',str(len(raw)));self.send_header('Cache-Control','no-store')
        self.send_header('Referrer-Policy','no-referrer');self.send_header('X-Content-Type-Options','nosniff')
        self.send_header('X-Frame-Options','DENY')
        if cookie:self.send_header('Set-Cookie','chooguard_ui='+cookie+'; HttpOnly; SameSite=Strict; Path=/')
        if html:self.send_header('Content-Security-Policy',"default-src 'none'; script-src 'nonce-"+self.nonce+"'; style-src 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'")
        self.end_headers();self.wfile.write(raw)

    def boundary(self):
        if self.client_address[0]!='127.0.0.1' or self.headers.get('Host')!='127.0.0.1:'+str(self.server.server_port):raise Failure('loopback_host_required',403)
        origin=self.headers.get('Origin')
        callback=self.command=='GET' and urllib.parse.urlsplit(self.path).path=='/auth/callback'
        if origin is not None and origin!=self.provider.endpoint and not (callback and origin in self.provider.callback_origins()):raise Failure('same_origin_required',403)
        if not callback and self.headers.get('Sec-Fetch-Site') in ('cross-site','same-site'):raise Failure('cross_site_request_refused',403)

    def ui_valid(self):
        try:cookie=http.cookies.SimpleCookie(self.headers.get('Cookie',''));value=cookie.get('chooguard_ui')
        except http.cookies.CookieError:return False
        return bool(value and secrets.compare_digest(value.value,self.provider.ui_cookie))

    def authorize(self,ui_only=False):
        if self.ui_valid():
            if self.command=='POST' and self.headers.get('Origin')!=self.provider.endpoint:raise Failure('same_origin_required',403)
            return
        if ui_only:raise Failure('browser_session_required',401)
        authorization=self.headers.get('Authorization','')
        if not secrets.compare_digest(authorization,'Bearer '+self.provider.capability):raise Failure('local_capability_required',401)

    def body(self):
        if self.headers.get('Transfer-Encoding'):raise Failure('transfer_encoding_not_supported')
        if self.headers.get('Content-Type','').split(';')[0]!='application/json':raise Failure('json_content_type_required',415)
        try:length=int(self.headers.get('Content-Length','-1'))
        except ValueError:raise Failure('content_length_required',411) from None
        maximum=2097152 if self.path=='/v1/chat/completions' else 524288
        if not 0<=length<=maximum:raise Failure('request_body_too_large',413)
        try:value=json.loads(self.rfile.read(length))
        except (ValueError,UnicodeError):raise Failure('invalid_json') from None
        if not isinstance(value,dict):raise Failure('json_object_required')
        self.ui_setting_names=sorted(set(value)&{'account_id','model','cloud_enabled','limits','resume_after_usage_review','initial_world_allowance'})
        return value

    def do_GET(self):self.dispatch()
    def do_POST(self):self.dispatch()
    def do_DELETE(self):self.dispatch()

    def redirect_home(self):
        self.send_response(303);self.send_header('Location','/');self.send_header('Cache-Control','no-store');self.send_header('Referrer-Policy','no-referrer');self.send_header('Content-Length','0');self.end_headers()

    def dispatch(self):
        try:
            self.boundary();path=urllib.parse.urlsplit(self.path).path
            if path.startswith('/native/v1/'):
                if not secrets.compare_digest(self.headers.get('Authorization',''),'Bearer '+self.provider.capability):raise Failure('local_capability_required',401)
                if not self.server.native_control:raise Failure('native_control_upgrade_required',409)
                if self.headers.get('Transfer-Encoding'):raise Failure('transfer_encoding_not_supported')
                if urllib.parse.urlsplit(self.path).query:raise Failure('native_control_query_not_supported')
                body=None
                if self.command=='POST':body=self.body()
                elif self.command=='DELETE':body=self.body() if self.headers.get('Content-Length') not in (None,'0') else {}
                result=self.server.native_control.dispatch(self.command,path,body,self.headers.get('X-ChooGuard-Session'))
                return self.reply(200,result)
            if self.command=='GET' and path=='/':
                self.nonce=secrets.token_urlsafe(24);return self.reply(200,(multi_page(self.nonce) if hasattr(self.provider,'registry') else page(self.nonce)),html=True)
            if self.command=='GET' and path=='/auth/callback':
                query=urllib.parse.parse_qs(urllib.parse.urlsplit(self.path).query,keep_blank_values=True)
                if any(len(values)!=1 for values in query.values()):raise Failure('ambiguous_oauth_callback')
                native=self.server.native_control
                state=query.get('state',[''])[0]
                native_flow=bool(native and native.before_callback(state))
                failure=None
                try:self.provider.callback({key:value[0] for key,value in query.items()})
                except Failure as error:
                    failure=error
                    if native:native.after_callback(state,error)
                    if error.code=='invalid_or_expired_oauth_state' and not native_flow:raise
                    with self.provider.lock:self.provider.last_error=error.code
                else:
                    if native:native.after_callback(state)
                if native_flow:
                    self.nonce=secrets.token_urlsafe(24)
                    message='로그인 절차가 완료되었습니다. 게임으로 돌아가세요.' if failure is None else '로그인을 완료하지 못했습니다. 게임으로 돌아가 연결 상태를 확인하세요.'
                    return self.reply(200,'<!doctype html><html lang="ko"><meta charset="utf-8"><title>CHOOGuard 로그인</title><p>'+message+'</p></html>',html=True)
                self.redirect_home();return
            if self.command=='POST' and path=='/ui/bootstrap':
                if self.headers.get('Origin')!=self.provider.endpoint:raise Failure('same_origin_required',403)
                body=self.body();candidate=body.get('bootstrap','')
                if self.ui_valid():return self.reply(200,self.provider.status())
                if not isinstance(candidate,str) or not secrets.compare_digest(candidate,self.provider.bootstrap) or self.provider.bootstrap_used:raise Failure('invalid_ui_bootstrap',401)
                self.provider.bootstrap_used=True;return self.reply(200,self.provider.status(),self.provider.ui_cookie)
            if self.command=='GET' and path in ('/status','/models'):
                self.authorize();return self.reply(200,self.provider.status() if path=='/status' else self.provider.models())
            if self.command=='POST' and path in ('/auth/start','/auth/logout','/auth/cancel','/auth/device/poll','/session'):
                self.authorize(ui_only=True);body=self.body()
                if path=='/auth/start':result=self.provider.auth_start(body.get('account_id'),body.get('reconsent',False),**({'provider_id':body.get('provider_id')} if body.get('provider_id') else {}))
                elif path=='/auth/logout':result=self.provider.logout(body.get('account_id'))
                elif path=='/auth/device/poll':result=self.provider.poll_device(body.get('provider_id'),body.get('state'))
                elif path=='/auth/cancel':
                    if hasattr(self.provider,'cancel_auth'):result=self.provider.cancel_auth()
                    else:
                        with self.provider.lock:self.provider.pending.clear()
                        result={'cancelled':True}
                else:result=self.provider.session(body)
                return self.reply(200,result)
            if self.command=='POST' and path=='/v1/chat/completions':
                # Bridge never accepts a browser cookie in place of its native capability.
                if not secrets.compare_digest(self.headers.get('Authorization',''),'Bearer '+self.provider.capability):raise Failure('local_capability_required',401)
                generation=self.headers.get('X-ChooGuard-Session')
                if not generation:raise Failure('provider_session_header_required',409)
                return self.reply(200,self.provider.chat(self.body(),generation))
            raise Failure('not_found',404)
        except Failure as failure:self.reply(failure.status,failure.public())
        except (BrokenPipeError,ConnectionResetError):pass
        except Exception:
            # Never interpolate exception bodies: transport/library errors may contain credentials.
            self.reply(500,Failure('internal_provider_error',500).public())


from browser_ui import page,multi_page


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--port',type=int,default=18953)
    parser.add_argument('--private-dir',type=Path,default=Path.home()/'.local/share/chooguard/provider')
    parser.add_argument('--open-browser',action='store_true')
    parser.add_argument('--github-client-id',help='Public client ID of CHOOGuard own device-enabled GitHub OAuth App; never a model API key.')
    args=parser.parse_args()
    if not 1024<=args.port<=65535:raise SystemExit('Use an unprivileged loopback port.')
    store=None;server=None
    try:
        # Scoped lifecycle owners use SIGTERM; keep protected descriptor cleanup identical to Ctrl+C.
        signal.signal(signal.SIGTERM,lambda *_: (_ for _ in ()).throw(KeyboardInterrupt()))
        from multi_provider import MultiProvider
        store=PrivateStore(args.private_dir)
        from provider_registry import Registry
        contracts=[OPENAI];factories={}
        github_client_id=args.github_client_id
        registration_file=store.directory/'provider-registrations.json'
        if not github_client_id and registration_file.exists():
            store.check(registration_file)
            if registration_file.stat().st_size>4096:raise Failure('invalid_public_provider_registration')
            registration=json.loads(registration_file.read_text())
            if set(registration)!={'schema','github_copilot_client_id'} or registration['schema']!=1:raise Failure('invalid_public_provider_registration')
            github_client_id=registration['github_copilot_client_id']
        if github_client_id:
            from copilot_adapter import contract,CopilotProvider
            contracts.append(contract(github_client_id));factories['github-copilot']=CopilotProvider
        provider=MultiProvider(store,registry=Registry(contracts),adapter_factories=factories);server=Server(('127.0.0.1',args.port),provider)
        print('CHOOGuard provider ready. Private descriptor: '+str(store.directory/'connection.json'),flush=True)
        if args.open_browser:webbrowser.open(provider.endpoint+'/#'+provider.bootstrap)
        server.serve_forever(poll_interval=.25)
    except KeyboardInterrupt:pass
    except Failure as failure:raise SystemExit(failure.code) from None
    finally:
        if server:server.server_close()
        if store:
            descriptor=store.directory/'connection.json'
            if descriptor.is_file():descriptor.unlink()
            store.close()


if __name__=='__main__':main()
