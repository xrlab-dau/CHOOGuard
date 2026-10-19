#!/usr/bin/env python3
"""Policy-delegated FIFO broker. Constructors own scene changes and saving.

Only the Python standard library is used. Importing this module does nothing.
There is no expiry, force-unlock, rollback, save, reopen or validation command.
"""
from __future__ import annotations

import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import fcntl
import hashlib
import json
import math
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import time
import uuid


PROJECT = Path('/Users/um-yunsang/CHOOGuard').resolve()
UNITY = Path('/Users/um-yunsang/.unity/bin/unity')
STATE = PROJECT / '.state'
POLICY = STATE / 'SCENE-WORKER-POLICY-C180.json'
AUTHORITY = STATE / 'WRITER-AUTHORITY.json'
LOCK = STATE / 'SCENE-WORKER-QUEUE-C180.lock'
QUEUE = STATE / 'SCENE-WORKER-QUEUE-C180.json'
DATA = STATE / 'SCENE-WORKER-C180'
COORDINATOR = '01a13cb4-b738-7823-bf16-30a39ea3d8c6'
POLL_SECONDS = 1.5


class BrokerError(Exception):
    pass


def utc():
    return datetime.now(timezone.utc).isoformat()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(obj):
    return json.dumps(obj, sort_keys=True, ensure_ascii=False,
                      separators=(',', ':')).encode('utf-8')


def relative(path):
    return str(Path(path).relative_to(PROJECT))


def read_json(path):
    try:
        value = json.loads(Path(path).read_text(encoding='utf-8'))
    except (OSError, ValueError) as ex:
        raise BrokerError(f'Cannot read JSON: {path}: {type(ex).__name__}') from ex
    if not isinstance(value, dict):
        raise BrokerError(f'JSON object required: {path}')
    return value


def atomic_bytes(path, payload, mode=0o600):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp = tempfile.mkstemp(prefix='.' + path.name + '.', dir=path.parent)
    try:
        with os.fdopen(fd, 'wb') as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
            os.fchmod(stream.fileno(), mode)
        os.replace(tmp, path)
        try:
            dfd = os.open(path.parent, os.O_RDONLY)
            try:
                os.fsync(dfd)
            finally:
                os.close(dfd)
        except OSError:
            pass  # Atomic rename is retained on filesystems without directory fsync.
    finally:
        if os.path.exists(tmp):
            os.unlink(tmp)


def write_json(path, obj):
    atomic_bytes(path, (json.dumps(obj, ensure_ascii=False, indent=2) + '\n').encode())


@contextmanager
def locked():
    STATE.mkdir(parents=True, exist_ok=True)
    fd = os.open(LOCK, os.O_RDWR | os.O_CREAT | getattr(os, 'O_NOFOLLOW', 0), 0o600)
    try:
        fcntl.flock(fd, fcntl.LOCK_EX)
        yield
    finally:
        fcntl.flock(fd, fcntl.LOCK_UN)
        os.close(fd)


def inside_project(value, must_exist=True):
    p = Path(value)
    p = (p if p.is_absolute() else PROJECT / p).resolve()
    try:
        p.relative_to(PROJECT)
    except ValueError as ex:
        raise BrokerError('Path escapes project, including through symlinks') from ex
    if must_exist and not p.is_file():
        raise BrokerError(f'Source file does not exist: {relative(p)}')
    return p


def name_checked(name):
    if not isinstance(name, str) or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]{0,119}', name):
        raise BrokerError('Name must be unique and use 1..120 ASCII letters/digits/._-')
    return name


def request_path(name):
    return DATA / 'requests' / (name_checked(name) + '.json')


def policy(enabled=True):
    p = read_json(POLICY)
    if p.get('coordinator_thread') != COORDINATOR:
        raise BrokerError('Policy coordinator does not match the explicitly delegated root')
    if enabled and p.get('enabled') is not True:
        raise BrokerError('C180 policy is not enabled')
    if not isinstance(p.get('allowed_workers'), list):
        raise BrokerError('Policy allowed_workers must be an array')
    return p


def script_allowed(script, roots):
    if not isinstance(roots, list):
        return False
    for item in roots:
        if not isinstance(item, str) or not item:
            continue
        root = inside_project(item, must_exist=False)
        if script == root or (root.suffix.lower() != '.cs' and script.is_relative_to(root)):
            return True
    return False


def permission(p, thread, scope, steps):
    # Thread IDs are opaque strings; classic:/root/... is intentionally supported.
    if not isinstance(thread, str) or not thread or not isinstance(scope, str) or not scope:
        raise BrokerError('Nonempty thread and scope are required')
    for worker in p['allowed_workers']:
        if not isinstance(worker, dict) or worker.get('thread_id') != thread:
            continue
        prefixes = worker.get('allowed_scope_prefixes', [])
        if not any(isinstance(x, str) and x and scope.startswith(x) for x in prefixes):
            continue
        if not all(script_allowed(inside_project(s['file']), worker.get('script_roots', []))
                   for s in steps):
            continue
        limit = worker.get('max_timeout_ms', p.get('max_timeout_ms'))
        if limit is not None and any(s['timeout_ms'] > int(limit) for s in steps):
            raise BrokerError('Requested native timeout exceeds policy limit')
        return worker
    raise BrokerError('No single whitelist entry permits this thread, scope and every script')


def queue_read():
    if not QUEUE.exists():
        return {'schema': 'C180_FIFO_QUEUE_V1', 'pending': [], 'active_request': None}
    q = read_json(QUEUE)
    if not isinstance(q.get('pending'), list) or len(set(q['pending'])) != len(q['pending']):
        raise BrokerError('Queue is malformed; coordinator intervention required')
    return q


def queue_write(q):
    q['updated_utc'] = utc()
    write_json(QUEUE, q)


def normalize_spec(args):
    raw = read_json(inside_project(args.request_json)) if args.request_json else {}
    for key in ('thread', 'scope', 'name'):
        cli = getattr(args, key)
        if cli is not None and raw.get(key) is not None and cli != raw[key]:
            raise BrokerError(f'CLI and request JSON disagree on {key}')
        raw[key] = cli if cli is not None else raw.get(key)
    name_checked(raw.get('name'))
    if not raw.get('thread') or not raw.get('scope'):
        raise BrokerError('thread, scope and name are required')
    if 'steps' in raw:
        if args.file or args.entry:
            raise BrokerError('Use batch steps or --file/--entry, not both')
        steps = raw['steps']
    else:
        steps = [{'file': args.file or raw.get('file'),
                  'entry': args.entry or raw.get('entry'),
                  'timeout_ms': args.timeout_ms or raw.get('timeout_ms', 180000)}]
    if not isinstance(steps, list) or not steps:
        raise BrokerError('A nonempty steps array is required')
    fixed = []
    for step in steps:
        if not isinstance(step, dict) or not step.get('file') or not step.get('entry'):
            raise BrokerError('Each step requires file and entry')
        source = inside_project(step['file'])
        if source.suffix.lower() != '.cs':
            raise BrokerError('Only .cs run_script sources are permitted')
        if not isinstance(step['entry'], str) or not re.fullmatch(
                r'[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*', step['entry']):
            raise BrokerError('Entry must be a C# method name, optionally qualified')
        timeout = step.get('timeout_ms', raw.get('timeout_ms', 180000))
        if not isinstance(timeout, int) or isinstance(timeout, bool) or timeout <= 0:
            raise BrokerError('timeout_ms must be a positive integer')
        fixed.append({'file': relative(source), 'entry': step['entry'], 'timeout_ms': timeout})
    return {'thread': raw['thread'], 'scope': raw['scope'], 'name': raw['name'],
            'steps': fixed, 'defer_release': bool(args.defer_release or raw.get('defer_release', False))}


def register(spec):
    with locked():
        p = policy()
        worker = permission(p, spec['thread'], spec['scope'], spec['steps'])
        rp = request_path(spec['name'])
        if rp.exists():
            raise BrokerError('Unique name already used; use run --request NAME for a queued request')
        q = queue_read()
        nonce = uuid.uuid4().hex
        steps = []
        for index, step in enumerate(spec['steps']):
            source = inside_project(step['file'])
            content = source.read_bytes()
            frozen = DATA / 'sources' / (spec['name'] + '--' + nonce) / (
                f'{index:03d}__' + source.name)
            atomic_bytes(frozen, content, mode=0o444)
            steps.append({**step, 'source_sha256': digest(content),
                          'frozen_file': relative(frozen), 'frozen_sha256': digest(content)})
        payload = {**spec, 'steps': steps, 'request_nonce': nonce,
                   'shop_id': worker.get('shop_id'), 'role': worker.get('role', 'worker')}
        req = {'schema': 'C180_SCENE_WORKER_REQUEST_V1', **payload,
               'command_source_hash': digest(canonical(payload)), 'frozen_payload': payload,
               'status': 'PENDING', 'created_utc': utc(), 'policy_sha256_at_enqueue': digest(POLICY.read_bytes())}
        write_json(rp, req)
        q['pending'].append(spec['name'])
        queue_write(q)
        return req


def request_read(name, thread):
    req = read_json(request_path(name))
    if req.get('thread') != thread:
        raise BrokerError('Request belongs to a different thread')
    if digest(canonical(req['frozen_payload'])) != req.get('command_source_hash'):
        raise BrokerError('Frozen command payload hash changed')
    for key in ('thread', 'scope', 'name', 'steps', 'request_nonce', 'shop_id', 'role', 'defer_release'):
        if req.get(key) != req['frozen_payload'].get(key):
            raise BrokerError('Request differs from its frozen payload')
    return req


def sources_fixed(req):
    for step in req['steps']:
        original, frozen = inside_project(step['file']), inside_project(step['frozen_file'])
        if digest(original.read_bytes()) != step['source_sha256']:
            raise BrokerError('Original source changed after enqueue; cancel and use a new request name')
        if digest(frozen.read_bytes()) != step['frozen_sha256']:
            raise BrokerError('Frozen execution source hash changed')


def paths_for(req):
    stem = req['name'] + '--' + req['request_nonce']
    return {kind: DATA / kind / (stem + '.json')
            for kind in ('grants', 'results', 'releases')}


def owns(auth, req):
    return (auth.get('status') == 'ACTIVE' and auth.get('Native_inflight') is True
            and auth.get('sole_writer_thread') == req['thread']
            and auth.get('scope') == req['scope']
            and auth.get('scene_worker_request') == req['name']
            and auth.get('scene_worker_grant_nonce') == req.get('grant_nonce'))


def wait_and_grant(name, thread):
    while True:
        with locked():
            req = request_read(name, thread)
            if req['status'] != 'PENDING':
                raise BrokerError('Only a pending request can run; active/unknown work is never retried')
            p = policy()
            worker = permission(p, thread, req['scope'], req['steps'])
            q = queue_read()
            if name not in q['pending']:
                raise BrokerError('Pending request is not in FIFO; coordinator intervention required')
            auth = read_json(AUTHORITY)
            free = (auth.get('status') == 'RELEASED'
                    and auth.get('Native_inflight') is False
                    and auth.get('sole_writer_thread') == p['coordinator_thread']
                    and q.get('active_request') is None)
            if q['pending'][0] == name and free:
                sources_fixed(req)
                if not UNITY.is_file():
                    raise BrokerError('Native CLI is unavailable; authority has not been granted')
                nonce = uuid.uuid4().hex
                req['grant_nonce'] = nonce
                paths = paths_for(req)
                grant = {'schema': 'C180_EXPLICIT_POLICY_DELEGATION_GRANT_V1',
                         'explicit_delegation': True, 'request': name, 'grant_nonce': nonce,
                         'coordinator_thread': p['coordinator_thread'], 'worker_thread': thread,
                         'shop_id': worker.get('shop_id'), 'role': worker.get('role', 'worker'),
                         'scope': req['scope'], 'policy_file': relative(POLICY),
                         'policy_sha256': digest(POLICY.read_bytes()),
                         'command_source_hash': req['command_source_hash'], 'steps': req['steps'],
                         'granted_utc': utc(), 'expires_utc': None,
                         'reclaim_rule': 'Only actual settled native results; never elapsed time'}
                write_json(paths['grants'], grant)
                # Unknown journal exists BEFORE native can start. A killed broker cannot silently free a lease.
                write_json(paths['results'], {'schema': 'C180_NATIVE_EXECUTION_RECEIPT_V1',
                           'request': name, 'thread': thread, 'scope': req['scope'],
                           'grant_nonce': nonce, 'grant_file': relative(paths['grants']),
                           'command_source_hash': req['command_source_hash'], 'settled': False,
                           'status': 'UNKNOWN_RESERVED_NOT_YET_STARTED_KEEP_ACTIVE', 'steps': [],
                           'created_utc': utc()})
                auth.update(status='ACTIVE', Native_inflight=True,
                            sole_writer_thread=thread,
                            sole_writer_agent=worker.get('agent_name', 'scene-worker:' + str(worker.get('shop_id', thread))),
                            scope=req['scope'], coordinator_thread=p['coordinator_thread'],
                            coordinator_hold=True, coordinator_native_hold=True,
                            release_required_before_coordinator_resume=True,
                            delegated_author_lease=relative(paths['grants']),
                            scene_worker_request=name, scene_worker_grant_nonce=nonce,
                            scene_worker_command_source_hash=req['command_source_hash'],
                            scene_worker_result_receipt=relative(paths['results']),
                            lease_started_utc=utc(), Native_completion_status='UNKNOWN_RESERVED')
                write_json(AUTHORITY, auth)
                req.update(status='ACTIVE', grant_file=relative(paths['grants']), granted_utc=utc())
                write_json(request_path(name), req)
                q['pending'].pop(0)
                q['active_request'] = name
                queue_write(q)
                return req
        time.sleep(POLL_SECONDS)


def native_command(step):
    seconds = math.ceil(step['timeout_ms'] / 1000) + 30
    command = [str(UNITY), '--json', 'command', '--project-path', str(PROJECT),
               '--timeout', str(seconds), 'run_script', '--file',
               str(inside_project(step['frozen_file'])), '--entry', step['entry'],
               '--timeout_ms', str(step['timeout_ms'])]
    return command, seconds + 10


def parse_native(returncode, stdout):
    if returncode < 0:
        return {'settled': False, 'success': False, 'reason': 'CLI_KILLED'}
    try:
        packet = json.loads(stdout)
    except (ValueError, TypeError):
        return {'settled': False, 'success': False, 'reason': 'UNPARSEABLE_ACTUAL_RESULT'}
    data = packet.get('data') if isinstance(packet, dict) else None
    inner = data.get('result') if isinstance(data, dict) else None
    if not isinstance(inner, dict) or not isinstance(inner.get('success'), bool):
        return {'settled': False, 'success': False, 'reason': 'MISSING_INNER_BOOLEAN', 'packet': packet}
    outer = packet.get('success')
    if not isinstance(outer, bool):
        return {'settled': False, 'success': False, 'reason': 'MISSING_OUTER_BOOLEAN', 'packet': packet}
    errors = [packet.get('errors'), packet.get('error'), data.get('error'), data.get('errors'),
              inner.get('error'), inner.get('errorDetails'), inner.get('errors')]
    error_text = json.dumps(errors, ensure_ascii=False).lower()
    if re.search(r'time[\s_-]*out|timed[\s_-]*out|cancelled|canceled|execution[\s_-]*aborted', error_text):
        return {'settled': False, 'success': False, 'reason': 'NATIVE_TIMEOUT_OR_CANCELLATION', 'packet': packet}
    return {'settled': True, 'success': returncode == 0 and outer and inner['success'],
            'reason': 'SETTLED_SUCCESS' if returncode == 0 and outer and inner['success'] else 'SETTLED_FAILURE',
            'outer_success': outer, 'inner_success': inner['success'], 'packet': packet}


def preserve_unknown(req, result):
    result.update(settled=False, status='UNKNOWN_KEEP_ACTIVE_NO_NEXT_WRITER', updated_utc=utc())
    write_json(paths_for(req)['results'], result)
    with locked():
        a = read_json(AUTHORITY)
        if owns(a, req):
            a['Native_completion_status'] = 'UNKNOWN_KEEP_ACTIVE_NO_SECOND_WRITER'
            a['scene_worker_result_receipt'] = relative(paths_for(req)['results'])
            write_json(AUTHORITY, a)
            req['status'] = 'UNKNOWN'
            write_json(request_path(req['name']), req)


def release_settled(req, result):
    paths = paths_for(req)
    if result.get('settled') is not True or result.get('grant_nonce') != req.get('grant_nonce'):
        raise BrokerError('Cannot release without the exact settled result receipt')
    if not result.get('steps') or not all(s.get('parsed', {}).get('settled') is True for s in result['steps']):
        raise BrokerError('Cannot release: an actual native step is missing or unsettled')
    with locked():
        p = policy(enabled=False)
        auth = read_json(AUTHORITY)
        q = queue_read()
        if not owns(auth, req):
            raise BrokerError('Owner/scope/grant nonce changed; authority is not reclaimed')
        if q.get('active_request') not in (None, req['name']):
            raise BrokerError('Active queue owner changed; authority is not reclaimed')
        release = {'schema': 'C180_SETTLED_EXPLICIT_COORDINATOR_RECLAIM_V1',
                   'request': req['name'], 'grant_nonce': req['grant_nonce'],
                   'released_worker_thread': req['thread'], 'scope': req['scope'],
                   'coordinator_thread': p['coordinator_thread'], 'policy_file': relative(POLICY),
                   'policy_sha256_at_reclaim': digest(POLICY.read_bytes()),
                   'grant_file': relative(paths['grants']), 'result_receipt': relative(paths['results']),
                   'result_receipt_sha256': digest(paths['results'].read_bytes()),
                   'actual_native_steps_settled': True, 'native_success': result['success'],
                   'released_utc': utc(), 'time_or_expiry_reclaim': False}
        write_json(paths['releases'], release)
        req.update(status='FINISHED_SUCCESS' if result['success'] else 'FINISHED_SETTLED_FAILURE',
                   release_receipt=relative(paths['releases']), completed_utc=utc())
        write_json(request_path(req['name']), req)
        q['active_request'] = None
        queue_write(q)
        # Authority is freed LAST, after durable result and release receipts.
        auth.update(status='RELEASED', Native_inflight=False,
                    sole_writer_thread=p['coordinator_thread'],
                    sole_writer_agent=p.get('coordinator_agent', '/root'),
                    scope=p.get('coordinator_released_scope', 'C180_COORDINATOR_RELEASED'),
                    coordinator_hold=False, coordinator_native_hold=False,
                    release_required_before_coordinator_resume=False,
                    delegated_author_lease=None, scene_worker_request=None,
                    scene_worker_grant_nonce=None, Native_completion_status='SETTLED',
                    last_scene_worker_grant=relative(paths['grants']),
                    latest_native_result_receipt=relative(paths['results']),
                    lease_release_receipt=relative(paths['releases']),
                    coordinator_reclaimed_utc=utc(), coordinator_reclaimed_from_thread=req['thread'],
                    coordinator_reclaimed_after_settled_receipt=relative(paths['results']))
        write_json(AUTHORITY, auth)
    return release


def execute(req):
    paths = paths_for(req)
    result = read_json(paths['results'])
    try:
        for index, step in enumerate(req['steps']):
            with locked():
                live = read_json(AUTHORITY)
                if not owns(live, req):
                    raise BrokerError('Exact active writer grant changed before native execution')
                permission(policy(), req['thread'], req['scope'], req['steps'])
                sources_fixed(req)
            command, timeout = native_command(step)
            result.update(status='UNKNOWN_NATIVE_IN_PROGRESS_KEEP_ACTIVE', active_step=index,
                          active_command=command, active_step_started_utc=utc())
            write_json(paths['results'], result)
            begun = utc()
            try:
                cp = subprocess.run(command, cwd=PROJECT, capture_output=True, text=True,
                                    encoding='utf-8', errors='replace', timeout=timeout,
                                    start_new_session=True)
            except subprocess.TimeoutExpired as ex:
                decode = lambda x: x.decode('utf-8', errors='replace') if isinstance(x, bytes) else x
                result['steps'].append({'index': index, 'command': command, 'started_utc': begun,
                                        'ended_utc': utc(), 'parsed': {'settled': False, 'success': False,
                                        'reason': 'CLI_PROCESS_TIMEOUT'}, 'stdout': decode(ex.stdout),
                                        'stderr': decode(ex.stderr), 'subprocess_timeout_seconds': timeout})
                preserve_unknown(req, result)
                return result
            parsed = parse_native(cp.returncode, cp.stdout)
            result['steps'].append({'index': index, 'command': command,
                                    'original_script': step['file'], 'source_sha256': step['source_sha256'],
                                    'frozen_script': step['frozen_file'], 'frozen_sha256': step['frozen_sha256'],
                                    'started_utc': begun, 'ended_utc': utc(), 'returncode': cp.returncode,
                                    'stdout': cp.stdout, 'stderr': cp.stderr, 'parsed': parsed})
            if not parsed['settled']:
                preserve_unknown(req, result)
                return result
            write_json(paths['results'], result)
            if not parsed['success']:
                break  # No further native steps after an actual settled failure.
        result.update(settled=True, success=(len(result['steps']) == len(req['steps']) and
                                           all(s['parsed']['success'] for s in result['steps'])),
                      status='SETTLED', requested_steps=len(req['steps']),
                      completed_steps=len(result['steps']), completed_utc=utc())
        if req['defer_release']:
            result['status'] = 'SETTLED_HELD_AWAITING_EXPLICIT_FINISH'
        write_json(paths['results'], result)
        if req['defer_release']:
            with locked():
                live = read_json(AUTHORITY)
                if not owns(live, req):
                    raise BrokerError('Active grant changed before explicit-finish hold')
                live['Native_completion_status'] = 'SETTLED_HELD_AWAITING_EXPLICIT_FINISH'
                write_json(AUTHORITY, live)
                req['status'] = 'SETTLED_HELD'
                write_json(request_path(req['name']), req)
        else:
            release_settled(req, result)
        return result
    except BaseException as ex:
        # Includes interruption/launch/IO failures. Never infer remote completion from CLI death.
        result['broker_error'] = {'type': type(ex).__name__, 'message': str(ex)}
        preserve_unknown(req, result)
        return result


def emit(value):
    print(json.dumps(value, ensure_ascii=False), flush=True)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    for verb in ('request', 'run'):
        p = sub.add_parser(verb)
        p.add_argument('--thread')
        p.add_argument('--scope')
        p.add_argument('--file')
        p.add_argument('--entry')
        p.add_argument('--name')
        p.add_argument('--timeout-ms', type=int)
        p.add_argument('--request-json', '--json', dest='request_json')
        p.add_argument('--defer-release', action='store_true')
        if verb == 'run':
            p.add_argument('--request', dest='existing_request')
    for verb in ('finish', 'cancel'):
        p = sub.add_parser(verb)
        p.add_argument('--thread', required=True)
        p.add_argument('--request', '--name', dest='existing_request', required=True)
        if verb == 'finish':
            p.add_argument('--nonce', required=True)
    args = parser.parse_args(argv)
    if args.command == 'cancel':
        with locked():
            req = read_json(request_path(args.existing_request))
            p = policy(enabled=False)
            if args.thread not in (req['thread'], p['coordinator_thread']):
                raise BrokerError('Only request owner or coordinator can cancel a pending request')
            if req['status'] != 'PENDING':
                raise BrokerError('Only pending work may be cancelled; active/unknown grants cannot be cancelled')
            q = queue_read()
            q['pending'] = [x for x in q['pending'] if x != req['name']]
            req.update(status='CANCELLED_BEFORE_GRANT', cancelled_utc=utc(), cancelled_by=args.thread)
            write_json(request_path(req['name']), req)
            queue_write(q)
        emit({'status': 'CANCELLED_BEFORE_GRANT', 'request': req['name']})
        return 0
    if args.command == 'finish':
        req = request_read(args.existing_request, args.thread)
        if req.get('grant_nonce') != args.nonce:
            raise BrokerError('Finish requires exact grant nonce')
        result = read_json(paths_for(req)['results'])
        release = release_settled(req, result)
        emit({'status': 'RELEASED_TO_COORDINATOR', 'request': req['name'],
              'release_receipt': relative(paths_for(req)['releases'])})
        return 0 if release['native_success'] else 1
    if getattr(args, 'existing_request', None):
        if not args.thread or any([args.scope, args.file, args.entry, args.name,
                                   args.request_json, args.defer_release, args.timeout_ms is not None]):
            raise BrokerError('run --request NAME takes only --thread; queued payload is immutable')
        req = request_read(args.existing_request, args.thread)
    else:
        req = register(normalize_spec(args))
    if args.command == 'request':
        emit({'status': 'QUEUED', 'request': req['name'],
              'request_file': relative(request_path(req['name']))})
        return 0
    req = wait_and_grant(req['name'], req['thread'])
    result = execute(req)
    emit({'status': result['status'], 'request': req['name'], 'grant_nonce': req['grant_nonce'],
          'settled': result.get('settled', False), 'success': result.get('success', False),
          'grant_file': relative(paths_for(req)['grants']),
          'result_receipt': relative(paths_for(req)['results']),
          'release_receipt': relative(paths_for(req)['releases']) if result.get('settled') and not req['defer_release'] else None})
    return 3 if not result.get('settled') else (0 if result.get('success') else 1)


if __name__ == '__main__':
    try:
        sys.exit(main())
    except BrokerError as exc:
        print('scene_worker: ' + str(exc), file=sys.stderr)
        sys.exit(2)
    except KeyboardInterrupt:
        print('scene_worker: interrupted; no authority is automatically released', file=sys.stderr)
        sys.exit(130)
    except Exception as exc:
        # Do not dump constructor source, credentials or a traceback to user output.
        print('scene_worker: ' + type(exc).__name__ + '; no automatic authority release', file=sys.stderr)
        sys.exit(2)
