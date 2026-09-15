#!/usr/bin/env python3
"""Create private local LiveKit settings. Does not start or buy a server."""
import argparse
import ctypes
import errno
import hashlib
import ipaddress
import json
import os
import secrets
import stat
import sys
from pathlib import Path


_TRUSTED_SYSTEM_SYMLINKS = {Path("/var"), Path("/tmp")}
_GENERATED_NAMES = ("keys.yaml", "livekit.yaml", "server-voice.json")
_ACL_TYPE_EXTENDED = 0x00000100
_ACL_FIRST_ENTRY = 0
_RENAME_EXCL = 0x00000004
# Every file this run writes is a small text configuration.  A staged entry
# longer than this is not one of them, so it is reported as a mismatch rather
# than read into memory while the boundary is being re-derived.
_STAGED_BYTES_LIMIT = 1 << 16


class _OutputPathChanged(OSError):
    """The output path no longer names the directory opened for this run."""


class _CleanupIncomplete(_OutputPathChanged):
    """Private output could not be completely removed after a failed run."""


class _StagingOwnershipUnproven(_OutputPathChanged):
    """The staging directory was not proven to belong to this invocation.

    Raised before the first credential byte is written and again before
    publication.  It is a refusal, not a failure: the caller stops instead of
    writing credentials into, or publishing, a directory it cannot vouch for.
    """


class _StagingEntryVanished(_OutputPathChanged):
    """An entry this run acquired no longer exists in the pinned directory.

    Publication renames the whole directory, so a name this run acquired and
    then lost would be handed out as this run's private configuration while
    silently lacking a file it was supposed to hold.  It is a refusal, and it
    is reported apart from an injected or replaced entry: the directory is
    missing something this run created rather than holding something it did
    not.
    """


class _StagingEntrySetUnproven(_OutputPathChanged):
    """The pinned staging directory could not be enumerated.

    Publication renames the whole directory, so the entry set is part of what
    the run must account for.  A directory that cannot be listed at all leaves
    that set unknown: the run refuses and preserves the directory for manual
    cleanup instead of removing it on the strength of the inode alone.
    """


class _PublishedEntrySetUnproven(_OutputPathChanged):
    """The published directory could not be re-enumerated after the rename.

    The re-derivation that follows publication needs the entry set of the
    directory the rename moved, and a directory that cannot be listed leaves it
    unknown.  It is reported apart from a demonstrated mismatch: this is an
    absence of evidence about what was published, not evidence of an entry this
    run did not write.
    """


class _PublishedEntryVanished(_OutputPathChanged):
    """A name this run acquired is missing from the published directory.

    The rename has already moved the directory, so a name lost in the window
    the rename cannot cover is reported as what the output is missing rather
    than folded into the finding about entries the output did not create.
    """


class _PublishedEntryNotOwned(_OutputPathChanged):
    """The published directory holds bytes this run did not write.

    Re-derived after the rename: an entry that was never acquired, or an
    acquired name whose identity changed, means the directory now carrying this
    run's output name is not the configuration this run staged.  It is a
    refusal - the run does not return its success result and does not hand the
    directory out - and it is detection after the fact, because the rename that
    published it has already happened.  The rollback that follows is the same
    exact-owned one as everywhere else: the names this run acquired are
    unlinked, and the directory itself is removed only while the cleanup name
    still resolves to the inode this run proved and now holds nothing else.
    A substituted or injected name is not this run's to remove, so it survives
    and the directory stays for manual cleanup.
    """


class _CleanupTargetMismatch(_CleanupIncomplete):
    """The directory removed during rollback was not proven to be this run's.

    ``_owned_cleanup_target`` proves that the cleanup name resolves to the
    staging inode, and ``os.rmdir`` then removes whatever that name addresses
    at a later instant.  Darwin keeps an open descriptor's ``st_nlink`` at 2
    after ``rmdir``, so the removal cannot be confirmed from the descriptor;
    it is re-derived by re-enumerating the parent after the fact, and this is
    that finding.  Detection, not prevention: the removal has already happened
    when it is raised, so the report is evidence about which directory was
    removed rather than a refusal to remove one.
    """


class _GeneratedOutputNotPrivate(ValueError):
    """A file this run created failed its private-permission verification.

    ``write_private`` creates the file ``0600`` and current-user-owned, then
    re-verifies those properties through the descriptor it still holds.  A
    failure here means something outside this run changed the file after
    creation, or the privacy backend cannot vouch for it.  The run refuses and
    does not delete the staging directory it can no longer vouch for.
    """


class _AcquiredEntry:
    """One name this run acquired: the identity it had and the bytes it held.

    ``identity`` is the ``(st_dev, st_ino)`` pair observed on the descriptor at
    open time; ``digest`` is the SHA-256 of the bytes this run writes into it.
    Identity alone does not show that a name still holds what this run put
    there: a file rewritten through the descriptor it already has keeps its
    inode, and on a filesystem that reuses inode numbers so does a name
    unlinked and recreated.  Binding the digest is what makes "this is still
    the file this run staged" answerable from the directory when the run
    re-derives it, before publication and again after it.
    """

    __slots__ = ("identity", "digest")

    def __init__(self, identity, digest):
        self.identity = identity
        self.digest = digest


def _digest_of_fd(fd):
    """SHA-256 of a descriptor's bytes, or ``None`` past the bound above."""
    payload = b""
    while True:
        chunk = os.read(fd, 4096)
        if not chunk:
            return hashlib.sha256(payload).hexdigest()
        payload += chunk
        if len(payload) > _STAGED_BYTES_LIMIT:
            return None


def _entry_digest(name, dir_fd):
    """Digest of one staged entry's bytes, or ``None`` when it cannot be read."""
    try:
        entry_fd = os.open(
            name, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0), dir_fd=dir_fd
        )
    except OSError:
        return None
    try:
        return _digest_of_fd(entry_fd)
    except OSError:
        return None
    finally:
        os.close(entry_fd)


def _open_flags():
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    return flags


def write_private(path, text, *, dir_fd=None, on_acquired=None):
    """Write one private file through the explicit output-directory descriptor."""
    path = Path(path)
    name = path.name if dir_fd is not None else path
    kwargs = {"dir_fd": dir_fd} if dir_fd is not None else {}
    payload = hashlib.sha256(text.encode("utf-8")).hexdigest()
    fd = os.open(name, _open_flags(), 0o600, **kwargs)
    try:
        # Raw ownership starts at os.open, not after identity inspection.  This
        # closes the descriptor when fstat, fchmod, or fdopen itself fails.
        # What is recorded is what this run is about to write, not what a later
        # re-derivation reads back, so that "the bytes changed" is a finding
        # about the directory rather than a tautology.
        acquired = _AcquiredEntry(_file_identity(os.fstat(fd)), payload)
        if on_acquired is not None:
            on_acquired(path.name, acquired)
        os.fchmod(fd, 0o600)
        stream = os.fdopen(fd, "w", encoding="utf-8")
        fd = None
        try:
            stream.write(text)
        finally:
            stream.close()
    finally:
        if fd is not None:
            os.close(fd)
    _verify_private_file(path, dir_fd=dir_fd, expected=acquired)


def _reject_symlink_components(path):
    try:
        absolute = Path(path).absolute()
        current = Path(absolute.anchor)
        for component in absolute.parts[1:]:
            current /= component
            is_junction = getattr(current, "is_junction", lambda: False)()
            if (current.is_symlink() or is_junction) and current not in _TRUSTED_SYSTEM_SYMLINKS:
                raise ValueError("Configuration path must not contain symlinks")
    except OSError as exc:
        raise ValueError("Configuration path could not be inspected safely") from exc


def _require_privacy_backend():
    if sys.platform == "darwin" and os.name == "posix":
        return
    if os.name == "nt":
        raise ValueError(
            "Windows private configuration generation is not supported by this verifier"
        )
    raise ValueError(
        "Private configuration generation is not supported on this platform"
    )


def _validate_parent(parent):
    try:
        if not parent.exists() or not parent.is_dir():
            raise ValueError("Configuration parent directory must already exist")
        _reject_symlink_components(parent)
        stat_result = parent.stat()
    except OSError as exc:
        raise ValueError("Configuration parent directory could not be inspected safely") from exc
    if stat_result.st_uid != os.getuid():
        raise ValueError("Configuration parent directory must be owned by the current user")
    if stat_result.st_mode & 0o022:
        raise ValueError("Configuration parent directory must not be group- or world-writable")


def _validate_output(output):
    _reject_symlink_components(output)
    try:
        exists = output.exists()
    except OSError as exc:
        raise ValueError("Configuration output path could not be inspected safely") from exc
    if exists:
        raise ValueError("Configuration already exists; do not rotate active credentials implicitly")
    _validate_parent(output.parent)


def _validate_node_ip(node_ip):
    if not isinstance(node_ip, str) or any(
        ord(char) < 0x20 or ord(char) == 0x7F for char in node_ip
    ):
        raise ValueError("node_ip must be a valid loopback address")
    if "%" in node_ip:
        raise ValueError("node_ip must be an unscoped loopback address")
    try:
        address = ipaddress.ip_address(node_ip)
    except ValueError as exc:
        raise ValueError("node_ip must be a valid loopback address") from exc
    if not address.is_loopback or address.is_unspecified:
        raise ValueError("node_ip must be loopback-only")
    return address


def _directory_open_flags():
    return os.O_RDONLY | getattr(os, "O_DIRECTORY", 0) | getattr(os, "O_NOFOLLOW", 0)


def _open_directory(path, *, dir_fd=None):
    kwargs = {"dir_fd": dir_fd} if dir_fd is not None else {}
    return os.open(path, _directory_open_flags(), **kwargs)


def _darwin_acl_api():
    """Bind the small descriptor ACL ABI exposed by Darwin's system libc."""
    try:
        libc = ctypes.CDLL(None, use_errno=True)
        libc.acl_get_fd_np.argtypes = [ctypes.c_int, ctypes.c_int]
        libc.acl_get_fd_np.restype = ctypes.c_void_p
        libc.acl_get_entry.argtypes = [
            ctypes.c_void_p,
            ctypes.c_int,
            ctypes.POINTER(ctypes.c_void_p),
        ]
        libc.acl_get_entry.restype = ctypes.c_int
        libc.acl_free.argtypes = [ctypes.c_void_p]
        libc.acl_free.restype = ctypes.c_int
        return libc
    except (AttributeError, OSError) as exc:
        raise ValueError("Configuration ACL privacy backend is unavailable") from exc


def _verify_darwin_no_additional_acl(fd):
    """Reject any extended ACL attached to the inode referenced by ``fd``."""
    libc = _darwin_acl_api()
    ctypes.set_errno(0)
    acl = libc.acl_get_fd_np(fd, _ACL_TYPE_EXTENDED)
    if not acl:
        if ctypes.get_errno() == errno.ENOENT:
            return
        raise ValueError("Configuration ACL privacy could not be verified")
    try:
        entry = ctypes.c_void_p()
        ctypes.set_errno(0)
        result = libc.acl_get_entry(acl, _ACL_FIRST_ENTRY, ctypes.byref(entry))
        if result == 0:
            raise ValueError("Configuration must not have additional ACL entries")
        if ctypes.get_errno() != errno.EINVAL:
            raise ValueError("Configuration ACL privacy could not be verified")
    finally:
        if libc.acl_free(acl) != 0:
            raise ValueError("Configuration ACL privacy could not be verified")


def _verify_private_directory(*, dir_fd):
    stat_result = os.fstat(dir_fd)
    if not stat.S_ISDIR(stat_result.st_mode):
        raise ValueError("Configuration output is not a directory")
    if stat_result.st_mode & 0o777 != 0o700:
        raise ValueError("Configuration directory privacy could not be verified")
    if stat_result.st_uid != os.getuid():
        raise ValueError("Configuration directory owner could not be verified")
    _verify_darwin_no_additional_acl(dir_fd)


def _verify_private_file(path, *, dir_fd=None, expected=None):
    file_fd = None
    try:
        if dir_fd is not None:
            flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
            file_fd = os.open(Path(path).name, flags, dir_fd=dir_fd)
            stat_result = os.fstat(file_fd)
        else:
            file_fd = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
            stat_result = os.fstat(file_fd)
        if expected is not None and _file_identity(stat_result) != expected.identity:
            raise _OutputPathChanged("Configuration file path changed during creation")
        if not stat.S_ISREG(stat_result.st_mode):
            raise _GeneratedOutputNotPrivate("Configuration file is not a regular file")
        if stat_result.st_mode & 0o777 != 0o600:
            raise _GeneratedOutputNotPrivate(
                "Configuration file privacy could not be verified"
            )
        if stat_result.st_uid != os.getuid():
            raise _GeneratedOutputNotPrivate(
                "Configuration file owner could not be verified"
            )
        try:
            _verify_darwin_no_additional_acl(file_fd)
        except ValueError as exc:
            raise _GeneratedOutputNotPrivate(str(exc)) from exc
        if expected is not None and _digest_of_fd(file_fd) != expected.digest:
            # The identity above says this is the inode this run created; the
            # digest says whether it still holds the bytes this run wrote into
            # it.  A file rewritten through that inode keeps the identity, so
            # this is the only check that sees it.
            raise _OutputPathChanged("Configuration file bytes changed during creation")
    finally:
        if file_fd is not None:
            os.close(file_fd)


def _file_identity(stat_result):
    return stat_result.st_dev, stat_result.st_ino


def _directory_identity(stat_result):
    return _file_identity(stat_result)


def _same_output_directory(parent_fd, output_name, directory_stat):
    try:
        current = os.stat(output_name, dir_fd=parent_fd, follow_symlinks=False)
    except OSError:
        return False
    return stat.S_ISDIR(current.st_mode) and _directory_identity(current) == _directory_identity(
        directory_stat
    )


def _same_parent_path(output_parent, parent_stat):
    try:
        current = output_parent.stat()
    except OSError:
        return False
    return stat.S_ISDIR(current.st_mode) and _directory_identity(current) == _directory_identity(
        parent_stat
    )


class _StagingOwnership:
    """Evidence tying an opened staging descriptor to a parent directory entry.

    ``identity`` is the ``(st_dev, st_ino)`` pair observed on the descriptor and
    re-observed on the parent entry at the same instant.  Every later use must
    re-observe the same pair on both sides before a credential is written or the
    directory is published, so a swap that happens *after* the proof is caught.

    Scope: the create->open window itself is not authenticated by this evidence.
    What keeps another principal out of that window is the precondition below,
    not this object.
    """

    __slots__ = ("identity",)

    def __init__(self, identity):
        self.identity = identity


def _prove_staging_ownership(parent_fd, staging_name, directory_fd):
    """Bind ``directory_fd`` to the entry ``staging_name`` under ``parent_fd``.

    Returns ``_StagingOwnership`` evidence, or ``None`` when the binding does
    not hold.  The caller must treat ``None`` as a refusal and stop before
    writing credentials.

    Precondition, enforced by the caller before the staging directory is
    created: the parent is private (mode 0700, owned by the current user, no
    additional ACL).  That precondition is what excludes other principals from
    the create->open window; this function detects a swap after it, which is the
    only window a same-uid actor could otherwise exploit undetected.
    """
    if parent_fd is None or directory_fd is None or not staging_name:
        return None
    try:
        descriptor_stat = os.fstat(directory_fd)
        entry_stat = os.stat(staging_name, dir_fd=parent_fd, follow_symlinks=False)
    except OSError:
        return None
    if not stat.S_ISDIR(descriptor_stat.st_mode) or not stat.S_ISDIR(entry_stat.st_mode):
        return None
    identity = _directory_identity(descriptor_stat)
    if identity != _directory_identity(entry_stat):
        return None
    return _StagingOwnership(identity)


def _require_owned_staging(ownership, parent_fd, staging_name, directory_fd, *, action):
    """Stop unless the staging directory is still the inode proven earlier."""
    current = _prove_staging_ownership(parent_fd, staging_name, directory_fd)
    if ownership is None or current is None or current.identity != ownership.identity:
        raise _StagingOwnershipUnproven(
            "Configuration staging ownership could not be proven; refusing to " + action
        )
    return ownership


def _owned_cleanup_target(ownership, parent_fd, cleanup_name, directory_fd):
    """Report whether ``cleanup_name`` still names the proven staging inode.

    Only an exact-owned target may be removed, so this is deliberately narrower
    than "the name resolves to some directory".
    """
    if ownership is None or directory_fd is None or not cleanup_name:
        return False
    current = _prove_staging_ownership(parent_fd, cleanup_name, directory_fd)
    return current is not None and current.identity == ownership.identity


def _proven_inode_still_present(parent_fd, ownership):
    """Report whether the proven staging inode is still reachable by any name.

    Called after the cleanup ``rmdir``.  Darwin keeps ``st_nlink`` at 2 on a
    descriptor whose name was removed, so nothing about the descriptor says
    what the removed name addressed; re-enumerating the parent afterwards is
    the only evidence available.  Finding the proven identity under some name
    means that ``rmdir`` removed something else and the staging directory this
    run created was left behind.

    Returns ``True`` when the proven identity is still reachable, ``False``
    when the parent was listed and it is gone, and ``None`` when the scan
    could not run at all.  ``None`` is deliberately not ``False``: no evidence
    is not the evidence that the removal was exact, and the caller reports the
    unconfirmed removal instead of passing it off as a clean one.
    """
    if ownership is None or parent_fd is None:
        return None
    try:
        names = os.listdir(parent_fd)
    except OSError:
        return None
    for name in names:
        try:
            current = os.stat(name, dir_fd=parent_fd, follow_symlinks=False)
        except OSError:
            continue
        if (
            stat.S_ISDIR(current.st_mode)
            and _directory_identity(current) == ownership.identity
        ):
            return True
    return False


def _preserved_directory_report(error, parent_fd, output_name, published):
    """Why the private directory must be preserved, or ``None`` to remove it.

    Directory removal is exact-owned: it is decided by inode evidence alone.
    Three failures are the exception, because in each the private output this
    run staged is demonstrably no longer only its own:

    * ``_GeneratedOutputNotPrivate`` - a file this run created ``0600`` and
      current-user-owned no longer holds that privacy.
    * ``_StagingEntrySetUnproven`` - the pinned directory could not be listed,
      so the entry set publication would rename is unknown.
    * A name that occupies the publication target although this run never
      published: ``_validate_output`` proved the output name absent before
      staging began, so that name was created while this run held staged
      credentials.

    In all three cases the run stops, unlinks only the file identities it
    acquired, and preserves the directory for manual reconciliation instead of
    deleting the last artifact it can still account for.

    The finding is returned as the operator-facing sentence, because the three
    cases are different facts: reporting the third one's wording for all of
    them would tell the operator the boundary was touched outside this run when
    what actually happened is that a created file lost its privacy or that the
    staged directory could not be enumerated.
    """
    if isinstance(error, _GeneratedOutputNotPrivate):
        return (
            "A file this run created no longer holds its private permissions; "
            "manual cleanup required"
        )
    if isinstance(error, _StagingEntrySetUnproven):
        return (
            "The staged directory could not be enumerated, so the entry set "
            "publication would have moved is unknown; manual cleanup required"
        )
    if published or not output_name or parent_fd is None:
        return None
    try:
        os.stat(output_name, dir_fd=parent_fd, follow_symlinks=False)
    except OSError:
        return None
    return (
        "The private output boundary was touched outside this run; "
        "manual cleanup required"
    )


def _unaccounted_staging_entries(directory_fd, owned_files):
    """What the pinned directory holds that this run cannot publish as its own.

    Publication renames the whole directory, so the entry set is part of what
    the run must account for.  Two findings are returned apart, because they
    are different facts about the same gate:

    * ``vanished`` - a name this run acquired no longer exists at all, so the
      renamed directory would be handed out as this run's private
      configuration while silently lacking a file it was supposed to hold.
    * ``unowned`` - a name that was never acquired, or an acquired name that no
      longer resolves to the identity it was acquired under or to the bytes
      written then, so the renamed directory would hand out bytes this run did
      not write.

    An acquired name is compared on both counts: the identity recorded at
    acquisition and the digest of the bytes this run wrote.  Identity alone
    leaves a window open, because a file rewritten through the inode it already
    has - and, where inode numbers are reused, a name unlinked and recreated -
    keeps the ``(st_dev, st_ino)`` pair and its place in the entry set while
    holding entirely different contents.

    ``vanished`` is derived first, so a lost entry reports as a loss instead of
    being folded into the broader finding.  Returns ``None`` when the directory
    cannot be listed at all, which is treated as unproven.
    """
    owned = dict(owned_files)
    try:
        present = os.listdir(directory_fd)
    except OSError:
        return None
    vanished = [name for name in owned if name not in present]
    unowned = []
    for name in present:
        evidence = owned.get(name)
        if evidence is None:
            unowned.append(name)
            continue
        try:
            current = os.stat(name, dir_fd=directory_fd, follow_symlinks=False)
        except OSError:
            unowned.append(name)
            continue
        if not stat.S_ISREG(current.st_mode) or _file_identity(current) != evidence.identity:
            unowned.append(name)
            continue
        if _entry_digest(name, directory_fd) != evidence.digest:
            unowned.append(name)
    return vanished, unowned


def _require_published_entry_set(directory_fd, owned_files):
    """Re-derive the published directory's contents after the rename.

    Publication renames the directory and the checks that follow it - the
    parent path and the output inode - name the directory rather than what it
    holds, so a swap that leaves the directory identity in place and changes
    its contents is invisible to them.  The entry set and every file identity
    are therefore re-derived from the pinned descriptor after the rename and
    before this run reports success.

    Detection after the fact, not prevention: the rename has already happened
    when this runs, so a mismatch is refused rather than blocked, and the
    published directory is preserved for manual cleanup instead of being
    repaired.  The three findings are kept apart exactly as the pre-rename gate
    keeps them: an entry set that cannot be established, a name this run
    acquired that is gone, and bytes this run did not write.
    """
    entry_set = _unaccounted_staging_entries(directory_fd, owned_files)
    if entry_set is None:
        raise _PublishedEntrySetUnproven(
            "The published directory could not be re-enumerated; whether it "
            "still holds only this run's configuration is unproven, and the "
            "rename had already published it, so this is detection after the "
            "fact, not prevention"
        )
    vanished, unowned = entry_set
    if vanished:
        raise _PublishedEntryVanished(
            "The published directory no longer holds every entry this run "
            "acquired; the rename had already published it, so this is "
            "detection after the fact, not prevention"
        )
    if unowned:
        raise _PublishedEntryNotOwned(
            "The published directory holds entries this run did not create, or "
            "an acquired name whose bytes changed; the rename had already "
            "published it, so this is detection after the fact, not prevention"
        )


def _rename_exclusive(parent_fd, staging_name, output_name):
    """Publish a completed private directory without replacing an existing name."""
    libc = ctypes.CDLL(None, use_errno=True)
    try:
        renameatx_np = libc.renameatx_np
    except AttributeError as exc:
        raise ValueError("Exclusive configuration publication is unavailable") from exc
    renameatx_np.argtypes = [
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_uint,
    ]
    renameatx_np.restype = ctypes.c_int
    if renameatx_np(
        parent_fd,
        os.fsencode(staging_name),
        parent_fd,
        os.fsencode(output_name),
        _RENAME_EXCL,
    ) != 0:
        error = ctypes.get_errno()
        if error in (errno.EEXIST, errno.ENOTEMPTY):
            raise ValueError(
                "Configuration already exists; do not rotate active credentials implicitly"
            )
        raise OSError(error, "Exclusive configuration publication failed")


def _cleanup_owned_files(directory_fd, owned_files):
    """Unlink only names still bound to file identities acquired by this call."""
    cleanup_error = None
    for name, evidence in reversed(owned_files):
        try:
            current = os.stat(name, dir_fd=directory_fd, follow_symlinks=False)
            if _file_identity(current) != evidence.identity:
                cleanup_error = cleanup_error or _CleanupIncomplete(
                    "A file this run created was replaced before cleanup and "
                    "was preserved; manual cleanup required"
                )
                continue
            os.unlink(name, dir_fd=directory_fd)
        except FileNotFoundError:
            pass
        except OSError as exc:
            cleanup_error = cleanup_error or exc
    return cleanup_error


def _cleanup_detail(cleanup_error):
    """The cleanup consequence to report alongside whatever stopped the run.

    A finding this run established is reported verbatim, so the operator reads
    what actually happened to the directory - preserved, replaced, or removed
    without confirmation - instead of an assumption.  A cleanup that failed
    for its own reasons is reported too: the directory is left in place either
    way, and saying so is not a substitution for the sentence the run already
    established, which is never replaced by this one.
    """
    if isinstance(cleanup_error, _CleanupIncomplete):
        return str(cleanup_error)
    return (
        f"cleanup could not complete ({cleanup_error}); the private directory "
        "was left in place for manual cleanup"
    )


# A run-established reason for stopping keeps its own operator-facing sentence
# even when the cleanup that followed it also failed.  Each of these is a
# different fact - a directory not proven to be this run's, a name lost, an
# entry set unknown, a substitution found too late - so replacing one with a
# cleanup failure tells the operator something that did not happen.
_PRESERVED_REFUSALS = (
    _StagingOwnershipUnproven,
    _StagingEntryVanished,
    _StagingEntrySetUnproven,
    _PublishedEntrySetUnproven,
    _PublishedEntryVanished,
    _PublishedEntryNotOwned,
)


def _safe_creation_error(original_error, cleanup_error):
    if cleanup_error is None:
        raise original_error
    if isinstance(original_error, _PRESERVED_REFUSALS):
        # The refusal is the actionable report and the cleanup failure is a
        # consequence of it, so the consequence is appended rather than
        # substituted for the reason the run stopped.
        raise type(original_error)(
            f"{original_error}. {_cleanup_detail(cleanup_error)}"
        ) from cleanup_error
    if isinstance(cleanup_error, _CleanupIncomplete):
        # This run established why it stopped and what it preserved - the
        # post-removal scan, a replaced acquired file, a boundary touched
        # outside the run - so its own wording is the operator-facing report.
        # The original failure is kept alongside it rather than dropped.
        raise type(cleanup_error)(
            f"{original_error}; {_cleanup_detail(cleanup_error)}"
        ) from original_error
    # Anything else - a path or entry-set refusal without a cleanup finding of
    # its own, or an unexpected OS failure the cleanup also could not finish -
    # is reported with the run's own sentence kept and the cleanup consequence
    # appended, under the reason that is now the operator's next problem: the
    # cleanup did not complete.
    raise _CleanupIncomplete(
        f"{original_error}; {_cleanup_detail(cleanup_error)}"
    ) from original_error


def configure(output, node_ip="127.0.0.1"):
    """Create a local-only LiveKit configuration without exposing credentials."""
    output = Path(output).absolute()
    address = _validate_node_ip(node_ip)
    _require_privacy_backend()
    # Classify impossible staging names before filesystem inspection can turn
    # ENAMETOOLONG into a generic path refusal. Keep privacy checks unchanged.
    staging_prefix = f".{output.name}."
    if len(os.fsencode(staging_prefix)) + 24 + len(b".tmp") > 255:
        raise ValueError("Configuration output name is too long")
    _validate_output(output)

    parent_fd = None
    directory_fd = None
    parent_stat = None
    directory_stat = None
    staging_name = None
    owned_files = []
    published = False
    # Darwin offers no mkdir variant that returns a descriptor, so creation
    # ownership is established after the fact: the descriptor and the parent
    # entry must name the same inode, before any credential byte is written and
    # again before publication.  Rollback removes a directory only while that
    # same evidence still holds, so an unproven or replaced inode is never
    # rmdir'd.  See _prove_staging_ownership for what this does and does not
    # authenticate.
    ownership = None
    try:
        parent_fd = _open_directory(output.parent)
        parent_stat = os.fstat(parent_fd)
        _verify_private_directory(dir_fd=parent_fd)
        if not _same_parent_path(output.parent, parent_stat):
            raise _OutputPathChanged("Configuration parent path changed during creation")

        for _ in range(16):
            staging_name = f".{output.name}.{secrets.token_hex(12)}.tmp"
            try:
                os.mkdir(staging_name, mode=0o700, dir_fd=parent_fd)
                break
            except FileExistsError:
                staging_name = None
        if staging_name is None:
            raise OSError("Private staging directory could not be created")

        directory_fd = _open_directory(staging_name, dir_fd=parent_fd)
        directory_stat = os.fstat(directory_fd)
        if not _same_output_directory(parent_fd, staging_name, directory_stat):
            raise _OutputPathChanged("Configuration staging path changed during creation")
        _verify_private_directory(dir_fd=directory_fd)

        # P0 boundary: credentials are not written until the staging directory
        # is proven to be the one this call created under the private parent.
        ownership = _prove_staging_ownership(parent_fd, staging_name, directory_fd)
        if ownership is None:
            raise _StagingOwnershipUnproven(
                "Configuration staging ownership could not be proven; "
                "refusing to write credentials"
            )

        acquire = lambda name, entry: owned_files.append((name, entry))
        key = "cg" + secrets.token_hex(8)
        secret = secrets.token_urlsafe(40)
        write_private(
            output / "keys.yaml",
            key + ": " + secret + "\n",
            dir_fd=directory_fd,
            on_acquired=acquire,
        )
        write_private(
            output / "livekit.yaml",
            f'''port: 17880
bind_addresses: [0.0.0.0]
rtc:
  tcp_port: 17881
  udp_port: 17882
  use_external_ip: false
  node_ip: {json.dumps(str(address))}
room:
  auto_create: false
  max_participants: 20
  enable_remote_unmute: false
key_file: /run/chooguard/keys.yaml
''',
            dir_fd=directory_fd,
            on_acquired=acquire,
        )
        write_private(
            output / "server-voice.json",
            json.dumps(
                {
                    "HttpUrl": "http://127.0.0.1:17880",
                    "WebSocketUrl": "ws://127.0.0.1:17880",
                    "ApiKey": key,
                    "ApiSecret": secret,
                },
                indent=2,
            )
            + "\n",
            dir_fd=directory_fd,
            on_acquired=acquire,
        )
        if not _same_parent_path(output.parent, parent_stat):
            raise _OutputPathChanged("Configuration parent path changed during creation")
        # The trusted-parent precondition must still hold at the publication
        # boundary, and the staging inode must still be the one proven above.
        # Ownership is checked before the looser path check so a swap surfaces
        # as a specific refusal rather than a generic path change.
        _verify_private_directory(dir_fd=parent_fd)
        _require_owned_staging(
            ownership,
            parent_fd,
            staging_name,
            directory_fd,
            action="publish the configuration",
        )
        if not _same_output_directory(parent_fd, staging_name, directory_stat):
            raise _OutputPathChanged("Configuration staging path changed during creation")
        # The rename below moves the whole directory, so publication also
        # requires it to hold exactly the entries this run acquired.  An entry
        # injected after the last write, an acquired name swapped for other
        # bytes, and an acquired name that has since disappeared all contradict
        # that; the last of them would publish a private directory silently
        # missing one of its files, so it is refused as a loss of its own.
        entry_set = _unaccounted_staging_entries(directory_fd, owned_files)
        if entry_set is None:
            raise _StagingEntrySetUnproven(
                "Configuration staging directory could not be enumerated; "
                "refusing to publish it"
            )
        vanished, unowned = entry_set
        if vanished:
            raise _StagingEntryVanished(
                "Configuration staging directory no longer holds every entry "
                "this run acquired; refusing to publish it"
            )
        if unowned:
            raise _OutputPathChanged(
                "Configuration staging directory holds entries this run did not "
                "create, or an acquired name whose bytes changed; refusing to "
                "publish them"
            )

        _rename_exclusive(parent_fd, staging_name, output.name)
        published = True
        if not _same_parent_path(output.parent, parent_stat):
            raise _OutputPathChanged("Configuration parent path changed during publication")
        if not _same_output_directory(parent_fd, output.name, directory_stat):
            raise _OutputPathChanged("Configuration output path changed during publication")
        # The checks above name the directory; this one re-derives what it
        # holds.  A swap between the entry-set gate and the rename leaves the
        # directory identity in place and changes its contents, so only this
        # re-derivation sees it - after the rename, before success is reported.
        _require_published_entry_set(directory_fd, owned_files)
    except BaseException as exc:
        cleanup_error = None
        if directory_fd is not None:
            cleanup_error = _cleanup_owned_files(directory_fd, owned_files)
            cleanup_name = output.name if published else staging_name
            if cleanup_error is None:
                preserved_report = _preserved_directory_report(
                    exc, parent_fd, output.name, published
                )
                if preserved_report is not None:
                    cleanup_error = _CleanupIncomplete(preserved_report)
            # Exact-owned rollback: the directory is removed only while it is
            # still the inode this call proved it created and this run can still
            # account for everything it staged.  A replaced, unproven or
            # externally touched directory is preserved for manual cleanup
            # instead.
            can_remove_directory = _owned_cleanup_target(
                ownership, parent_fd, cleanup_name, directory_fd
            )
            if cleanup_error is None and can_remove_directory:
                try:
                    os.rmdir(cleanup_name, dir_fd=parent_fd)
                except OSError as cleanup_exc:
                    cleanup_error = cleanup_exc
                else:
                    # The exact-owned proof above and this ``rmdir`` are
                    # separate steps, so a same-account actor can still swap
                    # the name between them.  Nothing about the descriptor says
                    # what was removed, so the removal is re-examined
                    # afterwards: if the proven inode is still reachable under
                    # some name, this run removed a directory it did not create
                    # and left its own behind.  That is detection after the
                    # fact - the swap has already happened by the time it is
                    # reported - never prevention of it.
                    removal_evidence = _proven_inode_still_present(parent_fd, ownership)
                    if removal_evidence is True:
                        cleanup_error = _CleanupTargetMismatch(
                            "A directory this run did not create may have been removed "
                            "and this run's staging directory was left behind; "
                            "manual cleanup required"
                        )
                    elif removal_evidence is None:
                        cleanup_error = _CleanupIncomplete(
                            "Which directory the cleanup removed could not be "
                            "confirmed because the parent could not be listed; "
                            "manual cleanup required"
                        )
            elif cleanup_error is None and cleanup_name:
                cleanup_error = _CleanupIncomplete(
                    "staging directory ownership could not be proven; "
                    "manual cleanup required"
                )
        elif staging_name is not None:
            cleanup_error = OSError("staging directory could not be pinned for safe cleanup")
        _safe_creation_error(exc, cleanup_error)
    finally:
        if directory_fd is not None:
            os.close(directory_fd)
        if parent_fd is not None:
            os.close(parent_fd)

    return {
        "status": "private_local_config_created",
        "serverVersion": "1.13.6",
        "ports": [17880, 17881, 17882],
        "recording": False,
        "scope": "loopback development only",
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--node-ip", default="127.0.0.1")
    args = parser.parse_args()
    try:
        result = configure(args.output, args.node_ip)
    except ValueError as exc:
        parser.error(str(exc))
    except _OutputPathChanged as exc:
        # Every deliberate refusal and every established cleanup finding
        # carries its own operator-facing wording.  The hint below is for an
        # unexpected OS failure: reporting a refusal - a gate that closed, an
        # entry set that changed, a directory preserved for manual cleanup -
        # with a permissions hint sends the operator after a cause that is not
        # there.
        parser.error(str(exc))
    except OSError:
        parser.error("Configuration could not be created; check the private parent and permissions")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
