"""Research-only Gymnasium adapter. All world dynamics execute in the C# host."""
from __future__ import annotations

import hashlib
import json
import queue
import subprocess
import threading
from pathlib import Path
from typing import Any

import gymnasium as gym
import numpy as np
from gymnasium import spaces

MAX_CANDIDATES = 32
CANDIDATE_FEATURES = 40


class HostError(RuntimeError):
    """The authoritative simulator did not return a usable transition."""


class GameplayEnv(gym.Env):
    metadata = {"render_modes": []}

    def __init__(self, command: list[str], episodes: list[dict[str, Any]], *,
                 memory: bool = True, timeout: float = 60.0) -> None:
        super().__init__()
        if not command or not episodes:
            raise ValueError("Host command and initial-condition specifications are required")
        self.command = command
        self.episodes = episodes
        self.memory = memory
        self.timeout = timeout
        self.action_space = spaces.Discrete(MAX_CANDIDATES)
        self.observation_space = spaces.Dict({
            "self": spaces.Box(-1.0, 1.0, (8,), dtype=np.float32),
            "candidates": spaces.Box(-1.0, 1.0,
                                     (MAX_CANDIDATES, CANDIDATE_FEATURES), dtype=np.float32),
            "available": spaces.MultiBinary(MAX_CANDIDATES),
        })
        self.process: subprocess.Popen[str] | None = None
        self.lines: queue.Queue[str | None] = queue.Queue()
        self.ended = True
        self.last: dict[str, Any] = {}

    def _start(self) -> None:
        if self.process is not None:
            return
        self.process = subprocess.Popen(self.command, stdin=subprocess.PIPE,
                                        stdout=subprocess.PIPE, text=True,
                                        encoding="utf-8", bufsize=1)
        stream = self.process.stdout
        assert stream is not None
        lines = self.lines

        def collect() -> None:
            try:
                for line in stream:
                    lines.put(line)
            finally:
                lines.put(None)

        threading.Thread(target=collect, daemon=True, name="learning-host-stdout").start()

    def request(self, message: dict[str, Any]) -> dict[str, Any]:
        self._start()
        assert self.process is not None and self.process.stdin is not None
        try:
            self.process.stdin.write(json.dumps(message, allow_nan=False) + "\n")
            self.process.stdin.flush()
            line = self.lines.get(timeout=self.timeout)
        except (BrokenPipeError, queue.Empty) as error:
            self.close()
            raise HostError("Simulator exited or exceeded its bounded response deadline") from error
        if line is None:
            self.close()
            raise HostError("Simulator closed stdout without a response")
        reply = json.loads(line)
        if not reply.get("ok"):
            raise HostError(reply.get("error", "Simulator rejected the request"))
        return reply

    @staticmethod
    def observation(reply: dict[str, Any]) -> dict[str, np.ndarray]:
        observed = reply["observation"]
        return {
            "self": np.asarray(observed["self"], dtype=np.float32),
            "candidates": np.asarray(observed["candidates"], dtype=np.float32),
            "available": np.asarray(observed["available"], dtype=np.int8),
        }

    def reset(self, *, seed: int | None = None,
              options: dict[str, Any] | None = None) -> tuple[dict[str, np.ndarray], dict[str, Any]]:
        super().reset(seed=seed)
        specification = ((options or {}).get("episode") or
                         self.episodes[int(self.np_random.integers(len(self.episodes)))])
        reply = self.request({"command": "reset", "episode": specification,
                              "memory": self.memory})
        self.ended = False
        self.last = reply
        return self.observation(reply), reply["info"]

    def step(self, action: int) -> tuple[dict[str, np.ndarray], float, bool, bool, dict[str, Any]]:
        if self.ended:
            raise gym.error.ResetNeeded("Call reset before stepping a finished episode")
        if not self.action_space.contains(action):
            raise ValueError("Action index is outside the declared finite action space")
        reply = self.request({"command": "step", "action": int(action)})
        self.last = reply
        terminated, truncated = bool(reply["terminated"]), bool(reply["truncated"])
        self.ended = terminated or truncated
        return (self.observation(reply), float(reply["reward"]), terminated,
                truncated, reply["info"])

    def select_fresh_jev(self) -> tuple[int | None, dict[str, Any]]:
        if self.ended:
            raise gym.error.ResetNeeded("No decision exists outside a running episode")
        reply = self.request({"command": "jev_decision"})
        return reply.get("action"), reply["inference"]

    def decision_fingerprint(self) -> str:
        # Contains only the controlled actor's observation/candidates, not hidden world truth.
        payload = {key: self.last[key] for key in ("observation", "candidates")}
        raw = json.dumps(payload, sort_keys=True, separators=(",", ":"), ensure_ascii=False)
        return hashlib.sha256(raw.encode("utf-8")).hexdigest()

    def close(self) -> None:
        process, self.process = self.process, None
        self.ended = True
        if process is None:
            return
        if process.stdin:
            process.stdin.close()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
        if process.stdout:
            process.stdout.close()
        self.lines = queue.Queue()


def load_config(path: Path) -> dict[str, Any]:
    config = json.loads(path.read_text(encoding="utf-8"))
    train, holdout = config["train_episodes"], config["holdout_episodes"]
    if not train or not holdout:
        raise ValueError("Training and held-out initial conditions must both exist")
    for field in ("seed", "placement", "intervention", "rule_combination"):
        def condition_key(episode: dict[str, Any]) -> str:
            value = episode[field]
            return json.dumps(sorted(value) if field == "rule_combination" else value)

        left = {condition_key(episode) for episode in train}
        right = {condition_key(episode) for episode in holdout}
        if left & right:
            raise ValueError(f"Train/holdout {field} overlap: {sorted(left & right)}")
    return config
