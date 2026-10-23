import cv2,sys,json,numpy as np
from pathlib import Path
B=Path(__file__).resolve().parent;sys.path.insert(0,str(B.parent));from photo_outline_to_solid import closed_solid_from_mask
P=B/'sign-model-authoring';P.mkdir(exist_ok=True)
a=cv2.imread(str(P/'fascia-source-rectified.png'));hsv=cv2.cvtColor(a,cv2.COLOR_BGR2HSV)
white=((hsv[:,:,1]<85)&(hsv[:,:,2]>175)).astype('uint8')*255;red=(((hsv[:,:,0]<10)|(hsv[:,:,0]>170))&(hsv[:,:,1]>120)&(hsv[:,:,2]>120)).astype('uint8')*255
lib={};report=[]
for key,mask,depth in [('fascia_white_actual_ink',white,.035),('fascia_red_actual_seal',red,.025)]:
 mask=mask[45:205,550:1020];cv2.imwrite(str(P/(key+'-mask.png')),mask);m=closed_solid_from_mask(mask,2.05,.58,depth,5,.35);lib[key]=m;report.append({'key':key,'triangles':len(m['triangles'])//3,'closed_depth':depth,'source_polygons':m['source_polygons']})
im=cv2.imread(str(B/'sources/ROOT-suin72--224355100225-06.jpg'));src=np.float32([[17,63],[893,226],[892,398],[7,333]]);H=cv2.getPerspectiveTransform(src,np.float32([[0,0],[1200,0],[1200,300],[0,300]]));menu=cv2.warpPerspective(im,H,(1200,300));cv2.imwrite(str(P/'menu-source-rectified.png'),menu)
# Printed source ink becomes actual closed colored solids, no photo mesh/texture/font.
lab=cv2.cvtColor(menu,cv2.COLOR_BGR2LAB);Z=lab.reshape(-1,3).astype('float32');cv2.setRNGSeed(19)
_,labels,centers=cv2.kmeans(Z,10,None,(cv2.TERM_CRITERIA_EPS+cv2.TERM_CRITERIA_MAX_ITER,40,.3),4,cv2.KMEANS_PP_CENTERS);labels=labels.reshape(lab.shape[:2]);palette=cv2.cvtColor(np.uint8(centers).reshape(1,-1,3),cv2.COLOR_LAB2RGB).reshape(-1,3)
colors={}
for i,rgb in enumerate(palette):
 if min(rgb)>155 and max(rgb)-min(rgb)<40:continue
 mask=np.uint8(labels==i)*255
 m=closed_solid_from_mask(mask,3.75,.82,.002,3,.4);key='menu_source_ink_%02d'%i;lib[key]=m;colors[key]=[float(v)/255 for v in rgb];report.append({'key':key,'triangles':len(m['triangles'])//3,'closed_depth':.002,'source_polygons':m['source_polygons']})
(P/'SIGN-MESH-LIBRARY.json').write_text(json.dumps(lib));(P/'SOURCE-INK-COLORS.json').write_text(json.dumps(colors));(P/'SOURCE-SIGN-CONSTRUCTION.json').write_text(json.dumps({'source_fascia':'ROOT-suin72--224355100225-00.jpg','source_menu':'ROOT-suin72--224355100225-06.jpg','methods':'Only sign-plane perspective rectification for image-contour extraction; front/back/sides, real counters. No floorplan/image placement distortion. No text/fonts/photobillboards.','geometry':report},indent=2));print(report)
