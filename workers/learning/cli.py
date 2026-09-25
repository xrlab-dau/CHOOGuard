"""Train/evaluate isolated policies; artifacts never replace the live game's policy."""
from __future__ import annotations

import argparse
import hashlib
import importlib.metadata
import json
import statistics
import time
from pathlib import Path
from typing import Any

import numpy as np

from environment import GameplayEnv, HostError, load_config


def save_json(path: Path, value: Any) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n",
                    encoding="utf-8")


def host_command(config: dict[str, Any], root: Path) -> list[str]:
    return [part.replace("{root}", str(root)) for part in config["host_command"]]


def provenance(config: dict[str, Any], root: Path) -> dict[str, Any]:
    sources = [root / item for item in config["provenance_files"]]
    return {
        "scope": "authored-training-geometry; not PhysX/FDS or railway safety certification",
        "background_policy": "fixed-research-baseline-only",
        "environment_policy": "deterministic-current-grounded-transition-baseline",
        "packages": {name: importlib.metadata.version(name) for name in
                     ("gymnasium", "stable-baselines3", "torch", "numpy")},
        "sha256": {str(path.relative_to(root)): hashlib.sha256(path.read_bytes()).hexdigest()
                   for path in sources},
    }


def rule_action(reply: dict[str, Any]) -> int:
    # This is an explicitly named comparison policy, not a world model or hidden-state oracle.
    priority = {"Clean": 0, "PickUp": 1, "MoveTo": 2, "Observe": 3, "Wait": 4}
    candidates = reply["candidates"]
    if not candidates:
        return 0
    return min(range(len(candidates)), key=lambda index:
               (priority.get(candidates[index]["verb"], 5), index))


def evaluate(config: dict[str, Any], root: Path, output: Path, policy: str,
             weights: Path | None, replay: Path | None, memory: bool) -> dict[str, Any]:
    output.mkdir(parents=True, exist_ok=False)
    save_json(output / "config.json", config)
    save_json(output / "provenance.json", provenance(config, root))
    env = GameplayEnv(host_command(config, root), config["holdout_episodes"], memory=memory)
    model = None
    if policy == "ppo":
        if weights is None:
            raise ValueError("PPO evaluation requires --weights")
        from stable_baselines3 import PPO
        model = PPO.load(weights, device="cpu")
    recorded: dict[tuple[str, int], dict[str, Any]] = {}
    if policy == "jev-replay":
        if replay is None:
            raise ValueError("Recorded JEV replay requires --replay choices.jsonl")
        for line in replay.read_text(encoding="utf-8").splitlines():
            row = json.loads(line)
            if row["mode"] != "jev-fresh" or row["inference"]["status"] != "selected":
                continue
            key = (row["episode"], row["step"])
            if key in recorded:
                raise ValueError("Ambiguous duplicate recorded decision")
            recorded[key] = row
    outcomes: list[dict[str, Any]] = []
    latencies: list[float] = []
    random = np.random.default_rng(config["algorithm_seed"])
    try:
        with (output / "choices.jsonl").open("w", encoding="utf-8") as choices, \
                (output / "failure-traces.jsonl").open("w", encoding="utf-8") as failures:
            for episode in config["holdout_episodes"]:
                observation, _ = env.reset(seed=episode["seed"], options={"episode": episode})
                total_reward = 0.0
                trajectory: list[dict[str, Any]] = []
                status = "running"
                start = time.perf_counter()
                info = env.last["info"]
                for step in range(episode["max_steps"]):
                    fingerprint = env.decision_fingerprint()
                    inference: dict[str, Any] = {"status": "not_requested"}
                    before = time.perf_counter()
                    if policy == "ppo":
                        assert model is not None
                        selected, _ = model.predict(observation, deterministic=True)
                        action = int(selected)
                    elif policy == "rule":
                        action = rule_action(env.last)
                    elif policy == "random":
                        available = np.flatnonzero(observation["available"])
                        action = int(random.choice(available)) if len(available) else 0
                    elif policy == "jev-fresh":
                        action, inference = env.select_fresh_jev()
                    else:
                        record = recorded.get((episode["id"], step))
                        if record is None or record["fingerprint"] != fingerprint:
                            status = "replay_unavailable_or_diverged"
                            trajectory.append({"step": step, "status": status,
                                               "fingerprint": fingerprint})
                            break
                        action, inference = record["action"], record["inference"]
                    latency = (time.perf_counter() - before) * 1000
                    latencies.append(latency)
                    decision = {"mode": policy, "episode": episode["id"], "step": step,
                                "fingerprint": fingerprint, "action": action,
                                "inference": inference, "decision_ms": latency,
                                "candidates": env.last["candidates"]}
                    choices.write(json.dumps(decision, ensure_ascii=False) + "\n")
                    if action is None:
                        status = "remote_unavailable"
                        trajectory.append(decision)
                        break  # No fabricated fallback counted as a JEV evaluation.
                    observation, reward, terminated, truncated, info = env.step(action)
                    total_reward += reward
                    decision.update(reward=reward, receipt=info["receipt"],
                                    terminated=terminated, truncated=truncated)
                    trajectory.append(decision)
                    if terminated or truncated:
                        status = info["outcome"]
                        break
                outcome = {"episode": episode["id"], "policy": policy, "memory": memory,
                           "status": status, "success": status == "task_completed",
                           "return": total_reward, "steps": len(trajectory),
                           "invalid_actions": info.get("invalid_actions", 0),
                           "blocked_actions": info.get("blocked_actions", 0),
                           "simulated_seconds": info.get("simulated_seconds", 0),
                           "environment_transitions": info.get("environment_transitions", 0),
                           "journal_run": info.get("journal_run"),
                           "wall_seconds": time.perf_counter() - start}
                outcomes.append(outcome)
                if not outcome["success"]:
                    failures.write(json.dumps({"outcome": outcome, "trace": trajectory},
                                              ensure_ascii=False) + "\n")
    finally:
        env.close()
    summary = {"policy": policy, "memory": memory, "episodes": outcomes,
               "success_rate": statistics.mean(row["success"] for row in outcomes),
               "mean_return": statistics.mean(row["return"] for row in outcomes),
               "decision_ms_p95": float(np.percentile(latencies, 95)) if latencies else None,
               "comparative_claim": "No algorithm superiority or live-world transfer is inferred."}
    save_json(output / "metrics.json", summary)
    return summary


def train(config: dict[str, Any], root: Path, output: Path, timesteps: int) -> None:
    import torch
    from stable_baselines3 import PPO
    from stable_baselines3.common.callbacks import BaseCallback
    from stable_baselines3.common.monitor import Monitor

    torch.set_num_threads(1)
    output.mkdir(parents=True, exist_ok=False)
    save_json(output / "config.json", config)
    save_json(output / "provenance.json", provenance(config, root))
    outcomes: list[dict[str, Any]] = []

    class RecordOutcomes(BaseCallback):
        def _on_step(self) -> bool:
            for done, info in zip(self.locals["dones"], self.locals["infos"]):
                if done:
                    outcomes.append({key: info[key] for key in
                                     ("outcome", "invalid_actions", "blocked_actions",
                                      "simulated_seconds", "environment_transitions", "journal_run", "episode")})
            return True

    env = Monitor(GameplayEnv(host_command(config, root), config["train_episodes"]),
                  str(output / "train-monitor.csv"))
    settings = config["ppo"]
    model = PPO("MultiInputPolicy", env, seed=config["algorithm_seed"], device="cpu",
                verbose=1, **settings)
    started = time.perf_counter()
    try:
        model.learn(total_timesteps=timesteps, callback=RecordOutcomes())
        model.save(output / "policy")
        save_json(output / "training-metrics.json", {
            "requested_timesteps": timesteps, "actual_timesteps": model.num_timesteps,
            "wall_seconds": time.perf_counter() - started, "outcomes": outcomes})
    finally:
        env.close()
    evaluate(config, root, output / "holdout", "ppo", output / "policy.zip", None, True)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("train", "evaluate", "compare"))
    parser.add_argument("--config", type=Path, default=Path(__file__).with_name("experiment.json"))
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--timesteps", type=int, default=4096)
    parser.add_argument("--policy", choices=("rule", "random", "ppo", "jev-fresh", "jev-replay"), default="rule")
    parser.add_argument("--weights", type=Path)
    parser.add_argument("--replay", type=Path)
    parser.add_argument("--without-memory", action="store_true")
    parser.add_argument("--include-fresh-jev", action="store_true")
    args = parser.parse_args()
    config = load_config(args.config)
    root = Path(__file__).resolve().parents[2]
    if args.command == "train":
        if args.timesteps <= 0:
            parser.error("--timesteps must be positive")
        train(config, root, args.output, args.timesteps)
    elif args.command == "evaluate":
        summary = evaluate(config, root, args.output, args.policy, args.weights,
                           args.replay, not args.without_memory)
        print(json.dumps(summary, ensure_ascii=False))
    else:
        if args.weights is None:
            parser.error("compare requires actual trained --weights")
        args.output.mkdir(parents=True, exist_ok=False)
        rows = []
        for policy in ("rule", "random", "ppo"):
            for memory in (True, False):
                name = policy + ("-memory" if memory else "-no-memory")
                rows.append(evaluate(config, root, args.output / name, policy,
                                     args.weights, None, memory))
        if args.include_fresh_jev:
            fresh = args.output / "jev-fresh"
            rows.append(evaluate(config, root, fresh, "jev-fresh", None, None, True))
            rows.append(evaluate(config, root, args.output / "jev-replay", "jev-replay",
                                 None, fresh / "choices.jsonl", True))
        save_json(args.output / "comparison.json", {
            "results": rows,
            "jev_evidence": "fresh-and-recorded-separated" if args.include_fresh_jev else "NOT_RUN",
            "limitations": "Authored geometry and fixed research backgrounds; no live comparative superiority claim."})


if __name__ == "__main__":
    main()
