"""Own-app provider contracts. A harness OAuth client is never an app registration.

Only OpenAI's documented dynamic-agent direct-subscription contract ships enabled.
Other providers stay discoverable with explicit eligibility/registration reasons.
An adapter contract is code-reviewed, not inferred from successful OAuth login.
"""
from dataclasses import dataclass


@dataclass(frozen=True)
class SubscriptionContract:
    id: str
    label: str
    issuer: str
    resource: str
    scopes: str
    required_scope: str
    authorization_path: str
    token_path: str
    usage_url: str
    client_id: str = 'dynamic_agent_client'
    dynamic_registration: bool = True
    evidence_url: str = ''
    registration: str = 'own_app_public_oauth'
    billing: str = 'user_subscription'

    @property
    def origins(self):
        from urllib.parse import urlsplit
        return tuple(sorted({urlsplit(self.issuer).netloc,urlsplit(self.resource).netloc}))


OPENAI = SubscriptionContract(
    id='openai-chatgpt',label='ChatGPT',issuer='https://auth.openai.com',
    resource='https://api.openai.com/v1',
    scopes='openid profile email offline_access resource.invoke chatgpt.tokens.use.direct',
    required_scope='chatgpt.tokens.use.direct',authorization_path='/api/accounts/authorize',
    token_path='/api/accounts/oauth/token',usage_url='https://chatgpt.com/settings/usage',
    evidence_url='https://developers.openai.com/codex/auth/')

# Registry inventory reflects harness OAuth families, not an inference entitlement claim.
UNSUPPORTED = (
    ('anthropic','Claude','provider_policy_restricted: Anthropic prohibits third-party apps from providing Claude.ai subscription login or routing those credentials. Use of the unmodified Claude Code binary is a separate exception, not this adapter.'),
    ('github-copilot','GitHub Copilot','An app-owned OAuth registration and permitted Copilot subscription inference/catalog contract are required; a harness client ID is not reused.'),
    ('google-gemini-cli','Google Gemini CLI','An app-owned OAuth registration plus the corresponding Code Assist quota/project/catalog contract is required; CLI credentials are not reused.'),
    ('google-antigravity','Google Antigravity','Own-app third-party OAuth and subscription inference/catalog entitlement is not verified; harness client IDs are not reused.'),
    ('amazon-q','Amazon Q Developer','Own-app device/OIDC registration and permitted subscription inference/catalog contract are not verified.'),
    ('kimi','Kimi','Own-app OAuth registration and account-entitled subscription inference/catalog contract are not verified.'),
    ('qwen','Qwen','Own-app OAuth registration and account-entitled subscription inference/catalog contract are not verified.'),
    ('minimax','MiniMax','Own-app OAuth registration and account-entitled subscription inference/catalog contract are not verified.'),
    ('prime-intellect','Prime Intellect','Available harness/API authentication does not establish no-extra-API-billing end-user subscription inference.'),
    ('openrouter','OpenRouter','API-credit billing does not meet the requested end-user subscription-only mode.'),
)


class Registry:
    def __init__(self, contracts=None):
        self.contracts={c.id:c for c in (contracts or (OPENAI,))}
        if len(self.contracts)!=len(contracts or (OPENAI,)):raise ValueError('duplicate_provider_contract')
        for contract in self.contracts.values():
            from urllib.parse import urlsplit
            if contract.billing!='user_subscription' or contract.registration!='own_app_public_oauth':raise ValueError('subscription_contract_required')
            if not contract.dynamic_registration and (not contract.client_id or contract.client_id=='dynamic_agent_client'):raise ValueError('own_app_public_client_required')
            for endpoint in (contract.issuer,contract.resource,contract.evidence_url):
                u=urlsplit(endpoint)
                if u.scheme!='https' or not u.netloc or u.username or u.password or u.fragment:raise ValueError('https_contract_endpoint_required')

    def rows(self):
        rows=[{'id':c.id,'label':c.label,'registered':True,'billing':'user_subscription',
               'capabilities':['initial_world','npc_plan'],'auth_modes':['browser_oauth'],
               'state':'registered','reason':'','registration':c.registration,
               'evidence_url':c.evidence_url,'usage_url':c.usage_url} for c in self.contracts.values()]
        rows.extend({'id':id,'label':label,'registered':False,'billing':'unsupported',
                     'capabilities':[],'auth_modes':[],'state':'unsupported_for_subscription_mode',
                     'reason':reason,'registration':None,'evidence_url':None,'usage_url':None}
                    for id,label,reason in UNSUPPORTED if id not in self.contracts)
        return sorted(rows,key=lambda r:r['label'])
