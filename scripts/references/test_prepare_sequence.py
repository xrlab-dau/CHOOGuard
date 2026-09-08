"""Synthetic FFmpeg cut-scan regression, including a cut outside the scanned interval."""
import tempfile
from pathlib import Path
import subprocess
import unittest

from prepare_sequence import prepare


class ContinuousSequenceTests(unittest.TestCase):
    def test_cut_scan_is_input_bounded_and_detects_in_interval_cut(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            video = root / 'synthetic.mp4'
            subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-nostdin',
                '-f', 'lavfi', '-i', 'color=black:s=320x180:r=10:d=3',
                '-f', 'lavfi', '-i', 'color=white:s=320x180:r=10:d=3',
                '-filter_complex', '[0:v][1:v]concat=n=2:v=1:a=0', '-c:v', 'mpeg4',
                '-threads', '2', '-n', str(video)], check=True, capture_output=True, timeout=30)
            clean = prepare(video, root / 'clean', [1, 1.5, 2, 2.5])
            cut = prepare(video, root / 'cut', [2, 2.5, 3, 3.5])
            self.assertEqual(clean['sequenceVerification']['cutCount'], 0)
            self.assertEqual(cut['sequenceVerification']['cutCount'], 1)


if __name__ == '__main__':
    unittest.main()
