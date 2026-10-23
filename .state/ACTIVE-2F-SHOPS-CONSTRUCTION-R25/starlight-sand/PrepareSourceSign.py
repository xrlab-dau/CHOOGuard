import cv2,numpy as np,pathlib,json,sys
p=pathlib.Path(__file__).resolve().parent
sys.path.insert(0,str(p.parent));from photo_outline_to_solid import closed_solid_from_mask
im=cv2.imread(str(p/'sources/ROOT-wcw93-224431893299-00.jpg'))
# Actual frontal source glyphs; small perspective rectification for extraction only.
regions={'korean':(308,215,579,273,2.72,.57),'logo':(239,209,302,276,.63,.67),'english':(588,218,675,274,.86,.56)}
for name,(x0,y0,x1,y1,w,h) in regions.items():
 crop=im[y0:y1,x0:x1];hsv=cv2.cvtColor(crop,cv2.COLOR_BGR2HSV)
 if name=='korean':mask=((hsv[:,:,2]>165)&(hsv[:,:,1]<80)).astype('uint8')*255
 elif name=='logo':mask=((hsv[:,:,2]>180)&(hsv[:,:,1]<110)).astype('uint8')*255
 else:mask=((hsv[:,:,2]>190)&(hsv[:,:,1]<80)).astype('uint8')*255
 cv2.imwrite(str(p/('sources/sign-'+name+'-crop.png')),crop);cv2.imwrite(str(p/('sources/sign-'+name+'-mask.png')),mask)
 solid=closed_solid_from_mask(mask,w,h,.035,minimum_area=3,epsilon=.22);(p/('sign-'+name+'-solid.json')).write_text(json.dumps(solid))
 print(name,solid['source_polygons'],len(solid['vertices']),len(solid['triangles'])//3)
