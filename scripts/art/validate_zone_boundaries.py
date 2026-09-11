#!/usr/bin/env python3
"""Check the 13-zone boundary draft against facility coverage; never certify surveyed placement."""
import hashlib
import json
from pathlib import Path
import re
import sys
from urllib.parse import urlsplit

ROOT=Path(__file__).resolve().parents[2]
COVERAGE='foundation/world/facility-coverage.json'
BOUNDARIES='foundation/world/zone-boundaries.json'
ALLOWED_USES={'form_reference','topology','floor_membership','historical_layout_reference','operator_published_dimension',
              'quantity_context','non_metric_surface_review','candidate_unreviewed'}
MEASUREMENTS={'operator_published','synthetic_estimate','none'}
LEVEL_CHANGES={'vertical_unverified','unknown'}
PORTAL_DIMENSIONS=('opening_width_m','clear_height_m')

def digest(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def public_url(value):
    try:
        u=urlsplit(value)
        return u.scheme=='https' and bool(u.netloc) and not u.username and not u.password
    except (TypeError,ValueError):return False

def text(value):return isinstance(value,str) and bool(value.strip())

def validate(boundaries,coverage,coverage_sha256):
    errors=[]
    if boundaries.get('schema_version')!='1.0':errors.append('Unknown zone boundary schema version')
    source=boundaries.get('coverage_source',{})
    if source.get('path')!=COVERAGE:errors.append('Coverage source must be '+COVERAGE)
    if source.get('sha256')!=coverage_sha256:errors.append('Facility coverage changed; reread it and update zone boundaries before reuse')
    covered={z['id']:z for z in coverage.get('zones',[])}
    connections=coverage.get('connections',[])
    constraints={c['id']:c for c in coverage.get('numeric_constraints',[])}
    expected={identifier:set() for identifier in covered}
    for c in connections:expected[c['from']].add(c['to']);expected[c['to']].add(c['from'])

    supplementary=[s.get('id') for s in boundaries.get('sources',[])]
    if len(set(supplementary))!=len(supplementary):errors.append('Duplicate source IDs')
    for s in boundaries.get('sources',[]):
        if not text(s.get('locator')):errors.append('Source needs a repository locator: '+str(s.get('id')))
        if 'url' in s and not public_url(s['url']):errors.append('Source URL must be public https: '+str(s.get('id')))
    known={s['id'] for s in coverage.get('sources',[])}|set(supplementary)
    known|={p['id'] for photos in coverage.get('photo_sets',{}).values() for p in photos}
    used=set()

    rows=boundaries.get('zones',[]);ids=[z.get('id') for z in rows]
    if len(set(ids))!=len(ids):errors.append('Duplicate zone IDs')
    if set(ids)!=set(covered):errors.append('Zone coverage differs: '+str(sorted(set(ids)^set(covered))))
    bundles=boundaries.get('bundles',[]);bundle_of={}
    for identifier in covered:
        owners=[b.get('id') for b in bundles if identifier in b.get('zones',[])]
        if len(owners)!=1:errors.append('Zone must belong to exactly one bundle: '+identifier)
        else:bundle_of[identifier]=owners[0]
    for b in bundles:
        for identifier in set(b.get('zones',[]))-set(covered):errors.append('Unknown zone in bundle '+str(b.get('id'))+': '+str(identifier))

    for zone in rows:
        identifier=zone.get('id');coverage_zone=covered.get(identifier,{})
        if identifier in bundle_of and zone.get('bundle')!=bundle_of[identifier]:errors.append('Zone bundle field disagrees with bundle list: '+str(identifier))
        if not (text(zone.get('level_assumption')) and text(zone.get('level_basis'))):errors.append('Zone needs a level assumption and basis: '+str(identifier))
        for entry in zone.get('inputs',[]):
            ref=entry.get('ref');used.add(ref)
            if ref not in known:errors.append('Unknown source reference: '+str(identifier)+'/'+str(ref))
            uses=entry.get('allowed_use') or []
            if not uses or not set(uses)<=ALLOWED_USES:errors.append('Unknown allowed use: '+str(identifier)+'/'+str(ref))
            if not text(entry.get('scope')):errors.append('Input needs a scope: '+str(identifier)+'/'+str(ref))
        basis=zone.get('measurement_basis');refs=zone.get('numeric_constraint_refs',[])
        if basis not in MEASUREMENTS:errors.append('Unknown measurement basis: '+str(identifier))
        if basis=='operator_published' and not refs:errors.append('Numeric constraint required for operator_published: '+str(identifier))
        if refs and basis!='operator_published':errors.append('Numeric constraint refs need operator_published basis: '+str(identifier))
        for ref in refs:
            if constraints.get(ref,{}).get('zone')!=identifier:errors.append('Numeric constraint '+str(ref)+' does not belong to zone '+str(identifier))
        if coverage_zone.get('metric_alignment_verified') is not True:
            if zone.get('provisional') is not True:errors.append('Unverified zone must stay provisional: '+str(identifier))
            if not zone.get('gaps'):errors.append('Unverified zone must list at least one gap: '+str(identifier))
        issues=zone.get('production_issues') or []
        if not issues or not all(re.fullmatch(r'#\d+',str(i)) for i in issues):errors.append('Zone needs production issues (#N): '+str(identifier))
        if identifier in expected and set(zone.get('adjacent_zones',[]))!=expected[identifier]:
            errors.append('Adjacency differs from coverage connections: '+str(identifier))

    portals=boundaries.get('portals',[]);portal_ids=[p.get('id') for p in portals]
    if len(set(portal_ids))!=len(portal_ids):errors.append('Duplicate portal IDs')
    pairs={(c['from'],c['to']):c for c in connections}
    if sorted((p.get('from'),p.get('to')) for p in portals)!=sorted(pairs):errors.append('Portal coverage differs from coverage connections')
    for p in portals:
        a,b=p.get('from'),p.get('to');connection=pairs.get((a,b),{})
        if p.get('id')!='portal.'+str(a)+'--'+str(b):errors.append('Portal ID must be portal.<from>--<to>: '+str(p.get('id')))
        crosses=bundle_of.get(a)!=bundle_of.get(b)
        if p.get('crosses_bundles')!=crosses:errors.append('Portal bundle crossing flag is wrong: '+str(p.get('id')))
        owner='pm_integration' if crosses else bundle_of.get(a)
        if p.get('geometry_owner')!=owner:errors.append('Portal geometry owner must be '+str(owner)+': '+str(p.get('id')))
        if p.get('level_change') not in LEVEL_CHANGES:errors.append('Unknown portal level change: '+str(p.get('id')))
        for key in PORTAL_DIMENSIONS:
            if p.get(key) is not None:errors.append('Portal dimension without a published opening basis: '+str(p.get('id'))+'/'+key)
        if connection.get('world_transform_verified') is not True and p.get('provisional') is not True:
            errors.append('Unverified portal must stay provisional: '+str(p.get('id')))
    for b in bundles:
        anchor=next((p for p in portals if p.get('id')==b.get('anchor_portal')),None)
        if anchor is None or not {anchor.get('from'),anchor.get('to')}&set(b.get('zones',[])):
            errors.append('Bundle anchor portal must touch the bundle: '+str(b.get('id')))

    for entry in boundaries.get('unassigned_sources',[]):
        ref=entry.get('ref');used.add(ref)
        if ref not in known:errors.append('Unknown source reference: unassigned/'+str(ref))
        if not text(entry.get('reason')):errors.append('Unassigned source needs a reason: '+str(ref))
    for ref in sorted(set(supplementary)-used,key=str):errors.append('Unused source: '+str(ref))

    return {'errors':errors,'zones':len(rows),'portals':len(portals),
            'crossBundlePortals':sum(1 for p in portals if p.get('crosses_bundles') is True),
            'metricVerifiedZones':sum(1 for z in covered.values() if z.get('metric_alignment_verified') is True),
            'openQuestions':len(boundaries.get('open_questions',[])),'facilityFidelityValidated':False}

def main():
    coverage=json.loads((ROOT/COVERAGE).read_text(encoding='utf-8'))
    boundaries=json.loads((ROOT/BOUNDARIES).read_text(encoding='utf-8'))
    report=validate(boundaries,coverage,digest(ROOT/COVERAGE))
    print(json.dumps(report,ensure_ascii=False,indent=2))
    return 1 if report['errors'] else 0

if __name__=='__main__':sys.exit(main())
