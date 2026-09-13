#!/usr/bin/env python3
"""Traverse the generated connected world with three real NGO clients and recover schema2.

All movement uses the ordinary FieldInput protocol. No server position override is present.
Private invitations, checkpoints and raw logs stay in the selected ignored session directory.
"""
import argparse
import copy
import hashlib
import json
import math
import os
import plistlib
import secrets
import subprocess
import time
from pathlib import Path

from create_multiplayer_session import private_json
from run_multiplayer_probe import events

PROJECT = Path(__file__).resolve().parents[2]
NAMES = ['participant-1', 'participant-2', 'instructor']
ROUTES = [
    ['station_concourse_2f', 'rail_platforms_mainline', 'rolling_stock_mainline',
     'rail_platforms_mainline', 'rail_tracks_mainline', 'rail_platforms_mainline',
     'station_concourse_2f', 'station_hall_1f', 'station_concourse_2f'],
    ['station_concourse_2f', 'rail_terminal_public', 'station_ticket_area',
     'rail_terminal_public', 'underground_connector', 'forecourt_eurasia',
     'underground_connector', 'rail_terminal_public', 'station_concourse_2f'],
    ['underground_shopping_passage', 'underground_connector', 'underground_shopping_passage',
     'metro_concourse', 'metro_platforms', 'rolling_stock_metro', 'metro_platforms',
     'metro_concourse', 'underground_shopping_passage'],
]


def local(frame, point):
    x, z = point['X'] - frame['Origin']['X'], point['Z'] - frame['Origin']['Z']
    angle = math.radians(frame['YawDegrees'])
    return {'X': math.cos(angle) * x - math.sin(angle) * z,
            'Y': point['Y'] - frame['Origin']['Y'],
            'Z': math.sin(angle) * x + math.cos(angle) * z}


def prepare(session):
    if session.exists():
        raise ValueError('Use a new private session directory')
    session.mkdir(mode=0o700, parents=True)
    (session / 'records').mkdir(mode=0o700)
    profile = json.loads((PROJECT / 'foundation/world/connected-world-profile.json').read_text())
    world = json.loads((PROJECT / 'foundation/network/connected-world-layout.json').read_text())
    world['ShiftId'] = 'shift-' + secrets.token_hex(8)
    world['Participants'] = []
    lookup = {r['Id']: r for r in profile['Regions']}
    frames = {f['FrameId']: f for f in profile['Frames']}
    tickets = []
    for name, route in zip(NAMES, ROUTES):
        start = lookup[route[0]]
        position = copy.deepcopy(start['Hub'])
        world['Participants'].append({'ParticipantId': name, 'TeamId': 'command' if name == 'instructor' else 'team-a',
            'RoleId': 'instructor' if name == 'instructor' else 'role-01', 'IsInstructor': name == 'instructor',
            'RegionId': start['Id'], 'FrameId': start['FrameId'], 'PortalId': '', 'Position': position,
            'LocalPosition': local(frames[start['FrameId']], position), 'InputEnabled': False, 'ObservedIds': []})
        secret = secrets.token_urlsafe(32)
        tickets.append({'ParticipantId': name, 'SecretSha256': hashlib.sha256(secret.encode()).hexdigest()})
        private_json(session / f'{name}.invitation.json', {'WorldId': world['WorldId'], 'ShiftId': world['ShiftId'],
            'ParticipantId': name, 'Secret': secret})
        waypoints = []
        for index, rid in enumerate(route):
            region = lookup[rid]
            if index:
                previous = route[index - 1]
                edge = next(e for e in profile['Portals'] if {e['From'], e['To']} == {previous, rid})
                reverse = edge['To'] == previous
                waypoints += [{'Position': edge['ToPoint'] if reverse else edge['FromPoint'], 'RegionId': previous},
                              {'Position': edge['FromPoint'] if reverse else edge['ToPoint'], 'RegionId': rid}]
            waypoints.append({'Position': region['Hub'], 'RegionId': rid})
            at = copy.deepcopy(region['EquipmentPosition'])
            dx = region['Hub']['X'] - at['X']; dz = region['Hub']['Z'] - at['Z']
            length = math.hypot(dx, dz)
            at['X'] += dx / length * 1.5; at['Z'] += dz / length * 1.5
            waypoints.append({'Position': at, 'RegionId': rid, 'TargetId': 'equipment.' + rid, 'Operations': 2})
            waypoints.append({'Position': region['Hub'], 'RegionId': rid})
        private_json(session / f'{name}.probe.json', {'Steps': [], 'Waypoints': waypoints,
            'ExitWhenRouteComplete': True, 'ExitAfterSeconds': 380})
    private_json(session / 'server-session.json', {'World': world, 'Tickets': tickets})
    return profile


def inspect(session, profile):
    clients = {n: events(session / f'{n}.events.jsonl') for n in NAMES}
    visited, operated = set(), set()
    for name, rows in clients.items():
        assert any(r['Kind'] == 'route_complete' for r in rows), f'Incomplete route: {name}'
        assert not any(r['Kind'] == 'waypoint_failed' for r in rows), f'Operation failed: {name}'
        for row in rows:
            if row['Kind'] == 'waypoint':
                data = row['Data']; visited.add(data['RegionId'])
                assert data['RegionId'] in data['LoadedRegions'], f'Actor entered unloaded region: {name}'
                if data['Operations'] == 2:
                    operated.add(data['RegionId'])
            elif row['Kind'] == 'view':
                data = row['Data']
                assert data['ProtocolVersion'] == 2 and data['SpatialProfileId'] == profile['ProfileId']
                assert all(e['RegionId'] == data['RegionId'] for e in data['Observed']['Entities'])
                assert not any(e['Kind'] == 2 for e in data['Observed']['Entities'])
    all_regions = {r['Id'] for r in profile['Regions']}
    assert visited == all_regions and operated == all_regions, (visited, operated)
    actual_edges = set()
    for name, rows in clients.items():
        previous = None
        for row in rows:
            if row['Kind'] != 'view':
                continue
            rid = row['Data']['RegionId']
            if previous is not None and previous != rid:
                actual_edges.add((previous, rid))
            previous = rid
    expected_edges = {(p['From'], p['To']) for p in profile['Portals']} | {(p['To'], p['From']) for p in profile['Portals']}
    assert actual_edges == expected_edges, {'missing': sorted(expected_edges - actual_edges), 'extra': sorted(actual_edges - expected_edges)}
    first_late_views = [r['Data'] for r in clients['participant-2'] if r['Kind'] == 'view']
    assert any(any(e['EntityId'] == 'equipment.station_concourse_2f' and e['Revision'] >= 2
                   for e in v['Observed']['Entities']) for v in first_late_views[:30]), 'Late client missed prior shared state'
    return {'status': 'local_connected_protocol_pass', 'protocolClients': 3, 'lateJoin': True,
            'traversedRegions': sorted(visited), 'bidirectionalConnections': len(actual_edges) // 2,
            'regionsWithTwoSharedOperations': sorted(operated),
            'scope': 'Synthetic 13-region traversal, shared interactions, additive loading and static boarding frames. No visual, facility, train-motion, WAN, voice or 20/100/2 acceptance.'}


def run(app, session, port, profile):
    with (app / 'Contents/Info.plist').open('rb') as stream:
        binary = app / 'Contents/MacOS' / plistlib.load(stream)['CFBundleExecutable']
    processes, handles = [], []

    def launch(name, args):
        handle = (session / f'{name}.stdio.log').open('w'); handles.append(handle)
        process = subprocess.Popen([str(binary), '-batchmode', '-nographics', '-logFile', str(session / f'{name}.player.log'),
            '--cg-port', str(port), '--cg-evidence', str(session / f'{name}.events.jsonl')] + args,
            stdout=handle, stderr=subprocess.STDOUT)
        processes.append(process); return process

    def server_start(name):
        process = launch(name, ['--cg-mode', 'server', '--cg-session', str(session / 'server-session.json'),
                               '--cg-records', str(session / 'records')])
        deadline = time.monotonic() + 40
        while not any(e['Kind'] == 'server_started' for e in events(session / f'{name}.events.jsonl')):
            if process.poll() is not None:
                raise RuntimeError(f'{name} exited during startup; inspect local log')
            if time.monotonic() > deadline:
                raise TimeoutError('Server startup deadline')
            time.sleep(.2)
        return process

    try:
        server = server_start('server')
        print('Connected-world server loaded 13 scenes; starting three real clients, one delayed by 10 seconds', flush=True)
        clients = {}
        for name in ['participant-1', 'instructor']:
            clients[name] = launch(name, ['--cg-mode', 'client', '--cg-credential', str(session / f'{name}.invitation.json'),
                                          '--cg-probe', str(session / f'{name}.probe.json')])
        started = time.monotonic(); last_report = started
        while any(p.poll() is None for p in clients.values()) or len(clients) < 3:
            now = time.monotonic()
            if len(clients) < 3 and now - started >= 10:
                name = 'participant-2'
                clients[name] = launch(name, ['--cg-mode', 'client', '--cg-credential', str(session / f'{name}.invitation.json'),
                    '--cg-probe', str(session / f'{name}.probe.json')])
            if now - started > 410:
                raise TimeoutError('Traversal exceeded 410 seconds')
            if now - last_report >= 15:
                counts = {n: sum(r['Kind'] == 'waypoint' for r in events(session / f'{n}.events.jsonl')) for n in NAMES}
                print(json.dumps({'seconds': round(now - started), 'waypoints': counts}), flush=True); last_report = now
            time.sleep(.2)
        assert all(p.returncode == 0 for p in clients.values()), 'A client returned a nonzero exit'
        result = inspect(session, profile)
        # Rejoin after a real server kill. The static frame pose is recovered from disk before instructor resume.
        before = [r['Data'] for r in events(session / 'participant-1.events.jsonl') if r['Kind'] == 'view'][-1]
        server.kill(); server.wait(timeout=8)
        server = server_start('recovered-server')
        private_json(session / 'rejoin.probe.json', {'Steps': [], 'ExitAfterSeconds': 8})
        private_json(session / 'resume.probe.json', {'Steps': [{'AtSeconds': 3, 'CommandId': 'resume-connected-world', 'Kind': 7}], 'ExitAfterSeconds': 8})
        rejoin = launch('rejoin', ['--cg-mode', 'client', '--cg-credential', str(session / 'participant-1.invitation.json'), '--cg-probe', str(session / 'rejoin.probe.json')])
        instructor = launch('resume', ['--cg-mode', 'client', '--cg-credential', str(session / 'instructor.invitation.json'), '--cg-probe', str(session / 'resume.probe.json')])
        rejoin.wait(timeout=35); instructor.wait(timeout=35)
        views = [r['Data'] for r in events(session / 'rejoin.events.jsonl') if r['Kind'] == 'view']
        assert views and views[0]['Observed']['Paused'] and not views[-1]['Observed']['Paused']
        assert (views[0]['RegionId'], views[0]['FrameId'], views[0]['LocalPosition']) == (before['RegionId'], before['FrameId'], before['LocalPosition'])
        result['forcedServerRecovery'] = 'schema2 pose preserved; paused until real instructor command'
        errors = {}
        for log in session.glob('*.player.log'):
            found = [line for line in log.read_text(errors='replace').splitlines() if 'Exception:' in line or 'startup failed' in line or 'command processing stopped' in line]
            if found: errors[log.name] = found[:6]
        if errors:
            result['status'] = 'runtime_error'; result['errors'] = errors
        (session / 'connected-protocol-result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
        print(json.dumps(result, ensure_ascii=False, indent=2), flush=True)
        assert not errors, 'Player errors prevent acceptance'
    finally:
        for process in processes:
            if process.poll() is None: process.terminate()
        for process in processes:
            try: process.wait(timeout=5)
            except subprocess.TimeoutExpired: process.kill(); process.wait()
        for handle in handles: handle.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, default=PROJECT / 'Builds/FoundationConnectedWorldMac/ChooGuardConnectedWorld.app')
    parser.add_argument('--session', required=True, type=Path)
    parser.add_argument('--port', type=int, default=17979)
    parser.add_argument('--prepare-only', action='store_true')
    args = parser.parse_args()
    profile = prepare(args.session.resolve())
    if not args.prepare_only: run(args.app.resolve(), args.session.resolve(), args.port, profile)


if __name__ == '__main__':
    main()
