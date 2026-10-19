import bpy, json
from pathlib import Path
from mathutils import Vector

ROOT=Path('/Users/um-yunsang/CHOOGuard')
BASE=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/story-lamant/sources'
src=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/free-assets/source-0d033270863d45748c14556a562bc08c/0d033270863d45748c14556a562bc08c.glb'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(src))
meshes=[o for o in bpy.data.objects if o.type=='MESH']
allv=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
mn=Vector(tuple(min(v[i] for v in allv) for i in range(3)))
mx=Vector(tuple(max(v[i] for v in allv) for i in range(3)))
cx=(mn.x+mx.x)/2
changed=0
for o in meshes:
    world=[o.matrix_world@v.co for v in o.data.vertices]
    o.parent=None
    o.matrix_world.identity()
    shell=any(m and m.name=='Plastic' for m in o.data.materials)
    for vert,w in zip(o.data.vertices,world):
        if shell:
            # Keep source formed plywood/chrome structure. Broaden the Ant neck
            # to approximate the less pinched rounded cafe backs in photo12.
            z=(w.z-mn.z)/(mx.z-mn.z)
            strength=max(0,1-abs(z-.70)/.17)
            w.x=cx+(w.x-cx)*(1+.30*strength)
            changed+=int(strength>0)
        vert.co=w
    o.data.update()
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(BASE/'FieldPlywoodChromeChair_MODEL.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False)
(BASE/'CHAIR-DERIVATION.json').write_text(json.dumps({'source':str(src),'credit':'4-Legged Ant Chair by Deborah Kumagai, CC-BY 4.0','source_original_preserved':True,'adaptation':'Independent formed-shell neck widening, pale wood URP material applied in own Unity asset; downloaded chrome tube chassis retained','changed_source_shell_vertices':changed,'exact_observed_product_identity':False,'metric_fit_is_MODEL':True},indent=2))
