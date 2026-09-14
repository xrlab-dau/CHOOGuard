"""Synthetic records only: no user logs, credentials, captures, or network."""
import copy
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import record_pipeline as pipeline


def event(content="synthetic harmless detail", classification="public", **changes):
    return {"kind": "test", "outcome": "passed", "classification": classification,
            "content": content, "requiredForEvidence": False, **changes}


def raw(*events):
    return {"schemaVersion": 1, "state": "raw", "events": list(events or [event()])}


class RedactionTests(unittest.TestCase):
    def test_classified_sensitive_values_leave_only_reason_and_policy(self):
        values = [event("fake-secret-value", "secret"),
                  event("fixture.person@example.invalid", "personal"),
                  event("C:/private-fixture/run.txt", "absolute_path"),
                  event("synthetic restricted station metadata", "restricted")]
        original = raw(*values)
        redacted = pipeline.redact(original, "a" * 64)
        self.assertEqual(original, raw(*values))
        for index, record in enumerate(redacted["events"]):
            self.assertNotIn("content", record)
            self.assertEqual(record["removal"]["reason"], values[index]["classification"])
            self.assertEqual(record["removal"]["retentionPolicy"], pipeline.POLICY)

    def test_obvious_sensitive_text_misclassified_public_is_removed(self):
        for value in ("token=synthetic-fixture", "user@example.invalid", "C:\\private\\trace",
                      "/Users/fixture/capture", "../private-data/capture", "Bearer fake-token"):
            with self.subTest(value=value):
                result = pipeline.redact(raw(event(value)), "a" * 64)
                self.assertNotIn("content", result["events"][0])
                self.assertEqual(result["events"][0]["removal"]["reason"], "suspected_sensitive")

    def test_public_projection_never_copies_free_text_or_raw_hash(self):
        result = pipeline.redact(raw(event("unrecognizable-sensitive-canary")), "a" * 64)
        public = pipeline.project_public(result)
        encoded = json.dumps(public)
        self.assertNotIn("unrecognizable-sensitive-canary", encoded)
        self.assertNotIn("a" * 64, encoded)
        self.assertEqual(public["publicTextOmitted"], 1)
        self.assertEqual(public["approval"], "pending")

    def test_unknown_classification_and_required_content_stop(self):
        for sample in (event(classification="unknown"), event(requiredForEvidence=True),
                       event("fake-secret", "secret", requiredForEvidence=True)):
            with self.subTest(sample=sample):
                with self.assertRaises(pipeline.RecordError):
                    pipeline.redact(raw(sample), "a" * 64)

    def test_unknown_fields_bad_types_and_control_values_are_rejected(self):
        samples = [raw(event(kind="C:/private")), raw(event(outcome="secret")),
                   raw(event(classification="made-up")), raw(event(content={"key": "value"})),
                   raw(event(requiredForEvidence="false"))]
        extra = raw()
        extra["privatePath"] = "C:/private"
        samples.append(extra)
        extra = raw()
        extra["schemaVersion"] = True
        samples.append(extra)
        for sample in samples:
            with self.subTest(sample=sample), self.assertRaises(pipeline.RecordError):
                pipeline.redact(sample, "a" * 64)

    def test_all_event_kinds_and_outcomes_survive_as_typed_metadata(self):
        for kind in pipeline.KINDS:
            for outcome in pipeline.OUTCOMES:
                public = pipeline.project_public(pipeline.redact(raw(event(kind=kind, outcome=outcome)), "a" * 64))
                self.assertEqual(public["events"][0]["kind"], kind)
                self.assertEqual(public["events"][0]["outcome"], outcome)
                self.assertEqual(public["approval"], "pending")


class BundleTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.source = self.root / "raw.json"
        self.private = self.root / "private-run"
        self.public = self.root / "candidate-run"
        self.source.write_text(json.dumps(raw(event(), event("fake-key", "secret"))), encoding="utf-8")

    def build(self):
        return pipeline.build(self.source, self.private, self.public)

    def test_build_preserves_raw_and_hashes_only_public_payload(self):
        before = self.source.read_bytes()
        result = self.build()
        self.assertEqual(before, self.source.read_bytes())
        self.assertEqual(pipeline.verify(self.public), result)
        self.assertEqual(set(result["files"]), {"events.json"})
        self.assertNotIn("manifest.json", result["files"])
        self.assertEqual(result["approval"], "pending")
        self.assertEqual(set(p.name for p in self.public.iterdir()), {"events.json", "manifest.json"})
        private = json.loads((self.private / "redacted.json").read_text())
        self.assertEqual(private["rawSha256"], pipeline.digest(before))
        self.assertNotIn("fake-key", (self.public / "events.json").read_text())

    def test_prior_outputs_are_never_overwritten(self):
        self.build()
        before = {p: p.read_bytes() for p in self.root.rglob("*.json")}
        with self.assertRaises((FileExistsError, pipeline.RecordError)):
            self.build()
        self.assertEqual(before, {p: p.read_bytes() for p in self.root.rglob("*.json")})

    @unittest.skipUnless(os.name == "posix", "requires POSIX permission bits")
    def test_private_output_is_owner_only_even_with_permissive_umask(self):
        previous = os.umask(0)
        try:
            self.build()
        finally:
            os.umask(previous)
        self.assertEqual(stat.S_IMODE(self.private.stat().st_mode), 0o700)
        self.assertEqual(stat.S_IMODE((self.private / "redacted.json").stat().st_mode), 0o600)

    def test_existing_public_destination_does_not_create_private_output(self):
        self.public.mkdir()
        (self.public / "keep.txt").write_text("preserve")
        with self.assertRaises((FileExistsError, pipeline.RecordError)):
            self.build()
        self.assertFalse(self.private.exists())

    def test_overlapping_private_public_or_input_paths_are_rejected(self):
        for private, public in ((self.public, self.public), (self.private, self.private / "public"),
                                (self.public / "private", self.public), (self.source, self.public)):
            with self.subTest(private=private, public=public), self.assertRaises(pipeline.RecordError):
                pipeline.build(self.source, private, public)

    def test_tampered_payload_is_rejected(self):
        self.build()
        (self.public / "events.json").write_text("{}")
        with self.assertRaises(pipeline.RecordError):
            pipeline.verify(self.public)

    def test_added_private_file_is_rejected(self):
        self.build()
        (self.public / "extra.txt").write_text("private")
        with self.assertRaises(pipeline.RecordError):
            pipeline.verify(self.public)

    def test_changed_manifest_self_hash_and_approval_are_rejected(self):
        original = self.build()
        for field, value in (("approval", "approved"), ("rawSha256", "a" * 64),
                             ("files", {"manifest.json": {"sha256": "a" * 64, "bytes": 1}})):
            changed = copy.deepcopy(original)
            changed[field] = value
            (self.public / "manifest.json").write_text(json.dumps(changed))
            with self.subTest(field=field), self.assertRaises(pipeline.RecordError):
                pipeline.verify(self.public)

    def test_unknown_or_required_input_emits_no_artifact(self):
        self.source.write_text(json.dumps(raw(event(classification="unknown"))))
        with self.assertRaises(pipeline.RecordError):
            self.build()
        self.assertFalse(self.private.exists())
        self.assertFalse(self.public.exists())

    def test_duplicate_json_key_is_rejected(self):
        self.source.write_text('{"schemaVersion":1,"schemaVersion":1,"state":"raw","events":[]}')
        with self.assertRaises(pipeline.RecordError):
            self.build()

    def test_invalid_unicode_and_nonfinite_json_leave_no_output(self):
        for value in (json.dumps(raw(event("\ud800"))), '{"schemaVersion":NaN,"state":"raw","events":[]}'):
            self.source.write_text(value)
            with self.subTest(value=value), self.assertRaises(pipeline.RecordError):
                self.build()
            self.assertFalse(self.private.exists())
            self.assertFalse(self.public.exists())

    def test_raw_or_private_records_inside_source_checkout_are_rejected(self):
        for checkout in (self.root, self.private):
            with self.subTest(checkout=checkout), patch.object(pipeline, "REPO", checkout):
                with self.assertRaises(pipeline.RecordError):
                    self.build()

    def test_rehashed_payload_still_cannot_contain_free_text(self):
        self.build()
        path = self.public / "events.json"
        record = json.loads(path.read_text())
        record["events"][0]["content"] = "private-canary"
        payload = pipeline.encoded(record)
        path.write_bytes(payload)
        (self.public / "manifest.json").write_bytes(pipeline.encoded(pipeline._manifest(payload)))
        with self.assertRaises(pipeline.RecordError):
            pipeline.verify(self.public)

    def test_write_failure_leaves_unverified_partial_output_and_refuses_reuse(self):
        write_new = pipeline._write_new

        def fail_manifest(path, data):
            if path.name == "manifest.json":
                raise OSError("synthetic interrupted write")
            write_new(path, data)

        with patch.object(pipeline, "_write_new", side_effect=fail_manifest), self.assertRaises(OSError):
            self.build()
        self.assertTrue((self.public / "events.json").exists())
        with self.assertRaises(pipeline.RecordError):
            pipeline.verify(self.public)
        with self.assertRaises(pipeline.RecordError):
            self.build()

    def test_cli_failure_returns_nonzero_without_echoing_private_input(self):
        self.source.write_text("PRIVATE-CANARY")
        run = subprocess.run([sys.executable, str(Path(pipeline.__file__)), "build", "--raw", str(self.source),
                              "--private-output", str(self.private), "--public-output", str(self.public)],
                             capture_output=True, text=True)
        self.assertNotEqual(run.returncode, 0)
        self.assertNotIn("PRIVATE-CANARY", run.stdout + run.stderr)
        self.assertNotIn(str(self.root), run.stdout + run.stderr)

    def test_integer_limit_decode_failure_is_a_record_error(self):
        previous = sys.get_int_max_str_digits()
        try:
            sys.set_int_max_str_digits(640)
            with self.assertRaisesRegex(pipeline.RecordError, "invalid UTF-8 JSON record"):
                pipeline.decode(b'{"schemaVersion":' + b'9' * 641 + b'}')
        finally:
            sys.set_int_max_str_digits(previous)

    def test_integer_limit_cli_failure_is_sanitized_and_emits_no_artifacts(self):
        self.source.write_bytes(b'{"schemaVersion":' + b'9' * 641 + b'}')
        run = subprocess.run([sys.executable, "-X", "int_max_str_digits=640", str(Path(pipeline.__file__)),
                              "build", "--raw", str(self.source), "--private-output", str(self.private),
                              "--public-output", str(self.public)], capture_output=True, text=True)
        self.assertNotEqual(run.returncode, 0)
        self.assertIn("invalid UTF-8 JSON record", run.stdout + run.stderr)
        for text in ("Traceback", str(self.root), str(Path(pipeline.__file__).parent), "9" * 641):
            self.assertNotIn(text, run.stdout + run.stderr)
        self.assertFalse(self.private.exists())
        self.assertFalse(self.public.exists())


if __name__ == "__main__":
    unittest.main()
