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
                with self.assertRaises(ValueError):
                    configure(target)
            self.assertEqual((target / "keys.yaml").read_text(), "synthetic foreign sentinel")
            # The foreign output is preserved untouched, while the staging
            # directory is removed only because it is still the exact inode this
            # invocation proved it created.  Cleanup is therefore complete, not
            # deferred to manual removal.
            self.assertEqual(len(list(root.glob(".voice.*.tmp"))), 0)

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
                with self.assertRaisesRegex(ValueError, "synthetic file ACL"):
                    configure(target)
            self.assertGreaterEqual(regular_file_checks, 1)
            self.assertFalse(target.exists())
            # Exact-owned rollback removes every generated file and the staging
            # directory it proved it created; no residue is left behind.
            self.assertEqual(len(list(Path(temp).glob(".voice.*.tmp"))), 0)

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
