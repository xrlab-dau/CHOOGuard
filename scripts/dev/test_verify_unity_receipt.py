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
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

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

    def test_redacted_xml_still_parses_and_keeps_its_tallies(self):
        # Redaction used to substitute on the raw text, which put the placeholder's angle
        # brackets inside attribute values and left the published evidence unparseable.
        xml = self.root / "raw.xml"
        xml.write_text(
            '<?xml version="1.0" encoding="utf-8" standalone="no"?>\n'
            '<test-run id="2" result="Passed" total="3" passed="2" failed="0" skipped="1"\n'
            '          inconclusive="0" duration="12.5" engine-version="3.5.0.0">\n'
            '  <test-suite type="TestFixture" name="SceneBuilderTests">\n'
            '    <test-case name="OwnedOutputBuilds"\n'
            '               fullname="C:\\Temp\\checkout-abc/Demo.OwnedOutputBuilds" result="Passed" />\n'
            '    <test-case name="ForeignOutputRefused" result="Passed" />\n'
            '    <test-case name="SourceAvailableBuild" result="Skipped">\n'
            "      <reason><message>Local reconstruction output is intentionally not "
            "Git-tracked</message></reason>\n"
            "    </test-case>\n"
            "  </test-suite>\n"
            "</test-run>\n",
            encoding="utf-8",
        )
        redacted = self.root / "clean.xml"
        self.run_powershell(
            f"""
Initialize-Redactions -CheckoutPath 'C:\\Temp\\checkout-abc' -UnityExe 'C:\\Editors\\6000\\Editor\\Unity.exe'
[System.IO.File]::WriteAllText('{redacted}', (Protect-XmlDocument '{xml}'))
"""
        )
        text = redacted.read_text(encoding="utf-8")
        self.assertNotIn("checkout-abc", text)
        self.assertIn("<CHECKOUT>", text.replace("&lt;", "<").replace("&gt;", ">"))
        # A dotted version is not an address; redacting it would corrupt the run metadata.
        self.assertIn('engine-version="3.5.0.0"', text)

        root = ET.parse(redacted).getroot()
        self.assertEqual(root.get("total"), "3")
        self.assertEqual(root.get("passed"), "2")
        self.assertEqual(root.get("skipped"), "1")
        names = [c.get("name") for c in root.iter("test-case")]
        self.assertIn("SourceAvailableBuild", names)
        reason = root.find(".//test-case[@result='Skipped']/reason/message")
        self.assertIn("not Git-tracked", reason.text)

    def test_a_run_without_executed_cases_is_not_a_pass(self):
        cases = {
            "empty": ("total=0; passed=0; failed=0; skipped=0; inconclusive=0", 0, "not_run"),
            "all_skipped": ("total=4; passed=0; failed=0; skipped=4; inconclusive=0", 0, "not_run"),
            "all_inconclusive": ("total=2; passed=0; failed=0; skipped=0; inconclusive=2", 0, "not_run"),
            "failing": ("total=4; passed=3; failed=1; skipped=0; inconclusive=0", 2, "fail"),
            "exit_disagrees": ("total=4; passed=4; failed=0; skipped=0; inconclusive=0", 3, "fail"),
            "healthy": ("total=119; passed=118; failed=0; skipped=1; inconclusive=0", 0, "pass"),
        }
        for label, (fields, exit_code, expected) in cases.items():
            with self.subTest(label):
                assignments = "; ".join(f"{k.strip()}" for k in fields.split(";"))
                output = self.run_powershell(
                    f"$s = [pscustomobject]@{{ {assignments.replace('=', '=')} }}; "
                    f"(Resolve-TestRunStatus -Summary $s -ExitCode {exit_code}).status"
                )
                self.assertEqual(output.strip(), expected)

    def test_refusal_needs_the_ownership_message_not_just_a_non_zero_exit(self):
        base = {
            "sentinelPreserved": "$true",
            "ownershipMarkerCreated": "$false",
            "playerCreated": "$false",
            "foreignTreeEntries": "1",
            "refusalMessageInLog": "$true",
        }
        cases = {
            "refused": (dict(base), 1, "pass"),
            "unrelated_failure": ({**base, "refusalMessageInLog": "$false"}, 1, "fail"),
            "sentinel_changed": ({**base, "sentinelPreserved": "$false"}, 1, "fail"),
            "marker_written": ({**base, "ownershipMarkerCreated": "$true"}, 1, "fail"),
            "player_written": ({**base, "playerCreated": "$true"}, 1, "fail"),
            "extra_file_left": ({**base, "foreignTreeEntries": "2"}, 1, "fail"),
            "built_anyway": (dict(base), 0, "fail"),
        }
        for label, (results, exit_code, expected) in cases.items():
            with self.subTest(label):
                fields = "; ".join(f"{k} = {v}" for k, v in results.items())
                output = self.run_powershell(
                    f"$r = [pscustomobject]@{{ {fields} }}; "
                    f"(Resolve-RefusalStatus -ExitCode {exit_code} -Results $r).status"
                )
                self.assertEqual(output.strip(), expected)

    def test_paths_with_spaces_cross_the_process_boundary_as_one_argument(self):
        # PowerShell joins an -ArgumentList array with spaces and adds no quoting, so this
        # has to be checked against a real child process, not against the produced string.
        probe = self.root / "argv.py"
        probe.write_text("import json, sys\nprint(json.dumps(sys.argv[1:]))\n", encoding="utf-8")
        for label, workspace in (("with_space", r"C:\Unity Verification\ws"), ("plain", r"C:\ws")):
            with self.subTest(label):
                out = self.root / f"argv-{label}.txt"
                self.run_powershell(
                    f"""
$line = ConvertTo-ProcessArgumentLine @('{probe}', '-projectPath', '{workspace}', '-logFile', '{workspace}\\a.log')
Start-Process -FilePath '{sys.executable}' -ArgumentList $line -Wait -NoNewWindow -RedirectStandardOutput '{out}'
"""
                )
                argv = json.loads(out.read_text(encoding="utf-8"))
                self.assertEqual(argv, ["-projectPath", workspace, "-logFile", workspace + "\\a.log"])

    def test_missing_result_xml_is_not_read_as_a_pass(self):
        output = self.run_powershell(
            f"$value = Read-NUnitSummary '{self.root / 'absent.xml'}'; "
            "if ($null -eq $value) { 'null' } else { 'value' }"
        )
        self.assertEqual(output.strip(), "null")


if __name__ == "__main__":
    unittest.main()
