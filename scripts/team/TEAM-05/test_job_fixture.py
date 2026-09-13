"""TEAM-05 job fixture contract tests: storage separation, resume, refusal, budget recording.

Synthetic files only. No real material, no large asset, no CV model cache.
"""
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

import job_fixture as job


def sha256_bytes(raw):
    return hashlib.sha256(raw).hexdigest()


class JobFixtureTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.layout = job.Layout(
            source=self.root / "source",
            cache=self.root / "cache",
            work=self.root / "work",
            candidate=self.root / "candidate",
        )
        self.layout.create()
        self.inputs = {}
        for name, payload in (("a.bin", b"alpha-payload\n"), ("nested/b.bin", b"beta-payload\n")):
            path = self.layout.source / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(payload)
            self.inputs[name] = payload

    def source_state(self):
        return {
            str(path.relative_to(self.layout.source)): path.read_bytes()
            for path in sorted(self.layout.source.rglob("*")) if path.is_file()
        }

    def run_job(self, job_id="job-01", **kwargs):
        return job.run_job(self.layout, job_id, sorted(self.inputs), **kwargs)

    # --- 수용 기준 1: 원본 / 캐시 / 작업 / 공개 후보 분리 ---

    def test_completed_job_separates_roots_and_never_writes_source(self):
        before = self.source_state()
        result = self.run_job(budget_bytes=1 << 20)
        self.assertEqual(result["state"], "completed")
        self.assertEqual(self.source_state(), before, "원본이 변경됐다")
        for name in self.inputs:
            self.assertTrue((self.layout.candidate / "job-01" / name).is_file())
        self.assertTrue((self.layout.work / "job-01" / "receipt.json").is_file())
        self.assertFalse(any(path.is_file() for path in self.layout.source.rglob("receipt.json")))

    def test_receipt_binds_every_input_digest_and_output_digest(self):
        result = self.run_job(budget_bytes=1 << 20)
        receipt = json.loads((self.layout.work / "job-01" / "receipt.json").read_text(encoding="utf-8"))
        self.assertEqual(receipt["jobId"], "job-01")
        self.assertEqual(set(receipt["inputs"]), set(self.inputs))
        for name, payload in self.inputs.items():
            self.assertEqual(receipt["inputs"][name]["sha256"], sha256_bytes(payload))
            self.assertEqual(receipt["outputs"][name]["sha256"], sha256_bytes(payload))
        self.assertEqual(receipt["inputsDigest"], result["inputsDigest"])

    # --- 수용 기준 2: 중단 후 재개, 중복 실행 거부 ---

    def test_interrupted_job_resumes_the_remaining_step_only(self):
        first = self.run_job(budget_bytes=1 << 20, stop_after=1)
        self.assertEqual(first["state"], "interrupted")
        self.assertEqual(len(first["processed"]), 1)
        resumed = self.run_job(budget_bytes=1 << 20, resume=True)
        self.assertEqual(resumed["state"], "completed")
        self.assertEqual(resumed["processed"], [name for name in sorted(self.inputs) if name not in first["processed"]])
        self.assertEqual(sorted(resumed["completedInputs"]), sorted(self.inputs))

    def test_completed_job_refuses_a_duplicate_run(self):
        self.run_job(budget_bytes=1 << 20)
        with self.assertRaises(job.JobError) as caught:
            self.run_job(budget_bytes=1 << 20)
        self.assertEqual(str(caught.exception), "JOB_ALREADY_COMPLETED")

    def test_resume_without_a_prior_run_is_refused(self):
        with self.assertRaises(job.JobError) as caught:
            self.run_job(budget_bytes=1 << 20, resume=True)
        self.assertEqual(str(caught.exception), "NO_RESUMABLE_RECEIPT")

    def test_second_writer_is_refused_while_a_lock_is_held(self):
        self.run_job(budget_bytes=1 << 20, stop_after=1)
        lock = self.layout.work / "job-01" / "job.lock"
        lock.write_text("holder", encoding="utf-8")
        with self.assertRaises(job.JobError) as caught:
            self.run_job(budget_bytes=1 << 20, resume=True)
        self.assertEqual(str(caught.exception), "JOB_LOCK_HELD")
        self.assertEqual(lock.read_text(encoding="utf-8"), "holder", "기존 lock을 덮어썼다")

    # --- 수용 기준 3: 경로 이탈 / hash 불일치 / 기존 출력 덮어쓰기 거부 ---

    def test_escaping_and_absolute_input_names_are_refused(self):
        for name in ("../outside.bin", "/etc/passwd", "nested\\b.bin", "C:/x.bin", ".git/config"):
            with self.subTest(name=name):
                with self.assertRaises(job.JobError) as caught:
                    job.run_job(self.layout, "job-esc", [name], budget_bytes=1 << 20)
                self.assertEqual(str(caught.exception), "INVALID_PORTABLE_PATH")

    def test_changed_input_bytes_stop_a_resume(self):
        self.run_job(budget_bytes=1 << 20, stop_after=1)
        remaining = sorted(self.inputs)[1]
        (self.layout.source / remaining).write_bytes(b"changed-after-plan\n")
        with self.assertRaises(job.JobError) as caught:
            self.run_job(budget_bytes=1 << 20, resume=True)
        self.assertEqual(str(caught.exception), "INPUT_DIGEST_CHANGED")

    def test_existing_output_is_never_overwritten(self):
        target = self.layout.candidate / "job-01" / sorted(self.inputs)[0]
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(b"previous-candidate\n")
        with self.assertRaises(job.JobError) as caught:
            self.run_job(budget_bytes=1 << 20)
        self.assertEqual(str(caught.exception), "OUTPUT_EXISTS")
        self.assertEqual(target.read_bytes(), b"previous-candidate\n", "기존 출력이 바뀌었다")

    def test_symlinked_input_is_refused(self):
        link = self.layout.source / "linked.bin"
        try:
            link.symlink_to(self.layout.source / "a.bin")
        except (OSError, NotImplementedError) as error:
            self.skipTest(f"이 환경에서 심볼릭 링크를 만들 수 없다: {error.__class__.__name__}")
        with self.assertRaises(job.JobError) as caught:
            job.run_job(self.layout, "job-link", ["linked.bin"], budget_bytes=1 << 20)
        self.assertEqual(str(caught.exception), "LINKED_INPUT")

    # --- 수용 기준 4: 용량 기록, 자동 삭제 금지 ---

    def test_missing_budget_is_refused_before_any_output(self):
        with self.assertRaises(job.JobError) as caught:
            self.run_job()
        self.assertEqual(str(caught.exception), "BUDGET_NOT_SET")
        self.assertFalse((self.layout.candidate / "job-01").exists())

    def test_budget_smaller_than_the_planned_bytes_is_refused(self):
        with self.assertRaises(job.JobError) as caught:
            self.run_job(budget_bytes=1)
        self.assertEqual(str(caught.exception), "BUDGET_EXCEEDED")
        self.assertFalse((self.layout.candidate / "job-01").exists())

    def test_receipt_records_observed_sizes_and_free_space(self):
        result = self.run_job(budget_bytes=1 << 20)
        receipt = json.loads((self.layout.work / "job-01" / "receipt.json").read_text(encoding="utf-8"))
        planned = sum(len(payload) for payload in self.inputs.values())
        self.assertEqual(receipt["budget"]["plannedBytes"], planned)
        self.assertEqual(receipt["budget"]["budgetBytes"], 1 << 20)
        self.assertGreater(receipt["budget"]["freeBytesBefore"], 0)
        self.assertEqual(result["budget"]["plannedBytes"], planned)

    def test_failed_job_preserves_partial_output_and_deletes_nothing(self):
        self.run_job(budget_bytes=1 << 20, stop_after=1)
        produced = sorted(path for path in (self.layout.candidate / "job-01").rglob("*") if path.is_file())
        self.assertEqual(len(produced), 1)
        (self.layout.source / sorted(self.inputs)[1]).write_bytes(b"changed\n")
        with self.assertRaises(job.JobError):
            self.run_job(budget_bytes=1 << 20, resume=True)
        still_there = sorted(path for path in (self.layout.candidate / "job-01").rglob("*") if path.is_file())
        self.assertEqual(still_there, produced, "거부 후 기존 산출물이 사라졌다")
        self.assertTrue((self.layout.work / "job-01" / "receipt.json").is_file())

    # --- CLI 종료 코드 ---

    def test_cli_exit_codes_separate_success_refusal_and_bad_invocation(self):
        args = ["--source", str(self.layout.source), "--cache", str(self.layout.cache),
                "--work", str(self.layout.work), "--candidate", str(self.layout.candidate),
                "--job-id", "job-cli", "--budget-bytes", str(1 << 20), "--input"]
        self.assertEqual(job.main(args + sorted(self.inputs)), 0)
        self.assertEqual(job.main(args + sorted(self.inputs)), 1)
        self.assertEqual(job.main(args + ["../escape.bin"]), 1)
        self.assertEqual(job.main(["--job-id", "x"]), 2)

    def test_module_never_removes_a_directory_tree(self):
        source = Path(job.__file__).read_text(encoding="utf-8")
        for forbidden in ("shutil.rmtree", "os.remove", "unlink(", "os.rmdir"):
            self.assertNotIn(forbidden, source, f"자동 삭제 경로가 있다: {forbidden}")


if __name__ == "__main__":
    unittest.main()
