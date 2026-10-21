"""Offline shop-location construction proposal on immutable metric R19 CAD."""
import datetime
import hashlib
import json
from pathlib import Path

from reportlab.pdfgen import canvas
from shapely import union_all, set_precision
from shapely.geometry import Polygon, LineString, box, shape, mapping

ROOT = Path('/Users/um-yunsang/CHOOGuard')
WORK = ROOT / '.state/ACTIVE-2F-SHOP-LOCATION-REVIEW-R20'
BASE = ROOT / '.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/cad/r19/CURRENT-LIVE-2F-OBJECT-VECTOR-CAD-R19-NO-TEXT.json'
cad = json.loads(BASE.read_text())
struct = json.loads((WORK/'geometry-lane/STRUCTURAL-ANCHORS-AND-CELLS-R20.json').read_text())
north = json.loads((WORK/'geometry-lane/NORTH-BANK-STRUCTURAL-BAYS-R20.json').read_text())
sources = json.loads((WORK/'source-lane/SHOP-IDENTITY-REGISTER.json').read_text())
floor = shape(struct['floor']['geometry'])
protected = shape(struct['official6_protected']['actual_Y7_surface_geometry'])

def polygon_parts(g):
    if g.is_empty:
        return []
    if g.geom_type == 'Polygon':
        return [g]
    return [p for part in g.geoms for p in polygon_parts(part)]

def section_geometry(obj):
    segments = obj['sections'].get('8.40000', [])
    return union_all([LineString(s) for s in segments]) if segments else Polygon()

# The exclusion is actual existing section geometry, never an all-height silhouette.
walls = union_all([shape(a['geometry']) for a in struct['fixed_saved_walls'] if a.get('geometry')])
columns = union_all([shape(a['geometry']) for a in struct['generated_2F_columns_UNCONFIRMED'] if a.get('geometry')])
transport = union_all([shape(a['geometry']) for a in struct['transport_matching_level_profiles'] if a.get('geometry')])
obstacles = union_all([walls.buffer(.01), columns, transport.buffer(.12), protected])
wall_rows = [(a['object_id'], shape(a['geometry'])) for a in struct['fixed_saved_walls'] if a.get('geometry')]
col_rows = [(a['object_id'], shape(a['geometry'])) for a in struct['generated_2F_columns_UNCONFIRMED'] if a.get('geometry')]

proposals = {}
def put(number, geometry, anchors, rationale, mode='NEW_RETAIL_LOCATION_MODEL'):
    g = geometry.intersection(floor).difference(obstacles)
    # Only proposal vectors are reduced to a 1mm drawing grid to eliminate
    # microscopic triangulation seams. The R19 gray reference remains verbatim.
    g = union_all(polygon_parts(set_precision(g,.001)))
    parts = [p for p in polygon_parts(g) if p.area > .15]
    g = union_all(parts)
    proposals[number] = {
        'geometry': mapping(g), 'area_MODEL_m2': g.area,
        'raw_structural_geometry': mapping(geometry),
        'current_floor_contact_fraction': g.area / geometry.area if geometry.area else 0,
        'basis': rationale, 'structural_anchor_keys': anchors,
        'current_wall_neighbors': [oid for oid, gg in wall_rows if geometry.distance(gg) < .6],
        'current_column_neighbors': [oid for oid, gg in col_rows if geometry.distance(gg) < .65],
        'frontage_and_dimensions_status': 'MODEL_FOR_USER_REVIEW; not surveyed or field-measured',
        'mode': mode,
        'source_pixels_used_as_metric': False,
        'existing_geometry_edited': False,
    }

# North stores: individual actual native-derived column bays. The long cafe has
# two adjacent bays. Back edge comes from the saved floor boundary, not source ink.
bays = north['northbank_bays']
for number, indexes in [(1,[0]), (2,[1]), (3,[2]), (4,[3,4])]:
    g = union_all([shape(bays[i]['extension_to_actual_currentfloor_exterior_geometry']) for i in indexes])
    put(number, g, [bays[i]['id'] for i in indexes],
        'Official October map gives Krispy / Artbox / Somidam / Pascucci order. '
        'Front native-derived column row and actual rear floor perimeter bound these bays. '
        'Source colored-box width/depth is not used.',
        'EXISTING_SHELL_FITOUT_LOCATION_REVIEW' if number == 1 else 'NEW_RETAIL_LOCATION_MODEL')

put(5, box(-11.54,86.55,-3.73,93.35), ['NATIVE_COL_034','NATIVE_COL_031','NATIVE_COL_039','NATIVE_COL_036'],
    'Separate north-core return bay west of the actual core; keep the passage break after Pascucci.')
put(6, box(-71.73,77.0,-65.9,85.95), ['west_actual_floor_return','NATIVE_COL_048'],
    'Hawaiian Paradise on the upper west return, above the red-bean-bread unit; '
    'actual inside floor return defines west limit, with the hall kept open.')
put(7, box(-71.78,70.65,-66.75,75.85), ['west_actual_entry_return','NATIVE_COL_056','NATIVE_COL_038'],
    'International-market red-bean bread below Hawaiian Paradise and above west boarding access; '
    'the existing door/landing traces remain excluded.')

put(10, box(-64.15,43.7,-57.1,47.88), ['NATIVE_COL_066','west_inner_return_qy48'],
    'Pharmacy uses the first inward west-return bay facing the hall. It is not an L extension '
    'and is not bound to an old pharmacy-named generated wall.')
put(11, box(-64.15,39.15,-57.1,43.55), ['west_inner_return_qy39','NATIVE_COL_066'],
    'American Trailer continues immediately below pharmacy in the same inside return row.')
put(12, Polygon([(-64.15,39.0),(-57.1,39.0),(-57.1,34.0),(-63.95,34.0)]),
    ['west_inner_return_to_diagonal','NATIVE_COL_024','NATIVE_COL_017'],
    'Gijang specialties is the lower end of the three-unit row before the diagonal hall edge.')
put(13, Polygon([(-58.0,27.8),(-53.9,20.45),(-48.65,23.45),(-52.8,30.8)]),
    ['actual_west_diagonal_floor_edge','NATIVE_COL_017','NATIVE_COL_009'],
    'International-market donuts follows the diagonal wall toward the southwest hall; '
    'the proposed long face follows the actual diagonal structural direction.')

put(14, box(-28.32,75.9,-19.92,80.9), ['NATIVE_COL_029','NATIVE_COL_028','C97_RESTAURANT_ESC_NORTH_SIDE'],
    '42sqm convenience kiosk occupies one actual 8.4m north-hall bay north of the restaurant '
    'escalator. Keep the landing and through passage separate. The published area is a cross-reference, not a scale transform.')
put(15, box(-.65,65.9,3.95,69.8), ['R11_WALL_046','native_boarding_elevator_B','NATIVE_COL_073'],
    'Wini Bini is immediately south of the north service/toilet core, on the hall side of the '
    'current wall/boarding-elevator return; not inside the toilet cell.')
put(16, box(-8.65,54.8,-4.65,57.3), ['NATIVE_COL_063','NATIVE_COL_065','north_of_central_retail_row'],
    'Rail-goods small island sits north of the Star-sand / Olive-young vertical row, '
    'west of the fixed core wall. Four by 2.5m is a review model for the official 10sqm kiosk, not as-built evidence.')
put(17, box(-9.95,40.1,-4.3,49.0), ['R11_WALL_041','R11_WALL_042','NATIVE_COL_018','NATIVE_COL_061'],
    'Busan Star-sand on the public west side of the actual central-core wall, below the goods '
    'island and above Olive Young; existing doorway/column bodies remain visible.')
put(18, box(-12.15,17.9,-4.32,36.2), ['R11_WALL_034','R11_WALL_027','NATIVE_COL_014','NATIVE_COL_003'],
    'Olive Young follows the west face of the central service block between actual lower '
    'structural rows. No duplicate service rooms or transport enclosure is proposed.')

put(19, box(-37.08,-11.45,-28.69,-7.12), ['C177_UPPER_028','C177_UPPER_029','WEST_ESC_3_4_NORTH_LANDING'],
    'West BnC island is north of the existing lower west escalators and west of the Hwangong-labelled '
    '2F source island. Footprints are split on the actual column axis, with landing clearance retained.')
put(20, box(-28.65,-11.45,-20.3,-7.12), ['C177_UPPER_029','C177_UPPER_030','WEST_THIRD_ESC_NORTH_SIDE'],
    'This is only the 2F source-map Hwangong location candidate, east of BnC. '
    'It does not recreate the user-deleted 1F SHOP17 booth and grants no Scene restoration authority.')
put(21, box(-58.35,-30.28,-45.6,-23.0), ['west_WC_south_turn','C177_UPPER_080','C177_UPPER_081','R11_WALL_015'],
    'Southwest convenience location spans the actual front return west of the Yeongjudang '
    'unit and before the west elevator bank. Existing partitions remain gray; their openings/layout are not changed here.')
put(22, box(-45.37,-30.25,-37.13,-24.72), ['R11_WALL_016','R11_WALL_017','C177_UPPER_081','C177_UPPER_082'],
    'Yeongjudang is in the existing southwest front return immediately east of the convenience '
    'store, ending at the actual front column bay before the EV/stair recess.')

for n, coords, keys in [
    (23,[(-25.01,-28.334),(-21.41,-28.35)],['134908987','747974883']),
    (24,[(6.27,-28.461),(9.88,-28.483)],['911265412','146414747']),
]:
    # The verified public facade correspondence is not proof that the shaft/stair
    # cell immediately behind it is a retail floor. Plot frontage only.
    line=LineString(coords)
    proposals[n]={'geometry':mapping(line),'area_MODEL_m2':None,
        'frontage_length_MODEL_m':line.length,
        'basis':'Flowers / Boksoondoga public facade correspondence beside the central entry '
                'is retained from current photo/source packet. Internal shop depth remains '
                'unresolved; do not color the stair/EV opening behind this facade as shop floor.',
        'structural_anchor_keys':keys,'mode':'EXISTING_STOREFRONT_LOCATION_FRONTAGE_ONLY',
        'frontage_and_dimensions_status':'LOCATION_REVIEW; interior extent pending native binding',
        'source_pixels_used_as_metric':False,'existing_geometry_edited':False}

put(29, box(26.2,-27.5,30.2,-24.2), ['C177_UPPER_089','C177_UPPER_090','east_hall_front_row'],
    'Palgong Tea is a small interior island near the front row, well north of the curved east '
    'retail fascia and west of the east escalator.')
put(30, box(49.75,-2.35,57.75,-.55), ['R11_WALL_061_P2','C177_UPPER_021','C177_UPPER_023','east_track_neck'],
    'Namcheon-halme kiosk is on the hall-facing south side of the east service/track-access neck; '
    'it does not occupy the protected Official6 corridor or escalator landing.')

# Curved eastern storefront sequence: first preserve the lounge bay. Shop bay
# boundaries come from the saved column axes; the actual curved floor edge bounds
# their backs. A public strip remains between columns and storefront fronts.
# Lower envelope of the actual exterior: internal column-hole seams must not
# become storefront corners. Sample at actual exterior vertex X coordinates and
# structural bay axes, then use the lowest actual floor contact at each X.
curve_x={42.98,51.018,59.056,67.094,82.72}
for p in polygon_parts(floor):
    curve_x.update(round(x,4) for x,y in p.exterior.coords if 42.98<x<82.72 and y<-24)
lower=[]
for x in sorted(curve_x):
    hit=floor.intersection(LineString([(x,-60),(x,-15)]))
    if not hit.is_empty:lower.append((x,hit.bounds[1]))
retail_strip=Polygon(lower+[(x,y+10.85) for x,y in reversed(lower)])
for number, x0, x1, keys in [
    (31,42.98,51.018,['C177_UPPER_092','C177_UPPER_093']),
    (32,51.018,59.056,['C177_UPPER_093','C177_UPPER_094']),
    (33,59.056,67.094,['C177_UPPER_094','O5_0453_AXIS']),
    (34,67.094,82.72,['O5_0453_AXIS','O5_0448_AXIS','O5_0444_AXIS']),
]:
    put(number, retail_strip.intersection(box(x0,-49,x1,-22.6)), keys+['actual_east_curved_floor_perimeter'],
        'Official source order is lounge / convenience / Soban / BnC / Subway. '
        'Keep the lounge outside this work overlay; bay divisions use actual repeated '
        'current column axes, rear limits use the current curved perimeter. '
        'A provisional 10.85m interior strip follows the actual facade curve; this depth is '
        'a review model cross-referenced to the published CVS area and actual bay width, not a photo-measured wall.')

pending = {
    8: 'West cropped exterior source location: actual Layer0_5/6 section geometry exists, '
       'but no qualified 2F retail floor/cell binding. No arbitrary migration into the hall.',
    9: 'Same west cropped exterior limitation as Sowoldang; platform/roof projections are '
       'not assumed to be completed retail floors.',
    25: 'User identifies this brand as an existing native empty shell, but the exact individual '
        'native shop body cannot yet be uniquely bound. Do not draw another booth over the central entrance/EV recess.',
    26: 'Existing native empty-shell claim retained; individual native-facing retail cell not '
        'yet identified independently of illustrative label stacking.',
    27: 'Existing native empty-shell claim retained; do not convert the east EV/service cell '
        'or exterior roof projection to a new shop from a colored map label.',
    28: 'Existing native empty-shell claim retained; exact cell needs correspondence to native '
        'retail wall/door geometry. No new geometry implied by source stacked labels.',
}

entities = []
for src in sources['shop_entities']:
    n = int(src['source_entity_id'].split('-')[-1])
    item = dict(src)
    if n in proposals:
        item.update(proposals[n]); item['status'] = 'PROPOSED_METRIC_LOCATION_FOR_USER_REVIEW'
    else:
        item.update(status='HOLD_INDIVIDUAL_NATIVE_BINDING', reason=pending[n], geometry=None)
    entities.append(item)

out = WORK/'2F-SHOP-LOCATION-REVIEW-R20-NO-TEXT'
record = {
    'schema':'chooguard.2F.shop_location_review.r20.v1',
    'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'status':'REVIEW_ONLY_NOT_CONSTRUCTION_AUTHORITY',
    'coordinate_system':cad['units'], 'worldXZ_to_q':cad['worldXZ_to_q'],
    'immutable_base':str(BASE.relative_to(ROOT)),
    'immutable_base_sha256':hashlib.sha256(BASE.read_bytes()).hexdigest(),
    'source_register':'source-lane/SHOP-IDENTITY-REGISTER.json',
    'source_latest_acquired':'100083811; official publication 2026-10-07; page1 Busan 2F only',
    'newer_publication':'100083825 2026-10-14 found; attachment returned empty and is not used as a drawing',
    'source_entities':entities, 'plotted_location_count':len(proposals),
    'area_location_count':26,'frontage_only_location_count':2,
    'held_individual_binding_count':len(pending),
    'source_raster_warped':False, 'Scene_Assets_changed':False,
    'proposal_vector_numeric_grid_metres':.001,'base_gray_vectors_unsnapped':True,
    'current_CAD_reference_edited':False, 'current_geometry_newly_confirmed':False,
    'visible_added_text':False, 'work_color':'orange', 'references':'achromatic locked metric layers',
    'excluded_facilities':sources['facilities_not_shops'],
    'protected_Official6':'actual matching-level geometry retained; not a destination for inferred retail',
    'validation_owner':'user; no agent Scene QA/reopen/probe/capture',
    'limitations':['Candidate locations and frontages are MODEL proposals, not field measurements.',
                  'Actual gray current geometry is preserved even where a source shop correspondence remains unresolved.',
                  'The source architectural raster and commercial map share one background and are not independent metric surveys.',
                  'R20 does not supersede current CAD/floor authority or user-deletion tombstones.'],
}
(out.with_suffix('.json')).write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')

# Metric vector PDF. Orange fill goes underneath all retained reference lines.
# No added text, symbols, arrows, source raster, legend or page number.
views = [(-83,-49,96,98), (-77,60,17,97), (-78,12,18,62),
         (-76,-37,19,13), (17,-49,96,19)]
pdf = out.with_suffix('.pdf')
c = canvas.Canvas(str(pdf),pagesize=(1190.55,841.89),pageCompression=1)
c.setTitle('2F specialist store locations for user review R20')
for region in views:
    minx,miny,maxx,maxy = region
    scale = min(1150.55/(maxx-minx),801.89/(maxy-miny))
    dx=(1190.55-(maxx-minx)*scale)/2-minx*scale
    dy=(841.89-(maxy-miny)*scale)/2-miny*scale
    area=box(*region)
    c.saveState(); clip=c.beginPath();clip.rect(18,18,1154.55,805.89);c.clipPath(clip,stroke=0,fill=0)
    for entry in proposals.values():
        g=shape(entry['geometry']).intersection(area)
        c.setFillColorRGB(1,.73,.28);c.setStrokeColorRGB(.94,.36,.035);c.setLineWidth(.65)
        if g.geom_type in ['LineString','MultiLineString']:
            c.setStrokeColorRGB(.94,.36,.035);c.setLineWidth(3.2)
            lines=[g] if g.geom_type=='LineString' else list(g.geoms)
            for ln in lines:
                c.lines([(a[0]*scale+dx,a[1]*scale+dy,b[0]*scale+dx,b[1]*scale+dy)
                         for a,b in zip(ln.coords,list(ln.coords)[1:])])
            continue
        for poly in polygon_parts(g):
            p=c.beginPath()
            for ring in [poly.exterior,*poly.interiors]:
                p.moveTo(ring.coords[0][0]*scale+dx,ring.coords[0][1]*scale+dy)
                for x,y in list(ring.coords)[1:]:p.lineTo(x*scale+dx,y*scale+dy)
                p.close()
            c.drawPath(p,stroke=1,fill=1,fillMode=0)
    for obj in cad['objects']:
        c.setStrokeGray(.69);c.setLineWidth(.24)
        for ring in obj['surface_rings']:
            if not LineString(ring).intersects(area):continue
            p=c.beginPath();p.moveTo(ring[0][0]*scale+dx,ring[0][1]*scale+dy)
            for x,y in ring[1:]:p.lineTo(x*scale+dx,y*scale+dy)
            c.drawPath(p,stroke=1,fill=0)
        segments=obj['sections'].get('8.40000',[])
        if not segments and obj['kind']=='CURRENT':
            lower=[(float(k),v) for k,v in obj['sections'].items() if 7.02<float(k)<8.4]
            if lower:segments=max(lower,key=lambda x:x[0])[1]
        c.setStrokeGray(.28 if obj['kind']=='CURRENT' else .44)
        c.setLineWidth(.38 if obj['kind']=='CURRENT' else .28)
        lines=[]
        for a,b in segments:
            if max(a[0],b[0])<minx or min(a[0],b[0])>maxx or max(a[1],b[1])<miny or min(a[1],b[1])>maxy:continue
            lines.append((a[0]*scale+dx,a[1]*scale+dy,b[0]*scale+dx,b[1]*scale+dy))
        if lines:c.lines(lines)
    c.restoreState();c.showPage()
c.save()

# Preserve the base DXF entities verbatim. Add AC1009 orange work boundaries,
# no text entities. Metadata retains each source identity and holes.
dxf_base=BASE.with_suffix('.dxf').read_text()
marker='0\nENDSEC\n0\nEOF\n'
if not dxf_base.endswith(marker):raise ValueError('Unexpected base DXF footer')
overlay=[]
def put(*v):overlay.extend(str(x) for x in v)
layer_entries=[]
for n,entry in proposals.items():
    layer=f'PROPOSED_2F_SHOP_R20_{n:02d}'
    layer_entries.extend([0,'LAYER',2,layer,70,0,62,30,6,'CONTINUOUS'])
    geometry=shape(entry['geometry'])
    if geometry.geom_type=='LineString':
        for a,b in zip(geometry.coords,list(geometry.coords)[1:]):
            put(0,'LINE',8,layer,62,30,10,f'{a[0]:.9f}',20,f'{a[1]:.9f}',30,0,
                11,f'{b[0]:.9f}',21,f'{b[1]:.9f}',31,0)
        continue
    for p in polygon_parts(geometry):
        for ring in [p.exterior,*p.interiors]:
            put(0,'POLYLINE',8,layer,62,30,66,1,70,1,10,0,20,0,30,0)
            for x,y in list(ring.coords)[:-1]:put(0,'VERTEX',8,layer,62,30,10,f'{x:.9f}',20,f'{y:.9f}',30,0)
            put(0,'SEQEND',8,layer)
tokens=dxf_base.splitlines()
start=tokens.index('LAYER')
# First LAYER follows TABLE; next pair is table record count.
tokens[start+2]=str(int(tokens[start+2])+len(proposals))
end=next(i for i in range(start,len(tokens)-1) if tokens[i]=='0' and tokens[i+1]=='ENDTAB')
tokens[end:end]=[str(x) for x in layer_entries]
dxf_base='\n'.join(tokens)+'\n'
out.with_suffix('.dxf').write_text(dxf_base[:-len(marker)]+'\n'.join(overlay)+'\n'+marker)

receipt={
    'status':'REVIEW_ARTIFACTS_CREATED_NOT_SCENE_APPLIED',
    'pdf':str(pdf.relative_to(ROOT)), 'dxf':str(out.with_suffix('.dxf').relative_to(ROOT)),
    'data':str(out.with_suffix('.json').relative_to(ROOT)),
    'pdf_pages':len(views),'plotted_locations':len(proposals),'area_locations':26,
    'frontage_only_locations':2,'held_individual_bindings':len(pending),
    'base_sha256_before_and_after':hashlib.sha256(BASE.read_bytes()).hexdigest(),
    'source_image_registered_or_warped':False,'Scene_Assets_changed':False,
    'source_documents_opened':False,'overlay_opened':False,
}
(WORK/'REVIEW-ARTIFACT-RECEIPT-R20.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(receipt,ensure_ascii=False))
