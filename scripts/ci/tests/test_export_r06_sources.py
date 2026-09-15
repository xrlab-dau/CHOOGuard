"""Exercise the source exporter against real temporary Git objects, without network."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile

EXPORTER = Path(__file__).resolve().parents[1] / "export_r06_sources.py"
REQUIRED = (
    "docs/team/M0-03/execution-profile.json",
    "docs/team/M1-05/record-schema.json",
    "docs/team/R-06/research-control.json",
    "scripts/team/M1-05/record_pipeline.py",
    "scripts/team/M1-05/test_record_schema.py",
    "scripts/team/M1-05/test_redaction.py",
    "scripts/team/R-06/egress_controls.py",
    "scripts/team/R-06/test_egress_controls.py",
)


class ExportSourcesTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="r06-export-test-")
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.repo = self.root / "repo"
        self.repo.mkdir()
        self.git("init", "-q")
        self.git("config", "user.name", "Synthetic test")
        self.git("config", "user.email", "test@example.invalid")
        self.git("config", "core.autocrlf", "false")
        self.original = {}
        for name in REQUIRED:
            path = self.repo / name
            path.parent.mkdir(parents=True, exist_ok=True)
            self.original[name] = ("# synthetic source " + name + "\n").encode()
            path.write_bytes(self.original[name])
        (self.repo / "unrelated.txt").write_text("not in export scope", encoding="utf-8")
        self.commit()
        self.ref = self.git("rev-parse", "HEAD").decode().strip()

    def git(self, *args, data=None):
        return subprocess.run(["git", "-C", str(self.repo), *args], input=data,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                              check=True, timeout=20).stdout

    def commit(self):
        self.git("add", ".")
        self.git("commit", "-qm", "synthetic fixture")

    def run_export(self, ref=None, output="export"):
        proc = subprocess.run([sys.executable, "-B", str(EXPORTER), "--repo", str(self.repo),
                               "--ref=" + (self.ref if ref is None else ref),
                               "--output", str(self.root / output)],
                              capture_output=True, text=True, timeout=30)
        return proc

    def assert_success(self, proc):
        self.assertEqual(proc.returncode, 0, proc.stdout + proc.stderr)
        self.assertEqual(proc.stderr, "")

    def test_exact_committed_bytes_and_manifest(self):
        self.assert_success(self.run_export())
        out = self.root / "export"
        manifest = json.loads((out / "manifest.json").read_text(encoding="utf-8"))
        self.assertEqual(manifest["sourceCommit"], self.ref)
        self.assertEqual(manifest["claim"], "source_bytes_only_not_test_or_acceptance")
        self.assertEqual(set(manifest["files"]), set(REQUIRED))
        self.assertEqual(manifest["archiveSha256"], hashlib.sha256((out / "sources.zip").read_bytes()).hexdigest())
        with zipfile.ZipFile(out / "sources.zip") as z:
            self.assertEqual(set(z.namelist()), set(REQUIRED))
            for name, data in self.original.items():
                self.assertEqual(z.read(name), data)
                meta = manifest["files"][name]
                self.assertEqual(meta["bytes"], len(data))
                self.assertEqual(meta["sha256"], hashlib.sha256(data).hexdigest())
                self.assertEqual(meta["gitBlob"], hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest())
        self.assertNotIn(str(self.repo), (out / "manifest.json").read_text())

    def test_dirty_worktree_and_untracked_files_are_not_exported(self):
        (self.repo / REQUIRED[0]).write_bytes(b"uncommitted change")
        (self.repo / ".env").write_text("synthetic fixture", encoding="utf-8")
        self.assert_success(self.run_export())
        with zipfile.ZipFile(self.root / "export/sources.zip") as z:
            self.assertEqual(z.read(REQUIRED[0]), self.original[REQUIRED[0]])
            self.assertNotIn(".env", z.namelist())
            self.assertNotIn("unrelated.txt", z.namelist())

    def test_repeated_exports_are_byte_identical(self):
        self.assert_success(self.run_export(output="one"))
        self.assert_success(self.run_export(output="two"))
        for name in ("sources.zip", "manifest.json"):
            self.assertEqual((self.root / "one" / name).read_bytes(), (self.root / "two" / name).read_bytes())

    def test_mutable_or_option_like_refs_are_refused(self):
        for ref in ("HEAD", "develop", "--help", "a" * 39, "A" * 40):
            with self.subTest(ref=ref):
                p = self.run_export(ref=ref)
                self.assertEqual(p.returncode, 2)
                self.assertIn("invalid_commit_ref", p.stdout)
        self.assertFalse((self.root / "export").exists())

    def test_missing_required_source_is_refused_without_output(self):
        self.git("rm", REQUIRED[0])
        self.git("commit", "-qm", "remove required fixture")
        p = self.run_export(ref=self.git("rev-parse", "HEAD").decode().strip())
        self.assertEqual(p.returncode, 2)
        self.assertIn("source_missing_or_not_regular", p.stdout)
        self.assertFalse((self.root / "export").exists())

    def test_existing_output_is_never_overwritten(self):
        self.assert_success(self.run_export())
        before = {p.name: p.read_bytes() for p in (self.root / "export").iterdir()}
        p = self.run_export()
        self.assertEqual(p.returncode, 2)
        self.assertIn("output_unavailable", p.stdout)
        self.assertEqual(before, {p.name: p.read_bytes() for p in (self.root / "export").iterdir()})

    def test_blob_ref_is_not_accepted_as_commit(self):
        blob = self.git("rev-parse", self.ref + ":" + REQUIRED[0]).decode().strip()
        p = self.run_export(ref=blob)
        self.assertEqual(p.returncode, 2)
        self.assertIn("ref_is_not_commit", p.stdout)

    def test_tracked_symbolic_link_is_refused_without_following(self):
        blob = self.git("hash-object", "-w", "--stdin", data=b"unrelated.txt").decode().strip()
        self.git("update-index", "--cacheinfo", "120000," + blob + "," + REQUIRED[0])
        self.git("commit", "-qm", "synthetic link fixture")
        p = self.run_export(ref=self.git("rev-parse", "HEAD").decode().strip())
        self.assertEqual(p.returncode, 2)
        self.assertIn("source_missing_or_not_regular", p.stdout)
        self.assertFalse((self.root / "export").exists())

    def test_non_utf8_source_is_refused(self):
        (self.repo / REQUIRED[0]).write_bytes(b"\xff\xfe")
        self.commit()
        p = self.run_export(ref=self.git("rev-parse", "HEAD").decode().strip())
        self.assertEqual(p.returncode, 2)
        self.assertIn("source_not_utf8", p.stdout)
        self.assertFalse((self.root / "export").exists())

    def test_oversized_source_is_refused_before_export(self):
        (self.repo / REQUIRED[0]).write_bytes(b"x" * (2 * 1024 * 1024 + 1))
        self.commit()
        p = self.run_export(ref=self.git("rev-parse", "HEAD").decode().strip())
        self.assertEqual(p.returncode, 2)
        self.assertIn("source_too_large", p.stdout)
        self.assertFalse((self.root / "export").exists())


if __name__ == "__main__":
    unittest.main()
