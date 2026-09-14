#!/usr/bin/env python3
"""M1-02 permission boundary (candidate): policy decisions from the M0-03 execution profile plus a guarded
synthetic workspace. No network call, no real secret or restricted material, no write outside a temporary directory.

decide() turns one request (role, action, target, data class, approvals, declared work scope) into allow or deny
with a fixed reason code. GuardedWorkspace.perform() applies an allowed file action inside a temporary workspace and
records the workspace state digest before and after, so a denied request is shown to leave state unchanged.
run_fixtures() replays the case catalog and recomputes the profile policy hash before and after every case; any
change invalidates the whole run.

This is a policy-decision layer with synthetic file effects. It does not claim OS-level sandbox, filesystem ACL,
network firewall or MCP enforcement; those remain M1-03 and M1-04 per the profile's m1BoundarySeparation.

Exit codes (run): 0 = every non-blocked case matched and the hash chain is intact; 1 = a case mismatched or the
chain was invalidated; 2 = the profile or settings are absent, invalid, or the published policy hash does not recompute.
"""
from __future__ import annotations

import argparse
import fnmatch
import hashlib
import json
import os
import platform
import re
import stat
import subprocess
import sys
import tempfile
from pathlib import Path, PurePosixPath

REPO = Path(__file__).resolve().parents[3]
DEFAULT_PROFILE = REPO / "docs/team/M0-03/execution-profile.json"
DEFAULT_SETTINGS = REPO / ".pi/settings.json"
sys.path.insert(0, str(REPO / "scripts/bootstrap"))
import verify_toolchain  # noqa: E402  read-only reuse of the canonical policy globs

CLASS_RANK = {"PUBLIC_SYNTHETIC": 0, "TEAM_INTERNAL": 1, "KORAIL_RESTRICTED": 2}
SECRET_SUFFIXES = (".pem", ".key", ".p12", ".pfx", ".ulf")
SECRET_NAMES = {"id_rsa", "id_ed25519", "credentials", "credentials.json", ".netrc", ".npmrc", ".pypirc"}
GENERATED_WRITE_DENY = ("library", "temp", "obj")
LOG_DIR = "logs"
DESTRUCTIVE = [re.compile(p, re.I) for p in (
    r"\brm\s+(-[a-z]*r[a-z]*f|-[a-z]*f[a-z]*r)\b", r"\bgit\s+reset\s+--hard\b", r"\bgit\s+clean\s+-[a-z]*f", r"\bgit\s+push\b.*(--force|\s-f\b)",
    r"\bgit\s+checkout\s+--\s", r"\bgit\s+branch\s+-D\b", r"\bRemove-Item\b.*-Recurse", r"\brmdir\s+/s\b", r"\bdel\s+/[sq]", r"\bformat\s+[a-z]:",
    r"\bdrop\s+(table|database)\b")]
NETWORK = re.compile(r"\b(curl|wget|Invoke-WebRequest|Invoke-RestMethod|ssh|scp|git\s+push|git\s+fetch|npm\s+install|pip\s+install|uv\s+sync)\b", re.I)


class ProfileError(ValueError):
    """The profile or settings cannot be used; the message is a fixed code."""


def canonical_sha(value) -> str:
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()


def recompute_policy_hash(profile: dict) -> str:
    policy = profile["policy"]
    return canonical_sha({"allowlistSha256": policy["allowlistSha256"], "policySources": policy["policySources"],
                          "dataClasses": policy["dataClasses"], "roleIds": [r["roleId"] for r in profile["roles"]],
                          "denyCaseIds": [c["caseId"] for c in profile["expectedDenyCases"]],
                          "egressDestinationIds": [d["destinationId"] for d in profile["egress"]["destinations"]]})


def load_model(profile_path: Path = DEFAULT_PROFILE, settings_path: Path = DEFAULT_SETTINGS) -> dict:
    try:
        profile = json.loads(Path(profile_path).read_text(encoding="utf-8"))
        settings = json.loads(Path(settings_path).read_text(encoding="utf-8"))
    except FileNotFoundError:
        raise ProfileError("profile_or_settings_missing") from None
    except ValueError:
        raise ProfileError("profile_or_settings_invalid") from None
    try:
        published = profile["policy"]["policyHash"]["value"]
        recomputed = recompute_policy_hash(profile)
        roles = {r["roleId"]: r for r in profile["roles"]}
        destinations = {d["destinationId"]: d for d in profile["egress"]["destinations"]}
        scope = settings["subagents"]["modelScope"]
        reviewer_deny = settings["subagents"]["watchdog"]["rules"]["roleModels"]["reviewer"]["deny"]
    except (KeyError, TypeError):
        raise ProfileError("profile_or_settings_invalid") from None
    if recomputed != published:
        raise ProfileError("policy_hash_mismatch")
    restricted = profile.get("protectedPaths", {}).get("observedInThisRepo", {}).get("restrictedSourceDirectoriesPresent", [])
    policy_globs = sorted(set(verify_toolchain.POLICY_GLOBS) | {s["path"] for s in profile["policy"]["policySources"]} | {"docs/team/M0-03/*"})
    return {"profile": profile, "policyHash": recomputed, "roles": roles, "destinations": destinations, "modelScope": scope,
            "reviewerDeny": reviewer_deny, "restrictedPrefixes": sorted({"private-data/"} | {p.rstrip("/") + "/" for p in restricted}),
            "policyGlobs": policy_globs}


def deny(code, case_ref, reason):
    return {"decision": "deny", "code": code, "dcRef": case_ref, "reason": reason}


def allow(reason):
    return {"decision": "allow", "code": "allowed", "dcRef": None, "reason": reason}


def lexical(path: str):
    """Repository-relative lexical form, or None when the text names an absolute or escaping path."""
    text = (path or "").replace("\\", "/")
    if not text or text.startswith("/") or re.match(r"^[a-zA-Z]:", text):
        return None
    parts = []
    for part in PurePosixPath(text).parts:
        if part == "..":
            if not parts:
                return None
            parts.pop()
        elif part not in (".", ""):
            parts.append(part)
    return "/".join(parts) if parts else None


def matches(path: str, patterns) -> bool:
    return any(fnmatch.fnmatchcase(path, p) or (p.endswith("/**") and path.startswith(p[:-2])) for p in patterns)


def is_link(path: Path) -> bool:
    try:
        info = os.lstat(path)
    except OSError:
        return False
    return stat.S_ISLNK(info.st_mode) or bool(getattr(info, "st_file_attributes", 0) & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0))


def decide(model: dict, request: dict, root: Path) -> dict:
    actor, action = request.get("actor"), request.get("action")
    role = model["roles"].get(actor)
    if role is None:
        return deny("unknown_role", None, "the actor is not a role in the execution profile")
    data_class = request.get("dataClass", "PUBLIC_SYNTHETIC")
    if data_class not in CLASS_RANK:
        return deny("unknown_data_class", "DC-03", "data class outside the profile vocabulary")
    approvals = set(request.get("approvals") or [])

    if action in ("read", "write", "delete"):
        rel = lexical(request.get("path"))
        if rel is None:
            return deny("out_of_root", "DC-09", "absolute or parent-escaping path")
        root_real = os.path.realpath(root)
        target_real = os.path.realpath(os.path.join(root, *rel.split("/")))
        if os.path.commonpath([root_real, target_real]) != root_real:
            return deny("link_escape", "DC-09", "a link or junction inside the workspace resolves outside it")
        lower = rel.lower()
        name = PurePosixPath(lower).name
        if data_class == "KORAIL_RESTRICTED" or any(lower.startswith(p.lower()) for p in model["restrictedPrefixes"]):
            return deny("restricted_input", "DC-02", "restricted source location; never read, listed or copied")
        if name == ".env" or (name.startswith(".env.") and name != ".env.example") or name.endswith(SECRET_SUFFIXES) or name in SECRET_NAMES:
            return deny("secret_material", "DC-02", "credential, key or environment file")
        top = lower.split("/", 1)[0]
        if action in ("write", "delete") and top in GENERATED_WRITE_DENY:
            return deny("generated_output", "DC-01", "only the Unity process creates Library/, Temp/ and Obj/")
        if action in ("write", "delete") and top == LOG_DIR:
            return deny("log_modification", "DC-12", "existing Unity logs may be read, not modified or deleted")
        if action == "read":
            return allow("read inside the workspace outside protected and restricted locations")
        if actor != "role:pm" and matches(rel, model["policyGlobs"]):
            return deny("policy_file", "DC-08", "policy files are written only by the PM or verifier")
        if actor != "role:pm" and lower.startswith("docs/evidence/") and (Path(root) / rel).exists():
            return deny("verification_result_overwrite", "DC-08", "existing verification results are append-only for agents")
        if actor != "role:pm" and not matches(rel, request.get("workScope") or []):
            return deny("outside_work_scope", "DC-09", "write outside the paths named by the current work contract")
        return allow("write inside the declared work scope")

    if action == "execute":
        command = request.get("command") or ""
        if any(p.search(command) for p in DESTRUCTIVE):
            return deny("destructive_command", None, "destructive command; the profile grants no destructive-command approval")
        if NETWORK.search(command) and not role.get("egress", {}).get("destinations"):
            return deny("network_command_without_destination", "DC-11", "the role has no egress destination in the profile")
        return allow("non-destructive command (decision only; not executed)")

    if action == "egress":
        destination = model["destinations"].get(request.get("destination"))
        if destination is None:
            return deny("unknown_destination", "DC-10", "destination is not on the profile's destination list (defaultDeny)")
        if actor not in destination.get("usedBy", []):
            return deny("destination_not_granted", "DC-10", "destination exists but is not granted to this role")
        if data_class == "KORAIL_RESTRICTED":
            return deny("restricted_egress", "DC-03", "restricted material never leaves for any destination")
        if data_class not in destination.get("allowedDataClasses", []):
            return deny("data_class_above_ceiling", "DC-03", "data class above the destination's allowed classes")
        if destination.get("approvalOwner") not in approvals:
            return deny("approval_not_recorded", "DC-03", "the destination's approval owner is not recorded for this item")
        return allow("granted destination, allowed data class and recorded approval (decision only; nothing sent)")

    if action == "select_model":
        model_id = request.get("model") or ""
        if request.get("agent") == "reviewer":
            if any(fnmatch.fnmatchcase(model_id, p) for p in model["reviewerDeny"]):
                return deny("reviewer_same_provider", "DC-05", "reviewer provider must differ from the authoring provider")
            if model_id not in model["modelScope"].get("agents", {}).get("reviewer", {}).get("allow", []):
                return deny("model_outside_scope", "DC-06", "model outside the reviewer allow list")
            return allow("reviewer model inside scope and provider-independent")
        role_allow = role.get("modelScope", {}).get("allow", [])
        if not any(fnmatch.fnmatchcase(model_id, p) for p in role_allow) or not any(fnmatch.fnmatchcase(model_id, p) for p in model["modelScope"].get("allow", [])):
            return deny("model_outside_scope", "DC-06", "model outside the role and settings allow lists")
        return allow("model inside the role and settings allow lists")

    return deny("unknown_action", None, "action is not part of the boundary vocabulary")


def workspace_digest(root: Path) -> str:
    entries = []
    for dirpath, dirnames, filenames in os.walk(root, followlinks=False):
        here = Path(dirpath)
        links = [d for d in dirnames if is_link(here / d)]
        entries += [((here / d).relative_to(root).as_posix(), "link" if d in links else "dir", "") for d in sorted(dirnames)]
        entries += [((here / f).relative_to(root).as_posix(), "file", hashlib.sha256((here / f).read_bytes()).hexdigest()) for f in sorted(filenames)]
        dirnames[:] = [d for d in dirnames if d not in links]
    return canonical_sha(sorted(entries))


class GuardedWorkspace:
    """A temporary synthetic workspace; allowed file actions are applied, denied ones never touch it."""

    def __init__(self, root: Path, model: dict):
        self.root, self.model = Path(root), model

    def perform(self, request: dict) -> dict:
        before = workspace_digest(self.root)
        decision = decide(self.model, request, self.root)
        effect = "none"
        if decision["decision"] == "allow" and request.get("action") in ("read", "write", "delete"):
            target = self.root.joinpath(*lexical(request["path"]).split("/"))
            if request["action"] == "read":
                effect = "read_bytes:%d" % (len(target.read_bytes()) if target.is_file() else -1)
            elif request["action"] == "write":
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text(request.get("content", "synthetic content\n"), encoding="utf-8")
                effect = "wrote"
            else:
                target.unlink()
                effect = "deleted"
        elif decision["decision"] == "allow":
            effect = "decision_only"
        after = workspace_digest(self.root)
        return {**decision, "effect": effect, "stateBefore": before, "stateAfter": after, "stateChanged": before != after}


SYNTHETIC_TREE = {
    "AGENTS.md": "synthetic policy placeholder\n",
    ".github/CODEOWNERS": "* @synthetic-lead\n",
    ".github/workflows/ci.yml": "name: synthetic\n",
    ".pi/settings.json": "{}\n",
    "docs/team/M0-03/execution-profile.json": "{}\n",
    "docs/evidence/SYNTH/receipt.json": "{\"synthetic\": true}\n",
    "scripts/team/M1-02/README.txt": "synthetic work scope\n",
    ".env": "SYNTHETIC_PLACEHOLDER=not-a-secret\n",
    ".env.example": "SYNTHETIC_PLACEHOLDER=\n",
    "keys/service.pem": "SYNTHETIC PLACEHOLDER, NOT A KEY\n",
    "private-data/facility-sources/placeholder.txt": "synthetic placeholder; real restricted content is never used\n",
    "Logs/Editor.log": "synthetic log line\n",
    "Library/.keep": "",
    "src/readme.md": "synthetic source\n",
}
AUTHOR_SCOPE = ["scripts/team/M1-02/**", "docs/team/M1-02/**"]
REVIEW_SCOPE = ["reviews/**"]
A, R, P, B, U = "role:authoring-agent", "role:independent-reviewer", "role:pm", "role:bootstrap-tooling", "role:unity-mcp-client"
FS = "policy decision plus a synthetic file action in a temporary workspace"
DECISION = "policy decision only; no process, network or model call is made"


def case(case_id, category, dc_ref, request, decision, code, capability, expected_failure):
    return {"id": case_id, "category": category, "dcRef": dc_ref, "request": request, "expect": {"decision": decision, "code": code},
            "capability": capability, "expectedFailure": expected_failure}


CASES = [
    case("M02-01", "out_of_root", "DC-09", {"actor": A, "action": "write", "path": "../outside.txt", "workScope": AUTHOR_SCOPE}, "deny", "out_of_root", FS, "parent-escaping write is refused and the workspace digest is unchanged"),
    case("M02-02", "out_of_root", "DC-09", {"actor": A, "action": "read", "path": "/etc/passwd"}, "deny", "out_of_root", FS, "absolute POSIX path is refused without reading"),
    case("M02-03", "out_of_root", "DC-09", {"actor": A, "action": "write", "path": "C:/escape.txt", "workScope": AUTHOR_SCOPE}, "deny", "out_of_root", FS, "absolute drive path is refused"),
    case("M02-04", "out_of_root", "DC-09", {"actor": A, "action": "write", "path": "scripts/team/M1-02/../../../../escape.txt", "workScope": AUTHOR_SCOPE}, "deny", "out_of_root", FS, "a path that starts inside the scope but climbs out is refused"),
    case("M02-05", "symlink", "DC-09", {"actor": A, "action": "write", "path": "scripts/team/M1-02/link-out/planted.txt", "workScope": AUTHOR_SCOPE, "setup": "link"}, "deny", "link_escape", FS + "; needs symlink privilege or a directory junction", "write through a link that resolves outside the workspace is refused; the outside directory stays empty"),
    case("M02-06", "secret", "DC-02", {"actor": A, "action": "read", "path": ".env"}, "deny", "secret_material", FS, "environment file is refused for reading"),
    case("M02-07", "secret", "DC-02", {"actor": A, "action": "read", "path": "keys/service.pem"}, "deny", "secret_material", FS, "key file is refused for reading"),
    case("M02-08", "secret", "DC-02", {"actor": P, "action": "write", "path": ".env.local"}, "deny", "secret_material", FS, "even the PM role cannot write an environment file"),
    case("M02-09", "secret", None, {"actor": A, "action": "read", "path": ".env.example"}, "allow", "allowed", FS, "the committed example file is not a secret and stays readable"),
    case("M02-10", "restricted_input", "DC-02", {"actor": P, "action": "read", "path": "private-data/facility-sources/placeholder.txt"}, "deny", "restricted_input", FS, "restricted source location is refused for every role"),
    case("M02-11", "restricted_input", "DC-03", {"actor": A, "action": "egress", "destination": "DEST-MODEL-AUTHOR", "dataClass": "KORAIL_RESTRICTED", "approvals": ["APR-EGRESS"]}, "deny", "restricted_egress", DECISION, "restricted class is refused even with a recorded approval"),
    case("M02-12", "egress", None, {"actor": A, "action": "egress", "destination": "DEST-MODEL-AUTHOR", "dataClass": "PUBLIC_SYNTHETIC", "approvals": ["APR-EGRESS"]}, "allow", "allowed", DECISION, "public synthetic content to the granted model destination with approval is allowed"),
    case("M02-13", "egress", "DC-03", {"actor": A, "action": "egress", "destination": "DEST-MODEL-AUTHOR", "dataClass": "PUBLIC_SYNTHETIC", "approvals": []}, "deny", "approval_not_recorded", DECISION, "sanitised public content still needs the recorded egress approval"),
    case("M02-14", "egress", "DC-03", {"actor": A, "action": "egress", "destination": "DEST-MODEL-AUTHOR", "dataClass": "TEAM_INTERNAL", "approvals": ["APR-EGRESS"]}, "deny", "data_class_above_ceiling", DECISION, "team-internal content above the model destination ceiling is refused"),
    case("M02-15", "egress", "DC-10", {"actor": A, "action": "egress", "destination": "DEST-UNLISTED-PASTE", "dataClass": "PUBLIC_SYNTHETIC", "approvals": ["APR-EGRESS"]}, "deny", "unknown_destination", DECISION, "destination absent from the profile is refused by default"),
    case("M02-16", "egress", "DC-10", {"actor": B, "action": "egress", "destination": "DEST-MODEL-AUTHOR", "dataClass": "PUBLIC_SYNTHETIC", "approvals": ["APR-EGRESS"]}, "deny", "destination_not_granted", DECISION, "a listed destination not granted to the bootstrap role is refused"),
    case("M02-17", "egress", None, {"actor": U, "action": "egress", "destination": "DEST-UNITY-MCP", "dataClass": "TEAM_INTERNAL", "approvals": ["APR-EGRESS"]}, "allow", "allowed", DECISION, "local MCP transport accepts team-internal content with approval"),
    case("M02-18", "egress", "DC-11", {"actor": B, "action": "execute", "command": "curl https://example.invalid/tool.sh"}, "deny", "network_command_without_destination", DECISION, "network command by a role with no destination is refused"),
    case("M02-19", "generated_output", "DC-01", {"actor": A, "action": "write", "path": "Library/ScriptAssemblies/planted.dll", "workScope": ["**"]}, "deny", "generated_output", FS, "Library/ write is refused even with a wildcard work scope"),
    case("M02-20", "generated_output", "DC-01", {"actor": A, "action": "delete", "path": "Library/.keep", "workScope": ["**"]}, "deny", "generated_output", FS, "Library/ delete is refused and the file remains"),
    case("M02-21", "generated_output", "DC-12", {"actor": A, "action": "write", "path": "Logs/Editor.log", "workScope": ["**"]}, "deny", "log_modification", FS, "overwriting an existing log is refused"),
    case("M02-22", "generated_output", "DC-12", {"actor": A, "action": "delete", "path": "Logs/Editor.log", "workScope": ["**"]}, "deny", "log_modification", FS, "deleting an existing log is refused"),
    case("M02-23", "generated_output", None, {"actor": A, "action": "read", "path": "Logs/Editor.log"}, "allow", "allowed", FS, "reading logs stays allowed"),
    case("M02-24", "destructive_command", None, {"actor": A, "action": "execute", "command": "git reset --hard origin/develop"}, "deny", "destructive_command", DECISION, "hard reset is refused"),
    case("M02-25", "destructive_command", None, {"actor": P, "action": "execute", "command": "git push --force origin develop"}, "deny", "destructive_command", DECISION, "force push is refused even for the PM role"),
    case("M02-26", "destructive_command", None, {"actor": A, "action": "execute", "command": "rm -rf ./Assets"}, "deny", "destructive_command", DECISION, "recursive forced delete is refused"),
    case("M02-27", "destructive_command", None, {"actor": A, "action": "execute", "command": "Remove-Item -Recurse -Force Packages"}, "deny", "destructive_command", DECISION, "PowerShell recursive delete is refused"),
    case("M02-28", "destructive_command", None, {"actor": A, "action": "execute", "command": "git clean -fdx"}, "deny", "destructive_command", DECISION, "forced clean is refused"),
    case("M02-29", "destructive_command", None, {"actor": A, "action": "execute", "command": "git status --porcelain"}, "allow", "allowed", DECISION, "a read-only command is allowed"),
    case("M02-30", "policy_file", "DC-08", {"actor": A, "action": "write", "path": "AGENTS.md", "workScope": ["**"]}, "deny", "policy_file", FS, "agent write to a policy file is refused even with a wildcard scope"),
    case("M02-31", "policy_file", "DC-08", {"actor": A, "action": "write", "path": ".github/workflows/ci.yml", "workScope": ["**"]}, "deny", "policy_file", FS, "workflow edit is refused"),
    case("M02-32", "policy_file", "DC-08", {"actor": R, "action": "write", "path": "docs/team/M0-03/execution-profile.json", "workScope": ["**"]}, "deny", "policy_file", FS, "reviewer cannot edit the execution profile"),
    case("M02-33", "policy_file", None, {"actor": P, "action": "write", "path": "AGENTS.md"}, "allow", "allowed", FS, "the PM role writes policy files"),
    case("M02-34", "policy_file", "DC-08", {"actor": A, "action": "write", "path": "docs/evidence/SYNTH/receipt.json", "workScope": ["docs/evidence/**"]}, "deny", "verification_result_overwrite", FS, "overwriting an existing verification result is refused"),
    case("M02-35", "work_scope", None, {"actor": A, "action": "write", "path": "scripts/team/M1-02/new_fixture.py", "workScope": AUTHOR_SCOPE}, "allow", "allowed", FS, "write inside the declared work scope is applied"),
    case("M02-36", "work_scope", "DC-09", {"actor": A, "action": "write", "path": "docs/team/OTHER/notes.md", "workScope": AUTHOR_SCOPE}, "deny", "outside_work_scope", FS, "write outside the declared scope is refused"),
    case("M02-37", "work_scope", "DC-09", {"actor": R, "action": "write", "path": "scripts/team/M1-02/README.txt", "workScope": REVIEW_SCOPE}, "deny", "outside_work_scope", FS, "reviewer cannot edit the artifact under review"),
    case("M02-38", "model_scope", "DC-05", {"actor": R, "action": "select_model", "agent": "reviewer", "model": "anthropic/claude-opus-5"}, "deny", "reviewer_same_provider", DECISION, "reviewer from the authoring provider is refused"),
    case("M02-39", "model_scope", None, {"actor": R, "action": "select_model", "agent": "reviewer", "model": "openai-codex/gpt-5.6-sol"}, "allow", "allowed", DECISION, "provider-independent reviewer inside scope is allowed"),
    case("M02-40", "model_scope", "DC-06", {"actor": A, "action": "select_model", "agent": "worker", "model": "unlisted-provider/model-x"}, "deny", "model_outside_scope", DECISION, "authoring model outside both allow lists is refused"),
    case("M02-41", "model_scope", None, {"actor": A, "action": "select_model", "agent": "worker", "model": "anthropic/claude-opus-5"}, "allow", "allowed", DECISION, "authoring model inside the allow lists is allowed"),
]


def build_workspace(base: Path) -> tuple[Path, Path]:
    root, outside = base / "workspace", base / "outside-target"
    for rel, text in SYNTHETIC_TREE.items():
        path = root.joinpath(*rel.split("/"))
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
    outside.mkdir()
    return root, outside


def make_link(link: Path, target: Path) -> str:
    link.parent.mkdir(parents=True, exist_ok=True)
    try:
        os.symlink(target, link, target_is_directory=True)
        return "symlink"
    except (OSError, NotImplementedError):
        pass
    if os.name == "nt":
        done = subprocess.run(["cmd", "/c", "mklink", "/J", str(link), str(target)], capture_output=True)
        if done.returncode == 0:
            return "junction"
    return "unavailable"


def capability_facts(link_kind: str) -> dict:
    return {"osFamily": platform.system(), "python": "%d.%d" % sys.version_info[:2], "linkCapability": link_kind,
            "networkEgressExercised": False, "osSandboxOrAclTested": False, "mcpEnforcementTested": False,
            "note": "anonymous capability facts only; no hostname, account, path or interpreter build string"}


def profile_state(profile_path: Path, settings_path: Path) -> dict:
    profile = json.loads(Path(profile_path).read_text(encoding="utf-8"))
    return {"policyHash": recompute_policy_hash(profile), "profileFileSha256": hashlib.sha256(Path(profile_path).read_bytes()).hexdigest(),
            "settingsFileSha256": hashlib.sha256(Path(settings_path).read_bytes()).hexdigest()}


def run_fixtures(profile_path: Path = DEFAULT_PROFILE, settings_path: Path = DEFAULT_SETTINGS, cases=None, after_case=None) -> dict:
    model = load_model(profile_path, settings_path)
    start = profile_state(profile_path, settings_path)
    results, chain, link_kind = [], [], "not_needed"
    with tempfile.TemporaryDirectory(prefix="m1-02-") as tmp:
        root, outside = build_workspace(Path(tmp))
        workspace = GuardedWorkspace(root, model)
        for item in (cases or CASES):
            before = profile_state(profile_path, settings_path)
            request = dict(item["request"])
            blocked = None
            if request.pop("setup", None) == "link":
                link_kind = make_link(root / "scripts/team/M1-02/link-out", outside)
                if link_kind == "unavailable":
                    blocked = "no symlink privilege and no junction support on this host"
            observed = None if blocked else workspace.perform(request)
            after = profile_state(profile_path, settings_path)
            chain.append({"caseId": item["id"], "before": before, "after": after, "intact": before == start and after == start})
            ok = None
            if not blocked:
                ok = (observed["decision"] == item["expect"]["decision"] and observed["code"] == item["expect"]["code"]
                      and (observed["decision"] == "allow" or not observed["stateChanged"]))
                if item["category"] == "symlink":
                    ok = ok and not any(outside.iterdir())
            target = request.get("path") or request.get("command") or request.get("destination") or request.get("model")
            results.append({"id": item["id"], "category": item["category"], "dcRef": item["dcRef"], "actor": request.get("actor"),
                            "action": request.get("action"), "target": target, "dataClass": request.get("dataClass"),
                            "capability": item["capability"], "expectedFailure": item["expectedFailure"], "expect": item["expect"],
                            "observed": None if blocked else {k: observed[k] for k in ("decision", "code", "dcRef", "effect", "stateChanged")},
                            "blocked": blocked, "ok": ok})
            if after_case:
                after_case(item["id"])
        if link_kind in ("symlink", "junction") and is_link(root / "scripts/team/M1-02/link-out"):
            os.rmdir(root / "scripts/team/M1-02/link-out") if link_kind == "junction" else os.unlink(root / "scripts/team/M1-02/link-out")
    intact = all(link["intact"] for link in chain)
    matched = all(r["ok"] for r in results if r["blocked"] is None)
    state = "invalidated_rerun_required" if not intact else "passed" if matched else "mismatch"
    return {"schemaVersion": 1, "kind": "m1-02-permission-boundary-run", "state": state,
            "profile": {"path": "docs/team/M0-03/execution-profile.json", "policyHashPublished": model["profile"]["policy"]["policyHash"]["value"],
                        "policyHashRecomputed": start["policyHash"], "fileSha256": start["profileFileSha256"]},
            "settings": {"path": ".pi/settings.json", "fileSha256": start["settingsFileSha256"]},
            "capability": capability_facts(link_kind), "cases": results, "hashChain": chain, "chainIntact": intact,
            "counts": {"total": len(results), "matched": sum(1 for r in results if r["ok"]), "mismatched": sum(1 for r in results if r["ok"] is False),
                       "blocked": sum(1 for r in results if r["blocked"]), "deny": sum(1 for r in results if r["expect"]["decision"] == "deny"),
                       "allow": sum(1 for r in results if r["expect"]["decision"] == "allow")}}


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    run = sub.add_parser("run", help="replay the case catalog in a temporary synthetic workspace")
    run.add_argument("--profile", type=Path, default=DEFAULT_PROFILE)
    run.add_argument("--settings", type=Path, default=DEFAULT_SETTINGS)
    run.add_argument("--output", type=Path)
    args = parser.parse_args(argv)
    try:
        report = run_fixtures(args.profile, args.settings)
    except ProfileError as error:
        print(json.dumps({"schemaVersion": 1, "kind": "m1-02-permission-boundary-run", "state": "blocked", "error": str(error)}))
        return 2
    text = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    if args.output is not None:
        with open(args.output, "x", encoding="utf-8", newline="\n") as stream:
            stream.write(text)
    print(text, end="")
    return 0 if report["state"] == "passed" else 1


if __name__ == "__main__":
    sys.exit(main())
