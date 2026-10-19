from pathlib import Path
import cv2,numpy as np,json,sys,hashlib
P=Path(__file__).parent;R=P.parents[2]
def sha(f):return hashlib.sha256(f.read_bytes()).hexdigest()
# Retain exact shared source coordinates used by existing sign prototypes.
red=cv2.imread(str(P/'sources/JUL26-00-original.jpg'));kr=red.shape[1]/966
x0,y0,x1,y1=155,246,237,322
rx0,ry0,rx1,ry1=[round(x*kr) for x in (x0,y0,x1,y1)]
rcrop=red[ry0:ry1,rx0:rx1];h,w=rcrop.shape[:2];gray=cv2.cvtColor(rcrop,cv2.COLOR_BGR2GRAY)
rmask=np.zeros((h,w),np.uint8)
# Source-visible smallest secondary inscription, below the existing mask's y=311 cutoff.
region=(np.indices((h,w))[0] >= round((310.6-y0)*kr)) & (np.indices((h,w))[0] <= round((318.3-y0)*kr)) & (np.indices((h,w))[1] >= round((172-x0)*kr)) & (np.indices((h,w))[1] <= round((220-x0)*kr))
rmask[region & (gray<172)]=255
# No closing or synthetic strokes: retain source punctuation and actual counters.
n,lab,stats,_=cv2.connectedComponentsWithStats(rmask)
for i in range(1,n):
 if stats[i,cv2.CC_STAT_AREA]<3:rmask[lab==i]=0
cv2.imwrite(str(P/'REDBEAN-SUBLINE-ORIGINAL-CROP.png'),rcrop);cv2.imwrite(str(P/'REDBEAN-SUBLINE-MASK.png'),rmask)
# BNC outside-ring lettering. Native photo contains real cut-through dark contours
# and reflective metal edges, so local dark/bright residues are both retained.
bnc=cv2.imread(str(P/'sources/BNC-WEST-OCT2026-original.jpg'));kb=bnc.shape[1]/966
bx0,by0,bx1,by1=[round(x*kb) for x in (471,76,718,331)]
bcrop=bnc[by0:by1,bx0:bx1];g=cv2.cvtColor(bcrop,cv2.COLOR_BGR2GRAY);hh,ww=g.shape
Y,X=np.indices(g.shape);xr=X/kb+471;yr=Y/kb+76
# Source annular letters are isolated from both the yellow inner logo and outer housing seam.
edge=np.float32([(594,80),(555,86),(522,106),(495,137),(479,177),(480,223),(495,263),(524,297),(563,319),(602,327),(644,319),(678,296),(703,258),(713,213),(706,165),(682,123),(646,94)]).reshape(-1,1,2)
(cx,cy),(aw,ah),angle=cv2.fitEllipse(edge)
rad=np.deg2rad(angle);dx=xr-cx;dy=yr-cy;uu=dx*np.cos(rad)+dy*np.sin(rad);vv=-dx*np.sin(rad)+dy*np.cos(rad)
rho=np.sqrt((uu/(aw/2))**2+(vv/(ah/2))**2)
ann=(rho>.70)&(rho<.94)
print('source selection ellipse',cx,cy,aw,ah,angle)
from scipy.ndimage import median_filter
# Angular local background is estimated at each actual source radius. It does
# not sample the luminous inner ring and does not synthesize letter contours.
angles=np.linspace(-np.pi,np.pi,3600,endpoint=False);radii=np.linspace(.68,.96,360)
rr,tt=np.meshgrid(radii,angles,indexing='ij');uu2=np.cos(tt)*rr*(aw/2);vv2=np.sin(tt)*rr*(ah/2)
mx=(cx+uu2*np.cos(rad)-vv2*np.sin(rad)-471)*kb
my=(cy+uu2*np.sin(rad)+vv2*np.cos(rad)-76)*kb
strip=cv2.remap(g,mx.astype('float32'),my.astype('float32'),cv2.INTER_LINEAR)
background=median_filter(strip,size=(1,401),mode='wrap')
diff=background.astype('int16')-strip.astype('int16')
smark=((diff>3)|(diff< -7)).astype('uint8')*255
# Return actual selected source pixels to the unchanged source XY coordinates.
ang=np.arctan2(vv/(ah/2),uu/(aw/2));mx2=(ang+np.pi)/(2*np.pi)*3600;my2=(rho-.68)/(.96-.68)*359
bmask=cv2.remap(smark,mx2.astype('float32'),my2.astype('float32'),cv2.INTER_NEAREST)
bmask[~ann]=0
cv2.imwrite(str(P/'BNC-ANNULAR-SOURCE.png'),strip)
cv2.imwrite(str(P/'BNC-ANNULAR-MASK.png'),smark)
# The top curved glyph is near the source shadow's tonal floor; Otsu within
# its photographed glyph window recovers its ink directly, without a font.
sx0,sy0,sx1,sy1=[round(v*kb) for v in (561-471,92-76,578-471,112-76)]
sroi=g[sy0:sy1,sx0:sx1]
_,ss=cv2.threshold(sroi,0,255,cv2.THRESH_BINARY_INV+cv2.THRESH_OTSU)
nn,ll,st,cent=cv2.connectedComponentsWithStats(ss)
if nn>1:
 target=np.array([(568.9-561)*kb,(102.6-92)*kb]);idx=min([j for j in range(1,nn) if st[j,cv2.CC_STAT_AREA]>100],key=lambda j:np.linalg.norm(cent[j]-target))
 bmask[sy0:sy1,sx0:sx1]=(ll==idx).astype('uint8')*255
# Keep connected source glyphs, reject subpixel JPEG/noise flecks only.
n,lab,stats,centers=cv2.connectedComponentsWithStats(bmask)
for i in range(1,n):
 a0=stats[i,cv2.CC_STAT_AREA];bw=stats[i,cv2.CC_STAT_WIDTH];bh=stats[i,cv2.CC_STAT_HEIGHT]
 ccx,ccy=centers[i];rr0=rho[int(ccy),int(ccx)]
 if a0<55 or a0/(bw*bh)<.13 or max(bw,bh)/min(bw,bh)>4 or (a0<800 and rr0>.90):bmask[lab==i]=0
cv2.imwrite(str(P/'BNC-OUTER-RING-ORIGINAL-CROP.png'),bcrop);cv2.imwrite(str(P/'BNC-OUTER-RING-MASK.png'),bmask)
# Source authoring contact image (evidence only; never imported or placed).
vis=np.concatenate([bcrop,cv2.cvtColor(bmask,cv2.COLOR_GRAY2BGR)],axis=1);cv2.imwrite(str(P/'BNC-SOURCE-CONTOUR-AUTHORING.png'),vis)
print('redbean',rcrop.shape,int(np.count_nonzero(rmask)),'BNC',bcrop.shape,int(np.count_nonzero(bmask)))
