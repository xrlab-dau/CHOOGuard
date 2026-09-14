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
from configure_local import (
    _CleanupIncomplete,
    _OutputPathChanged,
    _StagingOwnershipUnproven,
    configure,
)


# The staging-ownership and publication-boundary fixtures live on their own
# class so that "the boundary suite" is a runnable selector -
# `python3 -m unittest test_configure_local.StagingPublicationBoundaryTests` -
# instead of a claim in a document.  The fixtures that predate that work stay
# on LocalVoiceConfigurationTests, unchanged and equally selectable.
_BOUNDARY_FIXTURES = (
    "test_unproven_staging_ownership_blocks_every_credential_write",
    "test_staging_swap_after_credentials_is_refused_at_the_publication_gate",
    "test_symlinked_staging_entry_is_rejected_without_writing_outside_target",
    "test_foreign_output_directory_is_never_published_into_or_deleted",
    "test_publication_failure_rolls_back_exactly_the_owned_directory",
    "test_ownership_evidence_is_rechecked_on_both_sides_of_the_binding",
    "test_entry_injected_after_the_last_write_blocks_publication",
    "test_owned_name_swapped_for_other_bytes_blocks_publication",
)
_LEGACY_FIXTURE_TOTAL = 24


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
            # The output name was created outside this run while this run was
            # staging credentials: `_validate_output` proved the name absent
            # before staging began.  Rollback removes the credentials this run
            # acquired but preserves the staged directory for manual
            # reconciliation and reports incomplete cleanup, so the residue
            # stays until an operator removes it.
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


class StagingPublicationBoundaryTests(unittest.TestCase):
    def test_unproven_staging_ownership_blocks_every_credential_write(self):
        """The P0 boundary: no credential byte and no publication without proof."""
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            writes = []
            renames = []

            def refuse_proof(parent_fd, staging_name, directory_fd):
                return None

            def record_write(path, text, **kwargs):
                writes.append(Path(path).name)
                raise AssertionError("credentials must not be written without ownership proof")

            def record_rename(*args, **kwargs):
                renames.append(args)
                raise AssertionError("publication must not happen without ownership proof")

            with mock.patch(
                "configure_local._prove_staging_ownership", side_effect=refuse_proof
            ):
                with mock.patch("configure_local.write_private", side_effect=record_write):
                    with mock.patch(
                        "configure_local._rename_exclusive", side_effect=record_rename
                    ):
                        with self.assertRaises(_StagingOwnershipUnproven) as caught:
                            configure(target)
            self.assertEqual(writes, [])
            self.assertEqual(renames, [])
            self.assertFalse(target.exists())
            self.assertIn("manual cleanup", str(caught.exception))
            # The staging entry is preserved: an unproven inode is never rmdir'd.
            residues = list(root.glob(".voice.*.tmp"))
            self.assertEqual(len(residues), 1)
            self.assertEqual(list(residues[0].iterdir()), [])

    def test_staging_swap_after_credentials_is_refused_at_the_publication_gate(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            original_write = configure_local.write_private
            foreign = []

            def swap_after_credentials(path, text, **kwargs):
                result = original_write(path, text, **kwargs)
                if Path(path).name == "server-voice.json":
                    ours = list(root.glob(".voice.*.tmp"))
                    self.assertEqual(len(ours), 1)
                    ours[0].rename(root / "parked-staging")
                    replacement = root / ours[0].name
                    replacement.mkdir(mode=0o700)
                    (replacement / "foreign-marker").write_text("keep")
                    foreign.append(replacement)
                return result

            with mock.patch(
                "configure_local.write_private", side_effect=swap_after_credentials
            ):
                with self.assertRaises(_StagingOwnershipUnproven) as caught:
                    configure(target)
            self.assertEqual(len(foreign), 1)
            self.assertIn("manual cleanup", str(caught.exception))
            # Nothing was published, and the foreign replacement is untouched:
            # no generated file was written into it and it was not deleted.
            self.assertFalse(target.exists())
            self.assertEqual(
                sorted(item.name for item in foreign[0].iterdir()), ["foreign-marker"]
            )
            # The replaced staging inode is no longer reachable by name, so it
            # is preserved rather than rmdir'd on an assumption; the generated
            # files inside it were removed by exact-owned rollback.
            self.assertTrue((root / "parked-staging").is_dir())
            self.assertEqual(list((root / "parked-staging").iterdir()), [])

    def test_symlinked_staging_entry_is_rejected_without_writing_outside_target(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            outside = root / "outside"
            outside.mkdir(mode=0o700)
            original_open = configure_local.os.open
            planted = []

            def plant_symlink(path, flags, mode=0o777, *, dir_fd=None):
                if (
                    dir_fd is not None
                    and isinstance(path, str)
                    and path.startswith(".voice.")
                    and path.endswith(".tmp")
                    and not planted
                ):
                    ours = list(root.glob(".voice.*.tmp"))[0]
                    ours.rename(root / "parked-staging")
                    os.symlink(outside, root / ours.name)
                    planted.append(True)
                return original_open(path, flags, mode, dir_fd=dir_fd)

            with mock.patch("configure_local.os.open", side_effect=plant_symlink):
                with self.assertRaises((OSError, ValueError)):
                    configure(target)
            self.assertEqual(len(planted), 1)
            # O_NOFOLLOW refuses the symlink, so the adoption target is empty
            # and no credential was written through it.
            self.assertEqual(list(outside.iterdir()), [])
            self.assertFalse(target.exists())

    def test_foreign_output_directory_is_never_published_into_or_deleted(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            target.mkdir(mode=0o700)
            (target / "foreign-marker").write_text("keep")
            foreign_file = root / "foreign-file"
            foreign_file.write_text("keep")

            with self.assertRaises(ValueError):
                configure(target)

            self.assertEqual(
                sorted(item.name for item in target.iterdir()), ["foreign-marker"]
            )
            self.assertEqual((target / "foreign-marker").read_text(), "keep")
            self.assertEqual(foreign_file.read_text(), "keep")
            self.assertEqual(list(root.glob(".voice.*.tmp")), [])

    def test_publication_failure_rolls_back_exactly_the_owned_directory(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            foreign_file = root / "foreign-file"
            foreign_file.write_text("keep")

            def fail_publication(parent_fd, staging_name, output_name):
                raise OSError("synthetic rename failure")

            with mock.patch(
                "configure_local._rename_exclusive", side_effect=fail_publication
            ):
                with self.assertRaises(OSError):
                    configure(target)
            self.assertFalse(target.exists())
            # Exact-owned rollback completed: every generated file and the proven
            # staging directory are gone, and nothing foreign was touched.
            self.assertEqual(list(root.glob(".voice.*.tmp")), [])
            self.assertEqual(foreign_file.read_text(), "keep")
            self.assertEqual(sorted(item.name for item in root.iterdir()), ["foreign-file"])

    def test_entry_injected_after_the_last_write_blocks_publication(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            real_write_private = configure_local.write_private
            written = []

            def write_then_inject(path, text, *, dir_fd=None, on_acquired=None):
                result = real_write_private(
                    path, text, dir_fd=dir_fd, on_acquired=on_acquired
                )
                written.append(path)
                if len(written) == len(configure_local._GENERATED_NAMES):
                    injected_fd = os.open(
                        "injected.env",
                        os.O_WRONLY | os.O_CREAT | os.O_EXCL,
                        0o600,
                        dir_fd=dir_fd,
                    )
                    os.close(injected_fd)
                return result

            with mock.patch(
                "configure_local.write_private", side_effect=write_then_inject
            ):
                with self.assertRaises(_CleanupIncomplete):
                    configure(target)

            # The rename would have moved the whole directory, so an entry this
            # run did not create must never reach the output name as private
            # configuration: publication is refused and the entry survives for
            # manual cleanup while the run's own credentials are rolled back.
            self.assertFalse(target.exists())
            residues = list(root.glob(".voice.*.tmp"))
            self.assertEqual(len(residues), 1)
            self.assertEqual(
                sorted(item.name for item in residues[0].iterdir()), ["injected.env"]
            )

    def test_owned_name_swapped_for_other_bytes_blocks_publication(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            real_write_private = configure_local.write_private
            written = []

            def write_then_swap(path, text, *, dir_fd=None, on_acquired=None):
                result = real_write_private(
                    path, text, dir_fd=dir_fd, on_acquired=on_acquired
                )
                written.append(path)
                if len(written) == len(configure_local._GENERATED_NAMES):
                    # Same name, same file count, different inode: no name-set
                    # check can see this, only a per-entry identity comparison.
                    os.unlink("keys.yaml", dir_fd=dir_fd)
                    replacement = os.open(
                        "keys.yaml", os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600,
                        dir_fd=dir_fd,
                    )
                    os.write(replacement, b"attacker: substituted\n")
                    os.close(replacement)
                return result

            with mock.patch(
                "configure_local.write_private", side_effect=write_then_swap
            ):
                with self.assertRaises(_CleanupIncomplete):
                    configure(target)

            self.assertFalse(target.exists())
            residues = list(root.glob(".voice.*.tmp"))
            self.assertEqual(len(residues), 1)
            self.assertEqual(
                (residues[0] / "keys.yaml").read_bytes(), b"attacker: substituted\n"
            )

    def test_ownership_evidence_is_rechecked_on_both_sides_of_the_binding(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            seen = []
            original_prove = configure_local._prove_staging_ownership

            def observe_proof(parent_fd, staging_name, directory_fd):
                evidence = original_prove(parent_fd, staging_name, directory_fd)
                seen.append(
                    (
                        evidence is not None,
                        os.fstat(directory_fd).st_ino,
                        os.stat(staging_name, dir_fd=parent_fd, follow_symlinks=False).st_ino,
                    )
                )
                return evidence

            with mock.patch(
                "configure_local._prove_staging_ownership", side_effect=observe_proof
            ):
                configure(target)
            # A proof is taken before the credentials and again at publication.
            self.assertGreaterEqual(len(seen), 2)
            for proven, descriptor_ino, entry_ino in seen:
                self.assertTrue(proven)
                self.assertEqual(descriptor_ino, entry_ino)
            self.assertTrue((target / "keys.yaml").is_file())

    @unittest.skipUnless(sys.platform == "darwin", "requires Darwin ACL API")
    def test_adopted_empty_directory_in_the_create_to_pin_window_is_published(self):
        """Pin the one window this backend cannot authenticate (D1, branch a).

        README 13 documents the create->open window as unauthenticated.  Darwin
        and Python create a directory by pathname and open it separately, and no
        mkdir variant returns the descriptor of the directory it just created,
        so re-reading the name - however many times - stays inside the same
        window: a re-check cannot see a swap that already happened before the
        first read.  Narrowing this in code would need an atomic create-and-pin
        primitive, which this platform does not expose, so the behaviour below
        is pinned instead of closed, and this fixture fails the day a backend
        that can pin at creation changes it.

        The window also needs a same-account actor: the adopted directory must
        be 0700 and current-user-owned to pass `_verify_private_directory`, and a
        different principal can neither own a directory as this user nor write
        into the 0700 parent.  That is the same-account scope README 13 excludes.

        Pinned behaviour: an EMPTY adopted directory satisfies every identity
        and entry-set gate and is published as this run's private output.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            parked = root / "parked-staging"
            adopted_inodes = []
            swapped = []
            original_open = configure_local.os.open

            def adopt_empty_directory(path, flags, mode=0o777, *, dir_fd=None):
                if (
                    dir_fd is not None
                    and isinstance(path, str)
                    and path.startswith(f".{target.name}.")
                    and path.endswith(".tmp")
                    and not swapped
                ):
                    ours = list(root.glob(".voice.*.tmp"))[0]
                    ours.rename(parked)
                    replacement = root / ours.name
                    replacement.mkdir(mode=0o700)
                    adopted_inodes.append(replacement.stat().st_ino)
                    swapped.append(True)
                return original_open(path, flags, mode, dir_fd=dir_fd)

            with mock.patch("configure_local.os.open", side_effect=adopt_empty_directory):
                result = configure(target)

            self.assertEqual(len(swapped), 1)
            self.assertEqual(result["status"], "private_local_config_created")
            # The published directory is the adopted inode, not the one this run
            # created, and it now carries this run's credentials.
            self.assertEqual(target.stat().st_ino, adopted_inodes[0])
            self.assertTrue((target / "keys.yaml").is_file())
            self.assertTrue((target / "livekit.yaml").is_file())
            self.assertTrue((target / "server-voice.json").is_file())
            # The directory this run did create is left behind, empty.
            self.assertTrue(parked.is_dir())
            self.assertEqual(list(parked.iterdir()), [])

    def test_adopted_directory_with_a_foreign_entry_is_refused(self):
        """The same create->pin window, with a payload the entry-set gate sees.

        The adopted directory still satisfies the identity proof, but it now
        holds an entry this run never acquired, so publication is refused and
        the foreign entry is preserved for manual cleanup.  Together with the
        empty-directory fixture above this pins both branches of the window.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            parked = root / "parked-staging"
            adopted_inodes = []
            swapped = []
            original_open = configure_local.os.open

            def adopt_directory_with_foreign_entry(path, flags, mode=0o777, *, dir_fd=None):
                if (
                    dir_fd is not None
                    and isinstance(path, str)
                    and path.startswith(f".{target.name}.")
                    and path.endswith(".tmp")
                    and not swapped
                ):
                    ours = list(root.glob(".voice.*.tmp"))[0]
                    ours.rename(parked)
                    replacement = root / ours.name
                    replacement.mkdir(mode=0o700)
                    (replacement / "foreign-marker").write_text("keep")
                    adopted_inodes.append(replacement.stat().st_ino)
                    swapped.append(True)
                return original_open(path, flags, mode, dir_fd=dir_fd)

            with mock.patch(
                "configure_local.os.open",
                side_effect=adopt_directory_with_foreign_entry,
            ):
                with self.assertRaises(_CleanupIncomplete):
                    configure(target)

            self.assertEqual(len(swapped), 1)
            self.assertFalse(target.exists())
            residues = list(root.glob(".voice.*.tmp"))
            self.assertEqual(len(residues), 1)
            self.assertEqual(residues[0].stat().st_ino, adopted_inodes[0])
            # The foreign payload survives; only this run's own credentials were
            # rolled back, and the foreign inode was never rmdir'd.
            self.assertEqual(sorted(item.name for item in residues[0].iterdir()), ["foreign-marker"])
            self.assertEqual((residues[0] / "foreign-marker").read_text(), "keep")

    def test_parent_privacy_regression_before_publication_is_refused(self):
        """The trusted-parent precondition is re-verified at publication (D2).

        README 9 claims the trusted-parent precondition (0700, current-user
        owned, no additional ACL) is re-derived immediately before the exclusive
        publication rename.  This fixture makes the parent 0755 after the last
        credential write and before publication, so only that re-verification
        can stop the run.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            parent = root / "parent"
            parent.mkdir(mode=0o700)
            target = parent / "voice"
            real_write_private = configure_local.write_private
            written = []

            def loosen_parent_after_last_write(
                path, text, *, dir_fd=None, on_acquired=None
            ):
                result = real_write_private(
                    path, text, dir_fd=dir_fd, on_acquired=on_acquired
                )
                written.append(Path(path).name)
                if len(written) == len(configure_local._GENERATED_NAMES):
                    parent.chmod(0o755)
                return result

            with mock.patch(
                "configure_local.write_private",
                side_effect=loosen_parent_after_last_write,
            ):
                with self.assertRaisesRegex(
                    ValueError,
                    "Configuration directory privacy could not be verified",
                ):
                    configure(target)

            self.assertEqual(written, list(configure_local._GENERATED_NAMES))
            self.assertFalse(target.exists())
            # Nothing was published and the exact-owned rollback completed.
            self.assertEqual(list(parent.glob(".voice.*.tmp")), [])

    def test_binding_recheck_after_the_ownership_gate_blocks_publication(self):
        """The pre-rename binding check is load-bearing on its own (D3).

        The staging name is replaced with a different directory in the window
        between the ownership gate and the pre-rename binding check, so only
        that check can refuse: the ownership gate has already returned, the
        descriptor and the identity recorded at open are unchanged, and the
        entry set is still exactly what this run acquired.  Publication must
        never be attempted.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            parked = root / "parked-staging"
            original_require = configure_local._require_owned_staging
            original_rename = configure_local._rename_exclusive
            renames = []
            replaced = []

            def require_then_replace(*args, **kwargs):
                result = original_require(*args, **kwargs)
                ours = list(root.glob(".voice.*.tmp"))[0]
                ours.rename(parked)
                replacement = root / ours.name
                replacement.mkdir(mode=0o700)
                (replacement / "foreign-marker").write_text("keep")
                replaced.append(replacement)
                return result

            def record_rename(parent_fd, staging_name, output_name):
                renames.append((staging_name, output_name))
                return original_rename(parent_fd, staging_name, output_name)

            with mock.patch(
                "configure_local._require_owned_staging",
                side_effect=require_then_replace,
            ):
                with mock.patch(
                    "configure_local._rename_exclusive", side_effect=record_rename
                ):
                    with self.assertRaises(_CleanupIncomplete):
                        configure(target)

            self.assertEqual(len(replaced), 1)
            # The rename never ran, so nothing was published even though the
            # swap happened after the ownership evidence was re-derived.
            self.assertEqual(renames, [])
            self.assertFalse(target.exists())
            self.assertEqual(
                sorted(item.name for item in replaced[0].iterdir()), ["foreign-marker"]
            )
            # The replaced inode is no longer reachable by name, so it is
            # preserved rather than rmdir'd on an assumption; the credentials
            # this run acquired were removed from it.
            self.assertTrue(parked.is_dir())
            self.assertEqual(list(parked.iterdir()), [])

    def test_unlistable_staging_directory_is_refused_and_preserved(self):
        """The fail-closed arm of the entry-set gate (D4).

        `_unowned_staging_entries` returns None when the pinned directory cannot
        be listed, and None means the entry set publication would rename is
        unknown.  That decision must be the refusal branch and must not be
        confused with the empty branch that publishes, and the staged directory
        must survive it: an entry set that cannot be established is not an entry
        set this run may delete.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            original_listdir = configure_local.os.listdir
            original_rename = configure_local._rename_exclusive
            renames = []

            def deny_staging_listing(path):
                if isinstance(path, int):
                    raise OSError("synthetic listing failure")
                return original_listdir(path)

            def record_rename(parent_fd, staging_name, output_name):
                renames.append((staging_name, output_name))
                return original_rename(parent_fd, staging_name, output_name)

            with mock.patch(
                "configure_local.os.listdir", side_effect=deny_staging_listing
            ):
                with mock.patch(
                    "configure_local._rename_exclusive", side_effect=record_rename
                ):
                    with self.assertRaises(_CleanupIncomplete):
                        configure(target)

            self.assertEqual(renames, [])
            self.assertFalse(target.exists())
            residues = list(root.glob(".voice.*.tmp"))
            self.assertEqual(len(residues), 1)
            # Nothing was published and the staged directory was not removed:
            # its credentials are rolled back, the directory stays for manual
            # cleanup.
            self.assertEqual(list(residues[0].iterdir()), [])

    def test_acquired_name_swapped_for_an_outside_symlink_blocks_publication(self):
        """An acquired name may not resolve through a symlink (D5).

        keys.yaml is moved out of the staging directory and replaced by a
        symlink to the file it became.  The inode is still the one this run
        acquired, so an identity check that follows the symlink would accept it
        and publish a private directory whose keys.yaml is a link into a
        location this run does not control.  The entry check must read the entry
        itself, not its target.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            outside = root / "outside"
            outside.mkdir(mode=0o700)
            real_write_private = configure_local.write_private
            written = []
            linked = []

            def write_then_link(path, text, *, dir_fd=None, on_acquired=None):
                result = real_write_private(
                    path, text, dir_fd=dir_fd, on_acquired=on_acquired
                )
                written.append(Path(path).name)
                if len(written) == len(configure_local._GENERATED_NAMES):
                    staging = list(root.glob(".voice.*.tmp"))[0]
                    os.rename(staging / "keys.yaml", outside / "keys.yaml")
                    os.symlink(str(outside / "keys.yaml"), staging / "keys.yaml")
                    linked.append(outside / "keys.yaml")
                return result

            with mock.patch(
                "configure_local.write_private", side_effect=write_then_link
            ):
                with self.assertRaises(_CleanupIncomplete):
                    configure(target)

            self.assertEqual(len(linked), 1)
            self.assertFalse(target.exists())
            residues = list(root.glob(".voice.*.tmp"))
            self.assertEqual(len(residues), 1)
            # The substituted link is the only thing left in the staging
            # directory: an entry whose identity no longer holds is never
            # unlinked and never published.
            self.assertEqual(sorted(item.name for item in residues[0].iterdir()), ["keys.yaml"])
            self.assertTrue((residues[0] / "keys.yaml").is_symlink())
            self.assertTrue(linked[0].is_file())
            self.assertFalse(linked[0].is_symlink())

    def test_legacy_and_boundary_suites_are_separately_selectable(self):
        """Enforce the split into runnable selectors (D6).

        The two classes are the selector: legacy fixtures stay on
        LocalVoiceConfigurationTests and the staging-ownership boundary
        fixtures on StagingPublicationBoundaryTests, so each set can be run
        alone with `python3 -m unittest test_configure_local.<class>` and the
        boundary count cannot drift into the legacy total unnoticed.
        """
        legacy = unittest.defaultTestLoader.loadTestsFromTestCase(
            LocalVoiceConfigurationTests
        )
        boundary = unittest.defaultTestLoader.loadTestsFromTestCase(
            StagingPublicationBoundaryTests
        )
        self.assertEqual(legacy.countTestCases(), _LEGACY_FIXTURE_TOTAL)
        boundary_names = {case._testMethodName for case in boundary}
        for name in _BOUNDARY_FIXTURES:
            self.assertIn(name, boundary_names)
        self.assertGreaterEqual(boundary.countTestCases(), len(_BOUNDARY_FIXTURES))


if __name__ == "__main__":
    unittest.main()
