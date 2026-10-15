"""Bounded read-only handoff: released floor strip, guide figures and decisions."""
from pathlib import Path
import json, hashlib
import numpy as np
from shapely.geometry import Polygon, LineString, Point, mapping
from shapely.ops import unary_union
from PIL import Image, ImageDraw, ImageFont

ROOT=Path('/Users/um-yunsang/CHOOGuard')
BASE=ROOT/'workers/twin/output/floor1-multiview-next-20261009'
OUT=BASE/'remaining-guide-coverage'
EVIDENCE=ROOT/'workers/twin/evidence/floor1-multiview-next-20261009'
ns={}
exec((EVIDENCE/'remaining_guide_audit.py').read_text().split('# Test real collider')[0],ns)
ns['patches']={k:v for k,v in ns['patches'].items() if v['vertex_indices']}
data,binary,world,worldUV,owner=[ns[x] for x in ['data','binary','world','worldUV','owner']]
p4=json.loads((OUT/'conservative-proposals4.json').read_text())
p16=json.loads((OUT/'conservative-proposals16.json').read_text())
alt=json.loads((OUT/'footprint-alternatives4-8.json').read_text())
op=p4['operations'][0]
current=Polygon(op['current_polygon_UV']); proposed=Polygon(op['proposed_polygon_UV'])
# Remove the zero-area floating-point tail along the unchanged side line.
released=Polygon([op['current_polygon_UV'][i] for i in [0,8,7]]+
                 [op['proposed_polygon_UV'][i] for i in [7,8,0]])
assert released.symmetric_difference(current.difference(proposed)).area<1e-8
floorrows=[]; flushpolys=[]; belowpolys=[]; finishpolys=[]; unavailable=[]
def uv_box(r):
 lo,hi=np.array(r['world_bounds_min']),np.array(r['world_bounds_max'])
 corners=np.array([[lo[0],0,lo[2]],[hi[0],0,lo[2]],[hi[0],0,hi[2]],[lo[0],0,hi[2]]])
 return Polygon(worldUV(corners))
for r in data['renderers']:
 if not r.get('active',True) or not r.get('renderer_enabled',True) or not r.get('mesh'):continue
 if r['world_bounds_min'][1]>.12 or r['world_bounds_max'][1]<-.25:continue
 if not uv_box(r).intersects(released):continue
 if not ns['meshmeta'][r['mesh']].get('binary'):
  unavailable.append({'owner':owner(r),'mesh_metadata':ns['meshmeta'][r['mesh']],
   'bounds_world':[r['world_bounds_min'],r['world_bounds_max']],
   'reason':'No exported readable mesh binary; bounds alone do not establish floor coverage.'})
  continue
 v,t=binary(r['mesh']); w=world(r,v); uv=worldUV(w); tr=w[t]
 mask=(tr[:,:,1].max(1)-tr[:,:,1].min(1)<.002)&(tr[:,:,1].mean(1)>=-.25)&(tr[:,:,1].mean(1)<=.12)
 groups={}
 for f in np.where(mask)[0]:
  tri=Polygon(uv[t[f]])
  if not tri.is_valid or tri.area<1e-8:continue
  clip=tri.intersection(released)
  if clip.area<1e-8:continue
  h=float(tr[f,:,1].mean()); key=round(h,4)
  g=groups.setdefault(key,{'height':h,'faces':[],'polys':[]});g['faces'].append(int(f));g['polys'].append(clip)
 for g in groups.values():
  u=unary_union(g['polys']);h=g['height']
  cols=[owner(c) for c in data['colliders'] if c['path']==r['path']]
  role='floor' if 'floor' in r['path'].lower() else 'other_horizontal_surface'
  floorrows.append({'renderer':owner(r),'same_path_colliders':cols,'surface_role':role,'world_y_mean':h,'world_y_round4':round(h,4),
   'face_ordinals':g['faces'],'released_strip_covered_area':u.area,'covered_geometry_UV':mapping(u),
   'height_class':'flush_0' if abs(h)<=.02 else ('below_0' if h<-.02 else 'finish_above_0')})
  if role=='floor':(flushpolys if abs(h)<=.02 else belowpolys if h<-.02 else finishpolys).append(u)
flush=unary_union(flushpolys);below=unary_union(belowpolys);finish=unary_union(finishpolys)
floorpacket={'schema':'chooguard.unit4-released-public-strip.v1','frame':op['frame'],
 'guide_image_sha256':p4['guide_image_sha256'],'snapshot_scene_sha256':data['scene_file_sha256'],
 'production_receipt_scene_sha256':ns['receipt']['scene_sha256'],'source_geometry_updated_with_installed_rear_row':True,
 'released_strip_geometry_UV':mapping(released),'released_strip_area':released.area,
 'horizontal_surface_height_coverage':floorrows,
 'floor_owner_height_coverage':[x for x in floorrows if x['surface_role']=='floor'],'flush_world_y0_tolerance':.02,
 'overlapping_unavailable_mesh_candidates':unavailable,
 'flush_y0_covered_area':flush.area,'flush_y0_uncovered_area':released.difference(flush).area,
 'below0_covered_area':below.area,'finish_above0_covered_area':finish.area,
 'flush_y0_uncovered_geometry_UV':mapping(released.difference(flush)),
 'integration_requirement':'Preserve or replace current authored shop floor/collision support in the released strip. ContextPlaza atY−0.2 only provides lower support; do not count it as a flush public floor. These are rendered-face coverage results; same-path collider presence alone does not prove a runtime floor hit.',
 'runtime_floor_hit_verified':False}
collisionrows=[]
for row in floorpacket['floor_owner_height_coverage']:
 if row['height_class']!='flush_0':continue
 for co in row['same_path_colliders']:
  col=next(x for x in data['colliders'] if x['global_object_id']==co['global_object_id'])
  if not col.get('active',True) or not col.get('enabled',True) or col.get('trigger'):continue
  if col.get('mesh')!=row['renderer']['snapshot_mesh']:continue
  v,t=binary(col['mesh']);w=world(col,v);uv=worldUV(w)
  parts=[Polygon(uv[t[f]]).intersection(released) for f in row['face_ordinals']]
  u=unary_union(parts)
  collisionrows.append({'collider':co,'face_ordinals':row['face_ordinals'],
    'world_y_min_max':[float(w[t[row['face_ordinals']],1].min()),float(w[t[row['face_ordinals']],1].max())],
    'released_strip_covered_area':u.area,'released_strip_uncovered_area':released.difference(u).area})
floorpacket['offline_flush_floor_collision_coverage']=collisionrows
(OUT/'unit4-released-strip-floor.json').write_text(json.dumps(floorpacket,ensure_ascii=False,indent=2)+'\n')
p4['released_strip_packet']='unit4-released-strip-floor.json'
p4['released_strip_polygon_UV']=list(map(list,released.exterior.coords))[:-1]
p4['offline_qa']['released_strip_native_flushY0_floor_covered_area']=flush.area
p4['offline_qa']['released_strip_native_flushY0_floor_uncovered_area']=released.difference(flush).area
p4['integration_required']='Preserve native Ground_Floor renderer972814338/collider972814336, top face72 atY0; it supports the complete released39.0123-area strip. Retain or retile current shop granite faces7,8,11,12,13 atY0.004 as public floor. Remap moved aisle-facing storefront/doors/buttons/ceiling/interior fixtures through current production batches; keep native outside entrance frames and all cores fixed. No new floor extension needed.'
(OUT/'conservative-proposals4.json').write_text(json.dumps(p4,ensure_ascii=False,indent=2)+'\n')

# A native-entry-anchored aperture candidate for the constrained Unit8 frontage.
# Its guide position is not claimed to be a measured/authenticated doorway.
u8=alt['unit8']; q=np.array(u8['current_polygon_UV']); new=np.array(u8['constrained_guide_candidate_polygon_UV'])
sourceentry=(q[5]+q[6])/2; a,b=new[0],new[1];axis=(b-a)/np.linalg.norm(b-a)
fraction=float(np.clip(np.dot(sourceentry-a,b-a)/np.dot(b-a,b-a),0,1));centre=a+fraction*(b-a)
ends=[(centre-axis*.9).tolist(),(centre+axis*.9).tolist()]
unit8packet={'schema':'chooguard.unit8-constrained-guide-review.v1','status':'REVIEW_ONLY_REBUILD_AND_APERTURE_BINDING_REQUIRED',
 'frame':'station UV; native localXYZ=(V,Y,U), existing16.2degree root preserved','reference_only':True,
 'physical_accuracy_verified':False,'guide_source':p4['guide_source'],'guide_image_sha256':p4['guide_image_sha256'],
 'actual_wall_owner':u8['actual_wall_owner'],'current_polygon_UV':q.tolist(),'proposed_polygon_UV':new.tolist(),
 'current_vertex_count':len(q),'proposed_vertex_count':len(new),'offline_containment_qa':u8['candidate_qa'],
 'native_enclosure_boundary':u8['native_front_boundary_UV'],'preserved_native_core_approach':u8['preserved_native_end_core_approach_polygon_UV'],
 'max_hausdorff_displacement_model_units':u8['candidate_hausdorff_displacement_model_units'],
 'proposed_front_edge_indices':[0],
 'aperture_candidate':{'method':'Project existing authored doorEdge5 midpoint onto candidate hall-facing edge0; preserve the authored1.8-width estimate. No guide doorway metric claim.',
  'current_door_edge_indices':[5,6],'current_authored_midpoint_UV':sourceentry.tolist(),'proposed_edge_indices':[0,1],
  'proposed_midpoint_UV':centre.tolist(),'proposed_endpoints_UV':ends,'proposed_width_model_units':1.8,
  'centre_displacement_model_units':float(np.linalg.norm(sourceentry-centre)),
  'distance_to_candidate_front_endpoints_model_units':[float(np.linalg.norm(centre-a)),float(np.linalg.norm(centre-b))],
  'source_aperture_position_verified':False,'production_door_leaf_owner_binding_verified':False},
 'unsatisfied_integration_blockers':[
  '7→10 polygon vertices change concavity and wall ownership; equal-vertex deformation is inapplicable. Rebuild dedicated walls/floor/ceiling and match storefront gaps to the new front edge.',
  'Exact actual production door/glass/fascia component ownership and aperture binding have not been established for this10-point candidate. The projected aperture is a local review target, not an approved authored-door transaction.',
  'Complete current unit8 floor coverage at flushY0 has not been checked for the added footprint; the combined native support result includes lower ContextPlaza.',
  'No runtime approach/door/floor walk has verified the candidate. Preserve the end-stair/lift approach and all source MainShell/Room owners.'
 ],'apply_to_production':False}
(OUT/'constrained-proposals8-review.json').write_text(json.dumps(unit8packet,ensure_ascii=False,indent=2)+'\n')

# Keep coverage statuses tied to the production rear-row receipt, never claim staged patches installed.
d=json.loads((OUT/'coverage.json').read_text())
for f in d['features']:
 if f['feature']=='unit-4':
  f.update(status='KNOWN_MISMATCH',match_scope='Normalized guide aisle/store depth relation; bounded conservative correction proposed, not installed.',
   layout_status='KNOWN_MISMATCH',proposal_packet='conservative-proposals4.json',floor_packet='unit4-released-strip-floor.json',
   proposed_polygon_UV=op['proposed_polygon_UV'],note='Guide aisle/depth ratio0.5143 versus current0.1255 after corrected16. Preserve native plaza frontage and side-wall lines; move aisle frontage inward. Metric dimensions remain unverified.')
 elif f['feature']=='unit-16':
  f.update(status='KNOWN_MISMATCH',match_scope='Shared partition endpoints with already-corrected15; minimal1.17V restoration proposed, not asserted installed.',
   layout_status='KNOWN_MISMATCH',proposal_packet='conservative-proposals16.json',proposed_polygon_UV=p16['operations'][0]['proposed_polygon_UV'],
   note='Current16 shared edge retains1.17V shift relative to corrected15; proposal endpoints coincide exactly and preserve current7-point clipped shape.')
 elif f['feature']=='unit-8':
  f.update(projection_status='REJECTED_RAW_GUIDE_PROJECTION',review_packet='constrained-proposals8-review.json',
   note='Raw scaffold crosses7 native long wall segments and overlaps protected end-stair core2.9869 square model units. The constrained10-point candidate passes geometric containment but requires rebuild/aperture ownership/flush floor/runtime checks; no production operation supplied.')
 elif f['feature'].startswith('unit-') and f['feature']!='unit-1':
  f['match_scope']='Shop presence and order; exact dimensions UNKNOWN.'
  f['note']='Gross source-guide ordering agrees. Schematic affine residuals do not certify exact wall positions; preserve current native fronts and cores.'
d['new_bounded_architecture_repairs']=[{'feature':'unit-4','packet':'conservative-proposals4.json','status':'ROOT_STAGING_CANDIDATE'},
 {'feature':'unit-16','packet':'conservative-proposals16.json','status':'ROOT_STAGED_PENDING_OWNERSHIP_REVIEW_AT_HANDOFF'}]
d['conclusion']='Two bounded proposals resolve remaining guide-layout issues: Unit4 aisle depth while fixing native fronts, and Unit16 shared seam with corrected15. Unit8 raw projection is rejected; constrained rebuild is review-only with explicit blockers. Other feature existence/order and gross core placement agree. P1/P3 operability, all static passage probes, exact core doors/gender/details and dimensions remain UNKNOWN; no further exact phantom-wall deletion is supported.'
(OUT/'coverage.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')

# Technical QA figures: unmodified photographed source shown next to native/proposed geometry.
font='/Users/um-yunsang/Library/Fonts/NotoSansCJKkr-Regular.otf'
F=lambda n:ImageFont.truetype(font,n)
im=Image.new('RGB',(1700,1170),'#f8f9fc'); dr=ImageDraw.Draw(im)
dr.text((40,24),'1F GUIDE · UNIT4 CONSERVATIVE AISLE CORRECTION',font=F(28),fill='#17233a')
dr.text((40,65),'Source sign proportions + fixed native plaza frontage / reference correction, no survey accuracy claim',font=F(18),fill='#46546d')
source=Image.open(ROOT/p4['guide_native_image']);crop=(390,185,540,390); scale=2.55
panel=source.crop(crop).resize((int((crop[2]-crop[0])*scale),int((crop[3]-crop[1])*scale)))
im.paste(panel,(45,140));dr.text((45,110),'Photographed 1F guide (native bytes preserved)',font=F(18),fill='#17233a')
def sourcept(x,y):return (45+(x-crop[0])*scale,140+(y-crop[1])*scale)
for y0,y1,label,col in [(265,301,'aisle 36px','#cb5325'),(301,371,'store 70px','#227552')]:
 x=530;dr.line([sourcept(x,y0),sourcept(x,y1)],fill=col,width=5)
 for y in [y0,y1]:dr.line([sourcept(x-3,y),sourcept(x+3,y)],fill=col,width=3)
 dr.text((435,sourcept(x,(y0+y1)/2)[1]-8),label,font=F(18),fill=col)
dr.text((45,690),'Guide ratio: 36 / 70 = 0.5143',font=F(22),fill='#17233a')
dr.text((45,730),'#16 front / #4 aisle front / #4 plaza edge',font=F(18),fill='#46546d')
dr.text((45,765),'Ratio constrains depth; native side walls fix lateral placement.',font=F(16),fill='#46546d')

dr.text((680,110),'Native station UV · unchanged exterior front / proposed aisle front',font=F(18),fill='#17233a')
box=(710,150,1600,870); umin,umax=-14,24;vmin,vmax=-44,-4
def pt(p):return (box[0]+(umax-p[0])/(umax-umin)*(box[2]-box[0]),box[1]+(vmax-p[1])/(vmax-vmin)*(box[3]-box[1]))
for v in [-10,-20,-30,-40]:
 yy=pt([0,v])[1];dr.line([(box[0],yy),(box[2],yy)],fill='#dbe0eb');dr.text((box[2]+8,yy-9),f'V {v}',font=F(15),fill='#6c7890')
for f in d['features']:
 if f['feature'] in ['unit-3','unit-5']:
  pts=[pt(x) for x in f['current_polygon_UV']];dr.polygon(pts,fill='#e4e8ef');dr.line(pts+[pts[0]],fill='#8d98ab',width=2)
for poly,fill,col in [(current,'#efdddd','#b95656'),(proposed,'#d4efdf','#227552')]:
 pts=[pt(x) for x in poly.exterior.coords];dr.polygon(pts,fill=fill);dr.line(pts,fill=col,width=4)
rpts=[pt(x) for x in released.exterior.coords];dr.line(rpts,fill='#c54f31',width=5)
new16=p16['operations'][0]['proposed_polygon_UV'];dr.polygon([pt(x) for x in new16],fill='#d4e4fa');dr.line([pt(x) for x in new16+[new16[0]]],fill='#3066a4',width=4)
dr.text(pt([4,-12]),'#16 corrected seam target',font=F(17),fill='#3066a4')
dr.text(pt([5.2,-30]),'#4 candidate',font=F(20),fill='#227552')
dr.text(pt([5.1,-24]),'released public strip',font=F(16),fill='#b8442c')
front=[pt(op['current_polygon_UV'][i]) for i in range(1,7)];dr.line(front,fill='#17233a',width=6)
dr.text((900,890),'Black: native plaza frontage fixed (vertices1–6)',font=F(18),fill='#17233a')
dr.text((45,930),'Current gap/depth after16: 2.448 / 19.509 = 0.1255 → target ratio0.5143',font=F(21),fill='#17233a')
dr.text((45,970),'Moved aisle front V−22.591 → V−27.5998; max displacement5.0247; released area39.0123',font=F(21),fill='#17233a')
dr.text((45,1010),f'FlushY0 rendered-face coverage: {flush.area:.4f} / {released.area:.4f}; uncovered {released.difference(flush).area:.4f}',font=F(20),fill='#b8442c')
dr.text((45,1050),'Zero added shop area, protected-core overlap or native long-wall crossings. Floor/collision support must remain.',font=F(17),fill='#46546d')
dr.text((45,1105),'Source: yoonsscom/224376213780 · photo claim2026-08-11 · printed revision unknown · SHAec09e4e3…43d7cf',font=F(15),fill='#6c7890')
im.save(OUT/'unit4-guide-native-comparison.png')

# Full semantic coverage overlay uses source pixels only, no metric scaling claim.
im=source.resize((1932,1450));dr=ImageDraw.Draw(im)
colors={'MATCH':'#236847','KNOWN_MISMATCH':'#b93332','UNKNOWN':'#98641f'}
guide=json.loads((BASE/'guide-semantics.json').read_text())
for u in guide['units']:
 if u['boardNo'] not in list(range(1,9))+[16]:continue
 f=next(x for x in d['features'] if x['feature']==f"unit-{u['boardNo']}");ps=[(2*x,2*y) for x,y in u['source_polygon_pixels_approx']]
 dr.line(ps+[ps[0]],fill=colors[f['status']],width=5)
 cx=sum(x for x,y in ps)/len(ps);cy=sum(y for x,y in ps)/len(ps)
 dr.text((cx-10,cy-15),str(u['boardNo']),font=F(26),fill=colors[f['status']],stroke_width=2,stroke_fill='white')
for gap in guide['front_access_gaps']:
 if gap['id'] not in ['P1','P2','P3','P4','P5','P6']:continue
 x0,y0,x1,y1=gap['bbox'];dr.rectangle((2*x0,2*y0,2*x1,2*y1),outline='#98641f',width=4);dr.text((2*x0,2*y1+3),gap['id'],font=F(20),fill='#98641f',stroke_width=2,stroke_fill='white')
dr.rectangle((25,920,1907,1405),fill='#f8f9fc')
lines=['Remaining 1F guide coverage (production rear-row receipt; Unit4/16 proposals separate)',
 'GREEN: presence/order matches. RED4: aisle depth proposal. RED16: shared seam proposal. AMBER8: constrained rebuild review only.',
 'P1/P3 native thin leaves: exact owner IDs + runtime probes in coverage.json. Closed-pose static hits do not prove a phantom wall.',
 'P2/P4 contain printed lift/stair symbols: preserve cores. P5/P6 original MainShell frontage hits need runtime path checks.',
 'Both WC clusters, front stair/lift clusters, both rear escalators and locker17 present; exact doors/gender/stair glyphs remain unknown.',
 'Photographed guide is schematic. No surveyed dimensions or2cm physical accuracy claim. No source Room-face deletion approved.']
for i,line in enumerate(lines):dr.text((50,950+i*62),line,font=F(22 if i==0 else 20),fill='#17233a')
im.save(OUT/'remaining-guide-coverage-annotated.png')
print(json.dumps({'released_area':released.area,'flush_y0_covered_area':flush.area,'flush_y0_uncovered_area':released.difference(flush).area,
 'floor_owners':[(x['renderer']['path'],x['world_y_round4'],x['released_strip_covered_area']) for x in floorrows],
 'unit8_aperture_target':unit8packet['aperture_candidate']},ensure_ascii=False))
