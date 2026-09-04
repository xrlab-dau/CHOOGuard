# Git Flow policy

`develop` is the default integration branch. Ordinary work starts from it and returns through a pull request. `main` contains reviewed release history only.

- Squash feature, bugfix, chore, docs, test, and refactor PRs into `develop`.
- Merge release and hotfix PRs into `main` with merge commits.
- Tag releases as `vMAJOR.MINOR.PATCH`.
- Back-merge every release and hotfix into `develop`.
- Never force-push or delete `main` and `develop`.
