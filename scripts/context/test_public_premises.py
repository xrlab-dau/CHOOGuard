"""R-09 공개 자료 전제 감사 시험.

검사기의 계약을 고정한다. 특히 회귀가 실제로 났던 지점을 못 박는다.

* 부정 서술("촬영/회신 없이", "촬영 없음")은 **절대** 긍정 전제가 되지 않는다.
* 이력 문장("dated historical snapshot", "과거 계획")은 긍정 전제가 되지 않는다.
* 판정 우선순위는 부정 > 이력 > 긍정이다.
* CLI는 ``--input``·``--output``을 모두 받고, 미판정이 남으면 계약대로 실패한다.

시험은 검사기와 같은 디렉터리에 있고 ``SELF_EXCLUDED``로 스캔에서 빠진다.
"""

from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().with_name("audit_public_premises.py")


def _load_auditor():
    spec = importlib.util.spec_from_file_location(
        "audit_public_premises_under_test", MODULE_PATH
    )
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


A = _load_auditor()


def _audit_root() -> Path:
    """감사 대상 저장소 루트.

    ``docs/``와 ``scripts/``를 함께 가진 상위 디렉터리를 찾되, 후보가 여럿이면
    git 작업 트리(``.git``이 있는 쪽)를 고른다. 검사기가 후보 스테이징 디렉터리
    안에 복사되어 있어도 실제 저장소를 감사하기 위한 장치다.
    """
    fallback = None
    for candidate in MODULE_PATH.parents:
        if not ((candidate / "docs").is_dir() and (candidate / "scripts").is_dir()):
            continue
        if (candidate / ".git").exists():
            return candidate
        if fallback is None:
            fallback = candidate
    return fallback if fallback is not None else MODULE_PATH.parents[2]


WORKTREE = _audit_root()
DISPOSITIONS_PATH = WORKTREE / "docs/context/reviews/public-premise-dispositions.json"
GENERATED_ROOT = WORKTREE


def _run_cli(*args: str):
    """검사기를 실제 CLI로 실행해 ``(returncode, stdout, stderr)``를 낸다."""
    completed = subprocess.run(
        [sys.executable, str(MODULE_PATH), *args],
        capture_output=True,
        text=True,
        check=False,
    )
    return completed.returncode, completed.stdout, completed.stderr


class NegativeAssertionContractTest(unittest.TestCase):
    """요구사항이 명시한 회귀 지점. 부정은 어떤 경우에도 긍정으로 새지 않는다."""

    NEGATIVE_SAMPLES = (
        "촬영/회신 없이",
        "촬영 없음",
        "촬영안함",
        "코레일 제공 없음",
        "코레일 제공 없이 개발한다",
        "KORAIL 회신 없이 착수한다",
        "사전 서면 승인 없이 촬영한다",
        "실제 역사 촬영은 하지 않는다",
        "코레일은 아무것도 제공하지 않는다",
        "촬영 승인을 기다리지 않고 공개 자료로 개발한다",
        "회신을 받지 못했다",
        "제한 도면은 확보하지 못했다",
        "촬영은 현재 계획이 아니다",
    )

    def test_negative_samples_are_never_positive(self):
        for sample in self.NEGATIVE_SAMPLES:
            with self.subTest(sample=sample):
                self.assertNotEqual(
                    A.classify(sample, False),
                    "positive_premise",
                    f"{sample!r} must never be a positive premise",
                )

    def test_촬영_회신_없이_is_negative_exclusion(self):
        self.assertEqual(A.classify("촬영/회신 없이", False), "negative_exclusion")

    def test_촬영_없음_is_negative_exclusion(self):
        self.assertEqual(A.classify("촬영 없음", False), "negative_exclusion")

    def test_코레일_제공_없음_is_negative_exclusion(self):
        self.assertEqual(A.classify("코레일 제공 없음", False), "negative_exclusion")

    def test_scan_never_emits_a_positive_for_a_negative_line(self):
        """스캐너 수준에서도 부정 줄은 긍정 전제로 나오지 않는다."""
        for line in (
            "촬영/회신 없이",
            "촬영 없음",
            "현장 촬영 없음",
            "코레일 제공 없음",
            "실제 역사 촬영은 하지 않는다",
            "촬영안함",
        ):
            with self.subTest(line=line):
                for occurrence in A.scan_text(line, "docs/x.md", False):
                    self.assertNotEqual(
                        occurrence["classification"], "positive_premise"
                    )

    def test_negative_topic_line_is_reported_as_exclusion(self):
        """전제 주제가 걸린 부정은 배제로 기록된다. 미탐이 아니라 근거다."""
        occurrences = A.scan_text("현장 촬영 없음", "docs/x.md", False)
        self.assertEqual(len(occurrences), 1)
        self.assertEqual(occurrences[0]["classification"], "negative_exclusion")

    def test_bare_filming_negations_are_recorded_as_exclusions(self):
        """요구사항이 든 예시 문구가 보고서에 실제로 나타나야 한다."""
        for line in ("촬영/회신 없이", "촬영 없음", "촬영안함"):
            with self.subTest(line=line):
                occurrences = A.scan_text(line, "docs/x.md", False)
                self.assertTrue(occurrences, f"{line!r} 가 탐지되지 않았다")
                self.assertEqual(
                    occurrences[0]["classification"], "negative_exclusion"
                )

    def test_hangul_single_token_is_not_treated_as_path_noise(self):
        """``\\w``는 한글도 단어 문자다. 경로 잡음 판정을 ASCII로 좁혀야 한다."""
        self.assertFalse(A.NOISE_PATTERN.search("촬영안함"))
        self.assertTrue(A.NOISE_PATTERN.search("docs/context/work-graph.json"))

    def test_negative_exclusion_is_not_a_violation(self):
        """배제는 스스로 판정한다. 미판정으로 남지 않는다."""
        occurrence = {
            "path": "docs/example.md",
            "line": 1,
            "topic": "on_site_filming",
            "classification": "negative_exclusion",
            "statement": "촬영 없음",
            "documentHistorical": False,
        }
        findings = A.build_findings([occurrence])
        disposition, _ = A.disposition_for(findings[0])
        self.assertEqual(disposition, A.DISPOSITION_EXCLUDED)
        self.assertIn(disposition, A.RESOLVED_DISPOSITIONS)


class PositivePremiseTest(unittest.TestCase):
    """긍정 전제는 계속 긍정으로 남아야 한다. 부정 강화가 긍정을 삼키면 안 된다."""

    POSITIVE_SAMPLES = (
        "코레일 승인 후 현장 촬영을 수행한다",
        "KORAIL 회신을 받으면 O-항목을 갱신한다",
        "실제 역사 촬영 계획을 수립한다",
        "승인 도면을 확보해 합성 치수를 실측으로 승격한다",
        "촬영 동선과 현장 출입 승인을 준비한다",
        "코레일 제공 자료를 기준선으로 삼는다",
    )

    def test_positive_samples_stay_positive(self):
        for sample in self.POSITIVE_SAMPLES:
            with self.subTest(sample=sample):
                self.assertEqual(A.classify(sample, False), "positive_premise")

    def test_topics_are_detected(self):
        self.assertTrue(A.TOPIC_PATTERNS["korail_provision"].search("코레일 제공 자료"))
        self.assertTrue(A.TOPIC_PATTERNS["on_site_filming"].search("실제 역사 촬영 계획"))
        self.assertTrue(A.TOPIC_PATTERNS["non_public_drawings"].search("승인 도면"))

    def test_topic_guard_rejects_unrelated_provision(self):
        """이수 판정·피드백 제공은 코레일 제공 전제가 아니다."""
        line = "이수 판정 자료 제공 여부를 확인한다"
        match = A.TOPIC_PATTERNS["korail_provision"].search(line)
        self.assertIsNotNone(match, "주제 표지가 먼저 걸려야 가드 시험이 성립한다")
        window = A.statement_window(line, match.start(), match.end())
        self.assertIsNotNone(A.TOPIC_GUARDS["korail_provision"].search(window))
        self.assertEqual(A.scan_text(line, "docs/x.md", False), [])

    def test_document_topic_guard_suppresses_filming_label(self):
        """툴체인 위생 검사의 '촬영 원본'은 금지 파일 부류 이름이다."""
        path = "scripts/bootstrap/verify_toolchain.py"
        self.assertIn(path, A.DOCUMENT_TOPIC_GUARDS)
        self.assertIn("on_site_filming", A.DOCUMENT_TOPIC_GUARDS[path]["topics"])
        text = "FORBIDDEN = ('capture.mp4',)  # 촬영 원본\n"
        self.assertEqual(A.scan_text(text, path, False), [])

    def test_document_topic_guard_does_not_leak_to_other_paths(self):
        text = "승인된 촬영 원본을 확보한다\n"
        found = A.scan_text(text, "docs/other.md", False)
        self.assertTrue(found, "다른 경로에서는 촬영 원본이 계속 전제로 잡혀야 한다")

    def test_window_guard_rejects_forbidden_file_class(self):
        text = "촬영 원본·환경 파일·명백한 자격/라이선스는 의존성 트리 안이라도 금지다\n"
        found = A.scan_text(
            text, "scripts/bootstrap/verify_toolchain.py", False
        )
        self.assertEqual(found, [])


class HistoricalStatementTest(unittest.TestCase):
    def test_line_marker_dated_historical_snapshot(self):
        self.assertEqual(
            A.classify("dated historical snapshot", False), "historical_statement"
        )

    def test_line_marker_과거_계획(self):
        self.assertEqual(A.classify("과거 계획", False), "historical_statement")

    def test_document_header_marks_every_line_historical(self):
        text = "역사 범위 안내 · 2026-09-12\n코레일 승인 후 현장 촬영을 수행한다\n"
        self.assertTrue(A.document_is_historical(text))
        for occurrence in A.scan_text(text, "docs/old.md", True):
            self.assertEqual(occurrence["classification"], "historical_statement")

    def test_document_header_only_scans_head(self):
        text = "\n".join([""] * 80) + "\n역사 범위 안내\n"
        self.assertFalse(A.document_is_historical(text))

    def test_plain_document_is_not_historical(self):
        self.assertFalse(A.document_is_historical("코레일 승인 후 촬영한다\n"))


class PrecedenceTest(unittest.TestCase):
    def test_negative_beats_historical(self):
        self.assertEqual(
            A.classify("과거 계획이지만 촬영 없음", False), "negative_exclusion"
        )

    def test_negative_beats_document_historical(self):
        self.assertEqual(
            A.classify("코레일 제공 없이 개발한다", True), "negative_exclusion"
        )

    def test_historical_beats_positive(self):
        self.assertEqual(
            A.classify("코레일 승인 후 촬영한다 (과거 계획)", False),
            "historical_statement",
        )

    def test_historical_beats_positive_when_document_is_historical(self):
        self.assertEqual(
            A.classify("코레일 승인 후 촬영한다", True), "historical_statement"
        )


class FindingGroupingTest(unittest.TestCase):
    def _occurrence(self, path, line, statement, classification="positive_premise"):
        return {
            "path": path,
            "line": line,
            "topic": "korail_provision",
            "classification": classification,
            "statement": statement,
            "documentHistorical": False,
        }

    def test_finding_id_is_content_addressed_and_stable(self):
        first = A.finding_id("positive_premise", "korail_provision", "코레일 회신")
        second = A.finding_id("positive_premise", "korail_provision", "코레일 회신")
        other = A.finding_id("positive_premise", "korail_provision", "코레일 승인")
        self.assertEqual(first, second)
        self.assertNotEqual(first, other)

    def test_copied_statement_collapses_into_one_finding(self):
        occurrences = [
            self._occurrence("docs/a.md", 10, "코레일 회신 대기"),
            self._occurrence("docs/b.md", 3, "코레일 회신 대기"),
            self._occurrence("docs/c.md", 7, "코레일 회신 대기"),
        ]
        findings = A.build_findings(occurrences)
        self.assertEqual(len(findings), 1)
        self.assertEqual(findings[0]["occurrenceCount"], 3)

    def test_distinct_statements_stay_separate(self):
        findings = A.build_findings(
            [
                self._occurrence("docs/a.md", 1, "코레일 회신 대기"),
                self._occurrence("docs/a.md", 2, "현장 촬영 승인"),
            ]
        )
        self.assertEqual(len(findings), 2)

    def test_occurrences_carry_every_location(self):
        findings = A.build_findings(
            [
                self._occurrence("docs/b.md", 9, "코레일 회신 대기"),
                self._occurrence("docs/a.md", 2, "코레일 회신 대기"),
            ]
        )
        self.assertEqual(
            findings[0]["occurrences"],
            [{"path": "docs/a.md", "line": 2}, {"path": "docs/b.md", "line": 9}],
        )

    def test_blank_and_noise_lines_are_skipped(self):
        self.assertEqual(A.scan_text("   \n", "docs/a.md", False), [])
        self.assertEqual(
            A.scan_text("grep -n '코레일 회신' docs/\n", "docs/a.md", False), []
        )

    def test_canonical_statement_normalises_whitespace(self):
        self.assertEqual(
            A.canonical_statement("  코레일   회신  대기  "), "코레일 회신 대기"
        )


class DispositionTest(unittest.TestCase):
    def test_positive_without_ruling_is_unresolved(self):
        finding = {
            "classification": "positive_premise",
            "topic": "korail_provision",
            "occurrences": [{"path": "docs/unruled.md", "line": 1}],
        }
        disposition, _ = A.disposition_for(finding)
        self.assertEqual(disposition, A.DISPOSITION_UNRESOLVED)
        self.assertNotIn(disposition, A.RESOLVED_DISPOSITIONS)

    def test_positive_with_ruling_is_resolved(self):
        finding = {
            "classification": "positive_premise",
            "topic": "korail_provision",
            "occurrences": [
                {"path": "docs/choo-guard-requirements-baseline-v1.md", "line": 1}
            ],
        }
        disposition, note = A.disposition_for(finding)
        self.assertEqual(disposition, A.DISPOSITION_SUPERSEDED)
        self.assertTrue(note)

    def test_generated_only_finding_gets_derivative_disposition(self):
        finding = {
            "classification": "positive_premise",
            "topic": "korail_provision",
            "occurrences": [{"path": "docs/context/work-graph.json", "line": 1}],
        }
        disposition, _ = A.disposition_for(finding)
        self.assertEqual(disposition, A.DISPOSITION_GENERATED)

    def test_generated_prefix_covers_every_work_order(self):
        """빌더가 작업지시서를 늘려도 미판정이 생기지 않아야 한다."""
        self.assertTrue(A.is_generated_path("docs/context/work-orders/121.json"))
        self.assertTrue(A.is_generated_path("docs/context/work-orders/999.json"))
        self.assertFalse(A.is_generated_path("docs/context/work-orders.md"))
        self.assertFalse(A.is_generated_path("docs/other/121.json"))

    def test_new_work_order_finding_is_resolved_as_generated(self):
        finding = {
            "classification": "positive_premise",
            "topic": "korail_provision",
            "occurrences": [
                {"path": "docs/context/work-orders/121.json", "line": 174},
                {"path": "docs/context/work-graph.json", "line": 37882},
            ],
        }
        disposition, note = A.disposition_for(finding)
        self.assertEqual(disposition, A.DISPOSITION_GENERATED)
        self.assertTrue(note)

    def test_unknown_topic_is_unresolved(self):
        finding = {
            "classification": "positive_premise",
            "topic": "made_up_topic",
            "occurrences": [{"path": "docs/a.md", "line": 1}],
        }
        disposition, _ = A.disposition_for(finding)
        self.assertEqual(disposition, A.DISPOSITION_UNRESOLVED)

    def test_every_configured_ruling_uses_a_known_disposition(self):
        for path, ruling in A.DOCUMENT_RULINGS.items():
            with self.subTest(path=path):
                self.assertIn(ruling["disposition"], A.RESOLVED_DISPOSITIONS)
                self.assertTrue(ruling["note"].strip())

    def test_document_topic_guards_document_their_reason(self):
        for path, guard in A.DOCUMENT_TOPIC_GUARDS.items():
            with self.subTest(path=path):
                self.assertTrue(guard["topics"])
                self.assertTrue(guard["reason"].strip())
                for topic in guard["topics"]:
                    self.assertIn(topic, A.TOPIC_PATTERNS)


class RegistrySchemaTest(unittest.TestCase):
    def _registry(self, occurrences, scanned=1):
        return A.build_registry(occurrences, scanned_files=scanned, roots=["docs"])

    def _occurrence(self, path, line, statement):
        return {
            "path": path,
            "line": line,
            "topic": "korail_provision",
            "classification": "positive_premise",
            "statement": statement,
            "documentHistorical": False,
        }

    def test_generated_registry_validates(self):
        registry = self._registry(
            [self._occurrence("docs/choo-guard-requirements-baseline-v1.md", 4, "코레일 승인")]
        )
        self.assertEqual(A.validate_registry(registry), [])

    def test_empty_registry_validates_and_has_zero_unresolved(self):
        registry = self._registry([])
        self.assertEqual(A.validate_registry(registry), [])
        self.assertEqual(registry["unresolvedCount"], 0)

    def test_unresolved_count_matches_findings(self):
        registry = self._registry(
            [self._occurrence("docs/unruled.md", 1, "코레일 승인")]
        )
        self.assertEqual(registry["unresolvedCount"], 1)
        self.assertEqual(A.validate_registry(registry), [])

    def test_validation_rejects_unknown_disposition(self):
        registry = self._registry([])
        registry["findings"] = [
            {
                "id": "PP-TEST",
                "classification": "positive_premise",
                "topic": "korail_provision",
                "statement": "코레일 승인",
                "occurrences": [{"path": "docs/a.md", "line": 1}],
                "occurrenceCount": 1,
                "disposition": "made_up",
                "resolved": True,
                "note": "x",
            }
        ]
        self.assertTrue(A.validate_registry(registry))

    def test_validation_rejects_unresolved_count_mismatch(self):
        registry = self._registry([])
        registry["unresolvedCount"] = 5
        self.assertTrue(A.validate_registry(registry))

    def test_validation_rejects_duplicate_ids(self):
        registry = self._registry([])
        entry = {
            "id": "PP-DUP",
            "classification": "negative_exclusion",
            "topic": "korail_provision",
            "statement": "촬영 없음",
            "occurrences": [{"path": "docs/a.md", "line": 1}],
            "occurrenceCount": 1,
            "disposition": A.DISPOSITION_EXCLUDED,
            "resolved": True,
            "note": "x",
        }
        registry["findings"] = [entry, dict(entry)]
        registry["counts"]["findings"] = 2
        self.assertTrue(A.validate_registry(registry))

    def test_counts_agree_with_findings(self):
        registry = self._registry(
            [
                self._occurrence("docs/unruled.md", 1, "코레일 승인"),
                self._occurrence("docs/unruled.md", 2, "코레일 승인"),
            ]
        )
        self.assertEqual(registry["counts"]["findings"], 1)
        self.assertEqual(registry["counts"]["occurrences"], 2)
        self.assertEqual(registry["counts"]["positivePremise"], 1)
        self.assertEqual(A.validate_registry(registry), [])


class ScannerTest(unittest.TestCase):
    def test_iter_text_files_filters_by_suffix(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "a.md").write_text("코레일 회신", encoding="utf-8")
            (root / "b.png").write_bytes(b"\x89PNG")
            names = {path.name for path in A.iter_text_files(root)}
            self.assertEqual(names, {"a.md"})

    def test_iter_text_files_skips_ignored_directories(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "node_modules").mkdir()
            (root / "node_modules" / "a.md").write_text("코레일 회신", encoding="utf-8")
            (root / "docs").mkdir()
            (root / "docs" / "b.md").write_text("코레일 회신", encoding="utf-8")
            names = sorted(path.name for path in A.iter_text_files(root))
            self.assertEqual(names, ["b.md"])

    def test_resolve_roots_splits_repository_root(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "docs").mkdir()
            (root / "scripts").mkdir()
            roots, base = A.resolve_roots(root)
            self.assertEqual([Path(r).name for r in roots], ["docs", "scripts"])
            self.assertEqual(base, root)

    def test_resolve_roots_uses_plain_directory(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "docs").mkdir()
            roots, base = A.resolve_roots(root)
            self.assertEqual(roots, [root])
            self.assertEqual(base, root)

    def test_resolve_roots_accepts_single_file(self):
        with tempfile.TemporaryDirectory() as tmp:
            target = Path(tmp) / "a.md"
            target.write_text("코레일 회신", encoding="utf-8")
            roots, base = A.resolve_roots(target)
            self.assertEqual(roots, [target])
            self.assertEqual(base, target.parent)

    def test_self_excluded_covers_auditor_and_tests(self):
        self.assertIn(
            "scripts/context/audit_public_premises.py", A.SELF_EXCLUDED
        )
        self.assertIn("scripts/context/test_public_premises.py", A.SELF_EXCLUDED)

    def test_self_excluded_covers_its_own_registry_output(self):
        """처분표를 스캔하면 매 실행이 자기 발견을 다시 발견한다."""
        self.assertIn(A.DEFAULT_DISPOSITIONS_PATH, A.SELF_EXCLUDED)

    def test_scan_root_skips_the_published_registry(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            directory = root / "docs" / "context" / "reviews"
            directory.mkdir(parents=True)
            (directory / "public-premise-dispositions.json").write_text(
                json.dumps(
                    {"findings": [{"statement": "코레일 승인 후 현장 촬영을 수행한다"}]},
                    ensure_ascii=False,
                ),
                encoding="utf-8",
            )
            occurrences, scanned = A.scan_root(root, root)
            self.assertEqual(scanned, 1)
            self.assertEqual(occurrences, [])

    def test_scan_root_skips_self_excluded_path(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            directory = root / "scripts" / "context"
            directory.mkdir(parents=True)
            (directory / "audit_public_premises.py").write_text(
                "코레일 승인 후 촬영한다\n", encoding="utf-8"
            )
            occurrences, scanned = A.scan_root(root, root)
            self.assertEqual(scanned, 1)
            self.assertEqual(occurrences, [])


class CliTest(unittest.TestCase):
    """CLI 계약: ``--input``과 ``--output``을 모두 받는다."""

    def _make_repo(self, tmp):
        root = Path(tmp)
        (root / "docs").mkdir()
        (root / "scripts").mkdir()
        # 두 파일이 서로 다른 주제 하나씩만 건드리게 한다. 한 줄이 두 주제를
        # 함께 건드리면 발견이 둘로 갈라져 집계가 흔들린다.
        (root / "docs" / "a.md").write_text(
            "코레일 회신을 받으면 갱신한다\n", encoding="utf-8"
        )
        (root / "scripts" / "b.md").write_text("현장 촬영 없음\n", encoding="utf-8")
        return root

    def test_input_and_output_write_a_valid_registry(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self._make_repo(tmp)
            output = Path(tmp) / "out" / "registry.json"
            code, _, _ = _run_cli("--input", str(root), "--output", str(output))
            self.assertEqual(code, 0)
            registry = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual(A.validate_registry(registry), [])
            self.assertEqual(registry["counts"]["scannedFiles"], 2)
            self.assertEqual(registry["counts"]["positivePremise"], 1)
            self.assertEqual(registry["counts"]["negativeExclusion"], 1)

    def test_output_defaults_to_stdout(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self._make_repo(tmp)
            code, stdout, _ = _run_cli("--input", str(root), "--quiet")
            self.assertEqual(code, 0)
            registry = json.loads(stdout)
            self.assertEqual(A.validate_registry(registry), [])

    def test_check_validates_an_existing_registry(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self._make_repo(tmp)
            output = Path(tmp) / "registry.json"
            _run_cli("--input", str(root), "--output", str(output))
            code, _, stderr = _run_cli("--check", str(output))
            self.assertEqual(code, 0)
            self.assertIn("schema ok", stderr)

    def test_check_rejects_a_corrupt_registry(self):
        with tempfile.TemporaryDirectory() as tmp:
            broken = Path(tmp) / "broken.json"
            broken.write_text('{"schemaVersion": 99}', encoding="utf-8")
            code, _, stderr = _run_cli("--check", str(broken))
            self.assertEqual(code, 2)
            self.assertIn("error", stderr)

    def test_check_missing_file_exits_2(self):
        code, _, _ = _run_cli("--check", "/nonexistent/registry.json")
        self.assertEqual(code, 2)

    def test_missing_input_exits_2(self):
        code, _, stderr = _run_cli("--input", "/nonexistent/repo")
        self.assertEqual(code, 2)
        self.assertIn("error", stderr)

    def test_fail_on_unresolved_exits_1(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "docs").mkdir()
            (root / "docs" / "a.md").write_text(
                "코레일 승인 후 현장 촬영을 수행한다\n", encoding="utf-8"
            )
            output = Path(tmp) / "registry.json"
            code, _, _ = _run_cli(
                "--input",
                str(root),
                "--output",
                str(output),
                "--fail-on-unresolved",
                "--quiet",
            )
            self.assertEqual(code, 1)
            registry = json.loads(output.read_text(encoding="utf-8"))
            self.assertGreater(registry["unresolvedCount"], 0)

    def test_fail_on_unresolved_passes_when_all_ruled(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "docs").mkdir()
            (root / "docs" / "a.md").write_text("촬영 없음\n", encoding="utf-8")
            code, _, _ = _run_cli(
                "--input", str(root), "--fail-on-unresolved", "--quiet"
            )
            self.assertEqual(code, 0)

    def test_repeated_runs_are_deterministic(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self._make_repo(tmp)
            first = Path(tmp) / "first.json"
            second = Path(tmp) / "second.json"
            _run_cli("--input", str(root), "--output", str(first), "--quiet")
            _run_cli("--input", str(root), "--output", str(second), "--quiet")
            self.assertEqual(
                first.read_text(encoding="utf-8"), second.read_text(encoding="utf-8")
            )


class RepositoryContractTest(unittest.TestCase):
    """실제 저장소에 대한 종단 계약. 미판정이 남으면 R-09는 끝나지 않는다."""

    def test_auditor_runs_over_the_repository(self):
        registry = A.run_audit(GENERATED_ROOT)
        self.assertEqual(A.validate_registry(registry), [])
        self.assertGreater(registry["counts"]["scannedFiles"], 0)

    def test_repository_has_no_unresolved_finding(self):
        registry = A.run_audit(GENERATED_ROOT)
        unresolved = [
            finding["statement"]
            for finding in registry["findings"]
            if not finding["resolved"]
        ]
        self.assertEqual(unresolved, [], f"미판정 발견: {unresolved}")

    def test_repository_never_reports_a_negative_as_positive(self):
        registry = A.run_audit(GENERATED_ROOT)
        for finding in registry["findings"]:
            if finding["classification"] != "positive_premise":
                continue
            with self.subTest(statement=finding["statement"]):
                self.assertFalse(
                    A.is_negated(finding["statement"]),
                    "부정 문장이 긍정 전제로 보고되었다",
                )

    def test_dispositions_file_is_published_and_current(self):
        self.assertTrue(
            DISPOSITIONS_PATH.exists(), f"{DISPOSITIONS_PATH} is missing"
        )
        document = A.load_dispositions(DISPOSITIONS_PATH)
        self.assertEqual(document["unresolvedCount"], 0)
        self.assertEqual(A.validate_registry(document), [])
        regenerated = A.run_audit(GENERATED_ROOT)
        self.assertEqual(
            document["findings"],
            regenerated["findings"],
            "게시된 처분표가 재생성 결과와 어긋난다. 재발 검사가 실패했다",
        )


if __name__ == "__main__":
    unittest.main(verbosity=2)
