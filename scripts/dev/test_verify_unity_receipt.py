"""Check the receipt helpers that decide what leaves the machine.

Redaction and result parsing are the two places where a mistake would either publish a
personal path or turn an excluded test into a reported pass, so they are exercised
directly instead of being trusted after a full Unity run.
"""
from __future__ import annotations

import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

SCRIPT = Path(__file__).with_name("verify_unity_receipt.ps1")

NUNIT_FIXTURE = """<?xml version="1.0" encoding="utf-8"?>
<test-run id="2" result="Passed" total="3" passed="2" failed="0" skipped="1"
          inconclusive="0" duration="12.5">
  <test-suite type="TestFixture" name="SceneBuilderTests">
    <test-case name="OwnedOutputBuilds" fullname="Demo.SceneBuilderTests.OwnedOutputBuilds"
               result="Passed" />
    <test-case name="ForeignOutputRefused"
               fullname="Demo.SceneBuilderTests.ForeignOutputRefused" result="Passed" />
    <test-case name="SourceAvailableBuild"
               fullname="Demo.ReconstructionReviewTests.SourceAvailableBuild" result="Skipped">
      <reason><message>Local reconstruction output is intentionally not Git-tracked</message></reason>
    </test-case>
  </test-suite>
</test-run>
"""


@unittest.skipUnless(os.name == "nt", "the receipt script is a Windows entry point")
class VerifyUnityReceiptHelperTests(unittest.TestCase):
    def setUp(self):
        self.shell = shutil.which("powershell") or shutil.which("pwsh")
        if not self.shell:
            self.skipTest("PowerShell is not installed")
        self.temporary = tempfile.TemporaryDirectory(prefix="verify-receipt-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)

    def run_powershell(self, body: str) -> str:
        script = f". '{SCRIPT}' -AsModule\n{body}\n"
        completed = subprocess.run(
            [self.shell, "-NoProfile", "-NonInteractive", "-Command", script],
            capture_output=True,
            text=True,
            encoding="utf-8",
        )
        self.assertEqual(completed.returncode, 0, completed.stderr)
        return completed.stdout

    def test_literal_paths_are_redacted_in_both_slash_directions(self):
        # Unity writes mixed separators in one log, so a rule built from a Windows path
        # has to catch the forward-slash spelling of the same path as well.
        output = self.run_powershell(
            r"""
Initialize-Redactions -CheckoutPath 'C:\Temp\choo work\checkout-abc' -UnityExe 'C:\Editors\6000\Editor\Unity.exe'
Protect-Text 'opened C:\Temp\choo work\checkout-abc\Assets and C:/Temp/choo work/checkout-abc/Library'
"""
        )
        self.assertNotIn("choo work", output)
        self.assertEqual(output.count("<CHECKOUT>"), 2)

    def test_credential_lines_and_identifiers_are_dropped(self):
        output = self.run_powershell(
            r"""
Initialize-Redactions -CheckoutPath 'C:\Temp\checkout-abc' -UnityExe 'C:\Editors\6000\Editor\Unity.exe'
Protect-Text @'
LICENSE SYSTEM: serial ZZ-1234-5678
project at C:\Users\somebody\Choo owned by somebody@example.com
connected to 10.11.12.13
'@
"""
        )
        self.assertNotIn("ZZ-1234-5678", output)
        self.assertNotIn("somebody@example.com", output)
        self.assertNotIn("10.11.12.13", output)
        self.assertIn("<HOME>", output)

    def test_serialized_test_names_survive_the_credential_filter(self):
        # 'Serialized' contains 'serial'. Without a word boundary the credential rule
        # deletes the exclusion reasons that the receipt is meant to publish.
        output = self.run_powershell(
            r"""
Initialize-Redactions -CheckoutPath 'C:\Temp\checkout-abc' -UnityExe 'C:\Editors\6000\Editor\Unity.exe'
Protect-Text 'SourceAvailableBuildPreservesBoundsOneViewAndSerializedOriginCamera was skipped'
"""
        )
        self.assertIn("SerializedOriginCamera", output)
        self.assertNotIn("redacted", output)

    def test_nunit_summary_reports_totals_and_exclusion_reasons(self):
        xml = self.root / "results.xml"
        xml.write_text(NUNIT_FIXTURE, encoding="utf-8")
        output = self.run_powershell(
            f"Read-NUnitSummary '{xml}' | ConvertTo-Json -Depth 5 -Compress"
        )
        summary = json.loads(output)
        self.assertEqual(summary["total"], 3)
        self.assertEqual(summary["passed"], 2)
        self.assertEqual(summary["failed"], 0)
        self.assertEqual(summary["skipped"], 1)
        self.assertEqual(len(summary["excluded"]), 1)
        self.assertIn("not Git-tracked", summary["excluded"][0]["reason"])

    def test_missing_result_xml_is_not_read_as_a_pass(self):
        output = self.run_powershell(
            f"$value = Read-NUnitSummary '{self.root / 'absent.xml'}'; "
            "if ($null -eq $value) { 'null' } else { 'value' }"
        )
        self.assertEqual(output.strip(), "null")


if __name__ == "__main__":
    unittest.main()
