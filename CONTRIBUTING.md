# Contributing

## Git Flow

| Branch | Start from | Pull request target |
|---|---|---|
| `feature/<issue>-<slug>` | `develop` | `develop` |
| `bugfix/<issue>-<slug>` | `develop` | `develop` |
| `chore/<issue>-<slug>` | `develop` | `develop` |
| `docs/<issue>-<slug>` | `develop` | `develop` |
| `experiment/<slug>` | `develop` | `develop` |
| `release/<semver>` | `develop` | `main` |
| `hotfix/<semver>` | `main` | `main`, then back-merge to `develop` |

Feature branches use squash merge. Release and hotfix branches use merge commits and annotated `vX.Y.Z` tags.

## Pull requests

- Use a Conventional Commit title, for example `feat(xr): add emergency button interaction`.
- Link an issue with `Closes #123`, `Fixes #123`, or `Refs #123`.
- Complete test and data-safety sections in the PR template.
- Do not self-approve.
- Ordinary changes need one peer approval. Critical paths additionally require lead review through CODEOWNERS.
