#!/bin/zsh
# FMP-07a required-check reproduction.
#
# Runs the three required suites in ONE Unity invocation with ONE semicolon-separated
# -testFilter. Unity's -testFilter accepts only ';' as the multi-suite separator; a comma
# silently runs zero tests, so never change that character.
#
#   usage: ./reproduce-FMP-07a.sh [OUT_DIR]      (default OUT_DIR = this script's directory)
#
# It refuses to run unless the two hash-pinned artifacts match the digests this work item
# pins, so a green result cannot be produced against different bytes.
set -u

OUT="${1:-${0:A:h}}"
REPO="$(git -C "${0:A:h}" rev-parse --show-toplevel)"
UNITY="${UNITY:-/Applications/Unity/Unity-6000.3.23f1/Unity.app/Contents/MacOS/Unity}"

FIXTURE="foundation/tests/FMP-07a/world-crowd-fixture.json"
TESTFILE="Packages/com.xrlab.chooguard.foundation/Multiplayer/Tests/Editor/Fmp07aWorldCrowdTests.cs"
FIXTURE_SHA="d3beba2e8f1365e9844487d841dcfc7a242c9a43fb3db9ed75b0343e919a3aff"
TESTFILE_SHA="a36021ede6feab5941e7501cc880ad1f192033bb3d078a7ce14c78e5062848ca"
FILTER="ChooGuard.Foundation.Multiplayer.Tests.WorldMotionAdapterTests;ChooGuard.Foundation.Tests.CrowdWorldContactTests;ChooGuard.Foundation.Multiplayer.Tests.Fmp07aWorldCrowdTests"

cd "$REPO" || exit 90
mkdir -p "$OUT" || exit 90

got_fixture="$(shasum -a 256 "$FIXTURE" | awk '{print $1}')"
got_testfile="$(shasum -a 256 "$TESTFILE" | awk '{print $1}')"
if [ "$got_fixture" != "$FIXTURE_SHA" ] || [ "$got_testfile" != "$TESTFILE_SHA" ]; then
  echo "REFUSING: the working tree is not at the pinned FMP-07a bytes." >&2
  echo "  fixture  want $FIXTURE_SHA" >&2
  echo "           got  $got_fixture" >&2
  echo "  test     want $TESTFILE_SHA" >&2
  echo "           got  $got_testfile" >&2
  echo "restore with: cp docs/evidence/work-items/FMP-07a/adopted/\$(basename <path>) <path>" >&2
  exit 91
fi

{
  echo "cwd=$REPO"
  echo "host=$(uname -a)"
  echo "date_utc_start=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "git_head=$(git rev-parse HEAD)"
  echo "git_branch=$(git rev-parse --abbrev-ref HEAD)"
  echo "unity=$UNITY"
  echo "fixture_sha256_pre=$got_fixture"
  echo "test_sha256_pre=$got_testfile"
} > "$OUT/host.txt"

"$UNITY" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
  -testFilter "$FILTER" \
  -testResults "$OUT/FMP-07a-edit.xml" \
  -logFile "$OUT/FMP-07a-unity.log"
echo "unity_exit=$?" >> "$OUT/host.txt"

{
  echo "date_utc_end=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "fixture_sha256_post=$(shasum -a 256 "$FIXTURE" | awk '{print $1}')"
  echo "test_sha256_post=$(shasum -a 256 "$TESTFILE" | awk '{print $1}')"
} >> "$OUT/host.txt"

# The run writes its receipt under Temp/, which the editor removes on exit, so the receipt
# bytes are recovered from the FMP07A-RECEIPT log line instead.
python3 - "$OUT/FMP-07a-unity.log" "$OUT/fmp07a-world-crowd.json" <<'PY'
import io, sys
marker = 'FMP07A-RECEIPT '
line = None
for raw in io.open(sys.argv[1], encoding='utf-8', errors='replace'):
    at = raw.find(marker)
    if at >= 0:
        rest = raw[at + len(marker):].strip()
        line = rest.split(' ', 1)[1]
        break
if line is None:
    sys.exit('no FMP07A-RECEIPT line in the log')
io.open(sys.argv[2], 'w', encoding='utf-8').write(line)
print('receipt bytes written:', len(line.encode('utf-8')))
PY

python3 - "$OUT/FMP-07a-edit.xml" <<'PY'
import sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
print('root result=%s total=%s passed=%s failed=%s inconclusive=%s skipped=%s' % (
    root.get('result'), root.get('total'), root.get('passed'), root.get('failed'),
    root.get('inconclusive'), root.get('skipped')))
if root.get('result') != 'Passed' or root.get('failed') != '0' or root.get('skipped') != '0':
    sys.exit('FMP-07a gate: the invocation is not a clean pass')
PY
echo "DONE"
