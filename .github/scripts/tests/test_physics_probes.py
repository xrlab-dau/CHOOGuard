"""Physics probe verdict: failures, stale evidence and drift beyond one integration step."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import physics_probes  # noqa: E402

RECEIPT = {
    "script_sha256": "abc",
    "source_digests": {"sources/SocialForceModel-v1.4.2.cpp": "d1"},
    "results": [
        {"case": "free-0", "passed": True, "displacement_m": 40.0},
        {"case": "bottleneck-1.0", "passed": True, "elapsed_s": 40.8},
        {"case": "width-consistency", "passed": True, "narrow_s": 40.8, "wide_s": 26.0},
    ],
}


class CompareTests(unittest.TestCase):
    def test_reproduced_evidence_passes(self):
        fresh = copy.deepcopy(RECEIPT)
        fresh["results"][1]["elapsed_s"] = 40.85  # exactly one dt later is still the same outcome
        self.assertEqual(physics_probes.compare(RECEIPT, fresh, "abc"), [])

    def test_failed_probe_and_changed_outcome(self):
        fresh = copy.deepcopy(RECEIPT)
        fresh["results"][0]["passed"] = False
        errors = physics_probes.compare(RECEIPT, fresh, "abc")
        self.assertEqual(len(errors), 2)
        self.assertTrue(errors[0].startswith("probe free-0 failed"))
        self.assertIn("outcome changed: True -> False", errors[1])

    def test_stale_receipt_after_script_or_source_edit(self):
        fresh = copy.deepcopy(RECEIPT)
        fresh["source_digests"] = {"sources/SocialForceModel-v1.4.2.cpp": "d2"}
        errors = physics_probes.compare(RECEIPT, fresh, "edited")
        self.assertEqual(len(errors), 2)

    def test_drift_beyond_one_step(self):
        fresh = copy.deepcopy(RECEIPT)
        fresh["results"][2]["wide_s"] = 26.2
        self.assertEqual(physics_probes.compare(RECEIPT, fresh, "abc"), ["probe width-consistency wide_s moved 26.000 -> 26.200 s (more than one dt)"])

    def test_new_case_needs_committed_evidence(self):
        fresh = copy.deepcopy(RECEIPT)
        fresh["results"].append({"case": "stairs", "passed": True})
        self.assertEqual(physics_probes.compare(RECEIPT, fresh, "abc"), ["probe stairs has no committed evidence"])


if __name__ == "__main__":
    unittest.main()
