#!/usr/bin/env python3
"""Start/open/inspect/stop only this app's local helper, without starting OAuth or cloud requests."""
import argparse
import fcntl
import os
from pathlib import Path
import signal
import subprocess
import sys
import time

from local_client import (DEFAULT_PRIVATE, LocalClient, LocalFailure, canonical, digest,
                          private_directory, private_read, public_status, strict_json, write_json)

SIDECAR = Path(__file__).resolve().parents[1] / 'provider_auth/sidecar.py'
OWNER_FILE = 'runtime-owner.json'


def open_system_browser(url):
    # Direct argument passing avoids Python's legacy macOS AppleScript quoting path.
    command = ['/usr/bin/open', url] if sys.platform == 'darwin' else ['xdg-open', url]
    try:
        result = subprocess.run(command, stdin=subprocess.DEVNULL, capture_output=True, timeout=8)
        return result.returncode == 0
    except (OSError, subprocess.TimeoutExpired):
        return False


def process_identity(pid):
    if type(pid) is not int or pid <= 1:
        return None
    try:
        result = subprocess.run(['ps', '-p', str(pid), '-o', 'lstart=', '-o', 'command='],
                                capture_output=True, text=True, timeout=3)
    except (OSError, subprocess.TimeoutExpired):
        return None
    return result.stdout.strip() if result.returncode == 0 and result.stdout.strip() else None


def owned_process(private):
    try:
        record = strict_json(private_read(private / OWNER_FILE))
    except LocalFailure:
        return None
    if not isinstance(record, dict) or record.get('schema') != 1:
        return None
    identity = process_identity(record.get('pid'))
    if (not identity or identity != record.get('process_identity') or str(SIDECAR) not in identity
            or str(private) not in identity or record.get('sidecar_path') != str(SIDECAR)):
        return None
    return record


def status(private):
    client = LocalClient.load(private)
    record = owned_process(private)
    return {'running': True, 'owned_by_runtime': record is not None, 'pid': record['pid'] if record else None,
            'local_ui': client.endpoint + '/', 'status': public_status(client.status()),
            'provider_requests_made_by_status': False}


def start(private, port):
    if not 1024 <= port <= 65535:
        raise LocalFailure('unprivileged_loopback_port_required')
    private = private_directory(private, create=True)
    fd = os.open(private / 'runtime-launch.lock', os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)
    try:
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        os.close(fd)
        raise LocalFailure('local_helper_launch_in_progress') from None
    child = None
    try:
        # Preserve an already-running helper regardless of who owns its lifecycle.
        try:
            current = status(private)
            current['started_new_process'] = False
            return current
        except LocalFailure:
            if owned_process(private):
                raise LocalFailure('owned_helper_running_but_unreachable') from None
        log_fd = os.open(private / 'runtime-helper.log', os.O_WRONLY | os.O_CREAT | os.O_APPEND | os.O_NOFOLLOW, 0o600)
        try:
            os.fchmod(log_fd, 0o600)
            child = subprocess.Popen([sys.executable, str(SIDECAR), '--private-dir', str(private), '--port', str(port)],
                                     stdin=subprocess.DEVNULL, stdout=log_fd, stderr=log_fd, start_new_session=True,
                                     close_fds=True, cwd=SIDECAR.parents[3])
        finally:
            os.close(log_fd)
        identity = None
        deadline = time.monotonic() + 8
        while time.monotonic() < deadline:
            if child.poll() is not None:
                raise LocalFailure('local_helper_start_failed')
            try:
                client = LocalClient.load(private)
                value = client.status()
                identity = process_identity(child.pid)
                if identity and client.port == port:
                    break
            except LocalFailure:
                pass
            time.sleep(.1)
        if not identity:
            raise LocalFailure('local_helper_start_timeout')
        write_json(private / OWNER_FILE, {'schema': 1, 'pid': child.pid, 'process_identity': identity,
                                          'sidecar_path': str(SIDECAR), 'sidecar_sha256': digest(SIDECAR.read_bytes()),
                                          'browser_ui_sha256':digest(SIDECAR.with_name('browser_ui.py').read_bytes()) if SIDECAR.with_name('browser_ui.py').is_file() else None,
                                          'started_unix': time.time()})
        return {'running': True, 'owned_by_runtime': True, 'pid': child.pid, 'started_new_process': True,
                'local_ui': client.endpoint + '/', 'status': public_status(value),
                'provider_requests_made_by_start': False}
    except Exception:
        if child is not None and child.poll() is None:
            child.send_signal(signal.SIGTERM)
            try:
                child.wait(timeout=4)
            except subprocess.TimeoutExpired:
                child.kill()
                child.wait(timeout=2)
        raise
    finally:
        os.close(fd)


def stop(private):
    private = private_directory(private)
    record = owned_process(private)
    if not record:
        raise LocalFailure('no_matching_owned_helper_to_stop')
    # Exact PID plus creation/command identity prevents stopping an unrelated reused PID.
    os.kill(record['pid'], signal.SIGTERM)
    deadline = time.monotonic() + 5
    while time.monotonic() < deadline and process_identity(record['pid']) == record['process_identity']:
        time.sleep(.1)
    if process_identity(record['pid']) == record['process_identity']:
        raise LocalFailure('owned_helper_stop_timeout')
    (private / OWNER_FILE).unlink(missing_ok=True)
    return {'stopped': True, 'pid': record['pid'], 'registration_files_retained': True,
            'descriptor_removed_by_helper': not (private / 'connection.json').exists()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('start', 'status', 'open', 'stop'))
    parser.add_argument('--private-dir', type=Path, default=DEFAULT_PRIVATE)
    parser.add_argument('--port', type=int, default=18953)
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    try:
        if args.command == 'start':
            result = start(args.private_dir.expanduser().absolute(), args.port)
        elif args.command == 'stop':
            result = stop(args.private_dir.expanduser().absolute())
        elif args.command == 'open':
            client = LocalClient.load(args.private_dir)
            result = {'local_ui': client.endpoint + '/', 'browser_open_requested': client.open_browser(open_system_browser),
                      'provider_requests_made_by_open': False}
            if not result['browser_open_requested']:
                result['error'] = 'system_browser_open_failed'
        else:
            result = status(args.private_dir.expanduser().absolute())
        if args.report:
            write_json(args.report, result)
        print(canonical(result).decode('utf-8'))
        return 0
    except (LocalFailure, OSError, ValueError, subprocess.SubprocessError) as error:
        result = {'running': False, 'error': error.code if isinstance(error, LocalFailure) else 'local_lifecycle_error',
                  'provider_request_attempted': False}
        if args.report:
            write_json(args.report, result)
        print(canonical(result).decode('utf-8'))
        return 2


if __name__ == '__main__':
    sys.exit(main())
