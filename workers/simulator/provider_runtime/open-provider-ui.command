#!/bin/zsh
set -e
runtime_directory="${0:A:h}"
exec /usr/bin/env python3 "$runtime_directory/session.py" open
