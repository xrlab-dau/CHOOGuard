#!/usr/bin/env python3
"""Fixed exported native geometry authoring analysis; no live editor calls."""
from pathlib import Path
import collections, gzip, hashlib, json, struct
import numpy as np
from PIL import Image,ImageDraw

ROOT=Path('/Users/um-yunsang/CHOOGuard')
BASE=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26'
OUT=BASE/'public/rail-police/native-signature'
SRC=BASE/'cad/snapshot-001/CURRENT-2F-MESH-SNAPSHOT.json'
j=json.loads(SRC.read_text())
native=next(o for o in j['objects'] if o['path'].endswith('OfficialStation_-부산역_5'))
wanted={native['mesh_index']}|{o['mesh_index'] for o in j['objects'] if any(s in o['object_id'] for s in ['989813836-','1756346415-','1117893400-','1528514897-'])}
meshes={}
with gzip.open(ROOT/j['mesh_binary'],'rb') as f:
 while raw:=f.read(4):
  ix=struct.unpack('<i',raw)[0];nv=struct.unpack('<i',f.read(4))[0]
  vr=f.read(nv*12);ns=struct.unpack('<i',f.read(4))[0];subs=[]
  for s in range(ns):
   tp,n=struct.unpack('<ii',f.read(8));ind=f.read(n*4)
   if ix in wanted and tp==0:subs.append(np.frombuffer(ind,dtype='<i4').reshape(-1,3).copy())
   elif ix in wanted:subs.append(np.empty((0,3),dtype='i4'))
  if ix in wanted:meshes[ix]=(np.frombuffer(vr,dtype='<f4').reshape(-1,3).copy(),subs)
  if len(meshes)==len(wanted):break
def transformed(o):
 v,subs=meshes[o['mesh_index']];m=np.array(o['world_matrix_column_major']).reshape(4,4,order='F')
 v=v@m[:3,:3].T+m[:3,3]
 # Actual qY sign, as opposed to the plotting packet's -qY.
 q=np.stack([-.34290633335024717*v[:,0]-.9393696006089878*v[:,2],
             -.9393696006089878*v[:,0]+.34290633335024717*v[:,2],v[:,1]],axis=1)
 return q,subs
q,subs=transformed(native)
ROI=np.array([[45.,-15.,6.95],[80.,3.,12.3]])
def selected(t):
 a=q[t];return np.all(a.max(axis=1)>=ROI[0],axis=1)&np.all(a.min(axis=1)<=ROI[1],axis=1)
def bnds(a):return [a.min(axis=0).round(6).tolist(),a.max(axis=0).round(6).tolist()]
def clip_triangle(tri):
 p=list(tri)
 for axis in range(3):
  for value,above in [(ROI[0,axis],True),(ROI[1,axis],False)]:
   nxt=[]
   for i,aa in enumerate(p):
    bb=p[(i+1)%len(p)];ia=(aa[axis]>=value) if above else (aa[axis]<=value);ib=(bb[axis]>=value) if above else (bb[axis]<=value)
    if ia:nxt.append(aa)
    if ia!=ib:nxt.append(aa+(bb-aa)*((value-aa[axis])/(bb[axis]-aa[axis])))
   p=nxt
   if not p:return []
 return [np.asarray([p[0],p[i],p[i+1]]) for i in range(1,len(p)-1)]
material_stats=[];features=[];alltri=[];allmat=[]
for mi,t in enumerate(subs):
 t=t[selected(t)]
 if not len(t):continue
 a=q[t];n=np.cross(a[:,1]-a[:,0],a[:,2]-a[:,0]);ar=np.linalg.norm(n,axis=1)/2
 material_stats.append({'material_index':mi,'triangles':len(t),'qX_qY_worldY_bounds':bnds(a.reshape(-1,3)),'area_m2':round(float(ar.sum()),6),'material_asset':native['materials'][mi]['asset']})
 clipped=np.asarray([tr for tr0 in a for tr in clip_triangle(tr0)])
 if len(clipped):alltri.append(clipped);allmat.extend([mi]*len(clipped))
 # Weld vertices only for topology grouping; original geometry retained in records.
 unique,inv=np.unique(np.round(a.reshape(-1,3),5),axis=0,return_inverse=True)
 ids=inv.reshape(-1,3);parent=np.arange(len(t))
 def find(x):
  while parent[x]!=x:parent[x]=parent[parent[x]];x=parent[x]
  return int(x)
 owner={}
 for fi,vertices in enumerate(ids):
  for vi in vertices:
   if int(vi) in owner:
    aa,bb=find(fi),find(owner[int(vi)])
    if aa!=bb:parent[bb]=aa
   else:owner[int(vi)]=fi
 groups=collections.defaultdict(list)
 for fi in range(len(t)):groups[find(fi)].append(fi)
 for gi,faces in enumerate(groups.values()):
  aa=a[faces];nn=n[faces];area=ar[faces]
  f={'material_index':mi,'component_index':gi,'triangle_indices_in_submesh':np.flatnonzero(selected(subs[mi]))[faces].tolist(),'triangles':len(faces),'qX_qY_worldY_bounds':bnds(aa.reshape(-1,3)),'area_m2':round(float(area.sum()),6)}
  if area.sum()>1e-8:
   f['mean_absolute_normal_qXYZ']=(np.sum(np.abs(nn),axis=0)/(2*area.sum())).round(6).tolist()
  features.append(f)
a=np.concatenate(alltri);mat=np.array(allmat)
planes=[]
n=np.cross(a[:,1]-a[:,0],a[:,2]-a[:,0]);ar=np.linalg.norm(n,axis=1)/2
for axis in [0,1]:
 # Actual coplanar-ish vertical triangles, not projected horizontal slabs.
 sel=(np.ptp(a[:,:,axis],axis=1)<.025)&(ar>1e-8)&(np.abs(n[:,axis])/(2*np.maximum(ar,1e-12))>.97)
 vals=np.round(a[sel,:,axis].mean(axis=1)/.05)*.05
 for val in np.unique(vals):
  ids=np.flatnonzero(sel)[vals==val]
  planes.append({'axis':'qX' if axis==0 else 'qY','coordinate_bin_m':round(float(val),4),'triangles':len(ids),'area_m2':round(float(ar[ids].sum()),5),'qX_qY_worldY_bounds':bnds(a[ids].reshape(-1,3)),'material_triangle_counts':dict(collections.Counter(map(int,mat[ids])))})
planes.sort(key=lambda f:f['area_m2'],reverse=True)
report={'schema':'chooguard.r26.police_native_signature_geometry.v1','source_snapshot':str(SRC.relative_to(ROOT)),'source_binary_sha256':j['mesh_binary_sha256'],'native_object_id':native['object_id'],'native_mesh_asset':native['source_mesh_asset'],'native_mesh_index':native['mesh_index'],'coordinate_order':['actual_qX','actual_qY','worldY'],'ROI':ROI.tolist(),'intersecting_triangle_count':len(a),'material_stats':material_stats,'welded_same_material_components':features,'dominant_vertical_planes':planes,'Scene_Assets_changes':False,'native_calls':0,'authoring_geometry_only':True}
report['note']='Material/component bounds preserve original source triangles that intersect ROI; dominant planes and NPZ have been exactly clipped to ROI.'
report['actual_clipped_material_stats']=[]
for mi in np.unique(mat):
 aa=a[mat==mi];nn=np.cross(aa[:,1]-aa[:,0],aa[:,2]-aa[:,0]);area=np.linalg.norm(nn,axis=1)/2
 report['actual_clipped_material_stats'].append({'material_index':int(mi),'triangles':len(aa),'qX_qY_worldY_bounds':bnds(aa.reshape(-1,3)),'actual_clipped_area_m2':round(float(area.sum()),6)})
# Global physical quads using generic door/window source materials locate candidates;
# materials alone do not identify a public function.
report['global_generic_door_window_material_parts']=[]
for mi in [41,44,49]:
 for fi,t in enumerate(subs[mi]):
  aa=q[t];nn=np.cross(aa[1]-aa[0],aa[2]-aa[0]);area=np.linalg.norm(nn)/2
  report['global_generic_door_window_material_parts'].append({'material_index':mi,'source_submesh_triangle_index':fi,'qX_qY_worldY_bounds':bnds(aa),'area_m2':round(float(area),6)})
(OUT/'NATIVE5-ROI-GEOMETRY-SIGNATURE-R26.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
np.savez_compressed(OUT/'native5-roi-source-geometry.npz',triangles=a,material=mat)
im=Image.new('RGB',(1800,2600),'white');draw=ImageDraw.Draw(im)
def linepts(points,oy,vertical=False):
 if vertical:return [(int(90+(p[0]-45)*46),int(oy+300-(p[1]-6.9)*46)) for p in points]
 return [(int(90+(p[0]-45)*46),int(oy+850-(p[1]+15)*46)) for p in points]
for oi,h in enumerate([7.15,8.4,9.65]):
 oy=oi*600-250
 segs=[]
 for tri in a[(a[:,:,2].min(axis=1)<=h)&(a[:,:,2].max(axis=1)>=h)]:
  pts=[]
  for e in range(3):
   aa,bb=tri[e],tri[(e+1)%3]
   if (aa[2]<=h)!=(bb[2]<=h):pts.append((aa+(bb-aa)*(h-aa[2])/(bb[2]-aa[2]))[:2])
  if len(pts)==2:segs.append(pts)
 draw.text((70,oi*600+10),f'Original fixed Native5 horizontal section worldY {h} m; actual qX 45..80, qY -15..3',fill='black')
 for seg in segs:draw.line(linepts(seg,oy),fill='#444444',width=1)
 # Current generated front/back walls are contextual, not independent identity anchors.
 for o in j['objects']:
  if o.get('mesh_index') not in wanted or o==native:continue
  qq,ss=transformed(o);bb=np.array(bnds(qq));draw.line(linepts([[bb[0,0],np.mean(bb[:,1])],[bb[1,0],np.mean(bb[:,1])]],oy),fill='#cc6666',width=2)
sel=(np.ptp(a[:,:,1],axis=1)<.04)&(a[:,:,1].mean(axis=1)<-6)&(a[:,:,1].mean(axis=1)>-12.5)
polys=a[sel][:,:,[0,2]]
draw.text((70,2200),'Actual Native5 recessed qY -12.5..-6 faces elevation: qX 45..80 / worldY 6.9..12.3',fill='black')
for poly in polys:draw.line(linepts(list(poly)+[poly[0]],2250,True),fill='#444444',width=1)
im.save(OUT/'NATIVE5-ROI-AUTHORING-SECTIONS.png')
print(json.dumps({'triangles':len(a),'material_stats':material_stats,'components':len(features),'dominant_planes':planes[:22]},ensure_ascii=False,indent=2))
