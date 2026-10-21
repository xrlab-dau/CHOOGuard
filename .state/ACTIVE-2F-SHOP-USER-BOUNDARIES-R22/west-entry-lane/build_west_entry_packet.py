from pathlib import Path
from datetime import datetime,timezone
import json,math
import numpy as np
from shapely.geometry import Polygon,LineString,shape,mapping,box
from shapely.ops import unary_union
P=Path('.state/ACTIVE-2F-SHOP-USER-BOUNDARIES-R22/west-entry-lane')
G=json.loads(Path('.state/ACTIVE-2F-SHOP-LOCATION-REVIEW-R20/geometry-lane/STRUCTURAL-ANCHORS-AND-CELLS-R20.json').read_text())
D=json.loads(Path('.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/cad/r19/CURRENT-LIVE-2F-OBJECT-VECTOR-CAD-R19-NO-TEXT.json').read_text())
N=next(o for o in D['objects'] if o['object_id'].endswith('-1413895473-0'))
C=next(o for o in G['generated_2F_columns_UNCONFIRMED'] if o['object_id'].endswith('-27936212-0'))
F=shape(G['floor']['geometry']);Fo=unary_union([Polygon(p.exterior) for p in F.geoms])
# Stable actual native return face above shops; derive one rigid line, not annotation x.
a=np.array([-61.0186,48.1322]);b=np.array([-60.9968,52.5747]);t=(b-a)/np.linalg.norm(b-a);normal=np.array([t[1],-t[0]])
# A2D line d is shifted30mm toward facade, keeping all new fronts behind existing return.
front_d=float(normal@a)-.03
fx=lambda y:(front_d-normal[1]*y)/normal[0]
# Partition cap: normal-to-front through actual native cladding joint at10.5, close to red locator142.
clad=np.array([-64.40723035904642,40.83953224743903]);vdiv=float(t@clad)
# Lower divider: native vertical/diagonal facade vertex at8.4.
bend=np.array([-64.3396,34.225]);vbend=float(t@bend)
# Lower stop: native next diagonal frame point near red locator143 bottom.
stop=np.array([-62.4435,30.9]);vstop=float(t@stop)
# Stop before the fixed north support south face; do not create a new column or consume return access.
cs=shape(C['geometry']);north_y=cs.bounds[1]-.04;vnorth=float(t@np.array(C['centroid_qx_minus_qy'][:1]+[north_y]))
def y_at(v,x):return (v-t[0]*x)/t[1]
def panel(v0,v1):
 y0=y_at(v0,-61.1);y1=y_at(v1,-61.1)
 # Rear extends only as a construction ray; actual Main outer facade intersects it precisely.
 poly=Polygon([[-66,y_at(v0,-66)],[fx(y0),y0],[fx(y1),y1],[-66,y_at(v1,-66)]])
 return poly.intersection(Fo)
def front(v0,v1):
 # Solve intersection of t.q=v, n.q=front_d.
 A=np.vstack([t,normal]);return [np.linalg.solve(A,np.array([v0,front_d])).tolist(),np.linalg.solve(A,np.array([v1,front_d])).tolist()]
items=[]
for brand,v0,v1,ann in [('새부산약국',vdiv,vnorth,[142,148]),('아메리칸트레일러',vbend,vdiv,[148]),('기장특산품',vstop,vbend,[143])]:
 g=panel(v0,v1)
 items.append({'brand':brand,'status':'REVIEW_ONLY_BOUNDED_MODEL','geometry':mapping(g),'area_MODEL_m2':g.area,'public_frontage_qx_minus_qy_m':front(v0,v1),'public_normal_qx_minus_qy':normal.tolist(),'source_user_annotation_xrefs':ann,'boundary_method':['Front is actual native return face extended south with30mm inward clearance.','Rear follows actual R19 Main exterior through native vertical-to-diagonal facade bend, rather than red annotation west edge.','Tenant divider is perpendicular to the existing return face through native cladding joint/bend; tenant ownership of joint is MODEL.','Fixed support066 south face limits northern work; actual doorway/return north of it stays open.'],'current_column_anchor_ids':[C['object_id']],'existing_native_face_owner':N['object_id'],'Scene_Assets_changed':False})
access=Polygon([[-71.90,48.10],[fx(48.10)+2,48.10],[fx(52.88)+2,52.88],[-71.90,52.88],[-71.90,48.10]])
# This is an exclusion / access preservation region, not new floor or walls.
rec={'schema':'chooguard.r22.west-entry-shop-return-model.v1','utc':datetime.now(timezone.utc).isoformat(),'coordinate_system':'actualR19 [qx,-qy] metres','status':'REVIEW_ONLY_NOT_SCENE_CONSTRUCTION','Scene_Assets_changed':False,'currentCAD_changed':False,'user_markings_are_locators_not_footprint_dimensions':True,'actual_scene_source':'.state/ACTIVE-2F-SHOP-USER-BOUNDARIES-R22/scene-read/ACTUAL-SCENE-AND-VIEW-R22.json','scene_source_utc':'2026-10-19T05:59:36.5227790Z','scene_dirty':False,'base_actual_CAD':'.state/CURRENT-2F-VECTOR-CAD-R19.json','proposals':items,'do_not_place_shop_or_wall':{'id':'EXISTING_WEST_NATIVE_ACCESS_RETURN','geometry':mapping(access),'actual_native_access_limits_qy':[48.10,52.88],'basis':'Actual current native return/sill and same-level open recess; preserves approach from hall and west landing. Expanded eastward2m only as no-shop approach reservation, not as measured opening width.','subtract_from_work':True,'native_owner':N['object_id'],'native_door_leaf_identity':'Not separately named in the combined native mesh; frame/return geometry and open access are preserved without fabricating a new door.'},'source_segments':[{'id':'WEST_RETURN_FRONT_EXTENSION','owner':N['object_id'],'actual_worldY_section':8.4,'a':a.tolist(),'b':b.tolist(),'operation':'extend south in same plane; no object mutation','front_inset_m':.03},{'id':'NORTH_ACCESS_SILL','owner':N['object_id'],'actual_worldY_section':8.4,'a':[-63.8423,48.1142],'b':[-61.0499,48.1005],'operation':'preserve opening north side'},{'id':'PHARMACY_AMERICAN_MODEL_DIVIDER_REFERENCE','owner':N['object_id'],'actual_worldY_section':10.5,'point':clad.tolist(),'operation':'perpendicular-to-existing-front through actual native cladding joint; dashed tenant division only'},{'id':'AMERICAN_KIJANG_FACADE_BEND','owner':N['object_id'],'actual_worldY_section':8.4,'point':bend.tolist(),'operation':'perpendicular divider at actual vertical/diagonal facade vertex'},{'id':'KIJANG_SOUTH_STOP','owner':N['object_id'],'actual_worldY_section':8.4,'point':stop.tolist(),'operation':'next actual diagonal frame vertex; do not extend rectangular marked box beyond facade'},{'id':'FIXED_COLUMN066_SOUTH_FACE','owner':C['object_id'],'profile_bounds':list(cs.bounds),'operation':'north work limit40mm south of actual profile'}],'photographic_relationship':{'images_directly_viewed':['.state/ACTIVE-2F-SHOP-USER-BOUNDARIES-R22/inputs/USER-MARKED-R20-PAGE1.png','.state/ACTIVE-2F-SHOP-STRUCTURE-REVIEW-R21/northwest-lane/naver-20240515-source03-038-800.jpg','.state/ACTIVE-2F-SHOP-STRUCTURE-REVIEW-R21/northwest-lane/naver-20240515-source03-039-800.jpg','.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/research/root/american-trailer-20261006-front-original.jpg'],'URLs':['https://m.blog.naver.com/oyj10244/223446992106','https://m.blog.naver.com/blare_95/224433054269'],'dates':['2024-05-15','2026-10-06'],'observed':'Kijang glazed dry-goods shop / American open service counter / pharmacy glass frontage share one high advertising/fluted architectural band. Platform entrance is at one end. These are shallow facade shops, not freestanding regular-depth hall boxes.','MODEL':'Exact tenant split and numeric depths are review inference from current frame/bend/supports; no photo pixel dimension is treated as a metric measurement.'},'transform':{'region':[-83,-49,96,98],'page_points':[1190.55,841.89],'scale':5.4550340136054425,'x_center_margin_points':107.04945578231286,'pdf_topY_inverted':True},'notes':['R20 proposed eastward excess toqx-57.1 is withdrawn; new front follows native return aroundqx-61.03..-61.13.','Do not count the platform ceiling mesh as an existing2Ffloor: only actual matching-level frame/face segments are used as fixed structural lines.','Existing native cross traces aroundqY38.99 are preserved gray; their projection alone does not prove another door aperture and does not authorize a cut.','No old C21 booth/geometry is restored. User review determines tenant boundary acceptance.']}
(P/'WEST-ENTRY-SHOPS-RETURN-MODEL-R22.json').write_text(json.dumps(rec,ensure_ascii=False,indent=2)+'\n')
(P/'WEST-ENTRY-SHOPS-RETURN-MODEL-R22.geojson').write_text(json.dumps({'type':'FeatureCollection','features':[{'type':'Feature','properties':{'brand':x['brand'],'status':x['status']},'geometry':x['geometry']} for x in items]},ensure_ascii=False,indent=2)+'\n')
print(json.dumps([{'brand':x['brand'],'area':x['area_MODEL_m2'],'front':x['public_frontage_qx_minus_qy_m']} for x in items],ensure_ascii=False))
