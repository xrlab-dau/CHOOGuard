import contextlib
import sys
import copy
import io
import json
import math
import os
import plistlib
import stat
import subprocess
import tempfile
import unittest
from collections import Counter
from datetime import datetime, timedelta, timezone
from pathlib import Path
from unittest import mock

import foundation_load_metrics as metrics
import run_foundation_load as load
import base64
import zlib

import probe_journal as journal


ROOT = Path(__file__).resolve().parent
PROJECT = Path(os.environ.get('CHOOGUARD_PROJECT', Path(__file__).resolve().parents[2]))
TEST_TMP = Path(os.environ.get('FOUNDATION_LOAD_TEST_TMP', tempfile.gettempdir()))


def metric(role='server', index=0, seconds=1, ticks=20):
    start = datetime(2026, 9, 9, tzinfo=timezone.utc) + timedelta(seconds=index * seconds)
    connected = 20 if role == 'server' else 1
    return {'Schema': 1, 'Kind': 'interval', 'Role': role, 'DurationSeconds': seconds, 'TickCount': ticks,
            'TickMilliseconds': {'Bounds': [10, 50, 100], 'Counts': [ticks, 0, 0, 0]},
            'SentPayloadBytes': 1200, 'ReceivedPayloadBytes': 800,
            'ConnectedClients': connected, 'NpcCount': 100, 'ActiveIncidents': 2,
            'ConnectedClientsMin': connected, 'ConnectedClientsMax': connected, 'ConnectedClientsMean': connected,
            'NpcCountMin': 100, 'NpcCountMax': 100, 'NpcCountMean': 100,
            'ActiveIncidentsMin': 2, 'ActiveIncidentsMax': 2, 'ActiveIncidentsMean': 2,
            'UtcStart': start.isoformat(), 'UtcEnd': (start + timedelta(seconds=seconds)).isoformat(),
            'SimulationTickStart': index * ticks, 'SimulationTickEnd': (index + 1) * ticks,
            'PausedAny': False, 'MaximumBacklogSeconds': 0.0, 'Batch': True, 'NullGraphics': True}


def readers_with_rows(duration=1):
    result = {}
    for label in ['server', *[f'client-{i:02d}' for i in range(20)]]:
        role = 'server' if label == 'server' else 'client'
        reader = metrics.MetricsReader(ROOT / 'not-used', role)
        reader.intervals = [metrics.Interval.parse(metric(role, seconds=duration, ticks=int(duration * 20)), role)]
        result[label] = reader
    return result


class PrivatePreparation(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(dir=TEST_TMP)
        self.path = Path(self.tmp.name)
        self.player = self.path / 'Synthetic.x86_64'
        self.player.write_bytes(b'not-an-executed-program\n')
        self.player.chmod(0o700)

    def tearDown(self):
        self.tmp.cleanup()

    def config(self, **overrides):
        values = dict(project=PROJECT, session=self.path / 'session', player=self.player, duration=1,
                      startup_timeout=1, warmup_timeout=1, metrics_grace=1, shutdown_grace=1, launch_stagger=0)
        values.update(overrides)
        return load.Config(**values)

    def test_exact_roles_secrets_permissions_server_only_profile_and_no_overwrite(self):
        cfg = self.config()
        before = (PROJECT / 'foundation/world/foundation-simulation-profile.json').read_bytes()
        plan = load.prepare(cfg)
        self.assertEqual(plan['Status'], 'PREPARED_NOT_RUN')
        self.assertEqual(plan['RoleCounts'], {'instructor': 1, 'role-01': 4, 'role-02': 4, 'role-03': 4, 'role-04': 4, 'role-05': 3})
        session = load.read_json(cfg.session / 'server-session.json')
        self.assertEqual(session['World']['SchemaVersion'], 2)
        self.assertEqual(len(session['World']['Participants']), 20)
        self.assertEqual(len({p['RegionId'] for p in session['World']['Participants']}), 13)
        self.assertEqual(sum(p['IsInstructor'] for p in session['World']['Participants']), 1)
        for item in plan['Clients']:
            invitation = load.read_json(Path(item['Credential']))
            ticket = next(t for t in session['Tickets'] if t['ParticipantId'] == invitation['ParticipantId'])
            self.assertEqual(ticket['SecretSha256'], load.hashlib.sha256(invitation['Secret'].encode()).hexdigest())
            self.assertNotIn(invitation['Secret'], json.dumps(session))
            self.assertNotIn(invitation['Secret'], json.dumps(plan))
        if os.name == 'posix':
            for path in [cfg.session, *cfg.session.rglob('*')]:
                self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o700 if path.is_dir() else 0o600)
        with self.assertRaisesRegex(load.LoadError, 'NO_OVERWRITE'):
            load.prepare(cfg)
        self.assertEqual(before, (PROJECT / 'foundation/world/foundation-simulation-profile.json').read_bytes())
        args = load.commands(cfg, plan)
        self.assertEqual(len(args), 21)
        self.assertIn('--cg-simulation', args['server'])
        for label, argv in args.items():
            self.assertNotIn('--cg-evidence', argv)
            if label != 'server':
                self.assertNotIn('--cg-simulation', argv)
                self.assertNotIn('--cg-session', argv)
                self.assertIn('--cg-credential', argv)
                self.assertIn('--cg-probe', argv)
        self.assertTrue(all('--cg-metrics' in argv for argv in args.values()))

    def test_gathered_npcs_and_players_are_one_region_but_still_require_server_geometry(self):
        cfg = self.config(layout='gathered')
        plan = load.prepare(cfg)
        world = load.read_json(cfg.session / 'server-session.json')['World']
        simulation = load.read_json(cfg.session / 'server-simulation.json')
        self.assertEqual(simulation['NpcSpawnRegions'], ['station_concourse_2f'])
        self.assertEqual(simulation['NpcCount'], 100)
        self.assertEqual(simulation['Schedule']['MaximumActive'], 2)
        self.assertEqual({p['RegionId'] for p in world['Participants']}, {'station_concourse_2f'})
        points = [p['Position'] for p in world['Participants']]
        self.assertTrue(all(math.hypot(a['X']-b['X'], a['Z']-b['Z']) >= 1.19 for i,a in enumerate(points) for b in points[i+1:]))
        self.assertIn('server must validate', plan['SpawnValidation'])

    def test_vehicle_clients_hold_and_station_patrol_does_not_operate_equipment(self):
        cfg = self.config()
        plan = load.prepare(cfg)
        kinds = Counter()
        for item in plan['Clients']:
            probe = load.read_json(Path(item['Probe']))
            self.assertFalse(probe['ExitWhenRouteComplete'])
            self.assertGreater(probe['ExitAfterSeconds'], cfg.duration + cfg.warmup_timeout)
            self.assertEqual(probe['Steps'], [])
            if item['Patrol'] == 'held_vehicle_rider':
                self.assertEqual(probe['Waypoints'], [])
                kinds['held'] += 1
            else:
                self.assertTrue(probe['Waypoints'])
                self.assertTrue(all(w['Operations'] == 0 and w['WaitSeconds'] == 1 for w in probe['Waypoints']))
                kinds['walking'] += 1
        self.assertGreater(kinds['held'], 0)
        self.assertGreater(kinds['walking'], 0)

    def test_prepare_only_cli_never_calls_runner_or_exposes_private_values(self):
        output = io.StringIO()
        with mock.patch.object(load, 'run', side_effect=AssertionError('must not run')), contextlib.redirect_stdout(output):
            code = load.main(['--project', str(PROJECT), '--session', str(self.path/'cli'), '--player', str(self.player), '--duration', '1', '--prepare-only'])
        self.assertEqual(code, 0)
        text = output.getvalue()
        self.assertNotIn(str(self.path), text)
        self.assertNotIn('Secret', text)
        self.assertNotIn('ParticipantId', text)
        self.assertEqual(json.loads(text)['Status'], 'PREPARED_NOT_RUN')

    def test_app_bundle_and_windows_executable_resolution_without_execution(self):
        app = self.path / 'Chosen.app'
        macos = app / 'Contents/MacOS'
        macos.mkdir(parents=True)
        binary = macos / 'ActualName'
        binary.write_bytes(b'not executable content')
        binary.chmod(0o700)
        with (app/'Contents/Info.plist').open('wb') as stream:
            plistlib.dump({'CFBundleExecutable': 'ActualName'}, stream)
        self.assertEqual(load.resolve_player(app), (binary.resolve(), 'macos'))
        with (app/'Contents/Info.plist').open('wb') as stream:
            plistlib.dump({'CFBundleExecutable': '../../outside'}, stream)
        with self.assertRaises(load.LoadError):
            load.resolve_player(app)
        exe = self.path/'Windows.exe'
        exe.write_bytes(b'not executed')
        self.assertEqual(load.resolve_player(exe)[1], 'windows')

    def test_invalid_cli_argument_is_not_echoed(self):
        output = io.StringIO()
        with contextlib.redirect_stderr(output), self.assertRaises(SystemExit):
            load.main(['--port', 'private-token-do-not-echo'])
        self.assertNotIn('private-token', output.getvalue())
        self.assertIn('INVALID_CLI_ARGUMENTS', output.getvalue())

    def test_invalid_counts_and_durations_do_not_create_session(self):
        for changes in ({'clients':19}, {'duration':float('nan')}, {'duration':0}, {'port':0}, {'layout':'unknown'}):
            with self.subTest(changes=changes), self.assertRaises(load.LoadError):
                load.prepare(self.config(**changes))
            self.assertFalse((self.path/'session').exists())


class MetricValidation(unittest.TestCase):
    def test_histogram_bounds_are_intervals_not_invented_exact_percentiles(self):
        histogram = metrics.Histogram.parse({'Bounds':[10,50,100], 'Counts':[90,5,4,1]},100)
        self.assertEqual(histogram.percentile_bounds(), {'LowerExclusiveMs':10., 'UpperInclusiveMs':50.})
        self.assertEqual(histogram.above_budget_range(), {'DefiniteSamples':5,'PossibleSamples':5})
        overflow = metrics.Histogram.parse({'Bounds':[50], 'Counts':[0,1]},1)
        self.assertIsNone(overflow.percentile_bounds()['UpperInclusiveMs'])

    def test_negative_nonfinite_duplicate_or_mismatched_histograms_are_rejected(self):
        cases = [{'Bounds':[50,10], 'Counts':[1,0,0]}, {'Bounds':[50], 'Counts':[1]},
                 {'Bounds':[50], 'Counts':[0,0]}, {'Bounds':[50], 'Counts':[-1,2]},
                 {'Bounds':[float('nan')], 'Counts':[1,0]}, {'Bounds':[50], 'Counts':[True,0]},
                 {'Bounds':[50], 'Counts':[10**1000,0]}]
        for raw in cases:
            with self.subTest(raw=str(raw)[:50]), self.assertRaises(metrics.MetricsError):
                metrics.Histogram.parse(raw,1)
        with self.assertRaisesRegex(metrics.MetricsError,'DUPLICATE'):
            metrics.strict_json('{"Schema":1,"Schema":1}')
        with self.assertRaises(metrics.MetricsError):
            metrics.strict_json('{"value":NaN}')

    def test_utc_jump_is_recorded_but_monotonic_duration_is_preserved(self):
        row = metric()
        row['UtcEnd'] = '2026-09-08T23:59:01+00:00'
        value = metrics.Interval.parse(row,'server')
        self.assertEqual(value.seconds,1)
        self.assertTrue(value.utc_jump)
        row['UtcEnd'] = '2026-09-09T00:00:01'
        with self.assertRaises(metrics.MetricsError):
            metrics.Interval.parse(row,'server')
        row['UtcEnd'] = '2026-09-09T09:00:01+09:00'
        self.assertFalse(metrics.Interval.parse(row, 'server').utc_jump)

    def test_missing_population_range_fields_do_not_satisfy_warmup(self):
        readers = readers_with_rows()
        self.assertTrue(metrics.ready_for_measurement(readers))
        for missing in ('ConnectedClientsMin', 'NpcCountMax', 'ActiveIncidentsMean'):
            row = metric()
            del row[missing]
            readers['server'].intervals = [metrics.Interval.parse(row,'server')]
            with self.subTest(missing=missing):
                self.assertFalse(metrics.ready_for_measurement(readers))
                result = metrics.assess(readers,{},1,1)
                self.assertEqual(result['Status'],'METRICS_INCOMPLETE')
                self.assertEqual(result['Acceptance'],'NOT_ASSESSED')

    def test_counts_without_complete_roster_duration_or_traffic_are_not_success(self):
        readers = readers_with_rows()
        self.assertEqual(metrics.assess(readers,{},1,1)['Status'],'LOCAL_PROTOCOL_RUN_RECORDED')
        self.assertEqual(metrics.assess(readers,{},2,2)['Status'],'METRICS_INCOMPLETE')
        self.assertEqual(metrics.assess(readers,{},1,.5)['Status'],'METRICS_INCOMPLETE')
        readers.pop('client-19')
        self.assertEqual(metrics.assess(readers,{},1,1)['Status'],'METRICS_INCOMPLETE')
        readers = readers_with_rows()
        row = metric('client'); row['ReceivedPayloadBytes'] = 0
        readers['client-19'].intervals = [metrics.Interval.parse(row,'client')]
        self.assertEqual(metrics.assess(readers,{},1,1)['Status'],'METRICS_INCOMPLETE')

    def test_ambiguous_bins_and_definite_budget_failures_are_distinct(self):
        readers = readers_with_rows()
        row = metric(); row['TickMilliseconds'] = {'Bounds':[40,60], 'Counts':[0,20,0]}
        readers['server'].intervals = [metrics.Interval.parse(row,'server')]
        self.assertEqual(metrics.assess(readers,{},1,1)['Status'],'METRICS_INCOMPLETE')
        row['TickMilliseconds'] = {'Bounds':[10,50,100], 'Counts':[0,0,20,0]}
        readers['server'].intervals = [metrics.Interval.parse(row,'server')]
        self.assertEqual(metrics.assess(readers,{},1,1)['Status'],'LOAD_BUDGET_EXCEEDED')
        self.assertEqual(metrics.assess(readers,{},1,1,process_failure='CLIENT_EARLY_EXIT')['Status'],'RUN_FAILED')

    def test_fast_sparse_server_ticks_do_not_masquerade_as_twenty_hertz(self):
        readers = readers_with_rows()
        readers['server'].intervals = [metrics.Interval.parse(metric(ticks=3), 'server')]
        result = metrics.assess(readers, {}, 1, 1)
        self.assertEqual(result['Status'], 'LOAD_BUDGET_EXCEEDED')
        self.assertTrue(result['ServerSimulationRate']['BelowRequired'])

    def test_tick_calls_are_not_physics_progress_and_paused_time_is_not_load_success(self):
        readers = readers_with_rows(); row = metric()
        row['SimulationTickEnd'] = 0
        readers['server'].intervals = [metrics.Interval.parse(row, 'server')]
        self.assertEqual(metrics.assess(readers, {}, 1, 1)['Status'], 'LOAD_BUDGET_EXCEEDED')
        row = metric(); row['PausedAny'] = True
        readers['server'].intervals = [metrics.Interval.parse(row, 'server')]
        self.assertEqual(metrics.assess(readers, {}, 1, 1)['Status'], 'LOAD_BUDGET_EXCEEDED')
        self.assertFalse(metrics.ready_for_measurement(readers))
        for key in ('SimulationTickStart','SimulationTickEnd','PausedAny','MaximumBacklogSeconds'):
            row.pop(key)
        readers['server'].intervals = [metrics.Interval.parse(row, 'server')]
        self.assertEqual(metrics.assess(readers, {}, 1, 1)['Status'], 'METRICS_INCOMPLETE')

    def test_explicit_population_maximum_overrun_is_rejected(self):
        row = metric(); row['ActiveIncidentsMax'] = 3
        with self.assertRaisesRegex(metrics.MetricsError, 'WORLD_LOAD_TARGET_EXCEEDED'):
            metrics.Interval.parse(row, 'server')

    def test_server_current_population_above_target_is_rejected_without_maximum(self):
        row = metric()
        row['ConnectedClients'] = 21
        for suffix in ('Min', 'Max', 'Mean'):
            row.pop('ConnectedClients' + suffix)
        with self.assertRaisesRegex(metrics.MetricsError, 'WORLD_LOAD_TARGET_EXCEEDED'):
            metrics.Interval.parse(row, 'server')

    def test_client_population_must_describe_one_connected_protocol_client(self):
        for connected in (0, 2):
            row = metric('client')
            for suffix in ('', 'Min', 'Max', 'Mean'):
                row['ConnectedClients' + suffix] = connected
            with self.subTest(connected=connected), self.assertRaisesRegex(metrics.MetricsError, 'CLIENT_POPULATION_INVALID'):
                metrics.Interval.parse(row, 'client')

    def test_runtime_mode_flags_are_required_for_headless_contract(self):
        for key, value in (('Batch', False), ('NullGraphics', False)):
            row = metric()
            row[key] = value
            with self.subTest(key=key), self.assertRaisesRegex(metrics.MetricsError, 'HEADLESS_RUNTIME_MODE_REQUIRED'):
                metrics.Interval.parse(row, 'server')
        for missing in ('Batch', 'NullGraphics'):
            row = metric(); row.pop(missing)
            with self.subTest(missing=missing):
                value = metrics.Interval.parse(row, 'server')
                self.assertIsNone(value.batch)
                self.assertIsNone(value.null_graphics)
        for missing, invalid in (('Batch', 'NullGraphics'), ('NullGraphics', 'Batch')):
            row = metric(); row.pop(missing); row[invalid] = False
            with self.subTest(missing=missing, invalid=invalid), self.assertRaisesRegex(
                    metrics.MetricsError, 'HEADLESS_RUNTIME_MODE_REQUIRED'):
                metrics.Interval.parse(row, 'server')

    def test_adjacent_utc_ranges_are_diagnostic_not_monotonic_authority(self):
        readers = readers_with_rows()
        first = metric(index=0)
        second = metric(index=1)
        second['UtcStart'] = '2026-09-09T00:00:00.500000+00:00'
        readers['server'].intervals = [metrics.Interval.parse(first, 'server'), metrics.Interval.parse(second, 'server')]
        result = metrics.assess(readers, {}, 1, 1)
        self.assertNotIn({'Process': 'server', 'Code': 'UTC_INTERVAL_OVERLAP'}, result['Issues'])
        self.assertIn({'Process': 'server', 'Code': 'UTC_INTERVAL_OVERLAP'}, result['Diagnostics'])
        second['UtcStart'] = '2026-09-09T00:00:01.500000+00:00'
        readers['server'].intervals = [metrics.Interval.parse(first, 'server'), metrics.Interval.parse(second, 'server')]
        result = metrics.assess(readers, {}, 1, 1)
        self.assertNotIn({'Process': 'server', 'Code': 'UTC_INTERVAL_GAP'}, result['Issues'])
        self.assertIn({'Process': 'server', 'Code': 'UTC_INTERVAL_GAP'}, result['Diagnostics'])

    def test_every_measured_process_requires_positive_samples(self):
        readers = readers_with_rows()
        row = metric('client', ticks=0)
        readers['client-00'].intervals = [metrics.Interval.parse(row, 'client')]
        result = metrics.assess(readers, {}, 1, 1)
        self.assertEqual(result['Status'], 'METRICS_INCOMPLETE')
        self.assertIn({'Process': 'client-00', 'Code': 'POSITIVE_SAMPLE_COVERAGE_MISSING'}, result['Issues'])

    def test_precleanup_watermark_drains_buffered_complete_rows_before_stops(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            path = Path(tmp) / 'server.jsonl'
            first = json.dumps(metric(index=0)).encode() + b'\n'
            buffered = json.dumps(metric(index=1)).encode() + b'\n'
            partial = json.dumps(metric(index=2)).encode()[:20]
            path.write_bytes(first + buffered + partial)
            reader = metrics.MetricsReader(path, 'server'); reader.poll()
            self.assertEqual(len(reader.intervals), 2)
            watermark = reader.capture_watermark()
            reader.poll(through=watermark)
            self.assertEqual(len(reader.intervals), 2)
            self.assertEqual(reader.offset, watermark.end_offset)
            self.assertEqual(reader.pending, partial)
            with path.open('ab') as stream:
                stream.write(b'cleanup-only-bytes\n')
            self.assertEqual(reader.offset, watermark.end_offset)

    def test_watermark_rejects_identity_loss_or_truncation(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            path = Path(tmp) / 'server.jsonl'; path.write_text(json.dumps(metric()) + '\n')
            reader = metrics.MetricsReader(path, 'server'); reader.poll()
            watermark = reader.capture_watermark()
            path.write_text('')
            with self.assertRaisesRegex(metrics.MetricsError, 'METRICS_REPLACED_OR_TRUNCATED|METRICS_STOP_BOUNDARY_LOST'):
                reader.poll(through=watermark)

    def test_explicit_stop_boundaries_exclude_cleanup_intervals(self):
        readers = readers_with_rows()
        paused = metric(index=1, seconds=5, ticks=100)
        paused['PausedAny'] = True
        paused['SimulationTickStart'] = 20
        paused['SimulationTickEnd'] = 20
        readers['server'].intervals.append(metrics.Interval.parse(paused, 'server'))
        stops = {label: 1 for label in readers}
        result = metrics.assess(readers, {}, 1, 1, stops=stops)
        self.assertEqual(result['Status'], 'LOCAL_PROTOCOL_RUN_RECORDED')
        self.assertEqual(result['Processes']['server']['DurationSeconds'], 1)

    def test_backwards_utc_remains_diagnostic_not_a_monotonic_failure(self):
        readers = readers_with_rows()
        first = metric(index=0)
        first['UtcEnd'] = '2026-09-08T23:59:59+00:00'
        second = metric(index=1)
        second['UtcStart'] = first['UtcEnd']
        second['UtcEnd'] = '2026-09-08T23:59:58+00:00'
        readers['server'].intervals = [metrics.Interval.parse(first, 'server'), metrics.Interval.parse(second, 'server')]
        result = metrics.assess(readers, {}, 2, 2)
        self.assertNotIn('UTC_INTERVAL_OVERLAP', {issue['Code'] for issue in result['Issues']})
        self.assertNotIn('UTC_INTERVAL_GAP', {issue['Code'] for issue in result['Issues']})
        self.assertEqual(result['Processes']['server']['UtcJumpIntervals'], 2)

    def test_population_range_and_runtime_mode_are_required_for_measured_intervals(self):
        readers = readers_with_rows()
        row = metric()
        row.pop('NpcCountMax')
        row.pop('Batch')
        readers['server'].intervals = [metrics.Interval.parse(row, 'server')]
        result = metrics.assess(readers, {}, 1, 1)
        codes = {issue['Code'] for issue in result['Issues']}
        self.assertIn('POPULATION_RANGE_FIELDS_MISSING', codes)
        self.assertIn('RUNTIME_MODE_UNVERIFIED', codes)
        self.assertEqual(result['Status'], 'METRICS_INCOMPLETE')

    def test_zero_ticks_or_changed_histogram_contract_are_incomplete(self):
        readers = readers_with_rows()
        row = metric(ticks=0)
        readers['server'].intervals = [metrics.Interval.parse(row,'server')]
        self.assertEqual(metrics.assess(readers,{},1,1)['Status'],'METRICS_INCOMPLETE')
        row = metric(index=1); row['TickMilliseconds'] = {'Bounds':[50], 'Counts':[20,0]}
        readers['server'].intervals = [metrics.Interval.parse(metric(),'server'),metrics.Interval.parse(row,'server')]
        self.assertEqual(metrics.assess(readers,{},1,1)['Status'],'METRICS_INCOMPLETE')

    def test_zero_tick_nonempty_interval_is_not_ready_for_measurement(self):
        readers = readers_with_rows()
        row = metric(ticks=0)
        readers['server'].intervals = [metrics.Interval.parse(row, 'server')]
        self.assertFalse(metrics.ready_for_measurement(readers))
        readers = readers_with_rows()
        row = metric('client', ticks=0)
        readers['client-00'].intervals = [metrics.Interval.parse(row, 'client')]
        self.assertFalse(metrics.ready_for_measurement(readers))

    def test_incremental_reader_waits_for_complete_line_and_rejects_duplicates_truncation(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            path = Path(tmp)/'server.jsonl'
            raw = json.dumps(metric()).encode()+b'\n'
            path.write_bytes(raw[:15]); reader = metrics.MetricsReader(path,'server'); reader.poll()
            self.assertEqual(reader.intervals,[])
            with path.open('ab') as stream: stream.write(raw[15:])
            reader.poll(); self.assertEqual(len(reader.intervals),1)
            with path.open('ab') as stream: stream.write(raw)
            reader.poll(); self.assertIn('DUPLICATE_UTC_INTERVAL',reader.errors)
            path.write_bytes(b'')
            with self.assertRaises(metrics.MetricsError):reader.poll()

    def test_semantically_duplicate_utc_intervals_are_rejected_across_offsets(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            path = Path(tmp) / 'server.jsonl'
            first = metric()
            duplicate = metric()
            duplicate['UtcStart'] = '2026-09-09T09:00:00+09:00'
            duplicate['UtcEnd'] = '2026-09-09T09:00:01+09:00'
            path.write_text(json.dumps(first) + '\n' + json.dumps(duplicate) + '\n')
            reader = metrics.MetricsReader(path, 'server'); reader.poll(final=True)
            self.assertEqual(len(reader.intervals), 1)
            self.assertIn('DUPLICATE_UTC_INTERVAL', reader.errors)

    def test_assessment_does_not_accept_duplicate_utc_intervals_when_reader_is_injected(self):
        readers = readers_with_rows(duration=2)
        first = metrics.Interval.parse(metric(seconds=1), 'server')
        duplicate = metric(index=1, seconds=1)
        duplicate['UtcStart'] = '2026-09-09T09:00:00+09:00'
        duplicate['UtcEnd'] = '2026-09-09T09:00:01+09:00'
        readers['server'].intervals = [first, metrics.Interval.parse(duplicate, 'server')]
        result = metrics.assess(readers, {}, 2, 2)
        self.assertEqual(result['Status'], 'METRICS_INCOMPLETE')
        self.assertIn({'Process': 'server', 'Code': 'DUPLICATE_UTC_INTERVAL'}, result['Issues'])

    def test_final_read_drains_multiple_chunks_before_checking_truncated_tail(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            path = Path(tmp) / 'server.jsonl'
            rows = [json.dumps(metric(index=i)) + '\n' for i in range(2200)]
            path.write_text(''.join(rows))
            self.assertGreater(path.stat().st_size, 1024 * 1024)
            reader = metrics.MetricsReader(path, 'server'); reader.poll(final=True)
            self.assertEqual(len(reader.intervals), 2200)
            self.assertEqual(reader.errors, [])

    def test_fault_and_unterminated_records_are_explicit_and_do_not_echo_payload(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            path = Path(tmp)/'server.jsonl'; path.write_text(json.dumps({'Schema':1,'Role':'server','Kind':'fault','Message':'do-not-echo-private-secret'})+'\n'+json.dumps(metric()))
            reader = metrics.MetricsReader(path,'server'); reader.poll(final=True)
            self.assertIn('RUNTIME_METRIC_FAULT',reader.errors)
            self.assertIn('UNTERMINATED_METRIC_LINE',reader.errors)
            self.assertNotIn('private-secret',json.dumps(reader.errors))


class FakeProcess:
    def __init__(self, pid, *, stubborn=False):
        self.pid, self._handle, self.code, self.stubborn = pid, pid+1000, None, stubborn
        self.signals=[]
    def poll(self):return self.code
    def wait(self,timeout=None):
        if self.code is None:raise subprocess.TimeoutExpired('fake',timeout)
        return self.code
    def terminate(self):self.code=-15
    def kill(self):self.code=-9
    def send_signal(self,value):self.signals.append(value); self.code=0


class ProcessOwnership(unittest.TestCase):
    def test_only_owned_posix_groups_are_terminated_and_escalated(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            session=Path(tmp); (session/'logs').mkdir()
            made=[]; calls=[]; options=[]
            def popen(argv,**kwargs):
                value=FakeProcess(5000+len(made)); made.append(value);options.append(kwargs);return value
            def signal_group(pid,value):
                calls.append((pid,value))
                if value==load.signal.SIGKILL:next(p for p in made if p.pid==pid).kill()
            manager=load.OwnedProcesses(session,popen=popen,platform='posix',group_signal=signal_group,
                                        group_probe=lambda pid: False)
            manager.launch('server',['not-executed']); manager.launch('client-00',['not-executed'])
            self.assertTrue(all(o['start_new_session'] and not o['shell'] for o in options))
            manager.close(grace=.01)
            self.assertEqual({pid for pid,_ in calls},{5000,5001})
            self.assertTrue(all(h.closed for h in manager.handles))
            self.assertFalse(manager.incomplete_cleanup)

    def test_posix_reaped_after_term_never_receives_recycled_group_kill(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            session = Path(tmp); (session / 'logs').mkdir()
            state = {'owner': 'original', 'signals': []}
            class ReapingLeader(FakeProcess):
                def wait(self, timeout=None):
                    self.code = 0
                    state['owner'] = 'unrelated-reused-group'
                    return 0
            leader = ReapingLeader(5049)
            def signal_group(pid, value):
                state['signals'].append((pid, int(value), state['owner']))
            manager = load.OwnedProcesses(session, popen=lambda *a, **k: leader, platform='posix',
                                         group_signal=signal_group, clock=lambda: 0.)
            manager.launch('server', ['not-executed']); manager.close(grace=.01)
            self.assertEqual([entry for entry in state['signals'] if entry[1] == int(load.signal.SIGKILL)], [])
            self.assertTrue(manager.incomplete_cleanup)

    def test_posix_timeout_keeps_live_leader_as_kill_authority(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            session = Path(tmp); (session / 'logs').mkdir(); signals = []
            leader = FakeProcess(5048)
            def signal_group(pid, value):
                signals.append((pid, int(value)))
                if value == load.signal.SIGKILL:
                    leader.kill()
            manager = load.OwnedProcesses(session, popen=lambda *a, **k: leader, platform='posix',
                                         group_signal=signal_group, clock=lambda: 0.)
            manager.launch('server', ['not-executed']); manager.close(grace=.01)
            self.assertEqual([value for _, value in signals], [int(load.signal.SIGTERM), int(load.signal.SIGKILL)])
            self.assertFalse(manager.incomplete_cleanup)

    def test_posix_reaped_leader_is_not_blindly_signalled_and_cleanup_is_unverified(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            session=Path(tmp); (session/'logs').mkdir()
            leader=FakeProcess(5050); leader.code=0; calls=[]
            manager=load.OwnedProcesses(session,popen=lambda *a,**k:leader,platform='posix',
                                        group_signal=lambda pid,sig:calls.append((pid,sig)),clock=lambda:0.)
            manager.launch('server',['not-executed']); manager.close(grace=.01)
            self.assertEqual(calls, [])
            self.assertTrue(manager.incomplete_cleanup)

    def test_fault_log_scanning_is_incremental_and_keeps_private_text_out_of_error(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            path=Path(tmp)/'private.log'; path.write_bytes(b'normal startup\nNullReferenceExcep')
            reader=load.FaultLogReader(path); self.assertFalse(reader.poll())
            with path.open('ab') as stream:stream.write(b'tion: private-token-value\n')
            self.assertTrue(reader.poll())
            path.write_bytes(b'')
            with self.assertRaisesRegex(load.LoadError,'PLAYER_LOG_REPLACED_OR_OVERSIZED'):
                reader.poll()

    def test_fault_log_disappearance_after_observation_is_a_safe_failure(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            path = Path(tmp) / 'private.log'; path.write_text('ordinary diagnostics\n')
            reader = load.FaultLogReader(path); self.assertFalse(reader.poll())
            path.unlink()
            with self.assertRaisesRegex(load.LoadError, 'PLAYER_LOG_DISAPPEARED'):
                reader.poll(final=True)

    def test_fault_log_final_poll_drains_beyond_first_chunk(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            path=Path(tmp)/'private.log'
            path.write_bytes(b'x'*(1024*1024+100)+b'fatal error: private-token\n')
            reader=load.FaultLogReader(path)
            self.assertTrue(reader.poll(final=True))
            self.assertEqual(reader.offset, path.stat().st_size)

    def test_fault_log_replacement_is_rejected_even_when_not_shorter(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            path=Path(tmp)/'private.log'; replacement=Path(tmp)/'replacement.log'
            path.write_bytes(b'first\n'); reader=load.FaultLogReader(path); reader.poll()
            replacement.write_bytes(b'second-longer\n'); os.replace(replacement, path)
            with self.assertRaisesRegex(load.LoadError,'PLAYER_LOG_REPLACED_OR_OVERSIZED'):
                reader.poll(final=True)

    def test_signal_failure_is_reported_and_all_log_handles_still_close(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            session=Path(tmp); (session/'logs').mkdir()
            def denied(*args):raise PermissionError('not echoed')
            manager=load.OwnedProcesses(session,popen=lambda *a,**k:FakeProcess(6000),platform='posix',group_signal=denied)
            manager.launch('server',['not-executed']); manager.close(grace=.01)
            self.assertTrue(manager.incomplete_cleanup)
            self.assertTrue(all(h.closed for h in manager.handles))

    def test_clean_exit_before_requested_duration_is_a_fault(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            session=Path(tmp);(session/'logs').mkdir(); signals=[]
            value=FakeProcess(7000); value.code=0
            manager=load.OwnedProcesses(session,popen=lambda *a,**k:value,platform='posix',group_signal=lambda *args:signals.append(args))
            manager.launch('server',['not-executed'])
            with self.assertRaisesRegex(load.LoadError,'SERVER_EARLY_EXIT'):manager.check()
            manager.close()
            self.assertEqual(signals, [])

    def test_failed_launch_closes_stdio_and_does_not_register_a_process(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            session=Path(tmp); (session/'logs').mkdir()
            def denied(*args, **kwargs):
                raise OSError('private child launch detail')
            manager=load.OwnedProcesses(session,popen=denied,platform='posix',group_signal=lambda *a:None)
            with self.assertRaisesRegex(load.LoadError,'PLAYER_LAUNCH_FAILED'):
                manager.launch('server',['not-executed'])
            self.assertEqual(manager.processes, {})
            self.assertTrue(all(handle.closed for handle in manager.handles))
            manager.close()

    def test_interval_parses_fn_simulation_and_un_frame_histograms(self):
        row = metric('server')
        bounds = list(metrics.Histogram.parse(row['TickMilliseconds'], row['TickCount']).bounds)
        sim_counts = [0] * (len(bounds) + 1)
        sim_counts[0] = 2  # 2 ticks in <= 10ms bucket
        frame_counts = [0] * (len(bounds) + 1)
        frame_counts[0] = 10  # 10 frames in <= 10ms bucket
        row['SimulationTickCount'] = 2
        row['SimulationTickMilliseconds'] = {'Bounds': bounds, 'Counts': sim_counts}
        row['FrameCount'] = 10
        row['FrameMilliseconds'] = {'Bounds': bounds, 'Counts': frame_counts}
        row['TickCount'] = 2
        row['TickMilliseconds'] = {'Bounds': bounds, 'Counts': sim_counts}
        interval = metrics.Interval.parse(row, 'server')
        self.assertEqual(interval.simulation_tick_count, 2)
        self.assertEqual(interval.frame_count, 10)
        self.assertEqual(sum(interval.simulation_tick_histogram.counts), 2)
        self.assertEqual(sum(interval.frame_histogram.counts), 10)

    def test_interval_rejects_mismatched_fn_histogram_tick_count(self):
        row = metric('server')
        bounds = list(metrics.Histogram.parse(row['TickMilliseconds'], row['TickCount']).bounds)
        sim_counts = [0] * (len(bounds) + 1)
        sim_counts[0] = 5  # count is 5, but declare 2
        row['SimulationTickCount'] = 2
        row['SimulationTickMilliseconds'] = {'Bounds': bounds, 'Counts': sim_counts}
        with self.assertRaisesRegex(metrics.MetricsError, 'HISTOGRAM_TICK_COUNT'):
            metrics.Interval.parse(row, 'server')

    def test_zero_tick_frame_interval_parses_safely(self):
        row = metric('server')
        bounds = list(metrics.Histogram.parse(row['TickMilliseconds'], row['TickCount']).bounds)
        zero_counts = [0] * (len(bounds) + 1)
        frame_counts = [0] * (len(bounds) + 1)
        frame_counts[0] = 5
        row['TickCount'] = 0
        row['TickMilliseconds'] = {'Bounds': bounds, 'Counts': zero_counts}
        row['SimulationTickCount'] = 0
        row['SimulationTickMilliseconds'] = {'Bounds': bounds, 'Counts': zero_counts}
        row['FrameCount'] = 5
        row['FrameMilliseconds'] = {'Bounds': bounds, 'Counts': frame_counts}
        interval = metrics.Interval.parse(row, 'server')
        self.assertEqual(interval.ticks, 0)
        self.assertEqual(interval.simulation_tick_count, 0)
        self.assertEqual(interval.frame_count, 5)

    def test_windows_assignment_failure_terminates_suspended_process(self):
        events=[]
        process=FakeProcess(7900)
        class Job:
            def assign(self,handle):raise OSError('private assignment detail')
            def resume(self,pid):events.append(('resume',pid))
            def terminate(self):events.append(('terminate_job',)); process.kill()
            def close(self):events.append(('close_job',))
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            session=Path(tmp); (session/'logs').mkdir()
            manager=load.OwnedProcesses(session,popen=lambda *a,**k:process,platform='nt',job_factory=Job)
            with self.assertRaisesRegex(load.LoadError,'PLAYER_LAUNCH_FAILED'):
                manager.launch('server',['not-executed'])
            self.assertIsNotNone(process.poll())
            self.assertTrue(all(handle.closed for handle in manager.handles))
            manager.close()
            self.assertIn(('terminate_job',),events)
            self.assertIn(('close_job',),events)
            self.assertNotIn(('resume',7900),events)

    def test_windows_assignment_failure_retains_child_when_first_kill_is_denied(self):
        class DeniedOnce(FakeProcess):
            def kill(self):
                self.signals.append('kill')
                if len(self.signals) == 1:
                    raise PermissionError('private denied detail')
                self.code = -9
        process = DeniedOnce(7950)
        class Job:
            def assign(self, handle): raise OSError('private assignment detail')
            def resume(self, pid): raise AssertionError('suspended child must not resume')
            def terminate(self): pass
            def close(self): pass
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            session=Path(tmp); (session/'logs').mkdir()
            manager=load.OwnedProcesses(session,popen=lambda *a,**k:process,platform='nt',job_factory=Job,clock=lambda:0.)
            with self.assertRaisesRegex(load.LoadError,'PLAYER_LAUNCH_FAILED'):
                manager.launch('server',['not-executed'])
            self.assertIn('server', manager.processes)
            manager.close(grace=.01)
            self.assertIsNotNone(process.poll())

    def test_windows_process_is_suspended_until_assigned_to_owned_job(self):
        events=[]
        class Job:
            def assign(self,handle):events.append(('assign',handle))
            def resume(self,pid):events.append(('resume',pid))
            def terminate(self):events.append(('terminate_job',))
            def close(self):events.append(('close_job',))
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            session=Path(tmp);(session/'logs').mkdir();options=[]
            def popen(argv,**kwargs):options.append(kwargs);return FakeProcess(8000)
            manager=load.OwnedProcesses(session,popen=popen,platform='nt',job_factory=Job)
            manager.launch('client-00',['not-executed'])
            self.assertTrue(options[0]['creationflags'] & 4)
            self.assertEqual(events[:2],[('assign',9000),('resume',8000)])
            manager.close()
            self.assertIn(('close_job',),events)
            self.assertFalse(manager.incomplete_cleanup)

    def test_pure_run_uses_fake_processes_and_records_without_acceptance_claim(self):
        class Clock:
            now=0.
            def time(self):return self.now
            def sleep(self,seconds):self.now+=seconds
        clock=Clock(); holder={}
        class Manager:
            def __init__(self,session):self.processes={};self.incomplete_cleanup=False;self.closed=False;holder['manager']=self
            def launch(self,label,args):self.processes[label]=FakeProcess(10000+len(self.processes))
            def check(self):
                if any(p.poll() is not None for p in self.processes.values()):raise load.LoadError('CLIENT_EARLY_EXIT')
            def close(self,grace):
                self.closed=True
                for p in self.processes.values():p.code=0
        class Reader:
            def __init__(self,path,role):self.role=role;self.label=path.stem;self.intervals=[];self.errors=[]
            def poll(self,final=False,through=None):
                if self.label in holder['manager'].processes:
                    index = len(self.intervals)
                    row=metric(self.role,index=index,seconds=.2,ticks=4)
                    # The first row completed after readiness may have begun during
                    # warmup. It is a boundary row, not measured load evidence.
                    if self.role == 'server' and index == 21:
                        row['PausedAny'] = True
                        row['SimulationTickEnd'] = row['SimulationTickStart']
                    self.intervals.append(metrics.Interval.parse(row,self.role))
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            session=Path(tmp)
            cfg=load.Config(PROJECT,session,session/'not-executed',duration=1,launch_stagger=0)
            plan={'ServerBinary':'not-executed','ClientBinary':'not-executed','Clients':[{'Label':f'client-{i:02d}','Credential':'private-file','Probe':'private-file'} for i in range(20)],'SourceSha256':{},'SimulationCopySha256':'synthetic'}
            emitted=[]
            result=load.run(cfg,plan,manager_factory=Manager,clock=clock.time,sleep=clock.sleep,reader_factory=Reader,emit=emitted.append)
            self.assertEqual(result['Status'],'LOCAL_PROTOCOL_RUN_RECORDED')
            self.assertEqual(result['Acceptance'],'NOT_ASSESSED')
            self.assertTrue(holder['manager'].closed)
            self.assertEqual(len(holder['manager'].processes),21)
            self.assertNotIn('private-file',json.dumps(emitted))
            self.assertTrue((session/'result.json').is_file())

    def test_final_metric_failure_overrides_an_apparently_complete_run(self):
        class Clock:
            now=0.
            def time(self):return self.now
            def sleep(self,seconds):self.now+=seconds
        clock=Clock(); holder={}
        class Manager:
            def __init__(self,session):self.processes={};self.incomplete_cleanup=False;holder['manager']=self
            def launch(self,label,args):self.processes[label]=FakeProcess(10500+len(self.processes))
            def check(self):pass
            def close(self,grace):
                for process in self.processes.values():process.code=0
        class Reader:
            def __init__(self,path,role):self.role=role;self.label=path.stem;self.intervals=[];self.errors=[]
            def poll(self,final=False,through=None):
                if final:
                    raise metrics.MetricsError('TRUNCATED_PRIVATE_TAIL')
                if self.label in holder['manager'].processes:
                    self.intervals.append(metrics.Interval.parse(metric(self.role,index=len(self.intervals),seconds=.2,ticks=4),self.role))
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            session=Path(tmp)
            cfg=load.Config(PROJECT,session,session/'not-executed',duration=1,launch_stagger=0)
            plan={'ServerBinary':'not-executed','ClientBinary':'not-executed',
                  'ClientBinarySha256':'c'*64,'ServerBinarySha256':'d'*64,
                  'Clients':[{'Label':f'client-{i:02d}','Credential':'private-file','Probe':'private-file'} for i in range(20)],
                  'SourceSha256':{},'SimulationCopySha256':'synthetic'}
            result=load.run(cfg,plan,manager_factory=Manager,clock=clock.time,sleep=clock.sleep,reader_factory=Reader,emit=lambda _:None)
            self.assertEqual(result['Status'],'RUN_FAILED')
            self.assertEqual(result['ProcessFailure'],'FINAL_METRIC_READ_FAILED')

    def test_final_metric_failure_does_not_hide_an_existing_child_failure(self):
        class Clock:
            now=0.
            def time(self):return self.now
            def sleep(self,seconds):self.now+=seconds
        clock=Clock(); holder={}
        class Manager:
            def __init__(self,session):self.processes={};self.incomplete_cleanup=False;holder['manager']=self
            def launch(self,label,args):self.processes[label]=FakeProcess(10600+len(self.processes))
            def check(self):raise load.LoadError('SERVER_EARLY_EXIT')
            def close(self,grace):
                for process in self.processes.values():process.code=1
        class Reader:
            def __init__(self,path,role):self.role=role;self.label=path.stem;self.intervals=[];self.errors=[]
            def poll(self,final=False,through=None):
                if final:self.errors.append('METRICS_FILE_MISSING')
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            session=Path(tmp)
            cfg=load.Config(PROJECT,session,session/'not-executed',duration=1,launch_stagger=0)
            plan={'ServerBinary':'not-executed','ClientBinary':'not-executed',
                  'ClientBinarySha256':'c'*64,'ServerBinarySha256':'d'*64,
                  'Clients':[{'Label':f'client-{i:02d}','Credential':'private-file','Probe':'private-file'} for i in range(20)],
                  'SourceSha256':{},'SimulationCopySha256':'synthetic'}
            result=load.run(cfg,plan,manager_factory=Manager,clock=clock.time,sleep=clock.sleep,reader_factory=Reader,emit=lambda _:None)
            self.assertEqual(result['Status'],'RUN_FAILED')
            self.assertEqual(result['ProcessFailure'],'SERVER_EARLY_EXIT')

    def test_final_metric_error_recorded_without_exception_fails_the_run(self):
        class Clock:
            now=0.
            def time(self):return self.now
            def sleep(self,seconds):self.now+=seconds
        clock=Clock(); holder={}
        class Manager:
            def __init__(self,session):self.processes={};self.incomplete_cleanup=False;holder['manager']=self
            def launch(self,label,args):self.processes[label]=FakeProcess(10750+len(self.processes))
            def check(self):pass
            def close(self,grace):
                for process in self.processes.values():process.code=0
        class Reader:
            def __init__(self,path,role):self.role=role;self.label=path.stem;self.intervals=[];self.errors=[]
            def poll(self,final=False,through=None):
                if final:
                    self.errors.append('UNTERMINATED_METRIC_LINE')
                elif self.label in holder['manager'].processes:
                    self.intervals.append(metrics.Interval.parse(metric(self.role,index=len(self.intervals),seconds=.2,ticks=4),self.role))
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            session=Path(tmp)
            cfg=load.Config(PROJECT,session,session/'not-executed',duration=1,launch_stagger=0)
            plan={'ServerBinary':'not-executed','ClientBinary':'not-executed',
                  'ClientBinarySha256':'c'*64,'ServerBinarySha256':'d'*64,
                  'Clients':[{'Label':f'client-{i:02d}','Credential':'private-file','Probe':'private-file'} for i in range(20)],
                  'SourceSha256':{},'SimulationCopySha256':'synthetic'}
            result=load.run(cfg,plan,manager_factory=Manager,clock=clock.time,sleep=clock.sleep,reader_factory=Reader,emit=lambda _:None)
            self.assertEqual(result['Status'],'RUN_FAILED')
            self.assertEqual(result['ProcessFailure'],'FINAL_METRIC_READ_FAILED')

    def test_result_receipt_binds_plan_and_actual_summary_hash_without_private_paths(self):
        class Clock:
            now=0.
            def time(self):return self.now
            def sleep(self,seconds):self.now+=seconds
        clock=Clock(); holder={}
        class Manager:
            def __init__(self,session):self.processes={};self.incomplete_cleanup=False;holder['manager']=self
            def launch(self,label,args):self.processes[label]=FakeProcess(11000+len(self.processes))
            def check(self):pass
            def close(self,grace):
                for process in self.processes.values():process.code=0
        class Reader:
            def __init__(self,path,role):self.role=role;self.label=path.stem;self.intervals=[];self.errors=[]
            def poll(self,final=False,through=None):
                if self.label in holder['manager'].processes:
                    self.intervals.append(metrics.Interval.parse(metric(self.role,index=len(self.intervals),seconds=.2,ticks=4),self.role))
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            session=Path(tmp)
            cfg=load.Config(PROJECT,session,session/'private-player',duration=1,launch_stagger=0)
            plan={'ServerBinary':'private-server','ClientBinary':'private-client',
                  'ClientBinarySha256':'c'*64,'ServerBinarySha256':'d'*64,
                  'Clients':[{'Label':f'client-{i:02d}','Credential':'private-credential','Probe':'private-probe'} for i in range(20)],
                  'SourceSha256':{'world':'a'*64,'world_profile':'b'*64,'simulation':'e'*64},
                  'SimulationCopySha256':'f'*64}
            result=load.run(cfg,plan,manager_factory=Manager,clock=clock.time,sleep=clock.sleep,reader_factory=Reader,emit=lambda _:None)
            receipt=result['PublicationSafeReceipt']
            self.assertEqual(receipt['Acceptance'],'NOT_ASSESSED')
            self.assertEqual(receipt['Scope'],'local-headless-protocol-load-metrics')
            self.assertEqual(receipt['SummarySha256'],load.summary_sha256(result))
            self.assertEqual(receipt['MeasurementWindow'],{
                'RequiredDurationSeconds': 1, 'ObservedWallSeconds': result['ObservedWallSeconds']})
            self.assertEqual(receipt['PlanCanonicalSha256'],load.canonical_sha256(plan))
            self.assertEqual(set(receipt['MetricFilesSha256']),{'server',*[f'client-{i:02d}' for i in range(20)]})
            self.assertTrue(all(value is None for value in receipt['MetricFilesSha256'].values()))
            self.assertEqual(receipt['SourceSha256'],plan['SourceSha256'])
            self.assertEqual(receipt['SimulationCopySha256'],plan['SimulationCopySha256'])
            self.assertEqual(receipt['ClientBinarySha256'],plan['ClientBinarySha256'])
            self.assertEqual(receipt['ServerBinarySha256'],plan['ServerBinarySha256'])
            serialized=json.dumps(receipt)
            self.assertNotIn(str(session),serialized)
            self.assertNotIn('private-',serialized)


# ---------------------------------------------------------------------------
# FMP-12a-R1 load causal corpus (R6B items 1-5).
#
# Each case below is a function that executes the real code path and returns the
# observed values. The same function backs both the behaviour gate under the
# required unittest command and the appended corpus record written by
# --emit-corpus, so a recorded observation is never a hand-typed expectation.
# ---------------------------------------------------------------------------

def journal_fixture(parent):
    """Minimal Schema3 journal: one checkpoint, one approved command, one boundary."""
    participant = {'ParticipantId': 'a', 'ObservedIds': []}
    state = {'SchemaVersion': 3, 'WorldId': 'w', 'ShiftId': 's', 'SimulationDefinitionHash': 'd' * 64,
             'SimulationTick': 0, 'Sequence': 0, 'Participants': [participant], 'Frames': [], 'Entities': [],
             'Reports': [], 'Receipts': []}

    def envelope(kind, ordinal, sequence, tick, previous, body):
        plain = json.dumps(body, separators=(',', ':')).encode()
        compressor = zlib.compressobj(wbits=-zlib.MAX_WBITS)
        payload = compressor.compress(plain) + compressor.flush()
        result = {'Schema': 3, 'Kind': kind, 'WorldId': 'w', 'ShiftId': 's', 'DefinitionHash': 'd' * 64,
                  'Ordinal': ordinal, 'Sequence': sequence, 'Tick': tick, 'PreviousHash': previous,
                  'PlainBytes': len(plain), 'Payload': base64.b64encode(payload).decode()}
        result['Hash'] = journal.digest(result)
        return result

    checkpoint = envelope('checkpoint', 0, 0, 0, journal.EMPTY_HASH, state)
    person = {'ParticipantId': 'a', 'ObservedIds': ['incident-1']}
    receipt = {'WorldId': 'w', 'ShiftId': 's', 'ParticipantId': 'a', 'CommandId': 'discover', 'Fingerprint': 'f' * 64,
               'Code': 0, 'Sequence': 1}
    command = {'WorldSchemaVersion': 3, 'SimulationTick': 20, 'SimulationDefinitionHash': 'd' * 64, 'Receipt': receipt,
               'Participants': [person], 'Entities': [], 'Frames': [], 'Reports': [],
               'SimulationCheckpoint': 'private-cp', 'Paused': False}
    first = envelope('command', 1, 1, 20, journal.EMPTY_HASH, command)
    boundary = {'Sequence': 1, 'Participants': [person], 'Entities': [], 'Frames': [], 'Checkpoint': 'private-cp-2',
                'Paused': False}
    second = envelope('boundary', 2, 1, 40, first['Hash'], boundary)
    parent.mkdir(parents=True, exist_ok=True)
    (parent / 'checkpoint-v3.json').write_text(json.dumps(checkpoint))
    (parent / 'actions-v3.jsonl').write_text(json.dumps(first) + '\n' + json.dumps(second) + '\n')
    return parent


def case_c01_observation_schema_normal():
    with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
        observation = journal.observe_journal(journal_fixture(Path(tmp) / 'records'))
        derived = journal.judgment_input(observation)
    return {'ObservationName': observation['ObservationName'],
            'ObservationSchema': observation['ObservationSchema'],
            'ObservationKeys': sorted(observation),
            'JudgmentInputSchema': derived['JudgmentInputSchema'],
            'JudgmentInputKeys': sorted(derived),
            'JudgmentInputEmbedsReadback': sorted(set(derived) & journal.OBSERVATION_ONLY_KEYS),
            'ObservationRefName': derived['ObservationRef']['ObservationName'],
            'SimulationTick': derived['SimulationTick'], 'Sequence': derived['Sequence'],
            'TruncatedTailBytes': derived['TruncatedTailBytes']}


def case_c01_observation_schema_mismatch():
    with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
        observation = journal.observe_journal(journal_fixture(Path(tmp) / 'records'))
    observation['ObservationSchema'] = journal.JUDGMENT_INPUT_SCHEMA
    try:
        journal.judgment_input(observation)
    except journal.ProbeError as exc:
        return {'Code': str(exc)}
    return {'Code': None}


def case_c01_observation_field_set():
    with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
        observation = journal.observe_journal(journal_fixture(Path(tmp) / 'records'))
    # An undeclared top-level key (the raw private state) is not the declared field set.
    observation['State'] = observation['Readback']['State']
    try:
        journal.judgment_input(observation)
    except journal.ProbeError as exc:
        return {'Code': str(exc)}
    return {'Code': None}


def case_c01_judgment_input_leak():
    with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
        observation = journal.observe_journal(journal_fixture(Path(tmp) / 'records'))
        derived = journal.judgment_input(observation)
    leaked = dict(derived)
    leaked['State'] = observation['Readback']['State']
    try:
        journal.validate_judgment_input(leaked)
    except journal.ProbeError as exc:
        return {'Code': str(exc)}
    return {'Code': None}


def case_c02_exact_variant():
    return {'Selector': 'expected_kind=command_fault',
            'Case': metrics.dispatch_fault_case(expected_kind='command_fault')}


def case_c02_grouped_marker():
    return {'Selector': 'grouped_marker=command processing stopped',
            'Case': metrics.dispatch_fault_case(grouped_marker='command processing stopped')}


def case_c02_dispatch_agreement():
    agreement = metrics.assert_fault_dispatch_agreement()
    return {'Cases': sorted(agreement), 'AgreeingSelectors': len(metrics.EXACT_FAULT_VARIANTS) + len(metrics.GROUPED_FAULT_MARKERS)}


def case_c02_grouped_divergence():
    # A grouped marker that names a case with no exact variant is the divergence the
    # review found: the log channel would select a case the metrics channel cannot.
    injected = metrics.GROUPED_FAULT_MARKERS + (('unmapped fault token', 'orphan_marker_fault'),)
    with mock.patch.object(metrics, 'GROUPED_FAULT_MARKERS', injected):
        try:
            metrics.assert_fault_dispatch_agreement()
        except metrics.MetricsError as exc:
            return {'Code': str(exc)}
    return {'Code': None}


def case_c02_metrics_fault_row():
    row = {'Schema': 1, 'Role': 'server', 'Kind': 'fault', 'FaultKind': 'command_fault',
           'Message': 'do-not-echo-private-secret'}
    try:
        metrics.Interval.parse(row, 'server')
    except metrics.MetricsError as exc:
        return {'Code': str(exc), 'Leaked': 'private-secret' in str(exc)}
    return {'Code': None, 'Leaked': False}


def case_c02_metrics_fault_row_unknown_variant():
    row = {'Schema': 1, 'Role': 'server', 'Kind': 'fault', 'FaultKind': 'orphan_fault'}
    try:
        metrics.Interval.parse(row, 'server')
    except metrics.MetricsError as exc:
        return {'Code': str(exc)}
    return {'Code': None}


def case_c02_fault_log_reader_case():
    """The log channel must report the same canonical case as the exact variant."""
    with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
        path = Path(tmp) / 'server.player.log'
        path.write_bytes(b'ordinary startup\n')
        reader = load.FaultLogReader(path)
        before = reader.poll()
        with path.open('ab') as stream:
            stream.write(b'command processing stopped: private-token-value\n')
        matched = reader.poll()
    return {'Before': before, 'Case': matched,
            'Markers': [marker.decode('ascii') for marker in reader.MARKERS]}


def case_c02_fault_log_dispatch_ambiguous():
    with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
        path = Path(tmp) / 'server.player.log'
        path.write_bytes(b'fatal error: exception: both groups on one line\n')
        reader = load.FaultLogReader(path)
        try:
            reader.poll()
        except load.LoadError as exc:
            return {'Code': str(exc)}
    return {'Code': None}


def case_c03_server_role_normal():
    interval = metrics.Interval.parse(metric('server'), 'server')
    return {'Role': interval.role, 'Segments': interval.connected}


def case_c03_client_role_normal():
    interval = metrics.Interval.parse(metric('client'), 'client')
    return {'Role': interval.role, 'Segments': interval.connected}


def case_c03_wrong_role_server_reader_client_row():
    try:
        metrics.Interval.parse(metric('client'), 'server')
    except metrics.MetricsError as exc:
        return {'Code': str(exc)}
    return {'Code': None}


def case_c03_wrong_role_client_reader_server_row():
    try:
        metrics.Interval.parse(metric('server'), 'client')
    except metrics.MetricsError as exc:
        return {'Code': str(exc)}
    return {'Code': None}


def case_c03_invalid_reader_role():
    try:
        metrics.MetricsReader(ROOT / 'not-used', 'observer')
    except metrics.MetricsError as exc:
        return {'Code': str(exc)}
    return {'Code': None}


def case_c03_invalid_expected_role():
    try:
        metrics.Interval.parse(metric('server'), 'observer')
    except metrics.MetricsError as exc:
        return {'Code': str(exc)}
    return {'Code': None}


def case_c04_cleanup_begin_reference_missing():
    readers = readers_with_rows(duration=2)
    result = metrics.assess(readers, {}, 2, 2, overall_deadline_seconds=22.0)
    return {'Issues': sorted(issue['Code'] for issue in result['Issues']),
            'CleanupBeginSeconds': result['TerminalDeadline']['CleanupBeginSeconds']}


class SyntheticClock:
    def __init__(self):
        self.now = 0.

    def time(self):
        return self.now

    def sleep(self, seconds):
        self.now += seconds


def synthetic_run(*, duration=1, metrics_grace=10., shutdown_grace=10., reader_factory=None, scripted=None):
    """Execute the real run() against injected processes and readers."""
    clock = SyntheticClock()
    holder = {}

    class Manager:
        def __init__(self, session):
            self.processes = {}
            self.incomplete_cleanup = False
            self.closed = False
            holder['manager'] = self

        def launch(self, label, args):
            self.processes[label] = FakeProcess(12000 + len(self.processes))

        def check(self):
            pass

        def close(self, grace):
            self.closed = True
            for process in self.processes.values():
                process.code = 0

    class Reader:
        def __init__(self, path, role):
            self.role = role
            self.label = path.stem
            self.intervals = []
            self.errors = []
            self.path = path

        def poll(self, final=False, through=None):
            if self.label in holder['manager'].processes and not final and through is None:
                row = metric(self.role, index=len(self.intervals), seconds=.2, ticks=4)
                self.intervals.append(metrics.Interval.parse(row, self.role))

    if scripted is not None:
        reader_factory = None
    with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
        session = Path(tmp)
        config = load.Config(PROJECT, session, session / 'not-executed', duration=duration,
                             metrics_grace=metrics_grace, shutdown_grace=shutdown_grace, launch_stagger=0)
        plan = {'ServerBinary': 'not-executed', 'ClientBinary': 'not-executed',
                'ClientBinarySha256': 'c' * 64, 'ServerBinarySha256': 'd' * 64,
                'Clients': [{'Label': f'client-{i:02d}', 'Credential': 'private-credential',
                             'Probe': 'private-probe'} for i in range(20)],
                'SourceSha256': {'world': 'a' * 64, 'world_profile': 'b' * 64, 'simulation': 'e' * 64},
                'SimulationCopySha256': 'f' * 64}
        result = load.run(config, plan, manager_factory=Manager, clock=clock.time, sleep=clock.sleep,
                          reader_factory=reader_factory or Reader, emit=lambda _: None)
    return result


def case_c04_runner_cleanup_begin_reference():
    result = synthetic_run()
    return {'Status': result['Status'], 'CleanupBegin': result['CleanupBegin'],
            'TerminalDeadline': result['TerminalDeadline']}


def case_c05_deadline_before():
    readers = readers_with_rows(duration=2)
    result = metrics.assess(readers, {}, 2, 2.0, cleanup_begin_seconds=2.0, overall_deadline_seconds=22.0)
    return {'Issues': sorted(issue['Code'] for issue in result['Issues']),
            'DeadlineOutcome': result['TerminalDeadline']['DeadlineOutcome'],
            'TerminalReserveSign': result['TerminalDeadline']['TerminalReserveSign'],
            'TerminalReserveSeconds': result['TerminalDeadline']['TerminalReserveSeconds'],
            'DeadlineIssues': sorted(code for code in (issue['Code'] for issue in result['Issues'])
                                     if 'DEADLINE' in code or 'CLEANUP_BEGIN' in code)}


def case_c05_deadline_equal():
    readers = readers_with_rows(duration=2)
    result = metrics.assess(readers, {}, 2, 22.0, cleanup_begin_seconds=22.0, overall_deadline_seconds=22.0)
    return {'Issues': sorted(issue['Code'] for issue in result['Issues']),
            'DeadlineOutcome': result['TerminalDeadline']['DeadlineOutcome'],
            'TerminalReserveSign': result['TerminalDeadline']['TerminalReserveSign'],
            'TerminalReserveSeconds': result['TerminalDeadline']['TerminalReserveSeconds'],
            'DeadlineIssues': sorted(code for code in (issue['Code'] for issue in result['Issues'])
                                     if 'DEADLINE' in code or 'CLEANUP_BEGIN' in code)}


def case_c05_deadline_after():
    readers = readers_with_rows(duration=2)
    result = metrics.assess(readers, {}, 2, 23.0, cleanup_begin_seconds=23.0, overall_deadline_seconds=22.0)
    return {'Issues': sorted(issue['Code'] for issue in result['Issues']),
            'DeadlineOutcome': result['TerminalDeadline']['DeadlineOutcome'],
            'TerminalReserveSign': result['TerminalDeadline']['TerminalReserveSign'],
            'TerminalReserveSeconds': result['TerminalDeadline']['TerminalReserveSeconds'],
            'DeadlineIssues': sorted(code for code in (issue['Code'] for issue in result['Issues'])
                                     if 'DEADLINE' in code or 'CLEANUP_BEGIN' in code)}


def case_c05_deadline_contradiction():
    readers = readers_with_rows(duration=2)
    result = metrics.assess(readers, {}, 2, 23.0, cleanup_begin_seconds=23.0, overall_deadline_seconds=22.0,
                            declared_deadline_outcome='before')
    return {'DeadlineIssues': sorted(code for code in (issue['Code'] for issue in result['Issues'])
                                     if 'DEADLINE' in code),
            'DeclaredOutcomeMatchesObservation':
                result['TerminalDeadline']['DeclaredOutcomeMatchesObservation']}


CORPUS_CASES = (
    ('c01-observation-schema-normal', 'CC01', 'normal_sibling', case_c01_observation_schema_normal),
    ('c01-observation-schema-mismatch', 'CC01', 'negative', case_c01_observation_schema_mismatch),
    ('c01-observation-field-set', 'CC01', 'negative', case_c01_observation_field_set),
    ('c01-judgment-input-leak', 'CC01', 'negative', case_c01_judgment_input_leak),
    ('c02-exact-variant-selects-case', 'CC02', 'normal_sibling', case_c02_exact_variant),
    ('c02-grouped-marker-selects-case', 'CC02', 'normal_sibling', case_c02_grouped_marker),
    ('c02-dispatch-agreement', 'CC02', 'normal_sibling', case_c02_dispatch_agreement),
    ('c02-grouped-marker-divergence', 'CC02', 'negative', case_c02_grouped_divergence),
    ('c02-metrics-fault-row-exact-variant', 'CC02', 'normal_sibling', case_c02_metrics_fault_row),
    ('c02-metrics-fault-row-unknown-variant', 'CC02', 'negative', case_c02_metrics_fault_row_unknown_variant),
    ('c02-fault-log-reader-case', 'CC02', 'normal_sibling', case_c02_fault_log_reader_case),
    ('c02-fault-log-dispatch-ambiguous', 'CC02', 'negative', case_c02_fault_log_dispatch_ambiguous),
    ('c03-server-role-normal', 'CC03', 'normal_sibling', case_c03_server_role_normal),
    ('c03-client-role-normal', 'CC03', 'normal_sibling', case_c03_client_role_normal),
    ('c03-wrong-role-server-reader', 'CC03', 'negative', case_c03_wrong_role_server_reader_client_row),
    ('c03-wrong-role-client-reader', 'CC03', 'negative', case_c03_wrong_role_client_reader_server_row),
    ('c03-invalid-reader-role', 'CC03', 'negative', case_c03_invalid_reader_role),
    ('c03-invalid-expected-role', 'CC03', 'negative', case_c03_invalid_expected_role),
    ('c04-cleanup-begin-reference-missing', 'CC04', 'negative', case_c04_cleanup_begin_reference_missing),
    ('c04-runner-cleanup-begin-reference', 'CC04', 'normal_sibling', case_c04_runner_cleanup_begin_reference),
    ('c05-deadline-before', 'CC05', 'normal_sibling', case_c05_deadline_before),
    ('c05-deadline-equal', 'CC05', 'normal_sibling', case_c05_deadline_equal),
    ('c05-deadline-after', 'CC05', 'normal_sibling', case_c05_deadline_after),
    ('c05-deadline-contradiction', 'CC05', 'negative', case_c05_deadline_contradiction),
)


def emit_corpus(index_path, *, command, exit_code):
    """Append one complete run record per corpus case to an append-only index."""
    path = Path(index_path)
    record = {'recordKind': 'complete_run_record', 'command': command, 'exitCode': exit_code,
              'cases': [{'caseId': case_id, 'claimId': claim, 'variant': variant, 'observed': function()}
                        for case_id, claim, variant, function in CORPUS_CASES]}
    if path.is_file():
        document = json.loads(path.read_text())
        document['runHistory'].append(record)
    else:
        document = {'schemaVersion': 1, 'classification': 'PUBLIC_PROJECT_CONTEXT', 'workId': 'FMP-12a-R1',
                    'issue': 149, 'domainCode': 'QA', 'phase': 'candidate',
                    'artifact': 'artifact:149:load-causal-corpus:candidate', 'appendOnly': True,
                    'rawReceiptPolicy': 'Append complete sanitized run records; never overwrite prior raw '
                                        'XML/log/receipt, failure or NOT_RUN evidence.',
                    'runHistory': [record], 'supersedes': None}
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(document, ensure_ascii=False, indent=2) + '\n')
    return document


class LoadCausalContract(unittest.TestCase):
    """Behaviour gate for the five R6B items; each case is also a corpus record."""

    def test_c01_observation_name_and_schema_are_separate_from_the_judgment_input(self):
        observed = case_c01_observation_schema_normal()
        self.assertEqual(observed['ObservationName'], journal.OBSERVATION_NAME)
        self.assertEqual(observed['ObservationSchema'], journal.OBSERVATION_SCHEMA)
        self.assertEqual(observed['JudgmentInputSchema'], journal.JUDGMENT_INPUT_SCHEMA)
        self.assertEqual(observed['JudgmentInputEmbedsReadback'], [])
        self.assertEqual(observed['SimulationTick'], 40)

    def test_c01_renamed_schema_undeclared_fields_and_embedded_readback_are_refused(self):
        self.assertEqual(case_c01_observation_schema_mismatch()['Code'], 'OBSERVATION_SCHEMA_MISMATCH')
        self.assertEqual(case_c01_observation_field_set()['Code'], 'OBSERVATION_FIELD_SET')
        self.assertEqual(case_c01_judgment_input_leak()['Code'], 'JUDGMENT_INPUT_CARRIES_PRIVATE_OBSERVATION')

    def test_c02_exact_variant_and_grouped_marker_select_the_same_case(self):
        exact = case_c02_exact_variant()
        grouped = case_c02_grouped_marker()
        self.assertEqual(exact['Case'], grouped['Case'])
        self.assertEqual(exact['Case'], 'command_marker_P_fault')
        self.assertEqual(case_c02_dispatch_agreement()['Cases'], list(metrics.FAULT_CASES))

    def test_c02_log_channel_reports_the_same_canonical_case(self):
        observed = case_c02_fault_log_reader_case()
        self.assertIsNone(observed['Before'])
        self.assertEqual(observed['Case'], case_c02_exact_variant()['Case'])
        self.assertEqual(observed['Case'], case_c02_grouped_marker()['Case'])
        self.assertEqual(case_c02_fault_log_dispatch_ambiguous()['Code'], 'PLAYER_FAULT_LOG_DISPATCH_AMBIGUOUS')

    def test_c02_grouped_marker_without_an_exact_variant_is_a_divergence(self):
        self.assertEqual(case_c02_grouped_divergence()['Code'], 'FAULT_DISPATCH_DIVERGENCE')
        exact = case_c02_metrics_fault_row()
        self.assertEqual(exact['Code'], 'RUNTIME_METRIC_FAULT')
        self.assertFalse(exact['Leaked'])
        self.assertEqual(case_c02_metrics_fault_row_unknown_variant()['Code'], 'FAULT_KIND_UNKNOWN')

    def test_c03_legacy_reader_roles_and_wrong_role_refusals(self):
        self.assertEqual(case_c03_server_role_normal()['Role'], 'server')
        self.assertEqual(case_c03_client_role_normal()['Role'], 'client')
        self.assertEqual(case_c03_wrong_role_server_reader_client_row()['Code'], 'ROLE_MISMATCH')
        self.assertEqual(case_c03_wrong_role_client_reader_server_row()['Code'], 'ROLE_MISMATCH')
        self.assertEqual(case_c03_invalid_reader_role()['Code'], 'INVALID_READER_ROLE')
        self.assertEqual(case_c03_invalid_expected_role()['Code'], 'INVALID_EXPECTED_ROLE')

    def test_c04_cleanup_begin_is_a_recorded_reference_not_an_omission(self):
        reference = case_c04_runner_cleanup_begin_reference()
        self.assertEqual(reference['Status'], 'LOCAL_PROTOCOL_RUN_RECORDED')
        self.assertIsNotNone(reference['CleanupBegin']['ObservedSeconds'])
        self.assertIsNotNone(reference['CleanupBegin']['LocalDeadlineSeconds'])
        self.assertTrue(reference['CleanupBegin']['WithinLocalDeadline'])
        self.assertEqual(reference['TerminalDeadline']['DeadlineOutcome'], 'before')
        missing = case_c04_cleanup_begin_reference_missing()
        self.assertIn('CLEANUP_BEGIN_REFERENCE_MISSING', missing['Issues'])

    def test_c05_all_three_deadline_siblings_are_realizable_and_neutral(self):
        for name, case in (('before', case_c05_deadline_before), ('equal', case_c05_deadline_equal),
                           ('after', case_c05_deadline_after)):
            observed = case()
            self.assertEqual(observed['DeadlineOutcome'], name)
            self.assertEqual(observed['DeadlineIssues'], [])
            self.assertEqual(observed['Issues'], [])
        self.assertEqual(case_c05_deadline_before()['TerminalReserveSign'], 'positive')
        self.assertEqual(case_c05_deadline_equal()['TerminalReserveSign'], 'zero')
        self.assertEqual(case_c05_deadline_after()['TerminalReserveSign'], 'negative')

    def test_c05_declared_sibling_contradicting_the_reserve_is_a_failure(self):
        observed = case_c05_deadline_contradiction()
        self.assertIn('DEADLINE_OUTCOME_CONTRADICTION', observed['DeadlineIssues'])
        self.assertFalse(observed['DeclaredOutcomeMatchesObservation'])


if __name__ == '__main__':
    if '--emit-corpus' in sys.argv:
        index = sys.argv[sys.argv.index('--emit-corpus') + 1]
        emit_corpus(index, command=' '.join(sys.argv), exit_code=0)
        print(json.dumps({'Status': 'CORPUS_EMITTED', 'Index': index, 'Cases': len(CORPUS_CASES)}))
    else:
        unittest.main()
