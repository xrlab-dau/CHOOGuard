"""Contract boundary tests for the public, synthetic foundation fixture."""

import copy
import importlib.util
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


UNSET = object()
SCRIPT = Path(__file__).resolve().parents[1] / "validate.py"
ROOT = SCRIPT.parents[2]
SPEC = importlib.util.spec_from_file_location("foundation_validate", SCRIPT)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader
SPEC.loader.exec_module(MODULE)


class FoundationValidationTests(unittest.TestCase):
    def setUp(self):
        self.scenario = MODULE.load_json(ROOT / "foundation/scenarios/foundation-demo.json")
        self.anchors = MODULE.load_json(ROOT / "foundation/anchors/synthetic-room.json")
        self.schema = MODULE.load_json(ROOT / "schemas/foundation-scenario.schema.json")

    def reject(self, scenario=UNSET, anchors=UNSET, schema=UNSET, contains=None):
        errors = MODULE.validate_bundle(
            self.scenario if scenario is UNSET else scenario,
            self.anchors if anchors is UNSET else anchors,
            self.schema if schema is UNSET else schema,
        )
        self.assertTrue(errors, "invalid fixture unexpectedly accepted")
        if contains:
            self.assertIn(contains, "\n".join(errors))

    def test_tracked_sample_is_valid(self):
        self.assertEqual([], MODULE.validate_bundle(self.scenario, self.anchors, self.schema))

    def test_all_five_roles_can_be_representative(self):
        for index in range(1, 6):
            with self.subTest(index=index):
                self.scenario["representativeRoleId"] = f"role-{index:02d}"
                self.assertEqual([], MODULE.validate_bundle(self.scenario, self.anchors, self.schema))

    def test_each_role_has_all_four_other_team_roles(self):
        roles = {role["roleId"] for role in self.scenario["roles"]}
        for role in self.scenario["roles"]:
            self.assertEqual(roles - {role["roleId"]}, {event["roleId"] for event in role["expectedVirtualTeamEvents"]})

    def test_disclaimer_and_provisional_are_not_optional(self):
        for key, value in (("disclaimer", "검증 완료"), ("provisional", False), ("provisional", 1)):
            with self.subTest(key=key, value=value):
                changed = copy.deepcopy(self.scenario)
                changed[key] = value
                self.reject(scenario=changed, contains=key)

    def test_rejects_unsupported_score_and_procedure_fields(self):
        for key in ("score", "passFail", "procedure", "coordinates", "assetPath"):
            with self.subTest(key=key):
                changed = copy.deepcopy(self.scenario)
                changed["roles"][0][key] = "unsupported"
                self.reject(scenario=changed, contains=key)

    def test_rejects_extra_top_level_and_event_fields(self):
        self.scenario["unexpected"] = True
        self.reject(contains="unexpected")
        del self.scenario["unexpected"]
        self.scenario["roles"][0]["expectedVirtualTeamEvents"][0]["score"] = 100
        self.reject(contains="score")

    def test_requires_every_contract_field(self):
        for location in (self.scenario, self.scenario["roles"][0], self.scenario["roles"][0]["expectedVirtualTeamEvents"][0]):
            for field in list(location):
                with self.subTest(field=field):
                    value = location.pop(field)
                    self.reject(contains=field)
                    location[field] = value

    def test_rejects_duplicates_across_role_action_anchor_ids(self):
        for field in ("roleId", "actionId", "targetAnchorId"):
            with self.subTest(field=field):
                changed = copy.deepcopy(self.scenario)
                changed["roles"][1][field] = changed["roles"][0][field]
                self.reject(scenario=changed, contains="duplicate")

    def test_rejects_unknown_representative_and_anchor(self):
        self.scenario["representativeRoleId"] = "role-99"
        self.reject(contains="representativeRoleId")
        self.scenario["representativeRoleId"] = "role-01"
        self.scenario["roles"][0]["targetAnchorId"] = "anchor-99"
        self.reject(contains="targetAnchorId")

    def test_rejects_missing_or_extra_role(self):
        for count in (0, 4, 6):
            with self.subTest(count=count):
                changed = copy.deepcopy(self.scenario)
                changed["roles"] = (changed["roles"] * 2)[:count]
                self.reject(scenario=changed, contains="roles")

    def test_rejects_unknown_self_duplicate_or_missing_team_event(self):
        for replacement in ("role-99", "role-01", "role-03"):
            with self.subTest(replacement=replacement):
                changed = copy.deepcopy(self.scenario)
                changed["roles"][0]["expectedVirtualTeamEvents"][0]["roleId"] = replacement
                self.reject(scenario=changed, contains="expectedVirtualTeamEvents")
        self.scenario["roles"][0]["expectedVirtualTeamEvents"].pop()
        self.reject(contains="expectedVirtualTeamEvents")

    def test_rejects_empty_or_whitespace_semantics(self):
        for field in ("temporaryDisplayName", "briefing", "expectedFeedbackCode", "feedbackText"):
            for value in ("", " \t\n"):
                with self.subTest(field=field, value=value):
                    changed = copy.deepcopy(self.scenario)
                    changed["roles"][0][field] = value
                    self.reject(scenario=changed, contains=field)

    def test_rejects_unsupported_event_or_completion_state(self):
        for field, value in (("eventCode", "real-railway-command"), ("state", "approved")):
            changed = copy.deepcopy(self.scenario)
            changed["roles"][0]["expectedVirtualTeamEvents"][0][field] = value
            self.reject(scenario=changed, contains=field)
        self.scenario["roles"][0]["expectedQuestState"] = "approved"
        self.reject(contains="expectedQuestState")

    def test_virtual_team_events_only_notify_without_completing_other_quests(self):
        for role in self.scenario["roles"]:
            self.assertEqual("completed", role["expectedQuestState"])
            for event in role["expectedVirtualTeamEvents"]:
                self.assertEqual("synthetic-action-observed", event["eventCode"])
                self.assertEqual("notified", event["state"])

    def test_rejects_legacy_team_completion_events(self):
        for field, value in (("state", "completed"), ("eventCode", "synthetic-action-completed")):
            with self.subTest(field=field):
                changed = copy.deepcopy(self.scenario)
                changed["roles"][0]["expectedVirtualTeamEvents"][0][field] = value
                self.reject(scenario=changed, contains=field)

    def test_rejects_wrong_types_without_crashing(self):
        for field, value in (("roles", {}), ("roles", None), ("mapId", 1), ("representativeRoleId", [])):
            changed = copy.deepcopy(self.scenario)
            changed[field] = value
            self.reject(scenario=changed, contains=field)
        for value in (None, [], "", 1, False):
            self.reject(scenario=value)
        for field, value in (("actionId", []), ("expectedVirtualTeamEvents", {}), ("briefing", False)):
            changed = copy.deepcopy(self.scenario)
            changed["roles"][0][field] = value
            self.reject(scenario=changed, contains=field)

    def test_rejects_anchor_catalog_duplicates_unknown_fields_and_map_mismatch(self):
        self.anchors["anchors"][1]["anchorId"] = "anchor-01"
        self.reject(contains="duplicate")
        self.anchors["anchors"][1]["anchorId"] = "anchor-02"
        self.anchors["anchors"][0]["position"] = [0, 0, 0]
        self.reject(contains="position")
        del self.anchors["anchors"][0]["position"]
        self.anchors["mapId"] = "real-station"
        self.reject(contains="mapId")

    def test_rejects_non_synthetic_anchor_catalog(self):
        for field, value in (("provisional", False), ("disclaimer", ""), ("dataClassification", "restricted")):
            changed = copy.deepcopy(self.anchors)
            changed[field] = value
            self.reject(anchors=changed, contains=field)

    def test_schema_rules_are_executed(self):
        self.schema["properties"]["scenarioId"]["const"] = "different-synthetic-fixture"
        self.reject(contains="scenarioId")

    def test_unsupported_schema_keyword_fails_closed(self):
        self.schema["properties"]["roles"]["unevaluatedItems"] = False
        self.reject(contains="unsupported schema keyword")

    def test_malformed_schema_rules_fail_without_crashing(self):
        for field, value in (("type", []), ("type", None), ("properties", []), ("required", 1), ("minItems", True), ("pattern", "[")):
            with self.subTest(field=field):
                changed = copy.deepcopy(self.schema)
                changed[field] = value
                self.reject(schema=changed, contains="schema")

    def test_invalid_anchor_types_and_missing_metadata_fail_without_crashing(self):
        for value in (None, [], 1, False):
            self.reject(anchors=value, contains="anchors")
        for field in self.anchors:
            changed = copy.deepcopy(self.anchors)
            del changed[field]
            self.reject(anchors=changed, contains=field)

    def test_rejects_missing_in_range_references(self):
        self.scenario["representativeRoleId"] = "role-02"
        self.scenario["roles"][1]["roleId"] = "role-01"
        self.reject(contains="representativeRoleId: unknown role")
        self.anchors["anchors"][1]["anchorId"] = "anchor-01"
        self.reject(contains="targetAnchorId: unknown anchor")

    def test_does_not_resolve_schema_references(self):
        self.schema["$ref"] = "https://invalid.example/schema.json"
        self.reject(contains="unsupported schema keyword")

    def test_load_json_rejects_malformed_duplicate_nonfinite_and_non_utf8_inputs(self):
        for content in (b'{"broken":', b'{"x":1,"x":2}', b'{"x":NaN}', b'\xff'):
            with self.subTest(content=content), tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / "fixture.json"
                path.write_bytes(content)
                with self.assertRaises(MODULE.ValidationInputError):
                    MODULE.load_json(path)

    def test_load_json_rejects_oversized_inputs(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "fixture.json"
            path.write_bytes(b" " * (MODULE.MAX_JSON_BYTES + 1))
            with self.assertRaisesRegex(MODULE.ValidationInputError, "exceeds"):
                MODULE.load_json(path)

    def test_repository_reader_rejects_symlinks_outside_repository(self):
        with tempfile.TemporaryDirectory() as directory, tempfile.TemporaryDirectory() as external:
            root = Path(directory).resolve()
            outside = Path(external) / "private.json"
            outside.write_text("{}", encoding="utf-8")
            link = root / "fixture.json"
            try:
                link.symlink_to(outside)
            except OSError as error:
                self.skipTest(f"symlinks unavailable on this system: {error}")
            previous_root = MODULE.ROOT
            try:
                MODULE.ROOT = root
                with self.assertRaisesRegex(MODULE.ValidationInputError, "inside repository"):
                    MODULE._read_repository_json("fixture.json")
            finally:
                MODULE.ROOT = previous_root

    def test_cli_validates_fixed_repository_paths_from_other_working_directory(self):
        with tempfile.TemporaryDirectory() as directory:
            result = subprocess.run([sys.executable, str(SCRIPT)], cwd=directory, capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("synthetic foundation fixture", result.stdout)

    def test_cli_success_status_is_ascii_on_korean_windows_console(self):
        environment = dict(os.environ, PYTHONIOENCODING="cp949")
        result = subprocess.run(
            [sys.executable, str(SCRIPT)], env=environment,
            capture_output=True, text=True, encoding="cp949",
        )
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertTrue(result.stdout.isascii(), result.stdout)
        self.assertIn("PASS: synthetic foundation fixture", result.stdout)

    def test_cli_does_not_accept_arbitrary_paths(self):
        result = subprocess.run([sys.executable, str(SCRIPT), "../../private.json"], capture_output=True, text=True)
        self.assertNotEqual(0, result.returncode)
        self.assertNotIn("Traceback", result.stderr)

    def test_cli_reports_bad_json_without_traceback(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            script = root / "scripts/foundation/validate.py"
            script.parent.mkdir(parents=True)
            script.write_text(SCRIPT.read_text(encoding="utf-8"), encoding="utf-8")
            scenario = root / "foundation/scenarios/foundation-demo.json"
            scenario.parent.mkdir(parents=True)
            scenario.write_text('{"broken":', encoding="utf-8")
            result = subprocess.run([sys.executable, str(script)], capture_output=True, text=True)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("FAIL", result.stderr)
        self.assertNotIn("Traceback", result.stderr)


if __name__ == "__main__":
    unittest.main()
