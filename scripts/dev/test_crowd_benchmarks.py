import math
import unittest

from fetch_crowd_benchmarks import measurement_events, parse_txt


class CrowdSourceAdapterTests(unittest.TestCase):
    def test_centimetres_and_central_window_are_not_assumed_metres(self):
        raw = "\n".join(f"1 {frame} {(5.5-frame)*4} 0" for frame in range(12)).encode()
        samples = measurement_events(parse_txt(raw))
        self.assertEqual(samples[0]["DensityPM"], .25)
        self.assertAlmostEqual(samples[0]["SpeedMS"], 1)
        self.assertEqual(samples[0]["Frame"], 5)

    def test_missing_window_and_nan_are_missing_not_zero_speed(self):
        raw = "\n".join(f"1 {frame} {(5.5-frame)*4} 0" for frame in range(12) if frame != 0).encode()
        self.assertEqual(measurement_events(parse_txt(raw)), [])
        raw = "\n".join(f"1 {frame} {(5.5-frame)*4 if frame else math.nan} 0" for frame in range(12)).encode()
        self.assertEqual(measurement_events(parse_txt(raw)), [])

    def test_duplicate_identity_frame_cannot_silently_replace_measurement(self):
        with self.assertRaises(ValueError):
            parse_txt(b"1 0 0 0\n1 0 10 0")


if __name__ == "__main__":
    unittest.main()
