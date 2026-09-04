# CHOOguard
(Comprehensive Hazard Operational Optimizer Guard)

`CHOOguard` is XR Lab's camera-to-3D railway emergency-response training platform.

## Target architecture

Approved camera capture → DA3-1.1 reconstruction → SceneBundle → Open3D collision proxy → Unity OpenXR PC VR training.

## Development model

- Git Flow: `feature/*`, `bugfix/*`, and `chore/*` merge into `develop`.
- Releases: `release/*` and `hotfix/*` merge into `main`.
- No direct pushes to `main` or `develop`.
- Runtime scoring is deterministic and checklist-based. LLMs assist development only.

See [CONTRIBUTING.md](CONTRIBUTING.md), [AGENTS.md](AGENTS.md), and [docs/ci.md](docs/ci.md).

## Data policy

Raw railway captures, model weights, Unity license files, secrets, and unapproved reconstructed assets must never be committed. Store only approved, de-identified samples and manifests.

This repository is publicly visible but does not yet grant an open-source license. See [NOTICE.md](NOTICE.md).
