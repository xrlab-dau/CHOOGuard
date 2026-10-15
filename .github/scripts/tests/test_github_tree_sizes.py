"""Complete GitHub size listings, using mocked HTTP responses only."""
import http.client
import json
import sys
import unittest
import urllib.error
from io import BytesIO
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
    def fetch(self, responses):
        calls = []

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
            if isinstance(payload, Exception):
                if isinstance(payload, urllib.error.HTTPError):
                    self.addCleanup(payload.close)
                raise payload
            return BytesIO(payload if isinstance(payload, bytes) else json.dumps(payload).encode())

        with mock.patch("github_tree_sizes.urllib.request.urlopen", side_effect=urlopen):
            result = api_blob_sizes("owner/repo", "root", "test-token")
        return result, calls

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
