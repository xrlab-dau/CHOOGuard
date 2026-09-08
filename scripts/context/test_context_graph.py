"""Regression tests for portable context, claim boundaries and conscious refresh."""
from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SCRIPT = Path(__file__).with_name("context_graph.py")
SPEC = importlib.util.spec_from_file_location("context_graph", SCRIPT)
cg = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(cg)


class ContextGraphTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        (self.root / "docs").mkdir()
        (self.root / "src").mkdir()
        (self.root / "docs/rules.md").write_text("Current accepted rules.\n", encoding="utf-8")
        (self.root / "src/world.cs").write_text("world version one\n", encoding="utf-8")
        receipt = {"sourceSha256": {"src/world.cs": self.digest("src/world.cs")},
                   "passed": 99, "nativeManualResponseCompletion": "not claimed"}
        (self.root / "docs/receipt.json").write_text(json.dumps(receipt), encoding="utf-8")
        self.graph = {
            "schemaVersion": 1, "project": "CHOOGuard",
            "classification": "PUBLIC_PROJECT_CONTEXT",
            "description": "Portable navigation, not implementation acceptance.",
            "nodes": [
                self.node("visual.realistic", "decision", "active", "user_accepted",
                          decisionKey="visual_style"),
                self.node("visual.flat", "decision", "superseded", "user_accepted",
                          decisionKey="visual_style"),
                self.node("world", "component", "implemented", "tracked_source",
                          path="src/world.cs"),
                self.node("receipt", "evidence", "historical", "execution_receipt",
                          path="docs/receipt.json", freshnessPolicy="historical_evidence",
                          coverage={"receipt": "docs/receipt.json", "field": "sourceSha256"}),
                self.node("manual", "unknown", "unknown", "unverified"),
                self.node("board", "external_snapshot", "snapshot", "external_snapshot",
                          freshnessPolicy="external_snapshot",
                          sources=[{"url": "https://github.com/orgs/xrlab-dau/projects/1",
                                    "label": "Board location; status must be read live."}]),
            ],
            "edges": [
                {"from": "visual.realistic", "relation": "supersedes", "to": "visual.flat"},
                {"from": "world", "relation": "verified_by", "to": "receipt"},
                {"from": "manual", "relation": "limits_claims", "to": "world"},
            ],
            "refreshLog": [],
        }

    def digest(self, relative):
        return hashlib.sha256((self.root / relative).read_bytes()).hexdigest()

    def node(self, identifier, kind, status, authority, path="docs/rules.md", **extra):
        node = {"id": identifier, "kind": kind, "title": identifier,
                "summary": "Fixture context description.", "status": status,
                "authority": authority, "topics": ["art", "runtime", "handoff"],
                "observedAt": "2026-09-07", "freshnessPolicy": "content",
                "sources": [{"repoPath": path, "sha256": self.digest(path), "label": "Fixture source"}],
                "limits": ["No runtime acceptance is inferred."]}
        node.update(extra)
        return node

    def errors(self, graph=None):
        return cg.validate_graph(self.graph if graph is None else graph, self.root)

    def test_valid_graph_separates_structure_from_acceptance(self):
        self.assertEqual(self.errors(), [])
        report = cg.inspect_graph(self.graph, self.root)
        self.assertTrue(report["structureValid"])
        self.assertEqual(report["acceptance"], "not_assessed")
        self.assertEqual(report["nodes"]["world"]["sourceState"], "matched")

    def test_duplicate_ids_dangling_edges_and_extra_schema_keys_fail(self):
        for change in (lambda g: g["nodes"].append(copy.deepcopy(g["nodes"][0])),
                       lambda g: g["edges"].append({"from": "world", "relation": "depends_on", "to": "missing"}),
                       lambda g: g["nodes"][0].update({"acceptancePass": True})):
            with self.subTest(change=change):
                graph = copy.deepcopy(self.graph)
                change(graph)
                self.assertTrue(self.errors(graph))

    def test_active_decision_requires_accepted_provenance_and_exclusive_key(self):
        self.graph["nodes"][0]["authority"] = "unverified"
        self.assertTrue(self.errors())
        self.graph["nodes"][0]["authority"] = "user_accepted"
        self.graph["nodes"][1]["status"] = "active"
        self.assertTrue(any("visual_style" in e for e in self.errors()))

    def test_supersession_cycle_is_rejected(self):
        self.graph["edges"].append({"from": "visual.flat", "relation": "supersedes", "to": "visual.realistic"})
        self.assertTrue(any("cycle" in e for e in self.errors()))

    def test_startup_excludes_superseded_rules_but_history_recovers_them(self):
        pack = cg.make_brief(self.graph, self.root, "art", "local")
        self.assertNotIn("visual.flat", [n["id"] for n in pack["nodes"]])
        history = cg.make_brief(self.graph, self.root, "art", "local", history=True)
        self.assertIn("visual.flat", [n["id"] for n in history["nodes"]])

    def test_changed_source_marks_historical_coverage_stale_without_rewriting_receipt(self):
        receipt_before = (self.root / "docs/receipt.json").read_bytes()
        (self.root / "src/world.cs").write_text("world version two\n", encoding="utf-8")
        self.assertEqual(self.errors(), [], "Freshness is separate from schema validity.")
        report = cg.inspect_graph(self.graph, self.root)
        self.assertEqual(report["nodes"]["world"]["sourceState"], "stale")
        coverage = report["nodes"]["receipt"]["coverage"]
        self.assertEqual(coverage["state"], "source_drift")
        self.assertEqual(coverage["changed"], ["src/world.cs"])
        self.assertEqual((self.root / "docs/receipt.json").read_bytes(), receipt_before)

    def test_missing_manual_and_offline_board_do_not_block_synthetic_work(self):
        pack = cg.make_brief(self.graph, self.root, "runtime", "school-pc")
        self.assertEqual(pack["acceptance"], "not_assessed")
        self.assertFalse(pack["syntheticWorkBlocked"])
        board = next(n for n in pack["nodes"] if n["id"] == "board")
        self.assertEqual(board["freshness"]["sourceState"], "live_status_unknown")
        manual = next(n for n in pack["nodes"] if n["id"] == "manual")
        self.assertEqual(manual["status"], "unknown")

    def test_absolute_escape_and_symlink_paths_fail_without_reading_outside_root(self):
        for path in ("/Users/example/private.md", "../private.md", "C:\\private\\note.md", ".env", ".git/config"):
            with self.subTest(path=path):
                graph = copy.deepcopy(self.graph)
                graph["nodes"][0]["sources"][0]["repoPath"] = path
                self.assertTrue(self.errors(graph))
        with tempfile.TemporaryDirectory() as outside:
            (self.root / "docs/outside").symlink_to(Path(outside), target_is_directory=True)
            graph = copy.deepcopy(self.graph)
            graph["nodes"][0]["sources"][0]["repoPath"] = "docs/outside/source.md"
            self.assertTrue(self.errors(graph))

    def test_refresh_requires_conscious_review_and_never_updates_evidence_coverage(self):
        (self.root / "src/world.cs").write_text("reviewed next version\n", encoding="utf-8")
        before = copy.deepcopy(self.graph)
        with self.assertRaises(ValueError):
            cg.refresh_sources(self.graph, self.root, ["world"], reviewed=False, reason="reviewed source change")
        with self.assertRaises(ValueError):
            cg.refresh_sources(self.graph, self.root, ["receipt"], reviewed=True, reason="reviewed source change")
        updated = cg.refresh_sources(self.graph, self.root, ["world"], reviewed=True, reason="reviewed source change")
        self.assertEqual(self.graph, before, "Refresh returns a separate graph until the CLI writes it.")
        self.assertEqual(updated["nodes"][2]["sources"][0]["sha256"], self.digest("src/world.cs"))
        self.assertEqual(updated["nodes"][3], before["nodes"][3])
        self.assertEqual(cg.inspect_graph(updated, self.root)["nodes"]["receipt"]["coverage"]["state"], "source_drift")

    def test_duplicate_json_keys_and_nonfinite_values_fail(self):
        path = self.root / "bad.json"
        for raw in ('{"schemaVersion":1,"schemaVersion":2}', '{"value":NaN}'):
            path.write_text(raw, encoding="utf-8")
            with self.assertRaises(ValueError):
                cg.load_json(path)

    def test_public_source_query_parameters_are_allowed_but_credentials_are_not(self):
        source = self.graph["nodes"][-1]["sources"][0]
        source["url"] = "https://www.korail.com/ticket/train/stationGuide/station/view?stationSeq=10279506"
        self.assertEqual(self.errors(), [])
        source["url"] = "https://example.test/source?access_token=example"
        self.assertTrue(self.errors())

    def test_repository_graph_is_structurally_valid_without_requiring_fresh_historical_coverage(self):
        graph = cg.load_json(cg.ROOT / cg.GRAPH_PATH)
        self.assertEqual(cg.validate_graph(graph, cg.ROOT), [])
        packet = cg.make_brief(graph, cg.ROOT, "art", "local")
        self.assertIn("decision.visual.realistic", [node["id"] for node in packet["nodes"]])
        self.assertNotIn("decision.visual.flat", [node["id"] for node in packet["nodes"]])

    def test_official_mcp_receipt_binding_preserves_historical_coverage(self):
        graph = cg.load_json(cg.ROOT / cg.GRAPH_PATH)
        node = next(n for n in graph["nodes"]
                    if n["id"] == "evidence.official_editor_mcp_20260908")
        # The published receipt is immutable; correcting its index must not renew tests.
        receipt = cg.ROOT / node["coverage"]["receipt"]
        self.assertEqual(hashlib.sha256(receipt.read_bytes()).hexdigest(),
                         "2df11f5582610c7766416ef6c06c3cf76192278eca1377d6bb2bab34c895b4a0")
        self.assertEqual(cg.assess_node(node, cg.ROOT)["sourceState"], "matched")
        self.assertEqual(cg.assess_node(node, cg.ROOT)["acceptance"], "not_assessed")
        for identifier in ("evidence.official_editor_mcp_20260908",
                           "decision.tooling.official_unity_mcp"):
            entry = next(n for n in graph["nodes"] if n["id"] == identifier)
            self.assertNotIn("??", json.dumps(entry, ensure_ascii=False))

    def test_offline_html_does_not_execute_graph_text_or_fetch_external_dependencies(self):
        self.graph["nodes"][0]["summary"] = '</script><img src=x onerror="alert(1)">'
        document = cg.render_html(self.graph, self.root)
        self.assertNotIn('</script><img', document)
        self.assertNotIn('fetch(', document)
        self.assertNotIn('<script src=', document)
        self.assertIn("offline snapshot", document)
        self.assertIn("textContent", document)

    def test_real_project_briefs_start_at_the_requested_work_surface(self):
        graph = cg.load_json(cg.ROOT / cg.GRAPH_PATH)
        expected = {"art": "foundation/art/object-references.json",
                    "runtime": "Packages/com.xrlab.chooguard.foundation/Demo/Runtime/StationWorldSession.cs",
                    "handoff": "docs/choo-guard-foundation-handoff.md"}
        for topic, path in expected.items():
            self.assertEqual(cg.make_brief(graph, cg.ROOT, topic, "local")["readNext"][0], path)

    def test_all_briefs_are_bounded_and_portable(self):
        for topic in ("art", "runtime", "handoff"):
            for machine in ("local", "school-pc"):
                pack = cg.make_brief(self.graph, self.root, topic, machine)
                self.assertLessEqual(len(pack["readNext"]), 3)
                self.assertNotIn(str(self.root), json.dumps(pack))
                self.assertNotIn("visual.flat", [n["id"] for n in pack["nodes"]])


if __name__ == "__main__":
    unittest.main()
