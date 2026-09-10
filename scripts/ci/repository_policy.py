#!/usr/bin/env python3
"""Deterministic repository policy checks for choo-guard."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MAX_FILE_BYTES = 50 * 1024 * 1024
FORBIDDEN_PARTS = {"Library", "Temp", "Logs", "UserSettings", "MemoryCaptures", "Recordings"}
FORBIDDEN_SUFFIXES = {".ulf", ".pem", ".p12", ".pfx", ".ckpt", ".pth", ".pt", ".safetensors", ".onnx"}
SECRET_PATTERNS = {
    "private key": re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
    "GitHub token": re.compile(r"(?:ghp|github_pat)_[A-Za-z0-9_]{20,}"),
    "AWS access key": re.compile(r"AKIA[0-9A-Z]{16}"),
    "Hugging Face token": re.compile(r"hf_[A-Za-z0-9]{20,}"),
}
UNITY_TEXT_SUFFIXES = {".unity", ".prefab", ".asset", ".mat"}
ACTION_USE = re.compile(r"^\s*-?\s*uses:\s*([^\s#]+)", re.MULTILINE)
PINNED_ACTION = re.compile(r"^[^@]+@[0-9a-fA-F]{40}$")


def git(*args: str, quiet: bool = False) -> str:
    if quiet:
        completed = subprocess.run(
            ["git", "-C", str(ROOT), *args],
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            encoding="utf-8",
            check=True,
        )
        return completed.stdout
    return subprocess.check_output(["git", "-C", str(ROOT), *args], encoding="utf-8")


def tracked_files() -> list[Path]:
    output = git("ls-files", "-z")
    return [ROOT / item for item in output.split("\0") if item]


def changed_files(base: str, head: str) -> list[Path]:
    output = git("diff", "--name-only", "-z", "--diff-filter=ACMR", f"{base}...{head}")
    return [ROOT / item for item in output.split("\0") if item]


def missing_commits(*revisions: str) -> list[str]:
    """Return the revisions this clone cannot resolve to a commit.

    Any of these can produce an unresolvable revision: a squash-merged pull
    request whose head ref was deleted, a shallow/partial clone that never
    fetched the object, a typo, or a GC'd/rewritten commit. This check does
    not determine *why* a revision is unresolvable, only *that* it is; the
    motivating case for adding it is a gate run that reaches this script
    after a PR's head commit has become unreachable (e.g. following a
    squash-merge that deleted the head ref).

    Resolvability alone does not guarantee `changed_files()` will succeed:
    two individually-resolvable revisions can still lack a common merge
    base, which is handled separately in `main()`.
    """
    absent: list[str] = []
    for revision in revisions:
        try:
            git("rev-parse", "--verify", "--quiet", f"{revision}^{{commit}}", quiet=True)
        except subprocess.CalledProcessError:
            absent.append(revision)
    return absent


def relative(file: Path) -> str:
    return file.relative_to(ROOT).as_posix()


def inspect(files: list[Path]) -> list[str]:
    errors: list[str] = []
    tracked_rel = {relative(p) for p in tracked_files()}

    for file in files:
        rel = relative(file)
        parts = set(file.relative_to(ROOT).parts)
        if parts & FORBIDDEN_PARTS:
            errors.append(f"forbidden generated path: {rel}")
        if rel == ".env" or rel.startswith(".env.") and rel != ".env.example":
            errors.append(f"secret environment file: {rel}")
        if file.suffix.lower() in FORBIDDEN_SUFFIXES:
            errors.append(f"forbidden credential/model file: {rel}")
        if rel.startswith(("data/raw/", "data/restricted/", "private-data/", "checkpoints/", "models/")):
            errors.append(f"restricted data/model path: {rel}")
        if not file.exists() or not file.is_file():
            continue
        if file.stat().st_size > MAX_FILE_BYTES:
            errors.append(f"file exceeds 50 MiB policy limit: {rel} ({file.stat().st_size} bytes)")

        if rel.startswith("Assets/") and not rel.endswith(".meta"):
            meta = f"{rel}.meta"
            if meta not in tracked_rel:
                errors.append(f"missing Unity meta file: {meta}")

        if file.suffix.lower() in UNITY_TEXT_SUFFIXES:
            try:
                prefix = file.read_bytes()[:32]
            except OSError:
                prefix = b""
            if prefix and not prefix.startswith(b"%YAML"):
                errors.append(f"Unity asset is not Force Text YAML: {rel}")

        if file.stat().st_size <= 1_000_000:
            try:
                text = file.read_text(encoding="utf-8")
            except (UnicodeDecodeError, OSError):
                text = ""
            for name, pattern in SECRET_PATTERNS.items():
                if pattern.search(text):
                    errors.append(f"possible {name} in {rel}")

    for workflow in sorted((ROOT / ".github/workflows").glob("*.y*ml")):
        text = workflow.read_text(encoding="utf-8")
        for use in ACTION_USE.findall(text):
            if use.startswith(("./", "docker://")):
                continue
            if not PINNED_ACTION.match(use):
                errors.append(f"GitHub Action is not pinned to a full commit SHA: {relative(workflow)} -> {use}")

    manifest = ROOT / "Packages/manifest.json"
    if manifest.exists():
        try:
            dependencies = json.loads(manifest.read_text(encoding="utf-8")).get("dependencies", {})
        except json.JSONDecodeError as exc:
            errors.append(f"invalid Packages/manifest.json: {exc}")
        else:
            for package, value in dependencies.items():
                if isinstance(value, str) and "github.com" in value:
                    ref = value.rsplit("#", 1)[1] if "#" in value else ""
                    if not ref or ref in {"main", "master", "beta", "develop"}:
                        errors.append(f"Unity Git dependency must use an immutable tag or SHA: {package} -> {value}")

    schema = ROOT / "schemas/scene-bundle.schema.json"
    try:
        json.loads(schema.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        errors.append(f"invalid SceneBundle schema: {exc}")

    required_manifest = {"schemaVersion", "sceneId", "createdAt", "sourceHash", "model", "coordinates", "assets", "qa", "dataClassification"}
    for scene_manifest in ROOT.rglob("scene_manifest.json"):
        if any(part in {"Library", "Temp", ".git"} for part in scene_manifest.parts):
            continue
        try:
            data = json.loads(scene_manifest.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            errors.append(f"invalid JSON: {relative(scene_manifest)}: {exc}")
            continue
        missing = sorted(required_manifest - set(data))
        if missing:
            errors.append(f"SceneBundle manifest missing {missing}: {relative(scene_manifest)}")
        qa = data.get("qa", {})
        if isinstance(qa, dict) and qa.get("approved") is True and qa.get("controlPointCount", 0) < 10:
            errors.append(f"approved SceneBundle needs at least 10 control points: {relative(scene_manifest)}")

    return sorted(set(errors))


def main() -> int:
    # Fail-open policy decision (required before this gate can be treated as a
    # security control, not just CI hygiene): when the changed-file scope is
    # unavailable, this script falls back to scanning *all* tracked files and
    # still exits 1 if that full scan finds a violation, 0 otherwise. It does
    # NOT fail closed (i.e. it never forces a non-zero/blocking exit purely
    # because the scope degraded). This is a deliberate, scoped choice:
    #   - The full-tree fallback is a strict superset of the normal
    #     changed-files scope, so the fallback scan can only find the same
    #     violations or more, never fewer, for files that are actually
    #     present in the checked-out working tree.
    #   - The residual risk this does NOT cover is D2-class: the checked-out
    #     tree itself may not be the PR head (e.g. checkout resolved to
    #     `develop`), so a clean fallback result proves the checked-out tree
    #     is clean, not that the PR head is. That is why a degraded run always
    #     records the actual tree SHA in `## Notes` and always emits an
    #     `::warning::` annotation (below) instead of only writing prose into
    #     a step summary nobody opens.
    #   - Failing closed (blocking merges outright whenever scope degrades)
    #     was rejected: this failure mode is expected to fire on innocuous
    #     shallow-clone/race conditions, and a gate that blocks every PR
    #     merged near this race would be treated as flaky noise and routed
    #     around (e.g. admin-merged) rather than fixed, which is a worse
    #     security outcome than a loud, correctly-scoped fallback scan.
    # If this tradeoff changes, add a `--strict-on-fallback` flag that exits
    # non-zero whenever `degraded` is True, with a dedicated test asserting
    # that exit behaviour; do not silently change the default below.
    parser = argparse.ArgumentParser()
    parser.add_argument("--base")
    parser.add_argument("--head", default="HEAD")
    parser.add_argument("--report")
    args = parser.parse_args()

    notes: list[str] = []
    scope = "all tracked files"
    degraded = False

    if args.base:
        absent = missing_commits(args.base, args.head)
        files: list[Path] | None = None
        if not absent:
            try:
                files = changed_files(args.base, args.head)
                scope = f"changed files in `{args.base}...{args.head}`"
            except subprocess.CalledProcessError as exc:
                # Both revisions resolved individually, but the diff itself
                # still failed (e.g. no merge base between them). This is
                # the same failure signature as an unresolvable revision, so
                # it must fall back the same way instead of propagating.
                absent = [f"{args.base}...{args.head} (diff failed: exit {exc.returncode})"]
        if absent:
            degraded = True
            notes.append(
                "changed-file scope unavailable: this clone cannot resolve "
                + ", ".join(f"`{revision}`" for revision in absent)
                + ". Fell back to all tracked files."
            )
            files = tracked_files()
            try:
                actual_tree = git("rev-parse", "HEAD", quiet=True).strip()
            except subprocess.CalledProcessError:
                actual_tree = "unknown"
            notes.append(
                "full scan was performed against the working tree currently checked "
                f"out (`{actual_tree}`), which may not be the PR head."
            )
    else:
        files = tracked_files()

    errors = inspect(files)

    if degraded:
        # Printed to this step's stdout so the GitHub Actions runner surfaces
        # it as an annotation even though the job still exits 0 on a clean
        # repository; a silently-green degraded scan should not be invisible.
        print(
            "::warning::repository policy gate fell back to a full scan; "
            "changed-file scope was unavailable (see report Notes)"
        )

    lines = ["# Repository policy report", "", f"Scope: {scope}", f"Checked files: {len(files)}", f"Violations: {len(errors)}", ""]
    if notes:
        lines.append("## Notes")
        lines.append("")
        lines.extend(f"- {item}" for item in notes)
        lines.append("")
    if errors:
        lines.append("## Violations")
        lines.append("")
        lines.extend(f"- {item}" for item in errors)
        lines.append("")
    report = "\n".join(lines) + "\n"
    if args.report:
        Path(args.report).write_text(report, encoding="utf-8")
    print(report, end="")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
