from pathlib import Path
import json,sys,hashlib,cv2,numpy as np
P=Path(__file__).resolve().parent;ROOT=P.parents[2]
sys.path.insert(0,str(ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25'))
from photo_outline_to_solid import closed_solid_from_mask
sha=lambda f:hashlib.sha256(Path(f).read_bytes()).hexdigest()
rm=cv2.imread(str(P/'REDBEAN-SUBLINE-MASK.png'),0)
red=closed_solid_from_mask(rm,.50,.46,.0007,minimum_area=1.5,epsilon=.20)
bm=cv2.imread(str(P/'BNC-OUTER-RING-MASK.png'),0)
bnc=closed_solid_from_mask(bm,.64*247/165,.64*255/171,.0007,minimum_area=15,epsilon=.65)
for v in bnc['vertices']:v[0]+=(601.5-594.5)/165*.64;v[1]+=(201.5-203.5)/171*.64
specs=[dict(key='redbean_secondary_subline',parent_id='GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-1123713738-0',parent_name='SourceTaesungdangMaroonLogo',root_path='FPSWorld/Architecture/2F/MAIN2F_R25_SOURCE_BOUND_SPECIALIST_SHOPS/R25_redbean-bread_SOURCE_BOUND_MODEL',child_name='R26_Actual_Photo_Smallest_Secondary_Subline',local_position=[0,0,.0009],mesh=red,color=[.235,.155,.235,1],metallic=0,smoothness=.25,source_id='JUL26-00-ORIGINAL',source_mask='REDBEAN-SUBLINE-MASK.png',source_crop_ref=[155,246,237,322],source_normal='+Z, image-left maps local+X; no typed glyph substitution'),dict(key='bnc_west_outer_ring_inscription',parent_id='GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-876467796-0',parent_name='Round_Blade_Brass_Housing',root_path='FPSWorld/Architecture/2F/MAIN2F_R25_SOURCE_BOUND_SPECIALIST_SHOPS/R25_bnc-west_SOURCE_BOUND_MODEL',child_name='R26_Actual_Photo_Outside_Ring_Inscription',local_position=[0,0,.0009],mesh=bnc,color=[.009,.007,.0045,1],metallic=.12,smoothness=.30,source_id='WEST_CURRENT_OCT2026-ORIGINAL',source_mask='BNC-OUTER-RING-MASK.png',source_crop_ref=[471,76,718,331],source_normal='+Z, source coordinates mapped to existing blade prototype; same current housing parent, not original global pose')]
sources=[]
for f in [P/'sources/JUL26-00-original.jpg',P/'sources/BNC-WEST-OCT2026-original.jpg',P/'REDBEAN-SUBLINE-MASK.png',P/'BNC-OUTER-RING-MASK.png']:sources.append(dict(path=str(f),sha256=sha(f)))
packet=dict(scope='C180_R26_SMALL_SOURCE_SIGNS_001',author='classic:/root/r26_small_source_signs',asset_folder='Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/SmallSourceSigns',source_files=sources,objects=specs,method='Native photograph pixel ink masks -> closed front/back/side contour solids with actual counters, no font/OCR/text or photo rendering plane',qualification='PHOTO_REFERENCED_MODEL_DIMENSIONS_NOT_SURVEYED',existing_parent_transform_material_geometry_changed=False,Light_Text_TMP_photo_sign_planes_created=False)
(P/'SOURCE-SOLID-PACKET-R26.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
print('packet sha',sha(P/'SOURCE-SOLID-PACKET-R26.json'))
for x in specs:print(x['key'],x['mesh']['source_polygons'],'contours',len(x['mesh']['vertices']),'vertices',len(x['mesh']['triangles'])//3,'triangles')
(P/'MODEL-PLACEMENT-BASIS-R26.json').write_text(json.dumps({'method':'Exact existing current sign parents by persisted GlobalObjectId; source-photo XY coordinates map to existing prototype dimensions. Local child attachment follows saved redbean002 wall-contact rigid translation without resetting it. No global coordinates reapplied.','objects':[{k:v for k,v in x.items() if k!='mesh'} for x in specs],'whole2F_or_surveyed_equality_claim':False},ensure_ascii=False,indent=2))
