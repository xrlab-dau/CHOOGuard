import json,hashlib
from pathlib import Path
import numpy as np,cv2
from PIL import Image
p=Path(__file__).resolve().parent
src=p/'PANO01-PLAIN-TIMBER-BENCH-SOURCE-DETAIL.jpg'
a=np.asarray(Image.open(src).convert('RGB'))
quad=np.float32([[880,424],[1009,447],[998,524],[879,497]])
rect=cv2.warpPerspective(a,cv2.getPerspectiveTransform(quad,np.float32([[0,0],[1024,0],[1024,580],[0,580]])),(1024,580))
# This fixed region encloses the actually visible pedestal KORAIL mark only.
roi=rect[95:280,250:665]
g=cv2.cvtColor(roi,cv2.COLOR_RGB2GRAY)
mask=np.uint8(g<82)*255
n,lab,stats,cents=cv2.connectedComponentsWithStats(mask)
mask=np.uint8(np.isin(lab,[i for i in range(1,n) if stats[i,cv2.CC_STAT_AREA]>45]))*255
ys,xs=np.where(mask>0)
if len(xs)==0: raise RuntimeError('No source lettering contour extracted')
x0,x1,y0,y1=int(xs.min()),int(xs.max()+1),int(ys.min()),int(ys.max()+1)
mask=mask[y0:y1,x0:x1]
Image.fromarray(roi).save(p/'PANO01-KORAIL-RECTIFIED-SOURCE.png')
Image.fromarray(mask).save(p/'PANO01-KORAIL-SOURCE-CONTOUR-MASK.png')
contours,hier=cv2.findContours(mask,cv2.RETR_CCOMP,cv2.CHAIN_APPROX_SIMPLE)
rows=[]
for i,c in enumerate(contours):
 if cv2.contourArea(c)<3:continue
 eps=.38
 c=cv2.approxPolyDP(c,eps,True)[:,0,:]
 if len(c)<3:continue
 rows.append(dict(points_uv=[[(float(x)/(mask.shape[1]-1)),1-float(y)/(mask.shape[0]-1)] for x,y in c],hole=bool(hier[0,i,3]>=0),source_contour_index=i,parent_index=int(hier[0,i,3])))
(p/'KORAIL-SOURCE-CONTOURS-R31.json').write_text(json.dumps(dict(kind='actual source-pixel contours; no font substitution',source_image=src.name,source_image_sha256=hashlib.sha256(src.read_bytes()).hexdigest(),pedestal_face_quad_pixels=quad.tolist(),rectified_face_size=[1024,580],logo_region_pixels=[250,95,665,280],mask_bounds_in_region=[x0,y0,x1,y1],mask_size=[mask.shape[1],mask.shape[0]],contours=rows,threshold_gray_lt=82,contour_simplification_pixels=.38,pixel_qualification='low source resolution and projective dewarp; outline is SOURCE_MODEL, not vector-brand-master equality'),indent=2))
print({'contours':len(rows),'holes':sum(r['hole'] for r in rows),'source_mask_size':mask.shape[::-1]})
