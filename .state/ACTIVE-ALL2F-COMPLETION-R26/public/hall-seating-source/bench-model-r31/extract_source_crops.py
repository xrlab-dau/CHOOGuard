import json,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import map_coordinates
base=Path(__file__).resolve().parent
repo=next(x for x in base.parents if (x/'AGENTS.md').exists())
src=repo/'source-evidence/retained-work-originals/workers/twin/output/architecture-resume-20261010/MAIN1F-native-panorama-reference-20261011/native/google-pano-01-provider-s0.jpg'
a=np.asarray(Image.open(src).convert('RGB'));h,w=a.shape[:2]
params=[('PANO01-NEAR-BENCH-SOURCE-DETAIL',-21,-35,75),('PANO01-PLAIN-TIMBER-BENCH-SOURCE-DETAIL',-122,-30,74)]
rows=[]
for name,yaw,pitch,fov in params:
 W,H=1600,1150
 x=(np.arange(W)+0.5-W/2)/(W/2)*np.tan(np.radians(fov)/2)
 y=-(np.arange(H)+0.5-H/2)/(W/2)*np.tan(np.radians(fov)/2)
 X,Y=np.meshgrid(x,y);Z=np.ones_like(X)
 pp=np.radians(pitch);yy=np.radians(yaw)
 yp=Y*np.cos(pp)+Z*np.sin(pp);zp=Z*np.cos(pp)-Y*np.sin(pp)
 xp=X*np.cos(yy)+zp*np.sin(yy);zz=zp*np.cos(yy)-X*np.sin(yy)
 norm=np.sqrt(xp*xp+yp*yp+zz*zz)
 u=(np.arctan2(xp,zz)/(2*np.pi)+.5)*w
 v=(.5-np.arcsin(np.clip(yp/norm,-1,1))/np.pi)*h
 out=np.stack([map_coordinates(a[:,:,i],[v,u],order=1,mode='wrap') for i in range(3)],axis=-1).astype(np.uint8)
 f=base/(name+'.jpg');Image.fromarray(out).save(f,quality=96)
 rows.append(dict(file=f.name,yaw_degrees=yaw,pitch_degrees=pitch,horizontal_fov_degrees=fov,size=[W,H],role='readonly panorama perspective source analysis; not scene capture'))
(base/'PANO01-SOURCE-CROP-PROVENANCE.json').write_text(json.dumps(dict(source=str(src.relative_to(repo)),sha256=hashlib.sha256(src.read_bytes()).hexdigest(),original_size=[w,h],capture_epoch='UNKNOWN; do not assert current2026',crops=rows),indent=2))
print(json.dumps(rows))
