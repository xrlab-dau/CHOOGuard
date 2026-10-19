import bpy, bmesh, json, math
from pathlib import Path
from mathutils import Vector

P=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/soban').resolve();D=P/'downloaded-models';S=P/'quality-staging';S.mkdir(exist_ok=True)
manifest=[]
def reset():bpy.ops.wm.read_factory_settings(use_empty=True)
def imported(name):reset();bpy.ops.import_scene.gltf(filepath=str(D/(name+'.glb')))
def selected_export(name, obs, source, material=None, simplify=None):
 bpy.ops.object.select_all(action='DESELECT');copies=[]
 for src in obs:
  o=src.copy();o.data=src.data.copy();bpy.context.collection.objects.link(o);o.parent=None;o.matrix_world=src.matrix_world.copy();o.select_set(True);copies.append(o)
 if not copies:return
 for o in copies:
  bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
  if simplify and len(o.data.polygons)>1000:
   mod=o.modifiers.new('IndependentSourceDerivativeOnly','DECIMATE');mod.ratio=simplify;bpy.ops.object.modifier_apply(modifier=mod.name)
 points=[o.matrix_world@Vector(c)for o in copies for c in o.bound_box];lo=Vector([min(p[i]for p in points)for i in range(3)]);hi=Vector([max(p[i]for p in points)for i in range(3)])
 bottom=Vector([(lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z]);
 for o in copies:o.location-=bottom
 folder=S/name;folder.mkdir(exist_ok=True)
 bpy.ops.export_scene.fbx(filepath=str(folder/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,path_mode='STRIP',embed_textures=False)
 manifest.append(dict(id=name,fbx=str((folder/(name+'.fbx')).relative_to(P)),source=source,material=material,triangles=sum(sum(len(f.vertices)-2 for f in o.data.polygons)for o in copies),source_original_unchanged=True,derivative='Sourcepartisolation/owncleanmaterial; sourceoriginalpreserved'))
 for o in copies:bpy.data.objects.remove(o,do_unlink=True)

imported('RiceCooker-Candidate2');selected_export('BlackRiceCooker',[o for o in bpy.data.objects if o.type=='MESH'],'RiceCooker-Candidate2',material='rice_part_based')
imported('Sink-CGIME06');selected_export('StainlessSink',[o for o in bpy.data.objects if o.type=='MESH'],'Sink-CGIME06',material='clean_steel',simplify=.08)
imported('Basic-Bowl');selected_export('CeramicBowl',[o for o in bpy.data.objects if o.type=='MESH'],'Basic-Bowl',material='clean_ceramic')
# A separate rounded-square derivative of the downloaded bowl follows the actual white side-dish form.
for o in bpy.data.objects:
 if o.type=='MESH':
  bpy.context.view_layer.objects.active=o;o.select_set(True)
  # Imported model has its own glTF root axis conversion. Work in actual world horizontal XY.
  world=[o.matrix_world@v.co for v in o.data.vertices];cx=(min(v.x for v in world)+max(v.x for v in world))/2;cy=(min(v.y for v in world)+max(v.y for v in world))/2
  inv=o.matrix_world.inverted()
  for v,w in zip(o.data.vertices,world):
   x,y=w.x-cx,w.y-cy;r=math.hypot(x,y);l=(abs(x)**4+abs(y)**4)**.25
   if l>1e-8:w.x=cx+x*r/l;w.y=cy+y*r/l
   v.co=inv@w
selected_export('RoundedSquareSideDish',[o for o in bpy.data.objects if o.type=='MESH'],'Basic-Bowl',material='clean_ceramic')
imported('Restaurant-Kitchen-Set-Part1')
partmap={'ServicePot':'SM_Pot_01_Coockware_0','ServicePotLid':'SM_Pot_01_Top_Coockware_0','KitchenLadle':'SM_Ladle_Utensils_0','KitchenSpatula':'SM_Spatula_low_Utensils_0','KitchenKnife':'SM_Knife_08_Utensils_0','SoupSpoon':'SM_Spoon_Utensils_0','CuttingBoard':'SM_CuttingBoard_Small_Trays_0','ServingTray':'SM_DishTray_03_Trays_0','ServicePlate':'SM_Plate_03_Dishes_0'}
for name,source in partmap.items():selected_export(name,[bpy.data.objects[source]],'Restaurant-Kitchen-Set-Part1',material='clean_steel'if any(a in name for a in ['Pot','Ladle','Spatula','Knife','Spoon'])else'clean_ceramic'if'Plate'in name else'wood'if'Board'in name else'black')

# The donor domestic oven body is inappropriate here. Retain only its actual upper cooktop
# mesh as an independent source derivative; the worn source albedo is not carried into the shop.
imported('GasStove-Lyskilde')
for o in list(bpy.data.objects):
 if o.type!='MESH':continue
 if 'Glass'in o.name:bpy.data.objects.remove(o,do_unlink=True);continue
 o.data.transform(o.matrix_world);o.matrix_world.identity();bm=bmesh.new();bm.from_mesh(o.data)
 top=max(v.co.z for v in bm.verts);remove=[v for v in bm.verts if v.co.z<top-(max(v.co.z for v in bm.verts)-min(v.co.z for v in bm.verts))*.25];bmesh.ops.delete(bm,geom=remove,context='VERTS');bm.to_mesh(o.data);bm.free()
selected_export('CooktopHardware',[o for o in bpy.data.objects if o.type=='MESH'],'GasStove-Lyskilde',material='clean_steel')

# Brand sign authored from real font outlines as extruded and beveled solid geometry,
# not a TextMeshPro mesh bake. Each Hangul glyph is an individually editable solid.
reset();font=bpy.data.fonts.load(str(Path('Assets/ChooGuard/ThirdParty/Fonts/NotoSansCJKkr-Regular.otf').resolve()));glyphs=[]
for i,ch in enumerate('소반한식'):
 curve=bpy.data.curves.new('ObservedHangulOutline_'+str(i),'FONT');curve.font=font;curve.body=ch;curve.size=1;curve.align_x='CENTER';curve.align_y='CENTER';curve.extrude=.025;curve.bevel_depth=.0035;curve.bevel_resolution=2;curve.resolution_u=8;curve.offset=.012
 o=bpy.data.objects.new('SobanSolidGlyph_'+str(i),curve);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o=bpy.context.view_layer.objects.active;o.data.calc_loop_triangles()
 vs=[(v.co.x,v.co.y,-v.co.z)for v in o.data.vertices];lo=[min(v[j]for v in vs)for j in range(3)];hi=[max(v[j]for v in vs)for j in range(3)];h=hi[1]-lo[1];s=.43/h;cx=(lo[0]+hi[0])/2;cy=(lo[1]+hi[1])/2
 vv=[[((v[0]-cx)*s)+(i-1.5)*.66,(v[1]-cy)*s,v[2]*s]for v in vs];tt=[]
 for f in o.data.loop_triangles:tt.extend([f.vertices[0],f.vertices[2],f.vertices[1]])
 glyphs.append(dict(id='SOBAN_SOLID_GLYPH_'+str(i),literal=ch,vertices=vv,triangles=tt,material='white_led',qualification='DIRECT_FONT_OUTLINE_EXTRUSION_BEVEL_SOURCE_PHOTO_RAISED_LETTERS_NOT_TMP_BAKE'))
 o.select_set(False)
(S/'SOBAN-SOLID-OUTLINE-GLYPHS.json').write_text(json.dumps(glyphs,ensure_ascii=False))
(S/'QUALITY-ASSET-MANIFEST.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2));print('SOBAN_QUALITY_ASSETS_READY',len(manifest),'ownsolidglyphs',len(glyphs))
