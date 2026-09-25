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
probe_python() {
    printf 'Checking cloud packaging interpreter: %s\n' "$*"
    "$@" -B "$script_native" --check-python
}

python_command=()
if [[ -n "${CG_CLOUD_PYTHON:-}" ]]; then
    # An explicit override must work; never silently replace the requested interpreter.
    python_command=("$(cygpath -u "$CG_CLOUD_PYTHON")")
    probe_python "${python_command[@]}"
elif command -v py >/dev/null 2>&1 && probe_python py -3; then
    python_command=(py -3)
else
    # Inspect every PATH match, not just a Cygwin Python shadowing native Python.
    while IFS= read -r candidate; do
        if probe_python "$candidate"; then
            python_command=("$candidate")
            break
        fi
    done < <(type -aP python python3 || :)
    if [[ ${#python_command[@]} -eq 0 ]]; then
        bootstrap_native=$(cygpath -wa "$PROJECT_DIRECTORY/workers/cloud-bootstrap-python.ps1")
        python_native=$(powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$bootstrap_native" -ProjectRoot "$project_native")
        python_command=("$(cygpath -u "${python_native%$'\r'}")")
        probe_python "${python_command[@]}"
    fi
fi
"${python_command[@]}" -B "$script_native" --project-root "$project_native" --output-directory "$output_native" --env-file "$env_native"
