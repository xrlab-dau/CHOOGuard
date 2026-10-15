"""Bounded guide-first alternatives for4/8; native frontage and cores stay intact."""
from pathlib import Path
import json,struct
import numpy as np
from shapely.geometry import Polygon,LineString,Point
from shapely.ops import unary_union
ROOT=Path('/Users/um-yunsang/CHOOGuard');BASE=ROOT/'workers/twin/output/floor1-multiview-next-20261009';OUT=BASE/'remaining-guide-coverage'
guide=json.loads((BASE/'guide-semantics.json').read_text());spec=json.loads((ROOT/'.planning/2026-09-23-video-twin/1f-implementation/interior-spec.json').read_text());public=json.loads((ROOT/'.planning/2026-09-24-interior-twin/specs/full/public-1f.json').read_text());data=json.loads((ROOT/'.planning/2026-10-08-interior-runtime-completion/architecture/after-busanok-finishes/current-scene-structure.json').read_text());meta={m['id']:m for m in data['meshes']};sections=json.loads((ROOT/'.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/floor1-actual-segments.json').read_text());support=json.loads((OUT/'native-original-floor-support.json').read_text());c=.9602937;s=.278991133
def vertices(mid):
 raw=(ROOT/meta[mid]['binary']).read_bytes();nv,ni,nn=struct.unpack_from('<iii',raw);return np.frombuffer(raw,'<f4',nv*3,12).reshape(nv,3).astype(float),np.frombuffer(raw,'<i4',ni,12+nv*12).reshape(-1,3)
floorpieces=[]
for o in support['floor_owners']:
 r=next(r for r in data['renderers'] if r['global_object_id']==o['global_object_id']);v,t=vertices(o['mesh']);mat=np.array(r['world_matrix']);w=v@mat[:3,:3].T+mat[:3,3];uv=np.column_stack([s*w[:,0]+c*w[:,2],c*w[:,0]-s*w[:,2]])
 floorpieces.extend(Polygon(uv[t[i]]) for i in o['selected_floor_face_ordinals'])
floor=unary_union(floorpieces)
rooms=[(e,Polygon(e['geometry']['polygon'])) for e in public['elements'] if 'polygon' in e.get('geometry',{})]
clads=[(e,LineString(e['geometry']['polyline'])) for e in public['elements'] if 'polyline' in e.get('geometry',{})]
shell=[(o,LineString(seg)) for o in sections['owners'] if '/MainShell/' in o['path'] for seg in o['segments'] if LineString(seg).length>=1]
def qa(poly):
 return {'polygon_valid':poly.is_valid,'area':poly.area,'unsupported_combined_native_floor_area_at0_02':poly.difference(floor.buffer(.02)).area,
 'protected_core_overlaps':[{'id':e['id'],'overlap_area':poly.intersection(p).area} for e,p in rooms if poly.intersection(p).area>.001],
 'native_MainShell_crossings':[{'path':o['path'],'segment_UV':list(map(list,line.coords)),'length_inside':poly.buffer(-.01).intersection(line).length} for o,line in shell if poly.buffer(-.01).intersection(line).length>.01],
 'protected_BOH_cladding_crossings':[{'id':e['id'],'length_inside':poly.buffer(-.01).intersection(line).length} for e,line in clads if poly.buffer(-.01).intersection(line).length>.01]}
def actual(n):
 u=next(u for u in spec['units'] if u['boardNo']==n);r=next(r for r in data['renderers'] if f"Ground_Unit_{u['mid']}_Walls" in r['path']);return u,r

u4,r4=actual(4);current4=Polygon(u4['polygon']);g4=Polygon(next(g['scaffold_polygon_UV_approx'] for g in guide['units'] if g['boardNo']==4));p16=json.loads((OUT/'conservative-proposals16.json').read_text())['operations'][0]['proposed_polygon_UV'];current16=Polygon(p16)
# Preserve native frontage and side walls, reproduce source guide's aisle/depth proportion.
midU=(current4.bounds[0]+current4.bounds[2])/2
front16=np.array(p16[1:3]);frac=(midU-front16[0,0])/(front16[1,0]-front16[0,0]);v16=float(front16[0,1]+frac*(front16[1,1]-front16[0,1]))
plaza=np.array(u4['polygon'][5:7]);frac=(midU-plaza[0,0])/(plaza[1,0]-plaza[0,0]);vp=float(plaza[0,1]+frac*(plaza[1,1]-plaza[0,1]))
ratio=(301-265)/(371-301);targetV=(v16+ratio*vp)/(1+ratio)
def atV(a,b,y):return [float(a[0]+(b[0]-a[0])*(y-a[1])/(b[1]-a[1])),float(y)]
new4=[p[:] for p in u4['polygon']];new4[0]=atV(new4[0],new4[1],targetV);new4[7]=atV(new4[6],new4[7],targetV)
edgepart=(u4['polygon'][8][0]-u4['polygon'][0][0])/(u4['polygon'][7][0]-u4['polygon'][0][0]);new4[8]=[float(new4[0][0]+edgepart*(new4[7][0]-new4[0][0])),float(targetV)]
candidate4=Polygon(new4)

u8,r8=actual(8);current8=Polygon(u8['polygon']);g8=Polygon(next(g['scaffold_polygon_UV_approx'] for g in guide['units'] if g['boardNo']==8))
# Conservative enclosure from source MainShell_5 side/front segments; bridge doorway gaps only.
native_front=[[-91.05621615935996,-21.548141301613654],[-90.56001484145976,-28.413452279985805],[-90.39460663940812,-30.701996676166534],[-80.67647058109014,-34.238960727968376],[-79.00863817432217,-34.742759553375905],[-78.33249330549032,-34.94700129213846],[-72.96349435694295,-36.39236572145515],[-70.78688259684122,-36.978321733857534]]
enclosure=Polygon(native_front+[[-68,-38],[-68,-18],[-91.25,-18]])
protected=unary_union([p for e,p in rooms]).buffer(.15,join_style=2)
# Preserve the existing end-stair/lift public approach, not merely the room polygon.
native_core_approach=Polygon([u8['polygon'][0],u8['polygon'][1],[-94,u8['polygon'][1][1]],[-94,-18],[-82.8,-18]])
safe8=g8.intersection(enclosure.buffer(-.08,join_style=2)).difference(protected).difference(native_core_approach)
if safe8.geom_type=='MultiPolygon':safe8=max(safe8.geoms,key=lambda p:p.area)
safe8=safe8.simplify(.003,preserve_topology=True)

packet={'schema':'chooguard.remaining-guide-footprint-alternatives.v1','reference_only':True,'physical_accuracy_verified':False,
 'guide_source':guide['source_url'],'guide_image_sha256':guide['native_image_sha256'],
 'floor_support_reference':'native-original-floor-support.json; includes original ContextPlaza up to0.22 below1F plane; do not mistake this for a watertight inner enclosure.',
 'unit4':{'current_polygon_UV':u4['polygon'],'actual_wall_owner':r4,'raw_guide_scaffold_polygon_UV':list(map(list,g4.exterior.coords))[:-1],'raw_guide_qa':qa(g4),
 'frontage_anchored_guide_depth_candidate_polygon_UV':new4,'candidate_qa':qa(candidate4),
 'guide_proportions':{'guide_aisle_pixels':36,'guide_shop_depth_pixels':70,'ratio':ratio,'fixed_native_unit16_frontV_at_midU':v16,'fixed_native_plaza_edgeV_at_midU':vp,'target_aisle_frontV':targetV},
 'candidate_max_vertex_displacement_model_units':float(max(np.linalg.norm(np.array(a)-b) for a,b in zip(u4['polygon'],new4))),
 'candidate_outside_current_shop_area':candidate4.difference(current4).area,'freed_public_aisle_area':current4.difference(candidate4).area,
 'why_native_frontage_anchor':'Raw affine guide polygon moves the plaza-facing shop edge inward by about2–3 model units. Native front access/frames remain fixed, so retain current plaza edge and side-wall lines; source ratio widens the aisle without lateral movement.',
 'integration_note':'REVIEW alternative; preserve nine-vertex topology/frontage. Door/fascia/ceiling/interior component mappings must follow the moved aisle frontage. Newly freed39-ish area needs continuous public floor/collision finish at existing floorY; floor under it includes ContextPlaza below0.'},
 'unit8':{'current_polygon_UV':u8['polygon'],'actual_wall_owner':r8,'raw_guide_scaffold_polygon_UV':list(map(list,g8.exterior.coords))[:-1],'raw_guide_qa':qa(g8),
 'native_front_boundary_UV':native_front,'native_front_boundary_source':'MainShell_5 exact Y1.5 sections; doorway gaps bridged conservatively. Source side/front owners remain unchanged.',
 'preserved_native_end_core_approach_polygon_UV':list(map(list,native_core_approach.exterior.coords))[:-1],'protected_room_setback_model_units':.15,
 'constrained_guide_candidate_polygon_UV':list(map(list,safe8.exterior.coords))[:-1],'candidate_qa':qa(safe8),
 'candidate_hausdorff_displacement_model_units':current8.hausdorff_distance(safe8),'area_removed_from_raw_guide_by_native_enclosure_and_core_buffers':g8.area-safe8.area,
 'source_outline_mismatch':'Guide lower/front wing is broad and reaches the end facade; current footprint narrows to a short lower tip. Native-envelope clipping supplies a reviewable guide-first shape while protecting the end-stair core.',
 'integration_note':'REVIEW ONLY: changed concavity/vertex count requires unit wall/floor/ceiling rebuild and door/aperture remapping; do not feed blindly into equal-vertex per-unit mapper. Preserve all native outer walls and Room faces. Runtime/floor finish checks required.'},
 'photo_crosscheck':{'source01':'https://m.blog.naver.com/leedh960830/224021169842','source02':'https://www.youtube.com/watch?v=1cYjA4u14PE','classification':'insufficient','reason':'Retained sources have no newly registered architectural camera/depth proof for ARTBOX or Yangsan outer boundaries; capture epoch/state differs. They do not establish an exact contradiction to the guide. No contradictory photo claim is made.'}}
(OUT/'footprint-alternatives4-8.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'unit4_candidate':new4,'unit4_qa':packet['unit4']['candidate_qa'],'unit8_candidate_vertex_count':len(packet['unit8']['constrained_guide_candidate_polygon_UV']),'unit8_qa':packet['unit8']['candidate_qa']},ensure_ascii=False))
