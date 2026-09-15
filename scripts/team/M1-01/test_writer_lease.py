"""Tests for the M1-01 candidate session contract and simulated Writer Lease registry (temporary directories only)."""
import contextlib
import copy
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import writer_lease as wl  # noqa: E402

CONTRACT = wl.read_json(wl.CONTRACT)
PROFILE = wl.read_json(wl.PROFILE)
ALLOWLIST = wl.read_json(wl.ALLOWLIST)


def session(**overrides):
    value = {"sessionId": "session-17-a", "workId": "M1-01", "issueNumber": 17, "phase": "candidate", "baseRef": "15ac727f63830d6f70e87f6d2dc54c7d724e22c4",
             "writeScope": ["docs/team/M1-01/", "scripts/team/M1-01/"], "role": "role:authoring-agent", "tools": ["R-02", "M-01"],
             "model": {"provider": "anthropic", "modelId": "anthropic/claude-opus-5"}, "acceptance": ["docs/context/work-orders/017.json#/acceptance/0"],
             "evidencePaths": ["docs/team/M1-01/session-contract.json"]}
    value.update(overrides)
    return value


class RegistryCase(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="m1-01-test-")
        self.registry = wl.LeaseRegistry(CONTRACT, Path(self.tmp.name) / "journal.jsonl")

    def tearDown(self):
        self.tmp.cleanup()

    def acquire(self, holder, resource, minutes=0, ttl=30, base=wl.BASE_A, sha=wl.SHA_X):
        return self.registry.acquire(holder, resource, base, wl.at(minutes), wl.at(minutes + ttl), sha)


class ContractInspection(unittest.TestCase):
    def test_committed_contract_passes_inspection(self):
        report = wl.inspect_contract(CONTRACT)
        self.assertTrue(report["ok"], report["problems"])
        self.assertEqual({t["event"] for t in report["transitionTable"]}, wl.REQUIRED_EVENTS)

    def test_mutated_contracts_are_reported_and_refused_by_registry(self):
        mutations = {
            "session_required_missing:baseRef": lambda c: c["sessionInput"]["required"].__delitem__(4),
            "missing_expiry_limit": lambda c: c["lease"]["limits"].pop("maxTtlSeconds"),
            "duplicate_rejection_record_incomplete": lambda c: c["transitions"][0]["onReject"]["record"].remove("conflictingHolder"),
            "reject_does_not_preserve_holder_base:T-write": lambda c: c["transitions"][2]["onReject"]["preserves"].remove("baseRef"),
            "blocking_state_missing": lambda c: c["lease"]["blockingStates"].remove("crash_suspected"),
            "recover_mismatch_not_failed": lambda c: c["transitions"][7]["onReject"].__setitem__("to", "recovered"),
            "orchestrator_required": lambda c: c["sessionInput"]["required"].append({"field": "orchestrator"}),
            "named_assignment_not_forbidden": lambda c: c["sessionInput"]["forbiddenFields"].remove("assignee"),
        }
        for problem, mutate in mutations.items():
            with self.subTest(problem=problem):
                mutated = copy.deepcopy(CONTRACT)
                mutate(mutated)
                self.assertIn(problem, wl.inspect_contract(mutated)["problems"])
                with self.assertRaises(wl.InputError):
                    wl.LeaseRegistry(mutated, Path(tempfile.gettempdir()) / "never-written.jsonl")


class SessionInput(unittest.TestCase):
    def validate(self, value):
        return wl.validate_session(CONTRACT, PROFILE, ALLOWLIST, value)

    def test_complete_session_is_accepted_with_or_without_orchestrator(self):
        self.assertEqual(self.validate(session()), [])
        self.assertEqual(self.validate(session(orchestrator="pi")), [])

    def test_each_missing_required_field_is_refused(self):
        for field in sorted(wl.REQUIRED_SESSION):
            with self.subTest(field=field):
                value = session()
                del value[field]
                self.assertIn("missing:" + field, self.validate(value))

    def test_invalid_values_are_refused(self):
        cases = {
            "named_assignment_field:assignee": session(assignee="someone"),
            "invalid:baseRef": session(baseRef="15ac727"),
            "unsafe_repo_path": session(writeScope=["../outside/"], evidencePaths=["../outside/x.json"]),
            "unknown_role": session(role="role:unknown"),
            "unknown_tool_row:Z-99": session(tools=["R-02", "Z-99"]),
            "model_outside_role_scope": session(role="role:independent-reviewer"),
            "model_for_non_model_role": session(role="role:pm"),
            "evidence_outside_write_scope:docs/team/M1-02/x.json": session(evidencePaths=["docs/team/M1-02/x.json"]),
            "invalid:phase": session(phase="merge"),
            "unknown_orchestrator": session(orchestrator="cron"),
        }
        for problem, value in cases.items():
            with self.subTest(problem=problem):
                self.assertIn(problem, self.validate(value))
        self.assertEqual(self.validate(session(role="role:pm", model={"provider": "none", "modelId": "none"})), [])

    def test_contract_without_enumerated_values_reports_a_problem_instead_of_raising(self):
        broken = copy.deepcopy(CONTRACT)
        for field in broken["sessionInput"]["required"] + broken["sessionInput"]["optional"]:
            field.pop("values", None)
        problems = wl.validate_session(broken, PROFILE, ALLOWLIST, session())
        self.assertIn("contract_missing_values:phase", problems)
        self.assertIn("contract_missing_values:orchestrator", problems)


class Overlap(RegistryCase):
    def test_spellings_of_one_file_on_a_case_insensitive_filesystem_overlap(self):
        held = wl.wf("Assets/Scenes/Main.unity")
        self.assertTrue(self.acquire("session-a", held)["ok"])
        for other in ("assets/scenes/main.unity", "ASSETS\\Scenes\\Main.unity", "Assets/Scenes/Main.unity.", "Assets/Scenes /Main.unity", "assets/SCENES/",
                      "Assets/Scenes/Foo/../Main.unity", "Assets//Scenes//Main.unity", "Assets/./Scenes/Main.unity", "Assets/.../Scenes/Main.unity"):
            with self.subTest(path=other):
                result = self.acquire("session-b", wl.wf(other), base=wl.BASE_B)
                self.assertEqual((result["ok"], result["reason"], result["details"]["conflictingHolder"]), (False, "overlap_with_active", "session-a"))
        self.assertEqual(wl.resource_key(wl.wf("assets/scenes/main.unity")), wl.resource_key(held))
        self.assertTrue(self.acquire("session-b", wl.wf("Assets/Scenes/Main.unity.meta"))["ok"])

    def test_workspace_id_case_and_root_escaping_paths(self):
        self.assertTrue(self.acquire("session-a", {"kind": "unity-project", "workspaceId": "ws-1"})["ok"])
        refused = self.acquire("session-b", {"kind": "unity-project", "workspaceId": "WS-1"}, base=wl.BASE_B)
        self.assertEqual(refused["reason"], "overlap_with_active")
        for path in ("../outside.txt", "Assets/../../outside.txt", "/etc/passwd", "C:/escape.txt", "", "./"):
            with self.subTest(path=path):
                result = self.acquire("session-c", wl.wf(path, "ws-9"))
                self.assertEqual((result["ok"], result["reason"]), (False, "lease_fields_unrecordable"))
        self.assertEqual(len(self.registry.leases), 1)

    def assert_refused_with_holder(self, result, holder, base, scope):
        self.assertFalse(result["ok"])
        self.assertEqual(result["reason"], "overlap_with_active")
        details = result["details"]
        self.assertEqual((details["conflictingHolder"], details["conflictingBaseRef"], details["conflictingScope"]), (holder, base, scope))

    def test_same_path_parent_and_child_are_refused_with_existing_holder_recorded(self):
        held = wl.wf("Assets/Scenes/Main")
        self.assertTrue(self.acquire("session-a", held)["ok"])
        for other in (wl.wf("Assets/Scenes/Main"), wl.wf("Assets/Scenes/"), wl.wf("Assets/Scenes/Main/Lighting.asset")):
            with self.subTest(path=other["path"]):
                self.assert_refused_with_holder(self.acquire("session-b", other, base=wl.BASE_B), "session-a", wl.BASE_A, held)
        self.assertTrue(self.acquire("session-b", wl.wf("Assets/Scenes/MainMenu.unity"))["ok"])
        self.assertTrue(self.acquire("session-b", wl.wf("Assets/Scenes/Main", "ws-2"))["ok"])

    def test_unity_project_generated_output_and_settings_overlap(self):
        project = {"kind": "unity-project", "workspaceId": "ws-1"}
        self.assertTrue(self.acquire("session-a", project)["ok"])
        for other in ({"kind": "unity-project", "workspaceId": "ws-1"}, {"kind": "generated-output", "workspaceId": "ws-1", "root": "Assets/CHOOguardGenerated"},
                      {"kind": "project-settings", "workspaceId": "ws-1"}):
            with self.subTest(kind=other["kind"]):
                self.assert_refused_with_holder(self.acquire("session-b", other, base=wl.BASE_B), "session-a", wl.BASE_A, project)
        self.assertTrue(self.acquire("session-b", {"kind": "unity-project", "workspaceId": "ws-2"})["ok"])

    def test_generated_output_blocks_files_under_and_above_its_root(self):
        root = {"kind": "generated-output", "workspaceId": "ws-1", "root": "Assets/CHOOguardGenerated"}
        self.assertTrue(self.acquire("session-a", root)["ok"])
        self.assert_refused_with_holder(self.acquire("session-b", wl.wf("Assets/CHOOguardGenerated/a/b.prefab")), "session-a", wl.BASE_A, root)
        self.assert_refused_with_holder(self.acquire("session-b", wl.wf("Assets/")), "session-a", wl.BASE_A, root)
        self.assertTrue(self.acquire("session-b", wl.wf("Assets/CHOOguardGeneratedNotes.md"))["ok"])

    def test_unrecordable_or_unbounded_lease_is_not_issued(self):
        cases = [("", wl.BASE_A, 30, wl.SHA_X, "lease_fields_unrecordable"), ("session-a", None, 30, wl.SHA_X, "lease_fields_unrecordable"),
                 ("session-a", wl.BASE_A, 30, "", "lease_fields_unrecordable"), ("session-a", wl.BASE_A, 61, wl.SHA_X, "expiry_out_of_bounds"),
                 ("session-a", wl.BASE_A, 0, wl.SHA_X, "expiry_out_of_bounds")]
        for holder, base, ttl, sha, reason in cases:
            with self.subTest(reason=reason, ttl=ttl):
                result = self.registry.acquire(holder, wl.wf("docs/a.md"), base, wl.at(0), wl.at(ttl), sha)
                self.assertEqual((result["ok"], result["reason"]), (False, reason))
        missing_expiry = self.registry.acquire("session-a", wl.wf("docs/a.md"), wl.BASE_A, wl.at(0), None, wl.SHA_X)
        self.assertEqual(missing_expiry["reason"], "lease_fields_unrecordable")
        self.assertEqual(self.registry.leases, {})


class RenewWriteExpiry(RegistryCase):
    def test_every_write_refusal_records_caller_fence_and_path(self):
        lid = self.acquire("session-a", wl.wf("docs/team/M1-01/"), ttl=20)["lease"]["leaseId"]
        attempts = [("session-b", 1, "docs/team/M1-01/a.json", 5), ("session-a", 7, "docs/team/M1-01/b.json", 5),
                    ("session-a", 1, "docs/team/M1-02/c.json", 5), ("session-a", 1, "docs/team/M1-01/d.json", 25)]
        for caller, fence, path, minute in attempts:
            self.registry.write(lid, caller, fence, path, wl.at(minute), wl.SHA_Y)
        self.registry.expire_due(wl.at(30))
        self.registry.write(lid, "session-a", 1, "docs/team/M1-01/e.json", wl.at(31), wl.SHA_Y)
        refusals = [e for e in self.registry.journal() if e["event"] == "write" and e["result"] == "rejected"]
        self.assertEqual([e["reason"] for e in refusals], ["caller_not_holder", "stale_fence", "path_outside_lease", "lease_expired", "transition_not_in_contract"])
        for entry in refusals:
            with self.subTest(reason=entry["reason"]):
                self.assertTrue({"caller", "presentedFence", "path"} <= set(entry["details"]), entry["details"])

    def test_renew_and_write_guards(self):
        lid = self.acquire("session-a", wl.wf("docs/team/M1-01/"), ttl=20)["lease"]["leaseId"]
        self.assertEqual(self.registry.renew(lid, "session-b", 1, wl.at(5), wl.at(25))["reason"], "caller_not_holder")
        self.assertEqual(self.registry.write(lid, "session-a", 0, "docs/team/M1-01/x.json", wl.at(5), wl.SHA_Y)["reason"], "stale_fence")
        self.assertEqual(self.registry.write(lid, "session-a", 1, "docs/team/M1-02/x.json", wl.at(5), wl.SHA_Y)["reason"], "path_outside_lease")
        self.assertEqual(self.registry.renew(lid, "session-a", 1, wl.at(5), wl.at(70))["reason"], "expiry_out_of_bounds")
        self.assertTrue(self.registry.write(lid, "session-a", 1, "docs/team/M1-01/x.json", wl.at(5), wl.SHA_Y)["ok"])
        self.assertTrue(self.registry.renew(lid, "session-a", 1, wl.at(10), wl.at(40))["ok"])
        self.assertEqual(self.registry.write(lid, "session-a", 1, "docs/team/M1-01/x.json", wl.at(40), wl.SHA_Y)["reason"], "lease_expired")
        self.assertEqual(self.registry.renew(lid, "session-a", 1, wl.at(40), wl.at(50))["reason"], "lease_expired")
        current = self.registry.leases[lid]
        self.assertEqual((current["holder"], current["baseRef"], current["fence"], current["expiresAt"]), ("session-a", wl.BASE_A, 1, wl.iso(wl.at(40))))

    def test_expired_lease_blocks_until_pm_recovers(self):
        lid = self.acquire("session-a", wl.wf("Assets/Scenes/Main.unity"), ttl=10)["lease"]["leaseId"]
        self.assertEqual(len(self.registry.expire_due(wl.at(10))), 1)
        refused = self.acquire("session-b", wl.wf("Assets/Scenes/Main.unity"), minutes=11)
        self.assertEqual((refused["reason"], refused["details"]["conflictingState"]), ("overlap_with_expired", "expired"))
        self.assertEqual(self.registry.release(lid, "session-a", 1, wl.at(11))["reason"], "transition_not_in_contract")
        self.assertTrue(self.registry.recover(lid, "pm-session", "role:pm", wl.SHA_X, wl.at(12))["ok"])
        self.assertEqual(self.acquire("session-b", wl.wf("Assets/Scenes/Main.unity"), minutes=13)["lease"]["fence"], 2)


class HandoffAndRecovery(RegistryCase):
    def test_normal_handoff_requires_new_acquire_and_fences_out_previous_holder(self):
        lid = self.acquire("session-a", wl.wf("Assets/Prefabs/Gate.prefab"))["lease"]["leaseId"]
        record = {"nextSessionId": "session-b", "outputs": ["Assets/Prefabs/Gate.prefab"], "tests": ["EditMode"], "knownLimits": ["none recorded"]}
        self.assertEqual(self.registry.handoff(lid, "session-a", 1, wl.at(5), {"nextSessionId": "session-b"})["reason"], "handoff_record_incomplete")
        self.assertEqual(self.registry.handoff(lid, "session-b", 1, wl.at(5), record)["reason"], "caller_not_holder")
        handed = self.registry.handoff(lid, "session-a", 1, wl.at(5), record)
        self.assertEqual((handed["lease"]["state"], handed["lease"]["holder"], handed["lease"]["baseRef"]), ("released", "session-a", wl.BASE_A))
        self.assertFalse(any(lease["holder"] == "session-b" for lease in self.registry.leases.values()))
        nxt = self.acquire("session-b", wl.wf("Assets/Prefabs/Gate.prefab"), minutes=6, base=wl.BASE_B)["lease"]
        self.assertEqual(nxt["fence"], 2)
        self.assertEqual(self.registry.write(lid, "session-a", 1, "Assets/Prefabs/Gate.prefab", wl.at(7), wl.SHA_Y)["reason"], "transition_not_in_contract")
        self.assertEqual(self.registry.write(nxt["leaseId"], "session-b", 1, "Assets/Prefabs/Gate.prefab", wl.at(7), wl.SHA_Y)["reason"], "stale_fence")
        self.assertTrue(self.registry.write(nxt["leaseId"], "session-b", 2, "Assets/Prefabs/Gate.prefab", wl.at(7), wl.SHA_Y)["ok"])

    def test_crash_recovery_needs_pm_and_matching_content(self):
        lid = self.acquire("session-a", wl.wf("Assets/Scenes/Hall.unity"))["lease"]["leaseId"]
        self.registry.write(lid, "session-a", 1, "Assets/Scenes/Hall.unity", wl.at(2), wl.SHA_Y)
        self.assertEqual(self.registry.recover(lid, "pm-session", "role:pm", wl.SHA_Y, wl.at(3))["reason"], "transition_not_in_contract")
        self.assertTrue(self.registry.crash_detected(lid, wl.at(4))["ok"])
        self.assertEqual(self.registry.write(lid, "session-a", 1, "Assets/Scenes/Hall.unity", wl.at(4), wl.SHA_Y)["reason"], "transition_not_in_contract")
        refused = self.registry.recover(lid, "session-b", "role:authoring-agent", wl.SHA_Y, wl.at(5))
        self.assertEqual((refused["reason"], self.registry.leases[lid]["state"]), ("caller_not_pm", "crash_suspected"))
        recovered = self.registry.recover(lid, "pm-session", "role:pm", wl.SHA_Y, wl.at(6))
        self.assertEqual((recovered["ok"], recovered["lease"]["state"], recovered["lease"]["holder"]), (True, "recovered", "session-a"))

    def test_changed_content_fails_recovery_and_preserves_record(self):
        lid = self.acquire("session-a", wl.wf("Assets/Scenes/Platform.unity"))["lease"]["leaseId"]
        self.registry.crash_detected(lid, wl.at(1))
        before = copy.deepcopy(self.registry.leases[lid])
        failed = self.registry.recover(lid, "pm-session", "role:pm", "9" * 64, wl.at(2))
        self.assertEqual((failed["ok"], failed["reason"], failed["lease"]["state"]), (False, "content_changed_since_last_authorized_write", "failed"))
        for field in ("holder", "baseRef", "fence", "resource", "baseContentSha256"):
            self.assertEqual(failed["lease"][field], before[field], field)
        self.assertEqual(failed["details"]["observedSha256"], "9" * 64)
        self.assertEqual(self.acquire("session-b", wl.wf("Assets/Scenes/Platform.unity"), minutes=3)["reason"], "overlap_with_failed")
        self.assertEqual(self.registry.recover(lid, "pm-session", "role:pm", wl.SHA_X, wl.at(4))["reason"], "transition_not_in_contract")


class JournalAndOutcomes(RegistryCase):
    def test_journal_is_append_only_and_keeps_refusals(self):
        self.acquire("session-a", wl.wf("docs/a.md"))
        first = self.registry.journal_path.read_bytes()
        self.acquire("session-b", wl.wf("docs/a.md"))
        self.acquire("", wl.wf("docs/b.md"))
        entries = self.registry.journal()
        self.assertTrue(self.registry.journal_path.read_bytes().startswith(first))
        self.assertEqual([e["seq"] for e in entries], [1, 2, 3])
        self.assertEqual([e["result"] for e in entries], ["accepted", "rejected", "rejected"])
        self.assertEqual(entries[1]["details"]["conflictingHolder"], "session-a")
        self.assertEqual(set(entries[0]), set(CONTRACT["journal"]["entryFields"]))

    def test_outcome_records_keep_failed_attempt_and_link_retry(self):
        base = {"sessionId": "session-17-a", "workId": "M1-01", "baseRef": wl.BASE_A, "startedAt": "2026-09-15T00:00:00Z", "endedAt": "2026-09-15T00:10:00Z"}
        good = [{**base, "attempt": 1, "outcome": "failed", "reason": "stale_fence"}, {**base, "attempt": 2, "outcome": "retried", "retryOf": 1},
                {**base, "attempt": 3, "outcome": "cancelled", "reason": "scope change", "cancelledBy": "role:pm"}]
        self.assertEqual(wl.validate_outcomes(CONTRACT, good), [])
        bad = [{**base, "attempt": 1, "outcome": "failed"}, {**base, "attempt": 2, "outcome": "retried", "retryOf": 7},
               {**base, "attempt": 2, "outcome": "cancelled", "reason": "x"}]
        self.assertEqual(wl.validate_outcomes(CONTRACT, bad),
                         ["record0:missing:reason", "record1:retry_of_unknown_attempt", "record2:missing:cancelledBy", "record2:attempt_overwritten"])


class Cli(unittest.TestCase):
    def run_main(self, *argv):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = wl.main(list(argv))
        return code, json.loads(out.getvalue())

    def test_scenarios_pass_and_bad_contracts_exit_nonzero(self):
        code, report = self.run_main("scenarios")
        self.assertEqual((code, report["state"]), (0, "passed"))
        self.assertTrue(report["journal"]["sequencesContiguous"])
        self.assertGreater(report["journal"]["rejectedEntriesRetained"], 0)
        with tempfile.TemporaryDirectory() as tmp:
            self.assertEqual(self.run_main("inspect", "--contract", str(Path(tmp) / "absent.json"))[0], 2)
            broken = copy.deepcopy(CONTRACT)
            broken["lease"]["limits"] = {}
            path = Path(tmp) / "broken.json"
            path.write_text(json.dumps(broken), encoding="utf-8")
            self.assertEqual(self.run_main("inspect", "--contract", str(path))[0], 1)
            self.assertEqual(self.run_main("scenarios", "--contract", str(path))[0], 2)


if __name__ == "__main__":
    unittest.main()
