"""Temporary app-private helper lifecycle only; no real accounts or provider network."""
import json
from pathlib import Path
import socket
import tempfile
import unittest
from unittest.mock import patch
import sidecar as s
import upgrade_helper as u


class UpgradeTests(unittest.TestCase):
    def test_inspect_does_not_stop_or_start(self):
        with tempfile.TemporaryDirectory(prefix='chooguard-upgrade-mock-') as t:
            private=Path(t)/'private';private.mkdir(mode=0o700)
            with patch.object(u.session,'start') as start,patch.object(u.session,'stop') as stop:
                result=u.upgrade(private,apply=False)
            self.assertFalse(result['applied']);start.assert_not_called();stop.assert_not_called()

    def test_refuses_running_unowned_process(self):
        with tempfile.TemporaryDirectory(prefix='chooguard-upgrade-mock-') as t:
            private=Path(t)/'private';private.mkdir(mode=0o700)
            with patch.object(u,'inspect',return_value={'running':True,'owned_by_runtime':False}),patch.object(u.session,'stop') as stop:
                with self.assertRaises(u.LocalFailure) as e:u.upgrade(private,apply=True)
            self.assertEqual('upgrade_refuses_unowned_helper',e.exception.code);stop.assert_not_called()

    def test_main_helper_new_ui_preserves_own_fake_account_bytes_and_does_not_infer(self):
        with tempfile.TemporaryDirectory(prefix='chooguard-upgrade-live-local-') as t:
            private=Path(t)/'private';store=s.PrivateStore(private)
            store.data['accounts']['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa']={'client_id':'FAKE_OWN_APP',
                'label':'Fake fixture account','access_token':'FAKE_ACCOUNT_TOKEN','scopes':['chatgpt.tokens.use.direct']}
            store.data['active']='aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';store.save();store.close()
            sock=socket.socket();sock.bind(('127.0.0.1',0));port=sock.getsockname()[1];sock.close()
            try:
                result=u.upgrade(private,port=port,apply=True)
                self.assertTrue(result['applied']);self.assertTrue(result['loaded_current_source'])
                self.assertEqual('multi-provider-subscription-v1',result['protocol'])
                self.assertTrue(all(result['registration_files_byte_exact'].values()))
                client=u.LocalClient.load(private);status=client.status()
                self.assertIsNone(status['selection']);self.assertFalse(status['cloud_enabled']);self.assertEqual(0,status['usage']['requests'])
                self.assertEqual('catalog_required',status['accounts'][0]['inference_state'])
            finally:
                if u.session.owned_process(private):u.session.stop(private)


if __name__=='__main__':unittest.main()
