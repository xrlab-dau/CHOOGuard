# Execution and approval boundaries

The first Round 3 GPU request was rejected by automatic approval review: it included extensive issue bodies and unpublished review context, whose sensitivity and destination authorization the reviewer could not establish. It created no GPU job and executed no inference. `round3-input.json` records that prepared but unsent target/context.

The repository was then freshly checked through the GitHub connector: `xrlab-dau/CHOOGuard`, `private=false`, `visibility=public`. A materially narrower request excludes all issue bodies, unpublished reviewer context, actual asset registries/manifests, images and geometry. It uses only 20 pre-existing public source/test/documentation files with exact commit and SHA-256 bindings. Selected bytes were checked for credential/private-key patterns; no credential payload is included. The model has no tools and its output is never executed.

This public-code-only request was allowed and created job [6aa0ad0032d5d0c22c5aeff1](https://huggingface.co/jobs/umyunsang/6aa0ad0032d5d0c22c5aeff1). Its actual target is `round3-public-input.json`, not the earlier rejected input. Model: Qwen/Qwen3-32B-AWQ revision `0499c3ac83fdef8810b907a23894ba91e95eddd8`, verified public and ungated. The request has a 20-minute hosted timeout and an 18-minute in-process deadline. Completion exits the process after flushing receipts to avoid leaving the inference engine running.

Three fresh conversations cover spec, adversarial and safety per scoped implementation. The narrowed source manifests differ from earlier full manifests; a source approval does not become a new visual, full-registry, Unity, school-PC, PM-policy or institutional approval.

Round 2 emitted all 12 response records and its completion record, then the hosted job was marked ERROR / Job timeout. `round2-complete.json` preserves all records with the actual job status; `round2.json` is an earlier 11-response snapshot. The previously stated approximately $0.84 figure was a 20-minute GPU-rate estimate, not a verified bill; startup/service teardown timing and final charges have not been audited.

Fal was not used. No PR was merged or deployed, no Unity license was activated, and no KORAIL message was sent.

## Concurrent source update and completion

PR97 moved to `39e5dc24892744feacec09eac0c2e71951cb31c8` during the first public-only Round 3 job. The cancellation decision was based on this source movement before examining the emitted verdicts. Eight completed core/asset/capture lenses were preserved; no policy lens had run. The three core and asset lenses were not repeated.

The one missing capture lens and the three policy lenses resumed in allowed public-only job [6aa0af1c900620b5c77e49ca](https://huggingface.co/jobs/umyunsang/6aa0af1c900620b5c77e49ca), using the current policy source and public shell/PowerShell wrappers plus regression tests. Its exact input is `round3-remaining-input.json`; it had a 12-minute in-process deadline and a 14-minute service timeout. This continuation recorded thinking-mode sampling and retained the actual raw responses and hashes. All four responses and completion were emitted and the service reported COMPLETED. See `round3-part2.json`.

Across Round 3 there are exactly three completed lenses per unit, with the policy target updated before any policy lens executed. Core/asset/capture failed or disputed responses remain preserved. No approval was manufactured by dropping a failed lens, restarting a completed lens or resetting a round budget.
