import concurrent.futures
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import configure_local
from configure_local import _CleanupIncomplete, _OutputPathChanged, configure


class LocalVoiceConfigurationTests(unittest.TestCase):
    def test_local_service_has_no_auto_rooms_and_keeps_credentials_private(self):
        with tempfile.TemporaryDirectory() as temp:
            target = Path(temp) / "voice"
            result = configure(target)
            self.assertNotIn("ApiSecret", result)
            self.assertFalse(result["recording"])
            self.assertIn("auto_create: false", (target / "livekit.yaml").read_text())
            self.assertIn("bind_addresses: [0.0.0.0]", (target / "livekit.yaml").read_text())
            config = json.loads((target / "server-voice.json").read_text())
            self.assertGreaterEqual(len(config["ApiSecret"]), 32)
            self.assertIn(config["ApiKey"], (target / "keys.yaml").read_text())
            self.assertEqual((target / "keys.yaml").stat().st_mode & 0o777, 0o600)
            self.assertEqual(target.stat().st_mode & 0o777, 0o700)

    def test_existing_service_credentials_are_never_silently_rotated(self):
        with tempfile.TemporaryDirectory() as temp:
            target = Path(temp) / "voice"
            configure(target)
            original = (target / "keys.yaml").read_bytes()
            with self.assertRaises(ValueError):
                configure(target)
            self.assertEqual((target / "keys.yaml").read_bytes(), original)

    def test_non_loopback_node_ip_is_rejected_before_creating_credentials(self):
        with tempfile.TemporaryDirectory() as temp:
            target = Path(temp) / "voice"
            with self.assertRaises(ValueError):
                configure(target, node_ip="192.0.2.44")
            self.assertFalse(target.exists())

    def test_scoped_or_control_character_ipv6_is_rejected_before_writes(self):
        with tempfile.TemporaryDirectory() as temp:
            for unsafe_ip in ("::1%lo", "::1%lo\nlogging:\n  level: debug"):
                target = Path(temp) / f"voice-{len(unsafe_ip)}"
                with self.assertRaises(ValueError):
                    configure(target, node_ip=unsafe_ip)
                self.assertFalse(target.exists())

    def test_ipv4_mapped_non_loopback_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            target = Path(temp) / "voice"
            with self.assertRaises(ValueError):
                configure(target, node_ip="::ffff:192.0.2.44")
            self.assertFalse(target.exists())

    def test_symlinked_parent_is_rejected_without_writing_outside_target(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            redirected = root / "redirected"
            outside = root / "outside"
            outside.mkdir()
            redirected.symlink_to(outside, target_is_directory=True)
            target = redirected / "voice"

            with self.assertRaises(ValueError):
                configure(target)
            self.assertFalse((outside / "voice").exists())

    def test_unavailable_parent_is_rejected_without_creating_directories(self):
        with tempfile.TemporaryDirectory() as temp:
            target = Path(temp) / "missing" / "nested" / "voice"
            with self.assertRaises(ValueError):
                configure(target)
            self.assertFalse(target.parent.exists())

    def test_configuration_requires_verified_darwin_privacy_backend_before_keys(self):
        with tempfile.TemporaryDirectory() as temp:
            target = Path(temp) / "voice"
            with mock.patch.object(configure_local.sys, "platform", "linux"):
                with mock.patch("configure_local.secrets.token_hex") as token_hex:
                    with self.assertRaisesRegex(ValueError, "not supported"):
                        configure(target)
            token_hex.assert_not_called()
            self.assertFalse(target.exists())

    def test_concurrent_configurations_do_not_share_a_write_directory(self):
        self.assertFalse(hasattr(configure_local, "_WRITE_DIR_FD"))
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            targets = [root / "voice-a", root / "voice-b"]
            with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
                results = list(executor.map(configure, targets))
            self.assertEqual([result["status"] for result in results], [
                "private_local_config_created",
                "private_local_config_created",
            ])
            for target in targets:
                self.assertTrue((target / "keys.yaml").is_file())
                self.assertTrue((target / "livekit.yaml").is_file())
                self.assertTrue((target / "server-voice.json").is_file())

    def test_write_failure_removes_only_files_created_by_this_invocation(self):
        with tempfile.TemporaryDirectory() as temp:
            target = Path(temp) / "voice"
            original_open = configure_local.os.open

            def fail_livekit_open(path, flags, mode=0o777, *, dir_fd=None):
                if path == "livekit.yaml" and dir_fd is not None:
                    raise OSError("synthetic write failure")
                return original_open(path, flags, mode, dir_fd=dir_fd)

            with mock.patch("configure_local.os.open", side_effect=fail_livekit_open):
                with self.assertRaises(OSError):
                    configure(target)
            self.assertFalse(target.exists())

    def test_created_file_descriptor_closes_when_identity_probe_fails(self):
        with tempfile.TemporaryDirectory() as temp:
            target = Path(temp) / "voice"
            original_open = configure_local.os.open
            original_fstat = configure_local.os.fstat
            created_fd = None
            faulted = False

            def capture_created(path, flags, mode=0o777, *, dir_fd=None):
                nonlocal created_fd
                fd = original_open(path, flags, mode, dir_fd=dir_fd)
                if flags & os.O_CREAT and created_fd is None:
                    created_fd = fd
                return fd

            def fail_created_fstat(fd):
                nonlocal faulted
                if fd == created_fd and not faulted:
                    faulted = True
                    raise OSError("synthetic fstat failure")
                return original_fstat(fd)

            with mock.patch("configure_local.os.open", side_effect=capture_created):
                with mock.patch("configure_local.os.fstat", side_effect=fail_created_fstat):
                    with self.assertRaises((OSError, _CleanupIncomplete)):
                        configure(target)

            self.assertTrue(faulted)
            self.assertIsNotNone(created_fd)
            with self.assertRaises(OSError):
                os.fstat(created_fd)

    def test_write_private_closes_owned_descriptor_once_on_each_failure_stage(self):
        failures = ("fstat", "on_acquired", "fchmod", "fdopen", "write")
        for failure in failures:
            with self.subTest(failure=failure), tempfile.TemporaryDirectory() as temp:
                target = Path(temp) / "keys.yaml"
                original_open = configure_local.os.open
                original_fstat = configure_local.os.fstat
                original_close = configure_local.os.close
                original_fdopen = configure_local.os.fdopen
                original_fchmod = configure_local.os.fchmod
                created_fd = None
                raw_close_count = 0
                stream_close_count = 0

                def capture_open(path, flags, mode=0o777, *, dir_fd=None):
                    nonlocal created_fd
                    fd = original_open(path, flags, mode, dir_fd=dir_fd)
                    if flags & os.O_CREAT and created_fd is None:
                        created_fd = fd
                    return fd

                def close(fd):
                    nonlocal raw_close_count
                    if fd == created_fd:
                        raw_close_count += 1
                    return original_close(fd)

                def fstat(fd):
                    if failure == "fstat" and fd == created_fd:
                        raise OSError("synthetic fstat failure")
                    return original_fstat(fd)

                def on_acquired(_name, _identity):
                    if failure == "on_acquired":
                        raise OSError("synthetic acquisition callback failure")

                def fchmod(fd, mode):
                    if failure == "fchmod" and fd == created_fd:
                        raise OSError("synthetic fchmod failure")
                    return original_fchmod(fd, mode)

                def fdopen(fd, *args, **kwargs):
                    if failure == "fdopen" and fd == created_fd:
                        raise OSError("synthetic fdopen failure")
                    stream = original_fdopen(fd, *args, **kwargs)
                    if failure != "write":
                        return stream

                    class FailingStream:
                        def write(self, _text):
                            raise OSError("synthetic write failure")

                        def close(self):
                            nonlocal stream_close_count
                            stream_close_count += 1
                            stream.close()

                    return FailingStream()

                with mock.patch.object(configure_local.os, "open", side_effect=capture_open):
                    with mock.patch.object(configure_local.os, "fstat", side_effect=fstat):
                        with mock.patch.object(configure_local.os, "close", side_effect=close):
                            with mock.patch.object(configure_local.os, "fchmod", side_effect=fchmod):
                                with mock.patch.object(
                                    configure_local.os, "fdopen", side_effect=fdopen
                                ):
                                    with self.assertRaises(OSError):
                                        configure_local.write_private(
                                            target,
                                            "synthetic\n",
                                            on_acquired=on_acquired,
                                        )

                self.assertIsNotNone(created_fd)
                with self.assertRaises(OSError):
                    original_fstat(created_fd)
                if failure == "write":
                    self.assertEqual(raw_close_count, 0)
                    self.assertEqual(stream_close_count, 1)
                else:
                    self.assertEqual(raw_close_count, 1)
                    self.assertEqual(stream_close_count, 0)

    def test_failed_rollback_reports_manual_cleanup_without_private_path(self):
        with tempfile.TemporaryDirectory() as temp:
            target = Path(temp) / "SYNTHETIC_PRIVATE_PATH_voice"
            original_open = configure_local.os.open
            original_unlink = configure_local.os.unlink

            def fail_livekit_open(path, flags, mode=0o777, *, dir_fd=None):
                if path == "livekit.yaml" and dir_fd is not None:
                    raise OSError("synthetic write failure")
                return original_open(path, flags, mode, dir_fd=dir_fd)

            def deny_key_unlink(path, *, dir_fd=None):
                if path == "keys.yaml" and dir_fd is not None:
                    raise PermissionError("synthetic unlink denial")
                return original_unlink(path, dir_fd=dir_fd)

            with mock.patch("configure_local.os.open", side_effect=fail_livekit_open):
                with mock.patch("configure_local.os.unlink", side_effect=deny_key_unlink):
                    with self.assertRaises(_CleanupIncomplete) as raised:
                        configure(target)
            self.assertNotIn("SYNTHETIC_PRIVATE_PATH", str(raised.exception))
            residues = list(Path(temp).glob(".SYNTHETIC_PRIVATE_PATH_voice.*.tmp"))
            self.assertEqual(len(residues), 1)
            self.assertTrue((residues[0] / "keys.yaml").is_file())

    def test_replaced_owned_file_is_preserved_and_cleanup_is_incomplete(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            original_verify = configure_local._verify_private_file
            replaced = False

            def replace_key(path, **kwargs):
                nonlocal replaced
                if Path(path).name == "keys.yaml" and not replaced:
                    replaced = True
                    os.unlink("keys.yaml", dir_fd=kwargs["dir_fd"])
                    replacement_fd = os.open(
                        "keys.yaml",
                        os.O_WRONLY | os.O_CREAT | os.O_EXCL,
                        0o600,
                        dir_fd=kwargs["dir_fd"],
                    )
                    with os.fdopen(replacement_fd, "w") as stream:
                        stream.write("synthetic replacement sentinel")
                return original_verify(path, **kwargs)

            with mock.patch(
                "configure_local._verify_private_file", side_effect=replace_key
            ):
                with self.assertRaises(_CleanupIncomplete):
                    configure(target)
            residues = list(root.glob(".voice.*.tmp"))
            self.assertEqual(len(residues), 1)
            self.assertEqual(
                (residues[0] / "keys.yaml").read_text(),
                "synthetic replacement sentinel",
            )

    def test_foreign_collision_is_preserved_during_rollback(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            original_write = configure_local.write_private

            def inject_collision(path, text, **kwargs):
                if Path(path).name == "livekit.yaml":
                    collision_fd = os.open(
                        "livekit.yaml",
                        os.O_WRONLY | os.O_CREAT | os.O_EXCL,
                        0o600,
                        dir_fd=kwargs["dir_fd"],
                    )
                    with os.fdopen(collision_fd, "w") as stream:
                        stream.write("synthetic foreign sentinel")
                return original_write(path, text, **kwargs)

            with mock.patch("configure_local.write_private", side_effect=inject_collision):
                with self.assertRaises(_CleanupIncomplete):
                    configure(target)
            residues = list(root.glob(".voice.*.tmp"))
            self.assertEqual(len(residues), 1)
            self.assertEqual(
                (residues[0] / "livekit.yaml").read_text(),
                "synthetic foreign sentinel",
            )
            self.assertFalse((residues[0] / "keys.yaml").exists())

    def test_directory_swap_before_publication_does_not_mutate_replacement(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            original_rename = configure_local._rename_exclusive

            def inject_replacement(parent_fd, staging_name, output_name):
                target.mkdir(mode=0o700)
                (target / "keys.yaml").write_text("synthetic foreign sentinel")
                return original_rename(parent_fd, staging_name, output_name)

            with mock.patch(
                "configure_local._rename_exclusive", side_effect=inject_replacement
            ):
                with self.assertRaises(_CleanupIncomplete):
                    configure(target)
            self.assertEqual((target / "keys.yaml").read_text(), "synthetic foreign sentinel")
            # The staging inode was not proven to be ours after the swap; it
            # must remain for manual cleanup rather than risking foreign rmdir.
            self.assertEqual(len(list(root.glob(".voice.*.tmp"))), 1)

    def test_directory_swap_does_not_delete_replacement_directory(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            parked = root / "parked"
            original_open = configure_local.os.open
            swapped = False

            def swap_after_open(path, flags, mode=0o777, *, dir_fd=None):
                nonlocal swapped
                descriptor = original_open(path, flags, mode, dir_fd=dir_fd)
                if path == "voice" and dir_fd is not None and not swapped:
                    swapped = True
                    target.rename(parked)
                    target.mkdir()
                    (target / "preexisting-synthetic-marker").write_text("keep")
                return descriptor

            with mock.patch("configure_local.os.open", side_effect=swap_after_open):
                configure(target)
            self.assertFalse(swapped)
            self.assertTrue((target / "keys.yaml").is_file())

    def test_parent_path_swap_after_key_is_rejected_and_owned_files_are_cleaned(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            parent = root / "parent"
            parent.mkdir(mode=0o700)
            target = parent / "voice"
            parked = root / "parked-parent"
            original_write = configure_local.write_private

            def swap_parent_after_key(path, text, **kwargs):
                result = original_write(path, text, **kwargs)
                if Path(path).name == "keys.yaml":
                    parent.rename(parked)
                    parent.mkdir(mode=0o700)
                return result

            with mock.patch(
                "configure_local.write_private", side_effect=swap_parent_after_key
            ):
                with self.assertRaises(_OutputPathChanged):
                    configure(target)
            self.assertFalse((parent / "voice").exists())
            self.assertFalse((parked / "voice" / "keys.yaml").exists())

    @unittest.skipUnless(sys.platform == "darwin", "requires Darwin ACL API")
    def test_real_inherited_acl_is_rejected_but_no_acl_control_succeeds(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            acl_parent = root / "acl-parent"
            acl_parent.mkdir(mode=0o700)
            completed = subprocess.run(
                [
                    "/bin/chmod",
                    "+a",
                    "everyone allow read,search,file_inherit,directory_inherit",
                    str(acl_parent),
                ],
                capture_output=True,
                text=True,
                check=False,
                timeout=20,
            )
            if completed.returncode:
                self.fail("synthetic ACL fixture setup failed: " + completed.stderr)

            try:
                with mock.patch("configure_local.secrets.token_hex") as token_hex:
                    with self.assertRaisesRegex(ValueError, "ACL"):
                        configure(acl_parent / "voice")
                token_hex.assert_not_called()

                no_acl_parent = root / "no-acl-parent"
                no_acl_parent.mkdir(mode=0o700)
                result = configure(no_acl_parent / "voice")
                self.assertEqual(result["status"], "private_local_config_created")
            finally:
                subprocess.run(
                    ["/bin/chmod", "-RN", str(acl_parent)],
                    capture_output=True,
                    text=True,
                    check=False,
                    timeout=20,
                )

    @unittest.skipUnless(sys.platform == "darwin", "requires Darwin ACL API")
    def test_real_file_acl_is_rejected_but_no_acl_file_control_succeeds(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            acl_file = root / "acl-file"
            acl_file.write_text("synthetic")
            acl_file.chmod(0o600)
            completed = subprocess.run(
                ["/bin/chmod", "+a", "everyone allow read", str(acl_file)],
                capture_output=True,
                text=True,
                check=False,
                timeout=20,
            )
            if completed.returncode:
                self.fail("synthetic file ACL fixture setup failed: " + completed.stderr)

            no_acl_file = root / "no-acl-file"
            no_acl_file.write_text("synthetic")
            no_acl_file.chmod(0o600)
            acl_fd = os.open(acl_file, os.O_RDONLY)
            no_acl_fd = os.open(no_acl_file, os.O_RDONLY)
            try:
                with self.assertRaisesRegex(ValueError, "ACL"):
                    configure_local._verify_darwin_no_additional_acl(acl_fd)
                configure_local._verify_darwin_no_additional_acl(no_acl_fd)
            finally:
                os.close(no_acl_fd)
                os.close(acl_fd)
                subprocess.run(
                    ["/bin/chmod", "-N", str(acl_file)],
                    capture_output=True,
                    text=True,
                    check=False,
                    timeout=20,
                )

    def test_created_file_acl_is_verified_before_success(self):
        with tempfile.TemporaryDirectory() as temp:
            target = Path(temp) / "voice"
            original_verify = configure_local._verify_darwin_no_additional_acl
            regular_file_checks = 0

            def reject_first_regular_file(fd):
                nonlocal regular_file_checks
                if os.path.isfile(f"/dev/fd/{fd}"):
                    regular_file_checks += 1
                    raise ValueError("synthetic file ACL")
                return original_verify(fd)

            with mock.patch(
                "configure_local._verify_darwin_no_additional_acl",
                side_effect=reject_first_regular_file,
            ):
                with self.assertRaisesRegex(_CleanupIncomplete, "manual"):
                    configure(target)
            self.assertGreaterEqual(regular_file_checks, 1)
            self.assertFalse(target.exists())

    def test_cli_symlink_output_is_rejected_without_writing_outside_target(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            outside = root / "outside"
            outside.mkdir()
            redirected = root / "redirected"
            redirected.symlink_to(outside, target_is_directory=True)

            completed = self._run_cli("--output", str(redirected / "voice"))
            self.assertNotEqual(completed.returncode, 0)
            self.assertFalse((outside / "voice").exists())
            self.assertNotIn("ApiSecret", completed.stdout + completed.stderr)

    def test_cli_errors_are_sanitized_and_actionable(self):
        with tempfile.TemporaryDirectory() as temp:
            private_marker = "SYNTHETIC_PRIVATE_PATH_" + "x" * 240
            completed = self._run_cli("--output", str(Path(temp) / private_marker))
            output = completed.stdout + completed.stderr
            self.assertNotEqual(completed.returncode, 0)
            self.assertNotIn(private_marker, output)
            self.assertNotIn("Traceback", output)
            self.assertTrue(
                "check the private parent and permissions" in output
                or "private cleanup is incomplete" in output
                or "Configuration output name is too long" in output
            )

    def test_cli_failure_does_not_print_generated_credentials(self):
        with tempfile.TemporaryDirectory() as temp:
            completed = self._run_cli(
                "--output", str(Path(temp) / "voice"), "--node-ip", "192.0.2.44"
            )
            self.assertNotEqual(completed.returncode, 0)
            self.assertNotIn("ApiSecret", completed.stdout + completed.stderr)
            self.assertNotIn("cg", completed.stdout + completed.stderr)

    @staticmethod
    def _run_cli(*args):
        return subprocess.run(
            [sys.executable, str(Path(__file__).with_name("configure_local.py")), *args],
            capture_output=True,
            text=True,
            check=False,
        )


if __name__ == "__main__":
    unittest.main()
