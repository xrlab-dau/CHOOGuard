from pathlib import Path
import shutil,json,cv2,numpy as np
P=Path(__file__).resolve().parent;R=P.parent;S=P/'sources';A=S/'construction';A.mkdir(exist_ok=True)
for src,dst in [
(R/'flowers/sources/modern_shelf',A/'modern_shelf'),
(R/'flowers/sources/white_shelf',A/'white_shelf'),
(R/'flowers/sources/wicker_basket_02',A/'wicker'),
(R/'somidam/sources/case',A/'case'),
(R/'starlight-sand/sources/models/counter',A/'counter'),
(R/'starlight-sand/sources/models/financier-own',A/'financier'),
(R/'starlight-sand/sources/models/wrapped-biscuit',A/'wrapped-biscuit'),
(R/'starlight-sand/sources/models/giftbox-own',A/'giftbox'),
(R/'starlight-sand/sources/wood-pbr',A/'wood-pbr'),
(R/'free-assets/pos-terminal',A/'pos'),
(R/'free-assets/commercial-refrigerator',A/'fridge')]:
 if not dst.exists():shutil.copytree(src,dst)
for store in ['flowers','somidam','starlight-sand']:
 shutil.copy2(R/store/'SOURCE-EVIDENCE-R25.json',A/f'SOURCE-{store}.json')
shutil.copy2(R/'storyway-southwest/sources/ROOT-kusunny4514-224353973522-14.jpg',S/'STORYWAY-SW-CURRENT-20260722-14.jpg')
# Actual source letter contour extraction only; no drawing warp.
im=cv2.imread(str(S/'ROOT-dlwngml5351-224330049288-02.jpg'))
quad=np.float32([[127,187],[349,86],[348,145],[127,241]])
M=cv2.getPerspectiveTransform(quad,np.float32([[0,0],[1199,0],[1199,179],[0,179]]))
a=cv2.warpPerspective(im,M,(1200,180));cv2.imwrite(str(S/'sign-fascia-rectified.png'),a)
mask=((a[:,:,0]>165)&(a[:,:,1]>180)&(a[:,:,2]>160)).astype('uint8')*255
mask[:8]=0;mask[-8:]=0;mask[:,:8]=0;mask[:,-8:]=0
cv2.imwrite(str(S/'sign-fascia-ink-mask.png'),mask)
# Own true printed menu surfaces extracted from actual source, not any sign/photoequipment proxy.
im=cv2.imread(str(S/'ROOT-dlwngml5351-224330049288-11.jpg'))
for name,quad in [('menu-walnut',[[97,61],[391,12],[394,421],[97,400]]),('menu-egg',[[393,13],[754,0],[748,449],[394,421]])]:
 m=cv2.getPerspectiveTransform(np.float32(quad),np.float32([[0,0],[599,0],[599,799],[0,799]]));out=cv2.warpPerspective(im,m,(600,800));cv2.imwrite(str(S/f'{name}-print.png'),out)
# Counter end actual red logo geometry.
im=cv2.imread(str(S/'ROOT-wow_09-224194140807-00.jpg'));quad=np.float32([[380,339],[564,355],[567,555],[381,550]])
m=cv2.getPerspectiveTransform(quad,np.float32([[0,0],[399,0],[399,599],[0,599]]));out=cv2.warpPerspective(im,m,(400,600));cv2.imwrite(str(S/'counter-sign-rectified.png'),out)
mask=((out[:,:,2]>100)&(out[:,:,2]>out[:,:,1]*1.35)&(out[:,:,1]<170)).astype('uint8')*255
mask[:15]=0;mask[-15:]=0;mask[:,:10]=0;mask[:,-10:]=0;cv2.imwrite(str(S/'counter-sign-red-mask.png'),mask)
(P/'planning/progress.md').open('a').write('\nStageSources copied immutable downloaded source models/PBR into own staging. Exactfrontia letter and secondarycounter sign masks authored from originalphotos, plus genuine source menu prints. No Scene/Assets mutation.\n')
