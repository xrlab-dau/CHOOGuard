"""Complete blob sizes from GitHub Trees, including repositories above the recursive API limit.

GitHub limits recursive listings to 100,000 entries / 7 MB and recommends
non-recursive subtree traversal when truncated:
https://docs.github.com/en/rest/git/trees#get-a-tree
"""
from __future__ import annotations

import errno
import http.client
import json
import socket
import ssl
import sys
import time
import urllib.error
import urllib.parse
import urllib.request


REQUEST_TIMEOUT = 60
MAX_ATTEMPTS = 3
RETRY_DELAYS = (1, 2)
TRANSIENT_HTTP = frozenset((408, 429, 500, 502, 503, 504))
TRANSIENT_ERRNOS = frozenset((errno.ECONNRESET, errno.ECONNREFUSED, errno.ECONNABORTED,
                             errno.EHOSTUNREACH, errno.ENETUNREACH, errno.ETIMEDOUT, errno.EPIPE))


def _transient(error: BaseException) -> bool:
    if isinstance(error, urllib.error.HTTPError):
        return error.code in TRANSIENT_HTTP
    reason = error.reason if isinstance(error, urllib.error.URLError) else error
    if isinstance(reason, ssl.SSLCertVerificationError):
        return False
    if isinstance(reason, socket.gaierror):
        return reason.errno == socket.EAI_AGAIN
    return (isinstance(reason, (TimeoutError, ConnectionError, http.client.IncompleteRead, http.client.RemoteDisconnected))
            or isinstance(reason, OSError) and reason.errno in TRANSIENT_ERRNOS)


def _failure_label(error: BaseException) -> str:
    # Never print URLs, authorization, response bodies or raw exception reasons.
    if isinstance(error, urllib.error.HTTPError):
        return f"HTTP {error.code}" if type(error.code) is int else "HTTPError"
    if isinstance(error, urllib.error.URLError):
        return "URLError/" + type(error.reason).__name__
    return type(error).__name__


def _diagnostic(message: str) -> None:
    print("GitHub tree sizes: " + message, file=sys.stderr)


def _request_json(request: urllib.request.Request, recursive: bool, tree_sha: str):
    kind = "recursive" if recursive else "non-recursive"
    tree_hint = tree_sha[:12] if len(tree_sha) in (40, 64) and all(c in "0123456789abcdefABCDEF" for c in tree_sha) else "<tree>"
    for attempt in range(1, MAX_ATTEMPTS + 1):
        try:
            with urllib.request.urlopen(request, timeout=REQUEST_TIMEOUT) as response:
                return json.load(response)
        except (urllib.error.URLError, http.client.HTTPException, OSError) as error:
            if isinstance(error, urllib.error.HTTPError):
                error.close()
            transient = _transient(error)
            retry = transient and attempt < MAX_ATTEMPTS
            detail = f"{kind} listing tree {tree_hint}: {_failure_label(error)} on attempt {attempt}/{MAX_ATTEMPTS}"
            if not retry:
                reason = "retry limit reached" if transient else "non-transient failure"
                _diagnostic(detail + f"; {reason}; complete sizes unavailable (no partial result accepted).")
                raise
            delay = RETRY_DELAYS[attempt - 1]
            _diagnostic(detail + f"; retrying in {delay}s.")
            time.sleep(delay)


class _InvalidTree(ValueError):
    """The API response cannot establish complete, unambiguous blob sizes."""


def api_blob_sizes(repo: str, sha: str, token: str) -> dict[str, int] | None:
    """Return all blob sizes, or None if any required listing is unavailable or invalid.

    Prefer the usual single recursive request. A truncated listing is discarded;
    fetch that tree without the recursive parameter and resolve its subtrees.
    Complete results are cached by tree SHA, while paths are prefixed separately
    at each occurrence (for example, a checkpoint reusing an Assets subtree).
    """
    cache: dict[str, dict[str, int]] = {}
    visiting: set[str] = set()

    def listing(tree_sha: str, recursive: bool) -> tuple[bool, list[dict]]:
        endpoint = f"https://api.github.com/repos/{urllib.parse.quote(repo, safe='/')}/git/trees/{urllib.parse.quote(tree_sha, safe='')}"
        # Even recursive=0 enables recursion; the parameter must be absent here.
        request = urllib.request.Request(endpoint + ("?recursive=1" if recursive else ""), headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
        })
        payload = _request_json(request, recursive, tree_sha)
        if not isinstance(payload, dict) or type(payload.get("truncated")) is not bool or not isinstance(payload.get("tree"), list):
            raise _InvalidTree("Invalid tree response")
        entries = payload["tree"]
        by_path: dict[str, dict] = {}
        for entry in entries:
            if not isinstance(entry, dict):
                raise _InvalidTree("Invalid tree entry")
            path, kind = entry.get("path"), entry.get("type")
            if (not isinstance(path, str) or not path or "\0" in path
                    or any(part in ("", ".", "..") for part in path.split("/"))
                    or (not recursive and "/" in path) or path in by_path
                    or kind not in ("blob", "tree", "commit")):
                raise _InvalidTree("Invalid or duplicate tree path")
            if kind == "blob" and (type(entry.get("size")) is not int or entry["size"] < 0):
                raise _InvalidTree("Missing or invalid blob size")
            if kind == "tree" and (not isinstance(entry.get("sha"), str) or not entry["sha"]):
                raise _InvalidTree("Missing subtree SHA")
            by_path[path] = entry
        for path, entry in by_path.items():
            parent = path
            ancestor_shas = {tree_sha} if entry["type"] == "tree" else set()
            while "/" in parent:
                parent = parent.rsplit("/", 1)[0]
                if parent in by_path:
                    ancestor = by_path[parent]
                    if ancestor["type"] != "tree":
                        raise _InvalidTree("A blob or submodule cannot contain paths")
                    ancestor_shas.add(ancestor["sha"])
            if entry["type"] == "tree" and entry["sha"] in ancestor_shas:
                raise _InvalidTree("Cyclic tree response")
        return payload["truncated"], entries

    def complete(tree_sha: str) -> dict[str, int]:
        if tree_sha in visiting:
            raise _InvalidTree("Cyclic tree traversal")
        if tree_sha in cache:
            return cache[tree_sha]
        visiting.add(tree_sha)
        truncated, entries = listing(tree_sha, recursive=True)
        if not truncated:
            # A complete recursive response must not point back into our active ancestry.
            if any(e["type"] == "tree" and e["sha"] in visiting for e in entries):
                raise _InvalidTree("Cyclic tree traversal")
            sizes = {e["path"]: e["size"] for e in entries if e["type"] == "blob"}
        else:
            truncated, entries = listing(tree_sha, recursive=False)
            if truncated:
                raise _InvalidTree("Non-recursive listing is still truncated")
            sizes = {}
            for entry in entries:
                if entry["type"] == "blob":
                    sizes[entry["path"]] = entry["size"]
                elif entry["type"] == "tree":
                    for path, size in complete(entry["sha"]).items():
                        sizes[f"{entry['path']}/{path}"] = size
                # Commit entries are submodules, not blobs in this repository.
        visiting.remove(tree_sha)
        cache[tree_sha] = sizes
        return sizes

    try:
        if (not isinstance(repo, str) or len(repo.split("/")) != 2
                or any(part in ("", ".", "..") for part in repo.split("/"))
                or not isinstance(sha, str) or not sha or not isinstance(token, str) or not token):
            _diagnostic("invalid request inputs; complete sizes unavailable.")
            return None
        return complete(sha)
    except _InvalidTree as error:
        _diagnostic(f"{error}; complete sizes unavailable (no partial result accepted).")
        return None
    except (urllib.error.URLError, http.client.HTTPException, OSError):
        # The request diagnostic above identifies the sanitized transport cause.
        return None
    except (ValueError, RecursionError) as error:
        _diagnostic(f"{type(error).__name__} while reading or validating tree data; complete sizes unavailable (no partial result accepted).")
        return None
