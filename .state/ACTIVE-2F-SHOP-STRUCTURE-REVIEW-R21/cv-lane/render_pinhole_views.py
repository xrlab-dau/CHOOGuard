from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,math
OUT=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOP-STRUCTURE-REVIEW-R21/cv-lane')
rows=json.loads((OUT/'PANORAMA-ORIGINAL-INVENTORY.json').read_text());index=[]
for row in rows:
 p=Path(row['path'])
 if 'google-pano' not in p.name:continue
 source=np.asarray(Image.open(p).convert('RGB'));H,W=source.shape[:2]
 yaws=[-100,-60,-25,10,45,90,135,180] if 'pano-01' in p.name else [-90,-45,0,45,90,135,180]
 for yaw in yaws:
  w,h=1280,960;hfov=78;pitch=-3
  xx,yy=np.meshgrid((np.arange(w)+.5-w/2)/(w/2)*math.tan(math.radians(hfov/2)),-(np.arange(h)+.5-h/2)/(w/2)*math.tan(math.radians(hfov/2)))
  zz=np.ones_like(xx);v=np.stack([xx,yy,zz],axis=-1);v/=np.linalg.norm(v,axis=-1,keepdims=True)
  pi,ya=math.radians(pitch),math.radians(yaw);cp,sp=math.cos(pi),math.sin(pi);cy,sy=math.cos(ya),math.sin(ya)
  rx=v[...,0];ry=cp*v[...,1]+sp*v[...,2];rz=-sp*v[...,1]+cp*v[...,2]
  X=cy*rx+sy*rz;Y=ry;Z=-sy*rx+cy*rz
  theta=np.arctan2(X,Z);phi=np.arcsin(np.clip(Y,-1,1));u=(theta/(2*math.pi)+.5)*W;vv=(.5-phi/math.pi)*H
  ix=np.floor(u).astype(int)%W;iy=np.clip(np.floor(vv).astype(int),0,H-1);im=Image.fromarray(source[iy,ix]);name=f'{p.stem}-yaw{yaw:+04d}-pitch{pitch:+03d}-fov{hfov}.jpg';im.save(OUT/name,quality=92)
  index.append({'derived_path':str((OUT/name).resolve()),'source_sha256':row['sha256'],'source_path':row['path'],'projection':'equirectangular-to-pinhole nearestsample; no scene capture/sourcewarp','yaw_deg_arbitrary_panorama_frame':yaw,'pitch_deg':pitch,'horizontal_FOV_deg':hfov,'width':w,'height':h,'world_rotation_or_position_known':False})
(OUT/'PINHOLE-VIEW-INDEX.json').write_text(json.dumps(index,ensure_ascii=False,indent=2)+'\n')
