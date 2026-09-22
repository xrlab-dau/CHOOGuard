import bpy,json,pathlib,math
R=pathlib.Path(__file__).resolve().parents[2];A=R/'art/blender';m=json.loads((A/'restored-facilities-manifest.json').read_text());result=[]
for row in m['models']:
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False);bpy.ops.import_scene.fbx(filepath=str(R/row['fbx']));bpy.context.view_layer.update();obs=[o for o in bpy.context.scene.objects if o.type=='MESH'];v=[o.matrix_world@p.co for o in obs for p in o.data.vertices];lo=[min(p[k] for p in v) for k in range(3)];hi=[max(p[k] for p in v) for k in range(3)];expected=row['boundsBlenderMetres'];error=max(abs(actual[k]-expected[key][k]) for key,actual in [('min',lo),('max',hi)] for k in range(3));assert error<.3,(row['name'],error)
 bays=[o for o in obs if o.name.startswith('Apparatus opening recessed back')];roof=[o for o in obs if o.name.startswith('Roof slab') or o.name.startswith('Observed parapet roof')]
 if row['name']!='BusanStationRestored':
  assert len(bays)==3
  for o in roof:assert any(p.normal.z>.99 for p in o.data.polygons) and any(p.normal.z<-.99 for p in o.data.polygons)
 assert not [o for o in bpy.context.scene.objects if o.type=='FONT']
 result.append({'name':row['name'],'importedBounds':{'min':lo,'max':hi},'maximumBoundsDifferenceMeters':error,'bayRecesses':len(bays),'roofHasUpwardAndDownwardFaces':bool(roof),'meshCount':len(obs),'axisAndMetreRoundTrip':'PASS'})
(A/'restoration-verification.json').write_text(json.dumps({'surface':'Blender FBX roundtrip and geometry assertions only; not Unity validation','results':result},indent=2)+'\n')
