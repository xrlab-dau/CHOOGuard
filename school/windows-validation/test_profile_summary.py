import copy
import unittest

from summarize_profile import summarize


class ProfileSummaryTests(unittest.TestCase):
    def setUp(self):
        self.result = {"status": "completed", "requestedSeconds": 20, "elapsedSeconds": 20,
                       "frames": 1200, "cameraRenderCallbacks": 1200, "batchMode": False, "errors": []}
        self.rows = [{"seconds": str(i), "frames": "60", "fps": "60", "frame_p95_ms": "17",
                      "working_set_bytes": str(100*1024*1024), "private_bytes": str(120*1024*1024),
                      "unity_allocated_bytes": str(80*1024*1024)} for i in range(1, 21)]

    def test_complete_rendered_run_produces_memory_and_rate_summary(self):
        report = summarize(self.result, self.rows, warmup=0)
        self.assertEqual(report["mean_camera_callbacks_per_second"], 60)
        self.assertEqual(report["peak_working_set_mib"], 100)

    def test_early_exit_cannot_be_completed(self):
        self.result["elapsedSeconds"] = 5
        with self.assertRaises(ValueError): summarize(self.result, self.rows, warmup=0)

    def test_headless_update_rate_is_not_rendered_fps(self):
        self.result["batchMode"] = True
        with self.assertRaises(ValueError): summarize(self.result, self.rows, warmup=0)

    def test_no_camera_rendering_rejects_fps_claim(self):
        self.result["cameraRenderCallbacks"] = 0
        with self.assertRaises(ValueError): summarize(self.result, self.rows, warmup=0)

    def test_missing_samples_or_nonfinite_metrics_are_rejected(self):
        for rows in ([], self.rows[:5]):
            with self.subTest(rows=len(rows)), self.assertRaises(ValueError):
                summarize(self.result, rows, warmup=0)
        rows = copy.deepcopy(self.rows)
        rows[4]["working_set_bytes"] = "NaN"
        with self.assertRaises(ValueError): summarize(self.result, rows, warmup=0)


if __name__ == "__main__": unittest.main()
