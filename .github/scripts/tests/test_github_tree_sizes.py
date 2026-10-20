"""Complete GitHub size listings, using mocked HTTP responses only."""
import errno
import http.client
import json
import socket
import ssl
import sys
import unittest
import urllib.error
from io import BytesIO, StringIO
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from github_tree_sizes import api_blob_sizes  # noqa: E402


def blob(path, size):
    return {"type": "blob", "path": path, "size": size}


def tree(path, sha):
    return {"type": "tree", "path": path, "sha": sha}


def listing(*entries, truncated=False):
    return {"truncated": truncated, "tree": list(entries)}


class TreeSizeTests(unittest.TestCase):
    def fetch(self, responses, sequences=None):
        calls = []
        sequences = sequences or {}

        def urlopen(request, timeout):
            url = request.full_url
            prefix = "https://api.github.com/repos/owner/repo/git/trees/"
            self.assertTrue(url.startswith(prefix))
            key = url[len(prefix):]
            calls.append(key)
            self.assertEqual(timeout, 60)
            self.assertEqual(request.get_header("Authorization"), "Bearer test-token")
            self.assertEqual(request.get_header("Accept"), "application/vnd.github+json")
            self.assertEqual(request.get_header("X-github-api-version"), "2022-11-28")
            self.assertIn(key, responses, f"Unexpected request: {key}")
            payload = responses[key]
            if key in sequences:
                sequence = sequences[key]
                payload = sequence[min(calls.count(key) - 1, len(sequence) - 1)]
            if isinstance(payload, Exception):
                if isinstance(payload, urllib.error.HTTPError):
                    self.addCleanup(payload.close)
                raise payload
            return BytesIO(payload if isinstance(payload, bytes) else json.dumps(payload).encode())

        diagnostics = StringIO()
        with mock.patch("github_tree_sizes.urllib.request.urlopen", side_effect=urlopen), \
                mock.patch("github_tree_sizes.time.sleep") as sleep, \
                mock.patch("github_tree_sizes.sys.stderr", diagnostics):
            result = api_blob_sizes("owner/repo", "root", "test-token")
        self.retry_delays = [call.args[0] for call in sleep.call_args_list]
        self.diagnostics = diagnostics.getvalue()
        return result, calls

    def test_transient_http_retry_keeps_exact_request_and_recovers_complete_tree(self):
        error = urllib.error.HTTPError("https://private.invalid/sensitive-query", 502, "private error reason", {}, BytesIO(b"private body"))
        result, calls = self.fetch({"root?recursive=1": listing(blob("valid", 8))},
                                   {"root?recursive=1": [error, listing(blob("valid", 8))]})
        self.assertEqual(result, {"valid": 8})
        self.assertEqual(calls, ["root?recursive=1", "root?recursive=1"])
        self.assertEqual(self.retry_delays, [1])
        self.assertIn("HTTP 502 on attempt 1/3", self.diagnostics)
        self.assertNotIn("private", self.diagnostics)
        self.assertNotIn("test-token", self.diagnostics)

    def test_partial_traversal_retries_only_failed_subtree_and_preserves_sha_cache(self):
        responses = {
            "root?recursive=1": listing(blob("discarded-partial", 99), truncated=True),
            "root": listing(blob("known", 1), tree("A", "shared"), tree("B", "late"), tree("Alias", "shared")),
            "shared?recursive=1": listing(blob("source", 2)),
            "late?recursive=1": listing(blob("recovered", 3)),
        }
        result, calls = self.fetch(responses, {"late?recursive=1": [urllib.error.URLError(TimeoutError("private network reason")),
                                                                               responses["late?recursive=1"]]})
        self.assertEqual(result, {"known": 1, "A/source": 2, "B/recovered": 3, "Alias/source": 2})
        self.assertEqual(calls, ["root?recursive=1", "root", "shared?recursive=1", "late?recursive=1", "late?recursive=1"])
        self.assertEqual(self.retry_delays, [1])
        self.assertNotIn("discarded-partial", result)
        self.assertIn("URLError/TimeoutError", self.diagnostics)

    def test_known_transient_transport_failures_retry_but_return_only_final_complete_data(self):
        for error in (TimeoutError("private timeout"), ConnectionResetError("private reset"),
                      http.client.IncompleteRead(b"private partial"), http.client.RemoteDisconnected("private close"),
                      urllib.error.URLError(socket.gaierror(socket.EAI_AGAIN, "private dns")),
                      urllib.error.URLError(OSError(errno.ENETUNREACH, "private network"))):
            with self.subTest(error_type=type(error).__name__):
                result, calls = self.fetch({"root?recursive=1": listing(blob("complete", 4))},
                                           {"root?recursive=1": [error, listing(blob("complete", 4))]})
                self.assertEqual(result, {"complete": 4})
                self.assertEqual(len(calls), 2)
                self.assertEqual(self.retry_delays, [1])
                self.assertNotIn("private", self.diagnostics)

    def test_permanent_http_errors_are_not_retried_and_cannot_leak_details(self):
        for code in (401, 403, 404, 422, 501):
            with self.subTest(status=code):
                error = urllib.error.HTTPError("https://private.invalid/token-query", code, "private reason", {}, BytesIO(b"private body"))
                result, calls = self.fetch({"root?recursive=1": error})
                self.assertIsNone(result)
                self.assertEqual(calls, ["root?recursive=1"])
                self.assertEqual(self.retry_delays, [])
                self.assertIn(f"HTTP {code}", self.diagnostics)
                self.assertIn("non-transient failure", self.diagnostics)
                self.assertNotIn("private", self.diagnostics)
                self.assertNotIn("test-token", self.diagnostics)

    def test_invalid_json_schema_and_truncation_are_not_retried(self):
        for payload in (b"not json", listing(blob("wrong", -1)), {"truncated": "no", "tree": []}):
            with self.subTest(payload=payload):
                result, calls = self.fetch({"root?recursive=1": payload})
                self.assertIsNone(result)
                self.assertEqual(calls, ["root?recursive=1"])
                self.assertEqual(self.retry_delays, [])
        result, calls = self.fetch({"root?recursive=1": listing(truncated=True), "root": listing(truncated=True)})
        self.assertIsNone(result)
        self.assertEqual(calls, ["root?recursive=1", "root"])
        self.assertEqual(self.retry_delays, [])

    def test_permanent_certificate_dns_and_unknown_url_errors_are_not_retried(self):
        for error in (urllib.error.URLError(ssl.SSLCertVerificationError(1, "private certificate details")),
                      urllib.error.URLError(socket.gaierror(socket.EAI_NONAME, "private dns")),
                      urllib.error.URLError("private unknown URL reason"), PermissionError("private local permission")):
            with self.subTest(error_type=type(error).__name__):
                result, calls = self.fetch({"root?recursive=1": error})
                self.assertIsNone(result)
                self.assertEqual(len(calls), 1)
                self.assertEqual(self.retry_delays, [])
                self.assertNotIn("private", self.diagnostics)

    def test_exhausted_child_retry_discards_all_previously_known_sizes(self):
        result, calls = self.fetch({
            "root?recursive=1": listing(truncated=True),
            "root": listing(blob("known", 1), tree("cached", "complete"), tree("missing", "lost")),
            "complete?recursive=1": listing(blob("good", 2)),
            "lost?recursive=1": TimeoutError("private reason"),
        })
        self.assertIsNone(result)
        self.assertEqual(calls, ["root?recursive=1", "root", "complete?recursive=1"] + ["lost?recursive=1"] * 3)
        self.assertEqual(self.retry_delays, [1, 2])
        self.assertIn("TimeoutError on attempt 3/3", self.diagnostics)
        self.assertIn("retry limit reached", self.diagnostics)
        self.assertIn("no partial result accepted", self.diagnostics)
        self.assertNotIn("private", self.diagnostics)

    def test_retry_does_not_turn_later_invalid_schema_into_another_network_retry(self):
        result, calls = self.fetch({"root?recursive=1": listing(blob("invalid", True))},
                                   {"root?recursive=1": [TimeoutError("private reason"), listing(blob("invalid", True))]})
        self.assertIsNone(result)
        self.assertEqual(len(calls), 2)
        self.assertEqual(self.retry_delays, [1])
        self.assertIn("Missing or invalid blob size", self.diagnostics)

    def test_diagnostics_identify_only_valid_hex_tree_prefix_and_never_echo_untrusted_identifiers(self):
        for tree_sha, expected in (("a" * 40, "aaaaaaaaaaaa"), ("private-token-like-ref", "<tree>")):
            with self.subTest(tree_id=expected):
                diagnostics = StringIO()
                error = urllib.error.HTTPError("https://private.invalid/query", 404, "private reason", {}, None)
                with mock.patch("github_tree_sizes.urllib.request.urlopen", side_effect=error), \
                        mock.patch("github_tree_sizes.time.sleep") as sleep, \
                        mock.patch("github_tree_sizes.sys.stderr", diagnostics):
                    self.assertIsNone(api_blob_sizes("owner/repo", tree_sha, "private-token"))
                self.assertIn("tree " + expected + ": HTTP 404", diagnostics.getvalue())
                self.assertNotIn("private", diagnostics.getvalue())
                sleep.assert_not_called()

    def test_complete_recursive_tree_uses_one_request(self):
        result, calls = self.fetch({"root?recursive=1": listing(
            tree("Assets", "assets"), blob("Assets/한글.txt", 0), blob("README.md", 32),
            {"path": "vendor", "type": "commit", "sha": "submodule"})})
        self.assertEqual(result, {"Assets/한글.txt": 0, "README.md": 32})
        self.assertEqual(calls, ["root?recursive=1"])

    def test_truncated_root_and_nested_subtree_are_completed(self):
        result, calls = self.fetch({
            # Partial entries are discarded, not combined with complete results.
            "root?recursive=1": listing(blob("partial-only", 99), truncated=True),
            "root": listing(blob("top", 1), tree("Assets", "assets")),
            "assets?recursive=1": listing(truncated=True),
            "assets": listing(blob("a", 2), tree("Nested", "nested")),
            "nested?recursive=1": listing(blob("file", 3)),
        })
        self.assertEqual(result, {"top": 1, "Assets/a": 2, "Assets/Nested/file": 3})
        self.assertEqual(calls, ["root?recursive=1", "root", "assets?recursive=1", "assets", "nested?recursive=1"])

    def test_shared_subtree_is_cached_but_keeps_each_path_prefix(self):
        result, calls = self.fetch({
            "root?recursive=1": listing(truncated=True),
            "root": listing(tree("Assets", "shared"), tree("checkpoint", "snapshot")),
            "shared?recursive=1": listing(blob("model.asset", 42)),
            "snapshot?recursive=1": listing(truncated=True),
            "snapshot": listing(tree("Assets", "shared")),
        })
        self.assertEqual(result, {"Assets/model.asset": 42, "checkpoint/Assets/model.asset": 42})
        self.assertEqual(calls.count("shared?recursive=1"), 1)

    def test_child_http_failure_discards_previously_collected_sizes(self):
        result, _ = self.fetch({
            "root?recursive=1": listing(truncated=True),
            "root": listing(blob("known", 1), tree("missing", "missing")),
            "missing?recursive=1": urllib.error.HTTPError("url", 403, "Forbidden", {}, None),
        })
        self.assertIsNone(result)

    def test_missing_or_invalid_blob_size_fails_closed(self):
        for bad in ({"path": "missing", "type": "blob"}, blob("negative", -1), blob("boolean", True), blob("string", "1")):
            with self.subTest(entry=bad):
                result, _ = self.fetch({"root?recursive=1": listing(blob("known", 1), bad)})
                self.assertIsNone(result)

    def test_invalid_json_or_schema_fails_closed(self):
        for payload in (b"not json", [], {}, {"truncated": "false", "tree": []}, {"truncated": False, "tree": {}},
                        listing(None), listing({"path": "unknown", "type": "invalid"}), listing(tree("child", "")),
                        listing(blob("duplicate", 1), blob("duplicate", 2)), listing(blob("../outside", 1)),
                        listing(blob("parent", 1), blob("parent/child", 2))):
            with self.subTest(payload=payload):
                result, _ = self.fetch({"root?recursive=1": payload})
                self.assertIsNone(result)

    def test_truncated_nonrecursive_response_fails_closed(self):
        result, _ = self.fetch({"root?recursive=1": listing(truncated=True), "root": listing(blob("known", 1), truncated=True)})
        self.assertIsNone(result)

    def test_nested_paths_in_nonrecursive_response_fail_closed(self):
        result, _ = self.fetch({"root?recursive=1": listing(truncated=True), "root": listing(blob("wrong/nested", 1))})
        self.assertIsNone(result)

    def test_cycles_are_rejected_without_unbounded_requests(self):
        cases = (
            {"root?recursive=1": listing(tree("self", "root"))},
            {"root?recursive=1": listing(truncated=True), "root": listing(tree("a", "child")),
             "child?recursive=1": listing(tree("back", "root"))},
            {"root?recursive=1": listing(tree("a", "a"), tree("a/b", "b"), tree("a/b/c", "a"))},
        )
        for responses in cases:
            with self.subTest(responses=responses):
                result, calls = self.fetch(responses)
                self.assertIsNone(result)
                self.assertLessEqual(len(calls), 3)

    def test_network_failures_fail_closed(self):
        for error in (urllib.error.URLError("offline"), TimeoutError("timeout"), ConnectionResetError("reset"),
                      http.client.IncompleteRead(b"partial")):
            with self.subTest(error=error):
                result, _ = self.fetch({"root?recursive=1": error})
                self.assertIsNone(result)


if __name__ == "__main__":
    unittest.main()
