from pathlib import Path
import cv2,numpy as np,importlib.util,json
b=Path(__file__).parent;p=b/'sign-model-authoring';p.mkdir(exist_ok=True)
im=cv2.imread(str(b/'sources/ellie-28.jpg'))
# Current field photo is near frontal; crop actual raised source lettering.
logo=im[64:143,405:718];white=((logo.min(2)>160)&((logo.max(2)-logo.min(2))<75)).astype('uint8')*255
white=cv2.morphologyEx(white,cv2.MORPH_CLOSE,np.ones((2,2),'uint8'))
cross=im[64:143,331:402];hsv=cv2.cvtColor(cross,cv2.COLOR_BGR2HSV);red=(((hsv[:,:,0]<10)|(hsv[:,:,0]>170))&(hsv[:,:,1]>110)&(hsv[:,:,2]>80)).astype('uint8')*255
# Fill only observed outer cross silhouette; white inset is independent physical emitter.
cnt,_=cv2.findContours(red,cv2.RETR_EXTERNAL,cv2.CHAIN_APPROX_SIMPLE);solid=np.zeros_like(red)
for c in cnt:
 if cv2.contourArea(c)>100:cv2.drawContours(solid,[c],-1,255,-1)
inner=((cross.min(2)>175)&((cross.max(2)-cross.min(2))<65)).astype('uint8')*255
for n,a in [('source-letter-crop',logo),('letters-mask',white),('red-cross-mask',solid),('white-cross-mask',inner)]:cv2.imwrite(str(p/(n+'.png')),a)
s=importlib.util.spec_from_file_location('outline',b.parent/'photo_outline_to_solid.py');m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
meshes={'brand':m.closed_solid_from_mask(white,2.50,.58,.045,minimum_area=12,epsilon=.3),'cross_red':m.closed_solid_from_mask(solid,.56,.58,.06,minimum_area=12,epsilon=.2),'cross_white':m.closed_solid_from_mask(inner,.56,.58,.015,minimum_area=12,epsilon=.2)}
(p/'SOURCE-TRACED-SIGN-SOLIDS.json').write_text(json.dumps(meshes))
(p/'SOURCE-SIGN-PROVENANCE.json').write_text(json.dumps({'source':'https://m.blog.naver.com/ellie0116/224433385586','photo_local':str(b/'sources/ellie-28.jpg'),'photo_epoch':'2026-10-06','crop_letters_xyxy':[405,64,718,143],'crop_cross_xyxy':[331,64,402,143],'method':'Actual source pixel threshold outlines, holes retained, closed front/back/side solids; offline bevel on welded mesh. No font, TMP, texture plane or replacement text.','size_status':'MODEL photo visible proportional estimate; no field measurement'},indent=2))
print({k:(len(v['vertices']),v['source_polygons']) for k,v in meshes.items()})
