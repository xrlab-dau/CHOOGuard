import importlib.util
import pathlib
import tempfile
import unittest
from unittest import mock
spec=importlib.util.spec_from_file_location('cfg',pathlib.Path(__file__).with_name('configure_local.py'))
cfg=importlib.util.module_from_spec(spec);spec.loader.exec_module(cfg)

class OutputNamePreflightTests(unittest.TestCase):
    def invoke_until_path_check(self, name):
        # Isolate ordering only; this is not a replacement privacy backend.
        with tempfile.TemporaryDirectory() as tmp, mock.patch.object(cfg,'_require_privacy_backend'), mock.patch.object(cfg,'_validate_output',side_effect=RuntimeError('PATH_CHECK_REACHED')):
            return cfg.configure(pathlib.Path(tmp)/name)
    def test_overlong_names_are_classified_before_path_inspection(self):
        for name in ('x'*226,'SYNTHETIC_PRIVATE_PATH_'+'x'*240,'가'*76):
            with self.subTest(bytes=len(name.encode())), self.assertRaisesRegex(ValueError,'^Configuration output name is too long$'):
                self.invoke_until_path_check(name)
    def test_maximum_supported_name_still_reaches_security_checks(self):
        for name in ('x'*225,'가'*75,'voice'):
            with self.subTest(bytes=len(name.encode())), self.assertRaisesRegex(RuntimeError,'PATH_CHECK_REACHED'):
                self.invoke_until_path_check(name)
    def test_platform_check_is_not_bypassed_by_long_name(self):
        with mock.patch.object(cfg,'_require_privacy_backend',side_effect=ValueError('UNSUPPORTED')), self.assertRaisesRegex(ValueError,'^UNSUPPORTED$'):
            cfg.configure('x'*300)
    def test_invalid_network_destination_remains_rejected_first(self):
        with self.assertRaisesRegex(ValueError,'loopback'):
            cfg.configure('x'*300,node_ip='192.0.2.10')

if __name__=='__main__':unittest.main()
