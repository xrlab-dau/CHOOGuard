import bpy,pathlib,json,math
from mathutils import Vector
D=pathlib.Path.cwd()/'asset-library/research-public/2026-09-21/quality-a/station-model-source';bpy.ops.wm.open_mainfile(filepath=str(D/'station-batched-no3dletters.blend'));s=bpy.context.scene;allowed={'-부산역','Layer0','-부산역_선로상층부','-부산역_선로상층부(주차장)'}
for o in list(s.objects):
 if o.type=='MESH' and o.get('source_layer') not in allowed:bpy.data.objects.remove(o,do_unlink=True)
reg=json.loads((D/'station-rigid-registration.json').read_text());a=math.radians(reg['yawDegreesSourceXYCCW']);c,ss=math.cos(a),math.sin(a);tx,ty=reg['translationSourceXYMetres'];targets=[('ExistingConcourseAnchor',-20,0,5),('ExistingPlatformAnchor',48,4,0),('ExistingExitAnchor',-64,-4,0),('InteriorCandidateWest',-35,0,5),('InteriorCandidateCenter',0,0,5),('InteriorCandidateNorth',0,40,5),('InteriorCandidateSouth',-20,-40,5)];rows=[];dg=bpy.context.evaluated_depsgraph_get()
for name,east,north,floor in targets:
 dx,dy=east-tx,north-ty;x,y=c*dx+ss*dy,-ss*dx+c*dy;origin=Vector((x,y,100));hits=[]
 for _ in range(60):
  hit,p,n,idx,o,m=s.ray_cast(dg,origin,Vector((0,0,-1)),distance=130)
  if not hit:break
  hits.append({'sourceZMetres':p.z,'normalZ':n.z,'sourceLayer':o.get('source_layer'),'material':o.data.materials[o.data.polygons[idx].material_index].name});origin=p-Vector((0,0,.005))
 rows.append({'name':name,'stationLocalEastNorth':[east,north],'existingAuthoredFloorY':floor,'sourceXY':[x,y],'verticalIntersections':hits,'nearAuthoredFloorSupport':any(abs(p['sourceZMetres']-floor)<.25 and p['normalZ']>.8 for p in hits)})
(D/'station-floor-support-probes.json').write_text(json.dumps({'surface':'Read-only vertical geometric intersections; not nav/path validation','sourceLayers':sorted(allowed),'verticalRegistration':0,'probes':rows,'authority':'Existing named anchors from Assets/ChooGuard/Editor/MvpStationBuilder.cs; remainingpoints explicitly author-selected interior probes'},ensure_ascii=False,indent=2));print([(r['name'],r['nearAuthoredFloorSupport'],[round(h['sourceZMetres'],2) for h in r['verticalIntersections']]) for r in rows])
