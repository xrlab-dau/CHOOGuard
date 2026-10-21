"""Construct current metric vector CAD; no source-raster registration or Scene mutation."""
import json
import hashlib
import datetime
import sys
from pathlib import Path
from collections import Counter

import numpy as np
from shapely import union_all
from shapely.geometry import Polygon, box, LineString
from reportlab.pdfgen import canvas

ROOT = Path('/Users/um-yunsang/CHOOGuard')
WORK = ROOT / '.state/ACTIVE-ALL2F-PHOTO-TWIN-R18'
SOURCE = WORK / 'inputs/CURRENT-LIVE-2F-VECTOR-CAD-SOURCE-R18.json'
OUT = WORK / 'cad'
REVISION = 'R18'
if len(sys.argv) > 1:
    SOURCE = ROOT / sys.argv[1]
    REVISION = sys.argv[2] if len(sys.argv) > 2 else 'R19'
    OUT = WORK / 'cad' / REVISION.lower()
OUT.mkdir(exist_ok=True)
Q = np.array([[-.34290633335024717, -.9393696006089878],
              [-.9393696006089878, .3429063333502471]])


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def xy(p):
    q = Q @ np.asarray(p)[[0, 2]]
    return [float(q[0]), float(-q[1])]


def polys(g):
    if g.is_empty:
        return []
    if g.geom_type == 'Polygon':
        return [g]
    return [p for p in g.geoms if p.geom_type == 'Polygon']


def contours(g):
    for p in polys(g):
        yield list(p.exterior.coords)
        for hole in p.interiors:
            yield list(hole.coords)


def main():
    data = json.loads(SOURCE.read_text())
    layers = []
    floor_union = []
    total_segments = 0
    counts = Counter()
    for row in data['objects']:
        oid = row['object_id'].split('-')[-2]
        layer_role = {'current_2F_object': 'CURRENT', 'fixed_official_reference': 'OFFICIAL',
                      'fixed_current_1F_to_2F_transport_reference': 'TRANSPORT'}[row['source_kind']]
        obj = {'object_id': row['object_id'], 'path': row['path'],
               'mesh_asset': row['mesh_asset'], 'mesh_object_id': row['mesh_object_id'],
               'kind': layer_role, 'sections': {}, 'surface_rings': [], 'surface_area_MODEL_m2': None}
        for level, lines in row['sections_world'].items():
            seen = set()
            segments = []
            for _, _, a, b in lines:
                aa, bb = xy(a), xy(b)
                key = tuple(sorted((tuple(round(v, 6) for v in aa), tuple(round(v, 6) for v in bb))))
                if key in seen:
                    continue
                seen.add(key)
                segments.append([aa, bb])
            obj['sections'][level] = segments
            total_segments += len(segments)
        triangles = []
        for _, _, a, b, c in row['matching_Y7_upward_triangles_world']:
            p = Polygon([xy(a), xy(b), xy(c)])
            if p.area > 1e-10:
                triangles.append(p)
        if triangles:
            # Precision only affects the derived CAD union, never a live mesh vertex.
            g = union_all(triangles, grid_size=1e-6)
            obj['surface_rings'] = list(contours(g))
            obj['surface_area_MODEL_m2'] = float(g.area)
            if layer_role == 'CURRENT' and '/MAIN2F_APPROVED_' in row['path']:
                floor_union.append(g)
        counts[layer_role] += 1
        layers.append(obj)

    floor = union_all(floor_union, grid_size=1e-6)
    bounds = tuple(float(v) for v in floor.bounds)
    bounds = (bounds[0]-4, bounds[1]-4, bounds[2]+4, bounds[3]+4)
    record = {'schema': 'chooguard.actual_object_vector_cad.r18.v1',
              'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
              'construction_source': str(SOURCE.relative_to(ROOT)), 'source_sha256': sha(SOURCE),
              'source_snapshot_utc': data['utc'], 'scene': data['scene'],
              'units': 'metres; current actual Unity world XZ transformed rigidly into qx,-qy',
              'worldXZ_to_q': Q.tolist(), 'objects': layers,
              'counts': dict(counts), 'section_segments': total_segments,
              'fixed_official_mesh_parts_inventoried': data['fixed_official_mesh_parts_inventoried'],
              'excluded_alternative_LOD_objects': data['render_only_alternative_LOD_objects_excluded'],
              'all_refs_achromatic_and_locked': True, 'visible_text': False,
              'source_raster_or_unitless_source_DXF_used': False,
              'floor_trim_R17': '.state/CURRENT-2F-FLOOR-OVERFLOW-R17.json',
              'floor_surfaces_are_current_reference_not_fill_or_occupancy_authority': True,
              'official_surfaces_not_assumed_to_be_completed_2F_floor': True,
              'Scene_Assets_changed': False, 'validation_owner': 'user',
              'comparison_context': 'Current geometry only. Construction source correspondence, no Scene QA/probe/reopen/capture.'}
    basename = f'CURRENT-LIVE-2F-OBJECT-VECTOR-CAD-{REVISION}-NO-TEXT'
    vector_path = OUT / (basename+'.json')
    vector_path.write_text(json.dumps(record, ensure_ascii=False, separators=(',', ':'))+'\n')

    # R12 ASCII DXF supports individually locked reference layers and actual LINE/POLYLINE entities.
    dxf = OUT / (basename+'.dxf')
    names = []
    for obj in layers:
        oid = obj['object_id'].split('-')[-2]
        for level in obj['sections']:
            names.append(f"{obj['kind']}_{oid}_Y{level.replace('.', '_')}")
        if obj['surface_rings']:
            names.append(f"{obj['kind']}_{oid}_Y7_SURFACE_REFERENCE")
    with dxf.open('w') as f:
        def put(*items):
            f.write('\n'.join(str(x) for x in items)+'\n')
        put(0,'SECTION',2,'HEADER',9,'$ACADVER',1,'AC1009',9,'$INSUNITS',70,6,
            9,'$MEASUREMENT',70,1,0,'ENDSEC',0,'SECTION',2,'TABLES',0,'TABLE',2,'LAYER',70,len(names)+1,
            0,'LAYER',2,'0',70,0,62,8,6,'CONTINUOUS')
        for name in names:
            put(0,'LAYER',2,name,70,4,62,8,6,'CONTINUOUS')
        put(0,'ENDTAB',0,'ENDSEC',0,'SECTION',2,'ENTITIES')
        for obj in layers:
            oid = obj['object_id'].split('-')[-2]
            for level, segments in obj['sections'].items():
                name=f"{obj['kind']}_{oid}_Y{level.replace('.', '_')}"
                for a,b in segments:
                    put(0,'LINE',8,name,10,format(a[0],'.9f'),20,format(a[1],'.9f'),30,0,
                        11,format(b[0],'.9f'),21,format(b[1],'.9f'),31,0)
            for ring in obj['surface_rings']:
                name=f"{obj['kind']}_{oid}_Y7_SURFACE_REFERENCE"
                put(0,'POLYLINE',8,name,66,1,70,1,10,0,20,0,30,0)
                for a in ring[:-1]:
                    put(0,'VERTEX',8,name,10,format(a[0],'.9f'),20,format(a[1],'.9f'),30,0)
                put(0,'SEQEND',8,name)
        put(0,'ENDSEC',0,'EOF')

    pdf = OUT / (basename+'.pdf')
    c = canvas.Canvas(str(pdf), pagesize=(1190.55, 841.89), pageCompression=1)
    c.setTitle(f'Current 2F object vector CAD {REVISION}')
    views=[(bounds,'8.40000',True),((-43,-43,31,-20),'8.40000',True),
           (bounds,'7.15000',False),(bounds,'10.50000',False)]
    for region, level, use_low in views:
        minx,miny,maxx,maxy=region
        scale=min(1150.55/(maxx-minx),801.89/(maxy-miny))
        dx=(1190.55-(maxx-minx)*scale)/2-minx*scale
        dy=(841.89-(maxy-miny)*scale)/2-miny*scale
        c.saveState();p=c.beginPath();p.rect(18,18,1154.55,805.89);c.clipPath(p,stroke=0,fill=0)
        area=box(*region)
        for obj in layers:
            # One matching-level surface contour per object; floor voids stay white.
            c.setStrokeColorRGB(.68,.68,.68);c.setLineWidth(.24)
            for ring in obj['surface_rings']:
                if not LineString(ring).intersects(area):continue
                p=c.beginPath();p.moveTo(ring[0][0]*scale+dx,ring[0][1]*scale+dy)
                for x,y in ring[1:]:p.lineTo(x*scale+dx,y*scale+dy)
                c.drawPath(p,stroke=1,fill=0)
            segments=obj['sections'].get(level,[])
            if use_low and not segments and obj['kind']=='CURRENT':
                lower=[(float(k),v) for k,v in obj['sections'].items() if 7.02<float(k)<8.4]
                if lower:segments=max(lower,key=lambda x:x[0])[1]
            gray=.24 if obj['kind']=='CURRENT' else .52
            c.setStrokeColorRGB(gray,gray,gray);c.setLineWidth(.36 if obj['kind']=='CURRENT' else .22)
            pts=[]
            for a,b in segments:
                if max(a[0],b[0])<minx or min(a[0],b[0])>maxx or max(a[1],b[1])<miny or min(a[1],b[1])>maxy:continue
                pts.append((a[0]*scale+dx,a[1]*scale+dy,b[0]*scale+dx,b[1]*scale+dy))
            if pts:c.lines(pts)
        c.restoreState();c.showPage()
    c.save()
    receipt={'status':'CURRENT_LIVE_2F_VECTOR_CAD_CREATED_FOR_CONSTRUCTION_CORRESPONDENCE',
             'utc':record['utc'],'source':record['construction_source'],'source_sha256':record['source_sha256'],
             'data':str(vector_path.relative_to(ROOT)),'pdf':str(pdf.relative_to(ROOT)),
             'dxf':str(dxf.relative_to(ROOT)), 'sha256':{p.suffix[1:]:sha(p) for p in [vector_path,pdf,dxf]},
             'counts':record['counts'],'section_segments':total_segments,'pdf_pages':len(views),
             'all_existing_references_achromatic':True,'visible_text':False,
             'fixed_official_mesh_parts_inventoried':record['fixed_official_mesh_parts_inventoried'],
             'LOD1_excluded':record['excluded_alternative_LOD_objects'],
             'Scene_Assets_changed':False,'not_a_completion_or_99percent_claim':True}
    (OUT/f'CURRENT-CAD-CONSTRUCTION-RECEIPT-{REVISION}.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({k:v for k,v in receipt.items() if k not in ['sha256']},ensure_ascii=False))


if __name__=='__main__':
    main()
