#!/usr/bin/env python3
"""Force-kill a real local NGO server and verify rejoin, durable dedupe and instructor resume."""
import argparse
import json
import plistlib
import subprocess
import time
from pathlib import Path

from run_multiplayer_probe import events, receipt_rows


def wait_until(predicate, seconds, message):
    deadline = time.monotonic() + seconds
    while not predicate():
        if time.monotonic() > deadline:
            raise TimeoutError(message)
        time.sleep(.2)


def run(app, session, port):
    with (app / "Contents/Info.plist").open("rb") as stream:
        binary = app / "Contents/MacOS" / plistlib.load(stream)["CFBundleExecutable"]
    processes, handles = [], []
    def launch(name, mode, participant=None, plan=None):
        log = session / (name + ".player.log")
        output = session / (name + ".events.jsonl")
        if log.exists() or output.exists():
            raise ValueError("Recovery evidence already exists; create a fresh private session")
        handle = (session / (name + ".stdio.log")).open("w")
        handles.append(handle)
        args = [str(binary), "-batchmode", "-nographics", "-logFile", str(log), "--cg-port", str(port),
                "--cg-evidence", str(output), "--cg-mode", mode]
        if mode == "server":
            args += ["--cg-session", str(session / "server-session.json"), "--cg-records", str(session / "records")]
        else:
            args += ["--cg-credential", str(session / (participant + ".invitation.json")), "--cg-probe", str(plan)]
        process = subprocess.Popen(args, stdout=handle, stderr=subprocess.STDOUT)
        processes.append(process)
        return process
    def write_plan(name, steps, duration=12):
        path = session / (name + ".recovery-plan.json")
        with path.open("x") as stream:
            json.dump({"Steps": steps, "ExitAfterSeconds": duration}, stream)
        return path
    try:
        server = launch("before-server", "server")
        wait_until(lambda: any(r["Kind"] == "server_started" for r in events(session / "before-server.events.jsonl")), 30, "Initial server startup")
        before_clients = []
        for name in ["participant-1", "participant-2", "instructor"]:
            plan = json.loads((session / (name + ".probe.json")).read_text())
            before_clients.append(launch("before-" + name, "client", name, write_plan("before-" + name, plan["Steps"], 60)))
        wait_until(lambda: any(r["Code"] == 0 for r in receipt_rows(events(session / "before-participant-1.events.jsonl"), "report-a")), 30, "Initial approved report")
        rows_a = receipt_rows(events(session / "before-participant-1.events.jsonl"), "race-a")
        winner, command = ("participant-1", "race-a") if rows_a[0]["Code"] == 0 else ("participant-2", "race-b")
        original = receipt_rows(events(session / ("before-" + winner + ".events.jsonl")), command)[0]
        server.kill()
        server.wait(timeout=10)
        for client in before_clients:
            client.terminate()
        for client in before_clients:
            client.wait(timeout=10)
        print("Forced server process termination after a flushed shared action/report", flush=True)
        restarted = launch("after-server", "server")
        wait_until(lambda: any(r["Kind"] == "server_started" for r in events(session / "after-server.events.jsonl")), 30, "Recovered server startup")
        after_clients = []
        for name in ["participant-1", "participant-2", "instructor"]:
            if name == winner:
                steps = [
                    {"AtSeconds": 2, "CommandId": command, "Kind": 0, "TargetId": "anchor-01", "ExpectedRevision": 0},
                    {"AtSeconds": 3, "CommandId": "before-confirm", "Kind": 0, "TargetId": "anchor-01", "ExpectedRevision": 1},
                    {"AtSeconds": 8, "CommandId": "after-confirm", "Kind": 0, "TargetId": "anchor-01", "ExpectedRevision": 1}]
            elif name == "instructor":
                steps = [{"AtSeconds": 6, "CommandId": "confirm-recovery", "Kind": 7, "TargetId": ""}]
            else:
                steps = []
            after_clients.append(launch("after-" + name, "client", name, write_plan("after-" + name, steps)))
        wait_until(lambda: all(p.poll() is not None for p in after_clients), 45, "Recovered client completion")
        rows = events(session / ("after-" + winner + ".events.jsonl"))
        repeated = receipt_rows(rows, command)
        if len(repeated) != 1 or repeated[0]["Code"] != 0 or repeated[0]["Sequence"] != original["Sequence"]:
            raise AssertionError("Accepted action was not deduplicated across server restart")
        if [r["Code"] for r in receipt_rows(rows, "before-confirm")] != [13]:
            raise AssertionError("Recovered shift accepted an action before instructor confirmation")
        if [r["Code"] for r in receipt_rows(rows, "after-confirm")] != [0]:
            raise AssertionError("Instructor confirmation did not resume the recovered shift")
        for name in ["participant-1", "participant-2"]:
            views = [r["Data"]["Observed"] for r in events(session / ("after-" + name + ".events.jsonl")) if r["Kind"] == "view"]
            if not any(v["Paused"] for v in views) or not any(not v["Paused"] for v in views):
                raise AssertionError("Client did not see the recover-paused / instructor-resumed transition")
            if not any(v["Reports"] for v in views):
                raise AssertionError("Authorized team reports did not survive recovery")
        unexpected = {f.name: [x for x in f.read_text(errors="replace").splitlines() if "Exception:" in x]
                      for f in session.glob("*.player.log")}
        if any(unexpected.values()):
            raise AssertionError("Player exception during recovery; inspect local logs")
        result = {"status": "local_recovery_pass", "protocolClients": 3, "forcedServerKill": True,
                  "sameParticipantRejoin": True, "durableDuplicateSequence": original["Sequence"],
                  "instructorConfirmationRequired": True, "teamReportsRetained": True,
                  "scope": "small local NGO scene; no whole-world physics, voice, WAN or load acceptance"}
        (session / "recovery-result.json").write_text(json.dumps(result, indent=2) + "\n")
        print(json.dumps(result, indent=2), flush=True)
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
    parser.add_argument("--app", required=True, type=Path)
    parser.add_argument("--session", required=True, type=Path)
    parser.add_argument("--port", type=int, default=17978)
    args = parser.parse_args()
    run(args.app.resolve(), args.session.resolve(), args.port)


if __name__ == "__main__":
    main()
