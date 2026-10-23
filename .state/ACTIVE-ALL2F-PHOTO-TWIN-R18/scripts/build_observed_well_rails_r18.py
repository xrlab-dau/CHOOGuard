from pathlib import Path
import json,numpy as np,hashlib
P=Path(__file__).resolve().parents[1];ROOT=P.parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
src=P/'inputs/CURRENT-EAST-GUARD-CONSTRUCTION-R18.json';d=json.load(open(src));rows={r['path'].split('/')[-1]:r for r in d['east_guard_parts']}
def center(i):
 v=np.array(rows[f'EAST_POST_{i:03d}']['vertices_world']);return v[:,[0,2]].mean(0)
def norm(a):return a/np.linalg.norm(a)
points={i:center(i) for i in range(9,22)}
def crossnorm(a):return np.array([-a[1],a[0]])
def offset(i,endpoint):
 if i in [9,21]:return crossnorm(norm(points[10]-points[9] if i==9 else points[21]-points[20]))
 n0=crossnorm(norm(points[i]-points[i-1]));n1=crossnorm(norm(points[i+1]-points[i]));b=norm(n0+n1);return b/np.dot(b,n1)
# Source-visible broad rectangular brushed cap; dimensions are explicitly MODEL photo estimates.
w=.075;h=.055;bevel=.004;top=8.04;bottom=top-h
profile=[(-w/2+bevel,bottom), (w/2-bevel,bottom),(w/2,bottom+bevel),(w/2,top-bevel),(w/2-bevel,top),(-w/2+bevel,top),(-w/2,top-bevel),(-w/2,bottom+bevel)]
parts=[];anchors=[]
for i in range(9,21):
 a,b=points[i],points[i+1];na,nb=offset(i,0),offset(i+1,1);rings=[]
 for pt,nn in [(a,na),(b,nb)]:rings.append([np.array([*(pt+nn*x)[:1],y,(pt+nn*x)[1]]) for x,y in profile])
 vs=[];uv=[];ts=[];length=float(np.linalg.norm(b-a))
 def tri(v0,v1,v2,uv0,uv1,uv2):
  j=len(vs);vs.extend([v0.tolist(),v1.tolist(),v2.tolist()]);uv.extend([uv0,uv1,uv2]);ts.extend([j,j+1,j+2])
 def quad(a,b,c,d,u0,u1,u2,u3):tri(a,b,c,u0,u1,u2);tri(a,c,d,u0,u2,u3)
 for j in range(8):
  k=(j+1)%8;edge=float(np.linalg.norm(np.array(profile[k])-profile[j]));quad(rings[0][j],rings[1][j],rings[1][k],rings[0][k],[0,0],[length,0],[length,edge],[0,edge])
 for end in range(2):
  cen=np.array([points[i+end][0],(top+bottom)/2,points[i+end][1]])
  for j in range(8):
   k=(j+1)%8
   vv=[cen,rings[end][k],rings[end][j]] if end==0 else [cen,rings[end][j],rings[end][k]]
   tri(*vv,[0,0],[profile[k][0],profile[k][1]-bottom],[profile[j][0],profile[j][1]-bottom])
 # Orient each triangle outward using segment interior projected clamped to centerline.
 vv=np.array(vs)
 for j in range(0,len(ts),3):
  inds=ts[j:j+3];v=vv[inds];c=v.mean(0);n=np.cross(v[1]-v[0],v[2]-v[0]);xz=c[[0,2]];axis=norm(b-a);mid=a+axis*np.clip(np.dot(xz-a,axis),0,length);inside=np.array([mid[0],(top+bottom)/2,mid[1]])
  desired=c-inside if j<48 else np.array([axis[0],0,axis[1]])*(-1 if j<72 else 1)
  if np.dot(n,desired)<0:ts[j+1],ts[j+2]=ts[j+2],ts[j+1]
 r=rows[f'EAST_GLASS_{i:03d}_GASKET_TOP'];anchors.append(r)
 parts.append({'name':f'EAST_SOURCE_VISIBLE_STAINLESS_TOPRAIL_{i:03d}_R18','source_glass_index':i,'length_MODEL':length,'vertices_world':vs,'uv':uv,'submesh_triangles':[ts]})
for i in range(9,22):anchors.append(rows[f'EAST_POST_{i:03d}'])
sourceframes=[P/'research/curvedfront/selected-source-frames/source_286p45s.jpg',P/'research/curvedfront/selected-source-frames/frame_05.jpg']
payload={'schema':'chooguard.source_visible_well_rail.r18.v1','operation':'ADD observed actual stainless rectangular top rail bodies to existing east well head009-018 and visible short return019-020 only','parent_path':'FPSWorld/Architecture/2F/MAIN2F_USER_MARKED_FRONT_GUARDS_C176','new_root_name':'R18_SOURCE_VISIBLE_EAST_WELL_TOP_RAILS','frozen_inputs':[{'path':str(src.relative_to(ROOT)),'sha256':sha(src)}]+[{'path':str(x.relative_to(ROOT)),'sha256':sha(x)} for x in sourceframes if x.exists()],'anchor_rows':anchors,'existing_stainless_material':'Assets/ChooGuard/Art/StationInterior/MarkedFloorGuardsC176_20261018/Materials/C176_PHOTO_STAINLESS_HAIRLINE.mat','source_location':'https://www.youtube.com/watch?v=RY2qvE0Tugk retained clip local286.453 and287.987s; head joint and lounge corner/well continuous reverse angle','construction_qualification':'Existing post axes and guard course fixed metric anchors;75x55mm rounded rectangular cap estimated from source, not surveyed/99percent. Top remainsY8.04; no inferred LoungeA/B alias. Return021-023 pending extent.','top_y':top,'profile_MODEL_m':[w,h,bevel],'new_rail_parts':parts,'all_existing_glass_posts_gaskets_colliders_native_floors_WCs_1F_retained':True,'all2F_complete':False,'fidelity_measured_percent':None}
q=P/'geometry/OBSERVED-EAST-WELL-TOPRAILS-PAYLOAD-R18.json';q.write_text(json.dumps(payload,separators=(',',':')));print(q.relative_to(ROOT),sha(q),'parts',len(parts),'length',sum(x['length_MODEL'] for x in parts))
