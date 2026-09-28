#!/usr/bin/env bash
# Unity editor operations for the macOS CI lane.
#
#   unity_ci.sh activate            serial activation (UNITY_SERIAL, UNITY_EMAIL, UNITY_PASSWORD)
#   unity_ci.sh test EditMode|PlayMode
#   unity_ci.sh build               ChooGuard.Editor.PlayerBuild.BuildMac, same entry as local builds
#   unity_ci.sh return              give the activation back to the seat (run with if: always())
#   unity_ci.sh scrub               remove credentials from logs before they are uploaded
#
# Unity writes its full command line, including -serial and -password, at the top of every log. Job logs are
# masked by GitHub, uploaded artifacts are not, so licence logs stay in a private directory that is never
# uploaded and every uploadable log is scrubbed first.
set -euo pipefail

usage() { echo "usage: $0 activate | test EditMode|PlayMode | build | return | scrub" >&2; exit 64; }
[[ $# -ge 1 ]] || usage
: "${RUNNER_TEMP:?}"
logs="${UNITY_LOGS:-$RUNNER_TEMP/unity}"           # uploaded as an artifact after `scrub`
private="$RUNNER_TEMP/unity-licence"               # never uploaded
mkdir -p "$logs" "$private"
project="${UNITY_PROJECT:-${GITHUB_WORKSPACE:-$PWD}}"
serial_licence="/Library/Application Support/Unity/Unity_lic.ulf"
named_licence="$HOME/Library/Unity/licenses/UnityEntitlementLicense.xml"

editor() { : "${UNITY_EDITOR:?path to the Unity executable}"; "$UNITY_EDITOR" "$@"; }

case "$1" in
activate)
  : "${UNITY_SERIAL:?}" "${UNITY_EMAIL:?}" "${UNITY_PASSWORD:?}"
  status=0
  editor -quit -batchmode -nographics -serial "$UNITY_SERIAL" -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
    -logFile "$private/activate.log" || status=$?
  if [[ $status -eq 0 ]] && [[ -s $serial_licence || -s $named_licence ]]; then
    echo "Unity licence activated"
    exit 0
  fi
  reason="activation failed (editor exit $status)"
  if grep -qiE 'maximum number of activations|activation limit|reached .*limit' "$private/activate.log"; then
    reason="the seat has no free activation. Return one at id.unity.com > My Account > My Seats (the maintainer's Mac holds one, CI needs the other)"
  elif grep -qiE 'invalid serial|serial .*(not valid|invalid)' "$private/activate.log"; then
    reason="UNITY_SERIAL was rejected. Use the Student-plan licence key from Unity's email"
  elif grep -qiE 'credentials|password|log ?in failed|unauthori' "$private/activate.log"; then
    reason="UNITY_EMAIL / UNITY_PASSWORD were rejected"
  fi
  grep -iE 'licens|entitlement|activation' "$private/activate.log" | grep -viE 'password|serial' | tail -20 || true
  echo "::error title=Unity licence::$reason"
  exit 1
  ;;
test)
  platform="${2:-}"
  [[ $platform == EditMode || $platform == PlayMode ]] || usage
  args=(-batchmode -projectPath "$project" -runTests -testPlatform "$platform"
        -testResults "$logs/$platform-results.xml" -logFile "$logs/$platform.log")
  # EditMode needs no GPU; PlayMode keeps Metal so rendering paths run as they do on the team's Macs.
  [[ $platform == EditMode ]] && args+=(-nographics)
  status=0
  editor "${args[@]}" || status=$?
  echo "exit_code=$status" >> "${GITHUB_OUTPUT:-/dev/null}"
  echo "Unity $platform exited $status (verdict comes from unity_results.py)"
  ;;
build)
  status=0
  editor -quit -batchmode -projectPath "$project" -executeMethod ChooGuard.Editor.PlayerBuild.BuildMac \
    -logFile "$logs/build.log" || status=$?
  # PlayerBuild logs its verdict but never exits non-zero, so the marker and the bundle decide.
  if [[ $status -ne 0 ]] || ! grep -q 'CG_PLAYER_BUILD result=Succeeded' "$logs/build.log" || [[ ! -d $project/Builds/macOS/CHOOGuard.app ]]; then
    grep -E 'CG_PLAYER_BUILD|error CS[0-9]+|Scripts have compiler errors' "$logs/build.log" | head -40 || true
    echo "::error title=Unity build::macOS player build failed (editor exit $status); see the unity-build-logs artifact"
    exit 1
  fi
  grep -m1 'CG_PLAYER_BUILD result=' "$logs/build.log"
  ;;
return)
  : "${UNITY_EMAIL:?}" "${UNITY_PASSWORD:?}"
  status=0
  editor -quit -batchmode -nographics -returnlicense -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
    -logFile "$private/return.log" || status=$?
  if [[ $status -ne 0 ]]; then
    echo "::warning title=Unity licence::licence return failed (editor exit $status). If the next run finds no free activation, return it at id.unity.com > My Account > My Seats"
  else
    echo "Unity licence returned"
  fi
  rm -rf "$private"
  ;;
scrub)
  python3 - "$logs" <<'PY'
import os, pathlib, re, sys
secrets = [os.environ.get(k, "") for k in ("UNITY_SERIAL", "UNITY_EMAIL", "UNITY_PASSWORD")]
serial_like = re.compile(rb"\b[A-Z0-9]{2}-[A-Z0-9*]{4}-[A-Z0-9*]{4}-[A-Z0-9*]{4}-[A-Z0-9*]{4}-[A-Z0-9*]{4}\b")
for path in pathlib.Path(sys.argv[1]).rglob("*.log"):
    data = path.read_bytes()
    for secret in filter(None, secrets):
        data = data.replace(secret.encode(), b"***")
    path.write_bytes(serial_like.sub(b"**-****-****-****-****-****", data))
PY
  ;;
*) usage ;;
esac
