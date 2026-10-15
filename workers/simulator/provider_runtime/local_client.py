"""Protected app descriptor and loopback-only native client; never loads account/token files."""
import hashlib
import http.client
import json
import os
from pathlib import Path
import re
import stat
import tempfile
import urllib.parse
import unicodedata

DEFAULT_PRIVATE = Path.home() / '.local/share/chooguard/provider'
ROOT = Path(__file__).resolve().parents[3]


class LocalFailure(Exception):
    def __init__(self, code):
        self.code = code if isinstance(code, str) and re.fullmatch(r'[A-Za-z0-9_.:-]{1,100}', code) else 'local_runtime_error'
        super().__init__(self.code)


def canonical(value):
    return json.dumps(value, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf-8')


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def strict_json(raw):
    def pairs(items):
        value = {}
        for key, item in items:
            if key in value:
                raise ValueError('duplicate_json_key')
            value[key] = item
        return value
    try:
        return json.loads(raw, object_pairs_hook=pairs, parse_constant=lambda _: (_ for _ in ()).throw(ValueError('nonfinite_json')))
    except (ValueError, UnicodeError, TypeError):
        raise LocalFailure('invalid_json') from None


def private_directory(path, create=False):
    path = Path(path).expanduser()
    if path.resolve().is_relative_to(ROOT):
        raise LocalFailure('private_directory_must_be_outside_repository')
    if create:
        path.mkdir(parents=True, exist_ok=True, mode=0o700)
    try:
        info = path.lstat()
    except OSError:
        raise LocalFailure('provider_private_directory_missing') from None
    if not stat.S_ISDIR(info.st_mode) or info.st_uid != os.getuid() or info.st_mode & 0o077:
        raise LocalFailure('unsafe_private_directory')
    return path


def private_read(path, maximum=16384):
    path = Path(path)
    private_directory(path.parent)
    try:
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    except OSError:
        raise LocalFailure('protected_app_file_unavailable') from None
    with os.fdopen(fd, 'rb') as src:
        info = os.fstat(src.fileno())
        if not stat.S_ISREG(info.st_mode) or info.st_uid != os.getuid() or info.st_mode & 0o077:
            raise LocalFailure('unsafe_private_file')
        raw = src.read(maximum + 1)
    if len(raw) > maximum:
        raise LocalFailure('protected_app_file_too_large')
    return raw


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.is_symlink():
        raise LocalFailure('output_symlink_refused')
    fd, temporary = tempfile.mkstemp(prefix='.chooguard-', dir=path.parent)
    try:
        with os.fdopen(fd, 'wb') as dst:
            os.fchmod(dst.fileno(), 0o600)
            dst.write(canonical(value) + b'\n')
            dst.flush()
            os.fsync(dst.fileno())
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def identifier(value, minimum=1):
    return isinstance(value, str) and minimum <= len(value) <= 160 and re.fullmatch(r'[A-Za-z0-9_.:-]+', value) is not None


SELECTION_FIELDS = ('provider_id', 'account_id', 'model_id', 'session_generation', 'catalog_version',
                    'selection_version', 'billing', 'registration')


def model_identifier(value):
    return isinstance(value, str) and 1 <= len(value) <= 256 and not any(char.isspace() or unicodedata.category(char) == 'Cc' or ord(char) >= 0xd800 and ord(char) <= 0xdfff or ord(char) > 0xffff for char in value)


def selection_binding(value, providers=None):
    if (not isinstance(value, dict) or set(value) != set(SELECTION_FIELDS)
            or any(not isinstance(value[key], str) for key in SELECTION_FIELDS)
            or any(not identifier(value[key]) for key in ('provider_id', 'account_id', 'session_generation'))
            or not model_identifier(value['model_id']) or re.fullmatch(r'[0-9a-f]{64}', value['catalog_version']) is None
            or value['selection_version'] != 'provider-selection-v1' or value['billing'] != 'user_subscription'
            or value['registration'] != 'own_app_public_oauth'):
        raise LocalFailure('authorized_provider_selection_required')
    if providers is not None:
        matches = [row for row in providers if isinstance(row, dict) and row.get('id') == value['provider_id']]
        if (len(matches) != 1 or matches[0].get('registered') is not True or matches[0].get('billing') != 'user_subscription'
                or not isinstance(matches[0].get('auth_modes'), list) or 'browser_oauth' not in matches[0]['auth_modes']
                or not isinstance(matches[0].get('capabilities'), list) or not {'initial_world', 'npc_plan'}.issubset(matches[0]['capabilities'])):
            raise LocalFailure('registered_subscription_provider_required')
    return {key: value[key] for key in SELECTION_FIELDS}


def selection_record(value):
    selection = selection_binding(value)
    return {'provider_id': selection['provider_id'], 'account_ref': digest((selection['provider_id'] + '\n' + selection['account_id']).encode()),
            'model_id': selection['model_id'], 'catalog_version': selection['catalog_version'],
            'selection_version': selection['selection_version'], 'billing': selection['billing'], 'registration': selection['registration'],
            'selection_fingerprint': digest(canonical(selection))}


def validate_native_status(value, allow_legacy_readonly=False):
    if not isinstance(value, dict) or not identifier(value.get('session_generation')):
        raise LocalFailure('invalid_native_status_contract')
    for key in ('connected', 'cloud_enabled', 'plan_permission', 'quota_stopped'):
        if type(value.get(key)) is not bool:
            raise LocalFailure('invalid_native_status_contract')
    if not isinstance(value.get('initial_world'), dict) or not isinstance(value.get('usage'), dict):
        raise LocalFailure('invalid_native_status_contract')
    providers = value.get('providers')
    if allow_legacy_readonly and 'providers' not in value and 'selection' not in value and value.get('provider') == 'openai-chatgpt':
        return dict(value, protocol='legacy-chatgpt-readonly', selection=None, legacy_readonly=True)
    if not isinstance(providers, list) or not providers or len(providers) > 256:
        raise LocalFailure('provider_registry_protocol_required')
    ids = [row.get('id') for row in providers if isinstance(row, dict)]
    if len(ids) != len(providers) or any(not identifier(id) for id in ids) or len(ids) != len(set(ids)):
        raise LocalFailure('invalid_provider_registry')
    provider = value.get('provider')
    if provider not in ids and (provider != '' or value['connected'] or value['cloud_enabled']):
        raise LocalFailure('invalid_native_status_contract')
    selection = value.get('selection')
    if selection is not None:
        selection = selection_binding(selection, providers)
        if (selection['provider_id'] != provider or selection['model_id'] != value.get('model')
                or selection['session_generation'] != value['session_generation']):
            raise LocalFailure('provider_selection_status_mismatch')
    return value


class LocalClient:
    def __init__(self, descriptor):
        if not isinstance(descriptor, dict) or descriptor.get('schema') != 1:
            raise LocalFailure('invalid_connection_descriptor')
        try:
            uri = urllib.parse.urlsplit(descriptor.get('endpoint', ''))
            port = uri.port
            browser = urllib.parse.urlsplit(descriptor.get('browser_url', ''))
        except (ValueError, TypeError):
            raise LocalFailure('invalid_connection_descriptor') from None
        if (uri.scheme != 'http' or uri.hostname != '127.0.0.1' or not port or not 1024 <= port <= 65535
                or uri.username is not None or uri.password is not None or uri.path not in ('', '/') or uri.query or uri.fragment
                or uri.netloc != '127.0.0.1:' + str(port)):
            raise LocalFailure('invalid_connection_endpoint')
        self.endpoint = 'http://' + uri.netloc
        if (browser.scheme != 'http' or browser.netloc != uri.netloc or browser.path != '/'
                or browser.query or not identifier(browser.fragment, 32)):
            raise LocalFailure('invalid_connection_browser_url')
        capability = descriptor.get('capability')
        if not identifier(capability, 32):
            raise LocalFailure('invalid_local_capability')
        self._capability = capability
        self._browser_url = descriptor['browser_url']
        self.port = port
        self._selection = None

    @classmethod
    def load(cls, private=DEFAULT_PRIVATE):
        return cls(strict_json(private_read(Path(private) / 'connection.json')))

    def open_browser(self, opener):
        # The browser fragment is passed directly, never returned or printed.
        return bool(opener(self._browser_url))

    def request(self, method, path, body=None, generation=None, timeout=5):
        if (method, path) not in (('GET', '/status'), ('POST', '/v1/chat/completions')):
            raise LocalFailure('runtime_route_not_allowed')
        if method == 'POST':
            selection = selection_binding(body.get('chooguard_selection') if isinstance(body, dict) else None)
            if selection != self._selection or generation != selection['session_generation'] or body.get('model') != selection['model_id']:
                raise LocalFailure('provider_session_changed')
        raw = canonical(body) if body is not None else None
        if raw is not None and len(raw) > 524288:
            raise LocalFailure('native_request_body_too_large')
        headers = {'Authorization': 'Bearer ' + self._capability}
        if raw is not None:
            headers['Content-Type'] = 'application/json'
        if generation:
            headers['X-ChooGuard-Session'] = generation
        connection = http.client.HTTPConnection('127.0.0.1', self.port, timeout=timeout)
        try:
            connection.request(method, path, body=raw, headers=headers)
            response = connection.getresponse()
            data = response.read(2097153)
            status = response.status
        except (OSError, http.client.HTTPException):
            raise LocalFailure('local_helper_unreachable_or_timeout') from None
        finally:
            connection.close()
        if len(data) > 2097152:
            raise LocalFailure('local_reply_too_large')
        value = strict_json(data)
        if not isinstance(value, dict):
            raise LocalFailure('local_reply_object_required')
        if status != 200:
            if status == 409:
                self._selection = None
            error = value.get('error')
            code = error.get('code') if isinstance(error, dict) else None
            raise LocalFailure(code or 'local_helper_http_' + str(status))
        return value

    def status(self, allow_legacy_readonly=False):
        try:
            value = validate_native_status(self.request('GET', '/status'), allow_legacy_readonly)
            self._selection = selection_binding(value['selection'], value['providers']) if value.get('selection') is not None else None
            return value
        except LocalFailure:
            self._selection = None
            raise


def public_status(value):
    """Allowlist only: no identity, registration, native generation or credential fields."""
    usage = value.get('usage', {})
    initial = value.get('initial_world', {})
    result = {key: value.get(key) for key in ('provider', 'connected', 'cloud_enabled', 'plan_permission', 'quota_stopped')}
    result['model'] = value.get('model') if model_identifier(value.get('model')) else ''
    if value.get('selection') is not None:
        try:
            result['selection'] = selection_record(value['selection'])
        except LocalFailure:
            result['selection'] = None
    result['usage'] = {key: item for key in ('requests', 'input_tokens', 'output_tokens')
                       if type(item := usage.get(key)) is int and item >= 0}
    result['initial_attempt_allowed'] = initial.get('remaining') == 1
    allowance = initial.get('allowance')
    result['initial_allowance'] = {key: item for key in ('input_tokens', 'output_tokens')
                                  if type(item := allowance.get(key)) is int and item > 0} if isinstance(allowance, dict) else None
    result['error'] = value.get('error') if identifier(value.get('error')) else None
    diagnostic=value.get('browser_diagnostics')
    if isinstance(diagnostic,dict):
        safe={key:item for key in ('events','successes','failures') if type(item:=diagnostic.get(key)) is int and item>=0}
        for key in ('last','last_action'):
            event=diagnostic.get(key)
            if isinstance(event,dict) and event.get('route') in ('/status','/models','/session','/ui/bootstrap','/auth/start','/auth/cancel','/auth/logout'):
                safe[key]={name:event.get(name) for name in ('method','route','http_status','at')}
                safe[key]['code']=event.get('code') if identifier(event.get('code')) else None
                names=event.get('setting_names')
                if isinstance(names,list):safe[key]['setting_names']=[name for name in names if name in ('account_id','model','cloud_enabled','limits','resume_after_usage_review','initial_world_allowance')]
        result['browser_diagnostics']=safe
    return result
