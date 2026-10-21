"""R21 source-supported shop work regions on the unchanged actual-object CAD.

All output is offline review material. This script never reads or writes Scene
or Assets. Commercial-map glyph rectangles are identity locators only.
"""
from pathlib import Path
import datetime
import hashlib
import json

from reportlab.pdfgen import canvas
from shapely import union_all, set_precision
from shapely.geometry import Polygon, LineString, box, shape, mapping

ROOT = Path('/Users/um-yunsang/CHOOGuard')
WORK = ROOT / '.state/ACTIVE-2F-SHOP-STRUCTURE-REVIEW-R21'
BASE = ROOT / '.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/cad/r19/CURRENT-LIVE-2F-OBJECT-VECTOR-CAD-R19-NO-TEXT.json'
REFERENCE = ROOT / '.state/ACTIVE-2F-SHOP-LOCATION-REVIEW-R20'
cad = json.loads(BASE.read_text())
struct = json.loads((REFERENCE/'geometry-lane/STRUCTURAL-ANCHORS-AND-CELLS-R20.json').read_text())
identity = json.loads((REFERENCE/'source-lane/SHOP-IDENTITY-REGISTER.json').read_text())
floor = shape(struct['floor']['geometry'])
protected = shape(struct['official6_protected']['actual_Y7_surface_geometry'])

def parts(g, kind='Polygon'):
    if g.is_empty:
        return []
    if g.geom_type == kind:
        return [g]
    return [p for child in getattr(g, 'geoms', []) for p in parts(child, kind)]

columns = union_all([shape(a['geometry']) for a in struct['generated_2F_columns_UNCONFIRMED'] if a.get('geometry')])
walls = union_all([shape(a['geometry']) for a in struct['fixed_saved_walls'] if a.get('geometry')])
transport = union_all([shape(a['geometry']) for a in struct['transport_matching_level_profiles'] if a.get('geometry')])
solid = union_all([columns, walls, transport, protected])
proposals = []

def add(key, numbers, geometry, basis, evidence, mode='PROPOSED_SHOP_FITOUT_MODEL',
        frontage=None, permitted_floor=None, partial=False, boundary_inferred=True):
    raw = geometry
    area = geometry.geom_type in ('Polygon', 'MultiPolygon')
    if area:
        # Numeric cleanup is confined to colored proposal geometry. Existing gray
        # vectors are always copied unsnapped. Clipping only respects actual
        # solids/openings; it does not determine the proposed shop shape.
        g = raw.intersection(floor if permitted_floor is None else permitted_floor).difference(solid)
        g = union_all([p for p in parts(set_precision(g, .001)) if p.area > .08])
    else:
        g = raw.difference(union_all([columns, protected]))
    entry = {
        'id': key, 'identity_numbers': numbers,
        'geometry': mapping(g), 'structural_region_before_solid_subtraction': mapping(raw),
        'gross_proposed_region_MODEL_m2': raw.area if area else None,
        'net_colored_region_MODEL_m2': g.area if area else None,
        'mode': mode, 'basis_MODEL': basis, 'evidence': evidence,
        'boundary_inferred': boundary_inferred, 'partial_work_region': partial,
        'metric_status': 'USER_REVIEW_MODEL_NOT_FIELD_MEASURED',
        'commercial_colored_boxes_used_for_size_or_center': False,
        'Scene_Assets_changed': False,
    }
    if frontage is not None:
        entry['frontage'] = mapping(frontage.difference(columns))
    proposals.append(entry)
    return entry

# New field-evidence packet, not the superseded R20 proposal geometry.
nw_path = WORK/'northwest-lane/NORTHWEST-SHOP-STRUCTURE-MODEL-R21.json'
nw = json.loads(nw_path.read_text())
nw_by = {x['brand']: x for x in nw['shops']}
nw_index = [('크리스피크림도넛_NW',1), ('아트박스',2), ('소미담',3), ('파스쿠찌',4),
            ('스토리오브라망',5), ('하와이안파라다이스',6), ('국제시장단팥빵',7),
            ('소월당_2F',8), ('부산고등어빵',9), ('새부산약국',10),
            ('아메리칸트레일러',11), ('기장특산품',12), ('국제시장도나스',13)]

# A continuous north row is evidenced, but individual lease partitions are not
# dimensioned. Present it as a single work region, without invented equal bays.
north_shapes = [shape(nw_by[brand]['geometry_qx_minus_qy_m']) for brand,n in nw_index if n <= 4]
north_group = union_all(north_shapes)
# Panorama: glass fronts sit BEHIND the hall column row. Use actual north faces,
# not column centers, for the continuous public-side frontage datum.
row = [a for a in struct['generated_2F_columns_UNCONFIRMED']
       if -63 < a['centroid_qx_minus_qy'][0] < -18 and 85 < a['centroid_qx_minus_qy'][1] < 89]
north_front = max(shape(a['geometry']).bounds[3] for a in row)
north_group = north_group.intersection(box(-63,north_front,-18,96))
add('NORTH_CONNECTED_STOREFRONTS', [1,2,3,4], north_group,
    'Field photos link corner Krispy, former KakaoFriends/current Artbox, Somidam and Pascucci. '
    'Current north facade bounds the rear. Front is on the north faces of actual hall supports; '
    '360 view places Pascucci glass behind that row. No equal tenant split is asserted.',
    [str(nw_path.relative_to(ROOT)), 'cv-lane/google-pano-01-provider-s0-yaw+010-pitch-03-fov78.jpg'],
    'CONNECTED_EXISTING_SHELL_WORK_REGION_INDIVIDUAL_PARTITIONS_PENDING')

for brand,n in nw_index:
    if n <= 4:
        continue
    item = nw_by[brand]
    g = shape(item['geometry_qx_minus_qy_m'])
    e = add(f'SHOP_{n:02d}', [n], g,
        ' '.join(item['observed'])+' '+' '.join(item['MODEL']),
        [str(nw_path.relative_to(ROOT)), *item.get('source_urls',[])])
    e['source_dates'] = item.get('source_dates',[])
    e['structural_anchors'] = item.get('structural_anchors',[])
    if n in (8,9):
        e['binding_qualification'] = 'LOWER_CONFIDENCE_COVERED_WEST_POPUP_MODEL_FOR_USER_REVIEW'

center_path = WORK/'center-front-lane/BOUNDED-SHOP-MODEL-GEOMETRY-R21.json'
center = json.loads(center_path.read_text())
ci = {'위니비니':15,'편의점_SE87':31,'소반한식':32,'비엔씨제과_E':33,'써브웨이':34}
for item in center['models']:
    n = ci[item['shop']]
    e = add(f'SHOP_{n:02d}',[n],shape(item['geometry']),
        item['observations_OBSERVED']+' '+item['placement_basis_MODEL'],
        [str(center_path.relative_to(ROOT))],
        mode='PARTIAL_FRONT_COUNTER_WORK_REGION' if item.get('partial_area') else 'PROPOSED_SHOP_FITOUT_MODEL',
        frontage=shape(item['frontage']),partial=item.get('partial_area',False))
    e['qualification'] = item['qualification']
    e['structural_anchors'] = item.get('structural_anchor_objects',[])

# Primary original Goods design: explicit 5500x6000mm, with its existing column
# locally anchored to the fixed current reference. No global image registration.
migration_path = WORK/'cv-lane/SOURCE-ENTITY-MIGRATION-GOODS-R21.json'
migration = json.loads(migration_path.read_text())
goods = migration['Goods_dimensioned_migration']
g = shape(goods['proposed_CAD_polygon'])
e = add('GOODS_33SQM_DIMENSIONED_MODEL',[16],g,
    'Original 2026 construction design explicitly dimensions 5.5x6.0m, gross33sqm. '
    'Its existing rectangular support is locally proposed as current021. Preserve actual support '
    'profile and center; no column movement or source image warp. Design is not surveyed asbuilt.',
    [str(migration_path.relative_to(ROOT)), 'root-source/GOODS-ORIGINAL-STRUCTURE-DIMENSIONS-R21.json'],
    'ORIGINAL_DIMENSIONED_DESIGN_LOCAL_SUPPORT_MIGRATION', boundary_inferred=False)
e['explicit_source_dimensions_m'] = [5.5,6.0]
e['existing_support_inside_shop'] = 'NATIVE_COL_021_2F_MODEL; actual gray body excluded from orange fill only'

# Adjacent StarSand/OliveYoung: source SITE gives real shell front/depth alignment
# and included supports, but not uncropped complete dimensions. Highlight only
# structural frontage spans; do not fabricate another full footprint from boxes.
support = {x['source_entity']:x['actual_current_reference'] for x in migration['I_rectangle_source_entity_assignments']}
star = next(v for k,v in support.items() if k.startswith('StarSand'))
olive = next(v for k,v in support.items() if k.startswith('OliveYoung'))
goods_col = next(v for k,v in support.items() if k.startswith('NewGoods'))
front_x = g.bounds[0]
star_x = front_x + star['center'][0] - goods_col['center'][0]
olive_x = front_x + olive['center'][0] - goods_col['center'][0]
# These are explicitly partial structural spans on the actual two identified
# support neighborhoods. Their endpoints are not claimed lease partition ends.
# A frontage REVIEW RANGE is bounded by the neighboring actual support-row
# midlines, not commercial label dimensions or a claim about tenant endpoints.
star_north = (goods_col['center'][1] + star['center'][1])/2
star_south = (star['center'][1] + olive['center'][1])/2
below_olive = max((a for a in struct['generated_2F_columns_UNCONFIRMED']
                  if abs(a['centroid_qx_minus_qy'][0]-olive['center'][0])<.4
                  and a['centroid_qx_minus_qy'][1] < olive['center'][1]-1),
                 key=lambda a:a['centroid_qx_minus_qy'][1])
olive_south = (olive['center'][1] + below_olive['centroid_qx_minus_qy'][1])/2
star_line = LineString([(star_x,star_north),(star_x,star_south)])
olive_line = LineString([(olive_x,star_south),(olive_x,olive_south)])
for n,name,line,ref in [(17,'StarSand',star_line,star),(18,'OliveYoung',olive_line,olive)]:
    e = add(f'SHOP_{n:02d}_STRUCTURAL_FRONTAGE',[n],line,
        f'Original SITE identifies {name} and its included rectangular support. Local MODEL '
        'public face follows the shared source-front alignment and actual support center. '
        'The frontage REVIEW RANGE uses neighboring actual support-row midlines. '
        'Range endpoints are not tenant ends; full length/back boundary is not asserted '
        'from the cropped or non-scale SITE.',
        [str(migration_path.relative_to(ROOT))], 'PARTIAL_FRONTAGE_LOCAL_CORRESPONDENCE',partial=True)
    e['structural_anchor'] = ref['object_id']
    e['full_footprint_pending'] = True

field_packet = WORK/'center-front-lane/CENTER-FRONT-SHOP-STRUCTURE-EVIDENCE-R21.json'
field = json.loads(field_packet.read_text())
for n,name in [(23,'꽃들'),(24,'복순도가')]:
    item = next(x for x in field['shops'] if x['shop']==name)
    add(f'SHOP_{n:02d}_EXISTING_FRONTAGE',[n],LineString(item['frontage_drawingXY_MODEL']),
        item['observed']+' '+item['limitations'],
        [str(field_packet.relative_to(ROOT)),*item['sources']],
        'EXISTING_ENCLOSED_STOREFRONT_WORK_FRONTAGE_ONLY',partial=True)

# Four native shops in two already-built rooms. Their public doors/wall chains
# can be read directly from Official0 at actual2F height; reuse that shell and
# leave the central public entrance clear. Internal tenant split remains open.
native0 = next(a for a in cad['objects'] if a['path'].endswith('OfficialStation_-부산역_0'))
native0_floor = union_all([Polygon(r) for r in native0['surface_rings'] if Polygon(r).area>1])
for key,ns,coords,front in [
    ('NATIVE0_WEST_PAIRED_INTERIOR',[25,26],
     [(-19.880,-34.808),(-15.530,-34.987),(-15.574,-46.045),(-15.524,-46.145),(-19.924,-46.128)],
     [(-15.530,-34.987),(-15.574,-46.045)]),
    ('NATIVE0_EAST_PAIRED_INTERIOR',[27,28],
     [(.369,-35.049),(4.720,-34.904),(4.676,-46.224),(.276,-46.207)],
     [(.369,-35.049),(.326,-46.107)]),
]:
    add(key,ns,Polygon(coords),
        '2024 direct field photographs show two adjoining low-ceiling glazed stores on each '
        'side of the entrance, within the existing facade/diagonal-frame shell. Work region '
        'follows actual Official0 Y8.4 wall/door chains, not the whole object bounds. '
        'Reuse the native shell. No equal split between the two tenants is fabricated. '
        'West historic Dain identity changes to October2026 Krispy; record the epoch.',
        ['center-front-lane/dailyhappyday1604-16.jpg','center-front-lane/dailyhappyday1604-17.jpg',
         'https://dailyhappyday.tistory.com/1604',native0['object_id']],
        'EXISTING_NATIVE_ROOM_INTERIOR_FITOUT_ONLY',frontage=LineString(front),
        permitted_floor=native0_floor,boundary_inferred=False)

# Bounded remaining field/frontage lane; no R20 proposals are imported.
remaining_path = WORK/'center-front-lane/REMAINING-FRONTAGES-R21.json'
if remaining_path.exists():
    remaining = json.loads(remaining_path.read_text())
    for item in remaining.get('models',[]):
        n = {'비엔씨제과_W':19,'환공어묵_2F':20,'편의점_SW113':21,
             '영주당부산과자집':22,'편의점_N42':14,'남천할매떡볶이':30,'팔공티':29}[item['shop']]
        e = add(f'SHOP_{n:02d}',[n],shape(item['geometry']),
            item['observed']+' '+item['MODEL_basis'],[str(remaining_path.relative_to(ROOT))],
            item.get('mode','PROPOSED_SHOP_FITOUT_MODEL'),
            frontage=shape(item['frontage']) if item.get('frontage') else None,
            partial=item.get('partial_area',False) or item['geometry']['type']=='LineString')
        e['structural_anchors'] = item.get('structural_anchor_objects',[])
        e['qualification'] = item['qualification']

covered = {n for e in proposals for n in e['identity_numbers']}
source_entities = []
for source in identity['shop_entities']:
    number = int(source['source_entity_id'].rsplit('-',1)[-1])
    e = dict(source)
    e['identity_id'] = f'R21-IDENTITY-{number:02d}'
    e['work_regions'] = [p['id'] for p in proposals if number in p['identity_numbers']]
    e['status'] = 'MODEL_REGION_OR_FRONTAGE_FOR_REVIEW' if number in covered else 'INDIVIDUAL_STRUCTURAL_BINDING_PENDING'
    source_entities.append(e)

stem = WORK/'2F-SHOP-STRUCTURAL-WORK-OVERLAY-R21-NO-TEXT'
record = {
    'schema':'chooguard.2f.shop_structural_work_review.r21.v1',
    'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'status':'USER_REVIEW_ONLY_NOT_SCENE_CONSTRUCTION_AUTHORITY',
    'immutable_gray_reference':str(BASE.relative_to(ROOT)),
    'immutable_gray_reference_sha256':hashlib.sha256(BASE.read_bytes()).hexdigest(),
    'coordinates':cad['units'],'worldXZ_to_q':cad['worldXZ_to_q'],
    'commercial_map_role':'IDENTITY_AND_NEIGHBORHOOD_ONLY_NO_METRIC_BOXES',
    'original_plan_role':'Architectural entities, adjacency, doors, actual room topology; no warped carrier',
    'observed_vs_inferred':'Photos/360 establish morphology/adjacency; coordinate extents remain explicit MODEL review proposals unless locally dimensioned',
    'proposals':proposals,'source_entities':source_entities,
    'work_region_count':len(proposals),'identified_shop_count':len(source_entities),
    'shop_identities_with_plotted_work':len(covered),
    'full_footprint_not_asserted_for':[e['identity_numbers'] for e in proposals if e['partial_work_region']],
    'visible_added_text':False,'reference_color':'achromatic','work_color':'orange',
    'original_360_used':'actual retained equirectangular images converted to pinhole views; camera-world survey pose absent',
    'new_3DGS_or_NeRF_reconstruction_claimed':False,
    'source_image_warped':False,'Scene_Assets_changed':False,
    'base_current_reference_newly_confirmed':False,
    'user_validation_owner':True,'agent_Scene_QA_or_reopen_or_probe_or_capture':False,
    'source_epochs_retained':True,
    'limitations':[
        'All proposed absolute tenant extents require user review; they are not field measurements.',
        'Connected north and native-front pairs are intentionally not divided into arbitrary equal shops.',
        'StarSand/Olive/Flowers/Boksoon and partial counters do not assert a complete rear enclosure.',
        'Preserve all actual gray reference, transport openings, native rooms, existing edits and deletion tombstones.',
        'Commercial latest acquired source is100083811 published2026-10-07; newer attachment not acquired.',
    ],
}
stem.with_suffix('.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')

views = [(-83,-60,96,98), (-76,61,17,97), (-77,14,19,64),
         (-66,-51,19,14), (14,-49,95,18), (-24,13,13,62)]
c = canvas.Canvas(str(stem.with_suffix('.pdf')),pagesize=(1190.55,841.89),pageCompression=1)
c.setTitle('2F shop structural work overlay R21')
for region in views:
    minx,miny,maxx,maxy=region
    scale=min(1150.55/(maxx-minx),801.89/(maxy-miny))
    dx=(1190.55-(maxx-minx)*scale)/2-minx*scale
    dy=(841.89-(maxy-miny)*scale)/2-miny*scale
    area=box(*region)
    c.saveState();cp=c.beginPath();cp.rect(18,18,1154.55,805.89);c.clipPath(cp,stroke=0,fill=0)
    for entry in proposals:
        geometry=shape(entry['geometry']).intersection(area)
        c.setFillColorRGB(1,.78,.40);c.setStrokeColorRGB(.94,.36,.035);c.setLineWidth(.7)
        # Inferred MODEL extents are dashed; source/native-bound room boundaries
        # are continuous. Both remain the same single work color, with no text.
        c.setDash(3,2) if entry['boundary_inferred'] else c.setDash()
        for poly in parts(geometry):
            p=c.beginPath()
            for ring in [poly.exterior,*poly.interiors]:
                p.moveTo(ring.coords[0][0]*scale+dx,ring.coords[0][1]*scale+dy)
                for x,y in list(ring.coords)[1:]:p.lineTo(x*scale+dx,y*scale+dy)
                p.close()
            c.drawPath(p,stroke=1,fill=1,fillMode=0)
        lines=parts(geometry,'LineString')
        if entry.get('frontage'):lines+=parts(shape(entry['frontage']).intersection(area),'LineString')
        c.setDash();c.setLineWidth(2.4)
        for line in lines:
            c.lines([(a[0]*scale+dx,a[1]*scale+dy,b[0]*scale+dx,b[1]*scale+dy)
                     for a,b in zip(line.coords,list(line.coords)[1:])])
    c.setDash()
    for obj in cad['objects']:
        c.setStrokeGray(.70);c.setLineWidth(.22)
        for ring in obj['surface_rings']:
            if not LineString(ring).intersects(area):continue
            p=c.beginPath();p.moveTo(ring[0][0]*scale+dx,ring[0][1]*scale+dy)
            for x,y in ring[1:]:p.lineTo(x*scale+dx,y*scale+dy)
            c.drawPath(p,stroke=1,fill=0)
        segments=obj['sections'].get('8.40000',[])
        if not segments and obj['kind']=='CURRENT':
            low=[(float(k),v) for k,v in obj['sections'].items() if 7.02<float(k)<8.4]
            if low:segments=max(low,key=lambda x:x[0])[1]
        c.setStrokeGray(.28 if obj['kind']=='CURRENT' else .46)
        c.setLineWidth(.34 if obj['kind']=='CURRENT' else .26)
        lines=[]
        for a,b in segments:
            if max(a[0],b[0])<minx or min(a[0],b[0])>maxx or max(a[1],b[1])<miny or min(a[1],b[1])>maxy:continue
            lines.append((a[0]*scale+dx,a[1]*scale+dy,b[0]*scale+dx,b[1]*scale+dy))
        if lines:c.lines(lines)
    c.restoreState();c.showPage()
c.save()

# Append only work entities to the immutable gray source DXF.
dxf=BASE.with_suffix('.dxf').read_text()
footer='0\nENDSEC\n0\nEOF\n'
if not dxf.endswith(footer):raise ValueError('Unexpected DXF footer')
entities=[]; layers=[]
def emit(*v):entities.extend(str(x) for x in v)
for i,entry in enumerate(proposals,1):
    layer=f'R21_WORK_{i:02d}_{entry["id"]}'
    layers += [0,'LAYER',2,layer,70,0,62,30,6,'CONTINUOUS']
    g=shape(entry['geometry'])
    for p in parts(g):
        for ring in [p.exterior,*p.interiors]:
            emit(0,'POLYLINE',8,layer,62,30,66,1,70,1,10,0,20,0,30,0)
            for x,y in list(ring.coords)[:-1]:emit(0,'VERTEX',8,layer,62,30,10,f'{x:.9f}',20,f'{y:.9f}',30,0)
            emit(0,'SEQEND',8,layer)
    lines=parts(g,'LineString')
    if entry.get('frontage'):lines+=parts(shape(entry['frontage']),'LineString')
    for line in lines:
        for a,b in zip(line.coords,list(line.coords)[1:]):
            emit(0,'LINE',8,layer,62,30,10,f'{a[0]:.9f}',20,f'{a[1]:.9f}',30,0,11,f'{b[0]:.9f}',21,f'{b[1]:.9f}',31,0)
tokens=dxf.splitlines();start=tokens.index('LAYER');tokens[start+2]=str(int(tokens[start+2])+len(proposals))
end=next(i for i in range(start,len(tokens)-1) if tokens[i]=='0' and tokens[i+1]=='ENDTAB')
tokens[end:end]=[str(x) for x in layers]
dxf='\n'.join(tokens)+'\n'
stem.with_suffix('.dxf').write_text(dxf[:-len(footer)]+'\n'.join(entities)+'\n'+footer)

receipt={'status':'REVIEW_ARTIFACT_CREATED_NOT_SCENE_APPLIED','pdf':str(stem.with_suffix('.pdf').relative_to(ROOT)),
         'dxf':str(stem.with_suffix('.dxf').relative_to(ROOT)),'data':str(stem.with_suffix('.json').relative_to(ROOT)),
         'pages':len(views),'work_regions':len(proposals),'shop_identities_plotted':len(covered),
         'unplotted_identity_numbers':sorted(set(range(1,35))-covered),'Scene_Assets_changed':False,
         'immutable_base_sha256':record['immutable_gray_reference_sha256'],'Preview_opened':False}
(WORK/'REVIEW-ARTIFACT-RECEIPT-R21.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(receipt,ensure_ascii=False))
