#!/usr/bin/env python3
"""Detached local fallback scheduler when launchd is inaccessible to the caller.

start/status/stop use a PID, start-time and instance-token identity. The idle
supervisor sleeps between bounded cleanup subprocesses; it never deletes files.
"""
from __future__ import annotations

import argparse
import fcntl
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import threading
import time
import uuid

sys.dont_write_bytecode = True
from cleanup_daemon import (Refusal, STATE_DIR, atomic_json, no_symlinks,
                            read_config, read_json, state_directory, utc_now)

SCRIPT = Path(__file__).resolve()
CYCLE_SCRIPT = SCRIPT.with_name("cleanup_daemon.py")
INTERVAL_SECONDS = 300
CYCLE_TIMEOUT_SECONDS = 35


def ps_identity(pid):
    """No pid-only signal decisions: compare kernel start-time and command."""
    try:
        answer = subprocess.run(["/bin/ps", "-p", str(pid), "-o", "lstart=", "-o", "command="],
                                text=True, capture_output=True, timeout=3, check=False)
    except (OSError, subprocess.TimeoutExpired):
        return None
    if answer.returncode != 0:
        return None
    text = answer.stdout.strip()
    # lstart: weekday month day hh:mm:ss yyyy, followed by full command.
    fields = text.split(None, 5)
    if len(fields) != 6:
        return None
    return {"started": " ".join(fields[:5]), "command": fields[5]}


def identity_matches(record):
    pid = record.get("pid")
    token = record.get("instance_token")
    if not isinstance(pid, int) or pid <= 1 or not isinstance(token, str) or len(token) != 32:
        return False
    actual = ps_identity(pid)
    if actual is None or actual["started"] != record.get("process_start"):
        return False
    command = actual["command"]
    return (str(SCRIPT) in command and "serve" in command
            and "--instance-token " + token in command)


def service_status(root):
    file = root / STATE_DIR / "service.json"
    record = read_json(file) if file.exists() else {"status": "not_started"}
    record["process_alive_and_identified"] = identity_matches(record)
    return record


def lock_file(path):
    no_symlinks(path)
    return os.open(path, os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)


def start(config, root):
    state = state_directory(root)
    fd = lock_file(state / "service-starter.lock")
    try:
        fcntl.flock(fd, fcntl.LOCK_EX)
        current = service_status(root)
        if current["process_alive_and_identified"]:
            return {**current, "start_result": "already_running"}
        # If identity cannot be established but lock is held, do not replace it.
        check = lock_file(state / "service.lock")
        try:
            try:
                fcntl.flock(check, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                raise Refusal("Supervisor lock held but identity unknown; no takeover")
            fcntl.flock(check, fcntl.LOCK_UN)
        finally:
            os.close(check)
        token = uuid.uuid4().hex
        command = [sys.executable, str(SCRIPT), "--config", str(config),
                   "serve", "--instance-token", token]
        env = os.environ.copy()
        env["PYTHONDONTWRITEBYTECODE"] = "1"
        child = subprocess.Popen(command, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                                 stderr=subprocess.DEVNULL, cwd=root, env=env,
                                 start_new_session=True, close_fds=True)
        due = time.monotonic() + 6
        while time.monotonic() < due:
            current = service_status(root)
            if (current.get("pid") == child.pid and current.get("instance_token") == token
                    and current["process_alive_and_identified"]):
                return {**current, "start_result": "started_detached"}
            if child.poll() is not None:
                raise Refusal("Detached supervisor exited during startup")
            time.sleep(0.05)
        raise Refusal("Startup not confirmed; child identity retained, no automatic kill or duplicate start")
    finally:
        fcntl.flock(fd, fcntl.LOCK_UN)
        os.close(fd)


def stop(root):
    current = service_status(root)
    if not current["process_alive_and_identified"]:
        return {**current, "stop_result": "no_identified_running_service"}
    os.kill(current["pid"], signal.SIGTERM)
    due = time.monotonic() + CYCLE_TIMEOUT_SECONDS + 8
    while time.monotonic() < due:
        latest = service_status(root)
        if latest.get("status") == "stopped" or not identity_matches(current):
            return {**latest, "stop_result": "stopped"}
        time.sleep(0.1)
    return {**service_status(root), "stop_result": "stopping_bounded_cycle"}


def serve(config, root, token):
    if len(token) != 32 or any(c not in "0123456789abcdef" for c in token):
        raise Refusal("Private serve mode needs a valid instance token")
    state = state_directory(root)
    fd = lock_file(state / "service.lock")
    try:
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise Refusal("Another supervisor owns service.lock")
        stopped = threading.Event()

        def request_stop(signum, _frame):
            stopped.set()

        signal.signal(signal.SIGTERM, request_stop)
        signal.signal(signal.SIGINT, request_stop)
        identity = ps_identity(os.getpid())
        if identity is None:
            raise Refusal("Cannot establish own process identity")
        record = {"status": "running", "pid": os.getpid(), "instance_token": token,
                  "process_start": identity["started"], "script": str(SCRIPT),
                  "config": str(config), "started_utc": utc_now(),
                  "interval_seconds": INTERVAL_SECONDS, "completed_cycles": 0}
        atomic_json(state / "service.json", record)
        try:
            while not stopped.is_set():
                record.update(status="running_cycle", last_cycle_started_utc=utc_now())
                atomic_json(state / "service.json", record)
                began = time.monotonic()
                child = subprocess.Popen([sys.executable, str(CYCLE_SCRIPT), "--config", str(config), "cycle"],
                                         stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                                         stderr=subprocess.DEVNULL, cwd=root, close_fds=True)
                try:
                    child.wait(timeout=CYCLE_TIMEOUT_SECONDS)
                    record["last_cycle_exit_code"] = child.returncode
                except subprocess.TimeoutExpired:
                    # Bound the child even if a filesystem command stalls. A
                    # single unlink is atomic; no recursive tree operation exists.
                    child.terminate()
                    try:
                        child.wait(timeout=2)
                    except subprocess.TimeoutExpired:
                        child.kill()
                        child.wait(timeout=2)
                    record["last_cycle_exit_code"] = "timeout_terminated"
                record.update(status="waiting", last_cycle_completed_utc=utc_now(),
                              completed_cycles=record["completed_cycles"] + 1)
                # Keep nominal five-minute starts rather than cycle-duration drift.
                wait = max(0.0, INTERVAL_SECONDS - (time.monotonic() - began))
                from datetime import datetime, timezone
                record["next_due_utc"] = datetime.fromtimestamp(time.time() + wait, timezone.utc).isoformat()
                atomic_json(state / "service.json", record)
                stopped.wait(wait)
        finally:
            record.update(status="stopped", stopped_utc=utc_now())
            atomic_json(state / "service.json", record)
        return record
    finally:
        fcntl.flock(fd, fcntl.LOCK_UN)
        os.close(fd)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", required=True)
    parser.add_argument("command", choices=("start", "status", "stop", "serve"))
    parser.add_argument("--instance-token", default="")
    args = parser.parse_args(argv)
    try:
        config = Path(args.config).absolute()
        root, _cfg = read_config(config)
        if args.command == "start":
            result = start(config, root)
        elif args.command == "stop":
            result = stop(root)
        elif args.command == "serve":
            result = serve(config, root, args.instance_token)
        else:
            result = service_status(root)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except (OSError, Refusal, ValueError, KeyError, TypeError) as exc:
        print(json.dumps({"status": "refused", "detail": type(exc).__name__ + ": " + str(exc)}, ensure_ascii=False))
        return 1


if __name__ == "__main__":
    sys.exit(main())
