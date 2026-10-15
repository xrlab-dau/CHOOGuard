#!/usr/bin/env python3
"""Aggregate actual simulator evidence without converting missing proof to success."""
from pathlib import Path
import argparse
import hashlib
import json
import sys

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def current_sources(data, key='sources'):
    mismatches = []
    for source, digest in (data or {}).get(key, {}).items():
        path = ROOT / source
        if not path.is_file() or sha(path) != digest:
            mismatches.append(source)
    return mismatches


def read(path):
    return json.loads(path.read_text()) if path.is_file() else None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=ROOT / 'output/simulator/acceptance.json')
    args = parser.parse_args()
    current_manifest=read(ROOT/'output/simulator/platform-ktx-unity/Assets/Simulator/Resources/SourceManifest.json') or {}
    if current_manifest.get('world_snapshot'):
        from whole_world_acceptance import aggregate
        return aggregate(args.output)
    checks = []

    def add(name, passed, evidence, details=None):
        checks.append({'check': name, 'status': 'PASS' if passed else 'PENDING_OR_FAIL',
                       'evidence': str(evidence), 'details': details})

    for name in ['last-test.json', 'last-replay.json', 'unity-source-compile.json']:
        path = ROOT / 'output/simulator/checks' / name
        data = read(path)
        mismatches = current_sources(data)
        add(name, bool(data) and data.get('exit_code') == 0 and not mismatches and not data.get('source_changed_during_run', False) and not data.get('source_changed_during_compile', False),
            path, {'current_source_mismatches': mismatches})

    project = ROOT / 'output/simulator/platform-ktx-unity'
    manifest_path = project / 'Assets/Simulator/Resources/SourceManifest.json'
    manifest = read(manifest_path) or {}
    mismatches = []
    source_entries = {**manifest.get('runtime_sources', {}), **manifest.get('editor_sources', {})}
    for source, expected in source_entries.items():
        original = ROOT / 'workers/simulator' / source
        staged = project / 'Assets/Simulator' / source
        if not original.is_file() or not staged.is_file() or sha(original) != expected or sha(staged) != expected:
            mismatches.append(source)
    originals = list((ROOT / 'workers/simulator/Runtime').rglob('*.cs')) + list((ROOT / 'workers/simulator/Editor').glob('*.cs'))
    for source in originals:
        relative = str(source.relative_to(ROOT / 'workers/simulator'))
        if relative not in source_entries:
            mismatches.append('not_staged:' + relative)
    for original_name, staged_name in [('platform-source.json', 'PlatformSource.json'), ('semantic-coverage.json', 'SemanticCoverage.json'), ('semantic-coverage-car04-candidate.json', 'Car04Candidate.json'), ('car04-access.json', 'Car04Access.json')]:
        original = ROOT / 'workers/simulator/data' / original_name
        staged = project / 'Assets/Simulator/Resources' / staged_name
        if not original.is_file() or not staged.is_file() or sha(original) != sha(staged):
            mismatches.append('resource:' + staged_name)
    asset_mismatches = []
    for source, expected in manifest.get('assets', {}).items():
        original, staged = ROOT / source, project / source
        if not original.is_file() or not staged.is_file() or sha(original) != expected['sha256'] or sha(staged) != expected['sha256']:
            asset_mismatches.append(source)
    add('current_isolated_sources_and_original_asset_bytes', bool(manifest.get('runtime_fingerprint')) and not mismatches and not asset_mismatches, manifest_path, {'source_mismatches': mismatches, 'asset_mismatches': asset_mismatches, 'asset_count': len(manifest.get('assets', {}))})
    geometry = project / 'Verification/geometry-nav-probes.json'
    data = read(geometry)
    add('actual_unity_source_geometry', bool(data) and data.get('source_scene_modified') is False and data.get('source_assembly_hash') == manifest.get('runtime_fingerprint'),
        geometry, 'Actual isolated engine report required; compilation does not substitute.')
    if data:
        doors = data.get('navigation', {}).get('doors', [])
        add('qualified_open_door_paths', bool(doors) and all(row.get('linked') and row.get('outside_floor') and row.get('inside_floor') and row.get('complete_path') for row in doors), geometry, doors)
        passage = data.get('physical_door_passage', [])
        add('actual_capsule_door_passage', bool(passage) and all(row.get('passed') for row in passage), geometry, passage)
        replay = data.get('recorded_observation_replay', {})
        add('actual_unity_recorded_observation_replay', replay.get('passed') is True, geometry, replay)
    else:
        for name in ['qualified_open_door_paths', 'actual_capsule_door_passage', 'actual_unity_recorded_observation_replay']:
            add(name, False, geometry, 'Engine report not produced.')

    play_path = project / 'Verification/playmode-probes.json'
    play = read(play_path) or {}
    current_play = play.get('source_assembly_hash') == manifest.get('runtime_fingerprint') and bool(manifest.get('runtime_fingerprint'))
    transitions = play.get('door_state_transitions', [])
    add('actual_open_closed_restored_door_paths', current_play and len(transitions) == 7 and all(row.get('passed') is True and row.get('closed_capsule', {}).get('passed') is False and (row.get('closed_complete_path') is False or row.get('closed_alternative_avoids_aperture') is True) for row in transitions), play_path, transitions)
    add('actual_cordon_carve_remove', current_play and play.get('local_cordon', {}).get('passed') is True, play_path, play.get('local_cordon'))
    npc = play.get('npc_actual_movement', {})
    add('actual_npc_movement_and_core_arrival', current_play and npc.get('passed') is True and npc.get('core_observed_completion') is True and npc.get('displacement', 0) > 1, play_path, npc)
    add('actual_fps_seed_and_interaction', current_play and play.get('fps_interaction', {}).get('passed') is True and all(play.get('seed_ui_contract', {}).get(key) is True for key in ['invalid_seed_refused', 'new_seed_is_uint64', 'same_seed_initial_digest', 'started']), play_path)
    add('actual_playmode_recorded_observation_replay', current_play and play.get('recorded_observation_replay', {}).get('passed') is True, play_path)
    car04 = play.get('car04_passenger_access', {})
    add('actual_car04_source_passenger_access', current_play and car04.get('passed') is True and car04.get('source_seat_count') == 44 and car04.get('external_door_blocked') is True,
        play_path, car04 or 'Production adapter with source cabin doors, local navigation and actual actor feedback required.')

    for filename in ['fps-platform.png', 'platform-ktx-overview.png']:
        path = project / 'Verification' / filename
        add('actual_render_' + filename, path.is_file() and path.stat().st_size > 10000, path,
            'Artifact presence only; orchestrator visual inspection is separately required.')

    review_path = project / 'Verification/orchestrator-visual-review.json'
    review = read(review_path) or {}
    add('actual_graphics_and_visual_review', play.get('real_graphics_captured') is True and play.get('graphics_device') not in [None, 'Null'] and review.get('passed') is True and all((project / 'Verification' / name).is_file() and sha(project / 'Verification' / name) == digest for name, digest in review.get('images', {}).items()) and len(review.get('images', {})) >= 2, review_path)

    coverage_path = ROOT / 'workers/simulator/data/semantic-coverage.json'
    coverage = read(coverage_path)
    coverage_mismatches = []
    for source in (coverage or {}).get('sources', []):
        original = ROOT / source['path']
        if not original.is_file() or sha(original) != source['sha256']:
            coverage_mismatches.append(source['path'])
    add('source_qualified_coverage_manifest', bool(coverage) and not coverage_mismatches and len(coverage.get('visual_parts', [])) == coverage.get('report', {}).get('visual_parts') and coverage.get('report', {}).get('ids_unique') is True, coverage_path,
        'Counts and unsupported-object ledger require source/contract validation.')

    model_path = ROOT / 'output/simulator/model-benchmark/results.json'
    model = read(model_path)
    add('actual_local_generation_and_token_benchmark', bool(model) and model.get('status') == 'measured' and model.get('actual_generator_calls', 0) > 0 and model.get('source_changed_during_run') is False and model.get('matched_observation_hashes') is True and model.get('matched_requested_context_hashes') is True and not current_sources(model, 'source_hashes') and model.get('coverage_hash') == sha(coverage_path),
        model_path, 'Fixture replies or token estimates do not count as measured inference.')

    guard_path = ROOT / 'output/simulator/checks/replay-implementation-guard.json'
    guard = read(guard_path) or {}
    add('replay_implementation_mismatch_refused', guard.get('passed') is True and not current_sources(guard), guard_path)

    initial = play.get('initial_generation_integration', {})
    add('actual_initial_generation_and_offline_record_replay', current_play and initial.get('passed') is True, play_path,
        initial or 'Actual Unity integration fixture required; provider authentication remains separate.')

    auth_path = ROOT / 'output/simulator/provider-auth-tests.json'
    auth = read(auth_path) or {}
    add('browser_oauth_local_contract_tests', auth.get('exit_code') == 0 and auth.get('test_count', 0) > 0 and bool(auth.get('source_hashes')) and not current_sources(auth, 'source_hashes'), auth_path, 'Local mocked-provider verification; does not substitute for the end user browser login and subscription inference.')

    native_path = ROOT / 'output/simulator/native-auth-bridge.json'
    native = read(native_path) or {}
    add('actual_csharp_http_subscription_bridge', native.get('passed') is True and native.get('selection_routing_passed') is True and native.get('providers_tested') == 2 and native.get('initial_and_npc_same_selection') is True and native.get('mock_completed_inferences') == 4 and native.get('stale_session_prevented_extra_call') is True and native.get('source_changed_during_run') is False and not current_sources(native, 'source_hashes'), native_path,
        'Real localhost C# HTTP: two synthetic providers each route initial conditions and NPC plans through one identical selection tuple. No provider subscription was consumed.')

    initial_dir = ROOT / 'output/simulator/provider-live/final'
    context = read(initial_dir / 'native-context-report.json') or {}
    context_tool = read(initial_dir / 'native-tool-receipt.json') or {}
    provenance = read(initial_dir / 'request-source-receipt.json') or {}
    add('whole_world_compact_graph_exact_roundtrip', context.get('graph_roundtrip_exact') is True and context_tool.get('exit_code') == 0 and not context_tool.get('source_changed_during_run', True) and not current_sources(context_tool)
        and provenance.get('runtime_fingerprint') == manifest.get('runtime_fingerprint'), initial_dir / 'native-context-report.json', context)
    live_path = initial_dir / 'subscription-live.json'
    live = read(live_path) or {}
    applied = read(initial_dir / 'native-applied/native-context-report.json') or {}
    applied_tool = read(initial_dir / 'native-applied/native-tool-receipt.json') or {}
    live_pass = live.get('live_provider_inference') is True and live.get('disposition') == 'completed_contract_passed_pending_native_apply' and applied.get('disposition') in ('accepted', 'accepted_no_change')
    live_pass = live_pass and applied.get('replayed_ticks') == 40 and applied.get('network_calls') == 0 and applied.get('basis_digest') == context.get('basis_digest') and applied_tool.get('exit_code') == 0 and not current_sources(applied_tool) and not applied_tool.get('source_changed_during_run', True)
    add('actual_subscription_generation_and_native_replay', live_pass, live_path, {'provider': live, 'native_apply_replay': applied})

    result = {'schema': 'chooguard-acceptance-v2', 'implemented_scope': 'isolated platform/KTX simulator',
              'all_required_evidence_present': all(row['status'] == 'PASS' for row in checks),
              'all_functional_checks_passed': all(row['status'] == 'PASS' for row in checks if not row['check'].startswith('actual_render_') and row['check'] not in ['actual_graphics_and_visual_review','actual_subscription_generation_and_native_replay']),
              'checks': checks,
              'external_verification_pending': ([] if live_pass else ['Completed end-user subscription inference and native initial-state replay']) + ['Site measurement, railway procedure review and participant effectiveness evaluation'],
              'graphics_verification_pending': 'Automation host fails in macOS LaunchServices before graphics startup; inspect rendered views from a normal desktop session.',
              'held_source_scope': 'Car04 platform-facing external door remains blocked behind the original luggage rack. Qualified passenger access uses two source cabin doors and original intercar floors; the earlier 40 heuristic seat candidates are not used.',
              'claim_boundary': 'Evidence aggregate does not certify site measurement, all-world semantic completeness, live JEV economics, or training effectiveness.'}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'pass': sum(row['status'] == 'PASS' for row in checks), 'total': len(checks),
                      'complete': result['all_required_evidence_present'], 'report': str(args.output)}))
    return 0 if result['all_required_evidence_present'] else 2


if __name__ == '__main__':
    sys.exit(main())
