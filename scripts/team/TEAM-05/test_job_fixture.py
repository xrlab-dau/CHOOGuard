"""Tests for the resumable synthetic job fixture.

Positive paths: a normal run separates the stores, a run can be interrupted and
resumed to the same result, and a pilot observation records size and free space
without deleting anything.

Negative paths, each expected to be refused with a stable code:
  JOB_ACTIVE           a second open while a lease is held (duplicate execution)
  JOB_DONE             opening a job that already finished
  INPUT_HASH_MISMATCH  an original changed after the job was opened
  OUTPUT_EXISTS        the candidate target already exists
  BUDGET_UNDECLARED    no positive integer budget declared
  BUDGET_EXCEEDED      the declared budget is smaller than the job needs
  CONTRACT_MISMATCH    a resume that changes the declared budget
  PATH_ESCAPE          a path that leaves its store
  CACHE_HASH_MISMATCH  a cache entry whose bytes no longer match
"""

import json
import tempfile
import unittest
from pathlib import Path

import job_fixture
from job_fixture import JobError, JobStore

INPUT = b"alpha\nbravo\ncharlie\n"
SOURCE = "source/a.txt"
OUTPUT = "pack.txt"


class JobFixtureTestCase(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name) / "job-root"
        self.store = JobStore(self.root)
        self.store.write_original(SOURCE, INPUT)

    def begin(self, **overrides):
        kwargs = {"names": [SOURCE], "output_rel": OUTPUT,
                  "budget_bytes": 4096, "holder": "holder-1"}
        kwargs.update(overrides)
        return self.store.begin(**kwargs)

    def candidate(self, name=OUTPUT):
        return self.root / "candidate" / name


class PositivePathTests(JobFixtureTestCase):
    def test_normal_run_separates_stores_and_records_observation(self):
        job = self.begin()
        result = self.store.run(job)

        self.assertEqual(result["state"], "done")
        self.assertEqual(result["completedUnits"], result["totalUnits"])

        expected = job_fixture.expand(SOURCE, INPUT)
        self.assertEqual(self.candidate().read_bytes(), expected)

        observation = result["observation"]
        self.assertEqual(observation["outputBytes"], len(expected))
        self.assertEqual(observation["outputDigest"], job_fixture.sha256_bytes(expected))
        self.assertIsInstance(observation["freeBytesAfter"], int)
        self.assertGreaterEqual(observation["freeBytesAfter"], 0)
        self.assertGreater(observation["survey"]["stores"]["original"], 0)
        self.assertGreater(observation["survey"]["stores"]["cache"], 0)
        self.assertGreater(observation["survey"]["stores"]["candidate"], 0)

        # originals and cache survive; nothing is deleted automatically
        self.assertEqual((self.root / "original" / SOURCE).read_bytes(), INPUT)
        self.assertEqual(len(list((self.root / "cache").glob("*.part"))), 1)
        # the derived expansion never lands inside the original store
        self.assertFalse((self.root / "original" / "pack.txt").exists())

    def test_interrupt_then_resume_is_monotonic_and_matches_one_shot(self):
        job = self.begin()
        first = self.store.run(job, limit_units=1)
        self.assertEqual(first["state"], "interrupted")
        self.assertEqual(first["completedUnits"], 1)
        self.assertFalse(self.candidate().exists())

        resumed = self.begin()
        self.assertEqual(resumed["completedUnits"], 1)  # progress preserved
        second = self.store.run(resumed, limit_units=1)
        self.assertEqual(second["completedUnits"], 2)

        final = self.store.run(second)
        self.assertEqual(final["state"], "done")
        self.assertEqual(final["completedUnits"], 3)
        self.assertEqual(self.candidate().read_bytes(), job_fixture.expand(SOURCE, INPUT))

    def test_survey_reports_store_sizes_and_free_space(self):
        self.store.run(self.begin())
        survey = self.store.survey()
        self.assertGreater(survey["freeBytes"], 0)
        for store in job_fixture.STORES:
            self.assertIn(store, survey["stores"])


class NegativePathTests(JobFixtureTestCase):
    def test_duplicate_execution_and_finished_job_are_rejected(self):
        job = self.begin()
        with self.assertRaises(JobError) as active:
            self.begin()  # same job, lease still held
        self.assertEqual(active.exception.code, "JOB_ACTIVE")

        self.store.run(job)
        with self.assertRaises(JobError) as done:
            self.begin()
        self.assertEqual(done.exception.code, "JOB_DONE")

    def test_input_hash_change_is_rejected_on_resume(self):
        job = self.begin()
        self.store.run(job, limit_units=1)  # interrupted, lease released
        self.store.write_original(SOURCE, INPUT + b"delta\n")

        resumed = self.begin()
        with self.assertRaises(JobError) as ctx:
            self.store.run(resumed)
        self.assertEqual(ctx.exception.code, "INPUT_HASH_MISMATCH")
        self.assertFalse(self.candidate().exists())

    def test_existing_output_is_not_overwritten(self):
        self.store.write_original("source/b.txt", b"zulu\n")
        first = self.begin()                      # targets OUTPUT
        second = self.begin(names=["source/b.txt"])  # different job, same target
        self.store.run(first)
        self.assertTrue(self.candidate().is_file())
        before = self.candidate().read_bytes()

        with self.assertRaises(JobError) as ctx:
            self.store.run(second)
        self.assertEqual(ctx.exception.code, "OUTPUT_EXISTS")
        self.assertEqual(self.candidate().read_bytes(), before)

    def test_undeclared_budget_is_rejected(self):
        for bad in (None, 0, -5, True, 3.5, "4096"):
            with self.subTest(budget=bad):
                with self.assertRaises(JobError) as ctx:
                    self.begin(budget_bytes=bad)
                self.assertEqual(ctx.exception.code, "BUDGET_UNDECLARED")

    def test_budget_exceeded_stops_without_output_and_keeps_progress(self):
        first_piece = job_fixture.unit_pieces(SOURCE, INPUT)[0]
        job = self.begin(budget_bytes=len(first_piece) + 1)  # exactly one unit fits

        with self.assertRaises(JobError) as ctx:
            self.store.run(job)
        self.assertEqual(ctx.exception.code, "BUDGET_EXCEEDED")
        self.assertFalse(self.candidate().exists())

        checkpoint = json.loads(
            (self.root / "work" / job["jobID"] / "checkpoint.json").read_text(encoding="utf-8"))
        self.assertEqual(checkpoint["state"], "interrupted")
        self.assertEqual(checkpoint["completedUnits"], 1)
        self.assertLessEqual(checkpoint["producedBytes"], job["budgetBytes"])
        # the original is retained; the fixture never deletes to reclaim space
        self.assertEqual((self.root / "original" / SOURCE).read_bytes(), INPUT)

    def test_resume_with_changed_budget_is_rejected(self):
        job = self.begin()
        self.store.run(job, limit_units=1)
        with self.assertRaises(JobError) as ctx:
            self.begin(budget_bytes=8192)
        self.assertEqual(ctx.exception.code, "CONTRACT_MISMATCH")

    def test_output_path_escape_is_rejected(self):
        for bad in ("../escape.txt", "/abs.txt", "a\\b.txt", "C:/x.txt", "", ".", "./"):
            with self.subTest(output=bad):
                with self.assertRaises(JobError) as ctx:
                    self.begin(output_rel=bad)
                self.assertEqual(ctx.exception.code, "PATH_ESCAPE")

    def test_input_path_escape_is_rejected(self):
        for bad in ("../a.txt", "/a.txt", "a\\b.txt", ".git/config", ".", "a/../b"):
            with self.subTest(input=bad):
                with self.assertRaises(JobError) as ctx:
                    self.store.manifest([bad])
                self.assertEqual(ctx.exception.code, "PATH_ESCAPE")

    def test_cache_hash_mismatch_is_rejected_and_not_overwritten(self):
        self.store.run(self.begin(output_rel="one.txt"))
        part = next((self.root / "cache").glob("*.part"))
        part.write_bytes(b"tampered\n")

        second = self.begin(output_rel="two.txt")
        with self.assertRaises(JobError) as ctx:
            self.store.run(second)
        self.assertEqual(ctx.exception.code, "CACHE_HASH_MISMATCH")
        self.assertEqual(part.read_bytes(), b"tampered\n")
        self.assertFalse(self.candidate("two.txt").exists())

    def test_missing_original_is_rejected(self):
        with self.assertRaises(JobError) as ctx:
            self.store.manifest(["source/absent.txt"])
        self.assertEqual(ctx.exception.code, "INPUT_MISSING")

    def test_failed_publish_releases_lease_and_stays_resumable(self):
        self.store.run(self.begin(output_rel="one.txt"))
        part = next((self.root / "cache").glob("*.part"))
        part.write_bytes(b"tampered\n")

        job = self.begin(output_rel="two.txt")
        with self.assertRaises(JobError) as ctx:
            self.store.run(job)
        self.assertEqual(ctx.exception.code, "CACHE_HASH_MISMATCH")

        work = self.root / "work" / job["jobID"]
        # the lease is released, so the job is not falsely reported active
        self.assertFalse((work / "lock.json").exists())
        checkpoint = json.loads((work / "checkpoint.json").read_text(encoding="utf-8"))
        self.assertEqual(checkpoint["state"], "interrupted")

        part.write_bytes(job_fixture.expand(SOURCE, INPUT))  # repair the cache
        resumed = self.begin(output_rel="two.txt")            # not JOB_ACTIVE
        final = self.store.run(resumed)
        self.assertEqual(final["state"], "done")
        self.assertEqual(self.candidate("two.txt").read_bytes(),
                         job_fixture.expand(SOURCE, INPUT))


if __name__ == "__main__":
    unittest.main()
