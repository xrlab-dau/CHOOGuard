#!/usr/bin/env python3
"""Rerunnable actual-mesh 2F vector CAD authoring, no live editor/asset changes."""
import argparse, datetime, gzip, hashlib, json, math, struct, time, sys
from pathlib import Path
from collections import Counter, defaultdict
import numpy as np
import shapely
from shapely import union_all, polygons
from shapely.geometry import Polygon, MultiPolygon, box, LineString, MultiLineString
from shapely.ops import linemerge
from shapely.affinity import affine_transform, translate
from reportlab.pdfgen import canvas

ROOT=Path('/Users/um-yunsang/CHOOGuard')
BASE=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad'
sys.path.insert(0,str(BASE/'deps'))
import pyclipper
Q=np.array([[-.34290633335024717,-.9393696006089878],[.9393696006089878,-.3429063333502471]])
CUTS=[7.15,8.4,10.5,13.5]
LOW,HIGH=6.97,14.03
EPS=1e-4

def physical_error_budget(row):
 p=row['path'].lower()
 architecture=('wall','column','floor','roof','ceiling','soffit','door','glaz','entry','entrance','portal','booth','frame','fascia','header','partition','shaft','stair','escalator','handrail','balustrade')
 if row['source_kind']!='CURRENT_R25_MODEL_NOT_NEW_USER_CONFIRMATION' or any(w in p for w in architecture):return 1e-4
 dense=('display','counter','table','chair','shelf','bottle','candy','food','gummy','downloaded','sku','furniture','equipment','tableware','basket','kitchen','case','goods','product','rack','fridge','beverage','bread','sandwich','plate','cup','stool','bench','plant')
 return 1e-3 if any(w in p for w in dense) else 1e-4

def sha(p):
 h=hashlib.sha256()
 with Path(p).open('rb') as f:
  for chunk in iter(lambda:f.read(1024*1024),b''):h.update(chunk)
 return h.hexdigest()
def utc():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def rings(g):
 if g.is_empty:return []
 ps=[g] if g.geom_type=='Polygon' else list(g.geoms)
 return [{'exterior':np.round(np.asarray(p.exterior.coords),6).tolist(),'holes':[np.round(np.asarray(h.coords),6).tolist() for h in p.interiors]} for p in ps if p.geom_type=='Polygon' and p.area>1e-10]
def iter_rings(gs):
 for p in gs:
  yield p['exterior']
  yield from p['holes']
def read_meshes(path):
 meshes={}
 with gzip.open(path,'rb') as f:
  while raw:=f.read(4):
   ix=struct.unpack('<i',raw)[0];nv=struct.unpack('<i',f.read(4))[0]
   vs=np.frombuffer(f.read(nv*12),dtype='<f4').reshape(-1,3).astype(np.float64)
   ns=struct.unpack('<i',f.read(4))[0];tris=[];other=[]
   for s in range(ns):
    topo,ni=struct.unpack('<ii',f.read(8));indices=np.frombuffer(f.read(ni*4),dtype='<i4')
    if topo==0:tris.append(indices.reshape(-1,3))
    elif topo==2: # Quads -> exact two triangles per quad.
     a=indices.reshape(-1,4);tris.extend([a[:,[0,1,2]],a[:,[0,2,3]]])
    else:other.append((topo,ni))
   meshes[ix]={'v':vs,'t':np.concatenate(tris) if tris else np.empty((0,3),dtype=np.int32),'non_tri_topologies':other}
 return meshes

def merge_collinear(lines,digits=6):
 tol=10.0**(-digits)
 if len(lines)==0:return []
 a=np.round(np.asarray(lines,dtype=float),digits)
 a=a[np.linalg.norm(a[:,1]-a[:,0],axis=1)>tol]
 if len(a)==0:return []
 flat=a.reshape(-1,4)
 rev=(flat[:,0]>flat[:,2])|((flat[:,0]==flat[:,2])&(flat[:,1]>flat[:,3]))
 a[rev]=a[rev,::-1]
 a=np.unique(a.reshape(-1,4),axis=0).reshape(-1,2,2)
 # Merge only equal straight supporting lines. No curve simplification or gap fill.
 groups=defaultdict(list)
 for p in a:
  d=p[1]-p[0];length=np.linalg.norm(d);u=d/length
  key=(round(float(u[0]),6),round(float(u[1]),6),round(float(u[0]*p[0,1]-u[1]*p[0,0]),digits))
  groups[key].append(p)
 out=[]
 for pts in groups.values():
  if len(pts)==1:out.append(pts[0]);continue
  u=pts[0][1]-pts[0][0];u/=np.linalg.norm(u)
  # Keep actual endpoint coordinates; merge only overlapping/touching intervals.
  intervals=sorted([(float(p[0]@u),float(p[1]@u),p[0],p[1]) for p in pts],key=lambda v:v[0])
  lo,hi,pa,pb=intervals[0]
  for l,h,p,q in intervals[1:]:
   if l<=hi+tol:
    if h>hi:hi,pb=h,q
   else:out.append([pa,pb]);lo,hi,pa,pb=l,h,p,q
  out.append([pa,pb])
 return np.round(np.asarray(out),digits).tolist()

def section(v,t,y,plane_matrix=Q,precision_digits=6):
 if not len(t):return []
 tri=v[t];yy=tri[:,:,1];keep=(yy.min(axis=1)<=y)&(yy.max(axis=1)>=y)
 tri=tri[keep]
 if not len(tri):return []
 hits=[];which=[]
 for e in range(3):
  a,b=tri[:,e],tri[:,(e+1)%3];sel=(a[:,1]<=y)!=(b[:,1]<=y)
  ids=np.flatnonzero(sel);p=a[sel]+(b[sel]-a[sel])*((y-a[sel,1])/(b[sel,1]-a[sel,1]))[:,None]
  hits.append(p[:,[0,2]]@plane_matrix.T);which.append(ids)
 h=np.concatenate(hits);ids=np.concatenate(which);order=np.argsort(ids,kind='stable');h=h[order];ids=ids[order]
 u,c=np.unique(ids,return_counts=True);good=np.repeat(c==2,c);h=h[good].reshape(-1,2,2)
 return merge_collinear(h,precision_digits)

def projected_union(p,grid=EPS/8):
 if not len(p):return Polygon()
 # Integer triangle union, with same winding for all projected source faces.
 # Quantization is bounded in physical units by each actual mesh transform.
 q=np.rint(p/grid).astype(np.int64)
 a=q[:,1]-q[:,0];b=q[:,2]-q[:,0];areas=a[:,0]*b[:,1]-a[:,1]*b[:,0]
 q=q[areas!=0];areas=areas[areas!=0]
 if not len(q):return Polygon()
 neg=areas<0;q[neg]=q[neg,::-1]
 # Cancel exact opposite projected triangle edges before polygon union.
 # The residual directed edge field has identical nonzero winding to all triangles.
 # This keeps actual concavities/holes and eliminates coplanar internal diagonals.
 least=np.lexsort((q[:,:,1],q[:,:,0]),axis=1)[:,0]
 rotate=(least[:,None]+np.arange(3)[None,:])%3
 q=q[np.arange(len(q))[:,None],rotate]
 q=np.unique(q.reshape(-1,6),axis=0).reshape(-1,3,2)
 vertices,ids=np.unique(q.reshape(-1,2),axis=0,return_inverse=True);faces=ids.reshape(-1,3)
 edges=np.concatenate([faces[:,[0,1]],faces[:,[1,2]],faces[:,[2,0]]])
 signs=np.where(edges[:,0]<edges[:,1],1,-1)
 undirected=np.sort(edges,axis=1)
 pairs,ei=np.unique(undirected,axis=0,return_inverse=True)
 nets=np.bincount(ei,weights=signs,minlength=len(pairs)).astype(np.int64)
 adj=defaultdict(list)
 for (a,b),count in zip(pairs,nets):
  if count>0:adj[int(a)].extend([int(b)]*int(count))
  elif count<0:adj[int(b)].extend([int(a)]*int(-count))
 paths=[]
 for start in list(adj):
  while adj[start]:
   stack=[start];circuit=[]
   while stack:
    v=stack[-1]
    if adj[v]:stack.append(adj[v].pop())
    else:circuit.append(stack.pop())
   circuit=circuit[::-1]
   if len(circuit)>3:paths.append(vertices[circuit[:-1]].tolist())
 if not paths:return Polygon()
 clip=pyclipper.Pyclipper();clip.StrictlySimple=True;clip.PreserveCollinear=False
 clip.AddPaths(paths,pyclipper.PT_SUBJECT,True)
 tree=clip.Execute2(pyclipper.CT_UNION,pyclipper.PFT_NONZERO,pyclipper.PFT_NONZERO)
 ps=[]
 def collect(node):
  for child in node.Childs:
   if not child.IsHole and len(child.Contour)>=3:
    outer=np.asarray(child.Contour,dtype=float)*grid
    holes=[(np.asarray(h.Contour,dtype=float)*grid).tolist() for h in child.Childs if h.IsHole and len(h.Contour)>=3]
    ps.append(Polygon(outer,holes))
   collect(child)
 collect(tree)
 return ps[0] if len(ps)==1 else MultiPolygon(ps) if ps else Polygon()

def clipped_projected_triangles(world_tri):
 # Intersect 3D mesh triangles with the exact 2F vertical band before XZ projection.
 tri=world_tri[(world_tri[:,:,1].max(axis=1)>=LOW)&(world_tri[:,:,1].min(axis=1)<=HIGH)]
 if not len(tri):return np.empty((0,3,2))
 inside=(tri[:,:,1].min(axis=1)>=LOW)&(tri[:,:,1].max(axis=1)<=HIGH)
 result=[tri[inside][:,:,[0,2]]@Q.T]
 for initial in tri[~inside]:
  p=list(initial)
  for value,above in [(LOW,True),(HIGH,False)]:
   nxt=[]
   for i,a in enumerate(p):
    b=p[(i+1)%len(p)];aa=(a[1]>=value) if above else (a[1]<=value);bb=(b[1]>=value) if above else (b[1]<=value)
    if aa:nxt.append(a)
    if aa!=bb:nxt.append(a+(b-a)*((value-a[1])/(b[1]-a[1])))
   p=nxt
   if not p:break
  for i in range(1,len(p)-1):result.append(np.array([[p[0][[0,2]],p[i][[0,2]],p[i+1][[0,2]]]])@Q.T)
 return np.concatenate(result) if result else np.empty((0,3,2))

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--snapshot',default=str(BASE/'snapshot-001/CURRENT-2F-MESH-SNAPSHOT.json'));ap.add_argument('--out',default=str(BASE/'output-001'));args=ap.parse_args()
 source=Path(args.snapshot);source=source if source.is_absolute() else ROOT/source
 out=Path(args.out);out=out if out.is_absolute() else ROOT/out;out.mkdir(exist_ok=True,parents=True)
 data=json.loads(source.read_text());meshes=read_meshes(ROOT/data['mesh_binary'])
 layers=[];counts=Counter();segcount=0;footcache={};surfacecache={};sectioncache={};floor_bounds=[];t0=time.time();empty=[]
 local_mesh_grid=defaultdict(lambda:float('inf'))
 for row in data['objects']:
  if 'mesh_index' in row:
   a=np.asarray(row['world_matrix_column_major']).reshape(4,4,order='F')[:3,:3]
   # Every actual instance constrains its shared-mesh local grid; no assumed units.
   grid=physical_error_budget(row)/(8*max(1e-20,float(np.linalg.norm(a))))
   local_mesh_grid[row['mesh_index']]=min(local_mesh_grid[row['mesh_index']],grid)
 for ix,row in enumerate(data['objects']):
  obj={k:row[k] for k in ['object_id','renderer_id','path','source_kind','matching_2f_height_band','source_mesh_asset','source_mesh_object_id','materials','bounds_min_world','bounds_max_world']}
  short=row['object_id'].split('-')[-2];role=row['source_kind'];prefix={'OFFICIAL_REFERENCE':'OFFICIAL','CURRENT_R25_MODEL_NOT_NEW_USER_CONFIRMATION':'MODEL_R25','CURRENT_2F_GEOMETRY':'CURRENT2F','MATCHING_LEVEL_CROSSING_REFERENCE':'CROSS_LEVEL'}[role]
  obj['indexed_cad_layer']=f'{prefix}_{ix:05d}_{short}';obj['sections']={};obj['actual_band_projected_footprint']=[];obj['matching_y7_surface_reference']=[];obj['new_user_confirmation']=False;obj['derived_vector_physical_error_bound_m']=physical_error_budget(row)
  layers.append(obj);counts[prefix]+=1
  if 'mesh_index' not in row:continue
  mesh=meshes[row['mesh_index']]
  if len(mesh['v'])==0 or len(mesh['t'])==0:
   obj['empty_mesh_no_physical_triangles']=True;empty.append(ix);continue
  mat=np.asarray(row['world_matrix_column_major']).reshape(4,4,order='F');v=mesh['v']@mat[:3,:3].T+mat[:3,3];t=mesh['t']
  lo,hi=v[:,1].min(),v[:,1].max()
  # Actual section for every small object, including goods/signs above the main plane.
  levels=[y for y in CUTS if lo<=y<=hi]
  if role!='OFFICIAL_REFERENCE' and hi>LOW and lo<HIGH:
   mid=(max(lo,LOW)+min(hi,HIGH))*.5
   if not any(abs(y-mid)<.001 for y in levels):levels.append(mid)
  linear=mat[:3,:3];H=Q@linear[[0,2]];shift=Q@mat[[0,2],3];budget=physical_error_budget(row);maxlocal=np.max(np.abs(mesh['v']),axis=0)
  vertical=[j for j in range(3) if np.linalg.norm(H[:,j])*maxlocal[j]<=budget/16 and abs(linear[1,j])>1e-10]
  j=vertical[0] if len(vertical)==1 else None
  axes=[a for a in range(3) if a!=j] if j is not None else None
  upright=j is not None and sum(abs(linear[1,a])*maxlocal[a] for a in axes)<=budget/16
  for y in levels:
   if upright:
    cut=(y-mat[1,3])/linear[1,j]
    scale_norm=max(1e-20,float(np.linalg.norm(linear)))
    local_step=budget/(16*scale_norm)
    digits=max(0,int(math.ceil(-math.log10(local_step))))
    ck=(row['mesh_index'],j,round(float(cut),digits+3),digits)
    if ck not in sectioncache:sectioncache[ck]=section(mesh['v'][:,[axes[0],j,axes[1]]],t,cut,np.eye(2),digits)
    local=np.asarray(sectioncache[ck]);ss=np.round(local@H[:,axes].T+shift,6).tolist() if len(local) else []
   else:ss=section(v,t,y,precision_digits=max(3,math.ceil(-math.log10(budget/16))))
   if ss:obj['sections'][f'{y:.5f}']=ss;segcount+=len(ss)
  tri=v[t]
  # Physical footprint is exact projected mesh union, never bounds/hull/colored box.
  # For native objects only matching-level sections and near-Y7 surfaces are drawn.
  # This prevents full-height roof silhouettes from masquerading as 2F occupied areas.
  if role!='OFFICIAL_REFERENCE':
   is_inside=lo>=LOW and hi<=HIGH
   if is_inside and j is not None:
    # Exact affine reuse of each shared mesh footprint under any upright yaw/scale.
    # This avoids repeating GEOS unions for hundreds of identical physical goods.
    key=(row['mesh_index'],'local_projection',j)
    if key not in footcache:footcache[key]=projected_union(mesh['v'][t][:,:,axes],grid=local_mesh_grid[row['mesh_index']])
    A=H[:,axes];g=affine_transform(footcache[key],[A[0,0],A[0,1],A[1,0],A[1,1],shift[0],shift[1]])
   else:
    cache_key=(row['mesh_index'],tuple(np.round(linear[[0,2]].ravel(),9))) if is_inside else None
    if cache_key is not None and cache_key in footcache:g=translate(footcache[cache_key],*shift)
    else:
     if is_inside:
      p=(mesh['v'][t]@linear[[0,2]].T)@Q.T;g0=projected_union(p,grid=budget/8);footcache[cache_key]=g0;g=translate(g0,*shift)
     else:g=projected_union(clipped_projected_triangles(tri),grid=budget/8)
   obj['actual_band_projected_footprint']=rings(g)
  # Matching near-Y7 upward surfaces stay geometric references; no floor ownership inference.
  if len(tri):
   near=np.all(np.abs(tri[:,:,1]-7.0)<=.03,axis=1)
   tt=tri[near]
   if len(tt):
    n=np.cross(tt[:,1]-tt[:,0],tt[:,2]-tt[:,0]);ln=np.linalg.norm(n,axis=1);up=(ln>1e-12)&(n[:,1]>.9*ln);tt=tt[up]
    if len(tt):
     g=projected_union(tt[:,:,[0,2]]@Q.T);obj['matching_y7_surface_reference']=rings(g)
     if '/MAIN2F_APPROVED_' in row['path'] and not g.is_empty:floor_bounds.append(g.bounds)
  if not obj['sections'] and not obj['actual_band_projected_footprint'] and not obj['matching_y7_surface_reference']:empty.append(ix)
  if ix%200==0:print(json.dumps({'processed':ix,'total':len(data['objects']),'seconds':round(time.time()-t0,1),'footprint_cache':len(footcache)}),flush=True)
 record={'schema':'chooguard.actual_current_2f_vector_cad.r26.v1','utc':utc(),'source_snapshot':str(source.relative_to(ROOT)),'source_snapshot_sha256':sha(source),'source_scene':data['scene'],'source_scene_sha256':data['scene_sha256_after'],'source_snapshot_utc':data['utc'],'source_scene_dirty':data['scene_dirty_after'],'world_XZ_to_cad_qx_minus_qy':Q.tolist(),'units':'metres','objects':layers,'counts':dict(counts),'section_segments':segcount,'standard_sections_world_y':CUTS,'actual_footprint_band_world_y':[LOW,HIGH],'official_geometry_semantics':'Matching-level sections/surfaces only; full-height/native roof silhouettes not treated as occupied 2F floor. O36 platform ceiling remains reference.','new_models_are_current_not_user_confirmed':True,'alternative_LOD1_exclusions':data['alternative_LOD1_registry_count'],'actual_excluded_renderers':data['actual_excluded_renderers'],'all_layers_achromatic':True,'all_official_layers_individually_indexed_locked':True,'visible_text':False,'Scene_Assets_changed':False,'source_image_warp_or_guidebox_geometry':False,'floor_void_topology_changes':False,'native_transform_changes':False,'precision':{'architectural_physical_error_bound_m':1e-4,'dense_goods_furniture_physical_error_bound_m':1e-3,'scope':'Derived CAD only; every source Scene/Asset vertex unchanged','engine':'Exact opposite projected internal edge cancellation plus pyclipper1.4.0 nonzero winding integer boundary union with PolyTree holes','method':'Per-shared-mesh local grid is constrained by every actual instance physical error budget divided by8 times actual transform Frobenius norm; section grid uses additional16x allowance. No hull/bounds substitution.'},'derivation':'Actual vertices and triangle indices transformed through actual localToWorld. Triangle-plane sections merged only on collinear touching/overlapping support lines; physical nonnative footprint is union of actual mesh triangles clipped to 2F vertical band, preserving interior holes.','unreadable_meshes':data['unreadable_meshes'],'snapshot_inventory_only_no_matching_vector_indices':empty,'validation_owner':'user'}

 from compact_render_cad_r26 import compact_record,render_record
 record=compact_record(record)
 render_record(record,source,out,started=t0)

if __name__=='__main__':main()
