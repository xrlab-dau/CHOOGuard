from pathlib import Path
import json, hashlib, re, sys, collections, datetime
import numpy as np, cv2
from PIL import Image
from shapely import constrained_delaunay_triangles

ROOT=Path('/Users/um-yunsang/CHOOGuard')
B=Path(__file__).resolve().parent
C=B/'constructor';C.mkdir(exist_ok=True)
sys.path.insert(0,str(ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25'))
from photo_outline_to_solid import masked_polygons
rows=json.loads((B/'CURRENT-2F-IMAGE-MATERIAL-INVENTORY.json').read_text())
images=json.loads((B/'TEXTURE-CONTACT-INDEX.json').read_text())
# Each selected source was visually inspected. Other images are food, flowers,
# surface grain, normal maps or flat color palettes, not lettering sources.
selected=[0,1,2,3,4,5,6,12,13,14,15,16,17,19,22,24,25,28,29,30,33,45,46,50,51,52,53,61,65,69,70,71,72,73,74,75,76,77,78,79,80,81,82,85,92,96,99,101,116,119,120,148,149,176]
selected_paths={images[i] for i in selected}
scene=(ROOT/'Assets/ChooGuard/Scenes/FpsStation.unity').read_text()
live_ids=set(re.findall(r'^--- !u!1 &(\d+)\s*$',scene,re.M))
materials=[];art_by_sha={};source_table=[];objects={}
def glyph_mask(rgb,index):
    hsv=cv2.cvtColor(rgb,cv2.COLOR_RGB2HSV);sat=hsv[:,:,1];val=hsv[:,:,2]
    h,w=val.shape;mask=np.zeros((h,w),bool)
    def roi(y0=0,y1=1,x0=0,x1=1,mode='both'):
        allowed=np.zeros((h,w),bool);allowed[int(y0*h):int(y1*h),int(x0*w):int(x1*w)]=True
        if mode=='white':candidate=(val>150)&(sat<125)
        elif mode=='dark':candidate=val<135
        elif mode=='blue':candidate=(sat>35)&(hsv[:,:,0]>85)&(hsv[:,:,0]<145)&(val<220)
        elif mode=='light':candidate=val>110
        else:candidate=(val<105)|((val>172)&(sat<115))
        mask[:]|=candidate&allowed
    if index in [0,1,2,3,4,45]:roi()
    elif index in [5,6]:roi(mode='light')
    elif index==12:roi(0,.35,mode='white')
    elif index==13:roi(0,.31,mode='white');roi(.56,.80,mode='white');roi(.79,.98,mode='dark')
    elif index==14:roi(0,.2,mode='white');roi(.88,1,mode='white')
    elif index in [15,16]:roi(0,.23,mode='dark');roi(.77,1)
    elif index in [17,30,61,92,120]:roi(0,.17,mode='blue')
    elif index in [19,25,50,85,96,99,101,119,148,149]:
        roi(.045,.51,.03,.4);roi(.54,.84,0,.43,mode='white')
    elif index in [22,24]:roi(0,.43,mode='dark')
    elif index in [28,29,33,46,53,65]:roi()
    elif index==51:roi(.8,1,mode='white')
    elif index==52:roi(.62,1)
    elif 69<=index<=75:roi(.08,.44,mode='white');roi(.40,.78,mode='dark')
    elif 76<=index<=82:roi(.2,.54,mode='white');roi(.43,.84,mode='dark')
    elif index==116:roi(0,.34,mode='white')
    elif index==176:roi(0,.75,mode='dark')
    count,labels,stats,_=cv2.connectedComponentsWithStats(mask.astype('uint8'),8)
    clean=np.zeros((h,w),np.uint8)
    for i in range(1,count):
        x,y,ww,hh,area=stats[i]
        # Broad panels, food areas and texture background are not letters. Keep
        # the source's small visible strokes, icons and actual counters.
        if area<2 or area>w*h*.075 or hh>h*.42 or ww>w*.89:continue
        clean[labels==i]=255
    return clean
for row in rows:
    tex=next((t for t in row['textures'] if t['property']=='_BaseMap'),None)
    if not tex or tex['path'] not in selected_paths:continue
    live=[o for o in row['objects'] if o['object_id'].split('-')[-2] in live_ids]
    objects[row['material']]=live
    materials.append({'asset':row['material'],'texture':tex['path']})

# The rejected front-bar roots are absent from the live scene. Bind their reused
# materials to the newly saved side-return objects, never replay old placements.
tvm=json.loads((ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/public/track9-ticket-bank/constructor/side-return-r29/APPLIED-BOTH-TVM-SIDE-RETURNS-R29-005.json').read_text())
for bank in tvm['banks']:
    for o in bank['objects']:
        mat=o.get('reused_material_asset')
        if mat in objects and o['object_id'].split('-')[-2] in live_ids:
            objects[mat].append({'object_id':o['object_id'],'path':bank['root_path']+'/'+o['group']+'/'+o['id']})

for row in materials:
    source=ROOT/row['texture'];sha=hashlib.sha256(source.read_bytes()).hexdigest()
    if sha not in art_by_sha:
        im=Image.open(source).convert('RGB')
        # Trace actual visible contours, including counters; no OCR or invented
        # text is used. Color segmentation becomes geometry, not a glyph atlas.
        im.thumbnail((800,800),Image.Resampling.LANCZOS)
        index=images.index(row['texture']);rgb=np.array(im);mask=glyph_mask(rgb,index)
        erase=cv2.dilate(mask,np.ones((3,3),np.uint8))
        clean=cv2.inpaint(rgb,erase,3,cv2.INPAINT_TELEA)
        key=sha[:20];clean_file='LETTER-FREE-SOURCE-SURFACE-'+key+'.png'
        Image.fromarray(clean).save(C/clean_file)
        quant=im.quantize(colors=16,method=Image.Quantize.MEDIANCUT)
        labels=np.array(quant);pal=np.array(quant.getpalette(),np.uint8).reshape(-1,3)
        h,w=labels.shape;layers=[];polygons=0
        for label,count in collections.Counter(labels.ravel()).most_common():
            ink=((labels==label)&(mask>0)).astype('uint8')*255
            pp=masked_polygons(ink,minimum_area=1.1,epsilon=.35)
            triangles=[]
            for poly in pp:
                for t in constrained_delaunay_triangles(poly).geoms:
                    p=np.array(t.exterior.coords[:3],float)
                    p[:,0]/=w;p[:,1]=1-p[:,1]/h
                    if abs(np.cross(p[1]-p[0],p[2]-p[0]))<1e-12:continue
                    triangles.append(np.round(p,8).tolist())
            if triangles:
                layers.append({'rgb':pal[label].tolist(),'triangles_uv':triangles})
                polygons+=len(pp)
        name='SOURCE-CLOSED-COLOR-CONTOURS-'+key+'.json'
        art={'source_texture':row['texture'],'source_sha256':sha,'traced_size':[w,h],
             'method':'Actual source-color contour polygons and constrained triangles; holes retained. UV triangles clipped onto current actual mesh faces and closed extruded. No image/font/text glyph rendering.',
             'letter_free_surface_file':clean_file,'letter_free_surface_sha256':hashlib.sha256((C/clean_file).read_bytes()).hexdigest(),
             'source_blur_not_recovered_or_invented':True,'contour_polygon_count':polygons,'palette':layers}
        (C/name).write_text(json.dumps(art,separators=(',',':')))
        art_by_sha[sha]=name
        source_table.append({'sha256':sha,'file':name,'source':row['texture'],'letter_free_surface_file':clean_file,'letter_free_surface_sha256':hashlib.sha256((C/clean_file).read_bytes()).hexdigest(),'polygon_count':polygons,'triangles':sum(len(x['triangles_uv']) for x in layers),'palette_count':len(layers)})
    row['art_file']=art_by_sha[sha]
    row['targets']=list({o['object_id']:o for o in objects[row['asset']]}.values())

packet={'schema':'chooguard.r29.actual_source_closed_physical_lettering.v1',
        'author':'classic:/root/r29_physical_lettering','scope':'C180_R29_PHYSICAL_LETTERING_SOURCE_CONTOURS_001',
        'authority':'.state/ACTIVE-ALL2F-COMPLETION-R26/USER-PHYSICAL-LETTERING-R29.json',
        'asset_folder':'Assets/ChooGuard/Art/StationInterior/PhysicalLetteringR29',
        'materials':materials,'sources':source_table,
        'source_images_are_readonly_contour_reference_not_visible_letter_planes':True,
        'source_clipped_closed_relief_thickness_m':.00035,
        'qualification':'Source contour MODEL. Blurred or unreadable letters are not semantically invented. No field measurement or whole2F completion assertion.',
        'originals_native_1F_floor_column_openings_geometry_transforms_colliders_unchanged':True,
        'QA_Play_probe_reopen_capture':False}
out=C/'PHYSICAL-LETTERING-PACKET-R29-001.json';out.write_text(json.dumps(packet,separators=(',',':')))
(B/'SOURCE-CONSTRUCTION-SUMMARY.json').write_text(json.dumps({'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sources':source_table,'actual_materials':len(materials),'actual_targets':sum(len(x['targets'])for x in materials),'packet_sha256':hashlib.sha256(out.read_bytes()).hexdigest()},indent=2))
print(json.dumps({'materials':len(materials),'targets':sum(len(x['targets']) for x in materials),'source_shapes':len(source_table),'2D_triangles':sum(s['triangles']for s in source_table),'packet_sha256':hashlib.sha256(out.read_bytes()).hexdigest()}))
