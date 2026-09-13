#!/usr/bin/env python3
"""TEAM-05 synthetic job fixture: separated roots, resumable steps, refusal contract.

Standard library only. No network, no Unity, no deletion. The fixture never removes a
file or directory: refused runs preserve whatever an earlier run already produced, and
cleanup stays a human decision (work order nonGoal "전체환경정리/자동삭제").

Roots
  source     read-only inputs; the job never writes here
  cache      per-job staging for bytes that are still being assembled
  work       per-job receipt and lock observation
  candidate  publishable outputs; an existing output is never replaced

Exit codes: 0 completed or interrupted as asked, 1 refused by contract, 2 bad invocation.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys
from dataclasses import dataclass
from pathlib import Path, PurePosixPath

RECEIPT_NAME = "receipt.json"
LOCK_NAME = "job.lock"
SCHEMA_VERSION = 1


class JobError(ValueError):
    """Short uppercase code only; no absolute paths, credentials or file contents."""


@dataclass(frozen=True)
class Layout:
    source: Path
    cache: Path
    work: Path
    candidate: Path

    def create(self) -> None:
        for root in (self.source, self.cache, self.work, self.candidate):
            root.mkdir(parents=True, exist_ok=True)

    def job_dir(self, root: Path, job_id: str) -> Path:
        return contained_path(root, job_id)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def contained_path(root: Path, name: str) -> Path:
    """Reject absolute, escaping, backslash, drive-letter, .git and symlinked names."""
    if not isinstance(name, str) or not name:
        raise JobError("INVALID_PORTABLE_PATH")
    relative = PurePosixPath(name)
    if (relative.is_absolute() or ".." in relative.parts or "\\" in name or ":" in name
            or not relative.parts or relative.parts[0] in (".git", ".")):
        raise JobError("INVALID_PORTABLE_PATH")
    path = root.joinpath(*relative.parts)
    try:
        contained = path.resolve().is_relative_to(root.resolve())
    except OSError as error:  # pragma: no cover - platform dependent resolution failure
        raise JobError("INVALID_PORTABLE_PATH") from error
    if not contained:
        raise JobError("INVALID_PORTABLE_PATH")
    current = root
    for part in relative.parts:
        current = current / part
        if current.is_symlink():
            raise JobError("LINKED_INPUT")
    return path


def plan_inputs(layout: Layout, names) -> dict:
    """Hash every declared input without touching the source bytes."""
    planned = {}
    for name in sorted(set(names)):
        path = contained_path(layout.source, name)
        if not path.is_file():
            raise JobError("MISSING_INPUT")
        planned[name] = {"sha256": sha256_file(path), "bytes": path.stat().st_size}
    if not planned:
        raise JobError("NO_INPUT")
    return planned


def inputs_digest(planned: dict) -> str:
    encoded = json.dumps(planned, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def read_receipt(path: Path) -> dict | None:
    if not path.is_file():
        return None
    try:
        record = json.loads(path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError) as error:
        raise JobError("MALFORMED_RECEIPT") from error
    if not isinstance(record, dict) or record.get("schemaVersion") != SCHEMA_VERSION:
        raise JobError("MALFORMED_RECEIPT")
    return record


def store_receipt(path: Path, record: dict) -> None:
    """Write through a temporary sibling; os.replace keeps the previous receipt intact
    until the new one is complete and never deletes an unrelated path."""
    path.parent.mkdir(parents=True, exist_ok=True)
    staged = path.with_name(path.name + ".staged")
    with staged.open("w", encoding="utf-8", newline="\n") as stream:
        json.dump(record, stream, ensure_ascii=False, indent=2, sort_keys=True)
        stream.write("\n")
    os.replace(staged, path)


def write_output(source_path: Path, target: Path) -> None:
    """Copy one planned input into the candidate root, refusing an existing output."""
    if target.exists() or target.is_symlink():
        raise JobError("OUTPUT_EXISTS")
    target.parent.mkdir(parents=True, exist_ok=True)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0)
    try:
        handle = os.open(target, flags, 0o600)
    except FileExistsError as error:
        raise JobError("OUTPUT_EXISTS") from error
    with os.fdopen(handle, "wb") as stream:
        with source_path.open("rb") as reader:
            for chunk in iter(lambda: reader.read(1024 * 1024), b""):
                stream.write(chunk)


def run_job(layout: Layout, job_id: str, names, budget_bytes=None, resume=False, stop_after=None) -> dict:
    work_dir = layout.job_dir(layout.work, job_id)
    receipt_path = work_dir / RECEIPT_NAME
    if (work_dir / LOCK_NAME).exists():
        raise JobError("JOB_LOCK_HELD")

    previous = read_receipt(receipt_path)
    if previous is not None and previous.get("state") == "completed":
        raise JobError("JOB_ALREADY_COMPLETED")
    if resume and previous is None:
        raise JobError("NO_RESUMABLE_RECEIPT")

    planned = plan_inputs(layout, names)
    digest = inputs_digest(planned)
    if previous is not None and previous.get("inputsDigest") != digest:
        raise JobError("INPUT_DIGEST_CHANGED")

    done = list(previous.get("completedInputs", [])) if previous else []
    remaining = [name for name in sorted(planned) if name not in done]
    outputs = dict(previous.get("outputs", {})) if previous else {}

    planned_bytes = sum(entry["bytes"] for name, entry in planned.items() if name in remaining)
    if budget_bytes is None:
        raise JobError("BUDGET_NOT_SET")
    if isinstance(budget_bytes, bool) or not isinstance(budget_bytes, int) or budget_bytes <= 0:
        raise JobError("INVALID_BUDGET")
    if planned_bytes > budget_bytes:
        raise JobError("BUDGET_EXCEEDED")

    free_before = shutil.disk_usage(layout.work).free
    budget = {"plannedBytes": planned_bytes, "budgetBytes": budget_bytes,
              "freeBytesBefore": free_before,
              "policy": "관측값만 기록한다. 자동 삭제나 정리는 하지 않는다."}

    candidate_dir = layout.job_dir(layout.candidate, job_id)
    cache_dir = layout.job_dir(layout.cache, job_id)
    cache_dir.mkdir(parents=True, exist_ok=True)

    processed = []
    limit = len(remaining) if stop_after is None else max(0, int(stop_after))
    for name in remaining[:limit]:
        source_path = contained_path(layout.source, name)
        target = contained_path(candidate_dir, name)
        write_output(source_path, target)
        outputs[name] = {"sha256": sha256_file(target), "bytes": target.stat().st_size}
        processed.append(name)
        done.append(name)

    state = "completed" if len(done) == len(planned) else "interrupted"
    record = {
        "schemaVersion": SCHEMA_VERSION,
        "jobId": job_id,
        "state": state,
        "inputsDigest": digest,
        "inputs": planned,
        "outputs": outputs,
        "completedInputs": sorted(done),
        "budget": budget,
        "roots": {"source": "source", "cache": "cache", "work": "work", "candidate": "candidate"},
        "limits": [
            "합성 파일 재현 계약이다. 실제 소재 처리·성능·기기별 용량 수용이 아니다.",
            "거부된 실행은 기존 산출물과 영수증을 보존한다.",
        ],
    }
    store_receipt(receipt_path, record)
    return {"jobId": job_id, "state": state, "processed": processed, "completedInputs": sorted(done),
            "inputsDigest": digest, "outputs": outputs, "budget": budget,
            "receipt": str(receipt_path.relative_to(layout.work))}


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="TEAM-05 synthetic job fixture")
    parser.add_argument("--source")
    parser.add_argument("--cache")
    parser.add_argument("--work")
    parser.add_argument("--candidate")
    parser.add_argument("--job-id", required=True)
    parser.add_argument("--input", nargs="+", default=[])
    parser.add_argument("--budget-bytes", type=int)
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--stop-after", type=int)
    try:
        args = parser.parse_args(argv)
    except SystemExit:
        return 2
    roots = (args.source, args.cache, args.work, args.candidate)
    if not all(roots) or not args.input:
        print("BAD_INVOCATION", file=sys.stderr)
        return 2
    layout = Layout(source=Path(args.source), cache=Path(args.cache),
                    work=Path(args.work), candidate=Path(args.candidate))
    try:
        result = run_job(layout, args.job_id, args.input, budget_bytes=args.budget_bytes,
                         resume=args.resume, stop_after=args.stop_after)
    except JobError as error:
        print(str(error), file=sys.stderr)
        return 1
    except OSError:
        print("IO_FAILED", file=sys.stderr)
        return 2
    print(json.dumps({k: result[k] for k in ("jobId", "state", "processed", "budget")},
                     ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    sys.exit(main())
