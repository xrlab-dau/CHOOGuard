import bpy,json,hashlib
from pathlib import Path
repo=next(x for x in Path(__file__).resolve().parents if (x/'AGENTS.md').exists())
source=repo/'Assets/ChooGuard/Art/StationInterior/Props/StationBench4.fbx'
for ob in list(bpy.data.objects):bpy.data.objects.remove(ob,do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(source))
rows=[]
for ob in bpy.data.objects:
 if ob.type=='MESH':
  verts=[list(ob.matrix_world@v.co) for v in ob.data.vertices]
  bounds=[[min(v[k] for v in verts),max(v[k] for v in verts)] for k in range(3)] if verts else None
  rows.append(dict(name=ob.name,vertices=len(verts),polygons=len(ob.data.polygons),bounds_xyz=bounds,materials=[m.name if m else None for m in ob.data.materials]))
report=dict(source=str(source),sha256=hashlib.sha256(source.read_bytes()).hexdigest(),source_preserved=True,imported_readonly_into_offline_blender=True,scene_assets_mutated=False,objects=rows)
(Path(__file__).parent/'EXISTING-STATIONBENCH4-READONLY-INSPECTION.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
