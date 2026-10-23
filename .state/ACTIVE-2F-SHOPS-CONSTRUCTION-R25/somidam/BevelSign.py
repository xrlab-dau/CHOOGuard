import bpy,bmesh,json
from pathlib import Path
p=Path(__file__).parent;j=json.loads((p/'SIGN-CLOSED-SOURCE-SOLID.json').read_text());bpy.ops.wm.read_factory_settings(use_empty=True)
me=bpy.data.meshes.new('SomidamActualPhotoOutline');me.from_pydata(j['vertices'],[],[j['triangles'][i:i+3] for i in range(0,len(j['triangles']),3)]);me.update();ob=bpy.data.objects.new('SomidamLetterSolids',me);bpy.context.collection.objects.link(ob);bpy.context.view_layer.objects.active=ob;ob.select_set(True)
bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.00001);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(me);bm.free()
be=ob.modifiers.new('PhysicalSignEdgeBevel','BEVEL');be.width=.0018;be.segments=2;be.limit_method='ANGLE';be.angle_limit=.52;be.use_clamp_overlap=True;bpy.ops.object.modifier_apply(modifier=be.name)
me.calc_loop_triangles();j['vertices']=[list(v.co) for v in me.vertices];j['triangles']=[i for t in me.loop_triangles for i in t.vertices];j['bevel_m']=.0018;j['bevel_segments']=2;j['holes_preserved_by_source_CCOMP']=True
(p/'SIGN-BEVELED-SOURCE-SOLID.json').write_text(json.dumps(j));print('SOURCE_SIGN_BEVELED',len(me.vertices),len(me.loop_triangles))
