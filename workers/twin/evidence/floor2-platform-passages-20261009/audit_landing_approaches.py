from pathlib import Path
import json,math,hashlib
import numpy as np
from PIL import Image,ImageDraw
from shapely.geometry import Polygon,Point,LineString,box
from shapely.ops import unary_union

E=Path('workers/twin/evidence/floor2-platform-passages-20261009');O=Path('workers/twin/output/floor2-platform-passages-20261009')
scope={};exec((E/'stage_walk_spines.py').read_text().split('regions={')[0],scope)
x=scope['x'];ms=scope['ms'];load=scope['load'];sha=scope['sha']
P=Path('Assets/ChooGuard/Art/Emergency/station-points.json');entries={r['id']:r for r in json.loads(P.read_text())['escalators']}
xyz=lambda p:np.array([p['x'],p['y'],p['z']])
cases=[('esc-well-n-56','low'),('esc-well-s-1','high'),('esc-well-s-34','high'),('esc-well-m-89-1','low')]
audit=[]
for id,level in cases:
    e=entries[id];path=[xyz(p)for p in e['path']];high=path[-2]if e['up']else path[1];low=path[1]if e['up']else path[-2]
    belt=high if level=='high'else low;other=low if level=='high'else high
    foot=path[-1]if (level=='high')==e['up']else path[0]
    axis=belt-other;axis[1]=0;axis/=np.linalg.norm(axis);side=np.array([axis[2],0,-axis[0]])
    expected=7.02 if level=='high'else 0.0;centre=belt+axis*3
    lo=centre[[0,2]]-7;hi=centre[[0,2]]+7;domain=box(lo[0],lo[1],hi[0],hi[1]);floors=[];segments=[]
    for owner in x['colliders']:
        if not owner.get('active')or not owner.get('enabled')or owner.get('trigger'):continue
        if owner['world_bounds_max'][1]<expected-.25 or owner['world_bounds_min'][1]>expected+1.85:continue
        boundslo=np.array(owner['world_bounds_min'])[[0,2]];boundshi=np.array(owner['world_bounds_max'])[[0,2]]
        if np.any(boundshi<lo)or np.any(boundslo>hi):continue
        M=np.array(owner['world_matrix']);m=None
        if owner['type']=='MeshCollider':
            m=ms.get(owner.get('mesh'))
            if not m or not m.get('binary'):continue
            v,t=load(m)
        elif owner['type']=='BoxCollider':
            cen=np.array(owner['local_center']);sz=np.array(owner['local_size'])/2
            v=np.array([cen+[a*sz[0],b*sz[1],c*sz[2]]for a,b,c in[(-1,-1,-1),(-1,-1,1),(-1,1,-1),(-1,1,1),(1,-1,-1),(1,-1,1),(1,1,-1),(1,1,1)]])
            t=np.array([[0,1,3],[0,3,2],[4,6,7],[4,7,5],[0,4,5],[0,5,1],[2,3,7],[2,7,6],[0,2,6],[0,6,4],[1,5,7],[1,7,3]])
        else:continue
        w=v@M[:3,:3].T+M[:3,3];p=w[t];q=p[:,:,[0,2]]
        hits=(q.max(axis=1)[:,0]>=lo[0])&(q.min(axis=1)[:,0]<=hi[0])&(q.max(axis=1)[:,1]>=lo[1])&(q.min(axis=1)[:,1]<=hi[1])
        for ordinal in np.flatnonzero(hits):
            r=p[ordinal];yy=r[:,1];n=np.cross(r[1]-r[0],r[2]-r[0]);norm=np.linalg.norm(n)
            if norm>1e-9 and n[1]>.90*norm and yy.max()<=expected+.25 and yy.min()>=expected-.25:
                poly=Polygon(r[:,[0,2]])
                if poly.area>1e-9:floors.append((poly,r,owner,m,int(ordinal)))
            for delta in [.25,.75,1.5]:
                y=expected+delta
                if yy.min()>=y or yy.max()<=y:continue
                points=[]
                for a,b in[(0,1),(1,2),(2,0)]:
                    if(yy[a]-y)*(yy[b]-y)<0:points.append(r[a,[0,2]]+(r[b,[0,2]]-r[a,[0,2]])*(y-yy[a])/(yy[b]-yy[a]))
                if len(points)==2 and np.linalg.norm(points[1]-points[0])>1e-8:
                    segment=LineString(points).intersection(domain)
                    if not segment.is_empty:segments.append((segment,owner,int(ordinal),delta))
    floorunion=unary_union([r[0]for r in floors]).buffer(1e-6)
    def support(point):
        out=[];probe=Point(point[[0,2]])
        for poly,r,owner,m,ordinal in floors:
            if not poly.buffer(1e-6).covers(probe):continue
            A=np.column_stack((r[:,[0,2]],np.ones(3)));bary=np.linalg.solve(A.T,[point[0],point[2],1]);y=float(bary@r[:,1])
            out.append({'world_Y':y,'owner':owner['path'],'global_object_id':owner['global_object_id'],'mesh_asset':m['asset']if m else None,'mesh_guid':m['asset_guid']if m else None,'triangle_ordinal':ordinal,'barycentric':bary.tolist()})
        return max(out,key=lambda v:v['world_Y'])if out else None
    # This is a conservative three horizontal plane test only, not a Unity capsule check.
    def blockers(line):
        found={}
        for segment,owner,ordinal,delta in segments:
            if line.distance(segment)>.34001:continue
            k=owner['path'];row=found.setdefault(k,{'owner':k,'global_object_id':owner['global_object_id'],'triangles':set(),'body_plane_above_floor':set()})
            row['triangles'].add(ordinal);row['body_plane_above_floor'].add(delta)
        return[dict(row,triangles=sorted(row['triangles']),body_plane_above_floor=sorted(row['body_plane_above_floor']))for row in found.values()]
    rows=[]
    for distance in np.arange(1,6.001,.5):
        p=belt+axis*distance;p[1]=expected;hit=support(p)
        if hit:p[1]=hit['world_Y']
        line=LineString([belt[[0,2]],p[[0,2]]]);body=blockers(line)
        rows.append({'distance_from_native_belt_end_m':float(distance),'world_candidate':p.tolist(),'actual_floor':hit,'continuous_same_floor_projection_from_belt':floorunion.covers(line),'conservative_three_plane_blockers_from_belt':body,'actual_capsule_or_navigation_verified':False})
    intended=belt+axis;intended[1]=expected
    full=LineString([belt[[0,2]],(belt+axis*6)[[0,2]]]);part=floorunion.intersection(full)
    parts=[part]if part.geom_type=='LineString'else list(part.geoms)if hasattr(part,'geoms')else[]
    intervals=sorted([sorted([full.project(Point(g.coords[0])),full.project(Point(g.coords[-1]))])for g in parts if g.geom_type=='LineString'])
    plots=[]
    def local_point(p):
        d=np.array([p[0]-belt[0],0,p[1]-belt[2]]);return [float(d@axis),float(d@side)]
    for segment,owner,ordinal,delta in segments:
        components=[segment]if segment.geom_type=='LineString'else list(segment.geoms)if hasattr(segment,'geoms')else[]
        for g in components:
            if g.geom_type=='LineString':plots.append([local_point(p)for p in g.coords])
    im=Image.new('RGB',(1100,850),'white');draw=ImageDraw.Draw(im)
    def pixel(p):return (int(110+(p[0]+1)*95),int(720-(p[1]+4)*80))
    for distance in range(-1,7):
        draw.line([pixel([distance,-4]),pixel([distance,4])],fill='#dddddd');draw.text((pixel([distance,-4])[0],740),str(distance),fill='black')
    for lateral in range(-4,5):
        draw.line([pixel([-1,lateral]),pixel([6,lateral])],fill='#dddddd');draw.text((70,pixel([-1,lateral])[1]),str(lateral),fill='black')
    for points in plots:draw.line([pixel(p)for p in points],fill='#a05742',width=2)
    draw.line([pixel([0,0]),pixel([6,0])],fill='#3574a8',width=3)
    def dot(p,color,text):
        a,b=pixel(p);draw.ellipse((a-6,b-6,a+6,b+6),fill=color);draw.text((a+9,b+3),text,fill=color)
    fp=local_point(foot[[0,2]]);dot(fp,'#dd3333','canonical foot');dot([1,0],'#268552','nominal 1m');dot([2,0],'#268552','candidate 2m')
    for label,name in [('stairsTop','adjacent stair top'),('stairsBottom','adjacent stair bottom')]:
        if e.get('stairs')and ((level=='high')==(label=='stairsTop')):
            sp=local_point(xyz(e[label])[[0,2]]);dot(sp,'#9755bd',name)
    if id=='esc-well-m-89-1':
        op=xyz(entries['esc-well-m-89-0']['path'][0]);sp=local_point(op[[0,2]]);dot(sp,'#9755bd','paired platform foot')
    draw.text((90,30),id+' '+level+' | original collider sections at floor+.25/.75/1.5',fill='black');draw.text((90,785),'Horizontal: outward distance from native belt end (m); vertical: side offset (m)',fill='black');draw.text((90,810),'Read-only source geometry; no actual Unity capsule/navigation PASS claimed',fill='black');plotpath=E/(id+'-'+level+'-source-body-sections.png');im.save(plotpath)
    audit.append({'id':id,'landing':level,'operating_role':'boarding'if((level=='low')==e['up'])else'arrival','up':e['up'],'original_canonical_foot':foot.tolist(),'actual_source_floor_at_original_foot':support(foot),'native_belt_endpoint':belt.tolist(),'outward_axis':axis.tolist(),'native_one_metre_walk_on_off_nominal':intended.tolist(),'canonical_minus_nominal_m':float(np.linalg.norm(foot-intended)),'same_floor_projected_support_intervals_from_belt_m':intervals,'candidate_landing_points':rows,'source_body_plot':str(plotpath),'blocker_sources_are_static_no_exemptions':True})
receipt={'schema':'chooguard.existing-native-landing-candidates-readonly.v1','source_inventory':str(scope['invpath']),'source_inventory_sha256':sha(scope['invpath']),'canonical_points':str(P),'canonical_points_sha256':sha(P),'source_geometry_epoch_original_native_preserved_in_scene3719':True,'method':'Original collider floor faces and body slices at floor+.25/.75/1.5; 0.34 m buffered centerline; endpoint candidates extend the exact original belt axis onto existing floors only. This does not validate current Unity capsule physics, baked NavMesh connectivity, full source closure or human field metrics. No nav links, geometry or footpoints are modified.','cases':audit,'writes_Unity_Assets_or_canonical':False}
(O/'existing-native-landing-candidates.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2))
for row in audit:
    print(row['id'],row['landing'],'snap',row['canonical_minus_nominal_m'])
    for r in row['candidate_landing_points']:print(r['distance_from_native_belt_end_m'],[round(v,6)for v in r['world_candidate']],bool(r['actual_floor']),r['continuous_same_floor_projection_from_belt'],[(v['owner'].split('/')[-1],len(v['triangles']))for v in r['conservative_three_plane_blockers_from_belt']])
