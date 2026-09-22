from pathlib import Path
prefix=Path('asset-library/research-public/2026-09-21/quality-a/station-model-source/convert_station_batched.py').read_text().split('# Build reusable')[0];exec(prefix)
defs={c.name:c for c in model.component_definitions};results=[];s=bpy.context.scene;s.render.engine='BLENDER_EEVEE_NEXT';s.render.resolution_x=900;s.render.resolution_y=400;s.render.resolution_percentage=100;s.world.use_nodes=True;s.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.8
for i,name in enumerate(['ROOT/g56/g0/g0/g12','ROOT/g56/g0/g0/g13']):
 for o in list(s.objects):bpy.data.objects.remove(o,do_unlink=True)
 ent=model.entities;rootT=Matrix.Identity(4)
 for part in name.split('/')[1:]:
  g=list(ent.groups)[int(part[1:])];rootT=rootT@Matrix(g.transform);ent=g.entities
 items=walk(ent,'SIGN'+name);points=[]
 for k,(me,t,la) in enumerate(items):
  o=bpy.data.objects.new('sign_'+str(k),me);s.collection.objects.link(o);o.matrix_world=rootT@t;points.extend(rootT@t@v.co for v in me.vertices)
 lo=Vector(tuple(min(p[k] for p in points) for k in range(3)));hi=Vector(tuple(max(p[k] for p in points) for k in range(3)));size=hi-lo;center=(hi+lo)/2;axis=min(range(3),key=lambda k:size[k]);direction=Vector((0,0,0));direction=Vector((-.94,.34,.06))
 bpy.ops.object.light_add(type='SUN');bpy.context.object.rotation_euler=(.2,-.6,-.4);bpy.context.object.data.energy=2
 bpy.ops.object.camera_add();cam=bpy.context.object;s.camera=cam;cam.data.type='ORTHO';cam.location=center+direction*max(size)*3;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();q=cam.rotation_euler.to_quaternion().inverted();p=[q@(v-center) for v in points];cam.data.ortho_scale=max(max(v.x for v in p)-min(v.x for v in p),(max(v.y for v in p)-min(v.y for v in p))*900/400)*1.1;s.render.filepath=str(D/('sign-group-'+str(i)+'.png'));bpy.ops.render.render(write_still=True);results.append({'definition':name,'meshPieces':len(items),'boundsSize':list(size),'preview':s.render.filepath})
(D/'source-sign-group-audit.json').write_text(json.dumps(results,ensure_ascii=False,indent=2))
