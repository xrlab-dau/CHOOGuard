"""Resolve Force Text LFS assets without changing the working tree.

A pointer is not evidence of YAML serialization. Check the actual object, its
declared size and SHA-256 before handing its prefix to the policy checker.
"""
from __future__ import annotations

import hashlib
import posixpath
import re
import subprocess
from pathlib import Path


class LfsTextAssetError(RuntimeError):
    pass


POINTER = re.compile(
    rb"version https://git-lfs.github.com/spec/v1\n"
    rb"oid sha256:([0-9a-f]{64})\nsize (0|[1-9][0-9]*)\n?"
)


def resolve_lfs_text_assets(heads: dict[str, bytes], rev: str) -> dict[str, bytes]:
    pointers = {}
    for path, data in heads.items():
        match = POINTER.fullmatch(data)
        if match:
            pointers[path] = (match[1].decode("ascii"), int(match[2]))
    if not pointers:
        return heads

    try:
        env = subprocess.run(["git", "lfs", "env"], capture_output=True, timeout=30, check=False)
    except (OSError, subprocess.TimeoutExpired) as exc:
        raise LfsTextAssetError("Git LFS object storage is unavailable; serialization is unverified") from exc
    media = next((line.removeprefix("LocalMediaDir=")
                  for line in env.stdout.decode(errors="replace").splitlines()
                  if line.startswith("LocalMediaDir=")), "")
    if env.returncode or not media:
        raise LfsTextAssetError("Git LFS object storage is unavailable; Force Text serialization is unverified")
    storage = Path(media)

    def location(oid: str) -> Path:
        return storage / oid[:2] / oid[2:4] / oid

    if any(not location(oid).is_file() for oid, _ in pointers.values()):
        # Extension patterns avoid interpreting commas/brackets in asset names.
        # Download only affected Force Text categories, never all art assets.
        include = ",".join(sorted({"*" + posixpath.splitext(path)[1] for path in pointers}))
        try:
            fetched = subprocess.run(
                ["git", "-c", "lfs.fetchrecentalways=false", "lfs", "fetch",
                 "--include=" + include, "--exclude=", "origin", rev],
                capture_output=True, timeout=180, check=False,
            )
        except (OSError, subprocess.TimeoutExpired) as exc:
            raise LfsTextAssetError("Fetching Force Text LFS objects failed; serialization is unverified") from exc
        if fetched.returncode:
            raise LfsTextAssetError("Fetching Force Text LFS objects failed; serialization is unverified")

    resolved = dict(heads)
    cached = {}
    for path, (oid, expected_size) in pointers.items():
        if oid not in cached:
            digest, size, prefix = hashlib.sha256(), 0, b""
            try:
                with location(oid).open("rb") as stream:
                    while chunk := stream.read(1024 * 1024):
                        if not prefix:
                            prefix = chunk[:8]
                        size += len(chunk)
                        digest.update(chunk)
            except OSError as exc:
                raise LfsTextAssetError(f"LFS object missing or unreadable for {path}") from exc
            if digest.hexdigest() != oid:
                raise LfsTextAssetError(f"LFS SHA-256 mismatch for {path}; serialization is unverified")
            cached[oid] = size, prefix
        size, prefix = cached[oid]
        if size != expected_size:
            raise LfsTextAssetError(f"LFS size mismatch for {path}; serialization is unverified")
        resolved[path] = prefix
    return resolved
