from pathlib import Path
import cv2,numpy as np,json,math,hashlib
import shapely
from shapely.geometry import Polygon,MultiPolygon,GeometryCollection
from shapely.geometry.polygon import orient
from shapely.ops import unary_union,transform
from shapely import constrained_delaunay_triangles
P=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/gijang');srcfile=P/'gijang-front-20240515-original.jpg';im=cv2.imread(str(srcfile));W,H=1800,294;quad=np.float32([[154,494],[582,472],[583,535],[156,557]]);M=cv2.getPerspectiveTransform(quad,np.float32([[0,0],[W,0],[W,H],[0,H]]));warped=cv2.warpPerspective(im,M,(W,H),flags=cv2.INTER_CUBIC);hsv=cv2.cvtColor(warped,cv2.COLOR_BGR2HSV)
white=((hsv[:,:,2]>164)&(hsv[:,:,1]<78)).astype('uint8')*255;roi=np.zeros_like(white);roi[15:230,320:1225]=255;main=cv2.bitwise_and(white,roi)
red=(((hsv[:,:,0]<12)|(hsv[:,:,0]>168))&(hsv[:,:,1]>125)&(hsv[:,:,2]>105)).astype('uint8')*255;roi[:]=0;roi[20:220,1200:1390]=255;red=cv2.bitwise_and(red,roi);red=cv2.morphologyEx(red,cv2.MORPH_CLOSE,np.ones((3,3),np.uint8));rc,_=cv2.findContours(red,cv2.RETR_EXTERNAL,cv2.CHAIN_APPROX_SIMPLE);seal=max(rc,key=cv2.contourArea);sealMask=np.zeros_like(red);cv2.drawContours(sealMask,[seal],-1,255,-1);sealWhite=cv2.bitwise_and(white,sealMask)
cv2.imwrite(str(P/'source-sign-white-mask-006.png'),main);cv2.imwrite(str(P/'source-seal-white-mask-006.png'),sealWhite);cv2.imwrite(str(P/'source-seal-outer-mask-006.png'),sealMask)
def cv_polys(mask,minarea=9):
 contours,hierarchy=cv2.findContours(mask,cv2.RETR_TREE,cv2.CHAIN_APPROX_SIMPLE);out=[]
 if hierarchy is None:return out
 hh=hierarchy[0]
 for i,c in enumerate(contours):
  depth=0;pa=hh[i][3]
  while pa>=0:depth+=1;pa=hh[pa][3]
  if depth%2 or cv2.contourArea(c)<minarea:continue
  cc=cv2.approxPolyDP(c,.32,True).reshape(-1,2)
  if len(cc)<3:continue
  holes=[];child=hh[i][2]
  while child>=0:
   hc=cv2.approxPolyDP(contours[child],.25,True).reshape(-1,2)
   if len(hc)>=3 and cv2.contourArea(contours[child])>3:holes.append(hc)
   child=hh[child][0]
  g=shapely.make_valid(Polygon(cc,holes))
  if isinstance(g,Polygon):out.append(g)
  elif isinstance(g,MultiPolygon):out.extend(g.geoms)
 return out
mainpolys=cv_polys(main);sealpolys=cv_polys(sealWhite,4);plate=Polygon(cv2.approxPolyDP(seal,.65,True).reshape(-1,2));centers=[405,515,677,800,956,1071,1180];groups=[[]for _ in centers]
for po in mainpolys:groups[min(range(len(centers)),key=lambda k:abs(po.centroid.x-centers[k]))].append(po)
# Actual currently approved fascia dimensions; source image identifies contours, not font glyphs.
base=json.load(open(P/'partplacement.json'));lookup={o['id']:o for o in base['objects']};fascia=lookup['charcoal_fascia'];length=float(fascia['size'][0]);height=float(fascia['size'][1]);bottom=float(fascia['y'])-height/2;origin=list(fascia['q']);basis=json.load(open(P/'MODEL-PLACEMENT-BASIS-R25.json'));T=basis['frontage_tangent'];N=basis['interior_normal'];origin=[origin[i]+N[i]*(.035-.095)for i in range(2)];FY=-math.degrees(math.atan2(T[1],T[0]));records=[]
def metric(g):return transform(lambda x,y,z=None:((.5-np.asarray(x)/W)*length,bottom+(1-np.asarray(y)/H)*height),g)
def parts(g):
 if isinstance(g,Polygon):return[g]
 if isinstance(g,(MultiPolygon,GeometryCollection)):return[x for z in g.geoms for x in parts(z)]
 return[]
def solid(id,gs,depth,bevel,base_z,material):
 vs=[];tris=[]
 def face(vv,front=True):
  i=len(vs);vs.extend(vv)
  for k in range(1,len(vv)-1):tris.extend([i,i+k,i+k+1]if front else[i,i+k+1,i+k])
 polycount=0;holecount=0
 for gg in gs:
  for g in parts(metric(gg)):
   g=orient(g,1);polycount+=1;holecount+=len(g.interiors)
   inset=g.buffer(-bevel,join_style='round',quad_segs=2)
   if inset.is_empty:inset=g;localbevel=0
   else:localbevel=bevel
   # Frontcap, constrained triangulation keeps all intentional counters/holes.
   for tr in constrained_delaunay_triangles(inset).geoms:
    xy=list(orient(tr,1).exterior.coords)[:-1];face([[float(x),float(y),base_z+depth]for x,y in xy])
   for tr in constrained_delaunay_triangles(g).geoms:
    xy=list(orient(tr,1).exterior.coords)[:-1];face([[float(x),float(y),base_z]for x,y in xy],False)
   if localbevel:
    band=g.difference(inset)
    for tr in constrained_delaunay_triangles(band).geoms:
     xy=list(orient(tr,1).exterior.coords)[:-1];vv=[]
     for x,y in xy:
      dist=g.boundary.distance(shapely.Point(x,y));z=base_z+depth-localbevel+min(dist/localbevel,1)*localbevel;vv.append([float(x),float(y),float(z)])
     face(vv)
   for ring in [g.exterior,*g.interiors]:
    xy=list(ring.coords)
    for i in range(len(xy)-1):
     a,b=xy[i],xy[i+1];face([[float(a[0]),float(a[1]),base_z],[float(b[0]),float(b[1]),base_z],[float(b[0]),float(b[1]),base_z+depth-localbevel],[float(a[0]),float(a[1]),base_z+depth-localbevel]])
 records.append({'id':id,'kind':'mesh','q':origin,'y':0,'yaw':FY,'vertices':vs,'triangles':tris,'material':material,'source_contour_polygon_count':polycount,'source_counter_holes_preserved':holecount,'thickness_MODEL_m':depth,'bevel_MODEL_m':bevel,'source_pixel_derived':True,'font_used':False,'TMP_used':False,'photo_baked_to_letter_plane':False})
for i,gs in enumerate(groups):
 if gs:solid('GIJANG_SOURCE_PHOTO_BRUSH_GLYPH_'+str(i+1).zfill(2),gs,.024,.0018,0,'warm_white_enamel')
solid('GIJANG_SOURCE_PHOTO_RED_SEAL_SOLID',[plate],.009,.0012,0,'red_seal_enamel')
solid('GIJANG_SOURCE_PHOTO_WHITE_SEAL_MARKS',sealpolys,.0018,.00035,.009,'warm_white_enamel')
out={'schema':'chooguard.gijang.photo-contour-signsolid006','store_key':'gijang','source_path':str(srcfile),'source_url':'https://m.blog.naver.com/oyj10244/223446992106','source_epoch':'2024-05-15','source_sha256':hashlib.sha256(srcfile.read_bytes()).hexdigest(),'method':'ActualphotoHSVwhitemaskandredseal→hierarchycontours→even/oddholes→metricvectorpolygon→constrainedDelaunayfront/backcap+closedsidewalls+geometricbevel','source_plane_quad_pixels':quad.tolist(),'source_to_rectified_matrix':M.tolist(),'rectified_pixels':[W,H],'geometry_not_font':True,'no_TMP_or_font_bake':True,'remove_exact_own_names':['primary_shop_name','red_badge_letters','red_seal_badge'],'existing_fascia_glazing_lighting_unchanged':True,'approved_region_native_floor_columns_other_stores_unchanged':True,'objects':records,'source_letter_count':7,'source_closed_hole_count':sum(r['source_counter_holes_preserved']for r in records),'source_quality_limit':'Contour detail bounded by actual743x991historicalphoto; raiseddepth/bevelMODEL, notfieldmeasurement; no99percentclaim','Scene_QA_Play_reopen_capture':False}
(P/'photo-contour-sign006.json').write_text(json.dumps(out,ensure_ascii=False,indent=2));print('source_polys',len(mainpolys),'whitebadge_polys',len(sealpolys),'meshes',len(records),'holes',out['source_closed_hole_count'],'vertices',sum(len(x['vertices'])for x in records),'triangles',sum(len(x['triangles'])//3for x in records),'SHA',hashlib.sha256((P/'photo-contour-sign006.json').read_bytes()).hexdigest())
