"""Public receipts must never leak private extractor URLs or claim missing media."""
import importlib.util
from pathlib import Path
import unittest

PATH = Path(__file__).with_name('acquire_video.py')
SPEC = importlib.util.spec_from_file_location('acquire_video', PATH)
module = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(module)


class AcquisitionContract(unittest.TestCase):
    def test_stable_youtube_url_only(self):
        self.assertEqual(module.video_id('https://www.youtube.com/watch?v=RY2qvE0Tugk'),
                         'RY2qvE0Tugk')
        for url in ['file:///secret', 'https://example.com/watch?v=RY2qvE0Tugk',
                    'https://user:password@youtube.com/watch?v=RY2qvE0Tugk',
                    'https://www.youtube.com/watch?v=bad']:
            with self.assertRaises(ValueError):
                module.video_id(url)

    def test_public_metadata_omits_signed_formats_and_description(self):
        info = {'id': 'RY2qvE0Tugk', 'title': 'Station', 'uploader': 'Uploader',
                'upload_date': '20230501', 'duration': 832, 'license': None,
                'formats': [{'url': 'https://video.googlevideo.com/?sig=private'}],
                'description': 'private extractor body', 'webpage_url': 'https://evil.invalid/'}
        clean = module.public_metadata(info)
        self.assertEqual(clean['watchUrl'], 'https://www.youtube.com/watch?v=RY2qvE0Tugk')
        self.assertEqual(clean['license'], 'unknown')
        self.assertIsNone(clean['recordingDate'])
        self.assertEqual(clean['uploadDate'], '2023-05-01')
        self.assertNotIn('formats', clean)
        self.assertNotIn('description', clean)
        self.assertNotIn('sig=private', str(clean))

    def test_select_bounded_non_drm_single_video_stream(self):
        formats = [
            {'format_id': 'bad', 'height': 1080, 'width': 1920, 'vcodec': 'avc1',
             'acodec': 'none', 'filesize': 800, 'has_drm': True},
            {'format_id': '137', 'height': 1080, 'width': 1920, 'vcodec': 'avc1',
             'acodec': 'none', 'filesize': 1000, 'ext': 'mp4'},
            {'format_id': '248', 'height': 1080, 'width': 1920, 'vcodec': 'vp9',
             'acodec': 'none', 'filesize': 500, 'ext': 'webm'},
            {'format_id': '4k', 'height': 2160, 'width': 3840, 'vcodec': 'avc1',
             'acodec': 'none', 'filesize': 100, 'ext': 'mp4'}]
        self.assertEqual(module.select_format(formats, 2000)['format_id'], '137')
        self.assertEqual(module.select_format(formats, 700)['format_id'], '248')
        with self.assertRaises(ValueError):
            module.select_format(formats, 50)

    def test_error_redacts_volatile_urls(self):
        error = 'HTTP Error 403 for https://video.googlevideo.com/x?sig=secret\nDenied'
        self.assertNotIn('secret', module.safe_text(error))
        self.assertIn('HTTP Error 403', module.safe_text(error))


if __name__ == '__main__':
    unittest.main()
