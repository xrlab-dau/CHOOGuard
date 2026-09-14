#!/usr/bin/env python3
"""Prepare/run a private local20-protocol-client Foundation load.

This module is stdlib-only. --prepare-only launches nothing. Real runs deliberately
use21 owned headless player processes; they do not establish human/WAN/voice or
20-desktop-renderer acceptance. The runtime metrics producer contract is provisional.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import os
import plistlib
import secrets
import signal
import stat
import subprocess
import sys
import time
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from foundation_load_metrics import (GROUPED_FAULT_MARKERS, MetricsError, MetricsReader, assess,
                                   assert_fault_dispatch_agreement, dispatch_fault_case,
                                   ready_for_measurement, strict_json)

PUBLICATION_RECEIPT_SCOPE = "local-headless-protocol-load-metrics"


class LoadError(ValueError):
    """Safe code only; no paths, credentials, child output or raw metric values."""


@dataclass(frozen=True)
class Config:
    project: Path
    session: Path
    player: Path
    server_player: Path | None = None
    duration: float = 3600
    layout: str = "dispersed"
    clients: int = 20
    port: int = 17979
    startup_timeout: float = 60
    warmup_timeout: float = 300
    metrics_grace: float = 10
    shutdown_grace: float = 10
    launch_stagger: float = .15

    def validate(self):
        if self.clients != 20:
            raise LoadError("EXACT20CLIENTS_REQUIRED")
        if self.layout not in ("dispersed", "gathered") or not 1 <= self.port <= 65535:
            raise LoadError("INVALID_LAYOUT_OR_PORT")
        for value in (self.duration, self.startup_timeout, self.warmup_timeout, self.metrics_grace, self.shutdown_grace):
            if isinstance(value, bool) or not math.isfinite(value) or not 0 < value <= 86400:
                raise LoadError("INVALID_DURATION")
        if not math.isfinite(self.launch_stagger) or not 0 <= self.launch_stagger <= 10:
            raise LoadError("INVALID_STAGGER")
        if os.name == "nt" and sys.version_info < (3, 13):
            # Python3.13+ handles mode0700 with a private Windows directory DACL.
            # This compatibility restriction avoids pretending older Windows chmod implements POSIX privacy.
            raise LoadError("WINDOWS_REQUIRES_PYTHON313_FOR_PRIVATE_DIRECTORY")


def private_json(path: Path, value):
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0)
    with os.fdopen(os.open(path, flags, 0o600), "w", encoding="utf-8") as stream:
        json.dump(value, stream, ensure_ascii=False, allow_nan=False, indent=2)
        stream.write("\n")


def read_json(path: Path):
    try:
        if path.stat().st_size > 8 * 1024 * 1024:
            raise LoadError("INPUT_JSON_TOO_LARGE")
        return strict_json(path.read_bytes())
    except (OSError, MetricsError) as exc:
        raise LoadError("INPUT_JSON_UNAVAILABLE_OR_INVALID") from exc


def file_sha(path: Path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for part in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(part)
    return digest.hexdigest()


def resolve_player(path: Path):
    """Resolve an app bundle by Info.plist, or a selected Windows/Linux/Mac executable."""
    path = path.resolve()
    if path.is_dir() and path.suffix.lower() == ".app":
        try:
            with (path / "Contents/Info.plist").open("rb") as stream:
                name = plistlib.load(stream)["CFBundleExecutable"]
            if not isinstance(name, str) or not name or Path(name).name != name or "/" in name or "\\" in name:
                raise LoadError("UNSAFE_BUNDLE_EXECUTABLE")
            binary = (path / "Contents/MacOS" / name).resolve()
            if not binary.is_relative_to((path / "Contents/MacOS").resolve()):
                raise LoadError("BUNDLE_EXECUTABLE_ESCAPES_APP")
        except (OSError, KeyError, ValueError, plistlib.InvalidFileException) as exc:
            raise LoadError("INVALID_APP_BUNDLE") from exc
        platform = "macos"
    else:
        binary = path
        platform = "windows" if path.suffix.lower() == ".exe" else "linux" if path.suffix.lower() in (".x86_64", ".x86") else "native-executable"
    if not binary.is_file():
        raise LoadError("PLAYER_BINARY_MISSING")
    if platform != "windows" and os.name != "nt" and not os.access(binary, os.X_OK):
        raise LoadError("PLAYER_NOT_EXECUTABLE")
    return binary, platform


def local(frame, point):
    x, z = point["X"] - frame["Origin"]["X"], point["Z"] - frame["Origin"]["Z"]
    angle = math.radians(frame["YawDegrees"])
    return {"X": math.cos(angle) * x - math.sin(angle) * z,
            "Y": point["Y"] - frame["Origin"]["Y"],
            "Z": math.sin(angle) * x + math.cos(angle) * z}


def validate_sources(world, profile, simulation):
    if world.get("SchemaVersion") != 2 or world.get("SpatialProfileId") != profile.get("ProfileId"):
        raise LoadError("SCHEMA2_WORLD_PROFILE_MISMATCH")
    regions = profile.get("Regions", [])
    if len(regions) != 13 or len({r.get("Id") for r in regions}) != 13 or len(profile.get("Portals", [])) != 12:
        raise LoadError("COMPLETE13REGION_PROFILE_REQUIRED")
    frame_ids = {f["FrameId"] for f in profile["Frames"]}
    if frame_ids != {f["FrameId"] for f in world["Frames"]}:
        raise LoadError("INITIAL_FRAME_SET_MISMATCH")
    for region in regions:
        if region["FrameId"] not in frame_ids or any(not math.isfinite(region["Hub"][k]) for k in ("X", "Y", "Z")):
            raise LoadError("INVALID_REGION_POSE")
    if simulation.get("SchemaVersion") != 1 or simulation.get("NpcCount") != 100 or simulation.get("Schedule", {}).get("MaximumActive") != 2:
        raise LoadError("SIMULATION100NPC2INCIDENT_PROFILE_REQUIRED")
    if len(simulation.get("Trains", [])) != 2:
        raise LoadError("TWO_TRAIN_PROFILE_REQUIRED")
    ids = {r["Id"] for r in regions}
    if not simulation.get("NpcSpawnRegions") or not set(simulation["NpcSpawnRegions"]) <= ids:
        raise LoadError("INVALID_NPC_SPAWN_REGION")


def client_pose(profile, index, counts, layout):
    regions = {r["Id"]: r for r in profile["Regions"]}
    if layout == "gathered":
        region = regions["station_concourse_2f"]
        point = copy.deepcopy(region["Hub"])
        point["X"] += (index % 5 - 2) * 1.2
        point["Z"] += (index // 5 - 1.5) * 1.2
    else:
        region = regions["station_concourse_2f"] if index == 0 else profile["Regions"][(index - 1) % 13]
        point = copy.deepcopy(region["Hub"])
        point["X"] += counts.get(region["Id"], 0) * 1.2
    counts[region["Id"]] = counts.get(region["Id"], 0) + 1
    frame = next(f for f in profile["Frames"] if f["FrameId"] == region["FrameId"])
    return region, point, local(frame, point)


def patrol(region, point, exit_after, index):
    if region["FrameId"] != "world":
        # The old probe contract has world-space goals only. Hold so the authoritative
        # frame carries this participant; do not target a stale parked-car world position.
        waypoints = []
    else:
        waypoints = []
        for cycle in range(math.ceil(exit_after / 4) + 1):
            for dx, dz in ((.3, 0), (0, .3), (-.3, 0), (0, -.3)):
                goal = copy.deepcopy(point)
                goal["X"] += dx
                goal["Z"] += dz
                waypoints.append({"Position": goal, "RegionId": region["Id"], "WaitSeconds": 1,
                                  "Operations": 0})
    return {"Steps": [], "Waypoints": waypoints, "ExitWhenRouteComplete": False,
            "ExitAfterSeconds": exit_after}


def prepare(config: Config):
    config.validate()
    project, session = config.project.resolve(), config.session.resolve()
    client_binary, client_platform = resolve_player(config.player)
    server_binary, server_platform = resolve_player(config.server_player or config.player)
    sources = {"world": project / "foundation/network/connected-world-layout.json",
               "world_profile": project / "foundation/world/connected-world-profile.json",
               "simulation": project / "foundation/world/foundation-simulation-profile.json"}
    world, profile, simulation = (read_json(sources[key]) for key in ("world", "world_profile", "simulation"))
    try:
        validate_sources(world, profile, simulation)
    except (KeyError, TypeError, OverflowError) as exc:
        raise LoadError("INVALID_SOURCE_CONTRACT") from exc
    if not session.parent.is_dir():
        raise LoadError("SESSION_PARENT_MISSING")
    try:
        session.mkdir(mode=0o700)
    except FileExistsError as exc:
        raise LoadError("SESSION_EXISTS_NO_OVERWRITE") from exc
    for folder in ("records", "logs", "metrics", "credentials", "probes"):
        (session / folder).mkdir(mode=0o700)
    world = copy.deepcopy(world)
    world.update(ShiftId="load-" + secrets.token_hex(16), Participants=[], Reports=[], Receipts=[], Sequence=0, Paused=False)
    world["Entities"] = [e for e in world["Entities"] if e.get("Kind") == 0]
    simulation = copy.deepcopy(simulation)
    original_profile_id = simulation["ProfileId"]
    simulation["ProfileId"] += "-load-" + config.layout
    simulation["NpcSpawnRegions"] = (["station_concourse_2f"] if config.layout == "gathered" else
                                      [r["Id"] for r in profile["Regions"]])
    tickets, clients, counts = [], [], {}
    exit_after = config.duration + config.startup_timeout + config.warmup_timeout + config.metrics_grace + 120
    for i in range(config.clients):
        participant = "instructor" if i == 0 else f"participant-{i:02d}"
        role = "instructor" if i == 0 else f"role-{(i - 1) % 5 + 1:02d}"
        team = "command" if i == 0 else f"team-{(i - 1) // 5 + 1}"
        region, point, local_point = client_pose(profile, i, counts, config.layout)
        world["Participants"].append({"ParticipantId": participant, "TeamId": team, "RoleId": role,
            "IsInstructor": i == 0, "InputEnabled": False, "RegionId": region["Id"], "FrameId": region["FrameId"],
            "PortalId": "", "Position": point, "LocalPosition": local_point, "ObservedIds": []})
        secret = secrets.token_urlsafe(32)
        tickets.append({"ParticipantId": participant, "SecretSha256": hashlib.sha256(secret.encode("utf-8")).hexdigest()})
        credential = session / "credentials" / f"client-{i:02d}.json"
        probe = session / "probes" / f"client-{i:02d}.json"
        private_json(credential, {"WorldId": world["WorldId"], "ShiftId": world["ShiftId"], "ParticipantId": participant, "Secret": secret})
        private_json(probe, patrol(region, point, exit_after, i))
        clients.append({"Label": f"client-{i:02d}", "RoleId": role, "Credential": str(credential), "Probe": str(probe),
                        "Patrol": "held_vehicle_rider" if region["FrameId"] != "world" else "bounded_station_loop"})
    private_json(session / "server-session.json", {"World": world, "Tickets": tickets})
    private_json(session / "server-simulation.json", simulation)
    plan = {"Schema": 1, "Status": "PREPARED_NOT_RUN", "ClientCount": 20, "Layout": config.layout,
        "DurationSeconds": config.duration, "RoleCounts": dict(Counter(c["RoleId"] for c in clients)),
        "SessionSchema": "public Schema2 seed; server coordinator creates safe100NPC/2train Schema3 state",
        "SpawnValidation": "provisional Python placements; server must validate/rebind against authored colliders and complete roster",
        "NpcSpawnRegions": simulation["NpcSpawnRegions"], "SimulationSourceProfileId": original_profile_id,
        "SimulationChanges": ["ProfileId load suffix", "NpcSpawnRegions selected layout"],
        "SourceSha256": {key: file_sha(path) for key, path in sources.items()},
        "SimulationCopySha256": file_sha(session / "server-simulation.json"),
        "ClientBinary": str(client_binary), "ServerBinary": str(server_binary),
        "ClientPlatform": client_platform, "ServerPlatform": server_platform,
        "ClientBinarySha256": file_sha(client_binary), "ServerBinarySha256": file_sha(server_binary),
        "Clients": clients, "Port": config.port,
        "Limits": ["headless protocol load only", "station-local patrol; train clients held until frame-relative probe API exists",
                   "no interaction/role procedure acceptance", "no voice/WAN/desktop renderer acceptance", "runtime metrics producer integration pending"]}
    private_json(session / "run-plan.json", plan)
    return plan


def commands(config, plan):
    session = config.session.resolve()
    common = ["-batchmode", "-nographics", "--cg-port", str(config.port), "--cg-address", "127.0.0.1", "--cg-listen", "127.0.0.1"]
    server = [plan["ServerBinary"], *common, "-logFile", str(session / "logs/server.player.log"),
              "--cg-mode", "server", "--cg-session", str(session / "server-session.json"),
              "--cg-records", str(session / "records"), "--cg-simulation", str(session / "server-simulation.json"),
              "--cg-metrics", str(session / "metrics/server.jsonl")]
    result = {"server": server}
    for client in plan["Clients"]:
        label = client["Label"]
        result[label] = [plan["ClientBinary"], *common, "-logFile", str(session / "logs" / f"{label}.player.log"),
            "--cg-mode", "client", "--cg-credential", client["Credential"], "--cg-probe", client["Probe"],
            "--cg-metrics", str(session / "metrics" / f"{label}.jsonl")]
    return result


class OwnedProcesses:
    def __init__(self, session, *, popen=subprocess.Popen, platform=os.name, group_signal=os.killpg if hasattr(os, "killpg") else None,
                 group_probe=None, clock=time.monotonic, sleep=time.sleep, job_factory=None):
        self.session, self.popen, self.platform = session, popen, platform
        self.group_signal, self.group_probe, self.clock, self.sleep = group_signal, group_probe, clock, sleep
        self.processes, self.handles = {}, []
        self.incomplete_cleanup = False
        self.job = None
        if platform == "nt":
            if job_factory is None:
                from foundation_load_windows import WindowsJob
                job_factory = WindowsJob
            self.job = job_factory()

    def launch(self, label, argv):
        if label in self.processes or label != "server" and not (label.startswith("client-") and label[7:].isdigit()):
            raise LoadError("INVALID_OR_DUPLICATE_PROCESS_LABEL")
        handle = os.fdopen(os.open(self.session / "logs" / f"{label}.stdio.log", os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), "wb")
        self.handles.append(handle)
        kwargs = {"stdout": handle, "stderr": subprocess.STDOUT, "stdin": subprocess.DEVNULL, "shell": False,
                  "cwd": str(self.session)}
        if self.platform == "posix":
            kwargs["start_new_session"] = True
        else:
            kwargs["creationflags"] = getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0x200) | getattr(subprocess, "CREATE_SUSPENDED", 4)
        process = None
        try:
            process = self.popen(argv, **kwargs)
            self.processes[label] = process
            if self.job is not None:
                self.job.assign(process._handle)
                self.job.resume(process.pid)
        except OSError as exc:
            if process is not None:
                self._signal(process, kill=True, record_failure=False)
                # A successfully-created suspended Windows child remains owned until
                # termination is observed. Retaining Popen is the bounded retry handle.
                if process.poll() is not None:
                    self.processes.pop(label, None)
            else:
                self.processes.pop(label, None)
            handle.close()
            raise LoadError("PLAYER_LAUNCH_FAILED") from exc
        return process

    def check(self):
        for label, process in self.processes.items():
            code = process.poll()
            if code is not None:
                raise LoadError("SERVER_EARLY_EXIT" if label == "server" else "CLIENT_EARLY_EXIT")

    def _signal(self, process, *, kill, record_failure=True):
        try:
            if self.platform == "posix":
                self.group_signal(process.pid, signal.SIGKILL if kill else signal.SIGTERM)
            elif kill:
                process.kill()
            else:
                try:
                    process.send_signal(getattr(signal, "CTRL_BREAK_EVENT", 1))
                except (OSError, ValueError):
                    process.terminate()
            return True
        except ProcessLookupError:
            return False
        except OSError:
            if record_failure:
                self.incomplete_cleanup = True
            return False

    def close(self, grace=10):
        # A POSIX PGID is identified by a reusable number. Popen.wait() reaps the
        # leader and revokes the only stdlib-visible authority tying that number to our
        # launch. Escalate only after wait reports a timeout: then the original leader
        # is still live and the PGID is still its launch identity. If wait succeeds,
        # never issue a later numeric group signal; preserve the uncertainty instead.
        live_at_close = {id(process): process.poll() is None for process in self.processes.values()}
        if self.platform == "posix":
            deadline = self.clock() + grace
            for process in self.processes.values():
                if not live_at_close[id(process)]:
                    self.incomplete_cleanup = True
                    continue
                self._signal(process, kill=False)
                try:
                    process.wait(timeout=max(0, deadline - self.clock()))
                except subprocess.TimeoutExpired:
                    # wait timed out without reaping the leader, so SIGKILL remains
                    # bound to this process group's current, owned leader.
                    self._signal(process, kill=True)
                    try:
                        process.wait(timeout=0)
                    except (subprocess.TimeoutExpired, OSError):
                        self.incomplete_cleanup = True
                except OSError:
                    self.incomplete_cleanup = True
                else:
                    # TERM may have reaped the leader while an unknown descendant
                    # remains. Numeric PGID ownership is no longer demonstrable.
                    self.incomplete_cleanup = True
        else:
            for process in self.processes.values():
                if live_at_close[id(process)]:
                    self._signal(process, kill=False)
            deadline = self.clock() + grace
            deadline = self.clock() + grace
            for process in self.processes.values():
                try:
                    process.wait(timeout=max(0, deadline - self.clock()))
                except subprocess.TimeoutExpired:
                    self._signal(process, kill=True)
                except OSError:
                    self.incomplete_cleanup = True
        if self.job is not None:
            try:
                self.job.terminate()
            except OSError:
                self.incomplete_cleanup = True
            finally:
                try:
                    self.job.close()
                except OSError:
                    self.incomplete_cleanup = True
        deadline = self.clock() + 3
        for process in self.processes.values():
            if process.poll() is None:
                try:
                    process.wait(timeout=max(0, deadline - self.clock()))
                except (subprocess.TimeoutExpired, OSError):
                    self.incomplete_cleanup = True
        for handle in self.handles:
            handle.close()


def canonical_json(value):
    return json.dumps(value, ensure_ascii=False, allow_nan=False, sort_keys=True, separators=(",", ":"))


def canonical_sha256(value):
    return hashlib.sha256(canonical_json(value).encode("utf-8")).hexdigest()


def summary_sha256(result):
    summary = {key: value for key, value in result.items() if key != "PublicationSafeReceipt"}
    return canonical_sha256(summary)


def publication_safe_receipt(result, plan, metric_hashes):
    return {"Schema": 1, "Scope": PUBLICATION_RECEIPT_SCOPE, "Acceptance": "NOT_ASSESSED",
            "SummarySha256": summary_sha256(result), "PlanCanonicalSha256": canonical_sha256(plan),
            "MeasurementWindow": {"RequiredDurationSeconds": result["RequiredDurationSeconds"],
                                  "ObservedWallSeconds": result["ObservedWallSeconds"]},
            "MetricFilesSha256": metric_hashes, "SourceSha256": plan["SourceSha256"],
            "SimulationCopySha256": plan["SimulationCopySha256"],
            "ClientBinarySha256": plan.get("ClientBinarySha256"),
            "ServerBinarySha256": plan.get("ServerBinarySha256"),
            "HashMeaning": "unkeyed SHA-256 checksums for source and local result binding; not authenticity"}


class FaultLogReader:
    """Bounded incremental error detection; raw messages remain in private player logs.

    R6B item 2: the grouped marker dispatch is derived from the same canonical table
    the metrics exact-variant dispatch uses, and both are checked for agreement here,
    so the log channel and the metrics channel cannot select different cases.
    """
    MARKERS = tuple(marker.encode('ascii') for marker, _ in GROUPED_FAULT_MARKERS)
    def __init__(self, path):
        assert_fault_dispatch_agreement()
        self.path, self.offset, self.pending, self.identity = path, 0, b'', None
    def poll(self, *, final=False):
        if not self.path.exists():
            if self.identity is not None:
                raise LoadError("PLAYER_LOG_DISAPPEARED")
            return False
        stat_result = self.path.stat()
        identity = (stat_result.st_dev, stat_result.st_ino)
        if ((self.identity is not None and identity != self.identity) or
                stat_result.st_size < self.offset or stat_result.st_size > 256 * 1024 * 1024):
            raise LoadError("PLAYER_LOG_REPLACED_OR_OVERSIZED")
        self.identity = identity
        found = set()
        with self.path.open('rb') as stream:
            stream.seek(self.offset)
            while True:
                data = stream.read(1024 * 1024)
                self.offset += len(data)
                joined = self.pending + data.lower()
                # The case is resolved through the shared table; only the canonical
                # id leaves this reader, never the matched private log text.
                found.update(dispatch_fault_case(grouped_marker=marker.decode('ascii'))
                             for marker in self.MARKERS if marker in joined)
                # Retain only marker-overlap bytes; no arbitrary raw log content is copied to results.
                self.pending = joined[-64:]
                if not final or not data:
                    break
        if len(found) > 1:
            # Two canonical cases matched one observed line: the grouped dispatch is
            # not a function of the observation, so it cannot be reported as one case.
            raise LoadError("PLAYER_FAULT_LOG_DISPATCH_AMBIGUOUS")
        return found.pop() if found else None


def run(config, plan, *, manager_factory=OwnedProcesses, clock=time.monotonic, sleep=time.sleep,
        reader_factory=MetricsReader, emit: Callable = lambda value: print(json.dumps(value), flush=True)):
    """Future execution path. Tests inject all processes, clocks and readers; this task never calls live run()."""
    argv = commands(config, plan)
    manager = manager_factory(config.session.resolve())
    readers = {label: reader_factory(config.session.resolve() / "metrics" / f"{label}.jsonl", "server" if label == "server" else "client") for label in argv}
    starts = {label: 0 for label in readers}
    fault_logs = [FaultLogReader(config.session.resolve() / "logs" / f"{label}.player.log") for label in argv]
    measured_at = None
    stops = None
    pre_cleanup_watermarks = None
    failure = None
    # R6B item 2: every fault the log channel resolves is recorded by its canonical
    # case id, so the reported case and the metrics exact-variant case are comparable.
    fault_cases = []

    def poll():
        manager.check()
        for log in fault_logs:
            case = log.poll()
            if case and case not in fault_cases:
                fault_cases.append(case)
        if fault_cases:
            raise LoadError("PLAYER_FAULT_LOG_DETECTED")
        for reader in readers.values():
            reader.poll()
            if reader.errors:
                raise LoadError("RUNTIME_METRICS_INVALID_OR_FAULT")

    try:
        manager.launch("server", argv["server"])
        deadline = clock() + config.startup_timeout
        while not readers["server"].intervals:
            poll()
            if clock() >= deadline:
                raise LoadError("SERVER_METRICS_STARTUP_TIMEOUT")
            sleep(.1)
        for client in plan["Clients"]:
            manager.launch(client["Label"], argv[client["Label"]])
            sleep(config.launch_stagger)
            poll()
        deadline = clock() + config.warmup_timeout
        while not ready_for_measurement(readers):
            poll()
            if clock() >= deadline:
                raise LoadError("20CLIENT_100NPC_2INCIDENT_WARMUP_UNVERIFIED")
            sleep(.1)
        # Schema1 rows are ordered accumulator snapshots, and the producer resets a
        # successor's start to the preceding close boundary. Discard one post-start
        # fence row per process so a known straddling warmup row is not assessed.
        # This is only a conservative file-observation mitigation: without a producer
        # epoch acknowledgement or producer monotonic endpoints, Python cannot prove
        # that every subsequently observed byte was closed after measured_at.
        measured_at = clock()
        emit({"Status": "MEASUREMENT_STARTED", "ClientCount": 20, "DurationSeconds": config.duration, "Layout": config.layout})
        boundaries = {label: len(reader.intervals) + 1 for label, reader in readers.items()}
        boundary_deadline = clock() + config.metrics_grace
        while any(len(readers[label].intervals) < count for label, count in boundaries.items()):
            poll()
            if clock() >= boundary_deadline:
                raise LoadError("MEASUREMENT_START_BOUNDARY_UNVERIFIED")
            sleep(.1)
        # The fence row itself may straddle measured_at; begin at its successor rather
        # than at every row that happened to be read in the same poll batch.
        starts = boundaries
        next_report = measured_at + 15
        while True:
            elapsed = clock() - measured_at
            # Once duration has elapsed, observe the current file size before any
            # further ordinary poll. This captures a newly written multi-chunk
            # pre-cleanup backlog at its first visible boundary.
            if elapsed >= config.duration:
                pre_poll_watermarks = {
                    label: getattr(reader, "capture_watermark", lambda: None)()
                    for label, reader in readers.items()
                }
                if any(
                    watermark is not None and readers[label].offset < watermark.end_offset
                    for label, watermark in pre_poll_watermarks.items()
                ):
                    pre_cleanup_watermarks = pre_poll_watermarks
                    break
            poll()
            elapsed = clock() - measured_at
            covered = all(
                math.fsum(row.seconds for row in reader.intervals[starts[label]:]) >= config.duration
                for label, reader in readers.items()
            )
            # At duration, freeze each producer's observed byte boundary before
            # cleanup. A stop is safe when every reader already covers the duration
            # or has unread pre-cleanup bytes that may establish it after the bounded
            # final drain. Do not consume those bytes in another ordinary poll.
            if elapsed >= config.duration:
                candidate_watermarks = {
                    label: getattr(reader, "capture_watermark", lambda: None)()
                    for label, reader in readers.items()
                }
                can_cover = all(
                    math.fsum(row.seconds for row in reader.intervals[starts[label]:]) >= config.duration
                    or (
                        candidate_watermarks[label] is not None
                        and reader.offset < candidate_watermarks[label].end_offset
                    )
                    for label, reader in readers.items()
                )
                has_unread_precleanup_bytes = any(
                    watermark is not None and readers[label].offset < watermark.end_offset
                    for label, watermark in candidate_watermarks.items()
                )
                # If a producer has just exposed a multi-chunk backlog, freeze it
                # now rather than allowing the next ordinary poll to consume the
                # remainder. For fully drained ordinary streams, wait for coverage.
                if can_cover and (covered or has_unread_precleanup_bytes):
                    pre_cleanup_watermarks = candidate_watermarks
                    break
            if clock() >= next_report:
                emit({"Status": "MEASURING", "ElapsedSeconds": round(elapsed, 1), "OwnedProcesses": len(manager.processes)})
                next_report += 15
            sleep(.2)
    except (LoadError, MetricsError) as exc:
        failure = str(exc)
    except KeyboardInterrupt:
        failure = "INTERRUPTED"
    except OSError:
        failure = "LOCAL_IO_FAILURE"
    finally:
        # R6B items 4 and 5: the local CLEANUP_BEGIN deadline is captured at the
        # instant cleanup begins, before any blocking close, so the window end is a
        # declared reference instead of an omission. The measurement window ends
        # here; shutdown work after this instant is not measurement evidence.
        cleanup_begin = clock()
        wall = 0 if measured_at is None else cleanup_begin - measured_at
        try:
            manager.close(config.shutdown_grace)
        except (OSError, LoadError):
            manager.incomplete_cleanup = True
        if pre_cleanup_watermarks is not None:
            # Drain only the bytes observed before cleanup. The identity and size
            # checks reject truncation/replacement, and stops are frozen before the
            # later final=True reader pass can ingest shutdown-only metric rows.
            for label, reader in readers.items():
                watermark = pre_cleanup_watermarks[label]
                try:
                    if watermark is not None:
                        if isinstance(reader, MetricsReader):
                            MetricsReader.poll(reader, through=watermark)
                        else:
                            reader.poll(through=watermark)
                    if reader.errors and failure is None:
                        failure = "FINAL_METRIC_READ_FAILED"
                except (MetricsError, OSError):
                    if failure is None:
                        failure = "FINAL_METRIC_READ_FAILED"
            stops = {label: len(reader.intervals) for label, reader in readers.items()}
        elif stops is None:
            # Without a completed, captured stop boundary the producer contract cannot
            # attribute later buffered rows to measurement rather than cleanup.
            stops = {label: len(reader.intervals) for label, reader in readers.items()}
        if pre_cleanup_watermarks is not None and failure is None:
            covered = all(
                math.fsum(row.seconds for row in reader.intervals[starts[label]:stops[label]]) >= config.duration
                for label, reader in readers.items()
            )
            if not covered:
                failure = "MEASUREMENT_COVERAGE_UNVERIFIED"
        for log in fault_logs:
            try:
                case = log.poll(final=True)
                if case and case not in fault_cases:
                    fault_cases.append(case)
                if case and failure is None:
                    failure = "PLAYER_FAULT_LOG_DETECTED"
            except (LoadError, OSError):
                if failure is None:
                    failure = "FINAL_PLAYER_LOG_READ_FAILED"
        for reader in readers.values():
            error_count = len(reader.errors)
            try:
                reader.poll(final=True)
            except (MetricsError, OSError):
                reader.errors.append("FINAL_METRIC_READ_FAILED")
                if failure is None:
                    failure = "FINAL_METRIC_READ_FAILED"
            else:
                if len(reader.errors) > error_count and failure is None:
                    failure = "FINAL_METRIC_READ_FAILED"
    if manager.incomplete_cleanup:
        failure = "OWNED_PROCESS_CLEANUP_INCOMPLETE"
    result = assess(readers, starts, config.duration, wall, process_failure=failure, stops=stops,
                    cleanup_begin_seconds=None if measured_at is None else wall,
                    overall_deadline_seconds=None if measured_at is None else
                    config.duration + config.metrics_grace + config.shutdown_grace)
    result["PreparedLayout"] = config.layout
    result["FaultCases"] = list(fault_cases)
    result["CleanupBegin"] = {"ObservedSeconds": None if measured_at is None else wall,
                              "LocalDeadlineSeconds": None if measured_at is None else config.duration + config.metrics_grace,
                              "WithinLocalDeadline": None if measured_at is None else wall <= config.duration + config.metrics_grace}
    result["SourceSha256"] = plan["SourceSha256"]
    result["SimulationCopySha256"] = plan["SimulationCopySha256"]
    result["ExitCodes"] = {label: process.poll() for label, process in manager.processes.items()}
    metric_hashes = {}
    for label, reader in readers.items():
        path = getattr(reader, "path", None)
        metric_hashes[label] = file_sha(path) if isinstance(path, Path) and path.is_file() else None
    result["PublicationSafeReceipt"] = publication_safe_receipt(result, plan, metric_hashes)
    private_json(config.session.resolve() / "result.json", result)
    emit({"Status": result["Status"], "Acceptance": "NOT_ASSESSED", "IssueCount": len(result["Issues"]), "ClientCount": 20})
    return result


class SafeArgumentParser(argparse.ArgumentParser):
    def error(self, message):
        # argparse normally echoes invalid argument values; these may be private.
        self.exit(2, '{"Status":"ERROR","Code":"INVALID_CLI_ARGUMENTS","Acceptance":"NOT_ASSESSED"}\n')


def main(argv=None):
    parser = SafeArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path.cwd())
    parser.add_argument("--session", required=True, type=Path)
    parser.add_argument("--player", required=True, type=Path, help="Selected .app, .exe, .x86_64 or native executable")
    parser.add_argument("--server-player", type=Path)
    parser.add_argument("--prepare-only", action="store_true")
    parser.add_argument("--duration", type=float, default=3600)
    parser.add_argument("--layout", choices=("dispersed", "gathered"), default="dispersed")
    parser.add_argument("--clients", type=int, choices=(20,), default=20)
    parser.add_argument("--port", type=int, default=17979)
    parser.add_argument("--startup-timeout", type=float, default=60)
    parser.add_argument("--warmup-timeout", type=float, default=300)
    parser.add_argument("--metrics-grace", type=float, default=10)
    parser.add_argument("--shutdown-grace", type=float, default=10)
    parser.add_argument("--launch-stagger", type=float, default=.15)
    args = parser.parse_args(argv)
    config = Config(**{key: value for key, value in vars(args).items() if key != "prepare_only"})
    try:
        plan = prepare(config)
        if args.prepare_only:
            print(json.dumps({"Status": "PREPARED_NOT_RUN", "ClientCount": 20, "DurationSeconds": config.duration,
                              "Layout": config.layout, "Acceptance": "NOT_ASSESSED"}))
            return 0
        result = run(config, plan)
        return {"LOCAL_PROTOCOL_RUN_RECORDED": 0, "RUN_FAILED": 2, "LOAD_BUDGET_EXCEEDED": 3, "METRICS_INCOMPLETE": 4}[result["Status"]]
    except (LoadError, MetricsError) as exc:
        print(json.dumps({"Status": "ERROR", "Code": str(exc), "Acceptance": "NOT_ASSESSED"}))
        return 2
    except KeyboardInterrupt:
        print(json.dumps({"Status": "ERROR", "Code": "INTERRUPTED", "Acceptance": "NOT_ASSESSED"}))
        return 130
    except (OSError, KeyError, TypeError, ValueError):
        print(json.dumps({"Status": "ERROR", "Code": "LOCAL_PREPARATION_OR_SCHEMA_FAILURE", "Acceptance": "NOT_ASSESSED"}))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
