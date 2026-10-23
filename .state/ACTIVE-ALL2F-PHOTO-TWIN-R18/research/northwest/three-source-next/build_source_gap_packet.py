#!/usr/bin/env python3
"""Read retained evidence and write a non-applicable, bounded source-gap packet."""
from __future__ import annotations

import hashlib
import json
import math
from datetime import datetime, timezone
from pathlib import Path

import pymupdf

ROOT = Path(__file__).resolve().parents[5]
OUT = Path(__file__).resolve().parent
R18 = ROOT / '.state/ACTIVE-ALL2F-PHOTO-TWIN-R18'
INPUT = R18 / 'inputs/CURRENT-WALLS-AND-NATIVE-FEATURE-SECTIONS-R18.json'
PDF = ROOT / 'asset-library/research-public/2026-10-09/floor2-architectural-pdf-search/official-100083811-store-plan.pdf'
ORIGINAL = ROOT / 'workers/twin/evidence/floor2-architectural-pdf-search-20261009/official-100083313-store-plan-page-1-embedded-2.jpeg'
PHOTO = ROOT / 'asset-library/research-public/2026-10-08/measured-twin/photos/IMG20231215080637.jpg'
RECEIPT = R18 / 'receipts/APPLIED-THREE-BOUNDED-UPPER-FINISHES-R18.json'
Q = ((-.34290633335024717, -.9393696006089878),
     (-.9393696006089878, .34290633335024717))


def rel(p):
    return str(p.relative_to(ROOT))


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def q(v):
    return [Q[0][0] * v[0] + Q[0][1] * v[2],
            Q[1][0] * v[0] + Q[1][1] * v[2]]


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def minus(a, b):
    return [x - y for x, y in zip(a, b)]


def normal(a, b, c):
    u, v = minus(b, a), minus(c, a)
    n = [u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0]]
    size = math.sqrt(dot(n, n))
    return [x/size for x in n] if size else [0, 0, 0]


def by_id(rows, oid):
    return next(x for x in rows if x['object_id'].endswith(f'-{oid}-0'))


def metadata(x):
    return {k: x[k] for k in ['object_id', 'path', 'mesh_asset', 'mesh_object_id',
                              'world_matrix', 'bounds_min_world', 'bounds_max_world']}


def q_aabb_from_world_bounds(x):
    lo, hi = x['bounds_min_world'], x['bounds_max_world']
    pts = [q([a, 0, b]) for a in (lo[0], hi[0]) for b in (lo[2], hi[2])]
    return [min(p[0] for p in pts), min(p[1] for p in pts),
            max(p[0] for p in pts), max(p[1] for p in pts)]


def main():
    data = json.loads(INPUT.read_text())
    owner = by_id(data['nonWC_walls'], 1327299834)
    g = owner['geometry']
    verts = g['vertices_world']
    basis_indices = g['submesh_triangles'][0][98*3:99*3]
    bv = [verts[i] for i in basis_indices]
    nw = normal(*bv)
    nq = [Q[0][0]*nw[0]+Q[0][1]*nw[2], Q[1][0]*nw[0]+Q[1][1]*nw[2]]
    tangent = [-nq[1], nq[0]]
    plane = dot(nq, q(bv[0]))
    support = []
    ss = []
    for si, arr in enumerate(g['submesh_triangles']):
        keep = []
        for offset in range(0, len(arr), 3):
            vv = [verts[i] for i in arr[offset:offset+3]]
            if dot(normal(*vv), nw) > .99999 and max(abs(dot(nq, q(v))-plane) for v in vv) < .0001:
                keep.append(offset//3)
                ss.extend(dot(tangent, q(v)) for v in vv)
        if keep:
            support.append({'submesh': si, 'triangle_indices_in_submesh': keep})
    full = [min(ss), max(ss)]
    # This is the already-saved finish's conservative endpoint, not a new placement.
    saved_stop_qy = 10.865978660803682
    stop_s = (saved_stop_qy - nq[1]*plane) / tangent[1]
    def at(s):
        return [nq[i]*plane + tangent[i]*s for i in range(2)]

    wc = [by_id(data['other_2F_mesh_metadata'], oid) for oid in (1417713925, 2041710588)]
    pdf = pymupdf.open(PDF)
    page = pdf[0]
    labels = []
    for block in page.get_text('dict')['blocks']:
        for line in block.get('lines', []):
            for span in line['spans']:
                if span['text'] in ['새부산약국', '아메리칸', '트레일러']:
                    labels.append({'text': span['text'], 'bounds_pdf_pt': list(span['bbox']),
                                   'role': 'semantic_locator_only_not_wall_geometry'})
    embedded_hash = hashlib.sha256(pdf.extract_image(36)['image']).hexdigest()
    source_page = 'https://www.korailretail.com/board/boardDetail.do?boardTypeNum=4&boardNum=100083811'
    packet = {
        'schema': 'chooguard.r18.northwest.three_source_next.v1',
        'status': 'INSUFFICIENT_UNIQUE_OWNER_BINDING_AND_SERVICE_PIER_PROFILE',
        'utc': datetime.now(timezone.utc).isoformat(),
        'apply_ready': False,
        'candidate_detail_count': 1,
        'ready_construction_count': 0,
        'additions': [], 'deletions': [], 'material_changes': [],
        'scope': 'ONE previously unworked pharmacy-right service-pier/louver candidate; no saved common-band changes',
        'current_source': {'path': rel(INPUT), 'sha256': sha(INPUT),
                           'qualification': 'current basewall geometry and exact triangle/UV authority; saved upper append finishes are recorded separately'},
        'already_saved_finish_preservation': {
            'receipt_path': rel(RECEIPT), 'receipt_sha256_at_read': sha(RECEIPT),
            'base_owner_ids': [1922317721, 1967088396, 1327299834],
            'lower_world_y': 9.38, 'top_world_y': 12.0999947,
            'instruction': 'Keep all three saved boundedR2 finish meshes and underlying walls unchanged. No old lower fascia geometry is reinstated.'},
        'three_source_comparison': [
            {'kind': 'ACTUAL_LOCATION_PHOTO', 'id': 'RYOKOU_PHARMACY_20231215',
             'page_url': 'https://ryokou-recommend.com/busan-station/',
             'original_image_url': 'https://ryokou-recommend.com/wp-content/uploads/2023/12/IMG20231215080637.jpg',
             'local_original': rel(PHOTO), 'sha256': sha(PHOTO), 'dimensions_px': [4624, 3468],
             'source_epoch': {'embedded_EXIF_DateTimeOriginal': '2023:12:15 08:06:37', 'timezone': 'unknown',
                              'independent_capture_date_verified': False, 'publication_utc_date': '2024-01-09',
                              'current_2026_return_condition_qualified': False},
             'image_viewed': True,
             'observed_literal_shape': ['Gray public service pier immediately right of the identified 새부산약국 glazing.',
                                        'Horizontal louver grille above a gray-green flush service leaf/panel.',
                                        'Projecting illuminated advertising pylon hides part of the pier right/base; CCTV above.'],
             'supported': 'Actual same-branch visible architecture and feature order in this 2023 view.',
             'not_supported': 'Metric louver/pier bounds, current owner ID, 2026 unchanged condition, WC boundary, or permission to create a new opening/door.'},
            {'kind': 'SPECIALIST_STORE_ORIGINAL_LOCATION_DRAWING', 'id': 'KORAIL_RETAIL_100083811_P1',
             'source_url': source_page, 'local_original_pdf': rel(PDF), 'sha256': sha(PDF),
             'page_index_zero_based': 0, 'page_title': '상업시설 대상지 도면 – 부산역2층',
             'source_epoch': {'official_board_publication_kst': '2026-10-07 16:06:45',
                              'pdf_creation': pdf.metadata['creationDate'],
                              'role': '2026 recruitment location plan; not a pharmacy construction elevation/as-built survey'},
             'source_labels': labels,
             'supported': 'Identifies pharmacy and AmericanTrailer in the west retail neighborhood against the architectural background.',
             'not_supported': 'Colored box edges are not metric wall limits. This attachment contains no pharmacy service-pier/louver elevation or profile dimensions.'},
            {'kind': 'ORIGINAL_2F_ARCHITECTURAL_DRAWING', 'id': 'ORIGINAL_2F_49695',
             'exact_existing_alias': rel(ORIGINAL), 'sha256': sha(ORIGINAL),
             'original_dimensions_px': [631, 496],
             'embedded_in_specialist_pdf': {'path': rel(PDF), 'page_index_zero_based': 0,
                                           'xref': 36, 'extracted_image_sha256': embedded_hash,
                                           'placement_pdf_pt': [list(r) for r in page.get_image_rects(36)],
                                           'same_background_bytes_not_independent_measurement': True},
             'source_epoch': 'Architectural background drawing date unqualified; reproduced in 2026 location PDF.',
             'image_viewed': True,
             'supported': 'West diagonal enclosure, sanitary/transport topology and structural relationships are visible as source plan context.',
             'not_supported': 'No uniquely labeled pharmacy service-pier/louver elevation or fixed-current-owner correspondence. Original pixel endpoints and the stretched PDF carrier are not metric coordinates.',
             'authority_pointer_gap': 'WRITER-AUTHORITY original_2f_architectural_clean_source PNG path is absent on disk. The existing 100083313 JPEG alias and PDF xref36 supply exact49695 bytes without restoration; colored728cd sibling is not substituted.'}
        ],
        'current_candidate_face_reference_only': {
            **metadata(owner), 'individual_pharmacy_identity_qualified': False,
            'basis': {'submesh': 0, 'triangle_index_in_submesh': 98, 'vertex_indices': basis_indices,
                      'vertices_world': bv, 'uv': [g['uv'][i] for i in basis_indices]},
            'public_normal_world': nw, 'public_normal_q': nq, 'tangent_q': tangent,
            'plane_equation': 'dot(public_normal_q,q)=plane_d', 'plane_d': plane,
            'full_face_s_interval': full, 'full_face_a_q': at(full[0]), 'full_face_b_q': at(full[1]),
            'current_face_support_triangles': support,
            'withheld_end_reference': {'s_interval': [stop_s, full[1]], 'a_q': at(stop_s), 'b_q': at(full[1]),
                                       'length_MODEL_m': full[1]-stop_s,
                                       'role': 'unmodified remainder after saved boundedR2 finish; not a source-observed pier width'},
            'all_reference_geometry_only': True,
            'no_support_triangles_removed_or_recolored': True},
        'protected_WC_contact_evidence': [
            {**metadata(x), 'conservative_projected_world_AABB_q_bounds': q_aabb_from_world_bounds(x),
             'qualification': 'Metadata only; not actual matching-level face/footprint. Full mesh geometry absent in this input category.',
             'operation': 'KEEP_UNCHANGED'} for x in wc],
        'adjacent_unworked_owners': [
            {**metadata(by_id(data['nonWC_walls'], oid)),
             'qualification': 'No unique photo-facing side or pharmacy service-pier identity established; not a substitute target.'}
            for oid in (1041589445, 582404219)],
        'unique_anchor_chain': {
            'photo_chain': ['identified pharmacy glazing/sign', 'gray service pier with horizontal grille', 'partially occluding advertising pylon'],
            'current_chain': ['basewall1327299834 main public face', 'saved finish stops at qY10.865978660803682', 'nearby protected WC1417713925/2041710588 metadata'],
            'missing_bridge': 'Neither drawing identifies this photographed pier at one exact current owner/endface; no shared individually identified column/EV/WC corner is visible and metrically tied across the photo and current chain.',
            'binding_status': 'UNQUALIFIED_NO_CONSTRUCTION'},
        'pending': [
            {'id': 'NW_PHARMACY_PIER_OWNER_BINDING',
             'required_evidence': 'One same-location view or continuous walk connecting the pharmacy right glazing/pier to an individually identified fixed structural column/EV/WC public corner. Alternatively a pharmacy-specific dimensioned plan/elevation identifying that same structural corner.',
             'why_exactly_needed': 'The already accepted common upper finish does not establish individual tenant identity or authorize selecting a lower return.'},
            {'id': 'NW_WC_CONTACT_FACE',
             'required_evidence': 'Exact current matching-level public-face/endpoint mesh data for protected WC1417713925 and2041710588, supplied by the root if further construction is pursued.',
             'why_exactly_needed': 'Their projected world AABBs conservatively overlap the candidate end; they cannot define a cut, surface ownership boundary, or0.403m service-pier footprint.'},
            {'id': 'NW_LOUVER_PROFILE_AND_EPOCH',
             'required_evidence': 'Pharmacy/service-pier original elevation/detail or a qualified current view with two known architectural anchors and visible grille/pier bounds.',
             'why_exactly_needed': 'Historical photo establishes a horizontal grille but not its metric extents, blade pitch/depth, current2026 state, or relation to the existing service leaf. No new door/aperture is proposed.'}
        ],
        'preserved': ['all current walls/base UV/materials/world coordinates/colliders',
                      'all three saved boundedR2 upper finishes', 'lower tenant splits/entries and vent phase',
                      'protected WC/Native6/all native/1F/current floors/columns'],
        'new_original_download_count': 0, 'new_original_download_bytes': 0,
        'Scene_Assets_C180_UI_writes': False, 'QA_tests_probes_captures': False,
        'all2F_complete': False, '99percent_achieved_or_measured': False,
        'builder': rel(Path(__file__).resolve())
    }
    out = OUT / 'CONSTRUCTION-PACKET.json'
    out.write_text(json.dumps(packet, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'status': packet['status'], 'ready_construction_count': 0,
                      'path': rel(out), 'sha256': sha(out),
                      'withheld_reference_length_MODEL_m': full[1]-stop_s}, ensure_ascii=False))


if __name__ == '__main__':
    main()
