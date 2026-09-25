#!/usr/bin/env bash
set -euo pipefail

# UBA executes Bash under Cygwin on Windows. Native programs need native paths.
[[ "${CG_CLOUD_BUILD:-}" == "win-x64" && "${IS_BUILDER:-}" == "true" && "${BUILDER_OS:-}" == "WINDOWS" ]] || {
    printf '%s\n' 'This hook requires explicit CG_CLOUD_BUILD=win-x64 on a Windows UBA builder.' >&2
    exit 1
}
: "${PROJECT_DIRECTORY:?UBA project directory is required}"
: "${OUTPUT_DIRECTORY:?UBA output directory is required}"
: "${DEVOPS_ENV:?UBA environment propagation file is required}"
project_native=$(cygpath -wa "$PROJECT_DIRECTORY")
output_native=$(cygpath -wa "$OUTPUT_DIRECTORY")
env_native=$(cygpath -wa "$DEVOPS_ENV")
script_native=$(cygpath -wa "$PROJECT_DIRECTORY/workers/cloud_prepare.py")
python_command=python
if [[ -n "${CG_CLOUD_PYTHON:-}" ]]; then
    python_command=$(cygpath -u "$CG_CLOUD_PYTHON")
fi
"$python_command" -B "$script_native" --project-root "$project_native" --output-directory "$output_native" --env-file "$env_native"
