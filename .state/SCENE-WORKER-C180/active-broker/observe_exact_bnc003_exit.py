#!/usr/bin/env python3
"""One exact held-nonce execution-exit recovery. Never a writer or expiry unlock.

The original synchronous source ends with terminal save receipt then return.
A fixed, non-mutating callback on the same Pipeline main-thread dispatcher proves
that source body has exited. Original transport timeout stays a settled failure;
the actual saved application receipt remains separate. No scene inspection.
"""
import json
import subprocess
from pathlib import Path
import scene_worker as broker

NAME = 'r25-bnc-east-exact-branch-correction-003'
NONCE = '2fe6a838228845adb1a368cb77c58d4e'
SOURCE_SHA = '8782f21349a60b620e65dc27fc03a04d3f76a4af392eec29afc99cd62ad08956'
SAVE_SHA = 'ef697798362d43623c2ff691c970855ffdf702080aac5770751f9860a1690b34'
BASE = broker.PROJECT / '.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/bnc-east/east-correction003'
SAVE = BASE / 'APPLIED-EAST-BRANCH-CORRECTION-003.json'
OUT = BASE / 'exact-exit-recovery'
MARKER = NAME + ':' + NONCE + ':MAIN_THREAD_CALLBACK_ENTERED'
CALLBACK = '''public static class C180ExactBnc003ExitObservation {
 public static string Observe() {
  using (var sha = System.Security.Cryptography.SHA256.Create()) {
   var actual = System.BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes(@"SAVE_PATH"))).Replace("-", "").ToLowerInvariant();
   if (actual != "SAVE_DIGEST") throw new System.InvalidOperationException("Exact terminal save identity mismatch");
  return "MARKER_VALUE";
  }
 }
}
'''.replace('MARKER_VALUE', MARKER).replace('SAVE_PATH', str(SAVE)).replace('SAVE_DIGEST', SAVE_SHA)


def held():
    req = broker.read_json(broker.request_path(NAME))
    auth = broker.read_json(broker.AUTHORITY)
    q = broker.queue_read()
    if req.get('status') != 'UNKNOWN' or req.get('grant_nonce') != NONCE:
        raise broker.BrokerError('Exact unknown request/nonce not held')
    if not broker.owns(auth, req) or q.get('active_request') != NAME:
        raise broker.BrokerError('Exact original writer reservation changed')
    if len(req['steps']) != 1 or req['steps'][0]['frozen_sha256'] != SOURCE_SHA:
        raise broker.BrokerError('Not the exact reviewed synchronous source')
    broker.sources_fixed(req)
    if broker.digest(SAVE.read_bytes()) != SAVE_SHA:
        raise broker.BrokerError('Terminal save receipt changed')
    receipt = broker.read_json(SAVE)
    if receipt.get('status') != 'OWN_EXACT_EAST_BRANCH_CORRECTION_APPLIED_AND_SAVED':
        raise broker.BrokerError('Missing actual terminal saved receipt')
    if receipt.get('scope') != req['scope'] or receipt.get('author') != req['thread']:
        raise broker.BrokerError('Terminal receipt owner/scope mismatch')
    return req


def main():
    OUT.mkdir(exist_ok=True)
    with broker.locked():
        req = held()
        if (OUT / 'OBSERVATION-STARTED.json').exists():
            raise broker.BrokerError('Observation already issued; never retry blindly')
        callback = OUT / 'ObserveExactExit.cs'
        broker.atomic_bytes(callback, CALLBACK.encode())
        step = {'file': broker.relative(callback), 'frozen_file': broker.relative(callback),
                'entry': 'C180ExactBnc003ExitObservation.Observe', 'timeout_ms': 30000}
        broker.permission(broker.policy(), req['thread'], req['scope'], [step])
        record = {'request': NAME, 'grant_nonce': NONCE, 'utc': broker.utc(),
                  'fixed_callback_sha256': broker.digest(callback.read_bytes()),
                  'source_sha256': SOURCE_SHA, 'terminal_save_sha256': SAVE_SHA,
                  'reservation_held': True, 'new_writer': False,
                  'scene_or_asset_mutation': False, 'geometry_QA': False}
        broker.write_json(OUT / 'OBSERVATION-STARTED.json', record)
    command, timeout = broker.native_command(step)
    try:
        cp = subprocess.run(command, cwd=broker.PROJECT, capture_output=True,
                            text=True, timeout=timeout, start_new_session=True)
    except BaseException as ex:
        broker.write_json(OUT / 'OBSERVATION-UNSETTLED.json',
                          {**record, 'error': repr(ex), 'reservation_kept': True})
        raise
    parsed = broker.parse_native(cp.returncode, cp.stdout)
    observation = {**record, 'ended_utc': broker.utc(), 'command': command,
                   'stdout': cp.stdout, 'stderr': cp.stderr, 'parsed': parsed}
    broker.write_json(OUT / 'ACTUAL-CALLBACK-RESULT.json', observation)
    if not parsed.get('settled') or not parsed.get('success') or MARKER not in cp.stdout:
        raise broker.BrokerError('No actual callback entry proof; reservation kept')
    with broker.locked():
        req = held()
        if broker.digest(callback.read_bytes()) != record['fixed_callback_sha256']:
            raise broker.BrokerError('Fixed callback changed')
        paths = broker.paths_for(req)
        raw = paths['results'].read_bytes()
        broker.atomic_bytes(OUT / 'ORIGINAL-UNKNOWN-TRANSPORT-RESULT.json', raw)
        result = json.loads(raw)
        if len(result.get('steps', [])) != 1:
            raise broker.BrokerError('Unexpected original step count')
        result['steps'][0]['original_unknown_parsed'] = result['steps'][0]['parsed']
        result['steps'][0]['parsed'] = {
            'settled': True, 'success': False,
            'reason': 'ACTUAL_MAIN_THREAD_BODY_EXIT_OBSERVED_ORIGINAL_TRANSPORT_TIMEOUT',
            'original_transport_success_not_fabricated': True,
            'callback_result': broker.relative(OUT / 'ACTUAL-CALLBACK-RESULT.json')}
        result.update(settled=True, success=False,
                      status='SETTLED_TRANSPORT_FAILURE_APPLICATION_SAVED_BODY_EXIT_OBSERVED',
                      application_saved=True, completed_utc=broker.utc(),
                      original_unknown_result_archive=broker.relative(OUT / 'ORIGINAL-UNKNOWN-TRANSPORT-RESULT.json'),
                      exit_observation=broker.relative(OUT / 'ACTUAL-CALLBACK-RESULT.json'),
                      actual_application_receipt=broker.relative(SAVE),
                      elapsed_time_is_not_exit_evidence=True)
        broker.write_json(paths['results'], result)
    release = broker.release_settled(req, result)
    broker.write_json(OUT / 'ACTUAL-EXIT-RECONCILIATION-RELEASED.json', {
        **record, 'status': 'ACTUAL_BODY_EXIT_OBSERVED_RESERVATION_RELEASED',
        'application_saved': True, 'original_transport_success': False,
        'actual_callback_result': broker.relative(OUT / 'ACTUAL-CALLBACK-RESULT.json'),
        'release': release, 'utc': broker.utc(), 'time_or_expiry_unlock': False})
    broker.emit({'request': NAME, 'status': 'RELEASED_AFTER_ACTUAL_BODY_EXIT',
                 'application_saved': True, 'original_transport_success': False})


if __name__ == '__main__':
    main()
