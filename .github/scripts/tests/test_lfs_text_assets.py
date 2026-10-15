"""Force Text must be verified against LFS contents, not trusted pointers."""
import hashlib
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

    def test_missing_object_fetches_only_needed_text_category(self):
        data = b"%YAML 1.1\nnew"
        oid = hashlib.sha256(data).hexdigest()
        calls = []

        def run(args, **kwargs):
            calls.append(args)
            if args == ["git", "lfs", "env"]:
                return self.env()
            self.object(data)
            return subprocess.CompletedProcess(args, 0, b"", b"")

        with mock.patch.object(lfs.subprocess, "run", side_effect=run):
            result = lfs.resolve_lfs_text_assets({"Scene [1], v2.unity": pointer(oid, len(data))}, "exact-sha")
        self.assertEqual(result["Scene [1], v2.unity"], b"%YAML 1.")
        self.assertIn("--include=*.unity", calls[1])
        self.assertEqual(calls[1][-2:], ["origin", "exact-sha"])
        self.assertNotIn("checkout", calls[1])

    def test_failed_fetch_and_still_missing_object_fail_closed(self):
        oid = "a" * 64
        for code in (0, 1):
            with self.subTest(code=code), mock.patch.object(lfs.subprocess, "run", side_effect=[
                self.env(), subprocess.CompletedProcess([], code, b"", b"")
            ]), self.assertRaises(lfs.LfsTextAssetError):
                lfs.resolve_lfs_text_assets({"a.unity": pointer(oid, 8)}, "tip")

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
