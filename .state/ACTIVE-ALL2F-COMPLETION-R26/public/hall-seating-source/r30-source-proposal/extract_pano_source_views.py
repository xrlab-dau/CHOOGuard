from pathlib import Path
from PIL import Image
import numpy as np,cv2,json
B=Path(__file__).resolve().parent;P=Path('/Users/um-yunsang/CHOOGuard/source-evidence/retained-work-originals/workers/twin/output/architecture-resume-20261010/MAIN1F-native-panorama-reference-20261011/native/google-pano-01-provider-s0.jpg');im=np.array(Image.open(P));H,W=im.shape[:2]
def extract(name,ux,pitch=0,fov=75,size=1400):
 yaw=ux*2*np.pi;p=np.radians(pitch);f=size/2/np.tan(np.radians(fov/2));x,y=np.meshgrid((np.arange(size)-size/2)/f,-(np.arange(size)-size/2)/f);z=np.ones_like(x);yy=y*np.cos(p)+z*np.sin(p);zz=z*np.cos(p)-y*np.sin(p);xx=x*np.cos(yaw)+zz*np.sin(yaw);zf=zz*np.cos(yaw)-x*np.sin(yaw);u=(np.arctan2(xx,zf)%(2*np.pi))/2/np.pi*W;v=(.5-np.arctan2(yy,np.hypot(xx,zf))/np.pi)*H;out=cv2.remap(im,u.astype('f4'),v.astype('f4'),cv2.INTER_CUBIC,borderMode=cv2.BORDER_WRAP);Image.fromarray(out).save(B/name,quality=94)
for name,ux,p in [('PANO01-SOURCE-COLUMN-A.jpg',.155,15),('PANO01-SOURCE-COLUMN-B.jpg',.359,15),('PANO01-SOURCE-COLUMN-C.jpg',.625,15),('PANO01-SOURCE-COLUMN-D.jpg',.910,15),('PANO01-SOURCE-BENCH-FAMILY.jpg',.45,-25),('PANO01-SOURCE-BENCH-FLOOR.jpg',.09,-25)]:extract(name,ux,p)
print('pano',W,H)
