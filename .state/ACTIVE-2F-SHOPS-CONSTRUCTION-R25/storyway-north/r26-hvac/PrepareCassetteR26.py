from pathlib import Path
import json,struct,hashlib,datetime,math
import numpy as np
from shapely.geometry import box,Polygon
from shapely.ops import triangulate
ROOT=Path('/Users/um-yunsang/CHOOGuard'); BASE=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/storyway-north'; W=BASE/'r26-hvac'
SRC=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/free-assets/backrooms-hvac-source/licensed-kit.glb'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def put(n,d): (W/n).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
raw=SRC.read_bytes();jn,jt=struct.unpack_from('<II',raw,12);g=json.loads(raw[20:20+jn]);off=20+jn;bn,bt=struct.unpack_from('<II',raw,off);buf=raw[off+8:off+8+bn]
def accessor(i):
 a=g['accessors'][i];v=g['bufferViews'][a['bufferView']];nc={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']];dt={5126:'<f4',5125:'<u4',5123:'<u2',5121:'<u1'}[a['componentType']];dt=np.dtype(dt);start=v.get('byteOffset',0)+a.get('byteOffset',0);stride=v.get('byteStride',nc*dt.itemsize)
 return np.ndarray((a['count'],nc),dtype=dt,buffer=buf,offset=start,strides=(stride,dt.itemsize)).copy()
p=g['meshes'][41]['primitives'][0];v=accessor(p['attributes']['POSITION']).astype(float);tr=accessor(p['indices']).astype(int).reshape(-1,3)
# glTF RH to Unity LH, then fit source intake only to source-photo central dark intake.
v[:,2]*=-1;tr=tr[:,[0,2,1]];lo=v.min(0);hi=v.max(0);v=(v-(lo+hi)/2)*np.array([.70,.023,.70])/(hi-lo);v[:,1]+=.025
lib={'Licensed_Huuxloc_Air_Vent_35_Intake':{'vertices':v.round(8).tolist(),'triangles':tr.reshape(-1).tolist(),'source_mesh_index':41,'source_node':67,'license':'CC-BY-4.0','whole_source_kit_imported':False}}
def rounded(w,d,r):return box(-w/2+r,-d/2+r,w/2-r,d/2-r).buffer(r,quad_segs=8)
def extruded(poly,front,back):
 vertices=[];tri=[]
 def add_triangle(coords,y,reverse=False):
  start=len(vertices);coords=list(coords)[:3]; vertices.extend([[float(x),y,float(z)] for x,z in coords]);tri.extend([start,start+2,start+1] if reverse else [start,start+1,start+2])
 for t in triangulate(poly):
  if poly.covers(t):
   # CCW in XZ means down normal, suitable for visible underside.
   coords=list(t.exterior.coords)[:3]; cross=(coords[1][0]-coords[0][0])*(coords[2][1]-coords[0][1])-(coords[1][1]-coords[0][1])*(coords[2][0]-coords[0][0])
   if cross<0:coords.reverse()
   add_triangle(coords,front);add_triangle(coords,back,True)
 for ring in [poly.exterior,*poly.interiors]:
  coords=list(ring.coords)
  for a,b in zip(coords,coords[1:]):
   s=len(vertices);vertices.extend([[a[0],front,a[1]],[a[0],back,a[1]],[b[0],back,b[1]],[b[0],front,b[1]]]);tri.extend([s,s+1,s+2,s,s+2,s+3])
 return {'vertices':vertices,'triangles':tri}
outer=rounded(1.20,1.20,.065);central=rounded(.738,.738,.022);slots=[]
for x,z,w,d in [(0,.487,.86,.080),(0,-.487,.86,.080),(.487,0,.080,.86),(-.487,0,.080,.86)]:
 from shapely.affinity import translate
 slots.append(translate(rounded(w,d,.016),xoff=x,yoff=z))
poly=outer.difference(central)
for slot in slots:poly=poly.difference(slot)
lib['Source_Photo_White_Cassette_Face_Real_Five_Openings']=extruded(poly,0,.038)
objects=[]
def ob(name,kind,mat,pos=(0,0,0),size=(1,1,1),euler=(0,0,0),**extra):
 objects.append({'id':name,'kind':kind,'material':mat,'position':list(pos),'scale':list(size),'euler':list(euler),'source_ids':['NORTH_FIELD_jihui__-01.jpg','NORTH_FIELD_wnqkf517-09.jpg'],**extra})
ob('Cassette_White_Face_With_Real_Intake_And_Four_Discharge_Apertures','mesh','warm-white',mesh_key='Source_Photo_White_Cassette_Face_Real_Five_Openings')
ob('Cassette_Upper_Equipment_Casing','box','case-white',(0,.108,0),(1.05,.14,1.05))
ob('Cassette_Dark_Intake_Cavity','box','cavity-black',(0,.036,0),(.735,.014,.735))
ob('Downloaded_Huuxloc_Actual_Intake_Grille_Only','mesh','intake-gray',mesh_key='Licensed_Huuxloc_Air_Vent_35_Intake')
for i,(x,z,yaw) in enumerate([(0,.487,0),(0,-.487,180),(.487,0,90),(-.487,0,270)],1):
 ob(f'Discharge_{i}_Recess_Inner_Surface','box','cavity-black',(x,.028,z),(.852,.014,.076),(0,yaw,0))
 ob(f'Discharge_{i}_White_Adjustable_Louver','box','warm-white',(x,.016,z),(.832,.016,.049),(12,yaw,0))
 for s in [-1,1]:
  # separate small hinge end caps, attached to each observed discharge flap.
  rr=math.radians(yaw);hx=x+math.cos(rr)*.426*s;hz=z-math.sin(rr)*.426*s
  ob(f'Discharge_{i}_Hinge_Cap_{s+1}','box','warm-white',(hx,.016,hz),(.014,.019,.055),(0,yaw,0))
# Current editable tile identities, not the old source drawing's metric placement.
old=json.loads((BASE/'APPLIED-STORE-R25.json').read_text());by={x['id']:x for x in old['objects']}
tiles=[f'Acoustic_Ceiling_Tile_{i}_{j}' for i in [7,8] for j in [4,5]]
packet={'scope':'C180_R26_STORYWAY_NORTH_HVAC_APPLY_001','thread':'classic:/root/r26_storyway_north_hvac','existing_root':old['root_path'],'new_child_group':'R26_SOURCE_PHOTO_FOUR_WAY_CASSETTE_HVAC','asset_folder':'Assets/ChooGuard/Art/StationInterior/R25_Shops/storyway-north/R26_Source_Cassette_HVAC','replace_current_own_tiles':[{'name':n,'object_id':by[n]['object_id']} for n in tiles],'cassette_front_offset_from_current_tile_center_y_m':-.065,'position_qualification':'Current own tile-centroid in observed photo-left half and middle-depth ceiling; source-proportioned MODEL, not a survey or exact SKU. Source left on outward +Z front is +X.','mesh_library':lib,'materials':{'warm-white':{'color':[.87,.86,.80,1],'metallic':.08,'smoothness':.4},'case-white':{'color':[.76,.77,.73,1],'metallic':.12,'smoothness':.32},'cavity-black':{'color':[.027,.031,.032,1],'metallic':.05,'smoothness':.2},'intake-gray':{'color':[.24,.255,.25,1],'metallic':.2,'smoothness':.28}},'objects':objects,'source_attribution':'Backrooms Blender Kit by Huuxloc, CC BY 4.0, https://sketchfab.com/3d-models/backrooms-blender-kit-89fad66d968e44aabedf65ac5a06f877 . Only Air_Vent_35 mesh41 extracted; fit and untextured own photo-color finish adapted. Original entire kit remains outside runtime Assets.','source_glb_sha256':sha(SRC),'source_glb_absolute':str(SRC),'actual_photo_sources':['sources/jihui__-01.jpg','sources/wnqkf517-09.jpg','sources/daytour_88-09.jpg'],'new_Light_Text_TMP':False,'surveyed_dimensions_claimed':False,'all_2F_complete':False}
put('CASSETTE-HVAC-PACKET-R26.json',packet)
put('SOURCE-EVIDENCE-R26.json',{'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'direct_full_image_views':['jihui__-01.jpg','wnqkf517-09.jpg','daytour_88-09.jpg','downloaded kit source-preview.jpg'],'observed':['White square ceiling cassette around two acoustic tiles wide','Dark central rectangular intake and four surrounding narrow discharge slots with white flaps','Cassette on photo-left half of broad north glazed frontage, internal to store'],'source_manifest':'../SOURCE-EVIDENCE-R25.json','downloaded_asset':{'original':str(SRC),'sha256':sha(SRC),'title':'Backrooms Blender Kit','creator':'Huuxloc','license':'CC-BY-4.0','license_url':'https://creativecommons.org/licenses/by/4.0/','source_url':'https://sketchfab.com/3d-models/backrooms-blender-kit-89fad66d968e44aabedf65ac5a06f877','extracted_node':67,'extracted_mesh':41,'original_vertices':len(v),'original_triangles':len(tr),'adaption':'Axis conversion, fit only central grille to photo proportions, own untextured source-color material. No unrelated kit geometry.'},'classification':'DIRECTLY_SUPPORTED_VISIBLE_FORM_WITH_PHOTO_PROPORTIONED_MODEL_FIT','qualification':'No field dimensions/manufacturer exact match/current installation-epoch asserted; same-source branch photos 2023/2024.','physical_photo_only_sign_or_Text_TMP':False})
put('MODEL-PLACEMENT-BASIS-R26.json',{'existing_root':packet['existing_root'],'tile_selection':packet['replace_current_own_tiles'],'current_geometry_rule':'Resolve actual current tile global identities and current transforms in constructor. Their centroid and current rotation locate cassette. Preserve current root and all shell/aisle/native geometry.','source_left_hand_rule':'+X on outward +Z face','nominal_outer_face_m':[1.20,1.20],'visible_underside_offset_from_current_tiles_m':-.065,'own_replaced_tile_count':4,'existing_continuous_upper_ceiling_not_cut':True,'qualification':packet['position_qualification'],'original_source_Assets_modified':False})
print(json.dumps({'packet_sha256':sha(W/'CASSETTE-HVAC-PACKET-R26.json'),'objects':len(objects),'source_grille_vertices':len(v),'source_grille_triangles':len(tr),'own_tile_replacements':tiles}))
