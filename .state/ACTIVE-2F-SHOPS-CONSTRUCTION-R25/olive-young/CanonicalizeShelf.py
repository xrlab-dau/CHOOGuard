import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix
b=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/olive-young/sources/free-models')
report={}
for key,name in [('shelf','SingleFace-Shelf.fbx'),('counter','Checkout-Counter.fbx'),('pos','POS-Terminal.fbx'),('basket','shoppingbasket.fbx')]:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(b/key/name));obs=[o for o in bpy.context.scene.objects if o.type=='MESH'];pts=[o.matrix_world@Vector(c)for o in obs for c in o.bound_box];lo=Vector([min(p[k]for p in pts)for k in range(3)]);hi=Vector([max(p[k]for p in pts)for k in range(3)]);report[key]={'dimensions':list(hi-lo),'material_slots':sorted({m.name for o in obs for m in o.data.materials if m})}
print('CANONICAL_SOURCE',json.dumps(report));(b/'EXISTING-STAGED-SOURCE-AXES.json').write_text(json.dumps(report,indent=2))
