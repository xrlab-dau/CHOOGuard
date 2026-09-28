#!/usr/bin/env bash
# Unity editor operations for the macOS, Windows (Git Bash) and Linux CI lanes.
#
#   unity_ci.sh activate            serial activation (UNITY_SERIAL, UNITY_EMAIL, UNITY_PASSWORD)
#   unity_ci.sh test EditMode|PlayMode
#   unity_ci.sh build               ChooGuard.Editor.PlayerBuild.BuildAll: macOS, Windows and Linux players, the same
#                                   entry points as the editor's ChooGuard/Build menu
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
case "${RUNNER_OS:-$(uname -s)}" in
  macOS|Darwin)
    serial_licence="/Library/Application Support/Unity/Unity_lic.ulf"
    named_licence="$HOME/Library/Unity/licenses/UnityEntitlementLicense.xml" ;;
  Windows|MINGW*|MSYS*)
    serial_licence="${PROGRAMDATA:-C:/ProgramData}/Unity/Unity_lic.ulf"
    named_licence="${LOCALAPPDATA:-}/Unity/licenses/UnityEntitlementLicense.xml" ;;
  *)
    serial_licence="$HOME/.local/share/unity3d/Unity/Unity_lic.ulf"
    named_licence="$HOME/.config/unity3d/Unity/licenses/UnityEntitlementLicense.xml" ;;
esac
py=$(command -v python3 || command -v python)

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
  # EditMode needs no GPU. PlayMode keeps a real device (Metal, Direct3D/WARP, OpenGL on a virtual X display) so
  # rendering paths run as they do on the team's machines.
  runner=()
  if [[ $platform == EditMode ]]; then
    args+=(-nographics)
  elif [[ ${RUNNER_OS:-} == Linux ]]; then
    runner=(xvfb-run -a -s "-screen 0 1920x1080x24")
  fi
  status=0
  ${runner[@]+"${runner[@]}"} "$UNITY_EDITOR" "${args[@]}" || status=$?
  echo "exit_code=$status" >> "${GITHUB_OUTPUT:-/dev/null}"
  echo "Unity $platform exited $status (verdict comes from unity_results.py)"
  ;;
build)
  status=0
  editor -quit -batchmode -projectPath "$project" -executeMethod ChooGuard.Editor.PlayerBuild.BuildAll \
    -logFile "$logs/build.log" || status=$?
  # PlayerBuild logs a verdict per target but never exits non-zero, so the markers and the outputs decide.
  failed=()
  for target in StandaloneOSX:Builds/macOS/CHOOGuard.app StandaloneWindows64:Builds/Windows/CHOOGuard.exe StandaloneLinux64:Builds/Linux/CHOOGuard.x86_64; do
    name=${target%%:*}; output=$project/${target#*:}
    if grep -q "CG_PLAYER_BUILD target=$name result=Succeeded" "$logs/build.log" && [[ -e $output ]]; then
      grep -m1 "CG_PLAYER_BUILD target=$name " "$logs/build.log"
    else
      failed+=("$name")
    fi
  done
  if [[ $status -ne 0 || ${#failed[@]} -gt 0 ]]; then
    grep -E 'CG_PLAYER_BUILD|error CS[0-9]+|Scripts have compiler errors' "$logs/build.log" | head -40 || true
    echo "::error title=Unity build::player build failed for ${failed[*]:-the editor} (editor exit $status); see the unity-build-logs artifact"
    exit 1
  fi
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
  # Windows: a Unity helper process can keep return.log open for a moment after the editor exits, and deleting it then
  # fails with "Device or resource busy" (run 36396147103). The licence is already returned and RUNNER_TEMP goes with
  # the job, so a directory that stays locked only earns a notice.
  for _ in {1..10}; do rm -rf "$private" 2>/dev/null && break; sleep 1; done
  [[ ! -e $private ]] || echo "::notice title=Unity licence::$private is still held open by a Unity process; the runner deletes it with the job"
  ;;
scrub)
  "$py" - "$logs" <<'PY'
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
