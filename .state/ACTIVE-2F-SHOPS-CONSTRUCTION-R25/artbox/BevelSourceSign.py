import bpy,bmesh,json
from pathlib import Path
b=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/artbox')
for color in ['white','red']:
 j=json.loads((b/('sign-'+color+'-solid.json')).read_text());m=bpy.data.meshes.new('source_outline_'+color);t=j['triangles'];m.from_pydata(j['vertices'],[],[t[i:i+3] for i in range(0,len(t),3)]);m.update()
 bm=bmesh.new();bm.from_mesh(m);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(m);bm.free()
 o=bpy.data.objects.new('ActualPhotoSolid_'+color,m);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True)
 bevel=o.modifiers.new('PhysicalSoftEdge2mm','BEVEL');bevel.width=.002;bevel.segments=2;bevel.limit_method='ANGLE';bevel.angle_limit=.4
 bpy.ops.object.modifier_apply(modifier=bevel.name);m=o.data;m.calc_loop_triangles()
 out={'vertices':[list(v.co) for v in m.vertices],'triangles':[i for t in m.loop_triangles for i in t.vertices],'bevel_m':.002,'source_outline_holes_preserved':True}
 (b/('sign-'+color+'-beveled.json')).write_text(json.dumps(out));o.select_set(False);print(color,len(out['vertices']),len(out['triangles']))
