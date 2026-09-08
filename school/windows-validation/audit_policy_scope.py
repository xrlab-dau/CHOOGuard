"""Observe every declared policy glob's omission behavior using synthetic fixtures.

No real tool installation or credentials; the existing boundary-test fixture mocks
tool availability. This audits today's required/optional split, not its approval.
"""
from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path


def audit() -> dict:
    root = Path(__file__).resolve().parents[2]
    source = root / "scripts/bootstrap/test_verify_toolchain.py"
    spec = importlib.util.spec_from_file_location("scope_test", source)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    case = module.ToolchainBoundaryTests()
    case.setUp()
    try:
        for pattern in module.verifier.POLICY_GLOBS:
            if not list(case.root.glob(pattern)):
                target = case.root / pattern.replace("*", "scope-fixture")
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text("{}" if target.suffix == ".json" else "# synthetic fixture\n", encoding="utf-8")
        # A synthetic approval/pin exercises comparison; it is never a real
        # project policy approval. Keep the same pin through every omission.
        files = {path.relative_to(case.root).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
                 for pattern in module.verifier.POLICY_GLOBS
                 for path in case.root.glob(pattern) if path.is_file()}
        manifest = case.root.parent / "synthetic-baseline.json"
        manifest.write_text(json.dumps({"schemaVersion": 1, "status": "approved",
                            "approvalReference": "SYNTHETIC-AUDIT-ONLY", "sourceCommit": "0" * 40,
                            "policySha256": files}, sort_keys=True), encoding="utf-8")
        arguments = ("--policy-manifest", str(manifest), "--policy-manifest-sha256",
                     hashlib.sha256(manifest.read_bytes()).hexdigest())
        baseline = case.invoke(*arguments)
        if baseline != 0:
            raise RuntimeError("Complete synthetic policy fixture was rejected")
        rows = []
        for pattern in module.verifier.POLICY_GLOBS:
            members = {path: path.read_bytes() for path in case.root.glob(pattern) if path.is_file()}
            if not members:
                raise RuntimeError("Missing initial fixture for " + pattern)
            for path in members:
                if case.root.resolve() not in path.resolve().parents:
                    raise RuntimeError("Fixture path escaped temporary root")
                path.unlink()
            try:
                code = case.invoke(*arguments)
                rows.append({"glob": pattern, "requiredByCurrentCode": pattern in module.verifier.REQUIRED_POLICY_GLOBS,
                             "exitCodeWhenAbsent": code})
            finally:
                for path, content in members.items():
                    path.write_bytes(content)
        return {"scope": "Synthetic omission audit with a fixed synthetic manifest and mocked tools. No human/runtime approval.",
                "sourceSha256": hashlib.sha256((root / "scripts/bootstrap/verify_toolchain.py").read_bytes()).hexdigest(),
                "baselineExitCode": baseline, "rows": rows}
    finally:
        case.doCleanups()


if __name__ == "__main__":
    print(json.dumps(audit(), ensure_ascii=False, indent=2))
