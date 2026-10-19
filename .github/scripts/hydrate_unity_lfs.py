"""Hydrate strict LFS pointers in the current CI Unity checkout before import."""
from __future__ import annotations

import hashlib
import os
import stat
import subprocess
import sys
import tempfile
from pathlib import Path, PurePosixPath

from lfs_text_assets import LfsTextAssetError, POINTER, resolve_lfs_text_assets


PROJECT_ROOTS = ("Assets/", "Packages/", "ProjectSettings/")
MAX_POINTER_BYTES = 1024


def project_pointers(root: Path) -> dict[str, bytes]:
    listed = subprocess.run(
        ["git", "ls-files", "-z", "--", *PROJECT_ROOTS], cwd=root,
        capture_output=True, timeout=30, check=False,
    )
    if listed.returncode:
        raise LfsTextAssetError("Cannot list tracked Unity project files")
    pointers = {}
    for raw in listed.stdout.split(b"\0"):
        if not raw:
            continue
        relative = os.fsdecode(raw)
        # Git supplies checkout-relative paths. Keep the write boundary explicit,
        # including for symlinks and any unexpected traversal in a listing.
        if not relative.startswith(PROJECT_ROOTS) or ".." in PurePosixPath(relative).parts:
            continue
        path = root / relative
        if not path.resolve().is_relative_to(root.resolve()):
            continue
        try:
            info = path.lstat()
        except FileNotFoundError:
            continue  # Sparse checkout may leave tracked files absent.
        if not stat.S_ISREG(info.st_mode) or info.st_size > MAX_POINTER_BYTES:
            continue
        data = path.read_bytes()
        if POINTER.fullmatch(data):
            pointers[relative] = data
    return pointers


def media_directory() -> Path:
    result = subprocess.run(["git", "lfs", "env"], capture_output=True, timeout=30, check=False)
    media = next((line.removeprefix("LocalMediaDir=")
                  for line in result.stdout.decode(errors="replace").splitlines()
                  if line.startswith("LocalMediaDir=")), "")
    if result.returncode or not media:
        raise LfsTextAssetError("Git LFS object storage is unavailable")
    return Path(media)


def replace_pointer(path: Path, pointer: bytes, storage: Path) -> int:
    match = POINTER.fullmatch(pointer)
    assert match is not None
    oid, expected_size = match[1].decode("ascii"), int(match[2])
    source = storage / oid[:2] / oid[2:4] / oid
    temporary = None
    try:
        mode = path.lstat().st_mode
        if (not stat.S_ISREG(mode) or path.stat().st_size != len(pointer)
                or path.read_bytes() != pointer):
            raise LfsTextAssetError(f"Working-tree pointer changed before hydration: {path}")
        digest, size = hashlib.sha256(), 0
        with source.open("rb") as stream, tempfile.NamedTemporaryFile(
                dir=path.parent, prefix=".lfs-hydrate-", delete=False) as output:
            temporary = Path(output.name)
            while chunk := stream.read(1024 * 1024):
                output.write(chunk)
                digest.update(chunk)
                size += len(chunk)
        # Recheck the bytes actually copied, so a cache change after the shared
        # resolver's verification cannot replace a pointer with corrupt data.
        if digest.hexdigest() != oid or size != expected_size:
            raise LfsTextAssetError(f"LFS cached object changed during hydration: {path}")
        os.chmod(temporary, stat.S_IMODE(mode))
        os.replace(temporary, path)
        return size
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def hydrate() -> tuple[int, int]:
    root = Path.cwd()
    pointers = project_pointers(root)
    if not pointers:
        return 0, 0
    # This downloads exact missing objects using process-scoped CI credentials,
    # verifies every SHA/size, and never scans or fetches all objects at a ref.
    resolve_lfs_text_assets(pointers, "HEAD")
    storage = media_directory()
    total = sum(replace_pointer(root / path, pointer, storage)
                for path, pointer in pointers.items())
    return len(pointers), total


def main() -> int:
    try:
        count, size = hydrate()
    except LfsTextAssetError as error:
        print(f"Unity LFS hydration failed: {error}", file=sys.stderr)
        return 1
    except (OSError, subprocess.TimeoutExpired):
        # Never expose raw Git output, process environments or credential data.
        print("Unity LFS hydration failed: project files or Git LFS storage unavailable", file=sys.stderr)
        return 1
    print(f"Unity LFS: hydrated {count} tracked project file(s), {size} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
