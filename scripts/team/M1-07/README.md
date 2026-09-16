# M1-07 review integrity candidate

Refs #23. Run the synthetic contract tests with Python 3:

```sh
python scripts/team/M1-07/test_review_integrity.py
```

`review_integrity.py` reuses the published native manifest helper and M1-02
permission decisions. It checks the M0-03 allowlist/profile and M1-02 receipt
bindings before use. Standard-library preflight reads the contract from Git HEAD,
checks its declared refs/hashes, and verifies both direct Python suppliers plus
their transitive `verify_toolchain.py` dependency before importing any of them.
Every public operation rechecks checkout contract/source bytes, including after
the modules are cached. Missing/changed supplier bytes return `Refused` with
`cannot_proceed` before run/output creation. Complete local Git history and a
committed contract are required. Git commands only read source bindings; no model,
Unity process or review/rework loop is invoked.

`begin` receives fixture author/reviewer identities, a scoped list of synthetic
source files, base/head refs and round 1–3. It requires different sessions and
providers plus an allowed reviewer model. Identities are supplied per run; the
test constants describe synthetic actors, not the model executing these tests.

The new run directory has four distinct areas:

| Path | Purpose |
| --- | --- |
| `request.json` | Frozen target manifest, identities, policy hash and round |
| `input/` | Materialized target snapshot, verified before finalization |
| `execution/` | Separate file copies; the API permits new `generated/` output |
| `evidence/receipt.json` | Exclusively created result, bound to request/target hashes |

The fixture treats its selected files as uncommitted synthetic inputs and records
their exact hashes alongside base/head. Source names cannot point to protocol
outputs; linked/escaping paths and overlapping run/source roots are refused.
Only canonical portable file names are accepted: aliases such as `./fixture.txt`,
`.`/`./`, doubled separators and trailing slashes are refused before creating a
run. The same accepted names are used for copying, manifest keys and inspection.
Generated files are not silently added to the reviewed target manifest.

`finalize` accepts only the fixture reviewer role and a valid reviewer-output
shape. It never overwrites an existing receipt. `verify_receipt` requires the
caller's trusted request/receipt digests **and current writer source/base/head**.
Changed target bytes, refs, request or receipt invalidate reuse. An unchanged
snapshot alone cannot qualify a result for a newer writer revision. Rejected API
writes preserve existing receipts; direct out-of-API tampering is detected by the
trusted digest, not undone.

This is an integrity and policy API fixture. Caller role strings, model identities,
refs and external digest custody are trusted test inputs; they are not authenticated
accounts or provider attestations. Direct filesystem permissions are not enforced,
and this is not an adversarial concurrency test. A host with separate accounts or
sandbox controls must enforce those boundaries for a real run. M1-02's published
receipt also leaves OS sandbox/ACL enforcement untested.

`approved` inside a synthetic reviewer result does not approve this candidate or
the product. Three-perspective aggregation, real provider invocation, retry/fallback
enforcement and final acceptance belong to later scoped work (#45 and accept).
The proposed ADR's three-round ceiling is checked as fixture metadata; there is no
cross-process round counter or automatic orchestration. No unpublished Native
document is consumed or inferred.
