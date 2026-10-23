import json,cv2,numpy as np
from pathlib import Path
from shapely.geometry import Polygon
from shapely.geometry.polygon import orient
from shapely.ops import triangulate
from PIL import Image,ImageDraw
R=Path('/Users/um-yunsang/CHOOGuard');D=R/'.state/ACTIVE-1F-STORE-SIGN-GRAPHICS-20261019';B=R/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25'
O=D/'prepared-art';O.mkdir(exist_ok=True)
arts={};previews=[]
def read(path):return cv2.cvtColor(cv2.imread(str(path)),cv2.COLOR_BGR2RGB)
def crop(path,box):
 im=read(path);x,y,x2,y2=box;return im[y:y2,x:x2]
def warp(path,points,w,h):
 im=read(path);m=cv2.getPerspectiveTransform(np.float32(points),np.float32([[0,0],[w-1,0],[w-1,h-1],[0,h-1]]));return cv2.warpPerspective(im,m,(w,h))
def mesh(mask,depth=.025):
 mask=(mask.astype(np.uint8)*255);mask=cv2.morphologyEx(mask,cv2.MORPH_CLOSE,np.ones((2,2),np.uint8))
 cs,hier=cv2.findContours(mask,cv2.RETR_CCOMP,cv2.CHAIN_APPROX_SIMPLE)
 verts=[];tris=[];H,W=mask.shape;count=0
 if hier is None:return None
 def xy(pts):return [((float(x)-W/2)/H,(H/2-float(y))/H) for x,y in pts]
 def triangle(a,b,c):
  k=len(verts);verts.extend([a,b,c]);tris.extend([k,k+1,k+2])
 def quad(a,b,c,d):triangle(a,b,c);triangle(a,c,d)
 for i,c in enumerate(cs):
  if hier[0,i,3]!=-1 or cv2.contourArea(c)<2:continue
  ext=cv2.approxPolyDP(c,.36,True).reshape(-1,2);holes=[];child=hier[0,i,2]
  while child!=-1:
   if cv2.contourArea(cs[child])>=1.5:holes.append(xy(cv2.approxPolyDP(cs[child],.32,True).reshape(-1,2)))
   child=hier[0,child,0]
  if len(ext)<3:continue
  poly=Polygon(xy(ext),holes).buffer(0)
  for p in ([poly] if poly.geom_type=='Polygon' else getattr(poly,'geoms',[])):
   if p.area<1e-5:continue
   p=orient(p,sign=1.0);count+=1
   for t in triangulate(p):
    if not p.covers(t):continue
    ps=list(t.exterior.coords)[:3];area=(ps[1][0]-ps[0][0])*(ps[2][1]-ps[0][1])-(ps[1][1]-ps[0][1])*(ps[2][0]-ps[0][0])
    if area<0:ps.reverse()
    triangle([*ps[2],-depth],[*ps[1],-depth],[*ps[0],-depth]);triangle([*ps[0],0],[*ps[1],0],[*ps[2],0])
   for ring in [p.exterior,*p.interiors]:
    ps=list(ring.coords)
    for a,b in zip(ps,ps[1:]):quad([*a,-depth],[*b,-depth],[*b,0],[*a,0])
 return dict(vertices=verts,triangles=tris,contours=count,canvas_aspect=W/H)
def art(name,im,layers,source):
 groups=[];pv=np.zeros((*im.shape[:2],3),np.uint8)+35
 for key,mask,color in layers:
  if mask.sum()<3:raise ValueError('Empty actual-source mask '+name+' '+key)
  data=mesh(mask);data.update(name=key,color=color);groups.append(data)
  cv2.imwrite(str(O/(name+'-'+key+'-mask.png')),mask.astype(np.uint8)*255)
  pv[mask]=tuple(int(color[i:i+2],16) for i in [1,3,5])
 cv2.imwrite(str(O/(name+'-reference.png')),cv2.cvtColor(im,cv2.COLOR_RGB2BGR))
 cv2.imwrite(str(O/(name+'-contours.png')),cv2.cvtColor(pv,cv2.COLOR_RGB2BGR));previews.append((name,Image.fromarray(pv)))
 arts[name]=dict(groups=groups,source=str(source.relative_to(R)),method='Actual photographed silhouette contours with true holes and closed 3D extrusion. No font substitution and no TMP mesh conversion.')
white=lambda im:(im.min(2)>175)&((im.max(2).astype(int)-im.min(2))<75)
src=B/'soban/hayeont_-223374184311-01.jpg';im=crop(src,[344,114,685,204]);art('soban_word',im,[('Actual_Custom_Korean_Letterforms',im.max(2)<85,'#F5F7EA')],src)
im=crop(src,[257,120,324,191]);r,g,b=np.moveaxis(im,2,0).astype(int)
art('soban_bowl',im,[('Actual_White_Bowl_Rice',(r>170)&(g>165)&(b>145)&(im.max(2).astype(int)-im.min(2)<65),'#F3F1DD'),('Actual_Brown_Lid_And_Bowl_Lines',(r>65)&(r<220)&(g<165)&(b<155)&(r>g*1.05),'#9C6D48')],src)
src=B/'gijang/gijang-front-20240515-original.jpg';im=warp(src,[[238,492],[443,483],[447,524],[240,536]],430,92);art('gijang_brush',im,[('Actual_Brush_Characters',white(im),'#F8F6E9')],src)
im=cv2.resize(crop(src,[446,488,474,521]),(112,132),interpolation=cv2.INTER_CUBIC);art('gijang_seal',im,[('Actual_Seal_Calligraphy',white(im),'#FFF7E7')],src)
src=B/'bnc-east/sources/mongahae-224406407523-02.jpg';im=crop(src,[268,181,648,224]);art('bnc_main',im,[('Actual_Bread_Emblem_And_Korean_Logo',white(im),'#FFFEEE')],src)
im=crop(src,[477,181,648,224]);art('bnc_small',im,[('Actual_Custom_Bakery_Logo',white(im),'#202220')],src)
src=B/'bnc-east/sources/mongahae-224406407523-03.jpg';im=crop(src,[548,193,773,439]);im=cv2.resize(im,(256,256),interpolation=cv2.INTER_CUBIC);r,g,b=np.moveaxis(im,2,0).astype(int)
art('bnc_round',im,[('Actual_Split_Gold_Ring_Crossed_Rolling_Pins_BandC',(r>170)&(g>160)&(b<180),'#F5E875')],src)
src=B/'storyway-east/sources/naver-djagytt-20260518-15.jpg';im=warp(src,[[493,253],[580,244],[583,264],[496,275]],348,84);fast=(im.min(2)>100)&((im.max(2).astype(int)-im.min(2))<90);fast[:16]=False;art('fast_food',im,[('Actual_Refrigerator_Header_Letterforms',fast,'#FFF7EB')],src)
src=B/'soban/jin_n_y-223369303888-12.jpg';im=crop(src,[399,131,585,193]);art('tray_return',im,[('Actual_Bowl_Stack_Pictogram_And_Letters',im.max(2)<105,'#171B19')],src)
# Printed informational boards use their actual photo artwork on physical panels.
prints={}
def printed(name,src,points,w,h):
 im=warp(src,points,w,h);cv2.imwrite(str(O/(name+'.png')),cv2.cvtColor(im,cv2.COLOR_RGB2BGR));prints[name]=dict(path=str((O/(name+'.png')).relative_to(R)),source=str(src.relative_to(R)),aspect=w/h,qualification='Actual source photograph graphic, perspective rectification MODEL; retained source epoch/prices, not current-pricing assertion.')
printed('soban_printed_menu',B/'soban/hayeont_-223374184311-01.jpg',[[188,79],[756,92],[724,1130],[201,1138]],680,1320)
printed('soban_kitchen_lightbox',B/'soban/cherry_jaaej-224225306997-09.jpg',[[43,118],[850,85],[884,315],[0,319]],1600,420)
printed('soban_actual_tv',B/'soban/field-20240806-42.jpg',[[528,695],[638,703],[638,779],[528,777]],660,450)
printed('gijang_actual_header',B/'gijang/gijang-front-20240515-original.jpg',[[259,577],[337,576],[337,594],[259,596]],468,108)
story=next(x for x in json.loads((R/'.state/CURRENT-1F-DIRECT-MODELED-SHOP-SIGNS-C164.json').read_text())['stores'] if x['id']=='C160-SHOP-10')
packet=dict(status='READY_DIRECTLY_MODELED_ACTUAL_2F_SIGN_ART',floor='2F',arts=arts,prints=prints,storyway_reuse=story['separate_extruded_parts'],source_mapping='.state/ACTIVE-1F-STORE-SIGN-GRAPHICS-20261019/research/CURRENT-2F-SIGN-SOURCES.json',construction_inventory='.state/ACTIVE-1F-STORE-SIGN-GRAPHICS-20261019/CURRENT-LIVE-SIGN-CONSTRUCTION-STATE.json')
(D/'ACTUAL-2F-SIGN-ART-PACKET.json').write_text(json.dumps(packet,separators=(',',':')))
sheet=Image.new('RGB',(1000,len(previews)*125),(50,50,50));draw=ImageDraw.Draw(sheet)
for j,(name,pv) in enumerate(previews):
 pv.thumbnail((780,110));sheet.paste(pv,(210,j*125+8));draw.text((10,j*125+15),name,fill='white')
sheet.save(O/'SOURCE-CONTOUR-CONSTRUCTION-SHEET.jpg')
print(json.dumps({name:sum(len(g['triangles'])//3 for g in a['groups']) for name,a in arts.items()}))
