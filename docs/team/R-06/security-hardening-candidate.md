# R-06 v2 security hardening — unaccepted candidate

Base: `develop@86bb82bf5655562e2577cf2b83553ff261d8d5a6` (2026-09-15).
Scope: the in-process R-06 candidate, its candidate control document and regression tests. No Unity, M0-03, M1-05, global policy, protected-branch or actual research-harness implementation is changed.

## Approval must bind the actual publication

M1-05 deliberately emits an audit projection without free text. Different source bodies can therefore have the same public audit manifest without any cryptographic hash collision. That manifest must not authorize publication of a separately supplied research body.

`publication_candidate()` freezes the exact JSON and Markdown filenames and bytes. Its separate `r06-publication-manifest` contains file byte counts and SHA-256 hashes, sanitizer version and the M1-05 audit digest. It is an approval candidate, not a grant. `publish()` compares the external approval with that manifest and re-derives the full candidate immediately before writes.

### Deliberate v2 compatibility changes

| Surface | Contract |
|---|---|
| R-06 control | `schemaVersion: 2`; v1 controls are refused by v2 code |
| Sanitizer | `R-06/sanitize-v2` |
| `record.manifestSha256` | Remains the M1-05 **audit** digest |
| `record.publicationManifestSha256` | Digest the publication approver must approve |
| `approvalRecord.manifestSha256` | Must contain the **publication** digest, not the audit digest |
| Output JSON/Markdown | Carries `auditManifestSha256`; no circular self-digest |
| Topic `policyHash` | An immutable SHA-256 string equal to `profile.policy.policyHash.value`, not an aliased metadata object |
| `publicationRecord` | A separately recorded post-publication decision/outcome, preserved by aggregate reports |

Old audit-only approvals are rejected, not automatically migrated or reapproved. `_publicationManifestBytes` and `_publicationFiles` are private in-process candidate fields excluded from summary output. `approval_for()` remains a synthetic test helper; it does not authenticate an approver. M0-03/M1-05 byte pins are unchanged, and their source files are input-only.

## Boundary fixes

Unknown request field names are not reflected in refusal codes. Topic/model metadata and queries are inspected before the fake sender, including bounded percent-decoded representations. Model egress requires the exact declared positive disposition; missing/unknown dispositions, non-string approval references and conflicting approval records are denied. A missing DNS mapping refuses before sending.

HTTPS URL validation rejects ambiguous raw syntax, invalid DNS authorities, invalid ports and control/whitespace characters before parser normalization. The detector covers URL authority, path, query and fragment values, not just selected credential query keys. Markdown table text is HTML-escaped. These are limited candidate rules, not a complete sensitive-data or prompt-injection detector.

Redirects require HTTPS, the granted host and default HTTPS port. Earlier followed hops remain counted even if a later response is refused. Only status 200 is a successful fake response; malformed wrappers and unsuccessful statuses are recorded refusals. No socket, DNS resolver, HTTP library, model or search SDK is invoked by this harness.

Raw records are exclusively created, with POSIX mode 0600 independent of the ambient umask. The unchanged M1-05 pipeline produces and verifies each audit bundle. Publication results record approval state, success/failure and private digest bindings. The public M1 projection still omits free text and must not be mistaken for full receipt identity.

Existing entries, including dangling symlinks **inside** the output root, are refused before the first output write. Slugs are constrained and dates must be real ASCII calendar dates. An I/O failure reports refusal and preserves approved partial output rather than overwriting, deleting evidence or claiming success. Stable, exclusively owned local parents remain required. This is **not** atomic multi-file publication, crash durability, a hostile-writer sandbox or Windows ACL verification.

## Reproducible verification and limits

Run the complete suites from the repository root (Python 3.12 or later; the
schema conformance and M1-05 schema tests require jsonschema):

```sh
python3 -B -m unittest discover -s scripts/team/R-06 -p 'test_*.py' -v
python3 -B -m unittest discover -s scripts/team/M1-05 -p 'test_*.py' -v
python3 -B scripts/team/R-06/egress_controls.py run
```

On 2026-09-15 the canonical M0-03 profile, full M1-05 schema and unchanged
pipeline bytes were obtained from the pinned develop commit and verified by Git
blob ID and SHA-256. They were not replaced with synthetic hashes. On Python
3.13.5/POSIX the revised candidate ran 76 R-06 tests and 25 M1-05 tests with zero
failures or skips; the default CLI matched all 36 synthetic cases. The added
conformance matrix compares the dependency-free guard with the actual JSON
Schema validator on 320 type/reference combinations plus eight numeric-version
probes. Numeric 1.0 satisfies JSON Schema const 1; boolean true does not.

The separately isolated unit fixtures are still synthetic. Canonical-input
integration does not turn FakeTransport into a network test, authenticate a
human approver, or validate an operating-system sandbox. Platform CI outcomes
must be read on the proposed commit, not inferred from local results. Earlier
review packages' partial-source and unavailable-input limits describe those
historical executions, not the current canonical input availability.

Keep `docs/team/R-06/synthetic-execution.json` as historical v1 evidence. Do not
refresh its hashes or promote its old PASS. The real `tools/research/research.py`
is not wired to these controls. Policy acceptance, protected PR review and
product/Unity/network acceptance remain separate. No AAA, PM/CODEOWNER approval,
real egress or publication permission is granted by these test results.
