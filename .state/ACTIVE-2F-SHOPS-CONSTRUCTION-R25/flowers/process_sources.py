import cv2,numpy as np,json,sys
from pathlib import Path
R=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/flowers');S=R/'sources'
img=cv2.imread(str(S/'RYOKOU-20240315-FLOWERS-FRONT.jpg'))
src=np.float32([[1246,538],[1588,592],[1590,714],[1246,666]])
dst=np.float32([[0,0],[820,0],[820,300],[0,300]])
r=cv2.warpPerspective(img,cv2.getPerspectiveTransform(src,dst),(820,300));cv2.imwrite(str(S/'SIGN-RECTIFIED-SOURCE.png'),r)
gray=cv2.cvtColor(r,cv2.COLOR_BGR2GRAY);mask=np.where(gray>223,255,0).astype('uint8');mask[:,:245]=0
# Remove unrelated edges; retain actual letters and holes.
mask[:12,:]=0;mask[-12:,:]=0;mask[:,:260]=0;mask[:,805:]=0
cv2.imwrite(str(S/'SIGN-LETTER-INK-MASK.png'),mask)
# Source flower emblem white silhouette, plaque boundary excluded.
icon=np.zeros_like(mask);hsv=cv2.cvtColor(r,cv2.COLOR_BGR2HSV);icon[35:250,55:210]=np.where((hsv[35:250,55:210,1]<90)&(gray[35:250,55:210]>224),255,0)
cv2.imwrite(str(S/'SIGN-ICON-INK-MASK.png'),icon)
# Source-owned textures; no visible photo planes.
