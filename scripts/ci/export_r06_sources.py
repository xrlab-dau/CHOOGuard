#!/usr/bin/env python3
"""Export eight public R-06/M1-05 sources from an immutable Git commit.

No network or credential access. This exports source bytes, not acceptance.
The output parent must be trusted and exclusively owned by the CI job.
On an I/O failure preserve any partial output and choose a new output path.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import zipfile

FILES = (
    "docs/team/M0-03/execution-profile.json",
    "docs/team/M1-05/record-schema.json",
    "docs/team/R-06/research-control.json",
    "scripts/team/M1-05/record_pipeline.py",
    "scripts/team/M1-05/test_record_schema.py",
    "scripts/team/M1-05/test_redaction.py",
    "scripts/team/R-06/egress_controls.py",
    "scripts/team/R-06/test_egress_controls.py",
)
MAX_FILE_BYTES = 2 * 1024 * 1024


class ExportError(ValueError):
    """Fixed codes only; do not reflect local paths or input content."""


def git(repo: Path, *args: str) -> bytes:
    try:
        result = subprocess.run(["git", "-C", str(repo), *args], check=True,
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                timeout=20)
    except (OSError, subprocess.SubprocessError):
        raise ExportError("git_read_failed") from None
    return result.stdout


def export_sources(repo: Path, ref: str, output: Path) -> dict:
    if not re.fullmatch(r"[0-9a-f]{40}", ref):
        raise ExportError("invalid_commit_ref")
    if git(repo, "cat-file", "-t", ref).strip() != b"commit":
        raise ExportError("ref_is_not_commit")
    sources = {}
    metadata = {}
    for name in FILES:
        entry = git(repo, "ls-tree", "-z", ref, "--", name)
        expected_suffix = b"\t" + name.encode("utf-8") + b"\0"
        if not entry.endswith(expected_suffix) or entry.count(b"\0") != 1:
            raise ExportError("source_missing_or_not_regular")
        header = entry[:-len(expected_suffix)].split()
        if len(header) != 3 or header[0] not in (b"100644", b"100755") or header[1] != b"blob":
            raise ExportError("source_missing_or_not_regular")
        blob = header[2].decode("ascii")
        size = int(git(repo, "cat-file", "-s", blob))
        if size > MAX_FILE_BYTES:
            raise ExportError("source_too_large")
        data = git(repo, "cat-file", "blob", blob)
        if len(data) != size or hashlib.sha1(b"blob " + str(size).encode() + b"\0" + data).hexdigest() != blob:
            raise ExportError("source_integrity_mismatch")
        try:
            data.decode("utf-8")
        except UnicodeError:
            raise ExportError("source_not_utf8") from None
        sources[name] = data
        metadata[name] = {"gitBlob": blob, "sha256": hashlib.sha256(data).hexdigest(), "bytes": size}
    try:
        # Exclusive creation; never reuse a directory, including an empty one.
        output.mkdir(mode=0o700)
        archive = output / "sources.zip"
        with zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED) as z:
            for name, data in sources.items():
                info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
                info.create_system = 3
                info.external_attr = 0o100644 << 16
                info.compress_type = zipfile.ZIP_DEFLATED
                z.writestr(info, data)
        manifest = {"schemaVersion": 1, "sourceCommit": ref,
                    "claim": "source_bytes_only_not_test_or_acceptance",
                    "files": metadata, "archiveSha256": hashlib.sha256(archive.read_bytes()).hexdigest()}
        with (output / "manifest.json").open("x", encoding="utf-8", newline="\n") as stream:
            stream.write(json.dumps(manifest, sort_keys=True, indent=2) + "\n")
    except OSError:
        raise ExportError("output_unavailable") from None
    return manifest


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path("."))
    parser.add_argument("--ref", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        manifest = export_sources(args.repo, args.ref, args.output)
    except ExportError as error:
        print(json.dumps({"state": "refused", "code": str(error)}))
        return 2
    print(json.dumps({"state": "source_exported", "sourceCommit": manifest["sourceCommit"],
                      "files": len(manifest["files"]), "archiveSha256": manifest["archiveSha256"]}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
