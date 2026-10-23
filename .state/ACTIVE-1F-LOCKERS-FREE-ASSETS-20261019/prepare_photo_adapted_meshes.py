"""Adapt downloaded locker geometry; no Unity/Assets writes."""
import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector
D=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-1F-LOCKERS-FREE-ASSETS-20261019')
bpy.ops.wm.open_mainfile(filepath=str(D/'downloaded-locker-working.blend'))
components=json.loads((D/'SOURCE-MESH-COMPONENTS.json').read_text())
source=bpy.data.objects['01_1']; shell=bpy.data.objects['01']
door_ids=next(set(c['vertex_indices']) for c in components[1]['components'] if c['vertices']==8)
lock_ids=next(set(c['vertex_indices']) for c in components[1]['components'] if c['vertices']==240)
def source_copy(obj,ids,name,size):
 ids=sorted(ids);remap={v:i for i,v in enumerate(ids)}
 pts=[obj.matrix_world@obj.data.vertices[i].co for i in ids]
 lo=[min(v[k] for v in pts) for k in range(3)];hi=[max(v[k] for v in pts) for k in range(3)]
 # FBX in Blender: X=width, Z=height, -Y=front. Store as Unity-style X/Y/Z.
 verts=[((v.x-(lo[0]+hi[0])/2)/(hi[0]-lo[0])*size[0],(v.z-(lo[2]+hi[2])/2)/(hi[2]-lo[2])*size[1],-(v.y-(lo[1]+hi[1])/2)/(hi[1]-lo[1])*size[2]) for v in pts]
 faces=[tuple(remap[v] for v in p.vertices) for p in obj.data.polygons if set(p.vertices)<=set(ids)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
 ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob)
 return ob
def bevel(obj,width=.0015):
 bpy.context.view_layer.objects.active=obj
 mod=obj.modifiers.new('Folded powder-coated edge bevel','BEVEL');mod.width=width;mod.segments=3
 bpy.ops.object.modifier_apply(modifier=mod.name)
 for poly in obj.data.polygons:poly.use_smooth=False
def mesh_data(obj,provenance):
 mesh=obj.data;mesh.calc_loop_triangles();vs=[];ns=[];uv=[];tri=[]
 # Split corners retains clean hard steel folds and smooth curved barrel facets.
 for t in mesh.loop_triangles:
  for vid in t.vertices:
   v=mesh.vertices[vid].co;vs.append([round(float(a),8) for a in v]);ns.append([round(float(a),8) for a in t.normal]);uv.append([round(float(v.x)*2,7),round(float(v.y)*2,7)]);tri.append(len(vs)-1)
 return dict(name=obj.name,vertices=vs,normals=ns,uv=uv,triangles=tri,provenance=provenance)
out={}
casing=source_copy(shell,set(range(len(shell.data.vertices))),'Downloaded_Hollow_Cabinet_Shell',[.547,1.98,.68]);bevel(casing)
out['cabinet_shell']=mesh_data(casing,'Downloaded source FBX object01 hollow shell, normalized and scaled to existing current module; added 1.5mm edge bevel. Not source site measurement.')
barrel=source_copy(source,lock_ids,'Downloaded_Lock_Barrel',[.020,.020,.010])
out['lock_barrel']=mesh_data(barrel,'Downloaded source FBX object01_1 connected round lock component; fitted inside source-photo small square lock housing. Source curved pull handle omitted.')
shelf=source_copy(source,door_ids,'Downloaded_Panel_Adapted_Internal_Shelf',[.508,.015,.626]);bevel(shelf,.0009)
out['internal_shelf']=mesh_data(shelf,'Downloaded door panel topology resized into same-module internal horizontal compartment divider.')
for tag,h,wh in [('small',.314,.105),('medium',.634,.28),('large',.954,.46)]:
 panel=source_copy(source,door_ids,'Downloaded_Cream_Door_'+tag,[.538,h,.025])
 bpy.ops.mesh.primitive_cube_add(size=1,location=(.08,.04,0));cut=bpy.context.object;cut.name='Photo_Inspection_Window_Cutter';cut.dimensions=(.067,wh,.12)
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 bpy.context.view_layer.objects.active=panel
 mod=panel.modifiers.new('Actual-photo narrow vertical inspection window','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cut
 bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cut,do_unlink=True);bevel(panel)
 data=mesh_data(panel,'Downloaded source FBX object01_1 door-slab component; fitted to retained current door envelope; photo-specific true cut-through inspection window; bevel. Different-height repetition is MODEL, not measured.')
 data['height']=h;data['window_height']=wh
 out['door_'+tag]=data
 pane=source_copy(source,door_ids,'Downloaded_Panel_Adapted_Inspection_Glass_'+tag,[.066,wh-.001,.005]);bevel(pane,.0004)
 out['glass_'+tag]=mesh_data(pane,'Downloaded source panel topology resized for separate inset tinted inspection glazing behind real cut-through opening.')
original=D/'research/26dc07440e3d40dc86a1f3fc9c01b0f3-source/source/locker.fbx'
receipt={'asset_source':'https://sketchfab.com/3d-models/none-26dc07440e3d40dc86a1f3fc9c01b0f3','source_sha256':hashlib.sha256(original.read_bytes()).hexdigest(),'license':'CC-BY-4.0','author_credit':'adventurer','photo_reference':'source-evidence/retained-work-originals/workers/twin/output/architecture-resume-20261010/MAIN1F-approved-interior-ESC-stairs/targeted-ESC-locker-photos/02-locker-entry-and-underside.jpeg','adaptations':['Retain current module poses, door count, booth envelope and all circulation.','Reuse real downloaded hollow casing, slab and lock geometry as meaningful equipment components.','Cream powder-coated steel, narrow transparent inspection slits, small square-lock housings from actual photos.','Omit original nonmatching orange/red finish and curved pull handles.','Large slit height 0.46m, medium0.28m and small0.105m are photo-proportioned MODEL choices, not surveyed dimensions.','Retain numeric door labels as real markings only; no text represents cabinet, door, window or lock.'],'meshes':out}
(D/'PHOTO-ADAPTED-DOWNLOADED-MESHES.json').write_text(json.dumps(receipt,separators=(',',':')))
print(json.dumps({k:{'vertices':len(v['vertices']),'triangles':len(v['triangles'])//3} for k,v in out.items()}))
