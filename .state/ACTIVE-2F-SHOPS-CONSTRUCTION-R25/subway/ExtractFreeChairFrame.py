import bpy,json,sys
from mathutils import Vector
from pathlib import Path
root=Path('/Users/um-yunsang/CHOOGuard'); out=root/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/subway'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(out/'sources/free-models/SchoolChair_01/SchoolChair_01.fbx'))
vs=[];ts=[]
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 o.data.calc_loop_triangles();offset=len(vs)
 vs.extend([list(o.matrix_world@v.co) for v in o.data.vertices]);ts.extend([[offset+i for i in t.vertices] for t in o.data.loop_triangles])
# Blender Z-up; map to Unity Y-up and normalize to observed .84m chair height.
mins=[min(v[i] for v in vs) for i in range(3)];maxs=[max(v[i] for v in vs) for i in range(3)]
sx=.44/(maxs[0]-mins[0]);sy=.84/(maxs[2]-mins[2]);sz=.48/(maxs[1]-mins[1]);vs=[[(v[0]-(mins[0]+maxs[0])/2)*sx,(v[2]-mins[2])*sy,-(v[1]-(mins[1]+maxs[1])/2)*sz] for v in vs]
# Free sourced tubular legs only; actual photo seat/back reconstructed separately.
tri=[t for t in ts if max(vs[i][1] for i in t)<.415];used=sorted({i for t in tri for i in t});rem={v:i for i,v in enumerate(used)};frame={'vertices':[vs[i] for i in used],'triangles':[rem[i] for t in tri for i in reversed(t)],'source':'PolyHaven SchoolChair_01 CC0 downloadedFBX tubular leg geometry only','source_bounds_blender': [mins,maxs]}
(out/'FREE-SOURCED-STEEL-CHAIR-FRAME.json').write_text(json.dumps(frame));print('FRAME',len(used),len(tri))
