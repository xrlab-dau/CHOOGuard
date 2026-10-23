import bpy,json
from pathlib import Path
from mathutils import Vector
D=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-1F-LOCKERS-FREE-ASSETS-20261019')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(D/'research/26dc07440e3d40dc86a1f3fc9c01b0f3-source/source/locker.fbx'))
out=[]
for obj in bpy.context.scene.objects:
 if obj.type!='MESH': continue
 verts=[obj.matrix_world@v.co for v in obj.data.vertices]
 lo=[min(v[i] for v in verts) for i in range(3)];hi=[max(v[i] for v in verts) for i in range(3)]
 out.append(dict(name=obj.name,vertices=len(verts),faces=len(obj.data.polygons),min=lo,max=hi,materials=[m.name if m else None for m in obj.data.materials]))
(D/'SOURCE-MESH-STRUCTURE.json').write_text(json.dumps(out,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(D/'downloaded-locker-working.blend'))
print(json.dumps(out))
