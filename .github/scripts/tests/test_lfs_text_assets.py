"""Force Text must be verified against LFS contents, not trusted pointers."""
import base64
import hashlib
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import lfs_text_assets as lfs


def pointer(oid, size):
    return f"version https://git-lfs.github.com/spec/v1\noid sha256:{oid}\nsize {size}\n".encode()


class LfsTextAssetsTests(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.storage = Path(tmp.name)

    def object(self, data):
        oid = hashlib.sha256(data).hexdigest()
        target = self.storage / oid[:2] / oid[2:4] / oid
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        return oid, target

    def env(self):
        return subprocess.CompletedProcess([], 0, f"LocalMediaDir={self.storage}\n".encode(), b"")

    def test_regular_yaml_needs_no_lfs_process(self):
        heads = {"Assets/a.unity": b"%YAML 1.1\n"}
        with mock.patch.object(lfs.subprocess, "run") as run:
            self.assertEqual(lfs.resolve_lfs_text_assets(heads, "tip"), heads)
        run.assert_not_called()

    def test_shared_object_verified_and_resolved_without_fetch(self):
        data = b"%YAML 1.1\nscene"
        oid, _ = self.object(data)
        heads = {"Assets/a.unity": pointer(oid, len(data)), ".checkpoints/a.unity": pointer(oid, len(data))}
        with mock.patch.object(lfs.subprocess, "run", return_value=self.env()) as run:
            result = lfs.resolve_lfs_text_assets(heads, "tip")
        self.assertEqual(result, {p: b"%YAML 1." for p in heads})
        run.assert_called_once()

    def test_binary_lfs_content_is_not_converted_into_yaml(self):
        data = b"\x00\x00UnityFS"
        oid, _ = self.object(data)
        with mock.patch.object(lfs.subprocess, "run", return_value=self.env()):
            result = lfs.resolve_lfs_text_assets({"a.unity": pointer(oid, len(data))}, "tip")
        self.assertFalse(result["a.unity"].startswith(b"%YAML"))

    def test_missing_shared_object_smudges_exact_pointer_once_without_ref_scan(self):
        data = b"%YAML 1.1\nnew"
        oid = hashlib.sha256(data).hexdigest()
        calls = []

        def run(args, **kwargs):
            calls.append((args, kwargs))
            if args == ["git", "lfs", "env"]:
                return self.env()
            self.assertEqual(args, ["git", "-c", "lfs.fetchinclude=", "-c", "lfs.fetchexclude=",
                                    "-c", "lfs.skipdownloaderrors=false", "lfs", "smudge", "--", "Scene [1], v2.unity"])
            self.assertEqual(kwargs["input"], pointer(oid, len(data)))
            self.assertEqual(kwargs["stdout"], subprocess.DEVNULL)
            self.assertEqual(kwargs["stderr"], subprocess.DEVNULL)
            self.assertEqual(kwargs["timeout"], 180)
            self.assertNotIn("GIT_LFS_SKIP_SMUDGE", kwargs["env"])
            self.assertNotIn("GIT_LFS_SKIP_DOWNLOAD_ERRORS", kwargs["env"])
            self.assertEqual(kwargs["env"]["LFS_TEST_KEEP"], "kept")
            self.object(data)
            return subprocess.CompletedProcess(args, 0, b"", b"")

        heads = {"Scene [1], v2.unity": pointer(oid, len(data)), "checkpoint/Scene.unity": pointer(oid, len(data))}
        with mock.patch.object(lfs.subprocess, "run", side_effect=run), mock.patch.dict(os.environ, {
            "GIT_LFS_SKIP_SMUDGE": "1", "GIT_LFS_SKIP_DOWNLOAD_ERRORS": "1", "LFS_TEST_KEEP": "kept",
        }):
            result = lfs.resolve_lfs_text_assets(heads, "exact-sha")
        self.assertEqual(result, {path: b"%YAML 1." for path in heads})
        self.assertEqual(len(calls), 2)  # env and one exact-object download
        self.assertNotIn("exact-sha", calls[1][0])
        self.assertNotIn("fetch", calls[1][0])
        self.assertNotIn("checkout", calls[1][0])

    def test_failed_smudge_and_still_missing_object_fail_closed(self):
        oid = "a" * 64
        for code in (0, 1):
            with self.subTest(code=code), mock.patch.object(lfs.subprocess, "run", side_effect=[
                self.env(), subprocess.CompletedProcess([], code, b"", b"")
            ]), self.assertRaises(lfs.LfsTextAssetError):
                lfs.resolve_lfs_text_assets({"a.unity": pointer(oid, 8)}, "tip")

    def test_ci_auth_is_process_scoped_and_preserves_existing_git_configuration(self):
        data = b"%YAML 1.1\n"
        oid = hashlib.sha256(data).hexdigest()
        token = "test-ci-session"

        def run(args, **kwargs):
            if args == ["git", "lfs", "env"]:
                return self.env()
            self.assertNotIn(token, " ".join(args))
            env = kwargs["env"]
            self.assertEqual(env["GIT_CONFIG_COUNT"], "2")
            self.assertEqual(env["GIT_CONFIG_KEY_0"], "lfs.storage")
            self.assertEqual(env["GIT_CONFIG_VALUE_0"], "existing-cache")
            self.assertEqual(env["GIT_CONFIG_KEY_1"], "http.https://github.com/.extraheader")
            encoded = env["GIT_CONFIG_VALUE_1"].removeprefix("AUTHORIZATION: basic ")
            self.assertEqual(base64.b64decode(encoded), ("x-access-token:" + token).encode())
            self.object(data)
            return subprocess.CompletedProcess(args, 0, b"", b"")

        with mock.patch.dict(os.environ, {"GITHUB_TOKEN": token, "GIT_CONFIG_COUNT": "1",
                                         "GIT_CONFIG_KEY_0": "lfs.storage", "GIT_CONFIG_VALUE_0": "existing-cache"}), \
                mock.patch.object(lfs.subprocess, "run", side_effect=run):
            lfs.resolve_lfs_text_assets({"a.unity": pointer(oid, len(data))}, "tip")
            self.assertEqual(os.environ["GIT_CONFIG_COUNT"], "1")
            self.assertNotIn("GIT_CONFIG_KEY_1", os.environ)

    def test_download_errors_include_path_and_exit_without_raw_output(self):
        heads = {"Assets/Scene.unity": pointer("a" * 64, 8)}
        with mock.patch.object(lfs.subprocess, "run", side_effect=[
            self.env(), subprocess.CompletedProcess([], 7, b"secret-out", b"secret-error"),
        ]):
            with self.assertRaisesRegex(lfs.LfsTextAssetError, r"Assets/Scene\.unity \(exit 7\)") as raised:
                lfs.resolve_lfs_text_assets(heads, "tip")
        self.assertNotIn("secret", str(raised.exception))
        for error, message in ((subprocess.TimeoutExpired("git", 180), "timed out after 180s"),
                               (OSError("secret-system-error"), "command unavailable")):
            with self.subTest(error=error), mock.patch.object(lfs.subprocess, "run", side_effect=[self.env(), error]):
                with self.assertRaises(lfs.LfsTextAssetError) as raised:
                    lfs.resolve_lfs_text_assets(heads, "tip")
                self.assertIn(message, str(raised.exception))
                self.assertIn("Assets/Scene.unity", str(raised.exception))
                self.assertNotIn("secret", str(raised.exception))

    def test_downloaded_object_hash_size_and_binary_content_are_checked(self):
        for data, stored, declared_size, error in (
            (b"%YAML 1.1\n", b"corrupt", 10, "SHA-256 mismatch"),
            (b"%YAML 1.1\n", b"%YAML 1.1\n", 1, "size mismatch"),
            (b"\x00\x00UnityFS", b"\x00\x00UnityFS", 9, None),
        ):
            with self.subTest(error=error):
                oid = hashlib.sha256(data).hexdigest()
                target = self.storage / oid[:2] / oid[2:4] / oid
                target.unlink(missing_ok=True)

                def run(args, **kwargs):
                    if args == ["git", "lfs", "env"]:
                        return self.env()
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(stored)
                    return subprocess.CompletedProcess(args, 0, b"", b"")

                with mock.patch.object(lfs.subprocess, "run", side_effect=run):
                    if error:
                        with self.assertRaisesRegex(lfs.LfsTextAssetError, error):
                            lfs.resolve_lfs_text_assets({"a.unity": pointer(oid, declared_size)}, "tip")
                    else:
                        result = lfs.resolve_lfs_text_assets({"a.unity": pointer(oid, declared_size)}, "tip")
                        self.assertFalse(result["a.unity"].startswith(b"%YAML"))

    def test_unavailable_lfs_command_is_an_explicit_verification_error(self):
        heads = {"a.unity": pointer("a" * 64, 8)}
        with mock.patch.object(lfs.subprocess, "run", return_value=subprocess.CompletedProcess([], 1, b"", b"")):
            with self.assertRaises(lfs.LfsTextAssetError):
                lfs.resolve_lfs_text_assets(heads, "tip")
        with mock.patch.object(lfs.subprocess, "run", side_effect=subprocess.TimeoutExpired("git", 30)):
            with self.assertRaises(lfs.LfsTextAssetError):
                lfs.resolve_lfs_text_assets(heads, "tip")

    def test_object_hash_and_each_pointer_size_must_match(self):
        data = b"%YAML 1.1\n"
        oid, path = self.object(data)
        with mock.patch.object(lfs.subprocess, "run", return_value=self.env()):
            with self.assertRaisesRegex(lfs.LfsTextAssetError, "size mismatch"):
                lfs.resolve_lfs_text_assets({"a.unity": pointer(oid, len(data)), "b.unity": pointer(oid, 1)}, "tip")
            path.write_bytes(b"corrupt")
            with self.assertRaisesRegex(lfs.LfsTextAssetError, "SHA-256 mismatch"):
                lfs.resolve_lfs_text_assets({"a.unity": pointer(oid, len(data))}, "tip")

    def test_malformed_pointer_remains_invalid_text(self):
        data = b"version https://git-lfs.github.com/spec/v1\noid sha256:../../bad\nsize 1\n"
        with mock.patch.object(lfs.subprocess, "run") as run:
            self.assertEqual(lfs.resolve_lfs_text_assets({"a.unity": data}, "tip"), {"a.unity": data})
        run.assert_not_called()


if __name__ == "__main__":
    unittest.main()
