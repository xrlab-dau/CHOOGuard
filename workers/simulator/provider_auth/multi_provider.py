"""One immutable play selection across independent end-user OAuth accounts.

This facade never reads another application's credentials. Provider adapters own their
own tokens, scopes, account catalogs and quota state; there is no automatic fallback.
"""
import hashlib
import secrets
import threading
from provider_registry import Registry
from sidecar import Provider, Failure, compact

SELECTION_VERSION='provider-selection-v1'
SELECTION_KEYS={'provider_id','account_id','model_id','session_generation','catalog_version',
                'selection_version','billing','registration'}


class ScopedStore:
    """Provider-private view, persisted atomically in the existing app-owned store."""
    def __init__(self, root, provider_id, lock):
        self.root=root;self.directory=root.directory;self.provider_id=provider_id;self.lock=lock
        state=root.data.get('provider_states',{}).get(provider_id,{})
        self.data={'schema':1,'ext_agent_host_id':root.data['ext_agent_host_id'],
                   'accounts':{k:v for k,v in root.data['accounts'].items()
                               if v.get('provider_id','openai-chatgpt')==provider_id},
                   'active':state.get('active')}
        if root.data.get('active') in self.data['accounts']:self.data['active']=root.data['active']
        if state.get('quota_stops'):self.data['quota_stops']=state['quota_stops']
        if state.get('quota_stop'):self.data['quota_stop']=state['quota_stop']
        elif 'quota_stop' not in state and root.data.get('quota_stop') and provider_id=='openai-chatgpt':self.data['quota_stop']=root.data['quota_stop']

    def check(self,path):return self.root.check(path)
    def write(self,name,value):return self.root.write(name,value)

    def save(self):
        with self.lock:
            for id,account in self.data['accounts'].items():
                account['provider_id']=self.provider_id
                self.root.data['accounts'][id]=account
            self.root.data.setdefault('provider_states',{})[self.provider_id]={
                'active':self.data.get('active'),'quota_stop':self.data.get('quota_stop'),
                'quota_stops':self.data.get('quota_stops',{})}
            self.root.save()


class MultiProvider(Provider):
    def __init__(self,store,transports=None,clock=None,registry=None,adapter_factories=None):
        kwargs={} if clock is None else {'clock':clock}
        super().__init__(store,**kwargs)
        from pathlib import Path
        self.implementation={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in Path(__file__).parent.glob('*.py') if not p.name.startswith('test_')}
        self.registry=registry or Registry();self.adapters={};self.catalog_versions={}
        # Public loaded implementation/adapter contract, never account or token data.
        # Catalog binding must invalidate saved material when a helper's prompt
        # transformation, endpoint mapping or pinned SDK implementation changes.
        self.inference_contracts={id:hashlib.sha256(compact({
            'schema':'helper-inference-contract-v1','provider_id':id,
            'issuer':contract.issuer,'resource':contract.resource,
            'billing':contract.billing,'registration':contract.registration,
            'helper_implementation':self.implementation})).hexdigest()
            for id,contract in self.registry.contracts.items()}
        self.catalog_errors={};self.selected_account=None;self.selected_provider=None
        self.global_attempts=0
        for id,contract in self.registry.contracts.items():
            view=ScopedStore(store,id,self.lock)
            factory=(adapter_factories or {}).get(id,Provider)
            adapter=factory(view,transport=(transports or {}).get(id),contract=contract,**kwargs)
            adapter.lock=self.lock;adapter.account_scoped_quota=True
            self.adapters[id]=adapter
        old_active=store.data.get('active')
        for id,adapter in self.adapters.items():
            if old_active in adapter.store.data['accounts']:
                self.selected_provider=id;self.selected_account=old_active;break

    def configure(self,port):
        super().configure(port)
        for adapter in self.adapters.values():
            adapter.endpoint=self.endpoint;adapter.redirect_uri=self.redirect_uri

    def callback_origins(self):return tuple(c.issuer for c in self.registry.contracts.values())

    def selected(self):return self.adapters.get(self.selected_provider)

    def account(self):
        adapter=self.selected()
        return adapter.store.data['accounts'].get(self.selected_account) if adapter else None

    def selection(self):
        adapter=self.selected()
        if not adapter or not adapter.selected_model:return None
        version=self.catalog_versions.get((self.selected_provider,self.selected_account))
        if not version:return None
        return {'provider_id':self.selected_provider,'account_id':self.selected_account,
                'model_id':adapter.selected_model,'session_generation':self.generation,
                'catalog_version':version,'selection_version':SELECTION_VERSION,
                'billing':'user_subscription','registration':'own_app_public_oauth'}

    def status(self):
        with self.lock:
            adapter=self.selected()
            value=adapter.status() if adapter else super().status()
            value.update(provider=self.selected_provider or '',session_generation=self.generation,
                         selection=self.selection(),providers=self.registry.rows(),
                         inference_contracts=dict(self.inference_contracts),
                         protocol='multi-provider-subscription-v1',helper_implementation=self.implementation)
            accounts=[]
            for id,child in self.adapters.items():
                for account_id,account in child.store.data['accounts'].items():
                    connected=bool(account.get('access_token'))
                    permission=child.contract.required_scope in account.get('scopes',[])
                    catalog=child.catalog.get(account_id)
                    stopped=bool(child.quota_for(account_id))
                    error=self.catalog_errors.get((id,account_id))
                    inference=('sign_in_required' if not connected else 'permission_required' if not permission else
                               'quota_stopped' if stopped else 'catalog_unavailable' if error else
                               'catalog_required' if catalog is None else 'no_entitled_models' if not any(m.get('selectable',True) for m in catalog) else 'catalog_eligible_quota_checked_on_request')
                    accounts.append({'id':account_id,'provider_id':id,'label':account.get('label',id),
                        'signed_in':connected,'active':id==self.selected_provider and account_id==self.selected_account,
                        'login_state':'signed_in' if connected else 'signed_out',
                        'catalog_state':'ready' if catalog is not None else 'not_loaded',
                        'inference_state':inference,
                        'catalog_version':self.catalog_versions.get((id,account_id)),
                        'error':error})
            value['accounts']=accounts
            value['total_admitted_attempts']=self.global_attempts
            value['error']=self.last_error or value.get('error')
            return value

    def auth_start(self,account_id=None,reconsent=False,provider_id=None):
        with self.lock:
            if account_id:
                found=[id for id,a in self.adapters.items() if account_id in a.store.data['accounts']]
                if len(found)!=1:raise Failure('unknown_account')
                if provider_id and provider_id!=found[0]:raise Failure('provider_account_mismatch')
                provider_id=found[0]
            provider_id=provider_id or self.selected_provider or 'openai-chatgpt'
            adapter=self.adapters.get(provider_id)
            if not adapter:raise Failure('provider_not_available_for_subscription_mode',403)
            result=adapter.auth_start(account_id,reconsent)
            result['provider_id']=provider_id
            return result

    def callback(self,query):
        state=query.get('state','')
        with self.lock:
            matches=[(id,a) for id,a in self.adapters.items() if state in a.pending]
            if len(matches)!=1:raise Failure('invalid_or_expired_oauth_state')
            provider_id,adapter=matches[0]
            previous=adapter.store.data.get('active')
            preserved=self._preserve_selected(adapter)
            pending_account=adapter.pending[state].get('account_id')
            result=adapter.callback(query)
            new_account=adapter.store.data['active']
            self.catalog_versions.pop((provider_id,new_account),None)
            if self.selected_account is None:
                self.selected_provider=provider_id;self.selected_account=new_account
                self.store.data['active']=new_account;self.store.save();self.rotate()
            elif provider_id==self.selected_provider:
                # Adding a second login preserves the already selected account. Reauthorizing
                # that exact account invalidates the previous play generation and consent.
                adapter.store.data['active']=self.selected_account
                if pending_account!=self.selected_account:self._restore_selected(adapter,preserved)
                if pending_account==self.selected_account:
                    adapter.cloud_enabled=False;adapter.selected_model='';self.rotate()
                adapter.store.save()
            self.last_error=None
            return self.status()

    @staticmethod
    def _preserve_selected(adapter):
        return {key:getattr(adapter,key) for key in ('selected_model','cloud_enabled','generation','initial_remaining','initial_allowance')}

    @staticmethod
    def _restore_selected(adapter,preserved):
        for key,value in preserved.items():setattr(adapter,key,value)

    def poll_device(self,provider_id,state):
        adapter=self.adapters.get(provider_id)
        if not adapter or not hasattr(adapter,'poll_device'):raise Failure('provider_device_flow_not_available',403)
        with self.lock:
            pending=adapter.pending.get(state)
            pending_account=pending.get('account_id') if pending else None
        # Auth commits only its own account tokens. It NEVER restores a captured
        # selection, model, generation or consent after a network wait.
        result=adapter.poll_device(state,activate=False)
        if result.get('pending'):return result
        account_id=result['account_id']
        with self.lock:
            self.catalog_versions.pop((provider_id,account_id),None)
            if self.selected_account is None:
                self.selected_provider=provider_id;self.selected_account=account_id
                adapter.store.data['active']=account_id
                self.store.data['active']=account_id;self.store.save();self.rotate()
            elif provider_id==self.selected_provider and pending_account==self.selected_account:
                # Reauthorizing the CURRENT selected account changes authorization.
                # Preserve any newer model choice, but require fresh consent/session.
                adapter.cloud_enabled=False;adapter.rotate();self.rotate()
                adapter.store.save()
        return {'pending':False,'status':self.status()}

    def models(self):
        # Catalog discovery is independent from selecting a model or allowing inference.
        result=[]
        for provider_id,adapter in self.adapters.items():
            with self.lock:
                accounts=[id for id,a in adapter.store.data['accounts'].items() if a.get('access_token')]
            for account_id in accounts:
                try:
                    rows=adapter.models_for(account_id)['models']
                    version=hashlib.sha256(compact({'provider_id':provider_id,'account_id':account_id,
                        'models':sorted(rows,key=lambda m:m['slug']),'billing':'user_subscription',
                        'inference_contract_sha256':self.inference_contracts[provider_id],
                        'capabilities':['initial_world','npc_plan']})).hexdigest()
                    with self.lock:
                        old=self.catalog_versions.get((provider_id,account_id))
                        self.catalog_versions[(provider_id,account_id)]=version
                        self.catalog_errors.pop((provider_id,account_id),None)
                        if old and old!=version and provider_id==self.selected_provider and account_id==self.selected_account:
                            adapter.cloud_enabled=False;adapter.rotate();self.rotate()
                    result.extend({'provider_id':provider_id,'account_id':account_id,'model_id':m['slug'],
                        'slug':m['slug'],'display_name':m['display_name'],'catalog_version':version,
                        'billing':'user_subscription','capabilities':['initial_world','npc_plan'],
                        'selectable':m.get('selectable',True),'reason':m.get('reason',''),
                        'policy_state':m.get('policy_state','account_catalog_listed'),
                        'subscription_usage':m.get('subscription_usage',{})} for m in rows)
                except Failure as failure:
                    with self.lock:
                        self.catalog_errors[(provider_id,account_id)]=failure.code
                        adapter.catalog.pop(account_id,None)
                        self.catalog_versions.pop((provider_id,account_id),None)
                        if provider_id==self.selected_provider and account_id==self.selected_account:
                            adapter.cloud_enabled=False;adapter.rotate();self.rotate()
        return {'models':result,'selection':self.selection(),'session_generation':self.generation,
                'accounts':self.status()['accounts']}

    def session(self,body):
        if not isinstance(body,dict):raise Failure('json_object_required')
        allowed={'provider_id','account_id','model','catalog_version','cloud_enabled','limits',
                 'resume_after_usage_review','initial_world_allowance'}
        if set(body)-allowed:raise Failure('unsupported_session_setting')
        with self.lock:
            provider_id=body.get('provider_id',self.selected_provider)
            account_id=body.get('account_id',self.selected_account)
            adapter=self.adapters.get(provider_id)
            if not adapter:raise Failure('provider_not_available_for_subscription_mode',403)
            if account_id not in adapter.store.data['accounts']:raise Failure('provider_account_mismatch')
            changed=provider_id!=self.selected_provider or account_id!=self.selected_account
            version=self.catalog_versions.get((provider_id,account_id))
            if body.get('catalog_version') is not None and body['catalog_version']!=version:raise Failure('provider_catalog_changed',409)
            if body.get('model') and not version:raise Failure('account_model_catalog_required',409)
            if body.get('model'):
                row=next((m for m in adapter.catalog.get(account_id,[]) if m['slug']==body['model']),None)
                if row and row.get('selectable',True) is not True:raise Failure('account_model_not_available_for_subscription',403)
            old=(self.selected_provider,self.selected_account,adapter.selected_model,adapter.cloud_enabled)
            args={k:v for k,v in body.items() if k not in ('provider_id','catalog_version')}
            args['account_id']=account_id
            if changed and 'model' not in args:args['model']=''
            if changed and 'cloud_enabled' not in args:args['cloud_enabled']=False
            adapter.session(args)
            # Only commit global selection after every requested setting validates.
            prior=self.selected()
            if changed and prior and prior is not adapter:
                prior.cloud_enabled=False;prior.rotate()
            self.selected_provider=provider_id;self.selected_account=account_id
            self.store.data['active']=account_id;self.store.save()
            new=(provider_id,account_id,adapter.selected_model,adapter.cloud_enabled)
            if old!=new:self.rotate()
            self.last_error=None
            return self.status()

    def clear_selection(self):
        with self.lock:
            previous=self.selected()
            if previous:
                previous.cloud_enabled=False;previous.selected_model='';previous.rotate()
            self.selected_provider=None;self.selected_account=None
            self.store.data['active']=None;self.store.save();self.rotate()
            self.last_error=None
            return self.status()

    def logout(self,account_id=None):
        with self.lock:
            account_id=account_id or self.selected_account
            matches=[(id,a) for id,a in self.adapters.items() if account_id in a.store.data['accounts']]
            if len(matches)!=1:raise Failure('unknown_account')
            provider_id,adapter=matches[0]
            selected=provider_id==self.selected_provider and account_id==self.selected_account
            if selected:self.rotate()
        result=adapter.logout(account_id)
        with self.lock:
            self.catalog_versions.pop((provider_id,account_id),None)
            if selected:self.last_error=result.get('error')
        return {**self.status(),'remote_revocation_confirmed':result['remote_revocation_confirmed'],
                'local_session_cleared':result['local_session_cleared']}

    def cancel_auth(self):
        with self.lock:
            for adapter in self.adapters.values():adapter.pending.clear()
        return {'cancelled':True}

    def chat(self,body,generation):
        if not self.request_lock.acquire(blocking=False):raise Failure('provider_request_already_running',409)
        try:
            with self.lock:
                if not isinstance(body,dict):raise Failure('json_object_required')
                if not secrets.compare_digest(str(generation),self.generation):raise Failure('provider_session_changed',409)
                selected=self.selection();adapter=self.selected()
                if not selected or not adapter:raise Failure('select_account_model_first',403)
                binding=body.get('chooguard_selection')
                if not isinstance(binding,dict) or set(binding)!=SELECTION_KEYS or binding!=selected:raise Failure('provider_selection_mismatch',409)
                if self.global_attempts>=24:raise Failure('local_total_request_budget_exceeded',429)
                captured=dict(selected);child_generation=adapter.generation
                routed={k:v for k,v in body.items() if k!='chooguard_selection'}
                # Delegate identical selected provider/account/model for BOTH purposes.
                before=adapter.usage['requests']
            try:
                result=adapter.chat(routed,child_generation)
            except Failure:
                with self.lock:
                    if self.selection()!=captured:raise Failure('provider_session_changed',409) from None
                raise
            finally:
                with self.lock:self.global_attempts+=adapter.usage['requests']-before
            with self.lock:
                if self.selection()!=captured:raise Failure('provider_session_changed',409)
                result['chooguard'].update(session_generation=self.generation,billing='user_subscription',
                                           selection=captured,inference_contract_sha256=self.inference_contracts[captured['provider_id']])
                return result
        finally:self.request_lock.release()
