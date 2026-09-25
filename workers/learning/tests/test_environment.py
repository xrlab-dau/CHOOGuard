"""Integration regressions: require the compiled shared-kernel host, never mock dynamics."""
from __future__ import annotations

import json
from pathlib import Path

import gymnasium as gym
import numpy as np
import pytest

from cli import host_command, rule_action
from environment import GameplayEnv, load_config

ROOT = Path(__file__).resolve().parents[3]
CONFIG = Path(__file__).resolve().parents[1] / "experiment.json"


@pytest.fixture
def env(tmp_path: Path):
    config = load_config(CONFIG)
    command = host_command(config, ROOT)
    command[-1] = str(tmp_path / "journals")
    instance = GameplayEnv(command, config["train_episodes"])
    try:
        yield instance
    finally:
        instance.close()


def initial_episode() -> dict:
    return dict(load_config(CONFIG)["train_episodes"][0])


def finish(env: GameplayEnv) -> tuple[bool, bool, dict]:
    for _ in range(20):
        _, _, terminated, truncated, info = env.step(rule_action(env.last))
        if terminated or truncated:
            return terminated, truncated, info
    pytest.fail("Actual grounded pickup/contact skill never reached an episode boundary")


def test_hidden_fact_cannot_change_observation_or_forbidden_action_result(env):
    episode = initial_episode()
    observations = []
    for value in (False, True):
        env.reset(seed=17, options={"episode": dict(episode, private_canary=value)})
        before = env.decision_fingerprint()
        observation, reward, terminated, truncated, info = env.step(31)
        info = {key: value for key, value in info.items() if key != "journal_run"}
        observations.append((before, env.decision_fingerprint(), reward, terminated, truncated, info))
        assert env.observation_space.contains(observation)
    assert observations[0] == observations[1]
    assert observations[0][-1]["invalid_actions"] == 1


def test_reset_restores_consumed_resources_custody_and_clock(env):
    episode = initial_episode()
    first, _ = env.reset(seed=19, options={"episode": episode})
    assert finish(env)[2]["success"] is True
    second, info = env.reset(seed=19, options={"episode": episode})
    for key in first:
        np.testing.assert_array_equal(first[key], second[key])
    assert info["simulated_seconds"] == 0
    assert info["invalid_actions"] == info["blocked_actions"] == 0
    # The second completion needs the restored supply and fresh pickup, not stale completion state.
    terminated, truncated, final = finish(env)
    assert terminated and not truncated and final["success"]


def test_step_limit_truncates_without_awarding_success(env):
    episode = dict(initial_episode(), max_steps=1)
    env.reset(options={"episode": episode})
    wait = next(index for index, item in enumerate(env.last["candidates"]) if item["verb"] == "Wait")
    _, reward, terminated, truncated, info = env.step(wait)
    assert truncated and not terminated
    assert not info["success"] and reward < 0
    with pytest.raises(gym.error.ResetNeeded):
        env.step(wait)


def test_time_horizon_prevents_late_skill_completion(env):
    episode = dict(initial_episode(), max_seconds=0.5)
    env.reset(options={"episode": episode})
    _, reward, terminated, truncated, info = env.step(rule_action(env.last))
    assert truncated and not terminated and not info["success"]
    assert info["simulated_seconds"] == 0.5
    assert reward <= 0


def test_completion_requires_authoritative_contact_work_and_consumption(env, tmp_path):
    env.reset(options={"episode": initial_episode()})
    terminated, truncated, info = finish(env)
    assert terminated and not truncated and info["success"]
    mutations = []
    for journal in (tmp_path / "journals").glob("*/mutations.jsonl"):
        mutations.extend(json.loads(line) for line in journal.read_text(encoding="utf-8").splitlines())
    # Serialized fields use the shared GameplayCodec's actual public property names.
    entities = [entity for record in mutations for entity in record["mutation"]["Entities"]]
    surface = [entity for entity in entities if entity["Id"] == "practice-surface"][-1]
    tool = [entity for entity in entities if entity["Id"] == "practice-cleaning-tool"][-1]
    assert surface["Facts"]["dirty"] == "FALSE"
    assert surface["Measurements"]["soil:left"]["Value"] == 0
    assert surface["Measurements"]["soil:right"]["Value"] == 0
    assert tool["Measurements"]["supply"]["Value"] < 0.001


def test_reordered_causal_rules_do_not_evade_holdout_isolation(tmp_path):
    config = load_config(CONFIG)
    config["train_episodes"][0]["rule_combination"] = ["rule-a", "rule-b"]
    config["holdout_episodes"][0]["rule_combination"] = ["rule-b", "rule-a"]
    path = tmp_path / "overlapping.json"
    path.write_text(json.dumps(config), encoding="utf-8")
    with pytest.raises(ValueError, match="rule_combination overlap"):
        load_config(path)
