#!/usr/bin/env python3
"""Portable native source snapshots and payload verification; no network or Unity launch."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def contained_file(root, name):
    relative = PurePosixPath(name)
    if (not name or relative.is_absolute() or ".." in relative.parts
            or "\\" in name or ":" in name or relative.parts[0] == ".git"):
        raise ValueError(f"Invalid portable path: {name}")
    path = root.joinpath(*relative.parts)
    if not path.resolve().is_relative_to(root.resolve()):
        raise ValueError(f"Path leaves snapshot: {name}")
    current = root
    for part in relative.parts:
        current = current / part
        if current.is_symlink():
            raise ValueError(f"Linked input is not a materialized file: {name}")
    return path


def manifest(root, names, kind, **metadata):
    files = {}
    for name in sorted(set(names)):
        path = contained_file(root, name)
        if not path.is_file():
            raise ValueError(f"Missing regular file: {name}")
        files[name] = {"sha256": sha256(path), "bytes": path.stat().st_size}
    encoded = json.dumps(files, sort_keys=True, separators=(",", ":")).encode()
    return {"schemaVersion": 1, "kind": kind, "filesDigest": hashlib.sha256(encoded).hexdigest(),
            "files": files, **metadata}


def verify(root, record):
    if record.get("schemaVersion") != 1 or not isinstance(record.get("files"), dict):
        raise ValueError("Unsupported native manifest")
    actual = manifest(root, record["files"], record.get("kind", "unknown"))
    if actual["files"] != record["files"] or actual["filesDigest"] != record.get("filesDigest"):
        raise ValueError("Native manifest bytes or digest do not match")
    return len(actual["files"])


def write_new(path, record):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8") as stream:
        json.dump(record, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def snapshot(root, output, includes):
    tracked = subprocess.check_output(["git", "ls-files", "-z"], cwd=root).decode().split("\0")
    tracked = [name for name in tracked if name]
    deleted = [name for name in tracked if not contained_file(root, name).exists()]
    names = [name for name in tracked if name not in deleted] + includes
    record = manifest(root, names, "native-current-worktree",
                      baseCommit=subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(),
                      omittedDeleted=sorted(deleted), explicitAdditionalFiles=sorted(set(includes)),
                      acceptance="local relocated snapshot; not remote publication or another developer's execution")
    if output.exists():
        raise ValueError("Snapshot output already exists")
    output.mkdir(parents=True)
    for name in record["files"]:
        target = output / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(contained_file(root, name), target)
    write_new(output / "native-source-manifest.json", record)
    verify(output, record)
    return record


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    pack = sub.add_parser("snapshot")
    pack.add_argument("--root", type=Path, default=Path.cwd())
    pack.add_argument("--output", type=Path, required=True)
    pack.add_argument("--include", action="append", default=[])
    payload = sub.add_parser("payload")
    payload.add_argument("--root", type=Path, required=True)
    payload.add_argument("--output", type=Path, required=True)
    check = sub.add_parser("verify")
    check.add_argument("--root", type=Path, required=True)
    check.add_argument("--manifest", type=Path, required=True)
    args = parser.parse_args()
    try:
        if args.command == "snapshot":
            record = snapshot(args.root.resolve(), args.output.resolve(), args.include)
        elif args.command == "payload":
            root = args.root.resolve()
            if args.output.resolve().is_relative_to(root):
                raise ValueError("Write payload manifest outside the payload to avoid a self hash")
            record = manifest(root, [p.relative_to(root).as_posix() for p in root.rglob("*") if p.is_file()], "native-build-payload")
            write_new(args.output, record)
        else:
            record = json.loads(args.manifest.read_text(encoding="utf-8"))
            verify(args.root.resolve(), record)
        print(json.dumps({"status": "verified", "kind": record["kind"], "files": len(record["files"]), "filesDigest": record["filesDigest"]}))
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        parser.exit(1, f"Native manifest failed: {error}\n")


if __name__ == "__main__":
    main()
