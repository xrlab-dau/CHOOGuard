"""Platform-neutral ordering checks; real private filesystem behavior stays in the macOS suite."""
from pathlib import Path
import unittest
from unittest import mock

import configure_local as subject


class NamePreflightTests(unittest.TestCase):
    def test_oversized_name_is_rejected_before_filesystem_lookup(self):
        for name in ('x' * 300, '한' * 100):
            with self.subTest(name_kind='ascii' if name.isascii() else 'utf8'):
                with mock.patch.object(subject, '_require_privacy_backend'), \
                     mock.patch.object(subject, '_validate_output') as lookup:
                    with self.assertRaisesRegex(ValueError, 'Configuration output name is too long'):
                        subject.configure(Path(name))
                    lookup.assert_not_called()

    def test_valid_name_still_passes_through_privacy_and_path_guards(self):
        with mock.patch.object(subject, '_require_privacy_backend') as privacy, \
             mock.patch.object(subject, '_validate_output', side_effect=ValueError('path guard')) as lookup:
            with self.assertRaisesRegex(ValueError, 'path guard'):
                subject.configure(Path('voice'))
            privacy.assert_called_once_with()
            lookup.assert_called_once()

    def test_long_name_does_not_bypass_unavailable_privacy_backend(self):
        with mock.patch.object(subject, '_require_privacy_backend', side_effect=ValueError('privacy guard')):
            with self.assertRaisesRegex(ValueError, 'privacy guard'):
                subject.configure(Path('x' * 300))


if __name__ == '__main__':
    unittest.main()
