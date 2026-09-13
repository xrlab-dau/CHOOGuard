#!/usr/bin/env python3
"""Create private local LiveKit settings. Does not start or buy a server."""
import argparse
import ctypes
import errno
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


class _OutputPathChanged(OSError):
    """The output path no longer names the directory opened for this run."""


class _CleanupIncomplete(_OutputPathChanged):
    """Private output could not be completely removed after a failed run."""


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
    fd = os.open(name, _open_flags(), 0o600, **kwargs)
    try:
        # Raw ownership starts at os.open, not after identity inspection.  This
        # closes the descriptor when fstat, fchmod, or fdopen itself fails.
        acquired_identity = _file_identity(os.fstat(fd))
        if on_acquired is not None:
            on_acquired(path.name, acquired_identity)
        os.fchmod(fd, 0o600)
        stream = os.fdopen(fd, "w")
        fd = None
        try:
            stream.write(text)
        finally:
            stream.close()
    finally:
        if fd is not None:
            os.close(fd)
    _verify_private_file(path, dir_fd=dir_fd, expected_identity=acquired_identity)


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


def _verify_private_file(path, *, dir_fd=None, expected_identity=None):
    file_fd = None
    try:
        if dir_fd is not None:
            flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
            file_fd = os.open(Path(path).name, flags, dir_fd=dir_fd)
            stat_result = os.fstat(file_fd)
        else:
            file_fd = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
            stat_result = os.fstat(file_fd)
        if expected_identity is not None and _file_identity(stat_result) != expected_identity:
            raise _OutputPathChanged("Configuration file path changed during creation")
        if not stat.S_ISREG(stat_result.st_mode):
            raise ValueError("Configuration file is not a regular file")
        if stat_result.st_mode & 0o777 != 0o600:
            raise ValueError("Configuration file privacy could not be verified")
        if stat_result.st_uid != os.getuid():
            raise ValueError("Configuration file owner could not be verified")
        _verify_darwin_no_additional_acl(file_fd)
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
    for name, identity in reversed(owned_files):
        try:
            current = os.stat(name, dir_fd=directory_fd, follow_symlinks=False)
            if _file_identity(current) != identity:
                cleanup_error = cleanup_error or OSError("generated file identity changed")
                continue
            os.unlink(name, dir_fd=directory_fd)
        except FileNotFoundError:
            pass
        except OSError as exc:
            cleanup_error = cleanup_error or exc
    return cleanup_error


def _safe_creation_error(original_error, cleanup_error):
    if cleanup_error is not None:
        raise _CleanupIncomplete(
            "Configuration creation failed; private cleanup is incomplete. "
            "Remove the incomplete private output manually before retrying."
        ) from original_error
    raise original_error


def configure(output, node_ip="127.0.0.1"):
    """Create a local-only LiveKit configuration without exposing credentials."""
    output = Path(output).absolute()
    address = _validate_node_ip(node_ip)
    _require_privacy_backend()
    _validate_output(output)

    parent_fd = None
    directory_fd = None
    parent_stat = None
    directory_stat = None
    staging_name = None
    owned_files = []
    published = False
    # Darwin's pathname mkdir/open sequence cannot prove creation ownership.
    # Keep this false unless the API contract supplies an atomic create+fd
    # primitive; rollback must never rmdir an inode that was not proven ours.
    staging_ownership_proven = False
    try:
        staging_prefix = f".{output.name}."
        if len(os.fsencode(staging_prefix)) + 24 + len(b".tmp") > 255:
            raise ValueError("Configuration output name is too long")
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

        acquire = lambda name, identity: owned_files.append((name, identity))
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
        if not _same_output_directory(parent_fd, staging_name, directory_stat):
            raise _OutputPathChanged("Configuration staging path changed during creation")

        _rename_exclusive(parent_fd, staging_name, output.name)
        published = True
        if not _same_parent_path(output.parent, parent_stat):
            raise _OutputPathChanged("Configuration parent path changed during publication")
        if not _same_output_directory(parent_fd, output.name, directory_stat):
            raise _OutputPathChanged("Configuration output path changed during publication")
    except BaseException as exc:
        cleanup_error = None
        if directory_fd is not None:
            cleanup_error = _cleanup_owned_files(directory_fd, owned_files)
            cleanup_name = output.name if published else staging_name
            can_remove_directory = staging_ownership_proven
            if cleanup_error is None and can_remove_directory and cleanup_name and _same_output_directory(
                parent_fd, cleanup_name, directory_stat
            ):
                try:
                    os.rmdir(cleanup_name, dir_fd=parent_fd)
                except OSError as cleanup_exc:
                    cleanup_error = cleanup_exc
            elif cleanup_error is None and cleanup_name:
                cleanup_error = OSError(
                    "staging directory ownership could not be proven; manual cleanup required"
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
    except _CleanupIncomplete as exc:
        parser.error(str(exc))
    except OSError:
        parser.error("Configuration could not be created; check the private parent and permissions")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
