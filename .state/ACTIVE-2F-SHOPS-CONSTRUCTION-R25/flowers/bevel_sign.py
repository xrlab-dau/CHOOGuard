import bpy,bmesh,json
from pathlib import Path
R=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/flowers');s=json.load(open(R/'sign_raw_solid.json'));bpy.ops.wm.read_factory_settings(use_empty=True)
m=bpy.data.meshes.new('ActualPhotoLetters');m.from_pydata(s['vertices'],[],[s['triangles'][i:i+3] for i in range(0,len(s['triangles']),3)]);m.update();o=bpy.data.objects.new('ActualPhotoLetters',m);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True)
bm=bmesh.new();bm.from_mesh(m);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(m);bm.free()
b=o.modifiers.new('PhysicalEdgeBevel','BEVEL');b.width=.0018;b.segments=2;b.limit_method='ANGLE';b.angle_limit=.15;bpy.ops.object.modifier_apply(modifier=b.name);m.calc_loop_triangles();s['vertices']=[list(v.co) for v in m.vertices];s['triangles']=[x for t in m.loop_triangles for x in t.vertices];s['bevel_m']=.0018;(R/'SIGN-PHYSICAL-PHOTO-CONTOUR-MESH.json').write_text(json.dumps(s))
