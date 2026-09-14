import concurrent.futures
import contextlib
import io
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
    _CleanupTargetMismatch,
    _OutputPathChanged,
    _StagingEntryVanished,
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
    "test_acquired_name_removed_before_publication_blocks_publication",
    "test_inexact_removal_in_the_cleanup_window_is_detected_and_reported",
    "test_entry_replaced_after_the_gate_is_refused_after_publication",
    "test_entry_injected_after_the_gate_is_refused_after_publication",
    "test_vanished_entry_after_the_gate_is_refused_after_publication",
    "test_unlistable_published_directory_is_refused_after_publication",
    "test_refusals_reach_the_cli_with_their_own_wording",
    "test_unverifiable_post_removal_scan_is_reported",
    "test_over_long_output_name_is_refused_before_anything_is_created",
    "test_junction_component_is_refused_without_creating_directories",
    "test_exhausted_staging_name_collisions_are_refused",
    "test_acquired_entry_rewritten_in_place_is_refused_after_publication",
    "test_bytes_injected_into_an_acquired_entry_are_refused_after_publication",
    "test_refusal_reaches_the_cli_though_cleanup_failed_too",
    "test_cleanup_failure_never_replaces_the_wording_the_run_established",
    "test_created_file_that_does_not_hold_the_written_bytes_is_refused",
    "test_staging_directory_that_cannot_be_pinned_is_reported_for_cleanup",
    "test_ownership_binding_is_refused_without_both_sides_to_bind",
    "test_a_name_is_not_removable_without_proven_ownership",
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
                    with self.assertRaises(_OutputPathChanged) as caught:
                        configure(target)

            # The refusal is reported as itself, not folded into the general
            # incomplete-cleanup sentence: what stopped the run was an entry set
            # that could not be established, and that is what the operator can
            # act on.  The cleanup consequence is appended to it.
            self.assertIsInstance(
                caught.exception, configure_local._StagingEntrySetUnproven
            )
            self.assertIn("could not be enumerated", str(caught.exception))
            self.assertIn("manual cleanup", str(caught.exception))
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

    def test_acquired_name_removed_before_publication_blocks_publication(self):
        """A vanished acquired name must stop publication (D1).

        `_unaccounted_staging_entries` classifies what is present, so a name
        this run acquired and then lost leaves no trace in it: the directory
        still holds only unswapped owned entries, the comparison reports
        nothing, and a directory missing one of its three files is published as
        this run's private configuration.  The publication gate requires the
        whole acquired name set to still exist and every present name to
        resolve to the identity it was acquired under, so the disappeared name
        is refused and reported as a disappearance, apart from the injected or
        replaced case.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            real_write_private = configure_local.write_private
            written = []
            unlinked = []

            def write_then_unlink(path, text, *, dir_fd=None, on_acquired=None):
                result = real_write_private(
                    path, text, dir_fd=dir_fd, on_acquired=on_acquired
                )
                written.append(Path(path).name)
                if len(written) == len(configure_local._GENERATED_NAMES):
                    os.unlink("keys.yaml", dir_fd=dir_fd)
                    unlinked.append("keys.yaml")
                return result

            with mock.patch(
                "configure_local.write_private", side_effect=write_then_unlink
            ):
                with self.assertRaises(_StagingEntryVanished) as caught:
                    configure(target)

            self.assertEqual(len(unlinked), 1)
            self.assertFalse(target.exists())
            # The refusal names the disappearance, not foreign entries: those
            # are a different finding about the same gate.
            self.assertIn("no longer holds every entry", str(caught.exception))
            self.assertNotIn("did not create", str(caught.exception))
            # Rollback stays exact-owned: the two credentials this run still
            # held are unlinked and its own now-empty staging directory is
            # removed, so only the vanished name is left unaccounted for.
            self.assertEqual(list(root.glob(".voice.*.tmp")), [])

    def test_inexact_removal_in_the_cleanup_window_is_detected_and_reported(self):
        """The rollback stat->act window is detected after the fact (D2).

        Rollback proves the cleanup name still resolves to the staging inode
        and then calls `os.rmdir`, which removes whatever that name addresses
        at that later instant.  A same-account actor that renames the proven
        directory away and parks an empty directory under the same name inside
        that window makes this run remove a directory it did not create and
        leave its own behind.  Darwin keeps an open descriptor's `st_nlink` at
        2 after `rmdir`, so the descriptor cannot say what was removed; the
        removal is re-examined by re-enumerating the parent afterwards.  That
        is detection, not prevention, and an exactly-owned removal must keep
        reporting what it already reported.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            original_rmdir = configure_local.os.rmdir
            swapped = []

            def swap_then_remove(path, *, dir_fd=None):
                ours = list(root.glob(".voice.*.tmp"))[0]
                os.rename(ours, root / "parked-staging")
                replacement = root / ours.name
                replacement.mkdir(mode=0o700)
                swapped.append(path)
                return original_rmdir(path, dir_fd=dir_fd)

            def fail_publication(parent_fd, staging_name, output_name):
                raise OSError("synthetic rename failure")

            with mock.patch(
                "configure_local._rename_exclusive", side_effect=fail_publication
            ):
                with mock.patch(
                    "configure_local.os.rmdir", side_effect=swap_then_remove
                ):
                    with self.assertRaises(_CleanupTargetMismatch) as caught:
                        configure(target)

            self.assertEqual(len(swapped), 1)
            self.assertFalse(target.exists())
            # The directory this run created is still on disk under the name
            # the actor parked it at, empty because exact-owned rollback had
            # already unlinked its credentials, and the planted directory the
            # run actually removed is gone.
            self.assertTrue((root / "parked-staging").is_dir())
            self.assertEqual(list((root / "parked-staging").iterdir()), [])
            self.assertEqual(list(root.glob(".voice.*.tmp")), [])
            message = str(caught.exception)
            self.assertIn("did not create", message)
            self.assertIn("left behind", message)

        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"

            def fail_publication(parent_fd, staging_name, output_name):
                raise OSError("synthetic rename failure")

            with mock.patch(
                "configure_local._rename_exclusive", side_effect=fail_publication
            ):
                with self.assertRaises(OSError) as caught:
                    configure(target)

            # An exact-owned removal keeps its own behaviour and message: the
            # original failure is reported unchanged and the directory is gone.
            self.assertNotIsInstance(caught.exception, _CleanupTargetMismatch)
            self.assertEqual(str(caught.exception), "synthetic rename failure")
            self.assertFalse(target.exists())
            self.assertEqual(list(root.glob(".voice.*.tmp")), [])
            self.assertEqual(sorted(item.name for item in root.iterdir()), [])

    @staticmethod
    def _run_main_in_process(target):
        """Run `main` the way the CLI entry point does, and return its streams (D4)."""
        out, err = io.StringIO(), io.StringIO()
        with mock.patch.object(
            sys, "argv", ["configure_local.py", "--output", str(target)]
        ):
            with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
                with contextlib.suppress(SystemExit):
                    configure_local.main()
        return out.getvalue(), err.getvalue()

    def test_entry_replaced_after_the_gate_is_refused_after_publication(self):
        """The window between the entry-set gate and the rename (E-arm).

        The gate derives the entry set from the pinned descriptor and returns;
        the rename that publishes the whole directory happens in a later step,
        and the checks after it re-derive the parent path and the output inode -
        both of which name the same directory before and after a swap that only
        changes what the directory holds.  Replacing an acquired name with other
        bytes inside that window used to publish a private directory whose
        server-voice.json is not the configuration this run wrote while the
        keys.yaml beside it still is, and configure() still reported success.

        The published directory is re-derived after the rename and before
        success is reported, so the mismatch is refused.  Detection after the
        fact, not prevention: the rename has already happened, the published
        directory is preserved for manual cleanup, and the substituted entry,
        which this run never acquired, is never unlinked.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            original_rename = configure_local._rename_exclusive
            substitutions = []

            def substitute_before_rename(parent_fd, staging_name, output_name):
                staging = root / staging_name
                os.unlink(staging / "server-voice.json")
                substitution = staging / "server-voice.json"
                substitution.write_bytes(b'{"ApiKey": "ATTACKERKEY"}')
                os.chmod(substitution, 0o600)
                substitutions.append(substitution)
                return original_rename(parent_fd, staging_name, output_name)

            with mock.patch(
                "configure_local._rename_exclusive", side_effect=substitute_before_rename
            ):
                with self.assertRaises(OSError) as caught:
                    configure(target)
            self.assertIsInstance(
                caught.exception, configure_local._PublishedEntryNotOwned
            )

            self.assertEqual(len(substitutions), 1)
            message = str(caught.exception)
            # The refusal states what was found and that it was found too late
            # to prevent: the rename had already published the directory.
            self.assertIn("did not create", message)
            self.assertIn("detection after the fact", message)
            self.assertIn("manual cleanup", message)
            # The substituted bytes are never handed out as this run's private
            # configuration and are never unlinked: they are not this run's file
            # to remove.  The credentials this run did write are rolled back.
            self.assertTrue(target.is_dir())
            self.assertEqual(
                sorted(item.name for item in target.iterdir()), ["server-voice.json"]
            )
            self.assertEqual(
                (target / "server-voice.json").read_bytes(), b'{"ApiKey": "ATTACKERKEY"}'
            )
            self.assertEqual(list(root.glob(".voice.*.tmp")), [])

    def test_entry_injected_after_the_gate_is_refused_after_publication(self):
        """The same window with an entry this run never acquired (F-arm).

        The identity proof and the parent path are untouched - the directory is
        the one this run created - so only a re-derivation of the published
        directory's entry set can see the injected name, and it runs after the
        rename, so the finding is reported rather than prevented.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            original_rename = configure_local._rename_exclusive
            injected = []

            def inject_before_rename(parent_fd, staging_name, output_name):
                staging = root / staging_name
                (staging / "injected-backdoor.yaml").write_text("attacker: yes\n")
                os.chmod(staging / "injected-backdoor.yaml", 0o600)
                injected.append(staging / "injected-backdoor.yaml")
                return original_rename(parent_fd, staging_name, output_name)

            with mock.patch(
                "configure_local._rename_exclusive", side_effect=inject_before_rename
            ):
                with self.assertRaises(OSError) as caught:
                    configure(target)
            self.assertIsInstance(
                caught.exception, configure_local._PublishedEntryNotOwned
            )

            self.assertEqual(len(injected), 1)
            message = str(caught.exception)
            self.assertIn("did not create", message)
            self.assertIn("detection after the fact", message)
            # Nothing is reported as a created configuration, and the injected
            # entry survives for manual cleanup: this run never acquired it and
            # never removes a name it did not create.
            self.assertTrue(target.is_dir())
            self.assertEqual(
                sorted(item.name for item in target.iterdir()), ["injected-backdoor.yaml"]
            )
            self.assertEqual(
                (target / "injected-backdoor.yaml").read_text(), "attacker: yes\n"
            )

    def test_vanished_entry_after_the_gate_is_refused_after_publication(self):
        """A lost acquired name in the same window, reported as a loss.

        The published directory is missing a file it was supposed to hold, so
        the run refuses and reports a disappearance rather than an injected
        entry, exactly as the pre-rename gate does.  Every name it still
        accounts for is rolled back, and because the published directory is
        still the proven inode and holds nothing else, the rollback removes it.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            original_rename = configure_local._rename_exclusive

            def unlink_before_rename(parent_fd, staging_name, output_name):
                os.unlink(root / staging_name / "keys.yaml")
                return original_rename(parent_fd, staging_name, output_name)

            with mock.patch(
                "configure_local._rename_exclusive", side_effect=unlink_before_rename
            ):
                with self.assertRaises(OSError) as caught:
                    configure(target)
            self.assertIsInstance(
                caught.exception, configure_local._PublishedEntryVanished
            )

            message = str(caught.exception)
            self.assertIn("no longer holds every entry", message)
            self.assertIn("detection after the fact", message)
            self.assertNotIn("did not create", message)
            self.assertFalse(target.exists())
            self.assertEqual(list(root.glob(".voice.*.tmp")), [])
            self.assertEqual(sorted(item.name for item in root.iterdir()), [])

    def test_unlistable_published_directory_is_refused_after_publication(self):
        """The re-derivation needs a listing it may not be able to get (D3).

        The re-derivation that follows the rename reads the published directory
        the way the pre-rename gate reads the staged one, so the same
        fail-closed branch applies: a directory that cannot be listed leaves the
        entry set unknown, which is not the finding an injected or substituted
        entry produces and must be refused rather than assumed clean.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            original_rename = configure_local._rename_exclusive
            original_listdir = configure_local.os.listdir
            published = []

            def rename_then_deny_listing(parent_fd, staging_name, output_name):
                result = original_rename(parent_fd, staging_name, output_name)
                published.append(output_name)
                return result

            def deny_listing_once_published(path):
                if published and isinstance(path, int):
                    raise OSError("synthetic listing failure")
                return original_listdir(path)

            with mock.patch(
                "configure_local._rename_exclusive", side_effect=rename_then_deny_listing
            ):
                with mock.patch(
                    "configure_local.os.listdir",
                    side_effect=deny_listing_once_published,
                ):
                    with self.assertRaises(OSError) as caught:
                        configure(target)

            self.assertEqual(len(published), 1)
            self.assertIsInstance(
                caught.exception, configure_local._PublishedEntrySetUnproven
            )
            message = str(caught.exception)
            self.assertIn("could not be re-enumerated", message)
            self.assertIn("detection after the fact", message)
            # The credentials this run acquired are still rolled back, and the
            # directory holding them was the proven inode.
            self.assertFalse(target.exists())
            self.assertEqual(list(root.glob(".voice.*.tmp")), [])
            self.assertEqual(sorted(item.name for item in root.iterdir()), [])

    def test_refusals_reach_the_cli_with_their_own_wording(self):
        """A refusal is never reported as a permissions problem (D4).

        `main` sends unexpected OS failures to "check the private parent and
        permissions".  A closed gate is not one of those: the parent and its
        permissions are exactly as they were, so reporting a refusal with that
        hint sends the operator after a cause that is not there.  Each refusal
        carries its own wording and must reach the CLI with it.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "vanished"
            real_write_private = configure_local.write_private
            written = []

            def write_then_unlink(path, text, *, dir_fd=None, on_acquired=None):
                result = real_write_private(
                    path, text, dir_fd=dir_fd, on_acquired=on_acquired
                )
                written.append(Path(path).name)
                if len(written) == len(configure_local._GENERATED_NAMES):
                    os.unlink("keys.yaml", dir_fd=dir_fd)
                return result

            with mock.patch(
                "configure_local.write_private", side_effect=write_then_unlink
            ):
                (stdout, output) = self._run_main_in_process(target)

            self.assertEqual(stdout, "")
            self.assertIn("no longer holds every entry", output)
            self.assertNotIn("check the private parent and permissions", output)

        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "substituted"
            original_rename = configure_local._rename_exclusive

            def substitute_before_rename(parent_fd, staging_name, output_name):
                staging = root / staging_name
                os.unlink(staging / "server-voice.json")
                (staging / "server-voice.json").write_bytes(b'{"ApiKey": "X"}')
                return original_rename(parent_fd, staging_name, output_name)

            with mock.patch(
                "configure_local._rename_exclusive", side_effect=substitute_before_rename
            ):
                (stdout, output) = self._run_main_in_process(target)

            self.assertEqual(stdout, "")
            self.assertIn("detection after the fact", output)
            self.assertNotIn("check the private parent and permissions", output)

        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "occupied"
            original_rename = configure_local._rename_exclusive

            def occupy_output_before_rename(parent_fd, staging_name, output_name):
                occupied = root / output_name
                occupied.mkdir(mode=0o700)
                (occupied / "foreign-marker").write_text("keep")
                return original_rename(parent_fd, staging_name, output_name)

            with mock.patch(
                "configure_local._rename_exclusive",
                side_effect=occupy_output_before_rename,
            ):
                (stdout, output) = self._run_main_in_process(target)

            # An established reason to preserve the private directory is part of
            # what the operator is told, not a sentence that is replaced on the
            # way out by a generic one.
            self.assertEqual(stdout, "")
            self.assertIn("touched outside this run", output)
            self.assertIn("manual cleanup", output)

    def test_unverifiable_post_removal_scan_is_reported(self):
        """A scan that cannot run is not a scan that found nothing (D6b).

        Rollback re-examines the parent after `os.rmdir` to say which directory
        was removed.  When the parent cannot be listed the scan has no evidence
        at all, and reporting that as "nothing found" would let the one case the
        re-examination exists for pass unreported.  An unconfirmed removal is
        reported as an unconfirmed removal.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            original_rmdir = configure_local.os.rmdir
            original_listdir = configure_local.os.listdir
            removed = []

            def note_removal(path, *, dir_fd=None):
                result = original_rmdir(path, dir_fd=dir_fd)
                removed.append(path)
                return result

            def deny_listing_after_removal(path):
                if removed and isinstance(path, int):
                    raise OSError("synthetic listing failure")
                return original_listdir(path)

            def fail_publication(parent_fd, staging_name, output_name):
                raise OSError("synthetic rename failure")

            with mock.patch(
                "configure_local._rename_exclusive", side_effect=fail_publication
            ):
                with mock.patch("configure_local.os.rmdir", side_effect=note_removal):
                    with mock.patch(
                        "configure_local.os.listdir",
                        side_effect=deny_listing_after_removal,
                    ):
                        with self.assertRaises(_CleanupIncomplete) as caught:
                            configure(target)

            self.assertEqual(len(removed), 1)
            self.assertIn("could not be confirmed", str(caught.exception))
            self.assertFalse(target.exists())
            self.assertEqual(list(root.glob(".voice.*.tmp")), [])

    def test_over_long_output_name_is_refused_before_anything_is_created(self):
        """An output name the staging name cannot be built from is refused (D6c).

        The staging name is derived from the output name, and the creator
        refuses before it opens the parent or creates anything when that derived
        name would not fit a filesystem name.  Something must make that decision
        with a reason: without it the same run reaches the filesystem and
        reports an unexplained OS failure instead.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / ("voice" + "x" * 240)

            with self.assertRaisesRegex(ValueError, "too long"):
                configure(target)

            self.assertFalse(target.exists())
            self.assertEqual(list(root.iterdir()), [])

    def test_junction_component_is_refused_without_creating_directories(self):
        """The junction branch is the Windows fail-closed path (D6c).

        A junction is what a reparse point presents to a path check, and this
        backend refuses one wherever `is_symlink` is checked; Darwin has no
        junctions, so the branch is exercised with the platform predicate it
        reads.  Removing the junction arm leaves a path this verifier cannot
        inspect on the platform that does have them, so the refusal is pinned
        even though it cannot occur natively here.
        """
        with tempfile.TemporaryDirectory() as temp:
            parent = Path(temp) / "parent"
            parent.mkdir(mode=0o700)
            target = parent / "voice"

            def is_junction(self):
                return self.name == parent.name

            with mock.patch.object(Path, "is_junction", is_junction, create=True):
                with self.assertRaisesRegex(ValueError, "must not contain symlinks"):
                    configure(target)
            self.assertFalse(target.exists())
            self.assertEqual(list(parent.iterdir()), [])

    def test_exhausted_staging_name_collisions_are_refused(self):
        """Every staging name colliding must stop the run, not loop (D6c).

        The staging name is unpredictable and the creation is exclusive, so a
        collision is retried with a new name; the retry is bounded, and the
        bound is what keeps a hostile or unlucky name space from turning the
        creation into an endless loop.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            attempts = []

            def collide(path, mode=0o777, *, dir_fd=None):
                attempts.append(path)
                raise FileExistsError(17, "synthetic collision", path)

            with mock.patch("configure_local.os.mkdir", side_effect=collide):
                with self.assertRaises(OSError) as caught:
                    configure(target)

            self.assertEqual(len(attempts), 16)
            self.assertIn("could not be created", str(caught.exception))
            self.assertFalse(target.exists())
            self.assertEqual(list(root.glob(".voice.*.tmp")), [])

    def test_acquired_entry_rewritten_in_place_is_refused_after_publication(self):
        """The gate->rename window with the acquired bytes changed in place (D3).

        Replacing an acquired name with a fresh file changes the inode and the
        identity re-derivation catches it.  Rewriting the file through the
        inode it already has does not: the `(st_dev, st_ino)` pair
        `_require_published_entry_set` compares is unchanged, so the run used
        to publish a directory whose `server-voice.json` is the attacker's
        while the `keys.yaml` beside it is this run's, and returned its success
        result.  APFS reuses inode numbers, so the same hole covers a name
        unlinked and recreated.  What is re-derived after the rename is
        therefore the bytes as well as the identity: the digest bound to each
        name when this run acquired it is compared with what the published
        directory holds.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            original_rename = configure_local._rename_exclusive
            identity_kept = []

            def rewrite_before_rename(parent_fd, staging_name, output_name):
                victim = root / staging_name / "server-voice.json"
                inode = os.stat(victim).st_ino
                with open(victim, "wb") as stream:
                    stream.write(b'{"ApiKey": "ATTACKERKEY"}')
                identity_kept.append(os.stat(victim).st_ino == inode)
                return original_rename(parent_fd, staging_name, output_name)

            with mock.patch(
                "configure_local._rename_exclusive", side_effect=rewrite_before_rename
            ):
                with self.assertRaises(OSError) as caught:
                    configure(target)

            # The substitution preserved the identity it was acquired under,
            # so an identity-only re-derivation would report success here.
            self.assertEqual(identity_kept, [True])
            self.assertIsInstance(
                caught.exception, configure_local._PublishedEntryNotOwned
            )
            message = str(caught.exception)
            self.assertIn("did not create", message)
            self.assertIn("detection after the fact", message)
            # The rewritten bytes are never reported as this run's private
            # configuration: the run refuses, and the exact-owned rollback
            # removes the names it acquired rather than handing them out.
            self.assertFalse(target.exists())
            self.assertEqual(list(root.glob(".voice.*.tmp")), [])

    def test_bytes_injected_into_an_acquired_entry_are_refused_after_publication(self):
        """The same window, injecting content instead of a name (D3).

        An entry this run never wrote can be added to the published directory
        without adding a name: appending to an acquired file keeps its identity
        and its place in the entry set, and every check that only counts names
        and inodes still passes.  The bytes bound at acquisition are what
        refuses it, and the directory is reported as detection after the fact.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            original_rename = configure_local._rename_exclusive

            def append_before_rename(parent_fd, staging_name, output_name):
                victim = root / staging_name / "livekit.yaml"
                with open(victim, "ab") as stream:
                    stream.write(b"rtc:\n  udp_port: 1\n")
                return original_rename(parent_fd, staging_name, output_name)

            with mock.patch(
                "configure_local._rename_exclusive", side_effect=append_before_rename
            ):
                with self.assertRaises(OSError) as caught:
                    configure(target)

            self.assertIsInstance(
                caught.exception, configure_local._PublishedEntryNotOwned
            )
            message = str(caught.exception)
            self.assertIn("did not create", message)
            self.assertIn("detection after the fact", message)
            self.assertFalse(target.exists())

    def test_refusal_reaches_the_cli_though_cleanup_failed_too(self):
        """A refusal is not replaced by the cleanup failure that followed it (D4).

        An acquired name that goes missing stops the run, and the rollback
        `rmdir` then fails because something this run never created is still in
        the directory.  The cleanup failure is a consequence of the refusal;
        letting it displace the refusal's sentence sent the operator the
        generic "private cleanup is incomplete" wording, which is the text an
        unexpected OS failure gets, and nothing said the boundary was why the
        run stopped.  The refusal keeps its own wording and the cleanup
        consequence is appended to it.
        """
        def lose_and_collide(real_write_private):
            written = []

            def helper(path, text, *, dir_fd=None, on_acquired=None):
                result = real_write_private(
                    path, text, dir_fd=dir_fd, on_acquired=on_acquired
                )
                written.append(Path(path).name)
                if len(written) == len(configure_local._GENERATED_NAMES):
                    os.unlink("keys.yaml", dir_fd=dir_fd)
                    foreign = os.open(
                        "foreign.yaml",
                        os.O_WRONLY | os.O_CREAT | os.O_EXCL,
                        0o600,
                        dir_fd=dir_fd,
                    )
                    os.close(foreign)
                return result

            return helper

        def no_publication(*args):
            self.fail("publication must not run")

        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "vanished"

            with mock.patch(
                "configure_local.write_private",
                side_effect=lose_and_collide(configure_local.write_private),
            ):
                with mock.patch(
                    "configure_local._rename_exclusive", side_effect=no_publication
                ):
                    (stdout, output) = self._run_main_in_process(target)

            self.assertEqual(stdout, "")
            self.assertIn("no longer holds every entry", output)
            self.assertIn("manual cleanup", output)
            self.assertNotIn("check the private parent and permissions", output)

        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "vanished-typed"

            with mock.patch(
                "configure_local.write_private",
                side_effect=lose_and_collide(configure_local.write_private),
            ):
                with mock.patch(
                    "configure_local._rename_exclusive", side_effect=no_publication
                ):
                    with self.assertRaises(_OutputPathChanged) as caught:
                        configure(target)

            # The reason the run stopped is reported as itself, not folded into
            # whatever the cleanup did: a lost acquired name is a finding of its
            # own, distinct from the cleanup that followed it.
            self.assertIsInstance(caught.exception, _StagingEntryVanished)
            self.assertIn("no longer holds every entry", str(caught.exception))
            self.assertIn("manual cleanup", str(caught.exception))

    def test_cleanup_failure_never_replaces_the_wording_the_run_established(self):
        """The run's own sentence survives a cleanup failure (D-6a, note 6).

        `_safe_creation_error` is the last place a failed run's wording can be
        replaced.  A cleanup that fails for its own reasons used to substitute
        the generic incomplete-cleanup sentence for both the refusal this run
        established and the original failure, so the operator read a sentence
        neither of them produced.  Both are reported, and the cleanup outcome
        is appended.
        """
        with self.assertRaises(_OutputPathChanged) as caught:
            configure_local._safe_creation_error(
                _StagingEntryVanished("VANISH-SENTENCE"), OSError("CLEANUP-OS-ERROR")
            )
        message = str(caught.exception)
        self.assertIn("VANISH-SENTENCE", message)
        self.assertIn("CLEANUP-OS-ERROR", message)
        self.assertNotIn("check the private parent and permissions", message)

        with self.assertRaises(_OutputPathChanged) as caught:
            configure_local._safe_creation_error(
                OSError("ORIGINAL-OS"), OSError("CLEANUP-OS-ERROR")
            )
        message = str(caught.exception)
        self.assertIn("ORIGINAL-OS", message)
        self.assertIn("CLEANUP-OS-ERROR", message)

        with self.assertRaises(_OutputPathChanged) as caught:
            configure_local._safe_creation_error(
                OSError("ORIGINAL-OS"), _CleanupIncomplete("FINDING-SENTENCE")
            )
        # The finding this run established about the directory is reported
        # verbatim - it is what the operator has to act on - and the original
        # failure is kept beside it.
        self.assertIsInstance(caught.exception, _CleanupIncomplete)
        message = str(caught.exception)
        self.assertIn("FINDING-SENTENCE", message)
        self.assertIn("ORIGINAL-OS", message)
        # Reported as the finding it is, not dressed up as an unexpected OS
        # failure of the cleanup.
        self.assertNotIn("cleanup could not complete", message)

    def test_created_file_that_does_not_hold_the_written_bytes_is_refused(self):
        """Acquisition verifies the bytes, not only the inode (D3).

        `write_private` records the digest of what it writes and then reads the
        file back through the name and the identity it acquired, so a file that
        does not hold the bytes this run wrote at the moment the run claims it
        stops the run instead of being carried into the credential set.  The
        identity check alone cannot see this: the extra bytes landed on the same
        inode.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            real_fdopen = configure_local.os.fdopen

            def fdopen_writing_extra(fd, *args, **kwargs):
                stream = real_fdopen(fd, *args, **kwargs)

                class Extra:
                    def write(self, text):
                        stream.write(text)
                        stream.write("attacker: yes\n")

                    def close(self):
                        stream.close()

                return Extra()

            with mock.patch(
                "configure_local.os.fdopen", side_effect=fdopen_writing_extra
            ):
                with self.assertRaises(_OutputPathChanged) as caught:
                    configure(target)

            # The refusal comes from the acquisition read-back, not from the
            # entry-set gate further on: the file was never fit to be part of
            # the credential set at all.
            self.assertIn(
                "Configuration file bytes changed during creation",
                str(caught.exception),
            )
            self.assertFalse(target.exists())
            self.assertEqual(list(root.glob(".voice.*.tmp")), [])

    def test_staging_directory_that_cannot_be_pinned_is_reported_for_cleanup(self):
        """A staged directory nobody can account for is reported, not forgotten.

        The staging directory is created by name and then opened, so the open
        can fail after the directory exists.  Nothing about that directory was
        ever proven, so nothing in it may be removed; it must not be dropped
        silently either, because it is the only place the credentials of this
        failed run could be.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "voice"
            real_open_directory = configure_local._open_directory

            def fail_staging_open(path, *, dir_fd=None):
                if dir_fd is not None:
                    raise OSError("synthetic staging open failure")
                return real_open_directory(path, dir_fd=dir_fd)

            with mock.patch(
                "configure_local._open_directory", side_effect=fail_staging_open
            ):
                with self.assertRaises(_OutputPathChanged) as caught:
                    configure(target)

            message = str(caught.exception)
            self.assertIn("could not be pinned", message)
            self.assertIn("manual cleanup", message)
            self.assertFalse(target.exists())
            residues = list(root.glob(".voice.*.tmp"))
            self.assertEqual(len(residues), 1)
            self.assertEqual(list(residues[0].iterdir()), [])

    def test_ownership_binding_is_refused_without_both_sides_to_bind(self):
        """Nothing is proven from a missing descriptor or entry (D6-c).

        `_prove_staging_ownership` is the only binding this run accepts, and
        what it binds are the descriptor it opened and the parent entry it
        created.  A call missing either side is not a weaker proof of the same
        boundary, it is no proof at all, so it is refused with `None` before
        any comparison runs - rather than raising, which a caller could mistake
        for the check having happened.  Dropping that precondition leaves the
        same call raising `TypeError` instead of returning evidence.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            parent_fd = os.open(root, os.O_RDONLY)
            directory_fd = os.open(root, os.O_RDONLY)
            try:
                self.assertIsNone(
                    configure_local._prove_staging_ownership(
                        None, "staging", directory_fd
                    )
                )
                self.assertIsNone(
                    configure_local._prove_staging_ownership(parent_fd, "staging", None)
                )
                self.assertIsNone(
                    configure_local._prove_staging_ownership(parent_fd, "", directory_fd)
                )
            finally:
                os.close(directory_fd)
                os.close(parent_fd)

    def test_a_name_is_not_removable_without_proven_ownership(self):
        """Cleanup removes only what ownership was proven for (D6-c / D7).

        The rollback `rmdir` is the one destructive act after the ownership
        proof, so the name it targets must still resolve to the inode this run
        proved.  A run that never proved ownership has no such inode: a name
        that happens to exist under the parent is not thereby this run's to
        remove, so the target is refused rather than accepted on the strength
        of the name alone.
        """
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            staging = root / ".voice.cafe.tmp"
            staging.mkdir(mode=0o700)
            parent_fd = os.open(root, os.O_RDONLY)
            directory_fd = os.open(staging, os.O_RDONLY)
            try:
                self.assertFalse(
                    configure_local._owned_cleanup_target(
                        None, parent_fd, staging.name, directory_fd
                    )
                )
                self.assertFalse(
                    configure_local._owned_cleanup_target(None, parent_fd, "", directory_fd)
                )
            finally:
                os.close(directory_fd)
                os.close(parent_fd)

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
