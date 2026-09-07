#!/usr/bin/env python3
"""Portable, offline project context. Validation never grants runtime acceptance."""
from __future__ import annotations

import argparse
import copy
from datetime import date, datetime, timezone
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import sys
from urllib.parse import parse_qsl, urlsplit

ROOT = Path(__file__).resolve().parents[2]
GRAPH_PATH = "docs/context/project-context.json"
SCHEMA_PATH = Path(__file__).with_name("context-graph.schema.json")
TEMPLATE_PATH = Path(__file__).with_name("view-template.html")
TOPICS = ("art", "runtime", "handoff")
MACHINES = ("local", "school-pc")
MAX_BYTES = 4_000_000
PRIVATE_PARTS = {".git", ".planning", "Library", "Temp", "Logs", "UserSettings", "private-data", "checkpoints"}
PRIVATE_TEXT = re.compile(r"(?:/Users/|/home/|(?<![A-Za-z0-9])[A-Za-z]:[\\/]|\\\\[^\\\s]+\\|(?:ghp|github_pat)_[A-Za-z0-9_]{20,})")
ORDER = {"policy": 0, "decision": 1, "contract": 2, "component": 3, "workflow": 4,
         "evidence": 5, "unknown": 6, "external_snapshot": 7}


def load_json(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Duplicate JSON key: " + key)
            result[key] = value
        return result

    def nonfinite(value):
        raise ValueError("Non-JSON numeric constant: " + value)

    try:
        with Path(path).open("rb") as stream:
            raw = stream.read(MAX_BYTES + 1)
        if len(raw) > MAX_BYTES:
            raise ValueError("JSON exceeds the context size limit.")
        return json.loads(raw.decode("utf-8"), object_pairs_hook=unique, parse_constant=nonfinite)
    except (OSError, UnicodeError, RecursionError) as error:
        raise ValueError("Cannot read context JSON: " + Path(path).name) from error


def repo_path(root, relative):
    if not isinstance(relative, str) or not relative or "\\" in relative:
        raise ValueError("Source path must be repository-relative POSIX text.")
    path = PurePosixPath(relative)
    if path.is_absolute() or any(p in {".", ".."} for p in relative.split("/")) or ":" in relative:
        raise ValueError("Source path must stay inside the repository: " + relative)
    if set(path.parts) & PRIVATE_PARTS or any(p == ".env" or p.startswith(".env.") for p in path.parts):
        raise ValueError("Private/generated context source is prohibited: " + relative)
    target = Path(root).joinpath(*path.parts)
    try:
        target.resolve().relative_to(Path(root).resolve())
    except ValueError as error:
        raise ValueError("Source link escapes the repository: " + relative) from error
    return target


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def _schema_errors(value, rule, schema, at="graph"):
    """The shipped schema uses only this explicit JSON Schema subset; no dependency needed."""
    if "$ref" in rule:
        target = schema
        for key in rule["$ref"].removeprefix("#/").split("/"):
            target = target[key]
        return _schema_errors(value, target, schema, at)
    if "oneOf" in rule:
        branches = [_schema_errors(value, item, schema, at) for item in rule["oneOf"]]
        return [] if sum(not e for e in branches) == 1 else [at + ": expected exactly one source form"]
    errors = []
    expected = rule.get("type")
    types = {"object": dict, "array": list, "string": str, "integer": int, "boolean": bool}
    if expected and (not isinstance(value, types[expected]) or expected == "integer" and isinstance(value, bool)):
        return [at + ": expected " + expected]
    if "const" in rule and value != rule["const"]:
        errors.append(at + ": invalid constant")
    if "enum" in rule and value not in rule["enum"]:
        errors.append(at + ": unknown value")
    if isinstance(value, dict):
        for key in rule.get("required", []):
            if key not in value:
                errors.append(at + ": missing " + key)
        properties = rule.get("properties", {})
        for key, item in value.items():
            if key in properties:
                errors.extend(_schema_errors(item, properties[key], schema, at + "." + key))
            elif rule.get("additionalProperties") is False:
                errors.append(at + ": unexpected key " + key)
    if isinstance(value, list):
        if len(value) < rule.get("minItems", 0) or len(value) > rule.get("maxItems", 10000):
            errors.append(at + ": invalid item count")
        if rule.get("uniqueItems") and len({json.dumps(x, sort_keys=True) for x in value}) != len(value):
            errors.append(at + ": duplicate array value")
        if "items" in rule:
            for i, item in enumerate(value):
                errors.extend(_schema_errors(item, rule["items"], schema, at + "[" + str(i) + "]"))
    if isinstance(value, str):
        if len(value) < rule.get("minLength", 0) or len(value) > rule.get("maxLength", 10000):
            errors.append(at + ": invalid text length")
        if "pattern" in rule and not re.search(rule["pattern"], value):
            errors.append(at + ": invalid text format")
        if rule.get("format") == "date":
            try:
                date.fromisoformat(value)
            except ValueError:
                errors.append(at + ": invalid date")
    return errors


def validate_graph(graph, root=ROOT):
    schema = load_json(SCHEMA_PATH)
    errors = _schema_errors(graph, schema, schema)
    if errors:
        return errors
    if PRIVATE_TEXT.search(json.dumps(graph, ensure_ascii=False)):
        errors.append("Graph contains a private machine path or credential-shaped value.")
    nodes = {node["id"]: node for node in graph["nodes"]}
    if len(nodes) != len(graph["nodes"]):
        errors.append("Duplicate node IDs.")
    decision_keys = {}
    for node in graph["nodes"]:
        name = node["id"]
        if node["kind"] == "decision":
            if "decisionKey" not in node:
                errors.append(name + ": decisions need an exclusive decisionKey.")
            if node["status"] == "active":
                if node["authority"] != "user_accepted":
                    errors.append(name + ": active decision lacks accepted provenance.")
                key = node.get("decisionKey", "")
                if key in decision_keys:
                    errors.append("Multiple active decisions for " + key)
                decision_keys[key] = name
        if node["kind"] == "evidence" and (node["status"] != "historical" or "coverage" not in node):
            errors.append(name + ": evidence must be a historical snapshot with coverage.")
        if node["kind"] == "external_snapshot" and node["freshnessPolicy"] != "external_snapshot":
            errors.append(name + ": external state must stay a dated snapshot.")
        if node["kind"] == "unknown" and node["status"] != "unknown":
            errors.append(name + ": unknown authority cannot claim acceptance.")
        for source in node["sources"]:
            if "repoPath" in source:
                try:
                    repo_path(root, source["repoPath"])
                    if source["repoPath"] == GRAPH_PATH:
                        errors.append(name + ": the graph cannot hash itself as authority.")
                except ValueError as error:
                    errors.append(name + ": " + str(error))
            else:
                url = urlsplit(source["url"])
                private_query = {"token", "access_token", "api_key", "password", "authorization", "signature", "session", "sessionid"}
                if url.scheme != "https" or not url.netloc or url.username or url.password or any(key.lower() in private_query for key, _ in parse_qsl(url.query)):
                    errors.append(name + ": use a public HTTPS source URL without credentials/session state.")
        if "coverage" in node:
            try:
                repo_path(root, node["coverage"]["receipt"])
            except ValueError as error:
                errors.append(name + ": " + str(error))
    supersession = {}
    for edge in graph["edges"]:
        a, b = edge["from"], edge["to"]
        if a not in nodes or b not in nodes:
            errors.append("Dangling edge: " + a + " -> " + b)
            continue
        if edge["relation"] == "supersedes":
            supersession.setdefault(a, []).append(b)
            if nodes[b]["status"] != "superseded":
                errors.append(b + ": superseded edge target must be marked superseded.")
            if nodes[a].get("decisionKey") != nodes[b].get("decisionKey"):
                errors.append("Supersession must preserve the exact decision scope.")
        if edge["relation"] == "verified_by" and nodes[b]["kind"] != "evidence":
            errors.append("verified_by must point to scoped historical evidence.")
        if nodes[a]["kind"] == "unknown" and edge["relation"] != "limits_claims":
            errors.append("Unknown external authority limits claims; it is not a blanket development gate.")
    visited, active = set(), set()

    def walk(identifier):
        if identifier in active:
            errors.append("Supersession cycle at " + identifier)
            return
        if identifier in visited:
            return
        active.add(identifier)
        for target in supersession.get(identifier, []):
            walk(target)
        active.remove(identifier)
        visited.add(identifier)

    for identifier in supersession:
        walk(identifier)
    return errors


def assess_node(node, root):
    changed, missing = [], []
    for source in node["sources"]:
        if "repoPath" not in source:
            continue
        path = repo_path(root, source["repoPath"])
        if not path.is_file():
            missing.append(source["repoPath"])
        elif digest(path) != source["sha256"]:
            changed.append(source["repoPath"])
    state = "missing" if missing else "stale" if changed else "matched"
    if node["freshnessPolicy"] == "external_snapshot":
        state = "live_status_unknown"
    result = {"sourceState": state, "changed": changed, "missing": missing,
              "claimStatus": node["status"], "acceptance": "not_assessed"}
    if "coverage" in node:
        coverage = {"state": "historical_source_match", "changed": [], "missing": [], "count": 0}
        try:
            receipt = load_json(repo_path(root, node["coverage"]["receipt"]))
            recorded = receipt[node["coverage"]["field"]]
            if not isinstance(recorded, dict) or not recorded:
                raise ValueError("Missing coverage manifest")
            for relative, expected in recorded.items():
                if not isinstance(expected, str) or not re.fullmatch(r"[0-9a-f]{64}", expected):
                    raise ValueError("Invalid coverage digest")
                path = repo_path(root, relative)
                if not path.is_file():
                    coverage["missing"].append(relative)
                elif digest(path) != expected:
                    coverage["changed"].append(relative)
            coverage["count"] = len(recorded)
            if coverage["changed"] or coverage["missing"]:
                coverage["state"] = "source_drift"
        except (ValueError, KeyError, TypeError, OSError):
            coverage["state"] = "coverage_unavailable"
        if state in {"stale", "missing"}:
            coverage["state"] = "receipt_changed_or_missing"
        result["coverage"] = coverage
    return result


def inspect_graph(graph, root=ROOT):
    errors = validate_graph(graph, root)
    return {"structureValid": not errors, "errors": errors, "acceptance": "not_assessed",
            "nodes": {} if errors else {node["id"]: assess_node(node, root) for node in graph["nodes"]}}


def make_brief(graph, root, topic, machine, history=False):
    if topic not in TOPICS or machine not in MACHINES:
        raise ValueError("Choose a known topic and machine label.")
    report = inspect_graph(graph, root)
    if report["errors"]:
        raise ValueError("Invalid graph: " + "; ".join(report["errors"]))
    selected = [node for node in graph["nodes"] if topic in node["topics"] and
                (history or node["status"] != "superseded")]
    selected.sort(key=lambda node: (ORDER[node["kind"]], node["id"]))
    rows = []
    for node in selected:
        item = copy.deepcopy(node)
        item["freshness"] = report["nodes"][node["id"]]
        if machine in node.get("machineNotes", {}):
            item["machineNote"] = node["machineNotes"][machine]
        rows.append(item)
    identifiers = {node["id"] for node in selected}
    read_next = []
    # Prefer topic components and workflow sources; rules remain explicitly linked in each node.
    preferred = {"art": "component.art_scene", "runtime": "component.station_world",
                 "handoff": "workflow.start_validate"}[topic]
    candidates = sorted(selected, key=lambda n: (n["id"] != preferred,
                        n["kind"] not in {"component", "workflow"}, ORDER[n["kind"]]))
    for node in candidates:
        if node["status"] == "superseded":
            continue
        for source in node["sources"]:
            if "repoPath" in source and source["repoPath"] not in read_next:
                read_next.append(source["repoPath"])
                if len(read_next) == 3:
                    break
        if len(read_next) == 3:
            break
    return {"topic": topic, "machine": machine, "historyIncluded": history,
            "acceptance": "not_assessed", "syntheticWorkBlocked": False,
            "notice": "Navigation only. Reread stale sources. Historical tests, unknown references and board snapshots do not grant current acceptance or external-action permission.",
            "nodes": rows, "edges": [edge for edge in graph["edges"] if edge["from"] in identifiers and edge["to"] in identifiers],
            "readNext": read_next}


def refresh_sources(graph, root, identifiers, reviewed=False, reason=""):
    if not reviewed or len(reason.strip()) < 8 or not identifiers:
        raise ValueError("Refresh needs explicit --reviewed, named --node values and a meaningful --reason after reading the sources.")
    if PRIVATE_TEXT.search(reason):
        raise ValueError("Review reason must not contain private paths or credentials.")
    errors = validate_graph(graph, root)
    if errors:
        raise ValueError("Fix graph structure before refresh: " + "; ".join(errors))
    updated = copy.deepcopy(graph)
    nodes = {node["id"]: node for node in updated["nodes"]}
    for identifier in identifiers:
        node = nodes.get(identifier)
        if node is None:
            raise ValueError("Unknown node: " + identifier)
        if node["kind"] in {"evidence", "external_snapshot"} or node["status"] == "superseded":
            raise ValueError("Historical evidence, superseded decisions and external snapshots cannot be refreshed. Add a separately sourced record.")
        for source in node["sources"]:
            if "repoPath" in source:
                path = repo_path(root, source["repoPath"])
                if not path.is_file():
                    raise ValueError("Reviewed source is missing: " + source["repoPath"])
                source["sha256"] = digest(path)
        node["observedAt"] = date.today().isoformat()
    updated["refreshLog"] = (updated.get("refreshLog", []) + [{"date": date.today().isoformat(),
                             "nodes": sorted(set(identifiers)), "reason": reason.strip()}])[-20:]
    return updated


def render_html(graph, root=ROOT):
    report = inspect_graph(graph, root)
    if report["errors"]:
        raise ValueError("Cannot render an invalid graph.")
    payload = json.dumps({"graph": graph, "assessment": report,
                          "generatedAt": datetime.now(timezone.utc).isoformat(timespec="seconds")}, ensure_ascii=False)
    payload = payload.replace("<", "\\u003c").replace("&", "\\u0026").replace("\u2028", "\\u2028").replace("\u2029", "\\u2029")
    return TEMPLATE_PATH.read_text(encoding="utf-8").replace("__CONTEXT_PAYLOAD__", payload)


def _write(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(content, encoding="utf-8")
    temporary.replace(path)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--graph", default=GRAPH_PATH, help="Repository-relative graph file.")
    commands = parser.add_subparsers(dest="command", required=True)
    validate = commands.add_parser("validate", help="Check structure and report freshness separately.")
    validate.add_argument("--json", action="store_true")
    brief = commands.add_parser("brief", help="Read a bounded topic/machine startup packet.")
    brief.add_argument("--topic", choices=TOPICS, required=True)
    brief.add_argument("--machine", choices=MACHINES, required=True)
    brief.add_argument("--history", action="store_true")
    brief.add_argument("--json", action="store_true")
    refresh = commands.add_parser("refresh", help="Consciously update reviewed current source pointers only.")
    refresh.add_argument("--node", action="append", required=True)
    refresh.add_argument("--reviewed", action="store_true")
    refresh.add_argument("--reason", required=True)
    render = commands.add_parser("render", help="Regenerate the offline HTML snapshot.")
    render.add_argument("--output", default="docs/context/index.html")
    args = parser.parse_args(argv)
    try:
        path = repo_path(ROOT, args.graph)
        graph = load_json(path)
        report = inspect_graph(graph, ROOT)
        if args.command == "validate":
            if args.json:
                print(json.dumps(report, ensure_ascii=False, indent=2))
            else:
                print("Context structure: " + ("valid" if report["structureValid"] else "invalid"))
                for error in report["errors"]:
                    print("ERROR " + error)
                for identifier, item in report["nodes"].items():
                    suffix = "; evidence=" + item["coverage"]["state"] if "coverage" in item else ""
                    print(identifier + ": " + item["sourceState"] + suffix)
                print("Freshness is separate from structure. Runtime/field acceptance: not assessed.")
            return 0 if report["structureValid"] else 1
        if report["errors"]:
            raise ValueError("Invalid graph: " + "; ".join(report["errors"]))
        if args.command == "brief":
            packet = make_brief(graph, ROOT, args.topic, args.machine, args.history)
            if args.json:
                print(json.dumps(packet, ensure_ascii=False, indent=2))
            else:
                print("CHOOGuard context | " + args.topic + " | " + args.machine)
                print(packet["notice"])
                for node in packet["nodes"]:
                    fresh = node["freshness"]
                    coverage = "; " + fresh["coverage"]["state"] if "coverage" in fresh else ""
                    print("\n[" + node["status"] + "; " + fresh["sourceState"] + coverage + "] " + node["id"])
                    print(node["summary"])
                    if "machineNote" in node:
                        print(node["machineNote"])
                    print("Read: " + " | ".join(s.get("repoPath", s.get("url")) for s in node["sources"][:2]))
                print("\nRead next: " + " -> ".join(packet["readNext"]))
                print("Synthetic work is not blocked by missing facility evidence. Current acceptance and external permissions are not inferred.")
        elif args.command == "refresh":
            updated = refresh_sources(graph, ROOT, args.node, args.reviewed, args.reason)
            _write(path, json.dumps(updated, ensure_ascii=False, indent=2) + "\n")
            print("Updated reviewed current source pointers: " + ", ".join(args.node))
            print("Historical coverage is unchanged. Rerun validate and render; this is not an acceptance approval.")
        elif args.command == "render":
            output = repo_path(ROOT, args.output)
            if not args.output.startswith("docs/context/") or output.suffix != ".html":
                raise ValueError("The offline view must be an HTML file under docs/context/.")
            _write(output, render_html(graph, ROOT))
            print("Offline context snapshot written: " + args.output + "; rerun after reviewed graph changes.")
        return 0
    except (ValueError, OSError, KeyError) as error:
        print("Context error: " + (str(error) if not isinstance(error, OSError) else type(error).__name__), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
