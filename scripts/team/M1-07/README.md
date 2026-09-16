# M1-07 review integrity candidate

Refs #23. Run the synthetic contract checks with the default environment and Python 3:

```sh
python3 scripts/team/M1-07/test_review_integrity.py
```

The suite exits 0 with every case reporting `ok` on the default environment; no environment
variable or special `TMPDIR` is required. Each case asserts its exact refusal code, so a
refusal raised for an unrelated reason cannot satisfy a negative case.

`review_integrity.py` reuses the published native manifest helper and M1-02
permission decisions. It checks the M0-03 allowlist/profile and M1-02 receipt
bindings before use. Supplier drift stops the operation. It invokes no model,
shell command, Unity process or review/rework loop.

`begin` receives fixture author/reviewer identities, a scoped list of canonical
synthetic source file names, base/head refs and round 1–3. It requires different
sessions and providers plus an allowed reviewer model. Identities are supplied per
run and are written into that run's `request.json`; no reviewer provider or model is
preassigned by this artifact. The test constants describe synthetic actors, not the
model executing these tests.

The new run directory has four distinct areas:

| Path | Purpose |
| --- | --- |
| `request.json` | Frozen target manifest, identities, policy hash and round |
| `input/` | Materialized target snapshot, verified before finalization |
| `execution/` | Separate file copies; the API permits new `generated/` output |
| `evidence/receipt.json` | Exclusively created result, bound to request/target hashes |

Target names must be canonical portable relative paths: a `.` segment, a leading
`./`, a doubled separator, a `..` segment, a backslash, a drive letter, an absolute
path or a spelling that normalizes to something other than itself is refused as
`invalid_relative_name` or `non_canonical_relative_name`. The normalized form is the
manifest key and the on-disk relative name, so a spelling that normalizes differently
cannot freeze a manifest the run can never satisfy. Top-level `input/`, `execution/`,
`evidence/` and `request.json` are refused as `protocol_output_is_not_target_input`,
in every spelling, so the target manifest can never be self-referential.

The fixture treats its selected files as uncommitted synthetic inputs and records
their exact hashes alongside base/head. Linked/escaping paths and overlapping
run/source roots are refused. Any root reached through a link is refused, including a
host temporary directory reached through a system alias; callers must pass a real,
non-linked root, so the checks resolve their temporary root before calling this API.
Generated files are not silently added to the reviewed target manifest.

`finalize` accepts only the fixture reviewer role and a valid reviewer-output
shape. It never overwrites an existing receipt: a second or foreign result is refused
as `receipt_already_exists` or `writer_cannot_finalize`. `write_generated` creates new
files only under `execution/generated/` and refuses to overwrite one that exists.
`verify_receipt` requires the caller's trusted request/receipt digests **and current
writer source/base/head**. Changed target bytes, refs, request or receipt invalidate
reuse (`target_bytes_changed`, `current_writer_target_changed`, `request_digest_changed`,
`receipt_digest_changed`). An unchanged snapshot alone cannot qualify a result for a
newer writer revision. Rejected API writes preserve existing receipts; direct
out-of-API tampering is detected by the trusted digest, not undone.

The suite also recomputes the hashes of the committed `sample-request.json`,
`sample-receipt.json` and `sample-target.txt` records, so the recorded request digest,
target digest and receipt binding stay independently checkable.

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
