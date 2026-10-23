from pathlib import Path
import json,datetime,math
import numpy as np
from shapely.geometry import Polygon,mapping
P=Path('.state/ACTIVE-2F-BLUE-ONLY-VECTOR-CONSTRUCTION-R24/west-blue-lane')
# The latest annotation appearance is a four-edge trapezoid, not a rectangle or L.
# Actual independent R19 references, no previous tenant region loaded.
D=json.loads(Path('.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/cad/r19/CURRENT-LIVE-2F-OBJECT-VECTOR-CAD-R19-NO-TEXT.json').read_text())
O=next(o for o in D['objects'] if o['object_id'].endswith('-1413895473-0'))
segs=O['sections']['8.40000']
def closest(target):
 return min((np.array(p) for s in segs for p in s),key=lambda p:np.linalg.norm(p-np.array(target)))
# The native vertical-to-diagonal corner and next actual diagonal frame vertex.
A=closest([-64.3396,34.225]);B=closest([-62.4435,30.9])
# Independently trace current north entry-return face and extend to this locality.
R0=closest([-61.01859347064644,48.13220250844756]);R1=closest([-60.996829352749,52.574653116984834]);T=(R1-R0)/np.linalg.norm(R1-R0);N=np.array([T[1],-T[0]])
# Top/bottom are normals to actual return, through actual native vertices A/B.
# Intersection of return plane N.q=N.R0 and cap plane T.q=T.A/B.
M=np.vstack([N,T]);AR=np.linalg.solve(M,np.array([N@R0,T@A]));BR=np.linalg.solve(M,np.array([N@R0,T@B]))
points=[A,AR,BR,B];poly=Polygon(points)
roles=[(105,'TOP_NORMAL_FROM_NATIVE_BEND',A,AR),(108,'RIGHT_RETURN_FACE_EXTENSION',AR,BR),(107,'BOTTOM_NORMAL_FROM_NATIVE_FRAME_VERTEX',BR,B),(106,'ACTUAL_NATIVE_DIAGONAL',B,A)]
lines=[]
for xref,role,p0,p1 in roles:
 lines.append({'source_blue_xref':xref,'role':role,'geometry':{'type':'LineString','coordinates':[p0.tolist(),p1.tolist()]},'length_MODEL_m':float(np.linalg.norm(p1-p0))})
rec={'schema':'chooguard.r24.west.blue_only.precise_native_vector.v1','utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'status':'REVIEW_VECTOR_ONLY_NO_SCENE_MUTATION','only_blue_xrefs':[105,106,107,108],'coordinate_system':'actualR19 CAD [qx,-qy] metres','appearance_interpretation':{'source':'.state/ACTIVE-2F-BLUE-ONLY-VECTOR-CONSTRUCTION-R24/inputs/USER-MARKED-R23-LATEST.png','crop':str(P/'WEST-BLUE-APPEARANCE-CROP.png'),'image_viewed':True,'shape':'Four edges: full upper cap, right return, short lower-right cap, diagonal lower-left cutoff. No new left vertical edge.','xref_roles':'105 top;106 diagonal;107 bottom;108 right, classified by actual annotation appearance/rect location. Raw Line.vertices for106/107/108 are stale or rotated and do not define final geometry.','handdrawn_shape_not_metric_authority':True},'geometry':mapping(poly),'segments':lines,'corners':{'top_left_native_bend':A.tolist(),'top_right_intersection':AR.tolist(),'bottom_right_intersection':BR.tolist(),'bottom_left_native_diagonal_vertex':B.tolist()},'MODEL_area_m2':poly.area,'anchors':[{'id':'NATIVE_VERTICAL_DIAGONAL_CORNER','object_id':O['object_id'],'object_path':O['path'],'worldY_section':8.4,'point':A.tolist(),'selection':'Nearest actual native vertex to blue upper-left neighborhood, not annotation endpoint.'},{'id':'NATIVE_NEXT_DIAGONAL_FRAME_VERTEX','object_id':O['object_id'],'worldY_section':8.4,'point':B.tolist()},{'id':'ACTUAL_NORTH_ENTRY_RETURN','object_id':O['object_id'],'worldY_section':8.4,'segment':[R0.tolist(),R1.tolist()],'operation':'Rigid southward line extension; native geometry unchanged.'},{'id':'NATIVE_LAYER0_5_COMPACT_SUPPORT','object_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-829381564-0','classification':'Nearby compact support at about[-63.885,30.954], remains outside/west of diagonal cutoff; not used as a newly invented shop corner.'}],'formulas':{'return_tangent_T':T.tolist(),'return_normal_N':N.tolist(),'return_equation':'dot(N,q) = dot(N,R0)','return_d':float(N@R0),'top_equation':'dot(T,q) = dot(T,A)','top_d':float(T@A),'bottom_equation':'dot(T,q) = dot(T,B)','bottom_d':float(T@B),'diagonal_equation':'q=A+u*(B-A), 0<=u<=1','top_right':'solve([N;T]*q=[N.R0,T.A])','bottom_right':'solve([N;T]*q=[N.R0,T.B])'},'existing_red_green_handling':{'unchanged':True,'delete_or_move_nonblue_annotations':False,'note':'Actual native bend nearqY34.225 replaces blue approximateqY33.96 only; the original red annotation is retained verbatim, not moved to hide this~0.26m source-locator difference.'},'protection':['No previous R22 tenant polygon was loaded or restored.','No native mesh/TRS/material/column/floor changed.','Native return/accessqY48.1..52.88 stays unaffected.','Platform ceiling owner is used only for its actual matching-level fixed facade/frame edges, not as2Ffloor occupancy authority.'],'Scene_Assets_changed':False,'baseCAD_changed':False}
(P/'WEST-BLUE-PRECISE-VECTORS-R24.json').write_text(json.dumps(rec,ensure_ascii=False,indent=2)+'\n')
(P/'WEST-BLUE-PRECISE-VECTORS-R24.geojson').write_text(json.dumps({'type':'FeatureCollection','features':[{'type':'Feature','properties':{'source_blue_xref':x['source_blue_xref'],'role':x['role']},'geometry':x['geometry']} for x in lines]},ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'corners':rec['corners'],'area':poly.area,'segments':lines},ensure_ascii=False))
