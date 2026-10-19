"""Compare exact Unity input subtree IDs without GitHub's paginated diff API."""
from __future__ import annotations

import json
import re
import subprocess
import sys


INPUTS = ("Assets", "Packages", "ProjectSettings", "content", "docs/build",
          "docs/CHOOGuard_Story_Plan_v4/basis/v3/contracts")
SHA = re.compile(r"[0-9a-f]{40}")
REPOSITORY = re.compile(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+")


class ComparisonError(RuntimeError):
    pass


def api_json(path: str) -> dict:
    # gh reads GH_TOKEN from the step environment; credentials never enter argv,
    # output, repository configuration or a persisted checkout credential.
    try:
        result = subprocess.run(["gh", "api", path], capture_output=True, timeout=15, check=False)
        if result.returncode:
            raise ComparisonError("GitHub Git object metadata is unavailable")
        data = json.loads(result.stdout)
    except (OSError, subprocess.TimeoutExpired, ValueError) as error:
        raise ComparisonError("GitHub Git object metadata is unavailable") from error
    if not isinstance(data, dict):
        raise ComparisonError("GitHub Git object metadata has an invalid shape")
    return data


class GitInputs:
    def __init__(self, repository: str, request=api_json):
        if not REPOSITORY.fullmatch(repository):
            raise ComparisonError("Invalid repository identity")
        self.repository = repository
        self.request = request
        self.trees = {}

    def commit_tree(self, commit: str) -> str:
        if not SHA.fullmatch(commit):
            raise ComparisonError("Full immutable commit IDs are required")
        data = self.request(f"repos/{self.repository}/git/commits/{commit}")
        tree = data.get("tree", {}).get("sha")
        if data.get("sha") != commit or not isinstance(tree, str) or not SHA.fullmatch(tree):
            raise ComparisonError("GitHub commit metadata does not match the requested commit")
        return tree

    def tree(self, tree_id: str) -> dict:
        if tree_id not in self.trees:
            # Deliberately nonrecursive: read directory entries, never enumerate
            # the entire Assets tree, historical paths, file blobs or LFS data.
            data = self.request(f"repos/{self.repository}/git/trees/{tree_id}")
            entries = data.get("tree")
            if data.get("sha") != tree_id or data.get("truncated") is not False or not isinstance(entries, list):
                raise ComparisonError("GitHub tree metadata is incomplete")
            mapped = {}
            for entry in entries:
                if (not isinstance(entry, dict) or not isinstance(entry.get("path"), str)
                        or entry["path"] in mapped or "/" in entry["path"]
                        or entry.get("type") not in ("tree", "blob", "commit")
                        or not isinstance(entry.get("sha"), str) or not SHA.fullmatch(entry["sha"])
                        or not isinstance(entry.get("mode"), str)):
                    raise ComparisonError("GitHub tree metadata has an invalid entry")
                mapped[entry["path"]] = entry
            self.trees[tree_id] = mapped
        return self.trees[tree_id]

    def input_id(self, root: str, path: str):
        parts = path.split("/")
        current = root
        for index, part in enumerate(parts):
            entry = self.tree(current).get(part)
            if entry is None:
                return None
            if index == len(parts) - 1:
                return entry["type"], entry["mode"], entry["sha"]
            if entry["type"] != "tree":
                raise ComparisonError("A Unity input parent is not a directory")
            current = entry["sha"]

    def first_changed(self, before: str, after: str):
        if not SHA.fullmatch(before) or not SHA.fullmatch(after):
            raise ComparisonError("Full immutable commit IDs are required")
        if before == after:
            return None
        left, right = self.commit_tree(before), self.commit_tree(after)
        for path in INPUTS:
            if self.input_id(left, path) != self.input_id(right, path):
                return path
        return None


def main(argv=None) -> int:
    args = sys.argv[1:] if argv is None else argv
    try:
        if len(args) != 3:
            raise ComparisonError("Repository and two immutable commit IDs are required")
        changed = GitInputs(args[0]).first_changed(args[1], args[2])
    except (ComparisonError, KeyError, TypeError, AttributeError):
        # The workflow treats this nonzero outcome as requiring Unity. Do not
        # expose raw API bodies, gh diagnostics or process environment values.
        print("Unity input tree comparison unavailable; run Unity conservatively", file=sys.stderr)
        return 1
    print(changed or "")
    return 0


if __name__ == "__main__":
    sys.exit(main())
