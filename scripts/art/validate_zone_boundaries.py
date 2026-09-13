#!/usr/bin/env python3
"""Check the 13-zone boundary draft against the facility coverage contract.

Exit-code contract (identical to the table in docs/art/zone-boundaries.md):

  0  pass     every rule was evaluated and held
  1  fail     the document is well typed but violates a rule (coverage hash drift,
              missing portal, adjacency mismatch, a dimension asserted without an
              operator-published basis, a question silently resolved, ...)
  2  not_run  the document could not be evaluated at all: missing/unreadable file,
              non-UTF-8 bytes, invalid or truncated JSON, a top-level JSON value that
              is not an object, or a document-level list field carrying the wrong JSON
              type. A not_run input is NOT a rule violation and is never reported as pass.

The ratable / not-ratable split is deliberate: `[1,2,3]`, `null`, a bare string and a
truncated file are *unratable*, not *violating*. Inner entry type mistakes (for example
a zone entry that is not an object) keep the document ratable and are reported as
violations, because the document-level rules can still be evaluated.

This checker never certifies surveyed placement, facility fidelity, safety or acceptance.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[2]
COVERAGE = 'foundation/world/facility-coverage.json'
BOUNDARIES = 'foundation/world/zone-boundaries.json'

EXIT_PASS = 0
EXIT_FAIL = 1
EXIT_NOT_RUN = 2

DOCUMENT_LISTS = {
    'zone-boundaries': ('sources', 'bundles', 'zones', 'portals', 'unassigned_sources', 'open_questions'),
    'facility-coverage': ('sources', 'zones', 'connections', 'numeric_constraints'),
}
ALLOWED_USES = frozenset({'form_reference', 'topology', 'floor_membership', 'historical_layout_reference',
                          'operator_published_dimension', 'quantity_context', 'non_metric_surface_review',
                          'candidate_unreviewed'})
MEASUREMENTS = frozenset({'operator_published', 'synthetic_estimate', 'none'})
LEVEL_CHANGES = frozenset({'vertical_unverified', 'unknown'})
PORTAL_DIMENSIONS = ('opening_width_m', 'clear_height_m')
PM_QUESTION_COUNT = 7
PM_CLASSIFICATIONS = frozenset({'answered', 'open', 'followup_issue'})
FORBIDDEN_SELF_CLAIMS = frozenset({'accepted', 'approved', 'complete', 'done', 'final', 'merged', 'verified'})
PROPOSAL_MODE = 'isolated_proposal'
ISSUE_REF = re.compile(r'#\d+')
SHA256 = re.compile(r'[0-9a-f]{64}')
COMMIT = re.compile(r'[0-9a-f]{40}')


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def resolve(path):
    candidate = Path(path)
    return candidate if candidate.is_absolute() else ROOT / candidate


def public_url(value):
    try:
        parsed = urlsplit(value)
        return parsed.scheme == 'https' and bool(parsed.netloc) and not parsed.username and not parsed.password
    except (TypeError, ValueError):
        return False


def text(value):
    return isinstance(value, str) and bool(value.strip())


def objects(value):
    return [item for item in value if isinstance(item, dict)] if isinstance(value, list) else []


def ratability(obj, label):
    """Reasons the document cannot be evaluated at all. Empty list means ratable."""
    reasons = []
    if not isinstance(obj, dict):
        reasons.append('%s: top-level JSON must be an object, got %s' % (label, type(obj).__name__))
        return reasons
    for key in DOCUMENT_LISTS[label]:
        if key in obj and not isinstance(obj[key], list):
            reasons.append('%s: %s must be a JSON array, got %s' % (label, key, type(obj[key]).__name__))
    return reasons


def empty_report(result, unratable=None, errors=None):
    return {'result': result, 'errors': list(errors or []), 'unratable': list(unratable or []),
            'zones': 0, 'portals': 0, 'crossBundlePortals': 0, 'metricVerifiedZones': 0,
            'pmQuestions': 0, 'pmQuestionsClassified': 0, 'pmQuestionsAnswered': 0,
            'pmQuestionsOpen': 0, 'pmQuestionsFollowup': 0, 'openQuestions': 0,
            'facilityFidelityValidated': False}


def published_portal_dimensions(coverage, portal):
    """Operator-published opening values that a portal may copy, keyed by portal dimension."""
    zones = {portal.get('from'), portal.get('to')}
    published = {}
    for constraint in objects(coverage.get('numeric_constraints')):
        if constraint.get('zone') not in zones:
            continue
        for key in PORTAL_DIMENSIONS:
            if constraint.get(key) is not None:
                published[key] = constraint[key]
    return published


def validate(boundaries, coverage, coverage_sha256):
    """Return the report for a boundary document. Unratable inputs yield result=not_run, never pass."""
    unratable = ratability(boundaries, 'zone-boundaries') + ratability(coverage, 'facility-coverage')
    if unratable:
        return empty_report('not_run', unratable=unratable)

    errors = []
    if boundaries.get('schema_version') != '1.0':
        errors.append('Unknown zone boundary schema version')
    if boundaries.get('mode') != PROPOSAL_MODE:
        errors.append('Boundary document must stay a %s: %s' % (PROPOSAL_MODE, boundaries.get('mode')))
    if boundaries.get('canonical_write_allowed') is not False:
        errors.append('Boundary document must keep canonical_write_allowed false')
    status = boundaries.get('status')
    if isinstance(status, str) and status in FORBIDDEN_SELF_CLAIMS:
        errors.append('Boundary document may not claim status '+str(status))

    provenance = boundaries.get('provenance')
    if not isinstance(provenance, dict):
        errors.append('Boundary document needs a provenance record')
    else:
        if not (isinstance(provenance.get('base_commit'), str) and COMMIT.fullmatch(provenance['base_commit'])):
            errors.append('Provenance needs the 40-hex base commit')
        if not text(provenance.get('work_order_path')):
            errors.append('Provenance needs the work-order path it was built against')
        drift = provenance.get('drift')
        if not isinstance(drift, list) or not all(isinstance(d, dict) and text(d.get('item')) and text(d.get('note'))
                                                  for d in drift):
            errors.append('Provenance drift must list {item, note} entries (an empty list is allowed)')

    source = boundaries.get('coverage_source') if isinstance(boundaries.get('coverage_source'), dict) else {}
    if source.get('path') != COVERAGE:
        errors.append('Coverage source must be '+COVERAGE)
    if not (isinstance(source.get('sha256'), str) and SHA256.fullmatch(source['sha256'])):
        errors.append('Coverage source needs a 64-hex sha256')
    elif source.get('sha256') != coverage_sha256:
        errors.append('Facility coverage changed; reread it and update zone boundaries before reuse')

    covered = {z['id']: z for z in objects(coverage.get('zones')) if text(z.get('id'))}
    connections = objects(coverage.get('connections'))
    constraints = {c['id']: c for c in objects(coverage.get('numeric_constraints')) if text(c.get('id'))}
    expected = {identifier: set() for identifier in covered}
    pairs = {}
    for connection in connections:
        start, end = connection.get('from'), connection.get('to')
        if not (isinstance(start, str) and isinstance(end, str)):
            continue
        pairs[(start, end)] = connection
        if start in expected and end in expected:
            expected[start].add(end)
            expected[end].add(start)

    source_ids = [s.get('id') for s in objects(boundaries.get('sources'))]
    if len(set(map(repr, source_ids))) != len(source_ids):
        errors.append('Duplicate source IDs')
    if any(not text(identifier) for identifier in source_ids):
        errors.append('Source IDs must be non-empty strings')
    supplementary = [identifier for identifier in source_ids if isinstance(identifier, str)]
    if any(not isinstance(s, dict) for s in (boundaries.get('sources') if isinstance(boundaries.get('sources'), list) else [])):
        errors.append('Source entry must be a JSON object')
    for entry in objects(boundaries.get('sources')):
        if not text(entry.get('locator')):
            errors.append('Source needs a repository locator: '+str(entry.get('id')))
        if 'url' in entry and not public_url(entry.get('url')):
            errors.append('Source URL must be public https: '+str(entry.get('id')))
    known = {s['id'] for s in objects(coverage.get('sources')) if text(s.get('id'))} | set(supplementary)
    photo_sets = coverage.get('photo_sets') if isinstance(coverage.get('photo_sets'), dict) else {}
    for photos in photo_sets.values():
        known |= {p['id'] for p in objects(photos) if text(p.get('id'))}
    known.discard(None)
    used = set()

    rows = boundaries.get('zones') if isinstance(boundaries.get('zones'), list) else []
    ids = [z.get('id') if isinstance(z, dict) else None for z in rows]
    if len(set(map(repr, ids))) != len(ids):
        errors.append('Duplicate zone IDs')
    if set(i for i in ids if text(i)) != set(covered):
        errors.append('Zone coverage differs: '+str(sorted(set(i for i in ids if text(i)) ^ set(covered))))
    if any(not isinstance(z, dict) for z in rows):
        errors.append('Zone entry must be a JSON object')

    bundles = boundaries.get('bundles') if isinstance(boundaries.get('bundles'), list) else []
    bundle_of = {}
    for identifier in covered:
        owners = [b.get('id') for b in objects(bundles) if identifier in (b.get('zones') if isinstance(b.get('zones'), list) else [])]
        if len(owners) != 1:
            errors.append('Zone must belong to exactly one bundle: '+identifier)
        else:
            bundle_of[identifier] = owners[0]
    for bundle in objects(bundles):
        zones_in_bundle = bundle.get('zones') if isinstance(bundle.get('zones'), list) else []
        if not all(isinstance(zone_id, str) for zone_id in zones_in_bundle):
            errors.append('Bundle zone entries must be strings: '+str(bundle.get('id')))
        for identifier in [z for z in zones_in_bundle if isinstance(z, str) and z not in covered]:
            errors.append('Unknown zone in bundle '+str(bundle.get('id'))+': '+str(identifier))

    for zone in rows:
        if not isinstance(zone, dict):
            continue
        identifier = zone.get('id')
        coverage_zone = covered.get(identifier, {}) if isinstance(identifier, str) else {}
        if isinstance(identifier, str) and identifier in bundle_of and zone.get('bundle') != bundle_of[identifier]:
            errors.append('Zone bundle field disagrees with bundle list: '+str(identifier))
        if not (text(zone.get('level_assumption')) and text(zone.get('level_basis'))):
            errors.append('Zone needs a level assumption and basis: '+str(identifier))
        inputs = zone.get('inputs') if isinstance(zone.get('inputs'), list) else []
        if 'inputs' in zone and not isinstance(zone.get('inputs'), list):
            errors.append('Zone inputs must be a JSON array: '+str(identifier))
        for entry in inputs:
            if not isinstance(entry, dict):
                errors.append('Input entry must be a JSON object: '+str(identifier))
                continue
            ref = entry.get('ref')
            if isinstance(ref, str):
                used.add(ref)
            if not isinstance(ref, str) or ref not in known:
                errors.append('Unknown source reference: '+str(identifier)+'/'+str(ref))
            uses = entry.get('allowed_use')
            if not isinstance(uses, list) or not uses or not all(isinstance(use, str) and use in ALLOWED_USES for use in uses):
                errors.append('Unknown allowed use: '+str(identifier)+'/'+str(ref))
            if not text(entry.get('scope')):
                errors.append('Input needs a scope: '+str(identifier)+'/'+str(ref))
        basis = zone.get('measurement_basis')
        refs = zone.get('numeric_constraint_refs') if isinstance(zone.get('numeric_constraint_refs'), list) else []
        if 'numeric_constraint_refs' in zone and not isinstance(zone.get('numeric_constraint_refs'), list):
            errors.append('Zone numeric constraint refs must be a JSON array: '+str(identifier))
        if not (isinstance(basis, str) and basis in MEASUREMENTS):
            errors.append('Unknown measurement basis: '+str(identifier))
        if basis == 'operator_published' and not refs:
            errors.append('Numeric constraint required for operator_published: '+str(identifier))
        if refs and basis != 'operator_published':
            errors.append('Numeric constraint refs need operator_published basis: '+str(identifier))
        for ref in refs:
            if not isinstance(ref, str) or constraints.get(ref, {}).get('zone') != identifier:
                errors.append('Numeric constraint '+str(ref)+' does not belong to zone '+str(identifier))
        if basis == 'synthetic_estimate' and not objects(zone.get('synthetic_estimates')):
            errors.append('synthetic_estimate basis needs recorded synthetic values: '+str(identifier))
        if coverage_zone.get('metric_alignment_verified') is not True:
            if zone.get('provisional') is not True:
                errors.append('Unverified zone must stay provisional: '+str(identifier))
            if not zone.get('gaps'):
                errors.append('Unverified zone must list at least one gap: '+str(identifier))
        issues = zone.get('production_issues') if isinstance(zone.get('production_issues'), list) else []
        if not issues or not all(re.fullmatch(r'#\d+', str(i)) for i in issues):
            errors.append('Zone needs production issues (#N): '+str(identifier))
        adjacent = zone.get('adjacent_zones') if isinstance(zone.get('adjacent_zones'), list) else []
        if not all(isinstance(neighbour, str) for neighbour in adjacent):
            errors.append('Zone adjacency entries must be strings: '+str(identifier))
        elif isinstance(identifier, str) and identifier in expected and set(adjacent) != expected[identifier]:
            errors.append('Adjacency differs from coverage connections: '+str(identifier))

    portals = boundaries.get('portals') if isinstance(boundaries.get('portals'), list) else []
    portal_ids = [p.get('id') if isinstance(p, dict) else None for p in portals]
    if len(set(map(repr, portal_ids))) != len(portal_ids):
        errors.append('Duplicate portal IDs')
    if any(not isinstance(p, dict) for p in portals):
        errors.append('Portal entry must be a JSON object')
    portal_pairs = sorted((p.get('from'), p.get('to')) for p in portals
                          if isinstance(p, dict) and isinstance(p.get('from'), str) and isinstance(p.get('to'), str))
    if portal_pairs != sorted(pairs):
        errors.append('Portal coverage differs from coverage connections')
    for portal in objects(portals):
        start, end = portal.get('from'), portal.get('to')
        if not (isinstance(start, str) and isinstance(end, str)):
            errors.append('Portal needs string from/to: '+str(portal.get('id')))
            continue
        connection = pairs.get((start, end), {})
        if portal.get('id') != 'portal.'+start+'--'+end:
            errors.append('Portal ID must be portal.<from>--<to>: '+str(portal.get('id')))
        crosses = bundle_of.get(start) != bundle_of.get(end)
        if portal.get('crosses_bundles') != crosses:
            errors.append('Portal bundle crossing flag is wrong: '+str(portal.get('id')))
        owner = 'pm_integration' if crosses else bundle_of.get(start)
        if portal.get('geometry_owner') != owner:
            errors.append('Portal geometry owner must be '+str(owner)+': '+str(portal.get('id')))
        if not (isinstance(portal.get('level_change'), str) and portal.get('level_change') in LEVEL_CHANGES):
            errors.append('Unknown portal level change: '+str(portal.get('id')))
        published = published_portal_dimensions(coverage, portal)
        for key in PORTAL_DIMENSIONS:
            value = portal.get(key)
            if value is None:
                continue
            if key not in published:
                errors.append('Portal dimension needs an operator-published opening value in coverage; keep null: '
                              +str(portal.get('id'))+'/'+key)
            elif value != published[key]:
                errors.append('Portal dimension disagrees with the published value: '+str(portal.get('id'))+'/'+key)
        if connection.get('world_transform_verified') is not True and portal.get('provisional') is not True:
            errors.append('Unverified portal must stay provisional: '+str(portal.get('id')))
    for bundle in objects(bundles):
        anchor = next((p for p in objects(portals) if p.get('id') == bundle.get('anchor_portal')), None)
        bundle_zones = bundle.get('zones') if isinstance(bundle.get('zones'), list) else []
        touches = anchor is not None and any(isinstance(v, str) and v in bundle_zones
                                             for v in (anchor.get('from'), anchor.get('to')))
        if not touches:
            errors.append('Bundle anchor portal must touch the bundle: '+str(bundle.get('id')))

    for entry in objects(boundaries.get('unassigned_sources')):
        ref = entry.get('ref')
        if isinstance(ref, str):
            used.add(ref)
        if not isinstance(ref, str) or ref not in known:
            errors.append('Unknown source reference: unassigned/'+str(ref))
        if not text(entry.get('reason')):
            errors.append('Unassigned source needs a reason: '+str(ref))
    for ref in sorted((r for r in set(supplementary) - used if r is not None), key=str):
        errors.append('Unused source: '+str(ref))

    questions = boundaries.get('open_questions') if isinstance(boundaries.get('open_questions'), list) else []
    classified = answered = open_count = followup = 0
    if 'open_questions' not in boundaries:
        errors.append('Boundary document needs the %d PM questions' % PM_QUESTION_COUNT)
    elif len(questions) != PM_QUESTION_COUNT:
        errors.append('PM question count must be %d, got %d' % (PM_QUESTION_COUNT, len(questions)))
    question_ids = []
    for question in questions:
        if not isinstance(question, dict):
            errors.append('PM question entry must be a JSON object')
            continue
        question_ids.append(question.get('id'))
        if not text(question.get('question')):
            errors.append('PM question needs its text: '+str(question.get('id')))
        classification = question.get('classification')
        if not (isinstance(classification, str) and classification in PM_CLASSIFICATIONS):
            errors.append('PM question needs answered/open/followup_issue classification: '+str(question.get('id')))
            continue
        classified += 1
        if classification == 'answered':
            answered += 1
            if not text(question.get('decision_source')):
                errors.append('Answered PM question needs a decision source: '+str(question.get('id')))
        elif classification == 'open':
            open_count += 1
            if not text(question.get('open_blocker')):
                errors.append('Open PM question needs an explicit blocker, not a silent resolution: '+str(question.get('id')))
        else:
            followup += 1
            if not (isinstance(question.get('followup_issue'), str) and ISSUE_REF.fullmatch(question['followup_issue'])):
                errors.append('Follow-up PM question needs a #issue: '+str(question.get('id')))
    if len(set(map(repr, question_ids))) != len(question_ids):
        errors.append('Duplicate PM question IDs')

    return {'result': 'fail' if errors else 'pass', 'errors': errors, 'unratable': [],
            'zones': len(rows), 'portals': len(portals),
            'crossBundlePortals': sum(1 for p in objects(portals) if p.get('crosses_bundles') is True),
            'metricVerifiedZones': sum(1 for z in covered.values() if z.get('metric_alignment_verified') is True),
            'pmQuestions': len(questions), 'pmQuestionsClassified': classified,
            'pmQuestionsAnswered': answered, 'pmQuestionsOpen': open_count, 'pmQuestionsFollowup': followup,
            'openQuestions': len(questions), 'facilityFidelityValidated': False}


def load_document(path, label):
    """Return (object, None) or (None, unratable reason). Parse failures are not rule violations."""
    target = resolve(path)
    if not target.exists():
        return None, '%s not found: %s' % (label, path)
    if not target.is_file():
        return None, '%s is not a file: %s' % (label, path)
    try:
        raw = target.read_bytes()
    except OSError as exc:
        return None, '%s unreadable: %s: %s' % (label, path, exc)
    try:
        body = raw.decode('utf-8')
    except UnicodeDecodeError as exc:
        return None, '%s is not valid UTF-8: %s: %s' % (label, path, exc)
    try:
        return json.loads(body), None
    except json.JSONDecodeError as exc:
        return None, '%s is not valid JSON: %s: %s' % (label, path, exc)


def parse_args(argv):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--data', default=BOUNDARIES,
                        help='zone boundary document to check (default: %(default)s)')
    parser.add_argument('--coverage', default=COVERAGE,
                        help='upstream read-only coverage contract (default: %(default)s)')
    return parser.parse_args(argv)


def main(argv=None):
    args = parse_args(sys.argv[1:] if argv is None else argv)
    unratable = []
    coverage, reason = load_document(args.coverage, 'facility-coverage')
    if reason:
        unratable.append(reason)
    boundaries, reason = load_document(args.data, 'zone-boundaries')
    if reason:
        unratable.append(reason)
    if unratable:
        report = empty_report('not_run', unratable=unratable)
    else:
        try:
            sha = digest(resolve(args.coverage))
        except OSError as exc:
            report = empty_report('not_run', unratable=['facility-coverage unreadable: %s: %s' % (args.coverage, exc)])
        else:
            report = validate(boundaries, coverage, sha)
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return {'pass': EXIT_PASS, 'fail': EXIT_FAIL, 'not_run': EXIT_NOT_RUN}[report['result']]


if __name__ == '__main__':
    sys.exit(main())
