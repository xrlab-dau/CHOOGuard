import cv2,numpy as np,sys,json,hashlib
from pathlib import Path
b=Path(__file__).parent;p=b/'sign-model-authoring';p.mkdir(exist_ok=True)
im=cv2.imread(str(p/'source-logo-crop.png'))
quad=np.float32([[9,47],[387,-2],[385,80],[8,108]])
M=cv2.getPerspectiveTransform(quad,np.float32([[0,0],[1200,0],[1200,250],[0,250]]))
r=cv2.warpPerspective(im,M,(1200,250));cv2.imwrite(str(p/'rectified-source-header.png'),r)
hsv=cv2.cvtColor(r,cv2.COLOR_BGR2HSV)
region=np.zeros((250,1200),np.uint8);region[30:227,220:895]=255
white=cv2.inRange(hsv,np.array([0,0,145]),np.array([179,100,255]))&region
white[:,490:609]=0
green=cv2.inRange(hsv,np.array([30,60,90]),np.array([90,255,255]))&region
green[:,:475]=0;green[:,610:]=0
green=cv2.morphologyEx(green,cv2.MORPH_CLOSE,np.ones((3,3),np.uint8))
red=(cv2.inRange(hsv,np.array([0,70,100]),np.array([25,255,255]))|cv2.inRange(hsv,np.array([165,70,100]),np.array([179,255,255])))&region
for key,mask in [('white',white),('green',green),('red',red)]:
 num,lab,stats,_=cv2.connectedComponentsWithStats(mask);clean=np.zeros_like(mask)
 for i in range(1,num):
  if stats[i,4]>60:clean[lab==i]=255
 cv2.imwrite(str(p/(key+'-mask.png')),clean)
sys.path.insert(0,str(b.parent));from photo_outline_to_solid import closed_solid_from_mask
out={}
# All masks use common originalheader canvas: physical letter proportions retained.
for key in ['white','green','red']:
 mask=cv2.imread(str(p/(key+'-mask.png')),0);out[key]=closed_solid_from_mask(mask,4.15,.865,.035,minimum_area=30,epsilon=.65)
(p/'SOLID-LOGO-MESHES.json').write_text(json.dumps(out,separators=(',',':')))
(p/'SOURCE-SIGN-PROVENANCE.json').write_text(json.dumps({'source':'https://m.blog.naver.com/wondeok9/224257999622','source_local':'sources/ROOT-wondeok9-224257999622-01.jpg','epoch':'2026-04-25','method':'Actual photograph charcoal header rectified only for sign ink extraction, white/green/red threshold contours with holes, closed fronts/backs/sides. No font/TMP/whole-sign image.','outward_normal_d_w':[-1,0],'canvas_size_m':[4.15,.865],'depth_m':.035,'facade_dimensions_MODEL':True},ensure_ascii=False,indent=2))
print({k:(v['source_polygons'],len(v['vertices'])) for k,v in out.items()})
