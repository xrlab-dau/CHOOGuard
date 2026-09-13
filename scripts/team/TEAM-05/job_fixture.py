#!/usr/bin/env python3
"""Resumable synthetic production job over four separated stores.

The fixture works on small synthetic files only. It never launches Unity, opens a
network socket, or touches a real production asset. Stores are kept apart and are
addressed through separate accessors so one store's bytes cannot be mistaken for
another's:

  original/   immutable synthetic source bytes; the job binds to their digest
  cache/      content-addressed derived expansion, reused only while bytes match
  work/       per-job checkpoint, lease lock and pilot observation (ephemeral)
  candidate/  public candidate output; write-new only, never overwritten

Contract points reproduced here: a stable jobID per (input set, output target),
resume from an interrupted checkpoint with the same input, refusal of a duplicate
concurrent execution, refusal of an input whose hash changed after the job was
opened, refusal to overwrite an existing output, refusal of an undeclared budget,
refusal of a path that leaves its store, and a pilot observation of output size
and remaining free space that is recorded without deleting anything.

Only the standard library is used.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
from pathlib import Path, PurePosixPath

STORES = ("original", "cache", "work", "candidate")
CHECKPOINT_NAME = "checkpoint.json"
LOCK_NAME = "lock.json"
OBSERVATION_NAME = "observation.json"
SCHEMA_VERSION = 1


class JobError(Exception):
    """Typed rejection; ``code`` is the stable reason handed to successors."""

    def __init__(self, code, detail=""):
        super().__init__(code if not detail else f"{code}: {detail}")
        self.code = code
        self.detail = detail


# --------------------------------------------------------------------------
# portable paths and digests
# --------------------------------------------------------------------------

def sha256_bytes(data):
    return hashlib.sha256(data).hexdigest()


def sha256_file(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def portable_relative(name):
    """Return a relative POSIX path or reject it as leaving its store."""
    if not isinstance(name, str) or not name:
        raise JobError("PATH_ESCAPE", f"empty or non-string path: {name!r}")
    relative = PurePosixPath(name)
    # ``.`` and ``./`` normalize to no parts at all; they name no file inside a
    # store, so they are a path error rather than an index error.
    if not relative.parts:
        raise JobError("PATH_ESCAPE", name)
    if (relative.is_absolute() or ".." in relative.parts or "\\" in name
            or ":" in name or relative.parts[0] == ".git"):
        raise JobError("PATH_ESCAPE", name)
    return relative


def contained(root, name):
    """Resolve ``name`` inside ``root`` and refuse anything that escapes it."""
    relative = portable_relative(name)
    root = Path(root)
    path = root.joinpath(*relative.parts)
    if not path.resolve().is_relative_to(root.resolve()):
        raise JobError("PATH_ESCAPE", name)
    return path


def read_manifest(root, names):
    """Digest of a named set of original files, bound as one aggregate value."""
    files = {}
    for name in sorted(set(names)):
        path = contained(root, name)
        if not path.is_file():
            raise JobError("INPUT_MISSING", name)
        files[name] = {"sha256": sha256_file(path), "bytes": path.stat().st_size}
    encoded = json.dumps(files, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return {"files": files, "digest": sha256_bytes(encoded)}


def job_id(names, output_rel):
    """Stable identity of a job: the declared input set plus the output target.

    Input bytes are deliberately *not* part of the identity so that a changed
    original keeps the same job and is detected as an input-hash mismatch rather
    than silently becoming a different job.
    """
    raw = json.dumps({"names": sorted(set(names)), "output": output_rel},
                     sort_keys=True, separators=(",", ":")).encode("utf-8")
    return sha256_bytes(raw)[:24]


def unit_pieces(name, raw):
    """One deterministic output piece per source line; the unit of resumption."""
    return ["{}\t{}\t{}\n".format(name, index, line).encode("utf-8")
            for index, line in enumerate(raw.decode("utf-8").splitlines())]


def expand(name, raw):
    return b"".join(unit_pieces(name, raw))


def _validated_budget(value):
    if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
        raise JobError("BUDGET_UNDECLARED", repr(value))
    return value


def _read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def _write_new(path, record):
    """Write a receipt that must not already exist."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8") as stream:
        json.dump(record, stream, ensure_ascii=False, indent=2, sort_keys=True)
        stream.write("\n")


def _atomic(path, record):
    """Replace a checkpoint/lease so readers never see a half-written record."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    with temporary.open("w", encoding="utf-8") as stream:
        json.dump(record, stream, ensure_ascii=False, indent=2, sort_keys=True)
        stream.write("\n")
    os.replace(temporary, path)


def _release(lock_path):
    try:
        Path(lock_path).unlink()
    except FileNotFoundError:
        pass


# --------------------------------------------------------------------------
# store
# --------------------------------------------------------------------------

class JobStore:
    """Four separated stores under one job root."""

    def __init__(self, root, free_space_probe=None):
        self.root = Path(root).resolve()
        for store in STORES:
            (self.root / store).mkdir(parents=True, exist_ok=True)
        self._free_space_probe = free_space_probe or shutil.disk_usage

    # --- store accessors: one method per store, never interchangeable ------

    def original(self, name):
        return contained(self.root / "original", name)

    def cache(self, name):
        return contained(self.root / "cache", name)

    def candidate(self, name):
        return contained(self.root / "candidate", name)

    def work(self, name):
        return contained(self.root / "work", name)

    def write_original(self, name, data):
        """Place synthetic source bytes; the job binds to their digest on open."""
        path = self.original(name)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return path

    def manifest(self, names):
        return read_manifest(self.root / "original", names)

    def free_bytes(self):
        return self._free_space_probe(self.root).free

    def survey(self):
        """Pilot observation: per-store bytes and remaining free space."""
        stores = {}
        for store in STORES:
            stores[store] = sum(p.stat().st_size
                                for p in (self.root / store).rglob("*") if p.is_file())
        return {"stores": stores, "freeBytes": self.free_bytes()}

    # --- job paths ---------------------------------------------------------

    def _job_dir(self, jid):
        path = self.work(jid)
        path.mkdir(parents=True, exist_ok=True)
        return path

    def _checkpoint_path(self, jid):
        return self._job_dir(jid) / CHECKPOINT_NAME

    def _lock_path(self, jid):
        return self._job_dir(jid) / LOCK_NAME

    # --- lifecycle ---------------------------------------------------------

    def begin(self, names, output_rel, budget_bytes, holder):
        """Open or resume a job and take its lease, or reject.

        Raises JOB_ACTIVE if a lease is held, JOB_DONE if the job already
        finished, CONTRACT_MISMATCH if a resume changes the budget, OUTPUT_EXISTS
        if the target output already exists, BUDGET_UNDECLARED for a missing
        budget, PATH_ESCAPE for a path outside a store, INPUT_MISSING for an
        absent original.
        """
        budget = _validated_budget(budget_bytes)
        portable_relative(output_rel)
        names = sorted(set(names))
        manifest = self.manifest(names)
        jid = job_id(names, output_rel)
        checkpoint_path = self._checkpoint_path(jid)
        lock_path = self._lock_path(jid)

        if checkpoint_path.is_file():
            checkpoint = _read_json(checkpoint_path)
            if checkpoint.get("state") == "done":
                raise JobError("JOB_DONE", jid)
            if checkpoint.get("budgetBytes") != budget:
                raise JobError("CONTRACT_MISMATCH", jid)
            # A held lease means an execution is in flight, by anyone.
            if lock_path.is_file():
                raise JobError("JOB_ACTIVE", jid)
            if self.candidate(output_rel).exists():
                raise JobError("OUTPUT_EXISTS", output_rel)
            checkpoint["state"] = "running"
            checkpoint["holder"] = holder
            _atomic(checkpoint_path, checkpoint)
            _atomic(lock_path, {"jobID": jid, "holder": holder})
            return checkpoint

        if self.candidate(output_rel).exists():
            raise JobError("OUTPUT_EXISTS", output_rel)

        total_units = sum(len(unit_pieces(name, self.original(name).read_bytes()))
                          for name in names)
        checkpoint = {
            "schemaVersion": SCHEMA_VERSION,
            "jobID": jid,
            "inputDigest": manifest["digest"],
            "files": manifest["files"],
            "names": names,
            "outputRel": output_rel,
            "budgetBytes": budget,
            "totalUnits": total_units,
            "completedUnits": 0,
            "producedBytes": 0,
            "state": "running",
            "holder": holder,
            "freeBytesBefore": self.free_bytes(),
        }
        _write_new(checkpoint_path, checkpoint)
        _atomic(lock_path, {"jobID": jid, "holder": holder})
        return checkpoint

    def run(self, job, limit_units=None, holder=None):
        """Advance a job; stop after ``limit_units`` to emulate an interruption.

        The checkpoint is persisted after every unit, so a later ``begin`` with
        the same input resumes at the same completed count. Nothing is deleted.
        """
        jid = job["jobID"]
        holder = job.get("holder") if holder is None else holder
        checkpoint_path = self._checkpoint_path(jid)
        if not checkpoint_path.is_file():
            raise JobError("JOB_UNKNOWN", jid)
        checkpoint = _read_json(checkpoint_path)
        if checkpoint.get("state") == "done":
            raise JobError("JOB_DONE", jid)

        lock_path = self._lock_path(jid)
        if lock_path.is_file():
            if _read_json(lock_path).get("holder") != holder:
                raise JobError("JOB_ACTIVE", jid)
        else:
            _atomic(lock_path, {"jobID": jid, "holder": holder})

        # Bind to the exact bytes recorded when the job was opened.
        current = self.manifest(checkpoint["names"])
        if current["digest"] != checkpoint["inputDigest"]:
            _release(lock_path)
            raise JobError("INPUT_HASH_MISMATCH", jid)

        if self.candidate(checkpoint["outputRel"]).exists():
            _release(lock_path)
            raise JobError("OUTPUT_EXISTS", checkpoint["outputRel"])

        pieces = []
        for name in checkpoint["names"]:
            pieces.extend(unit_pieces(name, self.original(name).read_bytes()))

        completed = checkpoint["completedUnits"]
        produced = checkpoint["producedBytes"]
        budget = checkpoint["budgetBytes"]
        done = 0
        for piece in pieces[completed:]:
            if limit_units is not None and done >= limit_units:
                break
            if produced + len(piece) > budget:
                checkpoint.update(state="interrupted", completedUnits=completed,
                                  producedBytes=produced)
                _atomic(checkpoint_path, checkpoint)
                _release(lock_path)
                raise JobError("BUDGET_EXCEEDED", f"{jid} budget {budget}")
            produced += len(piece)
            completed += 1
            done += 1
            checkpoint.update(completedUnits=completed, producedBytes=produced)
            _atomic(checkpoint_path, checkpoint)

        finished = completed >= len(pieces)
        try:
            if finished:
                self._publish(checkpoint)
        except BaseException:
            # A failed publish must not strand the lease: record the stop and
            # release it so the same input can be resumed instead of being
            # reported as an active duplicate execution.
            checkpoint["state"] = "interrupted"
            _atomic(checkpoint_path, checkpoint)
            _release(lock_path)
            raise
        checkpoint["state"] = "done" if finished else "interrupted"
        _atomic(checkpoint_path, checkpoint)
        _release(lock_path)
        return checkpoint

    def _publish(self, checkpoint):
        """Assemble from cache and write the public candidate exactly once."""
        output = self._assemble_cached(checkpoint)
        if len(output) != checkpoint["producedBytes"]:
            raise JobError("OUTPUT_LENGTH_MISMATCH", checkpoint["jobID"])
        path = self.candidate(checkpoint["outputRel"])
        path.parent.mkdir(parents=True, exist_ok=True)
        try:
            with path.open("xb") as stream:
                stream.write(output)
        except FileExistsError as error:
            raise JobError("OUTPUT_EXISTS", checkpoint["outputRel"]) from error

        observation = {
            "jobID": checkpoint["jobID"],
            "outputBytes": len(output),
            "outputDigest": sha256_bytes(output),
            "freeBytesBefore": checkpoint.get("freeBytesBefore"),
            "freeBytesAfter": self.free_bytes(),
            "survey": self.survey(),
            "retention": ("originals, cache and work are retained; "
                          "nothing is deleted automatically"),
        }
        _write_new(self._job_dir(checkpoint["jobID"]) / OBSERVATION_NAME, observation)
        checkpoint["observation"] = observation

    def _assemble_cached(self, checkpoint):
        """Reuse a cache entry only while its bytes match; never overwrite one."""
        chunks = []
        for name in checkpoint["names"]:
            raw = self.original(name).read_bytes()
            expected = expand(name, raw)
            path = self.cache(checkpoint["files"][name]["sha256"] + ".part")
            if path.is_file():
                if path.read_bytes() != expected:
                    raise JobError("CACHE_HASH_MISMATCH", name)
            else:
                path.parent.mkdir(parents=True, exist_ok=True)
                try:
                    with path.open("xb") as stream:
                        stream.write(expected)
                except FileExistsError:
                    pass
            chunks.append(expected)
        return b"".join(chunks)


def demo(root, interrupt_after=1):
    """Run one small synthetic job and return its observable digests."""
    store = JobStore(root)
    store.write_original("source/sample.txt", b"alpha\nbravo\ncharlie\n")
    manifest = store.manifest(["source/sample.txt"])
    job = store.begin(["source/sample.txt"], "pack.txt", 4096, "demo")
    stopped = store.run(job, limit_units=interrupt_after)
    resumed = store.begin(["source/sample.txt"], "pack.txt", 4096, "demo")
    final = store.run(resumed)
    output = store.candidate("pack.txt").read_bytes()
    return {
        "inputSha256": manifest["files"]["source/sample.txt"]["sha256"],
        "inputDigest": manifest["digest"],
        "jobID": job["jobID"],
        "interruptedState": stopped["state"],
        "interruptedCompletedUnits": stopped["completedUnits"],
        "resumedCompletedUnits": resumed["completedUnits"],
        "finalState": final["state"],
        "totalUnits": final["totalUnits"],
        "outputBytes": len(output),
        "outputSha256": sha256_bytes(output),
        "observation": final["observation"],
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    survey = sub.add_parser("survey", help="report per-store bytes and free space")
    survey.add_argument("--root", type=Path, required=True)
    demo_parser = sub.add_parser("demo", help="run one small synthetic job and print digests")
    demo_parser.add_argument("--root", type=Path, required=True)
    demo_parser.add_argument("--interrupt-after", type=int, default=1)
    args = parser.parse_args(argv)
    if args.command == "survey":
        record = JobStore(args.root).survey()
    else:
        record = demo(args.root, args.interrupt_after)
    print(json.dumps(record, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
