"""Small transition regressions; no Unity or legacy gate execution."""
import copy
import importlib.util
import json
import subprocess
import sys
import unittest
from pathlib import Path
from unittest.mock import patch

REPO = Path(__file__).resolve().parents[3]


class EntryPointTests(unittest.TestCase):
    def test_prompts_use_single_active_story(self):
        names = ["README.md", "00-context-audit-max.md", "01-plan-story-max.md",
                 "02-implement-story-low.md", "03-review-story-max.md",
                 "04-repair-story.md", "05-accept-refresh-next.md"]
        for name in names:
            with self.subTest(prompt=name):
                text = (REPO / "prompts" / name).read_text(encoding="utf8")
                self.assertIn("CS-EXEC.01.01", text)
                self.assertIn("CHOOGuard_Story_Plan_v5", text)
                self.assertNotIn("STORY_ID = CS-BOOT.01.01", text)


class ActivePlanTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = REPO / "docs/CHOOGuard_Story_Plan_v5"
        spec = importlib.util.spec_from_file_location("active_plan", cls.root / "tools/active_plan.py")
        cls.tool = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.tool)
        cls.plan = cls.tool.read_json(cls.root / "plan.json")
        cls.state = cls.tool.read_json(cls.root / "state/progress.json")

    def validate(self, plan=None, state=None):
        return self.tool.validate(plan if plan is not None else self.plan,
                                  state if state is not None else self.state, REPO)

    def test_current_state_and_all_original_contracts_are_referenced(self):
        import planlib
        with patch.object(planlib, "validate_plan", side_effect=AssertionError("legacy gate")), \
             patch.object(planlib, "phase_graph", side_effect=AssertionError("legacy graph")), \
             patch.object(planlib, "readiness", side_effect=AssertionError("legacy readiness")):
            self.assertEqual(self.validate(), [])
        self.assertEqual(len(self.plan["activeStories"]), 1)
        self.assertEqual(self.state["status"], "IN_PROGRESS")
        self.assertEqual(self.state["acceptanceStatus"], "NOT_ACCEPTED")
        legacy = self.tool.read_json(REPO / self.plan["legacy"]["planPath"])
        resolved = self.tool.referenced_stories(self.plan, REPO)
        self.assertEqual(len(resolved), 109)
        self.assertCountEqual([s["id"] for s in resolved], [s["id"] for s in legacy["stories"]])
        # Whole original objects, not summaries: includes every requirement, AC/AT,
        # output, write path, dependency, and technical/source reference.
        original = {s["id"]: s for s in legacy["stories"]}
        for story in resolved:
            self.assertEqual(story, original[story["id"]])

    def test_missing_duplicate_and_wrong_area_references_fail(self):
        for mutation in ("missing", "duplicate", "wrong-area"):
            with self.subTest(mutation=mutation):
                plan = copy.deepcopy(self.plan)
                ids = plan["functionalAreas"][0]["legacyStoryIds"]
                if mutation == "missing":
                    ids.pop()
                elif mutation == "duplicate":
                    ids.append(ids[0])
                else:
                    other = plan["functionalAreas"][1]["legacyStoryIds"]
                    ids[0], other[0] = other[0], ids[0]
                self.assertTrue(self.validate(plan=plan))

    def test_second_story_unknown_fields_and_bad_shapes_fail(self):
        for mutate in (
            lambda p: p["activeStories"].append({"id": "CS-OTHER", "title": "extra"}),
            lambda p: p.update(scheduler={}),
            lambda p: p.update(functionalAreas="bad"),
            lambda p: p.update(version=True),
            lambda p: p["legacy"].update(planDigest="0" * 64),
        ):
            plan = copy.deepcopy(self.plan)
            mutate(plan)
            self.assertTrue(self.validate(plan=plan))
        for value in (None, [], "plan"):
            self.assertTrue(self.tool.validate(value, self.state, REPO))

    def test_invalid_status_path_and_progress_shape_fail(self):
        for mutate in (
            lambda s: s.update(status="PASSED"),
            lambda s: s.update(acceptanceStatus=[]),
            lambda s: s["areas"][0].update(evidenceRefs=["../outside"]),
            lambda s: s["areas"][0].update(evidenceRefs=["/etc/passwd"]),
            lambda s: s["areas"][0].update(evidenceRefs=["missing-evidence.json"]),
            lambda s: s.update(areas=[]),
            lambda s: s["issues"][0].update(requiredForAcceptance="false"),
            lambda s: s["tests"][0].update(evidenceRef=None),
        ):
            state = copy.deepcopy(self.state)
            mutate(state)
            self.assertTrue(self.validate(state=state))

    def test_bootstrap_only_cannot_complete_or_accept_project(self):
        for status, acceptance in (("COMPLETED", "NOT_ACCEPTED"),
                                   ("IN_PROGRESS", "ACCEPTED"),
                                   ("COMPLETED", "ACCEPTED")):
            state = copy.deepcopy(self.state)
            state.update(status=status, acceptanceStatus=acceptance)
            state["areas"][0]["status"] = "COMPLETED"
            state["acceptanceRef"] = state["areas"][0]["evidenceRefs"][0]
            self.assertTrue(self.validate(state=state))

    def test_future_progress_is_not_frozen(self):
        state = copy.deepcopy(self.state)
        state["areas"][1].update(status="IN_PROGRESS", summary="후속 구현 시작")
        self.assertEqual(self.validate(state=state), [])
        # Structural fixture only, not a product acceptance claim. Use this test file
        # as an existing path to prove future completion is possible without receipts.
        ref = str(Path(__file__).relative_to(REPO))
        for area in state["areas"]:
            area.update(status="COMPLETED", evidenceRefs=[ref])
        for issue in state["issues"]:
            issue.update(status="RESOLVED", evidenceRefs=[ref])
        state.update(status="COMPLETED", acceptanceStatus="ACCEPTED", acceptanceRef=ref)
        self.assertEqual(self.validate(state=state), [])
        state["issues"][0]["status"] = "OPEN"
        self.assertTrue(self.validate(state=state))

    def test_bootstrap_history_and_test_evidence_remain_scoped(self):
        boot = next(a for a in self.state["areas"] if a["areaId"] == "CS-BOOT")
        self.assertEqual(boot["status"], "PARTIAL")
        self.assertEqual(self.state["acceptanceStatus"], "NOT_ACCEPTED")
        self.assertIsNone(self.state["acceptanceRef"])
        evidence_root = "docs/build/evidence/CS-BOOT.01.01/"
        run = evidence_root + "20260920T023553Z-09c7d4/"
        expected = [
            (run + "editmode-final/results.xml", "PASS",
             "CS-BOOT.01.01 only; 과거 로컬 실행 재사용, 관리 전환에서 재실행 안 함"),
            (run + "playmode-final/results.xml", "PASS",
             "CS-BOOT.01.01 only; 과거 로컬 실행 재사용"),
            (run + "baseline-final-validation.json", "PASS",
             "CS-BOOT.01.01 only; 구조 검사이지 Windows 제품 수용 아님"),
            (evidence_root + "20260920T0252168282370Z-0f57d6/build-receipt.json",
             "NOT_RUN", "CS-BOOT.01.01 only"),
        ]
        historical_refs = {ref for ref, _, _ in expected}

        def assert_bootstrap_history(tests):
            actual = [(t["evidenceRef"], t["result"], t["scope"]) for t in tests
                      if t["evidenceRef"] in historical_refs]
            self.assertCountEqual(actual, expected)

        # Later areas and test runs may progress without rewriting these receipts.
        assert_bootstrap_history(self.state["tests"])
        for ref in sorted(historical_refs):
            with self.subTest(widened_evidence=ref):
                tests = copy.deepcopy(self.state["tests"])
                historical = next(t for t in tests if t["evidenceRef"] == ref)
                historical["scope"] += "; CS-PACK 및 전체 제품 수용 포함"
                with self.assertRaises(AssertionError):
                    assert_bootstrap_history(tests)
        self.assertIn("docs/CHOOGuard_Story_Plan_v4/state/progress.json", self.plan["historyRefs"])

    def test_cli_validate_brief_and_render_check(self):
        for args in (("validate",), ("brief",), ("render", "--check")):
            result = subprocess.run([sys.executable, "-B", str(self.root / "tools/active_plan.py"), *args],
                                    capture_output=True, text=True, timeout=15)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            if args[0] == "brief":
                data = json.loads(result.stdout)
                self.assertEqual(data["storyId"], "CS-EXEC.01.01")
                self.assertFalse(data["executionAuthorized"])
                self.assertFalse(data["productAcceptanceVerified"])
        expected = self.tool.render(self.plan, self.state)
        self.assertEqual((self.root / "ACTIVE_STORY.md").read_text(encoding="utf8"), expected)


if __name__ == "__main__":
    unittest.main()
