"""Trace actual BnC photographic marks into beveled, watertight solid objects."""
import pathlib, sys, json, hashlib
import cv2
import numpy as np
from shapely import constrained_delaunay_triangles
from shapely.affinity import affine_transform
from shapely.geometry.polygon import orient

HERE=pathlib.Path(__file__).resolve().parent
sys.path.insert(0,str(HERE.parents[1]))
from photo_outline_to_solid import masked_polygons

def solid(mask,width,height,depth,bevel,minimum_area=2,epsilon=.18):
    h,w=mask.shape[:2];vertices=[];indices=[];polys=masked_polygons(mask,minimum_area,epsilon)
    def face(tri,zfn,reverse=False):
        xy=list(tri.exterior.coords)[:3]
        if np.cross(np.array(xy[1])-xy[0],np.array(xy[2])-xy[0])<0:xy[1],xy[2]=xy[2],xy[1]
        if reverse:xy.reverse()
        i=len(vertices);vertices.extend([[x,y,zfn(x,y)]for x,y in xy]);indices.extend([i,i+1,i+2])
    holes=0
    for raw in polys:
        p=orient(affine_transform(raw,[-width/w,0,0,-height/h,width/2,height/2]),sign=1)
        holes+=len(p.interiors)
        inset=p.buffer(-bevel,join_style=2)
        if inset.is_empty:inset=p
        for t in constrained_delaunay_triangles(p).geoms:face(t,lambda x,y:-depth,True)
        for t in constrained_delaunay_triangles(inset).geoms:face(t,lambda x,y:bevel)
        rim=p.difference(inset)
        for t in constrained_delaunay_triangles(rim).geoms:
            from shapely.geometry import Point
            face(t,lambda x,y:0 if p.boundary.distance(Point(x,y))<1e-8 else bevel)
        for ring in [p.exterior,*p.interiors]:
            c=list(ring.coords)
            for a,b in zip(c,c[1:]):
                i=len(vertices);vertices.extend([[*a,-depth],[*b,-depth],[*b,0],[*a,0]])
                indices.extend([i,i+1,i+2,i,i+2,i+3])
    return {'vertices':vertices,'triangles':indices,'source_polygons':len(polys),'source_holes':holes,
            'depth_m':depth,'bevel_m':bevel,'front_normal':[0,0,1]}

base=HERE.parent
front=cv2.imread(str(HERE/'front-direct-ink-mask.png'),0)
roundmask=cv2.imread(str(HERE/'round-direct-yellow-mask.png'),0)
ad=cv2.imread(str(HERE/'ad-direct-ink-mask.png'),0)
rim=cv2.imread(str(HERE/'round-rim-actual-letter-mask.png'),0)
work=[]
# Retain the whole source crop coordinates while segmenting meaningful logo/glyph objects.
for key,lo,hi in [('bu',0,32),('san',32,61),('myeong',61,91),('pum',91,120),('ppang',120,151),('jip',151,184),('bread_logo',208,264),('bnc_custom_wordmark',266,378)]:
    m=np.zeros_like(front);m[:,lo:hi]=front[:,lo:hi]
    mesh=solid(m,2.12,.252,.035,.0015)
    work.append(dict(id='PHOTO_SOLID_FRONT_'+key,anchor='front_actual_white_lettering',
        material='sign_white',local_position_delta=[0,0,0],source='mongahae-224406407523-02.jpg',source_crop=[268,180,646,225],**mesh))
work.append(dict(id='PHOTO_SOLID_ROUND_BNC_CROSSED_ROLLING_PINS',anchor='round_actual_BNC_letters',
    material='warm_lit',local_position_delta=[-.042,0,0],source='mongahae-224406407523-03.jpg',source_crop=[547,192,773,440],**solid(roundmask,.60,.60,.018,.0012,3,.22)))
work.append(dict(id='PHOTO_SOLID_ROUND_OUTER_ACTUAL_LETTERS',anchor='round_actual_BNC_letters',
    material='black',local_position_delta=[-.042,0,0],source='mongahae-224406407523-03.jpg',source_crop=[484,137,823,491],**solid(rim,.70,.70,.0008,.00015,3,.20)))
ys,xs=np.where(ad>0);ad=ad[max(0,ys.min()-1):ys.max()+2,max(0,xs.min()-1):xs.max()+2]
work.append(dict(id='PHOTO_SOLID_VERTICAL_DISPLAY_ACTUAL_BRAND',anchor='vertical_ad_actual_brand',
    material='navy',local_position_delta=[-.009,0,0],source='mongahae-224406407523-03.jpg',source_crop=[176,607,249,645],**solid(ad,.45,.105,.0018,.0002,.6,.12)))
packet={'store':'bnc-east','method':'Actualphoto ink/color masks to closed beveled solid; holes retained. No font/TMP generation, no raster plane.',
        'objects':work,'remove_exact_own':['front_actual_white_lettering','round_actual_BNC_letters','vertical_ad_actual_brand']+
            ['sign_bread_emblem_'+part+'_'+str(i)for i in range(3)for part in ['stem','cap']]+
            ['round_BNC_lit_disc','round_BNC_inner_disc'],
        'preserve':'all booth/cabinetry/freefood/PBR/floor/native/otherstoreobjects unchanged',
        'source_photo_sha256':{n:hashlib.sha256((base/'sources'/n).read_bytes()).hexdigest()for n in ['mongahae-224406407523-02.jpg','mongahae-224406407523-03.jpg']}}
(HERE/'PHOTO-SOLID-SIGNS-R25.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2))
print(json.dumps({'solid_objects':len(work),'triangles':sum(len(o['triangles'])//3 for o in work),'holes':sum(o['source_holes']for o in work),'remove':packet['remove_exact_own'],'sha256':hashlib.sha256((HERE/'PHOTO-SOLID-SIGNS-R25.json').read_bytes()).hexdigest()},indent=2))
