"""Bounded offline PDF/source geometry audit. All writes stay in this worker lane."""
import json, hashlib, struct
from pathlib import Path
import numpy as np
import pymupdf as fitz
from PIL import Image, ImageDraw
from shapely.geometry import Polygon, Point, LineString
from shapely.ops import unary_union

ROOT=Path('/Users/um-yunsang/CHOOGuard')
EV=ROOT/'workers/twin/evidence/floor1-provided-pdf-20261009'
OUT=ROOT/'workers/twin/output/floor1-provided-pdf-20261009'
INV=ROOT/'.planning/2026-10-09-floor2-completion-pass2/after-facades/current-inventory.json'
PDF=Path('/Users/um-yunsang/Downloads/[붙임3] 매장 위치도.pdf')
PUBLIC=ROOT/'content/world/observed-interiors/public-1f.json'
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def dump(name,d): (OUT/name).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
inv=json.loads(INV.read_text()); meshes={x['id']:x for x in inv['meshes']}
public=json.loads(PUBLIC.read_text())
floor_r=next(r for r in inv['renderers'] if r['path']=='1층 · 원본 정합/Ground_Floor')
root_inv=np.linalg.inv(np.array(floor_r['world_matrix']))
def binary(mid):
    m=meshes[mid]; raw=(ROOT/m['binary']).read_bytes()
    assert hashlib.sha256(raw).hexdigest()==m['binary_sha256']
    nv,ni,nn=struct.unpack_from('<iii',raw)
    return np.frombuffer(raw,'<f4',nv*3,12).reshape(nv,3).astype(float), np.frombuffer(raw,'<i4',ni,12+nv*12).reshape(-1,3)
def uv_y(r,v):
    p=np.c_[v,np.ones(len(v))]@np.array(r['world_matrix']).T@root_inv.T
    return p[:,[2,0,1]]
def ident(r):
    m=meshes[r['mesh']]
    return {k:r[k] for k in ['path','global_object_id','mesh','world_matrix']}|{k:m[k] for k in ['asset','binary','binary_sha256','asset_sha256'] if k in m}
v,t=binary(floor_r['mesh']); pf=v[:,[2,0]]
floor=unary_union([Polygon(pf[f]) for f in t if np.max(np.abs(v[f,1]))<.001 and Polygon(pf[f]).area>1e-7])

# Two distinct front stair core rectangles; eight manually digitized corners.
# These are source-diagram registration correspondences, not field surveying controls.
anchor_data=[('e-front-left',[281,287],[27.42,-25.47]),('e-rear-left',[281,329],[28.34,-31.90]),
('e-rear-right',[300,329],[24.76,-32.13]),('e-front-right',[300,287],[24.29,-25.68]),
('w-front-right',[481,287],[-8.31,-28.01]),('w-front-left',[458,287],[-4.26,-27.70]),
('w-rear-left',[458,329],[-3.77,-34.10]),('w-rear-right',[481,329],[-7.82,-34.41])]
sx=np.array([a[1] for a in anchor_data],float); uv=np.array([a[2] for a in anchor_data],float)
A=np.linalg.lstsq(np.c_[sx,np.ones(len(sx))],uv,rcond=None)[0]
res=np.linalg.norm(np.c_[sx,np.ones(len(sx))]@A-uv,axis=1)
def project(p): return (np.c_[np.atleast_2d(p),np.ones(len(np.atleast_2d(p)))]@A).tolist()
def inverse(p): return ((np.asarray(p)-A[2])@np.linalg.inv(A[:2])).tolist()
anchors=[{'id':a[0],'source_native_pixel':a[1],'native_UV':a[2],'residual_model_units':float(r)} for a,r in zip(anchor_data,res)]

rooms=[
{'id':'pdf1f-disaster-center','label':'방재센터','label_confidence':'User-supplied semantic name; enlarged raster consistent with 방재센터. Earlier 재난센터 transcription rejected.','source_polygon_pixels_approx':[[44,90],[175,90],[175,173],[87,173],[44,118]],'source_label_pixel':[133,126],'note':'Left diagonal return includes a small service alcove. Keep alcove/side door detail source-only until a clearer image confirms exact jamb.'},
{'id':'pdf1f-military-consultation','label':'상담실 / 군장병 편의시설','label_confidence':'suffix directly readable; initial word before 상담실 unresolved','source_polygon_pixels_approx':[[175,90],[313,90],[313,173],[175,173]],'source_label_pixel':[248,133],'note':'The first small word is not transcribed as TMO/미군/대공 without adequate pixels.'},
{'id':'pdf1f-electrical-room','label':'전기실','label_confidence':'directly_readable','source_polygon_pixels_approx':[[313,90],[582,90],[582,132],[313,132]],'source_label_pixel':[445,113],'note':'Right edge at x582, not the neighboring unlabeled x582–616 bay. No sanitary/interior layout inferred.'}
]
doors=[
{'id':'disaster-public-double','room':'pdf1f-disaster-center','source_jamb_pixels':[[141,173],[154,173]],'visible_leaves':2,'swing':'two quarter-circle leaves into room; diagram symbol only'},
{'id':'military-public-double','room':'pdf1f-military-consultation','source_jamb_pixels':[[251,173],[267,173]],'visible_leaves':2,'swing':'two quarter-circle leaves into room; diagram symbol only'},
{'id':'electric-rear-double-a','room':'pdf1f-electrical-room','source_jamb_pixels':[[373,90],[386,90]],'visible_leaves':2,'swing':'two quarter-circle leaves toward rear side; reference only'},
{'id':'electric-rear-double-b','room':'pdf1f-electrical-room','source_jamb_pixels':[[508,90],[520,90]],'visible_leaves':2,'swing':'two quarter-circle leaves toward rear side; reference only'}]
for d in doors: d['proposed_jamb_UV']=project(d['source_jamb_pixels']);d['source_pixel_tolerance']=3;d['status']='ROOT_REVIEW_SOURCE_JAMBS_AND_NATIVE_OWNER'

# Exact current geometry sections, recomputed from the current inventory at station Y1.5.
sections=[]; render_cache={}; unreadable=[]
for r in inv['renderers']:
    if not r['active'] or not r['renderer_enabled'] or r['world_bounds_max'][1]<-.001 or r['world_bounds_min'][1]>6.7: continue
    if 'binary' not in meshes[r['mesh']]:
        unreadable.append(r);continue
    va,ta=binary(r['mesh']); q=uv_y(r,va); render_cache[r['path']]=(r,q,ta)
    if len(q)==0: continue
    if '/MainShell/' not in r['path'] and '/1F 공용/'not in r['path'] and '/1F 보강/'not in r['path']:continue
    segs=[];face_ids=[]
    for face_id,f in enumerate(ta):
        tri=q[f]; dy=tri[:,2]-1.5
        if np.min(dy)>=0 or np.max(dy)<=0:continue
        pts=[]
        for j in range(3):
            a,b=tri[j],tri[(j+1)%3]; da,db=dy[j],dy[(j+1)%3]
            if da*db<0:pts.append(a[:2]+(b[:2]-a[:2])*(-da/(db-da)))
        if len(pts)==2 and np.linalg.norm(pts[0]-pts[1])>.001:
            segs.append([p.tolist() for p in pts]);face_ids.append(face_id)
    if segs:sections.append({'owner':ident(r),'segments_UV':segs,'corresponding_face_ordinals':face_ids})
dump('current-1f-sections.json',{'inventory':str(INV.relative_to(ROOT)),'scene_sha256':inv['scene_file_sha256'],'section_y':1.5,'coordinate_frame':'station UVY; rigid inverse Ground_Floor world matrix; UV=[localZ,localX]','owners':sections})

native_lines=[(s['owner'],LineString(seg)) for s in sections if '/MainShell/' in s['owner']['path'] for seg in s['segments_UV'] if LineString(seg).length>.5]
generated_lines=[(s['owner'],LineString(seg)) for s in sections if '/1F 공용/'in s['owner']['path'] or '/1F 보강/'in s['owner']['path'] for seg in s['segments_UV'] if LineString(seg).length>.5]
protected=[(e,Polygon(e['geometry']['polygon'])) for e in public['elements'] if 'polygon'in e.get('geometry',{})]
for room in rooms:
    room['proposed_polygon_UV']=project(room['source_polygon_pixels_approx']);p=Polygon(room['proposed_polygon_UV'])
    room['ground_floor_supported_area']=p.intersection(floor).area;room['polygon_area_model_units2']=p.area
    room['ground_floor_unsupported_area']=p.difference(floor.buffer(.02)).area
    room['existing_public_polygon_overlaps']=[{'id':e['id'],'type':e['type'],'area_model_units2':p.intersection(g).area} for e,g in protected if p.intersection(g).area>.01]
    room['native_MainShell_interior_segments']=[{'owner':own,'segment_UV':list(map(list,l.coords)),'length_inside':l.intersection(p.buffer(-.10)).length} for own,l in native_lines if l.intersection(p.buffer(-.10)).length>.25]
    room['generated_public_interior_segments']=[{'owner':own,'segment_UV':list(map(list,l.coords)),'length_inside':l.intersection(p.buffer(-.10)).length} for own,l in generated_lines if l.intersection(p.buffer(-.10)).length>.25]
    # Each source boundary edge has nearest exact current native section distance.
    room['native_boundary_nearest_distances']=[float(min((LineString([a,b]).distance(l) for _,l in native_lines),default=float('inf'))) for a,b in zip(room['proposed_polygon_UV'],room['proposed_polygon_UV'][1:]+room['proposed_polygon_UV'][:1])]
    room['classification']='missing_named_recipe; proposed diagram footprint requires owner review'

wc=[e for e in public['elements'] if e['type']=='ToiletRoom']
fixture_checks=[]
for e in wc:
    p=Polygon(e['geometry']['polygon']).buffer(-.25); candidates=[]
    for path,(r,q,ta) in render_cache.items():
        if len(q)==0:continue
        # Exclude enclosing finishes. Candidate geometry must fit below 1.4Y and be inside footprint.
        if any(x in path for x in ['Ground_Floor','Kit_Floor','Kit_Ceiling','Kit_Light','Kit_RoomWall','Kit_Clad','Kit_Colliders']):continue
        if q[:,0].max()<p.bounds[0] or q[:,0].min()>p.bounds[2] or q[:,1].max()<p.bounds[1] or q[:,1].min()>p.bounds[3]:continue
        sel=[]
        for i,f in enumerate(ta):
            tr=q[f]
            if tr[:,2].min()<.05 or tr[:,2].max()>1.4:continue
            if all(p.covers(Point(a)) for a in tr[:,:2]):sel.append(i)
        if sel:candidates.append({'owner':ident(r),'face_ordinals':sel,'count':len(sel)})
    unresolved=[]
    for r in unreadable:
        lo=r['world_bounds_min'];hi=r['world_bounds_max']; corners=np.array([[x,y,z,1]for x in [lo[0],hi[0]]for y in [lo[1],hi[1]]for z in [lo[2],hi[2]]])@root_inv.T
        box=Polygon([[corners[:,2].min(),corners[:,0].min()],[corners[:,2].max(),corners[:,0].min()],[corners[:,2].max(),corners[:,0].max()],[corners[:,2].min(),corners[:,0].max()]])
        if box.intersects(p): unresolved.append({'path':r['path'],'asset':meshes[r['mesh']]['asset'],'reason':'non-readable mesh; conservative world AABB overlaps toilet interior'})
    fixture_checks.append({'recipe_id':e['id'],'kind':e['params']['kind'],'recipe_counters':{k:e['params'].get(k) for k in ['stalls','basins','urinals']},'current_low_interior_geometry_candidates':candidates,'non_readable_candidates':unresolved,'test_limitation':'All vertices inside footprint inset0.25, faces fully between Y0.05 and1.4; this is a candidate test, not proof of real station fixture counts.'})

# Deliberately bounded readable symbol centers; inaccessible/unreadable marks are not filled by defaults.
fixtures=[]
for cluster,room_id,points in [
('right-main','right-upper',[[913,138],[920,138],[927,138],[934,138],[941,138]]),
('right-main','right-lower',[[911,202],[917,202],[924,202],[930,202],[936,202]]),
('left-main','left-upper',[[58,219],[64,215],[69,212],[75,208],[80,204]]),
('left-main','left-lower',[[77,237],[83,233],[88,229],[94,225],[99,221]])]:
    for k,p in enumerate(points,1):fixtures.append({'id':f'{room_id}-wc-{k}','cluster':cluster,'source_room':room_id,'source_center_pixel':p,'diagram_symbol':'small toilet pan in stall row','classification':'VISIBLE_SYMBOL_MINIMUM; not total sanitary schedule','digitization_tolerance_pixels':2,'proposed_center_UV':project([p])[0]})
toilets={
'source_clusters':[
{'id':'left-main','rooms':2,'source_outline_pixels_approx':[[50,215],[88,191],[123,238],[85,258]],'split_line_pixels_approx':[[66,232],[104,209]],'source_room_polygons_pixels_approx':{'left-upper':[[50,215],[88,191],[104,209],[66,232]],'left-lower':[[66,232],[104,209],[123,238],[85,258]]},'adjacent_unlabelled_bay_pixels_approx':[[85,258],[123,238],[132,250],[92,275]],'adjacent_bay_note':'Below the male/female pair is a separate unlabelled bay; do not count it as a third toilet or include it in WC outline. Preserve existing boh-uplus5564 until exact correspondence review.','attached_to_main_hall':True,'native_candidates':['1Fp-wc-uplus-male','1Fp-wc-uplus-female'],'gender_assignment':'Source labels too blurred for exact gender mapping; do not swap recipe by assumed symbol counts.'},
{'id':'right-main','rooms':2,'source_outline_pixels_approx':[[897,130],[945,130],[945,211],[897,211]],'split_line_pixels_approx':[[897,172],[945,172]],'source_room_polygons_pixels_approx':{'right-upper':[[897,130],[945,130],[945,172],[904,172],[904,152],[897,152]],'right-lower':[[897,172],[945,172],[945,211],[897,211]]},'attached_to_main_hall':True,'native_candidates':['1Fp-wc-uminus-male','1Fp-wc-uminus-female'],'gender_assignment':'Do not equate upper/lower to current recipe gender without clearer source.'},
{'id':'detached-detail','rooms':2,'source_outline_pixels_approx':[[305,480],[455,480],[455,520],[305,520]],'attached_to_main_hall':False,'world_location':'UNRESOLVED; main affine MUST NOT be applied to this inset','classification':'Two additional drawn toilet rooms with circulation detail; no visible connector or matched scene owner.'}],
'drawn_room_count':6,'main_hall_located_room_count':4,'unlocated_detail_room_count':2,'current_recipe_room_count':len(wc),
'confidently_digitized_small_WC_symbols_minimum':len(fixtures),'other_fixture_counts':'UNKNOWN: accessible pans, basins and urinals not transcribed from unreadable symbols',
'source_stall_rows':[
{'room':'right-upper','visible_regular_stalls_minimum':5,'row_backwall_pixels_approx':[[910,132],[944,132]],'row_front_pixels_approx':[[910,143],[944,143]],'pan_heading_source_pixel':[0,1]},
{'room':'right-lower','visible_regular_stalls_minimum':5,'row_backwall_pixels_approx':[[908,208],[940,208]],'row_front_pixels_approx':[[908,199],[940,199]],'pan_heading_source_pixel':[0,-1]},
{'room':'left-upper','visible_regular_stalls_minimum':5,'row_backwall_pixels_approx':[[51,214],[84,192]],'row_front_pixels_approx':[[59,224],[91,202]],'pan_heading_source_pixel':[.55,.83]},
{'room':'left-lower','visible_regular_stalls_minimum':5,'row_backwall_pixels_approx':[[69,232],[101,210]],'row_front_pixels_approx':[[77,243],[108,221]],'pan_heading_source_pixel':[.55,.83]}],
'stall_row_limit':'Raster contours have 3px uncertainty. Five regular stalls per row are a visible lower bound; additional accessible fixtures cannot be counted. Per-stall side walls must follow visible partitions after root crop review, without generic spacing defaults.',
'fixtures':fixtures,'current_geometry_checks':fixture_checks,
'builder':{'path':'AgentScripts/MajibangBuilder.cs','sha256':sha(ROOT/'AgentScripts/MajibangBuilder.cs'),'KitToiletRoom_lines':[6605,6632],'actual_behavior':'Builds walls, floor and ceiling only. No KWc/KUrinal/KVanity/KStall calls in this method; helpers exist but no automatic fixture defaults.'}}
for cluster in toilets['source_clusters']:
    if not cluster['attached_to_main_hall']:continue
    cluster['source_pixel_tolerance']=3
    cluster['proposed_room_polygons_UV']={k:project(v)for k,v in cluster['source_room_polygons_pixels_approx'].items()}
    cluster['current_canonical_polygon_discrepancies']=[{'source_room':k,'source_UV_polygon':project(v),'existing_overlap_areas':[{'recipe_id':e['id'],'overlap_model_units2':Polygon(project(v)).intersection(g).area}for e,g in protected if Polygon(project(v)).intersection(g).area>.01]}for k,v in cluster['source_room_polygons_pixels_approx'].items()]
for f in fixtures:
    f['current_toilet_footprint_membership']=[e['id']for e in wc if Polygon(e['geometry']['polygon']).covers(Point(f['proposed_center_UV']))]
    f['implementation_note']='Place only after source room/polygon reconciliation; do not force pan into a current enclosure whose footprint differs.'
dump('toilet-source-controls.json',toilets)

# Withheld source checks: identities and shape centroids differ by epoch; report, do not hide.
plan=json.loads((ROOT/'.planning/2026-09-23-video-twin/first-floor-plan.json').read_text());units={u['boardNo']:u for u in plan['units']}
pdf_page=fitz.open(PDF)[0]; dr=pdf_page.get_drawings();rect=pdf_page.get_image_rects(212)[0]
def pt_to_px(p): return [(p[0]-rect.x0)*960/rect.width,(p[1]-rect.y0)*572/rect.height]
hold=[]
for no,box in [(3,[235.44,306.84,268.08,374.88]),(4,[300.72,306.48,334.8,374.52]),(7,[538.92,306.84,598.92,339.96]),(8,[636,246.72,705.6,276.6])]:
    source=pt_to_px([(box[0]+box[2])/2,(box[1]+box[3])/2]);native=list(Polygon(units[no]['polygon']).centroid.coords)[0];pred=project([source])[0]
    hold.append({'boardNo':no,'role':'generated_model_vs_source_discrepancy_candidate; not independent validation','source_pixel':source,'current_native_centroid_UV':list(native),'projected_UV':pred,'residual_model_units':float(np.linalg.norm(np.array(pred)-native)),'caveat':'The generated model may be misplaced. Different tenant outlines/shape centroids also vary across epochs. These differences do not invalidate or independently validate the PDF fit.'})
registration={'method':'Affine reference scaffold from two uniquely identified front stair-core rectangles; target polygons are Naver-derived generated public-1f recipes, not independent original MainShell controls','pixel_homogeneous_row_to_UV_matrix':A.tolist(),'fit_anchors':anchors,'fit_RMS_model_units':float(np.sqrt(np.mean(res**2))),'fit_max_model_units':float(res.max()),'source_pixel_digitization_tolerance':2,'nominal_local_digitization_UV_uncertainty':float(2*np.linalg.norm(A[:2],ord=2)),'withheld_checks':hold,'extrapolation_limitation':'Core fit lies at source x281–481,y287–329. Rear strip y90–173 is extrapolated. Whole-hall independent original-native heldouts remain insufficient: repeated small columns cannot be uniquely matched by ordinal alone. Generated-shop discrepancies may be placement defects and are not a hard fit rejection. Proposed UV supports bounded diagram architecture with ownership review; it is not exact field wall location. Preserve native shell/floor and do not claim centimetre registration.','native_MainShell_overlay':'workers/twin/evidence/floor1-provided-pdf-20261009/native-MainShell-on-PDF-core-fit.png','native_coordinate_frame':'UV=[Ground_Floor root local Z, local X]; Y local Y','PDF_point_to_native_pixel':{'page_image_rect':list(rect),'native_image_size':[960,572],'x':'(pageX-rect.x0)*960/rect.width','y':'(pageY-rect.y0)*572/rect.height'},'same_physical_layout_independently_verified':False}
dump('registration.json',registration)
packet={'schema':'chooguard.provided-1f-pdf-correction-packet.v1','status':'ROOT_REVIEW_SOURCE_SUPPORTED_TOPOLOGY_WITH_EXPLICIT_NATIVE_PLACEMENT_LIMITS','source_pdf':str(PDF),'source_pdf_sha256':sha(PDF),'reference_only':True,'site_accuracy_verified':False,'scene_inventory':str(INV.relative_to(ROOT)),'inventory_sha256':sha(INV),'scene_sha256':inv['scene_file_sha256'],'source_manifest':'workers/twin/evidence/floor1-provided-pdf-20261009/source-manifest.json','registration':'workers/twin/output/floor1-provided-pdf-20261009/registration.json','floor_support_owner':ident(floor_r),'ground_floor_union_area':floor.area,'ground_floor_union_bounds':list(floor.bounds),'rooms':rooms,'doors':doors,'open_area':{'label':'군인 맞이방','source_label_pixel':[281,193],'source_public_extent_approx':[[87,173],[313,173],[313,250],[128,250],[87,194]],'classification':'Open waiting-area label; no extra enclosed lounge walls authorized by source.'},'toilets':'workers/twin/output/floor1-provided-pdf-20261009/toilet-source-controls.json','implementation_boundaries':['Current native MainShell/room/collider identities are in current-1f-sections.json; do not mutate unrelated faces.','Room heights, wall thicknesses and actual door hardware are not supplied by this raster.','Four clearly depicted service double-door gaps are source topology; exact UV/existing boundary ownership requires root review.','Detached detail has no demonstrated world location or floor support; do not fabricate a projecting wing.','Twenty explicit small WC symbols are a lower bound; total accessible pans/urinals/basins remain unknown and must not be filled by default.'],'canonical_audit':{'public_recipe':str(PUBLIC.relative_to(ROOT)),'public_recipe_sha256':sha(PUBLIC),'record_count':len(public['elements']),'toilet_record_count':len(wc),'military_disaster_electrical_named_records':0,'name_absence_caveat':'Anonymous existing Room/core/wall geometry may cover some source lines; exact section overlap is included, so zero names alone does not establish geometric absence.'}}
dump('correction-packet.json',packet)

# Visual receipt: original source with manual controls; no generative restoration.
im=Image.open(EV/'embedded-architecture-white.png').convert('RGB').resize((1920,1144),Image.Resampling.NEAREST);d=ImageDraw.Draw(im)
colors=['#DC3F2B','#2477C8','#B07713']
for room,col in zip(rooms,colors):
    pts=[(2*x,2*y) for x,y in room['source_polygon_pixels_approx']];d.line(pts+[pts[0]],fill=col,width=3);p=room['source_label_pixel'];d.text((p[0]*2,p[1]*2+10),room['id'],fill=col)
for a in anchors:
    x,y=a['source_native_pixel'];d.ellipse((2*x-5,2*y-5,2*x+5,2*y+5),fill='#00AA88')
for f in fixtures:
    x,y=f['source_center_pixel'];d.ellipse((2*x-3,2*y-3,2*x+3,2*y+3),outline='#FF00AA',width=2)
for door in doors:d.line([(x*2,y*2)for x,y in door['source_jamb_pixels']],fill='#FF5500',width=5)
im.save(EV/'source-registered-controls.png')
print(json.dumps({'registration_RMS':registration['fit_RMS_model_units'],'withheld':hold,'room_checks':[{'id':r['id'],'unsupported':r['ground_floor_unsupported_area'],'existing_public_overlap':r['existing_public_polygon_overlaps'],'native_crossings':len(r['native_MainShell_interior_segments']),'generated_crossings':len(r['generated_public_interior_segments']),'boundary_distances':r['native_boundary_nearest_distances']}for r in rooms],'fixtures_candidates':[(x['recipe_id'],len(x['current_low_interior_geometry_candidates']))for x in fixture_checks]},ensure_ascii=False,indent=2))
