# Local model feasibility benchmark

This task-local backend runs a public, pinned Qwen2.5-1.5B-Instruct MLX conversion. It has no paid API dependency and never discovers credentials. The model artifact is Apache2 according to the upstream model card; this community conversion is a small feasibility model, not a validated railway-response adviser.

Verified machine: Apple M1, 16 GiB RAM, macOS26.5, Python3.14.8. Package versions are in `requirements.lock`; exact model revision, per-file hashes and package receipts are in `output/simulator/model-benchmark`. The current macOS execution context cannot reach MTLCompilerService (lookup141). CPU execution therefore dequantizes the immutable 4-bit file into float32 RAM once (~6.2 GiB resident) and uses MLX CPU matrix operations. This is CPU timing, not GPU performance. Shared Unity and other processes can change wall-clock latency.

The model and environment are already installed in `.venv` and `output/simulator/model-benchmark/models`. Do not download again when their receipt hashes match. To install a missing environment, create a task-local venv with uv and install exactly `requirements.lock`; run `prepare.py` only when the pinned public model files are missing.

Run from the repository root in separate terminals:

```sh
workers/simulator/model_service/.venv/bin/python workers/simulator/model_service/launch.py --device cpu
python3 workers/simulator/model_service/bridge.py
python3 workers/simulator/model_service/run_benchmark.py --timeout 180
python3 workers/simulator/model_service/run_evaluator.py
```

The MLX server binds only127.0.0.1:18952 and accepts one generation at a time. The bridge binds127.0.0.1:18951 and fixes clients to the installed model. Generator endpoint: `http://127.0.0.1:18951/v1/chat/completions`, model `default_model`, maximum output160tokens for this benchmark. The 180second benchmark deadline is explicit; the simulation continues during the asynchronous call. The Unity wrapper has a separate configurable deadline, whose default is60seconds (configurable independently).

The paired experiment starts each policy with an empty cache and uses identical fixed-seed/source/coverage observations: initial power availability, unchanged observation, unrelated seat/NPC state, actual power isolation, unchanged isolation and selected-target movement. Baseline regenerates at all six observations. Event/cache policy reuses only dependency-valid plans. A separate live causal world advances during waits, while identical observation snapshots and guarded execution copies keep the comparison inputs fixed. This tests provider/coordinator/cache accounting and executable support; it does not measure intervention effectiveness in a live training trial. The controlled supplied observer occlusion restricts visible context to the actor and source-placed training cabinet.

`results.json`, `paired-trace.json` and `planning-records.json` contain real C# provider responses, tokenizer usage, operation validation, cache invalidations, request/trace/model/config/source hashes and timing. `service-calls.jsonl` preserves raw backend requests/responses, including unsuccessful exploratory runs. Historical measured runs are preserved under `history` before replacement. The model sometimes returns two supported actions despite a one-step instruction; instruction compliance is reported separately from guarded action support.

`run_evaluator.py` exercises the actual C# `TypeSafeEvaluator` transport and Choice parsing against the **local model adapter**. `local-choice-evaluation.json` is not a call to TypeSafe JEV and establishes neither its quality nor its cost. Live JEV status remains `notcalled`. Local API cost is0; electricity, hardware and paid-provider savings are not measured.

Stop only this lane's processes after use:

```sh
python3 workers/simulator/model_service/stop.py
```

The stop helper validates each recorded PID's command before signaling it; it never touches Unity or other services.

Primary implementation sources:
- https://github.com/ml-explore/mlx-lm/blob/main/mlx_lm/SERVER.md
- https://huggingface.co/mlx-community/Qwen2.5-1.5B-Instruct-4bit
- https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct

## Whole-world initial generation

The native context exporter is `initial_world.py`. It reads the original Unity tape through C#/Newtonsoft, binds the exact world digest, and retains all entities, parent/link relations, rounded states and visual mappings. The compressed prefix/group/column representation is validated by exact decode comparison; output entity references resolve to original source IDs before the existing typed atomic validator. Unsupported capabilities and direct incident patches remain forbidden.

For the source-bound961-entity/456-visual fixture, actual cachedQwen tokenization measured102003→48633input tokens, a52.32% reduction. This still exceeds that model's32768context. Do not truncate the graph or claim whole-world initial generation on this local model. The measured9call planning benchmark uses smaller local observations; seeded simulation works without model calls. A compatible end-user subscription model can supply one explicit initial-world response.

`initial_world.py apply` consumes an already recorded sanitized Chat-shaped completion, performs native initial-state validation/application, and verifies40causal ticks of offline replay with zero network calls. It never authenticates or sends an inference request. Provider-runtime acceptance and native state/replay acceptance remain separate receipts.
