import tempfile
import unittest
from pathlib import Path
from native_manifest import contained_file, manifest, verify, write_new


class NativeManifestTests(unittest.TestCase):
    def test_relocated_payload_and_changed_or_missing_bytes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "sample.cs").write_text("original")
            record = manifest(root, ["sample.cs"], "fixture")
            self.assertEqual(verify(root, record), 1)
            (root / "sample.cs").write_text("changed")
            with self.assertRaises(ValueError):
                verify(root, record)
            (root / "sample.cs").unlink()
            with self.assertRaises(ValueError):
                verify(root, record)

    def test_linked_and_nonportable_paths_are_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for name in ("../other", "/outside", "C:/outside", "a\\b", ".git/config"):
                with self.assertRaises(ValueError):
                    contained_file(root, name)
            (root / "data").write_text("data")
            (root / "link").symlink_to(root / "data")
            with self.assertRaises(ValueError):
                contained_file(root, "link")

    def test_existing_receipt_is_not_overwritten(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "receipt.json"
            write_new(path, {"preserve": True})
            before = path.read_bytes()
            with self.assertRaises(FileExistsError):
                write_new(path, {"preserve": False})
            self.assertEqual(path.read_bytes(), before)


if __name__ == "__main__":
    unittest.main()
