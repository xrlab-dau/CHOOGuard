#!/usr/bin/env python3
"""Repository policy behind the required check "Policy, security and repository hygiene".

Works in a blobless partial clone: tree listings and diffs come from git metadata, and blob contents are
fetched only for the Unity text assets a change touches. Invariants that hold for the whole tree are
checked everywhere; rules that older history already breaks (research files committed before their ignore
rules) apply to the paths a pull request or push adds or modifies, so existing debt never blocks a PR.
"""
from __future__ import annotations

import argparse
import json
import os
import posixpath
import re
import subprocess
import sys
import urllib.error
import urllib.request
from dataclasses import dataclass
from typing import Callable, Iterable, Sequence

MIB = 1024 * 1024
WARN_BYTES = 50 * MIB  # GitHub warns on push above 50 MiB
LIMIT_BYTES = 100 * MIB  # GitHub rejects blobs above 100 MiB
GENERATED_ROOTS = ("Library/", "Temp/", "Obj/", "obj/", "Logs/", "UserSettings/", "Builds/", "MemoryCaptures/", "Recordings/")
JUNK_NAMES = (".DS_Store", "Thumbs.db")
BYTECODE_SUFFIXES = (".pyc", ".pyo")
# Unity writes these as YAML under Force Text; LightingData/NavMesh `.asset` files stay binary by design.
UNITY_TEXT_EXTENSIONS = frozenset((".unity", ".prefab", ".mat", ".controller", ".overridecontroller", ".anim", ".mask", ".physicmaterial", ".lighting"))
# TextMesh Pro empties Dynamic/DynamicOS font assets that have "Clear Dynamic Data On Build" on every editor exit
# and every player build (com.unity.ugui 2.0 TMP_EditorResourceManager, TMP_PreBuildProcessor). Their resting,
# committed state is therefore empty; editor sessions refill them on demand from the source font (#248).
TMP_DYNAMIC_MODES = frozenset(("1", "2"))
RELEASE_SOURCES = ("develop",)
RELEASE_SOURCE_PREFIXES = ("hotfix/", "release/")
GRAPH_FILE = "graphify-out/graph.json"
GRAPH_MANIFEST = "graphify-out/manifest.json"
ZERO_SHA = "0" * 40
MAX_ANNOTATIONS = 40
MAX_LISTED = 60


@dataclass(frozen=True)
class Finding:
    level: str  # error | warning | notice
    check: str
    message: str
    path: str | None = None


# ---------------------------------------------------------------- rules (pure; unit-tested)

def unity_hidden(name: str) -> bool:
    """Names the Unity asset database skips entirely, so they need no `.meta`."""
    return name.startswith(".") or name.endswith("~") or name.lower() == "cvs" or name.endswith(".tmp")


def asset_roots(paths: Iterable[str]) -> list[str]:
    """`Assets` plus every embedded package (a tracked `Packages/<name>/package.json`)."""
    roots = ["Assets"]
    for path in paths:
        parts = path.split("/")
        if len(parts) == 3 and parts[0] == "Packages" and parts[2] == "package.json":
            roots.append("Packages/" + parts[1])
    return roots


def check_unity_meta(paths: Sequence[str]) -> list[Finding]:
    """Every asset file and folder under an asset root has a tracked `.meta`, and every `.meta` has its asset."""
    findings: list[Finding] = []
    tracked = set(paths)
    for root in asset_roots(paths):
        prefix = root + "/"
        under = [p for p in paths if p.startswith(prefix)]
        visible = lambda rel: not any(unity_hidden(part) for part in rel.split("/"))  # noqa: E731
        files = [p for p in under if visible(p[len(prefix):])]
        # A folder exists in every clone when anything beneath it is tracked, even names Unity skips.
        folders: set[str] = set()
        for path in under:
            parent = posixpath.dirname(path)
            while parent != root and parent.startswith(prefix):
                if visible(parent[len(prefix):]):
                    folders.add(parent)
                parent = posixpath.dirname(parent)
        for path in files:
            if path.endswith(".meta"):
                target = path[:-5]
                if target not in tracked and target not in folders:
                    findings.append(Finding("error", "unity-meta", "orphan .meta: its asset is not tracked, so Unity deletes it on import (git does not track empty folders)", path))
            elif path + ".meta" not in tracked:
                findings.append(Finding("error", "unity-meta", "asset has no tracked .meta: every clone generates a new GUID and breaks references", path))
        for folder in sorted(folders):
            if folder + ".meta" not in tracked:
                findings.append(Finding("error", "unity-meta", "folder has no tracked .meta: every clone generates a new GUID", folder))
    return findings


def check_generated(paths: Iterable[str]) -> list[Finding]:
    """Editor caches, build output, Python bytecode and OS junk never belong in the repository."""
    findings = []
    for path in paths:
        name = posixpath.basename(path)
        if path.startswith(GENERATED_ROOTS):
            reason = "Unity/IDE generated folder"
        elif "__pycache__" in path.split("/") or name.endswith(BYTECODE_SUFFIXES):
            reason = "Python bytecode"
        elif name in JUNK_NAMES:
            reason = "operating-system metadata"
        else:
            continue
        findings.append(Finding("error", "generated-files", reason + " is tracked", path))
    return findings


def check_ignored(changed: Sequence[str], ignored: Callable[[Sequence[str]], set[str]]) -> list[Finding]:
    """A change must not add or modify paths that the repository's own ignore rules exclude."""
    hits = ignored(changed) if changed else set()
    return [Finding("error", "ignored-files", "path matches .gitignore but is tracked by this change (force-added?)", path) for path in changed if path in hits]


def check_serialization_mode(editor_settings: str | None) -> list[Finding]:
    if editor_settings is None:
        return [Finding("error", "unity-text", "ProjectSettings/EditorSettings.asset is missing")]
    if "m_SerializationMode: 2" not in editor_settings:
        return [Finding("error", "unity-text", "asset serialization must stay Force Text (m_SerializationMode: 2) so scenes and prefabs merge", "ProjectSettings/EditorSettings.asset")]
    return []


def unity_text_asset(path: str) -> bool:
    return posixpath.splitext(path)[1].lower() in UNITY_TEXT_EXTENSIONS


def check_text_assets(heads: dict[str, bytes]) -> list[Finding]:
    """`heads` maps changed Unity text-asset paths to their first bytes at the head commit."""
    return [Finding("error", "unity-text", "binary-serialized Unity asset: re-save with Force Text so it can be reviewed and merged", path)
            for path, head in sorted(heads.items()) if not head.startswith(b"%YAML")]


def tmp_font_asset(path: str) -> bool:
    """TextMesh Pro names its font assets "<Font> SDF.asset" (fallbacks "<Font> SDF - Fallback.asset")."""
    return path.startswith(("Assets/", "Packages/")) and path.endswith(".asset") and "SDF" in posixpath.basename(path)


def check_dynamic_fonts(texts: dict[str, bytes]) -> list[Finding]:
    """Changed dynamic, clear-on-build TMP font assets must be committed empty, the state TMP itself writes at rest."""
    findings = []
    for path, data in sorted(texts.items()):
        text = data.decode("utf-8", errors="replace")
        mode = re.search(r"^\s*m_AtlasPopulationMode: (\d+)\s*$", text, re.M)
        clear = re.search(r"^\s*m_ClearDynamicDataOnBuild: (\d+)\s*$", text, re.M)
        if not mode or mode.group(1) not in TMP_DYNAMIC_MODES or not clear or clear.group(1) != "1":
            continue
        if not re.search(r"^\s*m_GlyphTable: \[\]\s*$", text, re.M):
            findings.append(Finding("error", "tmp-dynamic-font",
                                    "dynamic TextMesh Pro font committed with editor-filled glyphs; TMP empties it on every Unity exit and build, "
                                    "so it is committed empty: leave it out of the commit or run `git checkout -- <file>` (Unity refills it on demand)", path))
    return findings


def check_sizes(sizes: dict[str, int]) -> list[Finding]:
    findings = []
    for path, size in sorted(sizes.items()):
        if size > LIMIT_BYTES:
            findings.append(Finding("error", "large-files", f"{size / MIB:.1f} MiB exceeds GitHub's 100 MiB blob limit", path))
        elif size > WARN_BYTES:
            findings.append(Finding("warning", "large-files", f"{size / MIB:.1f} MiB file: every clone downloads it; prefer references outside git", path))
    return findings


def check_branch_flow(base_ref: str, head_ref: str) -> list[Finding]:
    """`main` only receives release merges: develop, release/* or hotfix/* branches."""
    if base_ref != "main" or head_ref in RELEASE_SOURCES or head_ref.startswith(RELEASE_SOURCE_PREFIXES):
        return []
    return [Finding("error", "branch-flow", f"pull requests into main must come from develop, release/* or hotfix/* (got '{head_ref}')")]


def graph_findings(graph_commit: str | None, changed_since: Sequence[str], indexed_extensions: set[str]) -> list[Finding]:
    """Advisory only: indexed files changed after the commit that last updated the context graph."""
    if not graph_commit:
        return [Finding("notice", "graph-freshness", f"{GRAPH_FILE} is not tracked; freshness not checked")]
    stale = [p for p in changed_since if not p.startswith("graphify-out/") and posixpath.splitext(p)[1].lower() in indexed_extensions]
    if not stale:
        return []
    sample = ", ".join(stale[:5]) + (" …" if len(stale) > 5 else "")
    return [Finding("warning", "graph-freshness",
                    f"graphify-out is {len(stale)} indexed file(s) behind its last update ({graph_commit[:8]}): {sample}. "
                    "Run `graphify update .` and commit graphify-out/.")]


# ---------------------------------------------------------------- git and GitHub access

def git(*args: str, stdin: bytes | None = None, check: bool = True) -> bytes:
    result = subprocess.run(["git", *args], input=stdin, capture_output=True, check=False)
    if check and result.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed: {result.stderr.decode(errors='replace').strip()}")
    return result.stdout


def split_z(raw: bytes) -> list[str]:
    return [p for p in raw.decode("utf-8").split("\0") if p]


def tree_paths(rev: str) -> list[str]:
    return split_z(git("ls-tree", "-r", "--name-only", "-z", rev))


def changed_blobs(base: str, head: str) -> dict[str, str]:
    """Regular files added, modified or type-changed since the merge base of `base` and `head`, with their new blob OIDs."""
    fields = git("diff", "--raw", "--no-abbrev", "--no-renames", "-z", "--diff-filter=AMT", f"{base}...{head}").split(b"\0")
    blobs = {}
    for meta, path in zip(fields[0::2], fields[1::2]):
        _, new_mode, _, new_oid, _ = meta.decode().split()
        if new_mode in ("100644", "100755"):  # skip symlinks and submodules
            blobs[path.decode("utf-8")] = new_oid
    return blobs


def git_ignored(paths: Sequence[str]) -> set[str]:
    # The team works on macOS/Windows (case-insensitive), where `Obj/` also ignores `obj/`; Linux runners must agree.
    result = subprocess.run(["git", "-c", "core.ignorecase=true", "check-ignore", "--no-index", "--stdin", "-z"],
                            input="\0".join(paths).encode(), capture_output=True, check=False)
    if result.returncode not in (0, 1):  # 1 = nothing ignored
        raise RuntimeError("git check-ignore failed: " + result.stderr.decode(errors="replace"))
    return set(split_z(result.stdout))


def blob_heads(oids: dict[str, str], length: int | None = 8) -> dict[str, bytes]:
    """First `length` bytes (whole blob when None). Missing blobs are requested in one fetch (as git's own promisor fetch does), then read locally."""
    if not oids:
        return {}
    subprocess.run(["git", "-c", "fetch.negotiationAlgorithm=noop", "fetch", "-q", "--no-tags", "--no-write-fetch-head",
                    "--recurse-submodules=no", "--filter=blob:none", "--stdin", "origin"],
                   input="\n".join(oids.values()).encode(), capture_output=True, check=False)  # best effort; cat-file fetches stragglers lazily
    process = subprocess.run(["git", "cat-file", "--batch"], input="".join(oid + "\n" for oid in oids.values()).encode(), capture_output=True, check=True)
    heads, data, cursor = {}, process.stdout, 0
    for path in oids:
        end = data.index(b"\n", cursor)
        header = data[cursor:end].split()
        if header[-1] == b"missing":
            cursor = end + 1
            continue
        size = int(header[2])
        heads[path] = data[end + 1:end + 1 + (size if length is None else min(size, length))]
        cursor = end + 1 + size + 1
    return heads


def api_blob_sizes(repo: str, sha: str, token: str) -> dict[str, int] | None:
    request = urllib.request.Request(f"https://api.github.com/repos/{repo}/git/trees/{sha}?recursive=1",
                                     headers={"Authorization": f"Bearer {token}", "Accept": "application/vnd.github+json", "X-GitHub-Api-Version": "2022-11-28"})
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            tree = json.load(response)
    except (urllib.error.URLError, TimeoutError, ValueError):
        return None
    if tree.get("truncated"):
        return None
    return {entry["path"]: entry["size"] for entry in tree["tree"] if entry.get("type") == "blob"}


def graph_state(head: str) -> tuple[str | None, list[str], set[str]]:
    commit = git("log", "-1", "--format=%H", head, "--", GRAPH_FILE).decode().strip() or None
    if not commit:
        return None, [], set()
    changed = split_z(git("diff", "--name-only", "--no-renames", "-z", commit, head))
    try:
        manifest = json.loads(git("show", f"{head}:{GRAPH_MANIFEST}"))
        extensions = {posixpath.splitext(p)[1].lower() for p in manifest} - {""}
    except (RuntimeError, ValueError):
        extensions = {".cs", ".py", ".mjs", ".js", ".md"}
    return commit, changed, extensions


# ---------------------------------------------------------------- reporting

def escape(value: str, prop: bool = False) -> str:
    value = value.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
    return value.replace(":", "%3A").replace(",", "%2C") if prop else value


def annotation(finding: Finding) -> str:
    where = f"file={escape(finding.path, True)}," if finding.path else ""
    return f"::{finding.level} {where}title={escape(finding.check, True)}::{escape(finding.message)}"


def summary(findings: Sequence[Finding], checks: Sequence[str], notes: Sequence[str]) -> str:
    lines = ["## Repository policy", "", "| Check | Result |", "|---|---|"]
    for check in checks:
        own = [f for f in findings if f.check == check]
        errors = sum(f.level == "error" for f in own)
        warnings = sum(f.level == "warning" for f in own)
        result = f"❌ {errors} error(s)" if errors else (f"⚠️ {warnings} warning(s)" if warnings else "✅ pass")
        lines.append(f"| `{check}` | {result} |")
    listed = [f for f in findings if f.level != "notice"]
    if listed:
        lines += ["", "<details><summary>Findings</summary>", ""]
        lines += [f"- **{f.level}** `{f.check}` {('`' + f.path + '` — ') if f.path else ''}{f.message}" for f in listed[:MAX_LISTED]]
        if len(listed) > MAX_LISTED:
            lines.append(f"- … {len(listed) - MAX_LISTED} more (see the job log)")
        lines += ["", "</details>"]
    if notes:
        lines += [""] + [f"> {note}" for note in notes]
    return "\n".join(lines) + "\n"


# ---------------------------------------------------------------- entry point

def main(argv: Sequence[str] | None = None) -> int:
    env = os.environ.get
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--event", default=env("GITHUB_EVENT_NAME", "local"))
    parser.add_argument("--head", default=env("CI_HEAD_SHA") or "HEAD")
    parser.add_argument("--base", default=env("CI_BASE_SHA", ""), help="base commit of the change (PR base or push 'before')")
    parser.add_argument("--base-ref", default=env("CI_BASE_REF", ""))
    parser.add_argument("--head-ref", default=env("CI_HEAD_REF", ""))
    parser.add_argument("--repo", default=env("GITHUB_REPOSITORY", ""))
    args = parser.parse_args(argv)

    head = git("rev-parse", args.head).decode().strip()
    paths = tree_paths(head)
    findings = check_unity_meta(paths) + check_generated(paths)
    try:
        editor_settings = git("show", f"{head}:ProjectSettings/EditorSettings.asset").decode()
    except RuntimeError:
        editor_settings = None
    findings += check_serialization_mode(editor_settings)
    notes = []

    blobs: dict[str, str] = {}
    if args.base and args.base != ZERO_SHA:
        blobs = changed_blobs(args.base, head)
        notes.append(f"Change-scoped rules covered {len(blobs)} added/modified file(s) in `{args.base[:8]}...{head[:8]}`.")
    else:
        notes.append("No base commit (scheduled, manual or new-branch run): change-scoped rules skipped.")
    changed = list(blobs)
    findings += check_ignored(changed, git_ignored)
    findings += check_text_assets(blob_heads({p: oid for p, oid in blobs.items() if unity_text_asset(p)}))
    findings += check_dynamic_fonts(blob_heads({p: oid for p, oid in blobs.items() if tmp_font_asset(p)}, length=None))
    token = env("GITHUB_TOKEN", "")
    if changed and token and args.repo:
        sizes = api_blob_sizes(args.repo, head, token)
        if sizes is None:
            findings.append(Finding("error", "large-files", "Blob sizes unavailable (API error or truncated tree): cannot verify changed files."))
        else:
            for path in changed:
                if path not in sizes:
                    findings.append(Finding("error", "large-files", "Blob size missing from API response: cannot verify changed file.", path))
            findings += check_sizes({p: sizes[p] for p in changed if p in sizes})
    elif changed:
        notes.append("No GITHUB_TOKEN: size rule skipped.")
    if args.event == "pull_request":
        findings += check_branch_flow(args.base_ref, args.head_ref)
    findings += graph_findings(*graph_state(head))

    checks = ["unity-meta", "generated-files", "unity-text", "tmp-dynamic-font", "ignored-files", "large-files", "branch-flow", "graph-freshness"]
    for finding in findings[:MAX_ANNOTATIONS]:
        print(annotation(finding))
    for finding in findings[MAX_ANNOTATIONS:]:
        print(f"{finding.level}: [{finding.check}] {finding.path or ''} {finding.message}")
    report = summary(findings, checks, notes)
    if env("GITHUB_STEP_SUMMARY"):
        with open(env("GITHUB_STEP_SUMMARY"), "a", encoding="utf-8") as handle:
            handle.write(report)
    else:
        print(report)
    errors = sum(f.level == "error" for f in findings)
    print(f"policy: {len(paths)} tracked paths, {len(changed)} changed, {errors} error(s), {sum(f.level == 'warning' for f in findings)} warning(s)")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
