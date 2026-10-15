#!/usr/bin/env python3
"""Explicit local helper upgrade. Defaults to inspection; --apply opts into restart.

Retains this app's account/browser files, never calls OAuth/model endpoints, and
signals only a runtime-owned PID with an exact creation/command identity match.
"""
import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import sys

sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'provider_runtime'))
import session
from local_client import LocalClient,LocalFailure,private_directory,private_read,write_json


def source_hashes():
    root=Path(__file__).parent
    return {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in root.glob('*.py') if not p.name.startswith('test_')}


def inspect(private):
    try:
        client=LocalClient.load(private);status=client.status(allow_legacy_readonly=True);owned=session.owned_process(private)
        expected=source_hashes()
        return {'running':True,'owned_by_runtime':owned is not None,
                'pid':owned['pid'] if owned else None,'protocol':status.get('protocol','legacy-openai-only'),
                'loaded_current_source':status.get('helper_implementation')==expected,
                'signed_in_accounts':sum(bool(a.get('signed_in')) for a in status.get('accounts',[])),
                'upgrade_requires_new_play_selection_and_consent':True,
                'provider_requests_made':False,'unity_started':False}
    except LocalFailure:
        return {'running':False,'owned_by_runtime':False,'loaded_current_source':False,
                'provider_requests_made':False,'unity_started':False}


def upgrade(private,port=18953,apply=False,open_browser=False,github_client_id=None):
    private=private_directory(private,create=apply)
    before=inspect(private)
    if not apply:return {**before,'applied':False,'instruction':'Run with --apply to opt into the app-private helper restart; login files remain and play consent resets.'}
    fd=os.open(private/'runtime-upgrade.lock',os.O_RDWR|os.O_CREAT|os.O_NOFOLLOW,0o600)
    try:
        try:fcntl.flock(fd,fcntl.LOCK_EX|fcntl.LOCK_NB)
        except BlockingIOError:raise LocalFailure('local_helper_upgrade_in_progress') from None
        if before['running'] and not before['owned_by_runtime']:raise LocalFailure('upgrade_refuses_unowned_helper')
        protected={name:hashlib.sha256(private_read(private/name)).hexdigest() for name in ('accounts.json','browser-session.json') if (private/name).exists()}
        if github_client_id:
            # Public own-app registration only; never collect an app secret or model key.
            from copilot_adapter import contract
            contract(github_client_id)
            write_json(private/'provider-registrations.json',{'schema':1,'github_copilot_client_id':github_client_id})
        if before['running'] and (not before['loaded_current_source'] or github_client_id):session.stop(private)
        session.start(private,port)
        after=inspect(private)
        if not after['loaded_current_source']:raise LocalFailure('new_helper_source_mismatch')
        retained={name:hashlib.sha256(private_read(private/name)).hexdigest()==digest for name,digest in protected.items()}
        if not all(retained.values()):raise LocalFailure('upgrade_changed_registration_files')
        browser=False
        if open_browser:browser=LocalClient.load(private).open_browser(session.open_system_browser)
        return {**after,'applied':True,'registration_files_byte_exact':retained,
                'browser_open_requested':browser,'provider_requests_made':False,'unity_started':False}
    finally:os.close(fd)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--private-dir',type=Path,default=session.DEFAULT_PRIVATE)
    parser.add_argument('--port',type=int,default=18953)
    parser.add_argument('--apply',action='store_true')
    parser.add_argument('--open-browser',action='store_true')
    parser.add_argument('--github-client-id',help='Public client ID of this app own device-enabled GitHub OAuth App.')
    args=parser.parse_args()
    try:
        result=upgrade(args.private_dir.expanduser().absolute(),args.port,args.apply,args.open_browser,args.github_client_id)
        print(json.dumps(result,ensure_ascii=False,indent=2));return 0
    except (LocalFailure,OSError,ValueError) as e:
        print(json.dumps({'applied':False,'error':getattr(e,'code','local_upgrade_error'),
                          'provider_requests_made':False,'unity_started':False}));return 2

if __name__=='__main__':sys.exit(main())
