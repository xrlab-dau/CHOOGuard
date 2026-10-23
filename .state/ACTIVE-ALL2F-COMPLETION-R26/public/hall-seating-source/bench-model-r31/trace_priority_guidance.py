import json,hashlib
from pathlib import Path
import numpy as np,cv2
from PIL import Image
p=Path(__file__).resolve().parent
layers=[]
for name,source,roi,threshold,surface in [
 ('FrontMainPriorityLegend','PRIORITY-FRONT-FACE-RECTIFIED-SOURCE.png',[750,6,1550,80],55,'front_vertical'),
 ('TopNearLeftPriorityLegend','PRIORITY-TOP-DECK-RECTIFIED-SOURCE.png',[475,0,735,83],115,'top'),
 ('TopNearRightPriorityLegend','PRIORITY-TOP-DECK-RECTIFIED-SOURCE.png',[1210,0,1490,83],115,'top'),
]:
 a=np.asarray(Image.open(p/source).convert('RGB'));x0,y0,x1,y1=roi
 region=a[y0:y1,x0:x1];gray=cv2.cvtColor(region,cv2.COLOR_RGB2GRAY)
 mask=np.uint8(gray>threshold)*255
 n,labels,stats,_=cv2.connectedComponentsWithStats(mask)
 mask=np.uint8(np.isin(labels,[i for i in range(1,n) if stats[i,cv2.CC_STAT_AREA]>=4 and not (stats[i,cv2.CC_STAT_WIDTH]>.85*region.shape[1] and stats[i,cv2.CC_STAT_HEIGHT]<.25*region.shape[0])]))*255
 contours,hier=cv2.findContours(mask,cv2.RETR_CCOMP,cv2.CHAIN_APPROX_SIMPLE)
 rows=[]
 for i,c in enumerate(contours):
  if abs(cv2.contourArea(c))<1:continue
  c=cv2.approxPolyDP(c,.25,True)[:,0,:]
  if len(c)<3:continue
  rows.append(dict(points_source_pixels=[[float(x+x0),float(y+y0)] for x,y in c],hole=bool(hier[0,i,3]>=0)))
 Image.fromarray(region).save(p/(name+'-SOURCE-ROI.png'))
 Image.fromarray(mask).save(p/(name+'-SOURCE-CONTOUR-MASK.png'))
 layers.append(dict(name=name,source=source,sha256=hashlib.sha256((p/source).read_bytes()).hexdigest(),roi=roi,threshold_gray_gt=threshold,surface=surface,source_size=list(a.shape[1::-1]),contours=rows))
(p/'PRIORITY-SOURCE-CONTOURS-R31.json').write_text(json.dumps(dict(kind='readable actual priority-bank source white-glyph silhouettes; no font replacement',layers=layers,qualification='Projectively rectified low-resolution source contours. No invisible far vertical face lettering or hidden duplicate is invented.'),indent=2))
print([{ 'name':l['name'],'contours':len(l['contours']),'holes':sum(x['hole'] for x in l['contours'])} for l in layers])
