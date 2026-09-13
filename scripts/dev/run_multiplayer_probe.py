#!/usr/bin/env python3
"""Run a Mac local server and three real NGO clients, then inspect protocol evidence.

This is a bounded local functional rehearsal, not internet, voice or 20/100/2 acceptance.
"""
import argparse
import json
import plistlib
import subprocess
import time
from pathlib import Path


def events(path):
    if not path.exists():
        return []
    rows = []
    text = path.read_text()
    complete_lines = text.splitlines() if text.endswith("\n") else text.splitlines()[:-1]
    for line in complete_lines:
        row = json.loads(line)
        row["Data"] = json.loads(row["Json"])
        rows.append(row)
    return rows


def receipt_rows(rows, command_id):
    return [r["Data"] for r in rows if r["Kind"] == "receipt" and r["Data"].get("CommandId") == command_id]


def inspect(session):
    clients = {n: events(session / f"{n}.events.jsonl") for n in ["participant-1", "participant-2", "instructor"]}
    a = receipt_rows(clients["participant-1"], "race-a")
    b = receipt_rows(clients["participant-2"], "race-b")
    if len(a) != 2 or len(b) != 2:
        raise AssertionError("Expected both race requests and the duplicate reply from real clients")
    if sorted([a[0]["Code"], b[0]["Code"]]) != [0, 7]:
        raise AssertionError("Same target revision did not produce exactly Accepted + StaleTarget")
    winner = a if a[0]["Code"] == 0 else b
    if (winner[0]["Code"], winner[0]["Sequence"]) != (winner[1]["Code"], winner[1]["Sequence"]):
        raise AssertionError("Repeated accepted command did not return its first outcome")
    for name in ["participant-1", "participant-2"]:
        views = [r["Data"]["Observed"] for r in clients[name] if r["Kind"] == "view"]
        if not any(any(e["EntityId"] == "anchor-01" and e["Revision"] == 1 and not e["Active"] for e in v["Entities"]) for v in views):
            raise AssertionError("A client never observed the other process's shared equipment state")
    reports = receipt_rows(clients["participant-1"], "report-a")
    if not reports or reports[-1]["Code"] != 0:
        raise AssertionError("Participant did not report its observed incident")
    if not any(r["Kind"] == "view" and r["Data"]["Observed"]["Reports"] for r in clients["participant-2"]):
        raise AssertionError("Second client never received the authorized team report")
    return {"status": "local_protocol_pass", "protocolClients": 3, "instructors": 1,
            "raceCodes": [a[0]["Code"], b[0]["Code"]], "sharedRevision": 1,
            "acceptedDuplicateReceipt": True,
            "scope": "local NGO connection/shared action/team report; no voice, internet or load acceptance"}


def run(app, session, port, voice_config=None):
    with (app / "Contents/Info.plist").open("rb") as stream:
        executable_name = plistlib.load(stream)["CFBundleExecutable"]
    binary = app / "Contents/MacOS" / executable_name
    processes = []
    handles = []
    def launch(name, extra):
        log = session / (name + ".player.log")
        evidence = session / (name + ".events.jsonl")
        if log.exists() or evidence.exists():
            raise ValueError("Evidence already exists; create a new rehearsal session")
        handle = (session / (name + ".stdio.log")).open("w")
        handles.append(handle)
        process = subprocess.Popen([str(binary), "-batchmode", "-nographics", "-logFile", str(log),
            "--cg-port", str(port), "--cg-evidence", str(evidence)] + extra, stdout=handle, stderr=subprocess.STDOUT)
        processes.append(process)
        return process
    try:
        server_args = ["--cg-mode", "server", "--cg-session", str(session / "server-session.json"),
                       "--cg-records", str(session / "records")]
        if voice_config is not None:
            server_args += ["--cg-voice-config", str(voice_config), "--cg-exit-after", "18"]
        server = launch("server", server_args)
        deadline = time.monotonic() + 30
        while not any(r["Kind"] == "server_started" for r in events(session / "server.events.jsonl")):
            if server.poll() is not None:
                raise RuntimeError("Server exited during startup; inspect its local player log")
            if time.monotonic() > deadline:
                raise TimeoutError("Server startup did not produce protocol evidence")
            time.sleep(.2)
        print("Server listening; launching three actual protocol clients", flush=True)
        clients = [launch(name, ["--cg-mode", "client", "--cg-credential", str(session / (name + ".invitation.json")),
                                "--cg-probe", str(session / (name + ".probe.json"))] + (["--cg-voice-fixture"] if voice_config else []))
                   for name in ["participant-1", "participant-2", "instructor"]]
        deadline = time.monotonic() + 45
        while any(p.poll() is None for p in clients):
            if time.monotonic() > deadline:
                raise TimeoutError("Client rehearsal exceeded its deadline; inspect logs")
            time.sleep(.25)
        if any(p.returncode != 0 for p in clients):
            raise RuntimeError("At least one protocol client exited unsuccessfully")
        result = inspect(session)
        if voice_config is not None:
            for name in ["participant-1", "participant-2", "instructor"]:
                states = [r["Data"] for r in events(session / (name + ".events.jsonl")) if r["Kind"] == "voice_state"]
                if not any(s["Connected"] for s in states):
                    raise AssertionError("Game-authorized voice channels never connected for " + name)
            states = [r["Data"] for r in events(session / "participant-1.events.jsonl") if r["Kind"] == "voice_state"]
            if not any(s["Transmitting"] for s in states) or states[-1]["Transmitting"]:
                raise AssertionError("Synthetic PTT did not start and stop through the game adapter")
            server.wait(timeout=30)
            if server.returncode != 0 or not any(r["Kind"] == "voice_rooms_retired" for r in events(session / "server.events.jsonl")):
                raise AssertionError("Normal server shutdown did not retire voice rooms")
            result["syntheticVoiceChannels"] = "connected_and_ptt_stopped"
            result["normalVoiceRetirement"] = True
            result["scope"] = "local NGO+LiveKit synthetic protocol/PTT; no microphone, PCM playback, WAN or load acceptance"
        runtime_errors = {}
        for log in session.glob("*.player.log"):
            errors = [line for line in log.read_text(errors="replace").splitlines()
                      if "Exception:" in line or "multiplayer startup failed" in line or "Server command processing stopped" in line]
            if errors:
                runtime_errors[log.name] = errors[:8]
        if runtime_errors:
            result["status"] = "protocol_checks_passed_runtime_errors"
            result["runtimeGate"] = "failed"
            result["runtimeErrors"] = runtime_errors
        (session / "local-protocol-result.json").write_text(json.dumps(result, indent=2) + "\n")
        print(json.dumps(result, indent=2), flush=True)
        if runtime_errors:
            raise AssertionError("Protocol checks passed, but Player runtime errors prevent acceptance")
    finally:
        for process in processes:
            if process.poll() is None:
                process.terminate()
        for process in processes:
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
        for handle in handles:
            handle.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app", type=Path, required=True)
    parser.add_argument("--session", type=Path, required=True)
    parser.add_argument("--port", type=int, default=17977)
    parser.add_argument("--voice-config", type=Path)
    args = parser.parse_args()
    run(args.app.resolve(), args.session.resolve(), args.port, args.voice_config.resolve() if args.voice_config else None)


if __name__ == "__main__":
    main()
