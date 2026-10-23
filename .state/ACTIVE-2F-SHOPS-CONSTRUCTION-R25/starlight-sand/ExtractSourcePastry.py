import bpy,pathlib,json,numpy as np,bmesh
from mathutils import Vector,Matrix
p=pathlib.Path(__file__).resolve().parent/'sources/models/financier-own/financier-own.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(p));o=max([x for x in bpy.data.objects if x.type=='MESH'],key=lambda x:len(x.data.vertices));arr=np.array([o.matrix_world@v.co for v in o.data.vertices]);zlo=arr[:,2].min();arr[:,2]-=zlo
print('height',np.quantile(arr[:,2],[0,.1,.25,.5,.75,.9,.99,1]).tolist())
for h in [.015,.022,.027,.032,.04,.05]:
 a=arr[arr[:,2]>h];print(h,'bounds',a.min(axis=0).tolist(),a.max(axis=0).tolist())
# Exclude source plate by retaining high scanned pastry surfaces. Local origin and scale preserved in derivative only.
# Source plate/rim surfaces continue to32mm; pastry-only bounds are established above40mm in source units.
keep=arr[:,2]>.040
faces=[list(poly.vertices) for poly in o.data.polygons if all(keep[i] for i in poly.vertices)]
idx=sorted(set(i for f in faces for i in f));remap={i:j for j,i in enumerate(idx)};points=arr[idx];center=points.mean(axis=0)
# Pastry x/y principal direction from remaining observed mesh; normal stays sourceplate vertical.
v,q=np.linalg.eigh(np.cov((points[:,:2]-center[:2]).T));b=q[:,np.argsort(v)[::-1]]
if np.linalg.det(b)<0:b[:,1]*=-1
xy=(points[:,:2]-center[:2])@b;points[:,:2]=xy;points[:,2]-=points[:,2].min()
mesh=bpy.data.meshes.new('SourceScannedPastryWithoutPlate');mesh.from_pydata(points.tolist(),[],[[remap[i] for i in f] for f in faces]);mesh.materials.append(o.data.materials[0]);mesh.update()
# Transfer per-loop actual source UV coordinates to same source face loops.
uv=mesh.uv_layers.new(name='UVMap');oldUV=o.data.uv_layers.active
fi=0
for poly in o.data.polygons:
 if all(keep[i] for i in poly.vertices):
  newpoly=mesh.polygons[fi]
  for nl,ol in zip(newpoly.loop_indices,poly.loop_indices):uv.data[nl].uv=oldUV.data[ol].uv
  fi+=1
old=o.data;o.data=mesh;o.matrix_world=Matrix.Identity(4);bpy.data.meshes.remove(old)
# Close only the cut bottom boundary with explicit source-derived perimeter; retains top scanned shape/UV.
bm=bmesh.new();bm.from_mesh(mesh);boundary=[e for e in bm.edges if e.is_boundary];bmesh.ops.holes_fill(bm,edges=boundary,sides=0);bm.to_mesh(mesh);bm.free()
for a in list(bpy.data.objects):
 if a!=o:bpy.data.objects.remove(a,do_unlink=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(p),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,path_mode='COPY',embed_textures=False)
print('SOURCE_PASTRY_ONLY',len(mesh.polygons),'extent',np.ptp(points,axis=0).tolist())
