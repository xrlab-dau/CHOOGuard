#!/usr/bin/env python3
"""Build every Foundation candidate target into a new output root and record what really happened.

Each named target ends with an actual build/run result or a capability-specific blocked record; none is
skipped silently. Build attempts are diagnostic. Only a target that built, produced a verified payload
manifest and passed its positive launch check is marked runnable. Binaries stay outside Git.
"""
import argparse
import datetime
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import native_manifest  # noqa: E402

TARGETS = ("windows-desktop", "mac-development", "windows-server", "linux-server", "livekit")
BUILDER = "ChooGuard.Foundation.Multiplayer.Editor.MultiplayerSceneBuilder."
UNITY_TARGETS = {
    "windows-desktop": {"method": "BuildWindows", "build": "Builds/FoundationMultiplayerWindows",
                        "binary": "ChooGuardMultiplayer.exe", "engine": "windowsstandalonesupport", "variation": "win64_player_nondevelopment_mono"},
    "windows-server": {"method": "BuildWindowsServer", "build": "Builds/FoundationServerWindows",
                       "binary": "ChooGuardServer.exe", "engine": "windowsstandalonesupport", "variation": "win64_server_nondevelopment_mono"},
    "linux-server": {"method": "BuildLinuxServer", "build": "Builds/FoundationServerLinux",
                     "binary": "ChooGuardServer", "engine": "LinuxStandaloneSupport", "variation": "linux64_server_nondevelopment_mono"},
    "mac-development": {"method": "BuildMac", "build": "Builds/FoundationMultiplayerMac",
                        "binary": "ChooGuardMultiplayer.app", "engine": "MacStandaloneSupport", "variation": None},
}
RUNNABLE = "built_and_launched"
STATUSES = {RUNNABLE, "built_launch_failed", "built_launch_blocked", "build_failed", "blocked"}


def now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def sha256_file(path):
    return native_manifest.sha256(Path(path))


def parse_targets(text):
    names = [t.strip() for t in text.split(",") if t.strip()]
    unknown = sorted(set(names) - set(TARGETS))
    if unknown or not names or len(set(names)) != len(names):
        raise ValueError("Targets must be a non-empty, duplicate-free subset of " + ",".join(TARGETS) + (f"; unknown {unknown}" if unknown else ""))
    return [t for t in TARGETS if t in names]


def new_output_root(path):
    path = Path(path).resolve()
    if path.exists():
        raise ValueError("Output root already exists; every run needs a new location")
    path.mkdir(parents=True)
    return path


def classify(built, payload_verified, launch_status):
    if not built:
        return "build_failed"
    if not payload_verified:
        return "built_launch_failed"
    return {"passed": RUNNABLE, "blocked": "built_launch_blocked"}.get(launch_status, "built_launch_failed")


def runnable(records):
    return [r for r in records if r["status"] == RUNNABLE and r.get("payload", {}).get("verified") and r.get("launch", {}).get("status") == "passed"]


def run_logged(argv, log, timeout, cwd=None):
    started = time.monotonic()
    with open(log, "w", encoding="utf-8", errors="replace") as handle:
        try:
            code = subprocess.run([str(a) for a in argv], stdout=handle, stderr=subprocess.STDOUT, cwd=cwd, timeout=timeout).returncode
        except FileNotFoundError as error:
            handle.write(f"{type(error).__name__}: {error}\n")
            code = None
        except subprocess.TimeoutExpired:
            handle.write(f"timeout after {timeout}s\n")
            code = "timeout"
    return {"argv": [str(a) for a in argv], "exitCode": code, "seconds": round(time.monotonic() - started, 1),
            "log": str(log), "logSha256": sha256_file(log)}


def unity_capability(unity, spec):
    engine = Path(unity).parent / "Data" / "PlaybackEngines" / spec["engine"]
    variations = sorted(p.name for p in (engine / "Variations").iterdir()) if (engine / "Variations").is_dir() else []
    available = engine.is_dir() and (spec["variation"] is None or spec["variation"] in variations)
    return {"playbackEngine": spec["engine"], "installed": engine.is_dir(), "requiredVariation": spec["variation"],
            "available": available, "host": sys.platform}


def build_unity_target(target, project, unity, run_dir):
    spec = UNITY_TARGETS[target]
    record = {"target": target, "capability": unity_capability(unity, spec)}
    build_dir = project / spec["build"]
    if build_dir.exists():
        record.update(status="blocked", blockedReason=f"{spec['build']} already exists in the project; refusing to reuse or delete an earlier build")
        return record
    attempt = run_logged([unity, "-batchmode", "-quit", "-nographics", "-projectPath", project,
                          "-executeMethod", BUILDER + spec["method"], "-logFile", run_dir / "build.log"],
                         run_dir / "build.stdout.log", timeout=3600)
    build_log = run_dir / "build.log"
    log_text = build_log.read_text(encoding="utf-8", errors="replace") if build_log.exists() else ""
    attempt["buildLogSha256"] = sha256_file(build_log) if build_log.exists() else None
    attempt["successMarker"] = "Multiplayer build succeeded" in log_text
    attempt["errors"] = [line.strip() for line in log_text.splitlines() if "Exception:" in line or "error CS" in line][:6]
    record["attempt"] = attempt
    binary = build_dir / spec["binary"]
    built = attempt["exitCode"] == 0 and attempt["successMarker"] and binary.exists()
    if not built and not record["capability"]["available"]:
        record.update(status="blocked", blockedReason=f"{spec['engine']} build support ({spec['variation'] or 'any'}) is not installed on this host; Unity refused the attempt")
        return record
    if not built:
        record["status"] = "build_failed"
        return record
    payload = run_dir / "payload"
    shutil.move(str(build_dir), str(payload))
    manifest_path = run_dir / "payload-manifest.json"
    record_manifest = native_manifest.manifest(payload, [p.relative_to(payload).as_posix() for p in payload.rglob("*") if p.is_file()], "native-build-payload")
    native_manifest.write_new(manifest_path, record_manifest)
    files = native_manifest.verify(payload, json.loads(manifest_path.read_text(encoding="utf-8")))
    binary_path = payload / spec["binary"]
    record["payload"] = {"path": str(payload), "binary": spec["binary"], "binarySha256": sha256_file(binary_path) if binary_path.is_file() else None,
                         "manifest": str(manifest_path), "manifestSha256": sha256_file(manifest_path), "files": files,
                         "filesDigest": record_manifest["filesDigest"], "verified": True}
    return record


def events(path):
    if not Path(path).exists():
        return []
    rows = []
    for line in Path(path).read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            rows.append(json.loads(line))
        except json.JSONDecodeError:
            continue
    return rows


def wait_for(predicate, process, seconds):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        if predicate():
            return True
        if process is not None and process.poll() is not None:
            return predicate()
        time.sleep(0.25)
    return predicate()


def stop(process):
    if process and process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()


def launch_server(argv_prefix, run_dir, session, port, path_for_server, listen="127.0.0.1"):
    evidence = run_dir / "launch-server.events.jsonl"
    records = run_dir / "launch-records"
    records.mkdir()
    argv = argv_prefix + ["-batchmode", "-nographics", "-logFile", path_for_server(run_dir / "launch-server.player.log"), "--cg-mode", "server",
                          "--cg-listen", listen, "--cg-port", str(port), "--cg-session", path_for_server(session / "server-session.json"),
                          "--cg-records", path_for_server(records), "--cg-evidence", path_for_server(evidence)]
    stdio = open(run_dir / "launch-server.stdio.log", "w", encoding="utf-8", errors="replace")
    process = subprocess.Popen([str(a) for a in argv], stdout=stdio, stderr=subprocess.STDOUT)
    started = wait_for(lambda: any(r.get("Kind") == "server_started" for r in events(evidence)), process, 60)
    return process, stdio, {"argv": [str(a) for a in argv], "serverStarted": started, "evidence": str(evidence)}


def launch_client(client_binary, run_dir, session, port, address="127.0.0.1"):
    evidence = run_dir / "launch-client.events.jsonl"
    argv = [client_binary, "-batchmode", "-nographics", "-logFile", run_dir / "launch-client.player.log", "--cg-mode", "client",
            "--cg-address", address, "--cg-port", str(port), "--cg-credential", session / "instructor.invitation.json",
            "--cg-probe", session / "instructor.probe.json", "--cg-evidence", evidence]
    attempt = run_logged(argv, run_dir / "launch-client.stdio.log", timeout=90)
    views = sum(1 for r in events(evidence) if r.get("Kind") == "view")
    return {**attempt, "viewEvents": views, "passed": attempt["exitCode"] == 0 and views > 0}


def livekit_record(project, run_dir):
    compose = project / "services/livekit/compose.yaml"
    attempt = run_logged(["docker", "compose", "version"], run_dir / "docker-compose-version.log", timeout=60)
    reasons = []
    if attempt["exitCode"] != 0:
        reasons.append("container runtime unavailable: `docker compose version` did not run on this host")
    if sys.platform != "darwin":
        reasons.append("services/livekit/configure_local.py creates credentials only on supported macOS hosts; this host is " + sys.platform)
    record = {"target": "livekit", "attempt": attempt,
              "capability": {"compose": "services/livekit/compose.yaml", "composeSha256": sha256_file(compose),
                             "dockerComposeAvailable": attempt["exitCode"] == 0, "host": sys.platform}}
    if reasons:
        record.update(status="blocked", blockedReason="; ".join(reasons))
    else:
        record.update(status="build_failed", blockedReason="service start is not automated by this runner")
    return record


def to_wsl(path, distro):
    out = subprocess.run(["wsl.exe", "-d", distro, "--", "wslpath", "-a", str(path).replace("\\", "/")], capture_output=True, text=True, timeout=60)
    if out.returncode != 0 or not out.stdout.strip():
        raise RuntimeError("wslpath failed for " + str(path))
    return out.stdout.strip()


def launch_checks(records, root, session, port, distro):
    desktop = records.get("windows-desktop")
    client_binary = str(Path(desktop["payload"]["path"]) / desktop["payload"]["binary"]) if desktop and desktop.get("payload") else None
    if desktop and desktop.get("payload"):
        desktop["launch"] = {"status": "blocked", "reason": "client launch needs a started local server (windows-server not built)"}
    mac = records.get("mac-development")
    if mac and mac.get("payload"):
        mac["launch"] = {"status": "blocked", "reason": "macOS player cannot run on this host"}
    for target, offset in (("windows-server", 0), ("linux-server", 1)):
        record = records.get(target)
        if not record or not record.get("payload"):
            continue
        run_dir = root / target
        binary = Path(record["payload"]["path"]) / record["payload"]["binary"]
        process = stdio = None
        address = "127.0.0.1"
        try:
            if target == "windows-server":
                process, stdio, server = launch_server([str(binary)], run_dir, session, port + offset, str)
            else:
                # The runtime refuses non-loopback listeners without an encrypted transport, so the Linux server stays on
                # loopback inside WSL; the Windows client attempt then depends on WSL localhost forwarding and is reported as-is.
                wsl_binary = to_wsl(binary, distro)
                subprocess.run(["wsl.exe", "-d", distro, "--", "chmod", "+x", wsl_binary], check=True, timeout=60)
                process, stdio, server = launch_server(["wsl.exe", "-d", distro, "--", wsl_binary], run_dir, session, port + offset,
                                                       lambda p: to_wsl(p, distro))
                server["host"] = f"WSL2 {distro} (Linux x86_64 kernel under Windows)"
            client = launch_client(client_binary, run_dir, session, port + offset, address) if server["serverStarted"] and client_binary else None
            record["launch"] = {"status": "passed" if server["serverStarted"] else "failed", "server": server, "client": client,
                                "clientFromWindows": None if client is None else client["passed"]}
            if target == "windows-server" and desktop and desktop.get("payload"):
                desktop["launch"] = {"status": "passed" if client and client["passed"] else "failed", "via": "windows-server", "client": client}
        except (OSError, RuntimeError, subprocess.SubprocessError) as error:
            record["launch"] = {"status": "failed", "error": f"{type(error).__name__}: {error}"}
        finally:
            stop(process)
            if target == "linux-server" and process is not None:
                subprocess.run(["wsl.exe", "-d", distro, "--", "pkill", "-f", record["payload"]["binary"]], capture_output=True, timeout=60)
            if stdio:
                stdio.close()
            if process is not None and "launch" in record and "server" in record["launch"]:
                record["launch"]["server"]["exitCode"] = process.returncode


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path.cwd())
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--targets", default=",".join(TARGETS))
    parser.add_argument("--unity", type=Path, default=Path(os.environ.get("UNITY_EDITOR", "")))
    parser.add_argument("--port", type=int, default=17977)
    parser.add_argument("--wsl-distro", default="Ubuntu")
    args = parser.parse_args()
    try:
        targets = parse_targets(args.targets)
        if not args.unity.is_file():
            raise ValueError("Set --unity or UNITY_EDITOR to the exact Unity editor executable")
        project = args.project.resolve()
        root = new_output_root(args.output_root)
    except ValueError as error:
        parser.exit(2, f"{error}\n")
    result = {"schemaVersion": 1, "runId": root.name, "startedAt": now(), "host": sys.platform,
              "project": {"head": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=project, text=True).strip(),
                          "dirty": subprocess.check_output(["git", "status", "--porcelain"], cwd=project, text=True).splitlines()},
              "runner": {"script": "scripts/dev/build_foundation_candidates.py", "sha256": sha256_file(Path(__file__).resolve())},
              "unity": {"path": str(args.unity), "sha256": sha256_file(args.unity)}, "requestedTargets": targets}
    snapshot = native_manifest.snapshot(project, root / "source-snapshot", [])
    result["sourceSnapshot"] = {"path": str(root / "source-snapshot"), "manifest": str(root / "source-snapshot" / "native-source-manifest.json"),
                                "filesDigest": snapshot["filesDigest"], "files": len(snapshot["files"]), "baseCommit": snapshot["baseCommit"], "verified": True}
    session = root / "session"
    subprocess.run([sys.executable, str(project / "scripts/dev/create_multiplayer_session.py"), "--output", str(session)],
                   check=True, cwd=project, capture_output=True)
    records = {}
    for target in targets:
        run_dir = root / target
        run_dir.mkdir()
        records[target] = livekit_record(project, run_dir) if target == "livekit" else build_unity_target(target, project, args.unity, run_dir)
    launch_checks(records, root, session, args.port, args.wsl_distro)
    for record in records.values():
        if record.get("status") not in ("blocked", "build_failed"):
            record["status"] = classify(True, record.get("payload", {}).get("verified", False), record.get("launch", {}).get("status"))
    result["targets"] = [records[t] for t in targets]
    result["runnable"] = [r["target"] for r in runnable(result["targets"])]
    result["finishedAt"] = now()
    if len(result["targets"]) != len(targets) or any(r["status"] not in STATUSES for r in result["targets"]):
        raise AssertionError("A requested target has no recorded result")
    native_manifest.write_new(root / "candidate-build.json", result)
    print(json.dumps({"runId": result["runId"], "targets": {r["target"]: r["status"] for r in result["targets"]}, "runnable": result["runnable"]}, indent=2))


if __name__ == "__main__":
    main()
