from pathlib import Path
import importlib.util,json,cv2,numpy as np
P=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/soban');S=P/'quality-staging'
spec=importlib.util.spec_from_file_location('physical_sign',str(P.parent/'photo_outline_to_solid.py'));m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
im=cv2.imread(str(P/'field-20240806-42.jpg'))
out=[]
for name,rect,width,height in [('observed_white_word',(166,359,495,451),2.60,.55),('observed_bowl_icon',(82,371,148,443),.43,.43)]:
 x0,y0,x1,y1=rect;c=im[y0:y1,x0:x1];mask=(np.min(c,axis=2)>165).astype('uint8')*255
 if name=='observed_bowl_icon':mask=(np.min(c,axis=2)>185).astype('uint8')*255
 # Remove only isolated photographic glints; keep every letter counter and separate stroke.
 mask=cv2.morphologyEx(mask,cv2.MORPH_OPEN,np.ones((2,2),np.uint8));cv2.imwrite(str(S/(name+'-outline-mask.png')),mask)
 solid=m.closed_solid_from_mask(mask,width,height,.035 if'word'in name else .012,minimum_area=4,epsilon=.28)
 solid.update(id=name,source_photo='field-20240806-42.jpg',source_crop_pixels=rect,qualification='ACTUAL_FIELD_PHOTO_OUTLINE_DIRECT_CLOSED_EXTRUSION__NO_TMP_NO_RASTER_SIGN_PLANE',width_m=width,height_m=height)
 # A slightly wider recessed perimeter forms the solid letter return/beveled backing edge.
 outer=cv2.dilate(mask,np.ones((3,3),np.uint8),iterations=1)
 bevel=m.closed_solid_from_mask(outer,width,height,.035 if'word'in name else .012,minimum_area=4,epsilon=.28)
 solid['beveled_return']=bevel;out.append(solid)
(S/'PHOTO-TRACED-PHYSICAL-SIGN-SOLIDS.json').write_text(json.dumps(out,ensure_ascii=False));print([(x['id'],x['source_polygons'],len(x['triangles'])//3)for x in out])
