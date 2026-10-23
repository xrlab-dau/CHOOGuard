#!/usr/bin/env python3
"""Short lived, root-approved manifest-only cleanup cycles for launchd.

No directory traversal, recursive removal, Unity command, or network access.
Every deleted file must match an explicitly approved manifest and stat tuple.
"""
from __future__ import annotations

import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import errno
import fcntl
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import stat
import subprocess
import sys
import time

GIB = 1024 ** 3
HARD_PROTECTED = (
    "Assets", "Packages", "ProjectSettings", ".git", "Library", "Temp",
    "UserSettings", "obj", "source-evidence", "asset-library",
    "workers/twin/evidence", ".state", "tools/maintenance", "AGENTS.md",
    ".agents", ".codex", ".checkpoints",
)
AUTHORITY = ".state/WRITER-AUTHORITY.json"
QUEUE = ".state/SCENE-WORKER-QUEUE-C180.json"
POLICY = ".state/SCENE-WORKER-POLICY-C180.json"
QUEUE_LOCK = ".state/SCENE-WORKER-QUEUE-C180.lock"
STATE_DIR = ".state/cleanup-daemon"
MAX_JSON = 8 * 1024 ** 2


class Refusal(Exception):
    pass


def utc_now():
    return datetime.now(timezone.utc).isoformat()


def rel_path(value):
    if not isinstance(value, str) or not value or "\\" in value:
        raise Refusal("Invalid relative path")
    p = PurePosixPath(value)
    if p.is_absolute() or any(x in ("", ".", "..") for x in value.split("/")):
        raise Refusal("Absolute, empty, or parent path component")
    return str(p)


def within(path, root):
    return path == root or path.startswith(root + "/")


def no_symlinks(path):
    p = Path(path)
    for part in (p, *p.parents):
        if part.is_symlink():
            raise Refusal("Symlink path refused: " + str(part))


def read_bytes(path, max_bytes=MAX_JSON):
    no_symlinks(path)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    try:
        info = os.fstat(fd)
        if not stat.S_ISREG(info.st_mode) or info.st_size > max_bytes:
            raise Refusal("Input is not a bounded regular file: " + str(path))
        data = os.read(fd, max_bytes + 1)
        if len(data) > max_bytes:
            raise Refusal("JSON input too large")
        return data
    finally:
        os.close(fd)


def read_json(path):
    return json.loads(read_bytes(path))


def parse_time(value):
    if not isinstance(value, str):
        raise Refusal("UTC timestamp required")
    dt = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if dt.tzinfo is None:
        raise Refusal("Timestamp must include timezone")
    return dt.timestamp()


def fingerprint(info):
    return {"device": info.st_dev, "inode": info.st_ino,
            "size": info.st_size, "mtime_ns": info.st_mtime_ns}


def atomic_json(path, value):
    no_symlinks(path)
    temp = path.with_name(path.name + ".new-" + str(os.getpid()))
    fd = os.open(temp, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as out:
            json.dump(value, out, ensure_ascii=False, separators=(",", ":"))
            out.write("\n")
            out.flush()
            os.fsync(out.fileno())
        os.replace(temp, path)
    finally:
        if temp.exists():
            temp.unlink()


def read_config(path):
    cfg = read_json(Path(path).absolute())
    if cfg.get("schema_version") != 1:
        raise Refusal("Unsupported configuration schema")
    root = Path(cfg["project_root"])
    if not root.is_absolute() or not root.is_dir():
        raise Refusal("Existing absolute project_root required")
    no_symlinks(root)
    root = root.resolve()
    for name in ("allowed_roots", "protected_paths", "active_plans"):
        if not isinstance(cfg.get(name), list):
            raise Refusal(name + " must be an explicit list")
        cfg[name] = [rel_path(x.rstrip("/")) for x in cfg[name]]
    for allowed in cfg["allowed_roots"]:
        if any(within(allowed, p) or within(p, allowed) for p in HARD_PROTECTED):
            raise Refusal("Whitelist overlaps protected root: " + allowed)
    manifests = cfg.get("approved_manifests", [])
    if not isinstance(manifests, list) or len(manifests) > 128:
        raise Refusal("At most 128 explicitly approved manifests")
    for item in manifests:
        rel = rel_path(item["path"])
        if not within(rel, STATE_DIR + "/retirements") or not rel.endswith(".json"):
            raise Refusal("Manifest must live in dedicated retirements directory")
        digest = item.get("sha256", "")
        if len(digest) != 64 or any(c not in "0123456789abcdef" for c in digest):
            raise Refusal("Manifest SHA256 required")
    for key, default, low, high in (
        ("quiet_seconds", 3600, 3600, 31536000),
        ("max_entries_per_cycle", 2000, 1, 20000),
        ("max_files_per_cycle", 1000, 1, 5000),
        ("max_bytes_per_cycle", 4 * GIB, 1, 32 * GIB),
        ("max_seconds_per_cycle", 20, 1, 30),
        ("log_max_bytes", 524288, 4096, 1048576),
        ("log_backups", 2, 1, 3),
        ("normal_free_bytes", 30 * GIB, 1, 1024 * GIB),
        ("critical_free_bytes", 12 * GIB, 1, 1024 * GIB),
    ):
        value = cfg.setdefault(key, default)
        if not isinstance(value, int) or not low <= value <= high:
            raise Refusal("Out of bounds setting: " + key)
    if cfg["normal_free_bytes"] < cfg["critical_free_bytes"]:
        raise Refusal("normal_free_bytes must be >= critical_free_bytes")
    recurring = cfg.setdefault("recurring_expired_files", [])
    if not isinstance(recurring, list) or len(recurring) > 8:
        raise Refusal("At most 8 root-approved exact recurring temporary file paths")
    seen = set()
    for item in recurring:
        path = rel_path(item["path"])
        if path in seen or any(c in path for c in "*?[]"):
            raise Refusal("Recurring paths must be unique exact leaves without glob syntax")
        seen.add(path)
        if not any(within(path, x) and path != x for x in cfg["allowed_roots"]):
            raise Refusal("Recurring path must be below an explicit allowed_root")
        if any(within(path, x) for x in HARD_PROTECTED):
            raise Refusal("Recurring path is protected")
        if (not isinstance(item.get("quiet_seconds"), int)
                or item["quiet_seconds"] < cfg["quiet_seconds"]
                or not isinstance(item.get("reason"), str) or not item["reason"].strip()):
            raise Refusal("Recurring path requires quiet_seconds and explicit root reason")
    return root, cfg


def state_directory(root):
    path = root / STATE_DIR
    no_symlinks(path)
    path.mkdir(mode=0o700, parents=True, exist_ok=True)
    return path


def log_event(state, cfg, event):
    path = state / "events.jsonl"
    no_symlinks(path)
    encoded = (json.dumps({"utc": utc_now(), **event}, ensure_ascii=False,
                          separators=(",", ":")) + "\n").encode()
    if path.exists() and path.stat().st_size + len(encoded) > cfg["log_max_bytes"]:
        for index in range(cfg["log_backups"], 0, -1):
            previous = path if index == 1 else state / ("events.jsonl." + str(index - 1))
            target = state / ("events.jsonl." + str(index))
            no_symlinks(target)
            no_symlinks(previous)
            if previous.exists():
                os.replace(previous, target)
    fd = os.open(path, os.O_WRONLY | os.O_APPEND | os.O_CREAT | os.O_NOFOLLOW, 0o600)
    try:
        os.write(fd, encoded)
    finally:
        os.close(fd)


def free_bytes(root):
    fs = os.statvfs(root)
    return fs.f_bavail * fs.f_frsize


def pressure(free, cfg):
    if free < cfg["critical_free_bytes"]:
        return "critical"
    if free < cfg["normal_free_bytes"]:
        return "low"
    return "normal"


def protections(root, cfg):
    protected = list(HARD_PROTECTED) + cfg["protected_paths"] + cfg["active_plans"]
    # Explicit protection file is maintained by the orchestrator, never aged out.
    file = root / STATE_DIR / "active-protections.json"
    if file.exists():
        d = read_json(file)
        protected.extend(rel_path(x.rstrip("/")) for x in d.get("protected_paths", []))
    active = root / ".planning/.active_plan"
    if active.exists():
        value = read_bytes(active, 8192).decode().strip()
        if value:
            if value.startswith(str(root) + "/"):
                value = value[len(str(root)) + 1:]
            elif not value.startswith(".planning/"):
                value = ".planning/" + value
            protected.append(rel_path(value.rstrip("/")))
    policy = read_json(root / POLICY)
    for worker in policy.get("allowed_workers", []):
        if worker.get("allowed_scope_prefixes") and worker.get("may_execute_scene_save", True):
            protected.extend(rel_path(x.rstrip("/")) for x in worker.get("script_roots", []))
    for name in ("grant_dir", "receipt_dir"):
        if policy.get(name):
            protected.append(rel_path(policy[name].rstrip("/")))
    return tuple(set(protected))


@contextmanager
def native_idle_guard(root):
    path = root / QUEUE_LOCK
    no_symlinks(path)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    locked = False
    try:
        try:
            fcntl.flock(fd, fcntl.LOCK_SH | fcntl.LOCK_NB)
            locked = True
        except BlockingIOError:
            raise Refusal("C180 queue is being updated")
        auth = read_json(root / AUTHORITY)
        queue = read_json(root / QUEUE)
        if (auth.get("Native_inflight") is not False
                or auth.get("status") not in ("RELEASED", "IDLE")
                or queue.get("active_request") is not None
                or queue.get("pending")):
            raise Refusal("Active or pending C180 lease; cleanup yields")
        yield
    finally:
        if locked:
            fcntl.flock(fd, fcntl.LOCK_UN)
        os.close(fd)


def load_manifest(root, item):
    data = read_bytes(root / item["path"])
    if hashlib.sha256(data).hexdigest() != item["sha256"]:
        raise Refusal("Manifest hash changed: " + item["path"])
    manifest = json.loads(data)
    if (manifest.get("schema_version") != 1 or manifest.get("approved_by") != "root"
            or manifest.get("project_root") != str(root)
            or not manifest.get("manifest_id")):
        raise Refusal("Root approval and exact project scope required")
    parse_time(manifest["approved_at"])
    entries = manifest.get("entries")
    if not isinstance(entries, list) or len(entries) > 10000:
        raise Refusal("Manifest must contain at most 10000 exact file entries")
    return entries


def check_entry(entry, cfg, protected, now):
    path = rel_path(entry["path"])
    if not any(within(path, x) and path != x for x in cfg["allowed_roots"]):
        raise Refusal("Outside explicit cleanup whitelist")
    if any(within(path, x) for x in protected):
        raise Refusal("Protected current/source path")
    if entry.get("category") not in ("retired_artifact", "expired_temp"):
        raise Refusal("Retired artifact or expired temporary file required")
    if not isinstance(entry.get("reason"), str) or not entry["reason"].strip():
        raise Refusal("Retirement reason required")
    quiet = entry.get("quiet_seconds", cfg["quiet_seconds"])
    if not isinstance(quiet, int) or quiet < cfg["quiet_seconds"]:
        raise Refusal("Entry cannot shorten configured quiet period")
    if now < parse_time(entry["retire_after"]):
        raise Refusal("Retirement date has not arrived")
    fp = entry.get("fingerprint", {})
    if set(fp) != {"device", "inode", "size", "mtime_ns"} or not all(
            isinstance(v, int) and v >= 0 for v in fp.values()):
        raise Refusal("Exact file stat fingerprint required")
    if now - fp["mtime_ns"] / 1e9 < quiet:
        raise Refusal("File is inside quiet period")
    return path


@contextmanager
def parent_fd(root, relative):
    # dir_fd + O_NOFOLLOW stops parent symlink swaps and range escape.
    fd = os.open(root, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        parts = rel_path(relative).split("/")
        for part in parts[:-1]:
            nxt = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=fd)
            os.close(fd)
            fd = nxt
        yield fd, parts[-1]
    finally:
        os.close(fd)


def file_operation(root, path, expected, apply):
    with parent_fd(root, path) as (parent, leaf):
        fd = os.open(leaf, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK, dir_fd=parent)
        try:
            info = os.fstat(fd)
            if not stat.S_ISREG(info.st_mode) or info.st_nlink != 1:
                raise Refusal("Only single-link regular files can be retired")
            if fingerprint(info) != expected:
                raise Refusal("File changed since manifest approval")
            # Recheck the leaf binding after opening it; never follow a link.
            current = os.stat(leaf, dir_fd=parent, follow_symlinks=False)
            if fingerprint(current) != expected or not stat.S_ISREG(current.st_mode):
                raise Refusal("File binding changed")
            if apply:
                os.unlink(leaf, dir_fd=parent)
            return info.st_size
        finally:
            os.close(fd)


def closed_handles(root, path):
    """Bounded exact-file handle query; unknown is never treated as closed."""
    try:
        query = subprocess.run(["/usr/sbin/lsof", "-nP", "-t", "--", str(root / path)],
                               capture_output=True, text=True, timeout=5, check=False)
    except (OSError, subprocess.TimeoutExpired):
        return False, "open_handle_check_unavailable"
    if query.returncode == 1 and not query.stdout.strip() and not query.stderr.strip():
        return True, "closed"
    if query.returncode == 0 and query.stdout.strip():
        return False, "open_file_handles"
    return False, "open_handle_check_unavailable"


def recurring_cycle(root, cfg, previous, result, started, apply):
    observations = {}
    result["recurring_observations"] = observations
    for item in cfg["recurring_expired_files"]:
        if (time.monotonic() - started >= cfg["max_seconds_per_cycle"]
                or result["scanned"] >= cfg["max_entries_per_cycle"]
                or result["deleted_files"] >= cfg["max_files_per_cycle"]):
            break
        path = item["path"]
        result["scanned"] += 1
        try:
            # Only stat this explicitly registered leaf, through physical parents.
            with parent_fd(root, path) as (parent, leaf):
                info = os.stat(leaf, dir_fd=parent, follow_symlinks=False)
            if not stat.S_ISREG(info.st_mode) or info.st_nlink != 1:
                raise Refusal("Recurring target is not a single-link regular file")
            current = fingerprint(info)
            prior = previous.get("recurring_observations", {}).get(path, {})
            stable_since = prior.get("stable_since_epoch", time.time())
            if prior.get("fingerprint") != current:
                stable_since = time.time()
            observations[path] = {"fingerprint": current, "stable_since_epoch": stable_since,
                                  "last_observed_utc": utc_now(), "reason": item["reason"]}
            entry = {"path": path, "category": "expired_temp", "reason": item["reason"],
                     "quiet_seconds": item["quiet_seconds"], "fingerprint": current,
                     "retire_after": datetime.fromtimestamp(stable_since + item["quiet_seconds"], timezone.utc).isoformat()}
            protected = protections(root, cfg)
            check_entry(entry, cfg, protected, time.time())
            if result["deleted_logical_bytes"] + current["size"] > cfg["max_bytes_per_cycle"]:
                raise Refusal("Cycle byte budget; recurring file deferred")
            if time.monotonic() - started > cfg["max_seconds_per_cycle"] - 5:
                raise Refusal("Insufficient cycle time for bounded open-handle query")
            closed, detail = closed_handles(root, path)
            if not closed:
                raise Refusal(detail)
            with native_idle_guard(root):
                fresh = protections(root, cfg)
                check_entry(entry, cfg, fresh, time.time())
                size = file_operation(root, path, current, apply)
            result["eligible"] += 1
            if apply:
                result["deleted_files"] += 1
                result["deleted_logical_bytes"] += size
                observations.pop(path, None)
                log_event(root / STATE_DIR, cfg, {"event": "deleted_recurring_expired_temp",
                          "path": path, "fingerprint": current, "logical_bytes": size,
                          "reason": item["reason"]})
        except FileNotFoundError:
            result["skipped"] += 1
            result["skip_reasons"]["recurring_absent"] = result["skip_reasons"].get("recurring_absent", 0) + 1
        except Refusal as exc:
            why = str(exc)
            result["skipped"] += 1
            result["skip_reasons"][why] = result["skip_reasons"].get(why, 0) + 1
            if "C180" in why:
                raise
        except OSError as exc:
            if exc.errno in (errno.ELOOP, errno.ENOTDIR):
                result["skipped"] += 1
                result["skip_reasons"]["symlink_or_parent_type"] = result["skip_reasons"].get("symlink_or_parent_type", 0) + 1
            else:
                raise


def cycle(root, cfg, apply):
    state = state_directory(root)
    lock_fd = os.open(state / "cycle.lock", os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)
    try:
        try:
            fcntl.flock(lock_fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            return {"status": "already_running", "utc": utc_now()}
        previous = read_json(state / "status.json") if (state / "status.json").exists() else {}
        now, started = time.time(), time.monotonic()
        before = free_bytes(root)
        result = {"utc": utc_now(), "mode": "apply" if apply else "dry_run",
                  "status": "complete", "free_before_bytes": before,
                  "pressure": pressure(before, cfg), "scanned": 0, "eligible": 0,
                  "deleted_files": 0, "deleted_logical_bytes": 0, "skipped": 0,
                  "errors": 0, "skip_reasons": {}, "error_streak": 0,
                  "cursor": previous.get("cursor", {}), "manifest_cursor": 0,
                  "recurring_observations": previous.get("recurring_observations", {})}
        result["last_cycle_utc"] = result["utc"]
        result["next_due_utc"] = datetime.fromtimestamp(now + 300, timezone.utc).isoformat()
        if apply and not cfg.get("enabled", False):
            result["status"] = "disabled"
        elif apply and previous.get("retry_after_epoch", 0) > now:
            result["status"] = "error_backoff"
            result["retry_after_epoch"] = previous["retry_after_epoch"]
            result["error_streak"] = previous.get("error_streak", 0)
        else:
            try:
                with native_idle_guard(root):
                    protected = protections(root, cfg)
                recurring_cycle(root, cfg, previous, result, started, apply)
                manifests = cfg.get("approved_manifests", [])
                if manifests:
                    offset = previous.get("manifest_cursor", 0) % len(manifests)
                    manifests = manifests[offset:] + manifests[:offset]
                    result["manifest_cursor"] = (offset + 1) % len(manifests)
                valid_keys = {m["sha256"] for m in manifests}
                result["cursor"] = {k: v for k, v in result["cursor"].items() if k in valid_keys}
                stop = False
                for item in manifests:
                    if stop or time.monotonic() - started >= cfg["max_seconds_per_cycle"]:
                        break
                    entries = load_manifest(root, item)
                    count = len(entries)
                    start = result["cursor"].get(item["sha256"], 0) % max(1, count)
                    for index in range(count):
                        if (result["scanned"] >= cfg["max_entries_per_cycle"]
                                or result["deleted_files"] >= cfg["max_files_per_cycle"]
                                or time.monotonic() - started >= cfg["max_seconds_per_cycle"]):
                            stop = True
                            break
                        position = (start + index) % count
                        entry = entries[position]
                        result["scanned"] += 1
                        result["cursor"][item["sha256"]] = (position + 1) % count
                        try:
                            path = check_entry(entry, cfg, protected, time.time())
                            if (result["deleted_logical_bytes"] + entry["fingerprint"]["size"]
                                    > cfg["max_bytes_per_cycle"]):
                                raise Refusal("Cycle byte budget; larger file needs explicit configuration")
                            # The short shared lock prevents C180 claiming an author
                            # between the live-state check and this one unlink.
                            with native_idle_guard(root):
                                fresh = protections(root, cfg)
                                if any(within(path, x) for x in fresh):
                                    raise Refusal("Path is newly protected")
                                size = file_operation(root, path, entry["fingerprint"], apply)
                            result["eligible"] += 1
                            if apply:
                                result["deleted_files"] += 1
                                result["deleted_logical_bytes"] += size
                                log_event(state, cfg, {"event": "deleted", "path": path,
                                          "manifest": item["sha256"], "logical_bytes": size})
                        except FileNotFoundError:
                            result["skipped"] += 1
                            result["skip_reasons"]["already_absent"] = result["skip_reasons"].get("already_absent", 0) + 1
                        except Refusal as exc:
                            why = str(exc)
                            result["skipped"] += 1
                            result["skip_reasons"][why] = result["skip_reasons"].get(why, 0) + 1
                            if "C180" in why:
                                result["status"] = "native_writer_busy"
                                stop = True
                                break
                        except OSError as exc:
                            if exc.errno in (errno.ELOOP, errno.ENOTDIR):
                                result["skipped"] += 1
                                result["skip_reasons"]["symlink_or_parent_type"] = result["skip_reasons"].get("symlink_or_parent_type", 0) + 1
                            else:
                                raise
            except Refusal as exc:
                result["status"] = "native_writer_busy" if "C180" in str(exc) else "refused"
                result["detail"] = str(exc)
                if result["status"] != "native_writer_busy":
                    result["errors"] += 1
            except (OSError, ValueError, KeyError, TypeError) as exc:
                result["status"] = "error"
                result["errors"] += 1
                result["detail"] = type(exc).__name__ + ": " + str(exc)
        if result["errors"]:
            streak = min(previous.get("error_streak", 0) + 1, 8)
            result["error_streak"] = streak
            result["retry_after_epoch"] = now + min(3600, 300 * 2 ** (streak - 1))
        result["free_after_bytes"] = free_bytes(root)
        result["observed_free_delta_bytes"] = result["free_after_bytes"] - before
        result["elapsed_seconds"] = round(time.monotonic() - started, 3)
        result["next_scheduler_seconds"] = 300
        if apply:
            atomic_json(state / "status.json", result)
            log_event(state, cfg, {"event": "cycle", **{k: v for k, v in result.items() if k != "cursor"}})
        return result
    finally:
        fcntl.flock(lock_fd, fcntl.LOCK_UN)
        os.close(lock_fd)


def enroll(config_path, root, cfg, args):
    """The orchestrator explicitly retires exact paths; this never deletes them."""
    state = state_directory(root)
    lock_fd = os.open(state / "cycle.lock", os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)
    try:
        fcntl.flock(lock_fd, fcntl.LOCK_EX)
        # Re-read under the same lock used by cleanup; do not lose another enrollment.
        root, cfg = read_config(config_path)
        for value in args.allow_root:
            rel = rel_path(value.rstrip("/"))
            if any(within(rel, p) or within(p, rel) for p in HARD_PROTECTED):
                raise Refusal("Cannot enroll protected root: " + rel)
            if rel not in cfg["allowed_roots"]:
                cfg["allowed_roots"].append(rel)
        paths = list(args.path)
        if args.paths_file:
            raw = read_json(Path(args.paths_file).absolute())
            if not isinstance(raw, list) or not all(isinstance(x, str) for x in raw):
                raise Refusal("--paths-file requires JSON array of exact relative file paths")
            paths.extend(raw)
        paths = list(dict.fromkeys(paths))
        if not paths or len(paths) > 10000:
            raise Refusal("Enroll between 1 and 10000 exact files per manifest")
        if not args.reason.strip():
            raise Refusal("Explicit retirement reason required")
        name = args.manifest_id
        if not name or any(c not in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_" for c in name):
            raise Refusal("manifest-id must use ASCII letters, digits, hyphen or underscore")
        manifest_relative = STATE_DIR + "/retirements/" + name + ".json"
        manifest_file = root / manifest_relative
        if manifest_file.exists():
            raise Refusal("Manifest IDs are immutable; use a new manifest-id")
        now = time.time()
        due = parse_time(args.retire_after) if args.retire_after else now + cfg["quiet_seconds"]
        protected = protections(root, cfg)
        entries = []
        for value in paths:
            rel = rel_path(value)
            if not any(within(rel, x) and rel != x for x in cfg["allowed_roots"]):
                raise Refusal("Path outside explicit allowed_roots: " + rel)
            if any(within(rel, x) for x in protected):
                raise Refusal("Cannot enroll protected/active/source path: " + rel)
            with parent_fd(root, rel) as (parent, leaf):
                fd = os.open(leaf, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK, dir_fd=parent)
                try:
                    info = os.fstat(fd)
                    if not stat.S_ISREG(info.st_mode) or info.st_nlink != 1:
                        raise Refusal("Enroll only single-link regular files: " + rel)
                finally:
                    os.close(fd)
            entries.append({"path": rel, "category": args.category, "reason": args.reason,
                            "retire_after": datetime.fromtimestamp(due, timezone.utc).isoformat(),
                            "quiet_seconds": cfg["quiet_seconds"], "fingerprint": fingerprint(info)})
        manifest = {"schema_version": 1, "manifest_id": name, "approved_by": "root",
                    "approved_at": utc_now(), "project_root": str(root), "entries": entries}
        no_symlinks(manifest_file.parent)
        manifest_file.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
        atomic_json(manifest_file, manifest)
        digest = hashlib.sha256(read_bytes(manifest_file)).hexdigest()
        if len(cfg.get("approved_manifests", [])) >= 128:
            raise Refusal("Registry full; explicitly retire completed manifest registrations first")
        cfg.setdefault("approved_manifests", []).append({"path": manifest_relative, "sha256": digest})
        atomic_json(Path(config_path).absolute(), cfg)
        log_event(state, cfg, {"event": "enrolled", "manifest": manifest_relative,
                               "sha256": digest, "registered_files": len(entries)})
        return {"status": "enrolled", "manifest": manifest_relative, "sha256": digest,
                "registered_files": len(entries), "deleted_files": 0,
                "retire_after": manifest["entries"][0]["retire_after"]}
    finally:
        fcntl.flock(lock_fd, fcntl.LOCK_UN)
        os.close(lock_fd)


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--config", required=True, help="Explicit JSON config path")
    p.add_argument("command", choices=("cycle", "dry-run", "doctor", "status", "enroll"))
    p.add_argument("--path", action="append", default=[], help="Exact relative file to retire (repeatable)")
    p.add_argument("--paths-file", help="JSON list of exact relative file paths")
    p.add_argument("--allow-root", action="append", default=[], help="Explicitly add this cleanup whitelist root")
    p.add_argument("--manifest-id", help="Unique immutable enrollment ID")
    p.add_argument("--reason", default="", help="Root's explicit retirement reason")
    p.add_argument("--category", choices=("retired_artifact", "expired_temp"), default="retired_artifact")
    p.add_argument("--retire-after", help="Timezone-qualified retirement time; default now + quiet_seconds")
    args = p.parse_args(argv)
    try:
        root, cfg = read_config(args.config)
        if args.command == "enroll":
            result = enroll(args.config, root, cfg, args)
        elif args.command == "status":
            file = root / STATE_DIR / "status.json"
            result = read_json(file) if file.exists() else {"status": "not_run"}
            result["free_now_bytes"] = free_bytes(root)
            result["pressure_now"] = pressure(result["free_now_bytes"], cfg)
        elif args.command == "doctor":
            protected = protections(root, cfg)
            manifests = cfg.get("approved_manifests", [])
            totals = sum(len(load_manifest(root, item)) for item in manifests)
            with native_idle_guard(root):
                pass
            result = {"status": "configuration_ready", "enabled": bool(cfg.get("enabled")),
                      "approved_manifests": len(manifests), "registered_files": totals,
                      "protected_path_count": len(protected), "project_root": str(root)}
        else:
            result = cycle(root, cfg, args.command == "cycle")
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 1 if result.get("status") in ("error", "refused") else 0
    except (OSError, Refusal, ValueError, KeyError, TypeError) as exc:
        print(json.dumps({"status": "refused", "detail": type(exc).__name__ + ": " + str(exc)}, ensure_ascii=False))
        return 1


if __name__ == "__main__":
    sys.exit(main())
