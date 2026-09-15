"""M1-02 permission boundary tests: every denied request is refused with its reason and leaves state unchanged."""
import contextlib
import io
import json
import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import permission_boundary as pb  # noqa: E402

REQUIRED_CATEGORIES = {"out_of_root", "secret", "restricted_input", "egress", "symlink", "generated_output", "destructive_command"}
M1_02_CASE_REFS = {"DC-03", "DC-05", "DC-06", "DC-08", "DC-09", "DC-10"}


class PermissionBoundaryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.model = pb.load_model()
        cls.report = pb.run_fixtures()

    def test_profile_policy_hash_recomputes_and_tampering_blocks(self):
        published = self.model["profile"]["policy"]["policyHash"]["value"]
        self.assertEqual(self.model["policyHash"], published)
        with tempfile.TemporaryDirectory() as tmp:
            profile = json.loads(pb.DEFAULT_PROFILE.read_text(encoding="utf-8"))
            profile["roles"][0]["roleId"] = "role:renamed"
            tampered = Path(tmp) / "profile.json"
            tampered.write_text(json.dumps(profile), encoding="utf-8")
            with self.assertRaisesRegex(pb.ProfileError, "^policy_hash_mismatch$"):
                pb.load_model(tampered, pb.DEFAULT_SETTINGS)

    def test_missing_profile_is_blocked_not_passed(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = pb.main(["run", "--profile", str(Path(tempfile.gettempdir()) / "m1-02-absent-profile.json")])
        self.assertEqual(code, 2)
        self.assertEqual(json.loads(out.getvalue())["state"], "blocked")

    def test_every_case_matches_and_denials_leave_state_unchanged(self):
        self.assertEqual(self.report["state"], "passed", [c for c in self.report["cases"] if not c["ok"]])
        self.assertTrue(self.report["chainIntact"])
        for result in self.report["cases"]:
            if result["blocked"]:
                continue
            with self.subTest(case=result["id"]):
                self.assertEqual(result["observed"]["decision"], result["expect"]["decision"])
                self.assertEqual(result["observed"]["code"], result["expect"]["code"])
                if result["expect"]["decision"] == "deny":
                    self.assertFalse(result["observed"]["stateChanged"], "a denied request changed the workspace")
                    self.assertEqual(result["observed"]["effect"], "none")
        wrote = next(c for c in self.report["cases"] if c["id"] == "M02-35")
        self.assertEqual(wrote["observed"]["effect"], "wrote")
        self.assertTrue(wrote["observed"]["stateChanged"], "an allowed write must be applied, proving the guard is not a no-op")

    def test_catalog_covers_required_surfaces_and_states_capability(self):
        categories = {c["category"] for c in pb.CASES}
        self.assertTrue(REQUIRED_CATEGORIES <= categories, REQUIRED_CATEGORIES - categories)
        self.assertTrue(M1_02_CASE_REFS <= {c["dcRef"] for c in pb.CASES}, M1_02_CASE_REFS - {c["dcRef"] for c in pb.CASES})
        self.assertTrue(any(c["expect"]["decision"] == "allow" for c in pb.CASES))
        for item in pb.CASES:
            with self.subTest(case=item["id"]):
                self.assertTrue(item["capability"] and item["expectedFailure"])
                self.assertNotRegex(json.dumps(item["request"]), r"(?i)hostname|school|location|owner")

    def test_link_escape_uses_a_real_link_or_is_recorded_blocked(self):
        result = next(c for c in self.report["cases"] if c["category"] == "symlink")
        if result["blocked"]:
            self.assertIsNone(result["ok"])
            self.skipTest("link capability unavailable: " + result["blocked"])
        self.assertIn(self.report["capability"]["linkCapability"], ("symlink", "junction"))
        self.assertEqual(result["observed"]["code"], "link_escape")
        self.assertTrue(result["ok"])

    def test_profile_or_settings_change_mid_run_invalidates_every_result(self):
        with tempfile.TemporaryDirectory() as tmp:
            profile, settings = Path(tmp) / "profile.json", Path(tmp) / "settings.json"
            shutil.copyfile(pb.DEFAULT_PROFILE, profile)
            shutil.copyfile(pb.DEFAULT_SETTINGS, settings)

            def change(case_id):
                if case_id == "M02-10":
                    settings.write_bytes(settings.read_bytes() + b"\n")

            report = pb.run_fixtures(profile, settings, after_case=change)
        self.assertEqual(report["state"], "invalidated_rerun_required")
        self.assertFalse(report["chainIntact"])
        broken = [link["caseId"] for link in report["hashChain"] if not link["intact"]]
        ids = [c["id"] for c in pb.CASES]
        self.assertEqual(broken, ids[ids.index("M02-10") + 1:])

    def test_bypass_spellings_are_still_refused(self):
        root = Path(tempfile.mkdtemp(prefix="m1-02-unit-"))
        self.addCleanup(shutil.rmtree, root, True)
        author = "role:authoring-agent"
        cases = [
            ({"actor": author, "action": "write", "path": "..\\outside.txt", "workScope": ["**"]}, "out_of_root"),
            ({"actor": author, "action": "write", "path": "LIBRARY/x.bin", "workScope": ["**"]}, "generated_output"),
            ({"actor": author, "action": "read", "path": "config/.ENV"}, "secret_material"),
            ({"actor": author, "action": "read", "path": "Private-Data/facility-sources/x"}, "restricted_input"),
            ({"actor": author, "action": "write", "path": "scripts/bootstrap/verify_toolchain.py", "workScope": ["**"]}, "policy_file"),
            ({"actor": author, "action": "execute", "command": "git   reset   --hard"}, "destructive_command"),
            ({"actor": author, "action": "egress", "destination": "DEST-MODEL-AUTHOR", "dataClass": "SECRET_LEVEL", "approvals": ["APR-EGRESS"]}, "unknown_data_class"),
            ({"actor": "role:unlisted", "action": "read", "path": "src/readme.md"}, "unknown_role"),
            ({"actor": author, "action": "chmod", "path": "src/readme.md"}, "unknown_action"),
            # Review findings 2026-09-15: each of these was allowed before the fix.
            ({"actor": author, "action": "execute", "command": "rm -r -f ./Assets"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "rm --recursive --force ./Assets"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "RM.EXE -R ./Assets"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "git --no-pager reset --hard"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "git -c a=b -C . reset --hard"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "git push --force-with-lease origin develop"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "git push origin :develop"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "git checkout -- ."}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "git restore Assets"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "git branch -D feature/x"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "echo ok; rm -rf ./Assets"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "echo ok\nrm -rf ./Assets"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "FOO=1 sudo rm -rf ./Assets"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "cmd /c rmdir Assets"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "pwsh -Command Remove-Item Assets"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "C:\\tools\\rm.exe -rf ./Assets"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "echo $(rm -rf ./Assets)"}, "undecidable_command"),
            ({"actor": author, "action": "execute", "command": "iex (Get-Content x.ps1)"}, "undecidable_command"),
            ({"actor": author, "action": "execute", "command": "echo \"unterminated"}, "undecidable_command"),
            # Re-review of the fix (2026-09-15): interpreters, find and further git sub-commands.
            ({"actor": author, "action": "execute", "command": "python -c \"import shutil; shutil.rmtree('Assets')\""}, "undecidable_command"),
            ({"actor": author, "action": "execute", "command": "py -3 -c \"import os\""}, "undecidable_command"),
            ({"actor": author, "action": "execute", "command": "node -e \"require('fs').rmSync('Assets',{recursive:true})\""}, "undecidable_command"),
            ({"actor": author, "action": "execute", "command": "perl -e \"unlink glob('*')\""}, "undecidable_command"),
            ({"actor": author, "action": "execute", "command": "find . -name \"*.tmp\" -delete"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "find . -type f -exec rm {} +"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "git worktree remove --force wt"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "git submodule deinit -f ."}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "git lfs prune"}, "destructive_command"),
            ({"actor": author, "action": "execute", "command": "git branch -M main"}, "destructive_command"),
            ({"actor": author, "action": "write", "path": "agents.md", "workScope": ["**"]}, "policy_file"),
            ({"actor": author, "action": "write", "path": ".GITHUB/WORKFLOWS/ci.yml", "workScope": ["**"]}, "policy_file"),
            ({"actor": author, "action": "write", "path": "SCRIPTS/team/OTHER/x.py", "workScope": ["scripts/team/M1-02/**"]}, "outside_work_scope"),
            ({"actor": author, "action": "write", "path": "AGENTS.md.", "workScope": ["**"]}, "non_canonical_path"),
            ({"actor": author, "action": "write", "path": "AGENTS.md ", "workScope": ["**"]}, "non_canonical_path"),
            ({"actor": author, "action": "write", "path": ".env:x", "workScope": ["**"]}, "non_canonical_path"),
            ({"actor": author, "action": "read", "path": "keys/service.pem::$DATA"}, "non_canonical_path"),
            ({"actor": author, "action": "write", "path": "AGENTS~1.MD", "workScope": ["**"]}, "non_canonical_path"),
            ({"actor": author, "action": "write", "path": "scripts/team/M1-02/backup~1.py", "workScope": ["**"]}, "non_canonical_path"),
            ({"actor": author, "action": "execute", "command": "python -Ic \"import os\""}, "undecidable_command"),
            ({"actor": author, "action": "execute", "command": "deno eval \"Deno.removeSync('Assets')\""}, "undecidable_command"),
            ({"actor": author, "action": "write", "path": "scripts/team/M1-02/con.txt", "workScope": ["**"]}, "non_canonical_path"),
        ]
        for request, code in cases:
            with self.subTest(code=code, target=request.get("path") or request.get("command")):
                decision = pb.decide(self.model, request, root)
                self.assertEqual((decision["decision"], decision["code"]), ("deny", code))

    def test_ordinary_commands_and_paths_are_not_over_refused(self):
        root = Path(tempfile.mkdtemp(prefix="m1-02-unit-"))
        self.addCleanup(shutil.rmtree, root, True)
        author = "role:authoring-agent"
        commands = ["git status --porcelain", "git -C scripts log --oneline", "git commit -m \"fix; tests & docs\"",
                    "git checkout -b feature/x", "git branch -d merged", "python -m unittest discover -s scripts/team/M1-02", "echo remove-item is a word",
                    "python scripts/team/M1-02/permission_boundary.py run", "find . -name \"*.py\" -print", "git worktree list", "node --version"]
        for command in commands:
            with self.subTest(command=command):
                decision = pb.decide(self.model, {"actor": author, "action": "execute", "command": command}, root)
                self.assertEqual(decision["decision"], "allow", decision)
        # A tilde name longer than the 8.3 shape is an ordinary name; "backup~1.py" has the 8.3 shape and is refused (see bypass list).
        for path in ("scripts/team/M1-02/new.py", "Scripts/Team/M1-02/new.py", ".env.example", "docs/team/M1-02/v1.2/notes.md",
                     "docs/team/M1-02/notes~2024.md", "scripts/team/M1-02/release~candidate.py"):
            with self.subTest(path=path):
                decision = pb.decide(self.model, {"actor": author, "action": "write", "path": path, "workScope": ["scripts/team/M1-02/**", "docs/team/M1-02/**", ".env.example"]}, root)
                self.assertEqual(decision["decision"], "allow", decision)

    def test_link_planted_after_the_decision_is_refused_at_effect_time(self):
        base = Path(tempfile.mkdtemp(prefix="m1-02-unit-"))
        self.addCleanup(shutil.rmtree, base, True)
        root, outside = pb.build_workspace(base)
        workspace = pb.GuardedWorkspace(root, self.model)
        real_decide = pb.decide
        kinds = []

        def decide_then_plant(model, request, where):
            decision = real_decide(model, request, where)
            kinds.append(pb.make_link(root / "scripts/team/M1-02/late-link", outside))
            return decision

        pb.decide = decide_then_plant
        try:
            result = workspace.perform({"actor": "role:authoring-agent", "action": "write", "path": "scripts/team/M1-02/late-link/planted.txt",
                                        "workScope": ["scripts/team/M1-02/**"]})
        finally:
            pb.decide = real_decide
        if kinds[0] == "unavailable":
            self.skipTest("link capability unavailable")
        self.assertEqual((result["decision"], result["code"], result["effect"]), ("deny", "link_escape", "none"))
        self.assertEqual(list(outside.iterdir()), [])
        link = root / "scripts/team/M1-02/late-link"
        os.rmdir(link) if kinds[0] == "junction" else os.unlink(link)

    def test_report_carries_no_workspace_path_or_synthetic_file_content(self):
        text = json.dumps(self.report, ensure_ascii=False)
        self.assertNotIn(tempfile.gettempdir(), text)
        self.assertNotIn(tempfile.gettempdir().replace("\\", "/"), text)
        for content in pb.SYNTHETIC_TREE.values():
            if content.strip():
                self.assertNotIn(content.strip(), text)



    def test_installation_and_push_and_reviewer_spoofing_are_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / "workspace"
            root.mkdir()
        # 1. Package install attempt is refused
        req_install = {"actor": "role:authoring-agent", "action": "execute", "command": "pip install requests"}
        dec_install = pb.decide(self.model, req_install, root)
        self.assertEqual(dec_install["decision"], "deny")
        self.assertEqual(dec_install["code"], "installation_disallowed")

        # 2. Git push without APR-PUBLISH approval is refused
        req_push = {"actor": "role:authoring-agent", "action": "execute", "command": "git push origin develop", "approvals": []}
        dec_push = pb.decide(self.model, req_push, root)
        self.assertEqual(dec_push["decision"], "deny")
        self.assertEqual(dec_push["code"], "unapproved_publication")

        # 3. Non-reviewer claiming agent=reviewer is refused
        req_rev = {"actor": "role:unity-mcp-client", "action": "select_model", "agent": "reviewer", "model": "openai-codex/gpt-5.5"}
        dec_rev = pb.decide(self.model, req_rev, root)
        self.assertEqual(dec_rev["decision"], "deny")
        self.assertEqual(dec_rev["code"], "reviewer_role_mismatch")

if __name__ == "__main__":
    unittest.main()
