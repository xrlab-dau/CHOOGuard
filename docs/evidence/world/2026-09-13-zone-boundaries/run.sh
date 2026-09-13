#!/bin/sh
# Issue #70 candidate B — reproduce the zone-boundary evidence bundle.
#
#   sh docs/evidence/world/2026-09-13-zone-boundaries/run.sh
#
# Writes <name>.stdout / <name>.stderr / <name>.exit for every command, plus
# commands.tsv, hashes-before.txt, hashes-after.txt, SHA256SUMS.txt and receipt.json.
# It only writes inside this evidence directory; the four candidate artifacts and the
# read-only upstream coverage contract are hashed before and after to show the run
# did not change them.
set -u
ROOT=$(cd "$(dirname "$0")/../../../.." && pwd)
OUT=docs/evidence/world/2026-09-13-zone-boundaries
cd "$ROOT" || exit 1

ARTIFACTS="foundation/world/zone-boundaries.json scripts/art/validate_zone_boundaries.py scripts/art/test_zone_boundaries.py docs/art/zone-boundaries.md"
UPSTREAM=foundation/world/facility-coverage.json

python3 - "$OUT" <<'PY'
import pathlib, sys
fixtures = pathlib.Path(sys.argv[1]) / 'fixtures'
fixtures.mkdir(parents=True, exist_ok=True)
(fixtures / 'array.json').write_bytes(b'[1,2,3]')
(fixtures / 'truncated.json').write_bytes(b'{"schema_version":"1.0"')
(fixtures / 'non-utf8.json').write_bytes(b'{"schema_version":"\xff\xfe"}')
(fixtures / 'absent.json').unlink(missing_ok=True)
PY

: > "$OUT/hashes-before.txt"
for path in $ARTIFACTS $UPSTREAM; do shasum -a 256 "$path" >> "$OUT/hashes-before.txt"; done

: > "$OUT/commands.tsv"
record() {
  name=$1; command=$2
  sh -c "$command" > "$OUT/$name.stdout" 2> "$OUT/$name.stderr"
  code=$?
  printf '%s\n' "$code" > "$OUT/$name.exit"
  printf '%s\t%s\t%s\n' "$name" "$command" "$code" >> "$OUT/commands.tsv"
  printf '  %-20s exit=%s\n' "$name" "$code"
}

echo "== running (cwd=$ROOT) =="
record validate_default 'python3 scripts/art/validate_zone_boundaries.py'
record validate_array "python3 scripts/art/validate_zone_boundaries.py --data $OUT/fixtures/array.json"
record validate_missing "python3 scripts/art/validate_zone_boundaries.py --data $OUT/fixtures/absent.json"
record validate_truncated "python3 scripts/art/validate_zone_boundaries.py --data $OUT/fixtures/truncated.json"
record validate_non_utf8 "python3 scripts/art/validate_zone_boundaries.py --data $OUT/fixtures/non-utf8.json"
record tests 'python3 -m unittest scripts.art.test_zone_boundaries -v'
record discover "python3 -m unittest discover -s scripts/art -p 'test_*.py'"
record counts 'python3 -c "import json;c=json.load(open(\"foundation/world/facility-coverage.json\",encoding=\"utf-8\"));b=json.load(open(\"foundation/world/zone-boundaries.json\",encoding=\"utf-8\"));print(\"coverage: zones=%d connections=%d constraints=%d\"%(len(c[\"zones\"]),len(c[\"connections\"]),len(c[\"numeric_constraints\"])));print(\"boundaries: bundles=%d zones=%d portals=%d cross_bundle=%d questions=%d\"%(len(b[\"bundles\"]),len(b[\"zones\"]),len(b[\"portals\"]),sum(1 for p in b[\"portals\"] if p[\"crosses_bundles\"]),len(b[\"open_questions\"])));print(\"portals_with_dimensions=%d\"%sum(1 for p in b[\"portals\"] if p[\"opening_width_m\"] is not None or p[\"clear_height_m\"] is not None))"'
record binding 'python3 -c "import hashlib,json;r=json.load(open(\"foundation/world/zone-boundaries.json\",encoding=\"utf-8\"))[\"coverage_source\"][\"sha256\"];a=hashlib.sha256(open(\"foundation/world/facility-coverage.json\",\"rb\").read()).hexdigest();print(\"recorded=\"+r);print(\"actual  =\"+a);print(\"match   =%s\"%(r==a))"'
record upstream_unchanged "shasum -a 256 $UPSTREAM"

: > "$OUT/hashes-after.txt"
for path in $ARTIFACTS $UPSTREAM; do shasum -a 256 "$path" >> "$OUT/hashes-after.txt"; done

shasum -a 256 $ARTIFACTS > "$OUT/SHA256SUMS.txt"

python3 - "$OUT" "$(git rev-parse HEAD)" <<'PY'
import json, pathlib, re, sys
out = pathlib.Path(sys.argv[1])
head = sys.argv[2]
rows = [line.split('\t') for line in (out / 'commands.tsv').read_text().splitlines() if line.strip()]

def lines_of(name, suffix):
    path = out / (name + suffix)
    return path.read_text(errors='replace').splitlines() if path.exists() else []

def summary(name):
    text = [l.strip() for l in lines_of(name, '.stdout') + lines_of(name, '.stderr') if l.strip()]
    if name in ('counts', 'binding'):
        return ' | '.join(text)
    hits = [l for l in text if '"result"' in l or l.startswith('Ran ') or l.startswith('OK') or l.startswith('FAILED')]
    return ' | '.join(hits[:3]) if hits else (text[0] if text else '(no output)')

def hashes(path):
    rows = []
    for line in (out / path).read_text().splitlines():
        fields = line.split(None, 1)
        if len(fields) == 2 and re.fullmatch(r'[0-9a-f]{64}', fields[0]):
            rows.append((fields[1].strip(), fields[0]))
    return rows
before, after = hashes('hashes-before.txt'), hashes('hashes-after.txt')
receipt = {
    'issue': 70,
    'candidate': 'B',
    'base_commit': head,
    'generated_by': 'run.sh',
    'mode': 'isolated_proposal',
    'canonicalWriteAllowed': False,
    'policy_pass_claimed': False,
    'note': 'Exit codes and outputs below are evidence, not a policy PASS or an acceptance decision.',
    'commands': [{'name': n, 'command': c, 'exit': int(e),
                  'stdout': '%s.stdout' % n, 'stderr': '%s.stderr' % n, 'exit_file': '%s.exit' % n,
                  'result_line': summary(n)} for n, c, e in rows],
    'targets_unchanged_by_run': before == after,
    'artifact_sha256': {p: h for p, h in hashes('SHA256SUMS.txt')},
    'not_run': [
        'claim holder remote branch docs/70-zone-coverage-boundaries — deliberately not consulted (blind comparison)',
        '#82 local unpushed implementation ID reconciliation — not reachable from this worktree',
        'V02/V03 video zone attribution — requires human visual review',
        'surveyed placement / facility fidelity / safety acceptance — outside this candidate scope, no evidence',
        'GitHub issue/project state re-read — this candidate only touches repository files',
    ],
}
(out / 'receipt.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

if before == after:
    print('hashes-before == hashes-after: targets unchanged by the run')
else:
    print('WARNING: targets changed during the run')
    for b, a in zip(before, after):
        if b != a:
            print('  changed: %s' % (a,))
print('receipt.json written (%d commands)' % len(rows))
PY
