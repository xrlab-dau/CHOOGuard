"""Policy gate rules and the git plumbing they depend on (temporary repositories only)."""
import os
import subprocess
import sys
import tempfile
import unittest
from contextlib import contextmanager
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import ci_policy as policy  # noqa: E402

MIB = 1024 * 1024


def paths_of(findings):
    return sorted(f.path for f in findings)


class UnityMetaTests(unittest.TestCase):
    def test_complete_tree_passes(self):
        tree = ["Assets/A.meta", "Assets/A/x.cs", "Assets/A/x.cs.meta", "Assets/A/B.meta", "Assets/A/B/y.mat", "Assets/A/B/y.mat.meta"]
        self.assertEqual(policy.check_unity_meta(tree), [])

    def test_missing_file_and_folder_meta_are_errors(self):
        tree = ["Assets/A/x.cs", "Assets/A/x.cs.meta", "Assets/A/y.png"]
        self.assertEqual(paths_of(policy.check_unity_meta(tree)), ["Assets/A", "Assets/A/y.png"])

    def test_meta_of_an_untracked_folder_is_an_orphan(self):
        # git keeps no empty folders, so the receiving Unity deletes this meta on import.
        tree = ["Assets/A.meta", "Assets/A/x.cs", "Assets/A/x.cs.meta", "Assets/A/Empty.meta", "Assets/A/gone.png.meta"]
        self.assertEqual(paths_of(policy.check_unity_meta(tree)), ["Assets/A/Empty.meta", "Assets/A/gone.png.meta"])

    def test_names_unity_skips_need_no_meta(self):
        tree = ["Assets/.planning/n.json", "Assets/Samples~/s.cs", "Assets/A.meta", "Assets/A/CVS/c.txt", "Assets/A/t.tmp"]
        self.assertEqual(policy.check_unity_meta(tree), [])

    def test_embedded_packages_are_asset_roots(self):
        tree = ["Packages/manifest.json", "Packages/com.x/package.json", "Packages/com.x/package.json.meta", "Packages/com.x/R/a.cs"]
        self.assertEqual(paths_of(policy.check_unity_meta(tree)), ["Packages/com.x/R", "Packages/com.x/R/a.cs"])


class WholeTreeHygieneTests(unittest.TestCase):
    def test_generated_output_bytecode_and_junk_are_rejected(self):
        tree = ["Library/ArtifactDB", "Builds/macOS/CHOOGuard.app/x", "art/world/__pycache__/m.cpython-314.pyc",
                "tools/x.pyo", "Assets/.DS_Store", "docs/obj.md", "Assets/Library/readme.txt", "Assets/obj/model.obj"]
        self.assertEqual(paths_of(policy.check_generated(tree)),
                         ["Assets/.DS_Store", "Builds/macOS/CHOOGuard.app/x", "Library/ArtifactDB", "art/world/__pycache__/m.cpython-314.pyc", "tools/x.pyo"])

    def test_force_text_serialization_is_required(self):
        self.assertEqual(policy.check_serialization_mode("EditorSettings:\n  m_SerializationMode: 2\n"), [])
        self.assertEqual(len(policy.check_serialization_mode("  m_SerializationMode: 1\n")), 1)
        self.assertEqual(len(policy.check_serialization_mode(None)), 1)


class ChangeScopedTests(unittest.TestCase):
    def test_only_changed_paths_that_match_ignore_rules_fail(self):
        ignored = lambda paths: {"Assets/.planning/new.png", "asset-library/old.pyc"}
        findings = policy.check_ignored(["Assets/.planning/new.png", "Assets/A/x.cs"], ignored)
        self.assertEqual(paths_of(findings), ["Assets/.planning/new.png"])
        self.assertEqual(policy.check_ignored([], lambda paths: self.fail("no change, no git call")), [])

    def test_text_asset_types_and_binary_detection(self):
        self.assertTrue(policy.unity_text_asset("Assets/S/Main.UNITY"))
        self.assertTrue(policy.unity_text_asset("Assets/M/wall.mat"))
        self.assertFalse(policy.unity_text_asset("Assets/S/LightingData.asset"))  # binary by design
        heads = {"Assets/a.prefab": b"%YAML 1.", "Assets/b.unity": b"\x00\x00\x01\x02UnityFS"}
        self.assertEqual(paths_of(policy.check_text_assets(heads)), ["Assets/b.unity"])

    def test_size_thresholds(self):
        findings = policy.check_sizes({"ok": 50 * MIB, "warn": 50 * MIB + 1, "limit": 100 * MIB, "over": 100 * MIB + 1})
        self.assertEqual([(f.path, f.level) for f in findings], [("limit", "warning"), ("over", "error"), ("warn", "warning")])

    def test_main_only_takes_release_branches(self):
        for head in ("develop", "hotfix/door-crash", "release/0.2.0"):
            self.assertEqual(policy.check_branch_flow("main", head), [], head)
        self.assertEqual(len(policy.check_branch_flow("main", "feature/tutorial")), 1)
        self.assertEqual(policy.check_branch_flow("develop", "feature/tutorial"), [])


def font(mode, clear, glyphs):
    table = "  m_GlyphTable: []\n" if not glyphs else "  m_GlyphTable:\n" + "".join(f"  - m_Index: {i}\n    m_Scale: 1\n" for i in range(glyphs))
    return (f"%YAML 1.1\n--- !u!114 &11400000\nMonoBehaviour:\n  m_Name: Font SDF\n  m_AtlasPopulationMode: {mode}\n{table}"
            f"  m_CharacterTable: []\n  m_ClearDynamicDataOnBuild: {clear}\n").encode()


class DynamicFontTests(unittest.TestCase):
    def test_dynamic_clear_on_build_fonts_must_be_committed_empty(self):
        texts = {"Assets/F/Dynamic SDF.asset": font(1, 1, 3), "Assets/F/DynamicOS SDF.asset": font(2, 1, 1), "Assets/F/Rest SDF.asset": font(1, 1, 0)}
        self.assertEqual(paths_of(policy.check_dynamic_fonts(texts)), ["Assets/F/Dynamic SDF.asset", "Assets/F/DynamicOS SDF.asset"])

    def test_static_atlases_and_explicitly_kept_dynamic_data_may_carry_glyphs(self):
        texts = {"Assets/F/Static SDF.asset": font(0, 1, 250), "Assets/F/Kept SDF.asset": font(1, 0, 311),
                 "Assets/F/Other SDF.asset": b"%YAML 1.1\n--- !u!114 &1\nMonoBehaviour:\n  m_Name: not a font\n"}
        self.assertEqual(policy.check_dynamic_fonts(texts), [])

    def test_font_asset_paths(self):
        self.assertTrue(policy.tmp_font_asset("Assets/ChooGuard/Settings/ImportedAssets/Fonts/NotoSansCJKkr SDF.asset"))
        self.assertTrue(policy.tmp_font_asset("Assets/Fonts/Noto SDF - Fallback.asset"))
        self.assertFalse(policy.tmp_font_asset("Assets/ChooGuard/ThirdParty/Fonts/NotoSansCJKkr-Regular.otf"))
        self.assertFalse(policy.tmp_font_asset("Assets/ChooGuard/Scenes/FpsStation/NavMesh.asset"))


class GraphFreshnessTests(unittest.TestCase):
    def test_indexed_changes_after_the_graph_update_warn(self):
        findings = policy.graph_findings("a" * 40, ["Assets/X.cs", "graphify-out/GRAPH_REPORT.md", "Assets/tex.png"], {".cs", ".md"})
        self.assertEqual([(f.level, f.check) for f in findings], [("warning", "graph-freshness")])
        self.assertIn("1 indexed file", findings[0].message)

    def test_fresh_or_untracked_graph(self):
        self.assertEqual(policy.graph_findings("a" * 40, ["Assets/tex.png"], {".cs"}), [])
        self.assertEqual([f.level for f in policy.graph_findings(None, [], set())], ["notice"])


class ReportingTests(unittest.TestCase):
    def test_workflow_command_escaping(self):
        line = policy.annotation(policy.Finding("error", "unity-meta", "50% done\nnext", "Assets/a,b:c.png"))
        self.assertEqual(line, "::error file=Assets/a%2Cb%3Ac.png,title=unity-meta::50%25 done%0Anext")

    def test_summary_marks_each_check(self):
        text = policy.summary([policy.Finding("error", "unity-meta", "m", "p"), policy.Finding("warning", "large-files", "w", "q")],
                              ["unity-meta", "large-files", "branch-flow"], ["note"])
        self.assertIn("| `unity-meta` | ❌ 1 error(s) |", text)
        self.assertIn("| `large-files` | ⚠️ 1 warning(s) |", text)
        self.assertIn("| `branch-flow` | ✅ pass |", text)


@contextmanager
def repository():
    old = os.getcwd()
    with tempfile.TemporaryDirectory() as root:
        os.chdir(root)
        try:
            run("git", "init", "-q", "-b", "develop")
            yield Path(root)
        finally:
            os.chdir(old)


def run(*args):
    env = dict(os.environ, GIT_AUTHOR_NAME="t", GIT_AUTHOR_EMAIL="t@example.com", GIT_COMMITTER_NAME="t", GIT_COMMITTER_EMAIL="t@example.com")
    return subprocess.run(args, check=True, capture_output=True, env=env).stdout.decode().strip()


def write(root, path, data):
    target = root / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)


class GitPlumbingTests(unittest.TestCase):
    def test_change_set_ignore_matching_and_blob_heads(self):
        with repository() as root:
            write(root, ".gitignore", b"Obj/\n.planning/\n")
            write(root, "Assets/old.mat", b"%YAML 1.1\nold")
            write(root, "Assets/gone.mat", b"%YAML 1.1\n")
            run("git", "add", "-A")
            run("git", "commit", "-q", "-m", "base")
            base = run("git", "rev-parse", "HEAD")
            write(root, "Assets/old.mat", b"%YAML 1.1\nnew")
            write(root, "Assets/Scene [1], v2.unity", b"%YAML 1.1\n--- !u!29")
            write(root, "Assets/Hero.prefab", b"\x00\x01binary")
            write(root, "Assets/Kit/obj/model.obj", b"v 0 0 0")  # `Obj/` ignores `obj/` on the team's case-insensitive hosts
            os.symlink("old.mat", root / "Assets/link.mat")
            (root / "Assets/gone.mat").unlink()
            run("git", "add", "-A")
            run("git", "add", "-f", "Assets/Kit/obj/model.obj")
            run("git", "commit", "-q", "-m", "change")

            blobs = policy.changed_blobs(base, "HEAD")
            self.assertEqual(sorted(blobs), ["Assets/Hero.prefab", "Assets/Kit/obj/model.obj", "Assets/Scene [1], v2.unity", "Assets/old.mat"])
            self.assertEqual(policy.git_ignored(sorted(blobs)), {"Assets/Kit/obj/model.obj"})
            heads = policy.blob_heads({p: o for p, o in blobs.items() if policy.unity_text_asset(p)})
            self.assertEqual(heads, {"Assets/Hero.prefab": b"\x00\x01binary", "Assets/Scene [1], v2.unity": b"%YAML 1.", "Assets/old.mat": b"%YAML 1."})
            self.assertEqual(policy.blob_heads({"Assets/old.mat": blobs["Assets/old.mat"]}, length=None), {"Assets/old.mat": b"%YAML 1.1\nnew"})

    def test_gate_fails_on_a_binary_prefab_and_passes_once_fixed(self):
        with repository() as root:
            write(root, "ProjectSettings/EditorSettings.asset", b"%YAML 1.1\n  m_SerializationMode: 2\n")
            write(root, "Assets/A.meta", b"m")
            write(root, "Assets/A/x.cs", b"class X {}")
            write(root, "Assets/A/x.cs.meta", b"m")
            run("git", "add", "-A")
            run("git", "commit", "-q", "-m", "base")
            base = run("git", "rev-parse", "HEAD")
            write(root, "Assets/A/Hero.prefab", b"\x00binary")
            write(root, "Assets/A/Hero.prefab.meta", b"m")
            run("git", "add", "-A")
            run("git", "commit", "-q", "-m", "binary prefab")
            env = {k: v for k, v in os.environ.items() if k not in ("GITHUB_STEP_SUMMARY", "GITHUB_TOKEN")}
            with mock.patch.dict(os.environ, env, clear=True):
                self.assertEqual(policy.main(["--base", base, "--head", "HEAD", "--event", "push"]), 1)
                write(root, "Assets/A/Hero.prefab", b"%YAML 1.1\n--- !u!1")
                run("git", "commit", "-q", "-am", "text prefab")
                self.assertEqual(policy.main(["--base", base, "--head", "HEAD", "--event", "push"]), 0)

    def test_gate_rejects_a_filled_dynamic_font_and_accepts_it_at_rest(self):
        with repository() as root:
            write(root, "ProjectSettings/EditorSettings.asset", b"%YAML 1.1\n  m_SerializationMode: 2\n")
            write(root, "Assets/F.meta", b"m")
            write(root, "Assets/F/Noto SDF.asset", font(1, 1, 0))
            write(root, "Assets/F/Noto SDF.asset.meta", b"m")
            run("git", "add", "-A")
            run("git", "commit", "-q", "-m", "font at rest")
            base = run("git", "rev-parse", "HEAD")
            write(root, "Assets/F/Noto SDF.asset", font(1, 1, 311))  # an editor session filled it
            run("git", "commit", "-q", "-am", "filled font")
            env = {k: v for k, v in os.environ.items() if k not in ("GITHUB_STEP_SUMMARY", "GITHUB_TOKEN")}
            with mock.patch.dict(os.environ, env, clear=True):
                self.assertEqual(policy.main(["--base", base, "--head", "HEAD", "--event", "push"]), 1)
                write(root, "Assets/F/Noto SDF.asset", font(1, 1, 0))
                run("git", "commit", "-q", "-am", "back at rest")
                self.assertEqual(policy.main(["--base", base, "--head", "HEAD", "--event", "push"]), 0)


if __name__ == "__main__":
    unittest.main()
