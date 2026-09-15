# M1-06 synthetic contention and record integration

Refs #22. This candidate connects the published M1-01 simulated lease registry
to the M1-05 redaction/public-record pipeline. It adds no lease service or changes
to either supplier. It does not qualify a live Editor, Native runtime, OS lock,
or unpublished Native contract.

Run with Python 3 and `jsonschema` (also used by repository validation):

```sh
python scripts/team/M1-06/test_contention.py
# Commit source first; choose a fresh output path for every recorded run.
python scripts/team/M1-06/contention.py --output docs/evidence/M1-06/runs/UNIQUE-RUN
```

The fixture attempts overlapping workspace-file, project, Editor, exporter and
manifest leases. It also exercises handoff with explicit reacquisition, old-holder
write rejection, cancellation, unknown holders, expiry, crash recovery, and dirty
content recovery denial. All holders, scope paths, timestamps, base refs and content
hashes inside the observations are synthetic. No Unity files are opened or edited.
Independent registries execute sequentially; this is not a process race test.

Each registry call maps to one journal entry and one typed public event, in call
order. A refused operation remains `failure/failed` even when the rejection is the
expected test result. Successful reacquisition is `retry/recorded`; cancellation
uses `cancel/cancelled`. The separate synthetic receipt preserves holder, base,
scope, refusal reason and recovery detail; public pipeline events deliberately
omit text and retain their ordinal link to the receipt's `cases` array.

Newly authored placeholder raw events are public synthetic input with
`requiredForEvidence: false`. The actual redaction pipeline executes outside the
checkout, validates raw/private/public/manifest records against the central JSON
Schema, checks the unchanged input and private input hash, and verifies public
file hashes. Temporary raw/private files are discarded after the fixture; neither
their paths nor their hashes are exported. This is not private-data retention
evidence. The exported receipt and public bundle are entirely synthetic.

The CLI binds its inputs and harness/tests to a committed source revision, records
checkout and Git blob SHA-256 values separately for CRLF portability, and refuses
changed source files or an existing output directory. A failed assertion preserves
a failed receipt; invalid/missing supplier input stops with `cannot_proceed`.
The candidate index binds each immutable run's receipt and public files by hash.
Source qualification and final acceptance remain review decisions; no accept index
is produced.
