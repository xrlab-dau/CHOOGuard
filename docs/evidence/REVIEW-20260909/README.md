# Development Review follow-up

Refs #10, #14, #25, #26, #27, #32, #52, #64, #65, #66, #68. Review scope follows the live development board, not a list of all open PRs.

## Independent execution

Qwen/Qwen3-Coder-30B-A3B-Instruct revision `b2cff646eb4bb1d68355c01b18ae02e7cf42d120` ran on Hugging Face Jobs, NVIDIA A100-SXM4-80GB, vLLM 0.10.2, PyTorch 2.8.0+cu128 and Python 3.12.11. The model only received public text strings with no tools; it could neither mutate the repository nor execute its responses. No images, geometry or credentials were sent to the model. Fal was not used.

- Model preflight: `6aa09dd932d5d0c22c5aeeb6`, successful.
- First GPU attempt: `6aa09f0132d5d0c22c5aeede`, container startup failed because `python` was absent; no inference ran.
- [Round 1 job](https://huggingface.co/jobs/umyunsang/6aa09fb6900620b5c77e46fe): `python3` started correctly; 55 immutable public source files were SHA-256 verified; all 21 lens calls returned.

`round1.json` preserves model responses, source manifests, prompt/response hashes, token counts, finish reasons and structural validation. Three fresh, history-free conversations were run per target. JSON inputs were losslessly whitespace-compacted; no source was truncated. Source hashes were checked before and after inference. The model is a distinct Qwen model hosted by Hugging Face, not an OpenAI subagent. This does not validate Pi modelScope/watchdog or institutional approval.

## Round 1 acceptance limits

Questionnaire, context implementation and PR98 graph each returned three structurally valid approvals. The core safety response incorrectly asserted that unexecuted canonical Unity tests passed; it is rejected as evidence pending re-review. PR104 spec also overstates execution of the supplied Unity test file. Asset adversarial output says approved while listing findings, and asset safety output was truncated: both are `cannot_proceed`. PR97 findings are preserved for source-backed challenge and independent re-review; an external manifest path is part of its trusted-caller contract, and its current head already has regular-file/schema/stability checks.

No model statement renews Unity, visual or field acceptance. Fresh hosted PR104 proof remains 37 core + 12 filesystem C# tests and 92 reconstruction Python tests; actual Unity tests were skipped. Historical receipts retain their original source coverage.

## Reproduced asset defect and bounded fix

The adversarial input investigation found `references: [null]` raises `AttributeError`; malformed observation strings and mismatched image/hash counts can also be accepted. These are validator robustness/provenance defects, assessed as P2, not the model's unsupported P0 severity. Empty references and empty object entries already returned validation errors. A general hash-error consistency change and speculative digest caching were not justified.

The fix requires reference records, URLs, dates, observation arrays and SHA-256 arrays to have the expected types, requires nonempty observation strings, and binds every listed image to one digest. It returns ordinary validation errors for malformed references. It does not change assets, geometry, Unity files or institutional claims.

- `asset-input-red.log`: 13 tests, 3 failures / 9 errors before the guard.
- `asset-url-red.log`: numeric URL still raised `AttributeError` before the URL guard.
- `asset-input-green.log`: all 13 tests pass, including 21 malformed or incomplete input subcases.
- `asset-current-validation.json`: all 33 existing reference/model records validate with zero errors under `--require-reviewed`; this checks recorded bindings, not a new visual inspection.
- Context 17 tests, graph structure and repository policy passed locally. Current source pointers were refreshed; historical receipt/coverage hashes were not refreshed. The navigation refresh log keeps its existing 20-entry limit; earlier entries remain in Git history.

## Current board disposition

Issue #10 has unimplemented FMP-01 criteria: current canonical documents exclude network multiplayer and omit the new 20/NPC100/2 scope and participant/world/observed-state contracts. [Review findings](https://github.com/xrlab-dau/CHOOGuard/issues/10#issuecomment-5593599178) were posted and both cards were verified as In progress / Dependency. #66 remains closed/Done and #27 remains In progress; no prior user state is reversed.

Further independent re-review and CI results will be appended as new evidence. No PR has been merged, no Unity license has been activated, and no KORAIL message has been sent.
