"""Resolve Force Text LFS assets without changing the working tree.

A pointer is not evidence of YAML serialization. Check the actual object, its
declared size and SHA-256 before handing its prefix to the policy checker.
"""
from __future__ import annotations

import base64
import hashlib
import os
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

    def download(path: str) -> None:
        # Smudge the exact commit's pointer rather than enumerating every LFS
        # path at the ref. Output goes nowhere; Git LFS retains its cached object.
        download_env = os.environ.copy()
        download_env.pop("GIT_LFS_SKIP_SMUDGE", None)
        download_env.pop("GIT_LFS_SKIP_DOWNLOAD_ERRORS", None)
        token = download_env.get("GITHUB_TOKEN")
        if token:
            # checkout deliberately removes persisted credentials. Expose the
            # existing read-only CI token only to this GitHub HTTPS operation,
            # without putting it in argv or writing repository configuration.
            try:
                count = int(download_env.get("GIT_CONFIG_COUNT", "0"))
            except ValueError as exc:
                raise LfsTextAssetError("Invalid process Git configuration; LFS serialization is unverified") from exc
            if count < 0:
                raise LfsTextAssetError("Invalid process Git configuration; LFS serialization is unverified")
            encoded = base64.b64encode(("x-access-token:" + token).encode()).decode("ascii")
            download_env[f"GIT_CONFIG_KEY_{count}"] = "http.https://github.com/.extraheader"
            download_env[f"GIT_CONFIG_VALUE_{count}"] = "AUTHORIZATION: basic " + encoded
            download_env["GIT_CONFIG_COUNT"] = str(count + 1)
        try:
            smudged = subprocess.run(
                ["git", "-c", "lfs.fetchinclude=", "-c", "lfs.fetchexclude=",
                 "-c", "lfs.skipdownloaderrors=false", "lfs", "smudge", "--", path],
                input=heads[path], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                env=download_env, timeout=180, check=False,
            )
        except subprocess.TimeoutExpired as exc:
            raise LfsTextAssetError(f"LFS download timed out after 180s for {path}; serialization is unverified") from exc
        except OSError as exc:
            raise LfsTextAssetError(f"LFS download command unavailable for {path}; serialization is unverified") from exc
        if smudged.returncode:
            raise LfsTextAssetError(f"LFS download failed for {path} (exit {smudged.returncode}); serialization is unverified")

    resolved = dict(heads)
    cached = {}
    for path, (oid, expected_size) in pointers.items():
        if oid not in cached:
            if not location(oid).is_file():
                download(path)
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
