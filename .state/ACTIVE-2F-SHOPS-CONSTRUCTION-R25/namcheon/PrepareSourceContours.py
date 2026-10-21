import pathlib,cv2,numpy as np,json,sys
p=pathlib.Path(__file__).parent;s=p/'sources';o=s/'contours';o.mkdir(exist_ok=True)
def rect(name,img,pts,size):
 im=cv2.imread(str(s/img));dst=np.float32([[0,0],[size[0]-1,0],[size[0]-1,size[1]-1],[0,size[1]-1]]);r=cv2.warpPerspective(im,cv2.getPerspectiveTransform(np.float32(pts),dst),size);cv2.imwrite(str(o/(name+'.png')),r);return r
r=rect('brand', 'ROOT-wdefs420-224427350117-00.jpg',[[452,45],[787,56],[787,108],[452,96]],(1005,150));hsv=cv2.cvtColor(r,cv2.COLOR_BGR2HSV);mask=np.uint8((hsv[:,:,2]>175)&(hsv[:,:,1]<110))*255;cv2.imwrite(str(o/'brand-white-mask.png'),mask)
r=rect('seal','ROOT-sujin_note-224399487243-05.jpg',[[60,39],[181,81],[177,211],[51,164]],(280,380));hsv=cv2.cvtColor(r,cv2.COLOR_BGR2HSV);red=np.uint8(((hsv[:,:,0]<14)|(hsv[:,:,0]>169))&(hsv[:,:,1]>90)&(hsv[:,:,2]>65))*255;black=np.uint8((hsv[:,:,2]<125)&(hsv[:,:,1]<130))*255
# inside white printed plate only, excluding photo frame/shadow edge
red[:12]=0;red[-12:]=0;red[:,:12]=0;red[:,-12:]=0;black[:14]=0;black[-14:]=0;black[:,:14]=0;black[:,-14:]=0
cv2.imwrite(str(o/'seal-red-mask.png'),red);cv2.imwrite(str(o/'seal-black-mask.png'),black)
r=rect('menu','ROOT-wdefs420-224427350117-01.jpg',[[0,0],[899,0],[899,1199],[0,1199]],(900,1200)) if False else cv2.imread(str(s/'ROOT-wdefs420-224427350117-01.jpg'))
print(r.shape)
