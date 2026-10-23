"""Offline source interpretation and exact frozen transport geometry extraction only."""
import hashlib
import json
import re
from collections import defaultdict
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from shapely.geometry import Polygon
from shapely.ops import unary_union

ROOT = Path('.state/ACTIVE-ALL2F-PHOTO-TWIN-R18')
OUT = ROOT / 'research/east-curved-end'
M = np.array([[-.34290633335024717, -.9393696006089878],
              [-.9393696006089878, .3429063333502471]])


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def q_points(world):
    return np.asarray(world)[:, [0, 2]] @ M.T


def main():
    cp = ROOT / 'inputs/CURRENT-EAST-CIRCULATION-CONSTRUCTION-R18.json'
    ap = ROOT / 'inputs/CURRENT-WALLS-AND-NATIVE-FEATURE-SECTIONS-R18.json'
    circ = json.loads(cp.read_text())
    actual = json.loads(ap.read_text())
    frames = json.loads((OUT / 'SOURCE-FRAME-EXTRACTION.json').read_text())
    for r in frames:
        r['sha256'] = digest(Path(r['path']))
        m = re.search(r'pts_time:([0-9.]+)', r['showinfo'][0])
        r['exact_retained_clip_pts_seconds'] = float(m.group(1))
        r['original_video_timestamp'] = None
        r['time_qualification'] = 'Decoder PTS in retained clip; +215 s is not an exact upstream timestamp.'

    groups = defaultdict(list)
    upper_contact_parts = []
    for r in circ['current_east_circulation_parts']:
        w = np.asarray(r['vertices_world'])
        q = q_points(w)
        group = next(x for x in r['path'].split('/') if x in
                     ['C158_EAST_ESC_UP', 'C158_EAST_ESC_DOWN', 'C158_EAST_ATTACHED_FIXED_STAIR'])
        ref = {'object_id': r['object_id'], 'path': r['path'], 'mesh_asset': r['mesh_asset'],
               'q_bounds_min': q.min(axis=0).tolist(), 'q_bounds_max': q.max(axis=0).tolist(),
               'world_y': [float(w[:, 1].min()), float(w[:, 1].max())]}
        groups[group].append(ref)
        faces = []
        polys = []
        for sm, indices in enumerate(r['submesh_triangles']):
            if not indices:
                continue
            for ti, ids in enumerate(np.asarray(indices, dtype=int).reshape(-1, 3)):
                t = w[ids]
                tq = q_points(t)
                area = abs(np.linalg.det(np.asarray([tq[1]-tq[0], tq[2]-tq[0]]))) / 2
                if t[:, 1].min() >= 6.979 and t[:, 1].max() <= 7.006 and area > .015:
                    polys.append(Polygon(tq))
                    faces.append({'submesh': sm, 'triangle_index_in_submesh': ti,
                                  'vertex_indices': ids.tolist(), 'triangle_q': tq.tolist(),
                                  'world_y': t[:, 1].tolist(), 'projected_area_m2': float(area),
                                  'material_asset': r['materials'][sm] if sm < len(r['materials']) else None})
        if faces:
            union = unary_union(polys)
            geoms = [union] if union.geom_type == 'Polygon' else list(union.geoms)
            ref.update({'function': 'ACTUAL_CURRENT_2F_UPPER_STAIR_CONTACT_OR_TREAD',
                        'selection': 'Actual horizontal mesh triangles with all vertex Y in [6.979,7.006], projected area > .015 m2; not a source-inferred facade.',
                        'triangle_refs': faces,
                        'rings_q': [{'outer': list(map(list, g.exterior.coords)),
                                     'holes': [list(map(list, h.coords)) for h in g.interiors]} for g in geoms]})
            upper_contact_parts.append(ref)

    transport_groups = []
    for group, parts in groups.items():
        transport_groups.append({'id': group, 'part_count': len(parts),
                                 'q_bounds_min': np.min([p['q_bounds_min'] for p in parts], axis=0).tolist(),
                                 'q_bounds_max': np.max([p['q_bounds_max'] for p in parts], axis=0).tolist(),
                                 'world_y': [min(p['world_y'][0] for p in parts), max(p['world_y'][1] for p in parts)],
                                 'parts': parts, 'preservation': 'FIXED_USER_MOVED_CURRENT_TRANSPORT_NO_CHANGE'})

    columns = [r for r in actual['independent_columns_q_register']
               if 38 < r['q_bounds_min'][0] < 98 and 0 < r['q_bounds_min'][1] < 35]
    for r in columns:
        r['mesh_body_bounds_center_q'] = ((np.asarray(r['q_bounds_min']) + np.asarray(r['q_bounds_max'])) / 2).tolist()
        r['source_individual_identity'] = 'INSUFFICIENT; retained storefront/steel pier has no unique current column ID.'
    front_walls = [r for r in actual['nonWC_walls_q_register']
                   if r['q_bounds_max'][0] >= 40 and r['q_bounds_min'][0] <= 98
                   and r['q_bounds_max'][1] >= 20 and r['q_bounds_min'][1] <= 55]

    native_counts = []
    for r in actual['fixed_native_multiheight_sections']:
        counts = {}
        for y, segs in r['actual_material_triangle_sections_world'].items():
            count = 0
            for sm, ti, a, b in segs:
                q = q_points([a, b])
                if q[:, 0].max() >= 40 and q[:, 0].min() <= 98 and q[:, 1].max() >= 20 and q[:, 1].min() <= 55:
                    count += 1
            if count:
                counts[y] = count
        if counts:
            native_counts.append({'object_id': r['object_id'], 'path': r['path'], 'matching_level_ROI_section_counts': counts,
                                  'qualification': 'Fixed source geometry present; not classified as a new shop facade or occupied room from this count/projection.'})

    guide = ROOT / 'research/root/current-guide-2f-original-20260419.jpg'
    bnc_dir = Path('asset-library/research-public/2026-09-21/fps-research/interiors')
    bnc_sources = []
    for i in [2, 3, 4, 19, 20, 24]:
        p = bnc_dir / f'bnc-busan-2f-2022-{i:02}.jpg'
        bnc_sources.append({'path': str(p), 'sha256': digest(p), 'filename_level_not_authority': True})
    packet = {
        'status': 'BOUNDED_SOURCE_BINDING_COMPLETE_NO_UNSUPPORTED_FACADE_PLACEMENT',
        'region': 'Guide 29 Soban / 30 BnC / 31 Subway eastern curved-front end',
        'actual_input': {'path': str(cp), 'utc': circ['utc'], 'sha256': digest(cp), 'parts': len(circ['current_east_circulation_parts'])},
        'other_current_input': {'path': str(ap), 'utc': actual['utc'], 'sha256': digest(ap)},
        'coordinate_convention': {'world_to_q_xz_matrix': M.tolist(), 'vertical': 'Unity world Y',
                                  'drawing': '(qx,-qy)', 'source_pixels_or_walk_seconds_are_metric': False},
        'conclusion': {
            'new_world_facade_or_return_spans_supported': 0,
            'reason': 'The source at 266.533/268.501 s directly shows StoryWay and Lounge beside an ascending SR-crew bank, with SUBWAY on an overhead direction panel. It does not show the individual Subway31 or Soban29 storefront contacting a uniquely identified C158 upper landing/column. The current C158 1F-to-2F bank and the source SR-crew ascending bank must remain separate hypotheses.',
            'not_a_completion_claim': 'No unique individual storefront-to-current-body binding or 99 percent twin claim.'},
        'guide_semantic_topology': {'path': str(guide), 'sha256': digest(guide),
                                    'classification': 'directly_supported',
                                    'order_from_west_to_east_on_lower_curved_band': ['27 Korail Lounge (Tripin)', '28 CVS', '29 Soban', '30 BnC', '31 Subway', 'east WC/outer stair neighborhood'],
                                    'qualification': 'Guide is schematic and dated 2026-04-19; block centres, widths and distance to escalator symbol are not metric extent.'},
        'source_observations': [
            {'id': 'SOURCE-DESCENDING-EXIT-WELL', 'frame_pts': [256.523, 259.526, 262.528],
             'classification': 'directly_supported',
             'observed': 'Yellow Way out sign/down arrow, recessed downward exit bank/well between tall stainless-clad piers. At 262.528 s the separate ascending SR-crew bank also appears at right.',
             'current_binding': 'INSUFFICIENT individual C158 bank identity; do not substitute an upper-bank sign for a mesh contact.'},
            {'id': 'SOURCE-SR-ASCENDING-BANK', 'frame_pts': [262.528, 266.533, 268.501],
             'classification': 'directly_supported',
             'observed': 'Escalator and adjacent fixed stair rise away from the source concourse toward a higher SR 부산승무센터 landing. The bank foot is beside a stainless pier and the retail passage.',
             'current_binding': 'contradicts equating this ascending bank directly with the designated Y7 upper endpoint of C158 whose walking surface descends toward Y0.02. Absolute real-world storey correspondence remains unqualified.'},
            {'id': 'SOURCE-RETAIL-PASSAGE', 'frame_pts': [266.533, 268.501, 272.506],
             'classification': 'directly_supported',
             'observed': 'Green overhead panels name SUBWAY / StoryWay / 나베야. Literal visible storefront is StoryWay; adjoining Korail Lounge frontage is visible at right. SUBWAY text is a direction/destination panel, not proof that the visible glass/fascia is the Subway31 frontage.',
             'feature_profile': ['dark blue horizontal fascia with fine upper ribs', 'clear glazed retail front/transom', 'stainless structural pier', 'existing open retail passage'],
             'construction_limit': 'No new portal width/count/location from this view.'},
            {'id': 'SOURCE-BNC-LATER-ROUTE', 'frame_pts': [358.523, 378.51],
             'classification': 'near_match_only',
             'observed': 'Later native frames show a BnC-branded bakery display and another open customer face, stainless pier and nearby circulation guard. This resembles retained BnC photos but lacks the unique current C158 landing/column contact needed for guide30 world placement.',
             'construction_limit': 'Do not propagate the later bakery facade across the unbound route or assign a structural bay by walking duration.'}
        ],
        'retained_BnC_package': {'sources': bnc_sources,
                                 'direct_text': [{'path': str(bnc_dir/'bnc-busan-2f-2022-19.jpg'), 'text': 'B&C 부산 KTX역 1층점 신축인테리어공사; 건물현황 ... 1층 입점'},
                                                 {'path': str(bnc_dir/'bnc-busan-2f-2022-20.jpg'), 'text': '1층 평면도 계획안'}],
                                 'classification': 'contradicts_premise',
                                 'contradicted_premise': 'The filename string bnc-busan-2f alone proves the document is an as-built 2F guide30 placement source.',
                                 'qualification': 'The document is a proposal and its printed floor designation is 1F. This does not by itself prove every separately retained actual BnC photo is another store; those photos require individual current level/bay confirmation.',
                                 'observed_photo_morphology': ['two or more customer-facing sides', 'dark navy fascia', 'light timber panelled display bases', 'clear upper glazing', 'stainless adjoining pier', 'one side close to an escalator/guard'],
                                 'metric_adoption': 'None. Proposal 7.49 m dimension is not assigned to a current bay.'},
        'actual_fixed_transport_groups': transport_groups,
        'actual_2F_upper_contact_geometry': upper_contact_parts,
        'current_structural_columns_neighborhood': columns,
        'generated_nonWC_front_ROI_inventory': {'roi_q': [40,20,98,55], 'matching_parts': front_walls,
                                              'qualification': 'Only the 117 independent nonWC wall owners queried. Empty result is not proof that native/retail physical walls are absent.'},
        'fixed_native_ROI_sources_to_preserve': native_counts,
        'proposed_metric_entities': [],
        'supported_action': 'Preserve the exact current C158 footprint/upper contacts and source-visible open passage. Do not create new Subway/BnC/Soban facades until an actual storefront-to-fixed-pier/landing connection is identified.',
        'next_exact_source_needed': [
            {'id': 'SUBWAY31-C158-JOINT', 'required_view': 'A real Subway31 front/return and east WC or the current accepted C158 upper landing in the same continuous view, including the stainless pier foot and floor contact. Distinguish the SR-crew higher bank from the exit-down bank.'},
            {'id': 'BNC30-SOBAN29-COLUMN-ORDER', 'required_view': 'An unbroken reverse angle from Subway31 past BnC30 to Soban29, with two adjacent current column bodies and the same floor/void edge visible. Then exact current column faces and fixed native boundary can bound the infill; guide boxes alone cannot.'}
        ],
        'excluded_aliases': ['Native fixed stair object1440906411 (wrong physical region/level)', 'C158 parent/root pivot as occupied geometry', 'retired C21/C22 store placement', 'guide-box metric bounds'],
        'source_frames': frames,
        'limits': {'new_source_frames': len(frames), 'new_source_frame_bytes': sum(r['bytes'] for r in frames),
                   'new_network_download_bytes': 0, 'Scene_Assets_modified': False, 'native_UI_calls': False,
                   'QA_tests_probes_captures': False, 'source_frame_extraction_only': True,
                   'native_transport_and_Native6_fixed': True}
    }
    # Exact PTS metadata controls report numbers, rather than handwritten nominal times.
    for observation in packet['source_observations']:
        nominal = observation['frame_pts']
        observation['frame_pts'] = [min(frames, key=lambda r: abs(r['exact_retained_clip_pts_seconds']-t))['exact_retained_clip_pts_seconds'] for t in nominal]
    (OUT/'CONSTRUCTION-PACKET.json').write_text(json.dumps(packet, ensure_ascii=False, indent=2))
    board = Image.new('RGB', (1600, 5*245), 'white')
    draw = ImageDraw.Draw(board)
    for i, r in enumerate(frames):
        im = Image.open(r['path']); im.thumbnail((395,220))
        x, y = i % 4 * 400, i // 4 * 245
        board.paste(im, (x,y+22)); draw.text((x+4,y+4), f"clip PTS {r['exact_retained_clip_pts_seconds']:.3f}s", fill='black')
    board.save(OUT/'SOURCE-ROUTE-CONTACT-SHEET.jpg', quality=87)
    (OUT/'findings.md').write_text('''# East curved-front bounded source result

15 retained-video frames (3,284,902 bytes; zero network downloads) and current 225-part C158 geometry are indexed in `CONSTRUCTION-PACKET.json`.

The 262.528 s frame distinguishes an exit-down well and a separate SR-crew ascending escalator/stair. The visible SUBWAY text at 266–272 s is an overhead destination panel; the directly visible fronts are StoryWay and Lounge. No individual Subway31/Soban29 facade-to-current-column contact is established. Do not identify the SR ascending bank with C158 merely from stair adjacency.

Exact current C158 upper walking-contact triangles/rings are included for components495 and508. The 225 current transport parts and nearby structural columns remain fixed. No new facade endpoints or entry positions are proposed.

The retained `bnc-busan-2f-2022` name is misleading as document-level authority: image19 explicitly calls its proposal a 1F store, and image20 says 1F plan. Actual BnC photos still need their own level/bay binding; this is not a blanket assertion that every photo is 1F. Proposal dimensions and guide boxes were not used to place geometry.

Needed next evidence is one uninterrupted reverse angle showing Subway31/BnC30/Soban29 and a unique current pier-foot/upper-landing or WC contact. The requested new curved-front facade/return remains unplaced rather than assigned an arbitrary bay. Scene/Assets/native/UI untouched.
''')
    print(json.dumps({'packet': str(OUT/'CONSTRUCTION-PACKET.json'), 'transport_parts': len(circ['current_east_circulation_parts']),
                      'upper_contact_parts': len(upper_contact_parts), 'column_refs': len(columns),
                      'new_facade_spans': 0, 'frames': len(frames), 'frame_bytes': sum(r['bytes'] for r in frames)}, indent=2))


if __name__ == '__main__':
    main()
