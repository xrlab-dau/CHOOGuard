"""영상 표본의 순서·PTS·해시와 실패 시 출력 보존 계약을 검사한다."""
from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import numpy as np

PATH = Path(__file__).with_name('sample_video_frames.py')
SPEC = importlib.util.spec_from_file_location('sample_video_frames', PATH)
module = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(module)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class SampleContractTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.video = self.root / 'source.mp4'
        self.video.write_bytes(b'synthetic-video-source')
        self.output = self.root / 'samples'
        self.probe = {
            'streams': [{'width': 640, 'height': 360, 'start_time': '0.0',
                         'duration': '3.0', 'time_base': '1/10240'}],
            'format': {'duration': '3.0', 'start_time': '0.0'},
        }
        self.probe_mock = self.enterContext(patch.object(
            module.subprocess, 'check_output', side_effect=lambda *a, **k: json.dumps(self.probe)))
        self.decode_mock = self.enterContext(patch.object(
            module.subprocess, 'run', side_effect=self.decode))

    def decode(self, command, **kwargs):
        seconds = float(command[command.index('-ss') + 1])
        actual = float(self.probe['streams'][0]['start_time']) + seconds
        width = int(command[command.index('-vf') + 1].split('=')[1].split(':')[0])
        pixels = np.full((width * 9 // 16, width, 3), 100 + int(seconds * 10), dtype=np.uint8)
        module.cv2.imwrite(str(command[-1]), pixels)
        return subprocess.CompletedProcess(
            command, 0, '', f'[Parsed_showinfo] n: 0 pts: {round(actual * 10240)} pts_time:{actual} ')

    def test_success_preserves_request_order_and_hashes(self):
        result = module.sample(self.video, self.output, [0.0, 0.5, 1.0], width=320)
        self.assertEqual(result, json.loads((self.output / 'samples.json').read_text()))
        self.assertEqual(result['frameCount'], 3)
        self.assertEqual(result['sourceVideoSha256'], digest(self.video))
        self.assertEqual([f['requestedSeconds'] for f in result['frames']], [0.0, 0.5, 1.0])
        for frame in result['frames']:
            self.assertEqual(frame['sha256'], digest(self.output / frame['file']))
            self.assertEqual(frame['width'], 320)
            self.assertEqual(frame['status'], 'sampled_not_geometry_or_privacy_approved')
        self.assertNotIn(str(self.root), json.dumps(result))

    def test_existing_output_is_not_modified_or_decoded(self):
        self.output.mkdir()
        (self.output / 'samples.json').write_text('prior-receipt')
        (self.output / 't000000000.jpg').write_bytes(b'prior-frame')
        before = {p.name: p.read_bytes() for p in self.output.iterdir()}
        with self.assertRaises(FileExistsError):
            module.sample(self.video, self.output, [0.0], width=320)
        self.assertEqual(before, {p.name: p.read_bytes() for p in self.output.iterdir()})
        self.decode_mock.assert_not_called()

    def test_empty_directory_file_and_symlink_are_not_reused(self):
        for kind in ('directory', 'file', 'symlink', 'dangling-symlink'):
            with self.subTest(kind=kind):
                output = self.root / kind
                if kind == 'directory':
                    output.mkdir()
                elif kind == 'file':
                    output.write_text('owned elsewhere')
                else:
                    output.symlink_to(self.root if kind == 'symlink' else self.root / 'missing')
                with self.assertRaises(FileExistsError):
                    module.sample(self.video, output, [0.0], width=320)
        self.decode_mock.assert_not_called()

    def test_invalid_request_is_rejected_before_creating_output(self):
        cases = [([], 320), ([0.0] * 81, 320), ([0.0, 0.0], 320),
                 ([1.0, 0.0], 320), ([0.0101, 0.0102], 320),
                 ([float('nan')], 320), ([float('inf')], 320), ([-1.0], 320),
                 ([0.0, 3.0], 320), ([0.0], 319), ([0.0], 1921),
                 ([0.0], 320.5), ([True], 320)]
        for index, (times, width) in enumerate(cases):
            with self.subTest(times=times, width=width):
                output = self.root / f'invalid-{index}'
                with self.assertRaises(ValueError):
                    module.sample(self.video, output, times, width)
                self.assertFalse(output.exists())
        self.decode_mock.assert_not_called()

    def test_invalid_probe_is_rejected_before_creating_output(self):
        for value in ('NaN', 'inf', '0', '-1', 'N/A'):
            with self.subTest(duration=value):
                self.probe['streams'][0]['duration'] = value
                self.probe['format']['duration'] = value
                with self.assertRaises(ValueError):
                    module.sample(self.video, self.output, [0.0], width=320)
                self.assertFalse(self.output.exists())
        self.decode_mock.assert_not_called()

    def test_source_changes_during_decode_cannot_publish_success(self):
        def mutate(command, **kwargs):
            result = self.decode(command, **kwargs)
            self.video.write_bytes(b'changed-source')
            return result
        self.decode_mock.side_effect = mutate
        with self.assertRaisesRegex(ValueError, '(?i)(source|원본)'):
            module.sample(self.video, self.output, [0.0], width=320)
        self.assertFalse((self.output / 'samples.json').exists())

    def test_decode_failure_cannot_publish_success(self):
        calls = 0

        def fail_second(command, **kwargs):
            nonlocal calls
            calls += 1
            if calls == 2:
                raise subprocess.CalledProcessError(1, command)
            return self.decode(command, **kwargs)
        self.decode_mock.side_effect = fail_second
        with self.assertRaises(subprocess.CalledProcessError):
            module.sample(self.video, self.output, [0.0, 0.5], width=320)
        self.assertFalse((self.output / 'samples.json').exists())

    def test_missing_or_unexpected_pts_cannot_publish_success(self):
        for index, stderr in enumerate(('', 'n: 0 pts: 999999 pts_time:99.0')):
            def wrong_pts(command, **kwargs):
                result = self.decode(command, **kwargs)
                result.stderr = stderr
                return result
            self.decode_mock.side_effect = wrong_pts
            output = self.root / f'bad-pts-{index}'
            with self.assertRaises(ValueError):
                module.sample(self.video, output, [0.0], width=320)
            self.assertFalse((output / 'samples.json').exists())

    def test_duplicate_decoded_pts_cannot_publish_success(self):
        def duplicate(command, **kwargs):
            result = self.decode(command, **kwargs)
            result.stderr = 'n: 0 pts: 1024 pts_time:0.1'
            return result
        self.decode_mock.side_effect = duplicate
        with self.assertRaises(ValueError):
            module.sample(self.video, self.output, [0.0, 0.05], width=320)
        self.assertFalse((self.output / 'samples.json').exists())

    def test_ffmpeg_does_not_overwrite_and_has_a_timeout(self):
        module.sample(self.video, self.output, [0.0], width=320)
        command = self.decode_mock.call_args.args[0]
        self.assertIn('-n', command)
        self.assertNotIn('-y', command)
        self.assertGreater(self.decode_mock.call_args.kwargs['timeout'], 0)
        self.assertGreater(self.probe_mock.call_args.kwargs['timeout'], 0)

    def test_nonzero_start_retains_original_pts_and_clip_relative_requests(self):
        self.probe['streams'][0]['start_time'] = '5.0'
        self.probe['streams'][0]['duration'] = '2.0'
        self.probe['format']['start_time'] = '5.0'
        self.probe['format']['duration'] = '7.0'
        result = module.sample(self.video, self.output, [0.0, 0.5], width=320)
        self.assertEqual(result['sourceStartPtsSeconds'], 5.0)
        self.assertEqual(result['sourceDurationSeconds'], 2.0)
        self.assertEqual([f['sourcePtsSeconds'] for f in result['frames']], [5.0, 5.5])
        with self.assertRaises(ValueError):
            module.sample(self.video, self.root / 'beyond', [2.5], width=320)


@unittest.skipUnless(shutil.which('ffmpeg') and shutil.which('ffprobe'), 'FFmpeg/ffprobe 필요')
class FFmpegIntegrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory()
        cls.addClassCleanup(cls.temporary.cleanup)
        cls.root = Path(cls.temporary.name)
        cls.video = cls.root / 'synthetic.mp4'
        subprocess.run([
            'ffmpeg', '-hide_banner', '-loglevel', 'error', '-nostdin', '-f', 'lavfi',
            '-i', 'testsrc2=size=320x180:rate=10:duration=2', '-an', '-c:v', 'mpeg4',
            '-bf', '0', '-threads', '2', '-n', str(cls.video),
        ], check=True, capture_output=True, timeout=30)

    def test_cli_receipt_matches_actual_decoded_frames(self):
        output = self.root / 'cli-samples'
        result = subprocess.run([
            sys.executable, str(PATH), '--video', str(self.video), '--output', str(output),
            '--times', '0,0.5,1', '--width', '320',
        ], check=True, capture_output=True, text=True, timeout=40)
        self.assertEqual(json.loads(result.stdout)['frames'], 3)
        record = json.loads((output / 'samples.json').read_text())
        self.assertEqual(record['sourceVideoSha256'], digest(self.video))
        for frame in record['frames']:
            self.assertAlmostEqual(frame['sourcePtsSeconds'], frame['requestedSeconds'], places=3)
            self.assertEqual(frame['sha256'], digest(output / frame['file']))

    def test_nonzero_stream_start_with_real_ffmpeg(self):
        video = self.root / 'offset.mp4'
        subprocess.run([
            'ffmpeg', '-hide_banner', '-loglevel', 'error', '-nostdin', '-i', str(self.video),
            '-c', 'copy', '-output_ts_offset', '5', '-n', str(video),
        ], check=True, capture_output=True, timeout=30)
        result = module.sample(video, self.root / 'offset-samples', [0.0, 0.5, 1.0], width=320)
        self.assertEqual(result['sourceDurationSeconds'], 2.0)
        self.assertEqual(result['sourceStartPtsSeconds'], 5.0)
        self.assertEqual([f['sourcePtsSeconds'] for f in result['frames']], [5.0, 5.5, 6.0])


if __name__ == '__main__':
    unittest.main()
