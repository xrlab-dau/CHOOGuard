"""Offline R22 overlay: actual CAD constructions, user marks as locators only."""
from pathlib import Path
import json, datetime, hashlib
from shapely import union_all, set_precision
from shapely.geometry import shape, mapping, Polygon, LineString, box

ROOT=Path('/Users/um-yunsang/CHOOGuard')
WORK=ROOT/'.state/ACTIVE-2F-SHOP-USER-BOUNDARIES-R22'
OLD=ROOT/'.state/ACTIVE-2F-SHOP-STRUCTURE-REVIEW-R21/2F-SHOP-STRUCTURAL-WORK-OVERLAY-R21-NO-TEXT.json'
BASE=ROOT/'.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/cad/r19/CURRENT-LIVE-2F-OBJECT-VECTOR-CAD-R19-NO-TEXT.json'
cad=json.loads(BASE.read_text());previous=json.loads(OLD.read_text())
struct=json.loads((ROOT/'.state/ACTIVE-2F-SHOP-LOCATION-REVIEW-R20/geometry-lane/STRUCTURAL-ANCHORS-AND-CELLS-R20.json').read_text())
floor=shape(struct['floor']['geometry'])
cols={a['object_id'].split('-')[-2]:a for a in struct['generated_2F_columns_UNCONFIRMED']}
walls={a['object_id'].split('-')[-2]:a for a in struct['fixed_saved_walls']}
solid=union_all([shape(a['geometry']) for a in [*cols.values(),*walls.values(),*struct['transport_matching_level_profiles']] if a.get('geometry')])
protected=shape(struct['official6_protected']['actual_Y7_surface_geometry'])
proposals=[]
replaced={10,11,12,17,18,21,22,23,24,25,26,27,28,29,30,31,32,33,34}
for a in previous['proposals']:
    if not replaced.intersection(a['identity_numbers']):
        a=dict(a);a['retained_R21_review_candidate_not_user_confirmation']=True
        proposals.append(a)

def parts(g,kind='Polygon'):
    if g.is_empty:return []
    if g.geom_type==kind:return [g]
    return [p for c in getattr(g,'geoms',[]) for p in parts(c,kind)]

def add(n,name,geom,frontage,basis,evidence,mode,construction=None,already_clipped=False):
    raw=shape(geom) if isinstance(geom,dict) else geom
    g=raw if already_clipped else raw.intersection(floor).difference(union_all([solid,protected]))
    if g.geom_type in ('Polygon','MultiPolygon','GeometryCollection'):
        g=union_all([p for p in parts(set_precision(g,.001)) if p.area>.04])
    e={'id':f'R22_SHOP_{n:02d}','identity_numbers':[n],'brand':name,'geometry':mapping(g),
       'gross_proposed_region_MODEL_m2':raw.area,'net_colored_region_MODEL_m2':g.area,
       'mode':mode,'basis_MODEL':basis,'evidence':evidence,
       'boundary_inferred':True,'partial_work_region':False,'construction':construction,
       'metric_status':'USER_REVIEW_MODEL_NOT_FIELD_MEASUREMENT',
       'user_annotations_are_locations_not_literal_metric_boundaries':True,
       'Scene_Assets_changed':False}
    if frontage is not None:
        f=shape(frontage) if isinstance(frontage,dict) else LineString(frontage)
        e['frontage']=mapping(f)
    proposals.append(e);return e

westp=WORK/'west-entry-lane/WEST-ENTRY-SHOPS-RETURN-MODEL-R22.json'
west=json.loads(westp.read_text())
for a in west['proposals']:
    n={'새부산약국':10,'아메리칸트레일러':11,'기장특산품':12}[a['brand']]
    e=add(n,a['brand'],a['geometry'],a['public_frontage_qx_minus_qy_m'],
          ' '.join(a['boundary_method']),[str(westp.relative_to(ROOT))],
          'ACTUAL_NATIVE_RETURN_EXTENSION_RETAIL_WORK_MODEL',west['source_segments'])
    e['preserved_entry_and_approach']=west['do_not_place_shop_or_wall']

nativep=WORK/'native-front-lane/NATIVE-FRONT-SHOP-WORK-POLYGONS-R22.json'
native=json.loads(nativep.read_text())
for a in native['shops']:
    e=add(a['identity_number'],a['brand'],a['geometry'],a.get('frontage'),a['basis'],
          [str(nativep.relative_to(ROOT))],a['mode'],a['actual_segment_anchors'],already_clipped=True)
    e['native_shell_reuse']=True;e['new_booth_shell_proposed']=False
    e['native_owner_ids']=a['native_owner_ids'];e['user_annotation_index']=a['user_annotation_index']
    e['boundary_status']=a['boundary_status']

reservedp=WORK/'reserved-room-lane/RESERVED-ROOMS-AND-SE-VECTOR-MODELS-R22.json'
reserved=json.loads(reservedp.read_text())
for a in reserved['models']:
    n={'편의점_SW113':21,'영주당부산과자집':22,'편의점_SE87':31,
       '소반한식':32,'비엔씨제과_E':33,'써브웨이':34}[a['shop']]
    e=add(n,a['shop'],a['geometry'],a.get('frontage'),a['construction'],
          [str(reservedp.relative_to(ROOT))],a['mode'],a.get('actual_native_segment_anchors',a.get('anchors')))
    e['limits']=a['limits']
    if n in (21,22):e['native_or_existing_room_reuse']=True;e['new_booth_shell_proposed']=False
    else:e['blue_example_line_intersections']=reserved['line_by_line_intersections']

# Central island stores: shared frontage/depth from the locally dimensioned
# Goods original; longitudinal stops are normals from actual existing wall faces.
# This replaces short frontage-only ranges with bounded MODEL work regions.
goods=next(a for a in proposals if a['identity_numbers']==[16])
gr=shape(goods['structural_region_before_solid_subtraction']);gx0,_,gx1,_=gr.bounds
for n,name,low_id,high_id,high_side in [
    (17,'부산별빛샌드','1201191874','1404385831',3),
    (18,'올리브영','873429150','263894832',3),
]:
    lo=shape(walls[low_id]['geometry']).bounds[3]
    hi=shape(walls[high_id]['geometry']).bounds[high_side]
    raw=box(gx0,lo,gx1,hi)
    construction={
        'public_front_and_rear':'extend dimensioned Goods booth side planes along the actual rectangular-column row',
        'public_plane_qx':gx0,'rear_plane_qx':gx1,
        'south_normal_from_actual_wall':{'id':walls[low_id]['object_id'],'profile':mapping(shape(walls[low_id]['geometry']))},
        'north_normal_from_actual_wall':{'id':walls[high_id]['object_id'],'profile':mapping(shape(walls[high_id]['geometry']))},
        'actual_pillars_inside_work':['1922034296'] if n==17 else ['664227203','1147506611'],
        'qualification':'New tenant boundaries are MODEL. Original SITE gives shared booth morphology; source graphics are not metric. User red locators select the bays.'}
    e=add(n,name,raw,[[gx0,lo],[gx0,hi]],
          'User red location + existing architectural wall-end normals + Goods dimensioned shared facade alignment. '
          'Current columns remain fixed and their actual bodies are not construction work. '
          'No colored map width/depth is reused.',
          ['inputs/USER-ANNOTATIONS-METRIC-LOCATORS-R22.json',str(OLD.relative_to(ROOT)),
           '.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/research/root/2026-goods-shop-architecture-original.pdf'],
          'BOUNDED_SOURCE_AND_CAD_NORMAL_CONSTRUCTION_MODEL',construction)

# User relocated Palgong Tea into the higher actual four-column bay. Do not keep
# the R21 frontage ten metres farther south, or infer kiosk height from a source box.
pl=shape(cols['1727208799']['geometry']).bounds
pr=shape(cols['1025727368']['geometry']).bounds
pb=shape(cols['2128157048']['geometry']).bounds
pg=box(pl[2],pb[3],pr[0],pr[1])
e=add(29,'팔공티',pg,[[pl[2],pr[1]],[pr[0],pr[1]]],
      'User annotation26 selects actual R03/R04 bay. Work envelope uses the inner faces of '
      'actual052/053/071/072 supports. This envelope identifies the work bay; it is not a '
      'measurement of the final kiosk counter footprint.',
      ['inputs/USER-ANNOTATIONS-METRIC-LOCATORS-R22.json'],'USER_MARKED_STRUCTURAL_WORK_BAY_NOT_LEASE_FOOTPRINT',
      [{'id':cols[k]['object_id'],'profile':cols[k]['geometry']} for k in ['1727208799','1025727368','2128157048','2101415011']])
e['final_counter_extent_pending_user_review']=True

# Namcheon: previously represented by the wrong shifted frontage. The red locator
# now selects the actual existing room between060 and061, with north stop at the
# existing pier face; its hall-facing glazing stays on061_P2, outside Native6.
left=shape(walls['1098542955']['geometry']).bounds
right=shape(walls['1104302546']['geometry']).bounds
front=shape(walls['2069855408']['geometry']).bounds
ng=box(left[2],front[3],right[0],min(left[3],right[3]))
add(30,'남천할매떡볶이',ng,[[left[2],front[1]],[right[0],front[1]]],
    'User red annotation27 identifies existing room060/061. Use actual inner wall faces '
    'and pier-side ends, extend the existing side lines to their normal intersections. '
    'Existing gray room, public path and protected official corridor remain fixed.',
    ['inputs/USER-ANNOTATIONS-METRIC-LOCATORS-R22.json'],'EXISTING_RESERVED_ROOM_INTERIOR_FITOUT_MODEL',
    [{'id':walls[k]['object_id'],'geometry':walls[k]['geometry']} for k in ['1098542955','1104302546','2069855408']])

covered={n for a in proposals for n in a['identity_numbers']}
annotations=json.loads((WORK/'inputs/USER-ANNOTATIONS-METRIC-LOCATORS-R22.json').read_text())
# Existing official room marks in the protected east corridor remain gray. They
# are reference/anchor clues, not authority to build duplicated shops over Native6.
annotation_resolution=[]
for a in annotations['annotations']:
    i=a['annotation_index']
    status='USER_LOCATION_CUE_SNAP_TO_EXISTING_GEOMETRY'
    if i in [28,29]:status='EXISTING_PROTECTED_OFFICIAL_REFERENCE_NOT_DUPLICATE_RETAIL'
    if a['type'][1]=='Line':status='CAD_EXTENSION_NORMAL_INTERSECTION_EXAMPLE_APPLIED_TO_SE_WORK'
    if a['type'][1]=='Stamp':status='PRESERVED_USER_MARK_NOT_A_METRIC_BOUNDARY'
    annotation_resolution.append({'index':i,'xref':a['xref'],'status':status})

stem=WORK/'2F-SHOP-CAD-LINE-EXTENSION-REVIEW-R22-NO-TEXT'
record={
    'schema':'chooguard.2f.shop_cad_line_construction_review.r22.v1',
    'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'status':'AWAITING_USER_REVIEW_NO_SCENE_CONSTRUCTION_AUTHORITY',
    'immutable_gray_reference':str(BASE.relative_to(ROOT)),
    'immutable_gray_reference_sha256':hashlib.sha256(BASE.read_bytes()).hexdigest(),
    'source_user_marked_R20':'inputs/USER-MARKED-R20-20261019.pdf',
    'source_user_marked_sha256':annotations['sha256'],
    'source_current_scene':'scene-read/ACTUAL-SCENE-AND-VIEW-R22.json',
    'source_current_scene_image':'scene-read/CURRENT-SCENEVIEW-CAMERA-SOURCE-R22.png',
    'coordinates':cad['units'],'worldXZ_to_q':cad['worldXZ_to_q'],
    'method':'extend existing CAD lines; intersect; project normals; connect actual supports; retain existing room shells',
    'commercial_plan_role':'identity and relative neighborhood only',
    'user_mark_shapes_used_as_final_dimensions':False,
    'proposals':proposals,'source_entities':previous['source_entities'],
    'work_region_count':len(proposals),'shop_identities_plotted':len(covered),
    'annotations':annotation_resolution,
    'preserved_west_entry':west['do_not_place_shop_or_wall'],
    'source_epochs_and_inferred_dimensions_qualified':True,
    'visible_added_text':False,'reference_color':'achromatic','work_color':'orange',
    'Scene_Assets_changed':False,'source_raster_warped':False,
    'base_geometry_newly_confirmed':False,
    'Scene_screen_inspection_requested_by_user':True,
    'Scene_save_reopen_tests_or_general_QA':False,
    'user_validation_owner':True,
}
for s in record['source_entities']:
    n=int(s['identity_id'].rsplit('-',1)[-1]);s['work_regions']=[p['id'] for p in proposals if n in p['identity_numbers']]
stem.with_suffix('.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')

# Same offline vector-only renderer; copied into this active script below.

from reportlab.pdfgen import canvas
views = [(-83,-60,96,98), (-76,61,17,97), (-77,14,19,64),
         (-66,-51,19,14), (14,-49,95,18), (-24,13,13,62)]
c = canvas.Canvas(str(stem.with_suffix('.pdf')),pagesize=(1190.55,841.89),pageCompression=1)
c.setTitle('2F shop structural work overlay R22')
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
    layer=f'R22_WORK_{i:02d}_{entry["id"]}'
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


receipt={'status':'REVIEW_ARTIFACT_CREATED_NOT_SCENE_APPLIED','pdf':str(stem.with_suffix('.pdf').relative_to(ROOT)),'dxf':str(stem.with_suffix('.dxf').relative_to(ROOT)),'data':str(stem.with_suffix('.json').relative_to(ROOT)),'pages':len(views),'work_regions':len(proposals),'shop_identities_plotted':len(covered),'Scene_Assets_changed':False,'Preview_opened':False}
(WORK/'REVIEW-ARTIFACT-RECEIPT-R22.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(receipt,ensure_ascii=False))
