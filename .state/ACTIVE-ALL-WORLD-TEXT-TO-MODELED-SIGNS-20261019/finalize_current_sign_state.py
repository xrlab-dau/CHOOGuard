"""Summarize settled construction saves; never invokes Unity or changes Assets."""
from pathlib import Path
from datetime import datetime, timezone
import fcntl
import json
import os
import tempfile

ROOT = Path('/Users/um-yunsang/CHOOGuard')
STATE = ROOT / '.state'
D = '.state/ACTIVE-ALL-WORLD-TEXT-TO-MODELED-SIGNS-20261019'

def read(path):
    return json.loads((ROOT / path).read_text())

def write(path, value):
    dest = ROOT / path
    fd, temp = tempfile.mkstemp(prefix=dest.name + '.', dir=dest.parent)
    with os.fdopen(fd, 'w') as f:
        json.dump(value, f, ensure_ascii=False, indent=2)
        f.write('\n')
    os.replace(temp, dest)

receipts = [D + '/APPLIED-ALL-WORLD-TEXT-REMOVED.json'] + [
    D + '/APPLIED-SOURCE-CONTOUR-SIGNS-' + floor + '.json' for floor in ['1F', '2F']]
results = [read(path) for path in receipts]
assert all(r['status'].startswith('APPLIED_SAVED') for r in results)
assert results[0]['remaining_world_text_components_in_constructor'] == 0
assert results[2]['remaining_world_text_components_at_constructor_save'] == 0
requests = ['remove-all-world-text-create-physical-signs-20261019',
            'direct-1f-source-contour-signs-save-20261019',
            'direct-2f-source-contour-signs-save-20261019']
settled = []
for request in requests:
    matches = list((STATE / 'SCENE-WORKER-C180/results').glob(request + '--*.json'))
    success = [p for p in matches if (lambda d: d.get('settled') and d.get('status') == 'SETTLED' and d.get('success') is True)(json.loads(p.read_text()))]
    if len(success) != 1:
        raise RuntimeError('Require one exact actual successful settled receipt: ' + request)
    settled.append(str(success[0].relative_to(ROOT)))

now = datetime.now(timezone.utc).isoformat()
current = {
    'status': 'APPLIED_SAVED_ALL_WORLD_TEXT_REMOVED_AND_SOURCE_SIGNS_DIRECTLY_MODELED',
    'utc': now,
    'user_authority': D + '/USER-AUTHORITY.json',
    'scene': 'Assets/ChooGuard/Scenes/FpsStation.unity',
    'actual_saved_receipts': receipts,
    'actual_settled_broker_receipts': settled,
    'removed_world_text_objects_or_components': results[0]['removed_text_objects_or_components'],
    'inactive_world_text_included': True,
    'remaining_world_text_at_last_construction_save': 0,
    'physical_sign_and_number_assemblies_created': results[0]['physical_sign_and_number_assemblies'],
    'printed_surfaces_replaced_by_closed_source_contours': {
        '1F': results[1]['actual_print_faces_replaced_by_geometry'],
        '2F': results[2]['actual_print_faces_replaced_by_geometry'],
        'total': sum(r['actual_print_faces_replaced_by_geometry'] for r in results[1:])},
    'shared_mapped_meshes': {r['floor']: r['shared_contour_mesh_count'] for r in results[1:]},
    'unique_mapped_triangles': {r['floor']: r['unique_triangles'] for r in results[1:]},
    'preserved_primary_2f_sign_state': '.state/CURRENT-2F-ACTUAL-PHYSICAL-SIGNS-20261019.json',
    'runtime_assets': 'Assets/ChooGuard/Art/StationInterior/DirectWorldSignGeometry_20261019',
    'source_art_geometry': 'Closed continuous source contour solids with counters; untextured source-color materials.',
    'font_atlas_or_photo_plane_substitute_used': False,
    'unsupported_Lovely_Cosmetics_removed': True,
    'numeric_marks': 'Existing MODEL sequence201-263 retained as closed outlines; not field-confirmed numbering.',
    'source_limits': 'Original blur, source epoch and missing card edges remain qualified; no unreadable text invented.',
    'fixed_native_floor_columns_booths_equipment_positions_changed': False,
    'original_source_assets_changed': False,
    'scene_Light_components_created': 0,
    'runtime_screen_ui_changed': False,
    'validation_owner': 'user',
    'QA_tests_probes_reopen_captures': False,
    'surveyed_or_complete_digital_twin_equality_claimed': False,
}

with (STATE / 'SCENE-WORKER-QUEUE-C180.lock').open('a') as lock:
    fcntl.flock(lock, fcntl.LOCK_EX)
    queue = read('.state/SCENE-WORKER-QUEUE-C180.json')
    if queue.get('active_request') in requests:
        raise RuntimeError('Own native request is not yet released')
    write('.state/CURRENT-DIRECT-MODELED-WORLD-SIGNS-20261019.json', current)
    old = read('.state/CURRENT-2F-ACTUAL-PHYSICAL-SIGNS-20261019.json')
    old.update(utc=now, status='APPLIED_SAVED_ACTUAL_SOURCE_2F_SOLID_CONTOUR_SIGNS',
               latest_all_world_text_correction='.state/CURRENT-DIRECT-MODELED-WORLD-SIGNS-20261019.json',
               current_secondary_contour_surface_receipt=receipts[2],
               current_2f_print_faces_replaced_by_geometry=results[2]['actual_print_faces_replaced_by_geometry'],
               photo_only_sign_sufficiency_superseded=True,
               previous_1f_no_change_scope_superseded_by_latest_user_correction=True)
    if 'secondary_actual_source_print_and_mesh_receipt' in old:
        old['superseded_secondary_photo_print_receipt'] = old.pop('secondary_actual_source_print_and_mesh_receipt')
    old.pop('all1F_signs_preserved', None)
    write('.state/CURRENT-2F-ACTUAL-PHYSICAL-SIGNS-20261019.json', old)
    text = read('.state/CURRENT-WORLD-TEXT-REMOVAL-20261019.json')
    text['subsequent_direct_sign_geometry_state'] = '.state/CURRENT-DIRECT-MODELED-WORLD-SIGNS-20261019.json'
    write('.state/CURRENT-WORLD-TEXT-REMOVAL-20261019.json', text)
    authority = read('.state/WRITER-AUTHORITY.json')
    authority['current_direct_modeled_world_signs'] = '.state/CURRENT-DIRECT-MODELED-WORLD-SIGNS-20261019.json'
    authority['current_world_text_removal'] = '.state/CURRENT-WORLD-TEXT-REMOVAL-20261019.json'
    write('.state/WRITER-AUTHORITY.json', authority)
    policy = read('.state/SCENE-WORKER-POLICY-C180.json')
    for worker in policy['allowed_workers']:
        if worker.get('shop_id') == 'ALL_WORLD_TEXT_DIRECT_SIGN_GEOMETRY':
            worker['completed_scope_prefixes'] = worker['allowed_scope_prefixes'] or worker.get('completed_scope_prefixes', [])
            worker['allowed_scope_prefixes'] = []
            worker['completion_status'] = 'ACTUAL_SAVES_SETTLED_AND_RELEASED'
            worker['completed_utc'] = now
            worker['completion_receipt'] = '.state/CURRENT-DIRECT-MODELED-WORLD-SIGNS-20261019.json'
    write('.state/SCENE-WORKER-POLICY-C180.json', policy)

agents = ROOT / 'AGENTS.md'
text = agents.read_text()
marker = 'Latest ALL-world-text correction (2026-10-19):'
start = text.index(marker)
end = text.index('\n\n', start)
applied = (' Actual correction is APPLIED/SAVED: all95world TextMesh/TMP components, including26inactive, '
           'removed; last constructor remaining count0. Created68physical sign/number assemblies and '
           'replaced117printed surfaces (70on1F,47on2F) with closed source-contour meshes and untextured '
           'source-color materials. Current compact receipt `.state/CURRENT-DIRECT-MODELED-WORLD-SIGNS-20261019.json`. '
           'Existing numeric sequence remains MODEL, not field-confirmed. Preserve this correction and '
           'never replay the superseded photo-only sign replacements.')
if 'Actual correction is APPLIED/SAVED:' not in text[start:end]:
    agents.write_text(text[:end] + applied + text[end:])

plan = ROOT / '.planning/2026-10-19-1f-lockers-free-3d-assets/task_plan.md'
text = plan.read_text().replace(
    '8. Serialized direct removal/replacement/save and actual receipt — in_progress (all95worldText removed/saved;117printed surfaces awaiting contour batch)',
    '8. Serialized direct removal/replacement/save and actual receipt — complete (95worldText removed;117printed faces directly modeled/saved)'
).replace('9. Update exact current authority and finish — pending', '9. Update exact current authority and finish — complete')
plan.write_text(text)
with (plan.parent / 'progress.md').open('a') as f:
    f.write('\n- Final2Fbatch APPLIED/SAVED and actual broker settlement SUCCESS/released.47faces replaced; '
            f"{results[2]['shared_contour_mesh_count']}shared mapped meshes; remaining worldText count0inactualconstructor. "
            'All117print faces and95text removal integrated in CURRENT-DIRECT-MODELED-WORLD-SIGNS-20261019.json. '
            'Only completed root sign author prefix retired; othercurrentauthors and sceneLight deletion preserved. '
            'No agentQA/Play/probes/reopen/captures performed.\n')
print(json.dumps({k: current[k] for k in ['status', 'removed_world_text_objects_or_components',
      'printed_surfaces_replaced_by_closed_source_contours', 'remaining_world_text_at_last_construction_save']}, ensure_ascii=False))
