#!/bin/zsh
set -eu
SIMULATOR_DIRECTORY="${0:A:h}"
exec python3 "$SIMULATOR_DIRECTORY/launch_app.py"
