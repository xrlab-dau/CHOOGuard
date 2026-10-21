"""Clip only the marked current slab overhang to the actual sloping native glass.
All kept faces retain their interpolated source UVs and normals. No old floor restore.
"""
from pathlib import Path
import json,hashlib
import numpy as np
from shapely.geometry import Polygon,box
from shapely.ops import unary_union,triangulate
from shapely import union_all
from shapely.affinity import affine_transform
T=Path(__file__).resolve().parents[1]
ROOT=T.parents[1]
Q=np.array([[-.34290633335024717,-.9393696006089878],[-.9393696006089878,.3429063333502471]])
CLEARANCE=.003
def surface_union(polys):
    # Live merged floor triangles contain micron-scale duplicate seam offsets.
    # A 1-micron computational grid resolves their topology without changing
    # the retained source vertices used to construct the derivative meshes.
    return union_all(polys,grid_size=1e-6)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def clip(poly,fun):
    out=[]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        da,db=fun(a),fun(b);da=0. if abs(da)<1e-9 else da;db=0. if abs(db)<1e-9 else db;ia,ib=da<=0,db<=0
        if ia:out.append(a)
        if ia!=ib:out.append(a+(b-a)*min(1.,max(0.,da/(da-db))))
    return out
def section(tri,y=7.):
    hit=[]
    for a,b in zip(tri,np.roll(tri,-1,axis=0)):
        if (a[1]<=y)!=(b[1]<=y):hit.append(a+(b-a)*(y-a[1])/(b[1]-a[1]))
    return np.array(hit)
def polygon_parts(geom):
    if geom.is_empty:return []
    if geom.geom_type=='Polygon':return [geom]
    return [p for g in geom.geoms for p in polygon_parts(g)]
def geometry(g,planes,label):
    vv=np.array(g['vertices_world']);uv=np.array(g['uv']);uv2=np.array(g.get('uv2',[]));norm=np.array(g.get('normals_local',[]))
    top=float(vv[:,1].max());bottom=float(vv[:,1].min());assert top-bottom<.32
    before=[]
    for ts in g['submesh_triangles']:
        for k in range(0,len(ts),3):
            tr=vv[ts[k:k+3]]
            ar=np.cross(tr[1]-tr[0],tr[2]-tr[0]);al=np.linalg.norm(ar)
            if max(abs(tr[:,1]-top))<1e-5 and al>1e-10 and abs(ar[1])/al>.99999:
                p=Polygon(tr[:,[0,2]]@Q.T)
                if p.area>1e-10:before.append(p)
    footprint=surface_union(before)
    # Exact removed region is constrained by the current mesh itself. The
    # halfspaces intersect it only along the user's one curtainwall strip.
    used=np.unique(np.concatenate([np.asarray(ts,dtype=int) for ts in g['submesh_triangles']]))
    ff=np.stack([vv[used]@p['n']-p['h'] for p in planes]);outside=ff.max(0)>1e-7
    oq=vv[used[outside]][:,[0,2]]@Q.T
    assert len(oq)>0 and oq[:,0].min()>33 and oq[:,0].max()<87 and oq[:,1].min()>31 and oq[:,1].max()<49, 'Cut would exceed exact marked facade region'
    has_uv=len(uv)==len(vv);has_uv2=len(uv2)==len(vv);has_norm=len(norm)==len(vv)
    outv=[];outuv=[];outuv2=[];outn=[];outts=[[] for _ in g['submesh_triangles']]
    def emit(poly,sub):
        if len(poly)<3:return
        for i in range(1,len(poly)-1):
            tr=[poly[0],poly[i],poly[i+1]]
            if np.linalg.norm(np.cross(tr[1][:3]-tr[0][:3],tr[2][:3]-tr[0][:3]))<1e-10:continue
            for x in tr:
                outts[sub].append(len(outv));outv.append(x[:3].tolist());outuv.append(x[3:5].tolist());outuv2.append(x[5:7].tolist());n=x[7:10];ln=np.linalg.norm(n);outn.append((n/ln if ln>1e-10 else np.array([0,1,0])).tolist())
    changed=0
    for sub,ts in enumerate(g['submesh_triangles']):
        for k in range(0,len(ts),3):
            ids=ts[k:k+3];tr=vv[ids];face=np.cross(tr[1]-tr[0],tr[2]-tr[0]);face/=max(1e-12,np.linalg.norm(face))
            poly=[np.r_[vv[i],uv[i] if has_uv else [0.,0.],uv2[i] if has_uv2 else [0.,0.],norm[i] if has_norm else face] for i in ids]
            if any(max(x[:3]@p['n']-p['h'] for x in poly)>1e-7 for p in planes):changed+=1
            for p in planes:
                poly=clip(poly,lambda x,p=p:float(x[:3]@p['n']-p['h']))
                if not poly:break
            emit(poly,sub)
    cap_start=len(outv)
    cap_area=0.;cap_parts=[]
    for plane_index,p in enumerate(planes):
        n=p['n'];nh=n[[0,2]];n2=float(nh@nh);origin_h=(p['h']-n[1]*top)*nh/n2;origin=np.array([origin_h[0],top,origin_h[1]])
        tangent=np.array([-n[2],0,n[0]])/np.sqrt(n2);up=np.array([-n[1]*n[0]/n2,1,-n[1]*n[2]/n2]);baseq=Q@origin[[0,2]];B=np.c_[Q@tangent[[0,2]],Q@up[[0,2]]];inv=np.linalg.inv(B);off=-inv@baseq
        source_param=affine_transform(footprint,[inv[0,0],inv[0,1],inv[1,0],inv[1,1],off[0],off[1]+top]);lo,_,hi,_=source_param.bounds
        cap=source_param.intersection(box(lo-1,bottom,hi+1,top))
        for j,p2 in enumerate(planes):
            if j==plane_index:continue
            r=[np.array([lo-1,bottom-1]),np.array([hi+1,bottom-1]),np.array([hi+1,top+1]),np.array([lo-1,top+1])]
            pp=clip(r,lambda x,p2=p2:float((origin+tangent*x[0]+up*(x[1]-top))@p2['n']-p2['h']))
            if len(pp)<3:cap=Polygon();break
            cap=cap.intersection(Polygon(pp))
            if cap.is_empty:break
        for poly in polygon_parts(cap):
            if poly.area<1e-9:continue
            parts=[tr for tr in triangulate(poly) if poly.covers(tr)]
            assert abs(sum(x.area for x in parts)-poly.area)<1e-6, 'Cap needs explicit constrained triangulation'
            for tr in parts:
                pp=[]
                for t,y in list(tr.exterior.coords)[:3]:
                    pos=origin+tangent*t+up*(y-top);pp.append(np.r_[pos,t,y-bottom,0.,0.,n])
                if np.cross(pp[1][:3]-pp[0][:3],pp[2][:3]-pp[0][:3])@n<0:pp.reverse()
                emit(pp,min(2,len(outts)-1))
            cap_area+=poly.area*np.linalg.norm(np.cross(tangent,up));cap_parts.append({'glass_plane':plane_index,'param_area':poly.area})
    after=[];cap_top=[];original_top=[]
    av=np.array(outv)
    for ts in outts:
        for i in range(0,len(ts),3):
            tr=av[ts[i:i+3]]
            ar=np.cross(tr[1]-tr[0],tr[2]-tr[0]);al=np.linalg.norm(ar)
            if max(abs(tr[:,1]-top))<1e-5 and al>1e-10 and abs(ar[1])/al>.99999:
                z=Polygon(tr[:,[0,2]]@Q.T)
                if z.area>1e-10:
                    after.append(z)
                    (cap_top if min(ts[i:i+3])>=cap_start else original_top).append(z)
    retained=surface_union(after);removed=footprint.difference(retained)
    expansion=retained.difference(footprint).area
    print(json.dumps({'construction':label,'current_top_area':footprint.area,'removed_top_area':removed.area,'outside_old_footprint_area':expansion,'cap_start':cap_start}),flush=True)
    assert removed.area>0 and removed.area<30 and expansion<.005
    return {'vertices_world':outv,'uv':outuv,'uv2':outuv2 if has_uv2 else [],'normals_local':outn,'submesh_triangles':outts}, {'label':label,'source_top_area_MODEL_m2':footprint.area,'removed_top_area_MODEL_m2':removed.area,'removed_top_rings_q':removed.__geo_interface__,'removed_vertex_q_bounds':[oq.min(0).tolist(),oq.max(0).tolist()],'top_world_y':top,'bottom_world_y':bottom,'source_faces_clipped':changed,'cap_surface_area_MODEL_m2':cap_area,'cap_parts':cap_parts}

def main():
    source=T/'inputs/USER-MARKED-CURRENT-FLOOR-AND-GLASS-SOURCE-R17.json';d=json.loads(source.read_text());row=next(x for x in d['floor_parts'] if x['object_id'].split('-')[-2]=='36765735')
    # This floor has an authored translation; clipping uses world positions,
    # while its existing local normals stay valid for the identity linear part.
    assert np.max(np.abs(np.array(row['world_matrix']).reshape(4,4,order='F')[:3,:3]-np.eye(3)))<1e-7
    planes=[]
    for owner in d['fixed_native_slab_band_surfaces']:
        if owner['path'].split('/')[-1] not in ['OfficialStation_-부산역_1','OfficialStation_-부산역_2','OfficialStation_-부산역_4']:continue
        for t in owner['actual_slab_band_triangles']:
            mat=owner['materials'][t['submesh']]
            if mat is None or 'a280c283311849b2fbb7' not in mat['asset']:continue
            v=np.array(t['vertices_world']);hit=section(v)
            if len(hit)!=2:continue
            aq=hit[:,[0,2]]@Q.T;mid=aq.mean(0)
            if not (33<mid[0]<87 and 31<mid[1]<49):continue
            n=np.cross(v[1]-v[0],v[2]-v[0]);ln=np.linalg.norm(n)
            if ln<.2:continue
            n/=ln
            if abs(n[1]+.202575)>.0001 or (Q@n[[0,2]])[1]<.8:continue
            h=float(n@v[0])
            if any(np.linalg.norm(n-p['n'])<1e-4 and abs(h-p['h_native'])<.002 for p in planes):continue
            planes.append({'n':n,'h':h-CLEARANCE,'h_native':h,'owner_id':owner['object_id'],'owner_mesh':owner['mesh_asset'],'owner_mesh_id':owner['mesh_object_id'],'source_vertex_indices':t['source_vertex_indices'],'submesh':t['submesh'],'triangle':t['triangle'],'source_vertices_world':t['vertices_world']})
    assert len(planes)==8, f'Expected actual 8 glass planes, got {len(planes)}'
    render,render_info=geometry(row['geometry'],planes,'CURRENT_RENDER')
    collider=row['colliders'][0];cg=collider.get('geometry',row['geometry']);collision,collider_info=geometry(cg,planes,'CURRENT_COLLIDER')
    serialized=[]
    for p in planes:serialized.append({k:(v.tolist() if isinstance(v,np.ndarray) else v) for k,v in p.items()})
    payload={'schema':'EXACT_USER_MARKED_CURTAINWALL_FLOOR_TRIM_R17','source':str(source.relative_to(ROOT)),'source_sha256':sha(source),'authority':'.state/ACTIVE-2F-FLOOR-TRIM-R17/USER-AUTHORITY.json','target':{k:v for k,v in row.items() if k!='geometry'},'render_geometry':render,'collider_geometry':collision,'source_glass_planes':serialized,'inside_glass_clearance_MODEL_m':CLEARANCE,'cut_construction':render_info,'collider_cut_construction':collider_info,'preserve_unmarked_floor_and_all_transport_X_voids':True,'originalAssets_native_1F_WC_columns_walls_unchanged':True,'new_openings':False,'QA_reopen_comparisoncaptures':False}
    out=T/'geometry/EXACT-MARKED-FLOOR-TRIM-PAYLOAD-R17.json';out.write_text(json.dumps(payload,ensure_ascii=False,separators=(',',':')))
    print(json.dumps({'payload':str(out),'sha256':sha(out),'glass_planes':len(planes),'top_removed_area':render_info['removed_top_area_MODEL_m2'],'collider_top_removed_area':collider_info['removed_top_area_MODEL_m2'],'only_current_main_floor_owner':row['object_id']},ensure_ascii=False))
if __name__=='__main__':main()
