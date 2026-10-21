"""CI hydration stays scoped, verifies full bytes and fails before Unity import."""
import base64
import contextlib
import hashlib
import io
import os
import stat
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import hydrate_unity_lfs as hydrate


def pointer(data, size=None):
    return (f"version https://git-lfs.github.com/spec/v1\n"
            f"oid sha256:{hashlib.sha256(data).hexdigest()}\n"
            f"size {len(data) if size is None else size}\n").encode()


class HydrateUnityLfsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        previous = Path.cwd()
        os.chdir(self.root)
        self.addCleanup(os.chdir, previous)
        self.storage = self.root / "lfs-cache"
        self.files = []

    def file(self, relative, data):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        self.files.append(relative)
        return path

    def cache(self, data):
        oid = hashlib.sha256(data).hexdigest()
        path = self.storage / oid[:2] / oid[2:4] / oid
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return path

    def git(self, args, **kwargs):
        if args == ["git", "ls-files", "-z", "--", "Assets/", "Packages/", "ProjectSettings/"]:
            self.assertEqual(kwargs["cwd"], self.root)
            return subprocess.CompletedProcess(args, 0, b"\0".join(os.fsencode(p) for p in self.files) + b"\0", b"")
        if args == ["git", "lfs", "env"]:
            return subprocess.CompletedProcess(args, 0, f"LocalMediaDir={self.storage}\n".encode(), b"")
        self.fail(f"Unexpected Git command: {args}")

    def test_no_pointers_does_not_consult_lfs_or_read_large_files(self):
        plain = self.file("Assets/plain.asset", b"%YAML 1.1\n")
        large = self.file("Assets/large.asset", b"x" * (hydrate.MAX_POINTER_BYTES + 1))
        malformed = self.file("Packages/bad.asset", b"version https://git-lfs.github.com/spec/v1\noid sha256:bad\nsize 1\n")
        read = Path.read_bytes

        def small_only(path):
            self.assertNotEqual(path, large)
            return read(path)

        with mock.patch.object(hydrate.subprocess, "run", side_effect=self.git) as git, \
                mock.patch.object(Path, "read_bytes", small_only):
            self.assertEqual(hydrate.hydrate(), (0, 0))
        self.assertEqual(git.call_count, 1)
        self.assertEqual(plain.read_bytes(), b"%YAML 1.1\n")
        self.assertIn(b"sha256:bad", malformed.read_bytes())

    def test_full_scene_binary_and_shared_package_bytes_replace_only_scoped_pointers(self):
        scene, mesh = b"%YAML 1.1\nscene content" * 80, b"\0UnityFS\xffmesh" * 200
        paths = [("Assets/장면 [1], v2.unity", scene), ("Assets/mesh.asset", mesh),
                 ("Packages/local/shared.asset", mesh), ("ProjectSettings/settings.asset", scene)]
        for relative, data in paths:
            self.file(relative, pointer(data))
            self.cache(data)
        executable = self.root / paths[0][0]
        executable.chmod(0o755)
        ignored = self.file("source-evidence/model-weights.bin", pointer(mesh))
        historical = self.file(".checkpoints/Assets/history.unity", pointer(scene))
        traversal = self.file("outside.asset", pointer(scene))
        self.files.append("Assets/../outside.asset")
        with mock.patch.object(hydrate.subprocess, "run", side_effect=self.git) as git:
            self.assertEqual(hydrate.hydrate(), (4, 2 * len(scene) + 2 * len(mesh)))
        for relative, data in paths:
            self.assertEqual((self.root / relative).read_bytes(), data)
        self.assertEqual(stat.S_IMODE(executable.stat().st_mode), 0o755)
        self.assertEqual(ignored.read_bytes(), pointer(mesh))
        self.assertEqual(historical.read_bytes(), pointer(scene))
        self.assertEqual(traversal.read_bytes(), pointer(scene))
        self.assertFalse(any("smudge" in call.args[0] for call in git.call_args_list))
        self.assertFalse(list(self.root.rglob(".lfs-hydrate-*")))

    def test_exact_download_reuses_scoped_token_without_logging_or_persisting_it(self):
        data = b"\0UnityFS downloaded binary mesh" * 100
        target = self.file("Assets/mesh [1], 한국.asset", pointer(data))
        token = "test-only-session-token"
        calls = []

        def git(args, **kwargs):
            calls.append(args)
            if "smudge" not in args:
                return self.git(args, **kwargs)
            self.assertEqual(args, ["git", "-c", "lfs.fetchinclude=", "-c", "lfs.fetchexclude=",
                                    "-c", "lfs.skipdownloaderrors=false", "lfs", "smudge", "--", self.files[0]])
            self.assertEqual(kwargs["input"], pointer(data))
            self.assertEqual(kwargs["stdout"], subprocess.DEVNULL)
            self.assertEqual(kwargs["stderr"], subprocess.DEVNULL)
            self.assertNotIn(token, " ".join(args))
            env = kwargs["env"]
            self.assertEqual(env["GIT_CONFIG_COUNT"], "2")
            self.assertEqual(env["GIT_CONFIG_KEY_0"], "lfs.storage")
            self.assertEqual(env["GIT_CONFIG_KEY_1"], "http.https://github.com/.extraheader")
            encoded = env["GIT_CONFIG_VALUE_1"].removeprefix("AUTHORIZATION: basic ")
            self.assertEqual(base64.b64decode(encoded), ("x-access-token:" + token).encode())
            self.assertNotIn("GIT_LFS_SKIP_SMUDGE", env)
            self.cache(data)
            return subprocess.CompletedProcess(args, 0, b"", b"")

        output = io.StringIO()
        with mock.patch.dict(os.environ, {"GITHUB_TOKEN": token, "GIT_CONFIG_COUNT": "1",
                                         "GIT_CONFIG_KEY_0": "lfs.storage", "GIT_CONFIG_VALUE_0": "existing",
                                         "GIT_LFS_SKIP_SMUDGE": "1"}), \
                mock.patch.object(hydrate.subprocess, "run", side_effect=git), \
                contextlib.redirect_stdout(output):
            self.assertEqual(hydrate.main(), 0)
            self.assertEqual(os.environ["GIT_CONFIG_COUNT"], "1")
            self.assertNotIn("GIT_CONFIG_KEY_1", os.environ)
        self.assertEqual(target.read_bytes(), data)
        self.assertNotIn(token, output.getvalue())
        self.assertEqual(sum("smudge" in args for args in calls), 1)
        self.assertFalse(any("fetch" in args or "config" in args for args in calls))

    def test_all_objects_must_verify_before_any_pointer_is_replaced(self):
        data = b"correct contents"
        first = self.file("Assets/first.asset", pointer(data))
        self.cache(data)
        for bad_bytes, declared_size, message in [(b"corrupt", len(data), "SHA-256 mismatch"),
                                                   (data, 1, "size mismatch")]:
            with self.subTest(message=message):
                second = self.file("Assets/second.asset", pointer(data, declared_size))
                self.cache(data).write_bytes(bad_bytes)
                with mock.patch.object(hydrate.subprocess, "run", side_effect=self.git), \
                        self.assertRaisesRegex(hydrate.LfsTextAssetError, message):
                    hydrate.hydrate()
                self.assertEqual(first.read_bytes(), pointer(data))
                self.assertEqual(second.read_bytes(), pointer(data, declared_size))

    def test_cache_change_after_verification_cannot_write_corrupt_bytes(self):
        data = b"verified bytes"
        target = self.file("Assets/a.asset", pointer(data))
        cached = self.cache(data)

        def changed_cache():
            cached.write_bytes(b"changed")
            return self.storage

        with mock.patch.object(hydrate.subprocess, "run", side_effect=self.git), \
                mock.patch.object(hydrate, "media_directory", side_effect=changed_cache), \
                self.assertRaisesRegex(hydrate.LfsTextAssetError, "cached object changed"):
            hydrate.hydrate()
        self.assertEqual(target.read_bytes(), pointer(data))
        self.assertFalse(list(self.root.rglob(".lfs-hydrate-*")))

    def test_changed_checkout_file_is_preserved(self):
        data = b"verified bytes"
        target = self.file("Assets/a.asset", pointer(data))
        self.cache(data)

        def changed_pointer():
            target.write_bytes(b"new checkout edit")
            return self.storage

        with mock.patch.object(hydrate.subprocess, "run", side_effect=self.git), \
                mock.patch.object(hydrate, "media_directory", side_effect=changed_pointer), \
                self.assertRaisesRegex(hydrate.LfsTextAssetError, "pointer changed"):
            hydrate.hydrate()
        self.assertEqual(target.read_bytes(), b"new checkout edit")

    def test_symlink_pointer_cannot_overwrite_its_target(self):
        data = b"verified bytes"
        target = self.file("outside.asset", pointer(data))
        link = self.root / "Assets/link.asset"
        link.parent.mkdir()
        try:
            link.symlink_to(target)
        except OSError:
            self.skipTest("symlink creation unavailable on this test host")
        self.files.append("Assets/link.asset")
        with mock.patch.object(hydrate.subprocess, "run", side_effect=self.git) as git:
            self.assertEqual(hydrate.hydrate(), (0, 0))
        self.assertEqual(git.call_count, 1)
        self.assertEqual(target.read_bytes(), pointer(data))

    def test_git_failure_is_nonzero_without_leaking_raw_output(self):
        error = subprocess.CompletedProcess([], 1, b"", b"private diagnostic token")
        output = io.StringIO()
        with mock.patch.object(hydrate.subprocess, "run", return_value=error), \
                contextlib.redirect_stderr(output):
            self.assertEqual(hydrate.main(), 1)
        self.assertIn("Cannot list tracked Unity project files", output.getvalue())
        self.assertNotIn("private diagnostic", output.getvalue())


if __name__ == "__main__":
    unittest.main()
