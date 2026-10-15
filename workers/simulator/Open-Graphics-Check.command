#!/bin/zsh
set -e
SIMULATOR_TASK_DIR="$(cd -- "$(dirname -- "$0")" && pwd)"
exec python3 "$SIMULATOR_TASK_DIR/launch_app.py" --graphics-proof
