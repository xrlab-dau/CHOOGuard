import hashlib
import tempfile
import unittest
from pathlib import Path

from prepare_multiplayer_dependencies import guarded_patch, verified_write


class MultiplayerDependencyTests(unittest.TestCase):
    def test_corrupt_download_never_replaces_target(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "library.dll"
            path.write_bytes(b"original pointer")
            with self.assertRaises(ValueError):
                verified_write(path, b"corrupt", hashlib.sha256(b"expected").hexdigest(), 8)
            self.assertEqual(path.read_bytes(), b"original pointer")

    def test_exact_binary_materialization_is_repeatable(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "library.dll"
            data = b"expected"
            for _ in range(2):
                verified_write(path, data, hashlib.sha256(data).hexdigest(), len(data))
            self.assertEqual(path.read_bytes(), data)

    def test_source_drift_patch_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "runtime.asmdef"
            path.write_bytes(b"unexpected")
            with self.assertRaises(ValueError):
                guarded_patch(path, "0" * 64, b"patched")
            self.assertEqual(path.read_bytes(), b"unexpected")

    def test_guarded_patch_is_idempotent(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "runtime.asmdef"
            path.write_bytes(b"original")
            before = hashlib.sha256(b"original").hexdigest()
            self.assertTrue(guarded_patch(path, before, b"patched"))
            self.assertFalse(guarded_patch(path, before, b"patched"))


if __name__ == "__main__":
    unittest.main()
