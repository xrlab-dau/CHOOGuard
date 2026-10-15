"""Manual source-guide occupation polygons plus current exact recipe coordinates.

Guide polygons are diagrammatic. They never become built walls or a metric map.
"""
from pathlib import Path
import json,hashlib,math
import numpy as np
from PIL import Image,ImageDraw,ImageFont
ROOT=Path('/Users/um-yunsang/CHOOGuard')
OUT=ROOT/'workers/twin/output/floor2-architecture-next-20261009'
EVIDENCE=ROOT/'workers/twin/evidence/floor2-architecture-next-20261009'
ASSET=ROOT/'asset-library/research-public/2026-10-09/floor2-architecture-next/guide03-kgnwrite-005-original.jpg'
PLAN=ROOT/'.planning/2026-09-23-video-twin/majibang-plan.json'
plan=json.loads(PLAN.read_text())
units=plan['secondFloorUnits']
# x/y polygons manually read from a proportional 2048x1153 display of the4000x2252 source.
# Values below preserve source left/right/top/bottom; they are NOT compass or model axes.
names=['국제시장도나스','부산기장특산물','아메리칸트레일러','새부산약국','국제시장단팥빵','하와이안파라다이스','크리스피크림도넛','카카오프렌즈','떡공방형제','파스쿠찌','편의점','위니비니','부산별빛샌드','올리브영','비엔씨','환공어묵','편의점','백미당','꽃들','다인명가','유부주머니 & 부산오뎅','반월당닭강정','청도할매김밥','복순도가','팔공티','남천할매떡볶이','코레일라운지(트리핀)','편의점','소반한식','비엔씨제과','써브웨이','물품보관함']
polygons={
1:[[505,615],[530,606],[550,634],[528,650]],
2:[[369,487],[423,486],[423,516],[369,516]],3:[[369,457],[425,456],[425,486],[369,487]],4:[[369,427],[425,426],[425,455],[369,456]],
5:[[369,264],[422,263],[422,296],[369,296]],6:[[370,233],[467,232],[467,261],[370,261]],
7:[[521,165],[588,165],[588,229],[521,229]],8:[[590,165],[658,164],[658,228],[590,229]],9:[[661,164],[727,163],[727,227],[661,228]],10:[[730,163],[821,162],[821,226],[730,228]],
11:[[768,247],[820,245],[820,296],[768,299]],12:[[1003,308],[1063,305],[1064,337],[1003,340]],
13:[[911,438],[957,437],[959,516],[913,517]],14:[[913,535],[960,534],[964,630],[917,631]],
15:[[763,740],[828,738],[829,804],[765,806]],16:[[832,738],[899,736],[900,802],[833,804]],
17:[[521,897],[598,866],[668,864],[669,918],[517,927]],18:[[671,867],[704,890],[732,917],[672,919]],
19:[[875,896],[939,893],[941,923],[875,927]],20:[[875,936],[926,934],[927,977],[875,980]],21:[[875,982],[927,979],[928,1020],[875,1022]],
22:[[1044,982],[1093,979],[1095,1019],[1046,1022]],23:[[1043,939],[1090,936],[1091,977],[1044,979]],24:[[1031,893],[1094,890],[1095,920],[1032,922]],
25:[[1233,762],[1292,759],[1294,801],[1234,804]],26:[[1360,655],[1427,652],[1429,683],[1360,687]],
27:[[1363,905],[1427,890],[1442,956],[1380,971]],28:[[1436,888],[1513,870],[1534,934],[1453,954]],
29:[[1519,868],[1586,851],[1610,917],[1540,933]],30:[[1591,847],[1658,829],[1689,896],[1616,915]],31:[[1664,827],[1736,807],[1786,854],[1694,894]],
32:[[1368,574],[1516,570],[1517,612],[1369,616]],
}
current_names={1:'국제시장도나스',2:'부산기장특산물',3:'아메리칸트레일러',4:'새부산약국',5:'국제시장단팥빵',6:'하와이안파라다이스',7:'크리스피크림도넛',8:'카카오프렌즈',9:'떡공방명제',10:'CAFFE PASCUCCI',11:'편의점 StoryWay',12:'위니비니',13:'부산별빛샌드',14:'올리브영',15:'비엔씨',16:'환공어묵',17:'편의점  ',18:'백미당',19:'꽃들',20:'다인명가',21:'호두나니&부산오뎅',22:'반월당닭강정',23:'청도할매김밥',24:'복순도가',25:'팔공티',26:'남천할매떡볶이',27:'코레일라운지 편의점',28:'편의점   ',29:'소반한식',30:'비엔씨제과',31:'SUBWAY'}
scale=np.array([4000/2048,2252/1153]);map_origin=np.array([324,154]);map_span=np.array([1544,900])
def normalized(poly):return ((np.array(poly)-map_origin)/map_span).tolist()
def root_uv(poly,frame):
    if frame!='box':return poly
    q=np.deg2rad(plan['boxFrame']['rotDeg']);a=np.array(poly)[:,0];b=np.array(poly)[:,1]
    return np.c_[a*np.cos(q)-b*np.sin(q)+plan['boxFrame']['origin'][0],b*np.cos(q)+a*np.sin(q)+plan['boxFrame']['origin'][1]].tolist()
scene_path=ROOT/'.planning/2026-10-08-interior-runtime-completion/architecture/after-busanok-finishes/current-scene-structure.json'
scene=json.loads(scene_path.read_text());mm={m['id']:m for m in scene['meshes']}
rows=[]
for n in range(1,33):
    current=next((u for u in units if u['name']==current_names.get(n)),None)
    poly=polygons[n];row={'id':f'guide-2f-retail-{n:02d}','guide_number':n,'guide_label':names[n-1],'kind':'locker' if n==32 else 'retail_occupation','source_polygon_original_pixels':(np.array(poly)*scale).tolist(),'source_polygon_normalized':normalized(poly),'geometry_meaning':'colored occupation zone; not built wall','source_digitization_error_pixels':10}
    if current:
        u0,u1=current['u'];v0,v1=current['v'];native=root_uv([[u0,v0],[u1,v0],[u1,v1],[u0,v1]],current.get('frame','root'))
        owners=[r for r in scene['renderers'] if ('Shop_'+current['name']+'_Walls' in r['path'])]
        row['current']={'unit_name':current['name'],'recipe_index':units.index(current),'recipe_frame':current.get('frame','root'),'recipe_u':current['u'],'recipe_v':current['v'],'face':current['face'],'native_root_uv_polygon':native,'native_root_uv_centroid':np.mean(native,axis=0).tolist(),'named_wall_owners':[{'path':r['path'],'global_object_id':r['global_object_id'],'mesh':r['mesh'],'asset':mm[r['mesh']]['asset'],'binary_sha256':mm[r['mesh']]['binary_sha256'],'world_matrix':r['world_matrix']} for r in owners]}
        row['identity_note']='guide and current name differ; alias or dated tenant change requires review' if current['name']!=names[n-1] else 'name matches'
    else:row['current_match_status']='not a secondFloorUnit; facility binding remains to inspect'
    rows.append(row)
facilities=[]
def facility(id,kind,xy,extent=(24,24),meaning='operator diagram icon or labeled occupation; no metric bounds'):
    x,y=xy;w,h=extent;p=[[x-w/2,y-h/2],[x+w/2,y-h/2],[x+w/2,y+h/2],[x-w/2,y+h/2]]
    facilities.append({'id':id,'kind':kind,'source_center_original_pixels':(np.array(xy)*scale).tolist(),'source_center_normalized':normalized([xy])[0],'source_polygon_normalized':normalized(p),'geometry_meaning':meaning,'current_match_status':'map identity established; native owner assignment requires separate architectural control'})
for id,xy in [('tracks-upper-left',(395,374)),('tracks-upper-right',(1088,373)),('tracks-diagonal-left',(450,552)),('tracks-main-up',(1347,716))]:facility(id,'tracks_access_corridor',xy)
for id,xy in [('escalator-upper-left',(390,318)),('escalator-upper-mid',(796,322)),('escalator-main-left',(825,823)),('escalator-main-right',(1510,757))]:facility(id,'escalator',xy)
for id,xy in [('stairs-upper-left',(411,194)),('stairs-left-corridor',(405,708)),('stairs-entry-left',(847,922)),('stairs-entry-right',(1123,913)),('stairs-main-right',(1819,808))]:facility(id,'stairs',xy)
for id,xy in [('elevator-upper-toilet',(999,282)),('elevator-office',(1025,419)),('elevator-diagonal-entry',(495,596))]:facility(id,'elevator_single',xy)
for id,xy in [('elevator-entry-left-pair',(794,935)),('elevator-entry-right-pair',(1190,910))]:facility(id,'elevator_pair',xy,(42,56))
for id,kind,xy in [('wc-upper-female','toilet_female',(1103,196)),('wc-upper-male','toilet_male',(1106,293)),('wc-main-left-male','toilet_male',(465,760)),('wc-main-left-female','toilet_female',(532,837)),('wc-main-right-male','toilet_male',(1820,710)),('wc-main-right-female','toilet_female',(1832,770))]:facility(id,kind,xy)
for id,xy in [('entrance-upper',(878,189)),('entrance-diagonal',(441,664)),('entrance-main-central',(990,934))]:facility(id,'entrance',xy,(60,18))
for id,kind,xy,size in [('station-office','office',(1047,521),(60,115)),('nursery','nursery',(1045,596),(58,24)),('ticket-machines','ticket_machines',(1049,626),(58,22)),('ticket-counter','ticket_counter',(1124,669),(225,45)),('railway-police','railway_police',(1547,659),(216,48)),('information-desk','information_desk',(754,534),(205,205))]:facility(id,kind,xy,size)
edges=[{'a':f'guide-2f-retail-{a:02d}','b':f'guide-2f-retail-{b:02d}','relation':'same strip / consecutive occupation; no metric gap'} for a,b in [(2,3),(3,4),(5,6),(7,8),(8,9),(9,10),(13,14),(15,16),(17,18),(19,20),(20,21),(24,23),(23,22),(27,28),(28,29),(29,30),(30,31)]]
edges += [{'a':'entrance-main-central','b':f'guide-2f-retail-{n:02d}','relation':'same central entry block;19/24hallside;20/21/23/22belowdoorline'} for n in [19,20,21,22,23,24]]
edges += [{'a':'entrance-main-central','b':n,'relation':'EV pair flanks this entry block'} for n in ['elevator-entry-left-pair','elevator-entry-right-pair']]
packet={'schema':'chooguard.whole-2f-guide-semantic-layout.v1','role':'reference_only','source_url':'https://m.blog.naver.com/kgnwrite/224291656711','operator_board_title':'BUSAN STATION CONVENIENCE FACILITIES MAP / 부산역 편의시설 안내도','floor_label':'2F','map_family':'diagrammatic operator facilities/retail guide; not as-built metric architecture','publication_date':'2026-05-20 20:07 Asia/Seoul','capture_epoch':'author previousmonthstatement +20260419filename; no independently validatedEXIF','source_image':str(ASSET.relative_to(ROOT)),'source_sha256':hashlib.sha256(ASSET.read_bytes()).hexdigest(),'source_dimensions':[4000,2252],'source_normalization':{'source_2048_display_bounds':[324,154,1868,1054],'original_pixel_origin':(map_origin*scale).tolist(),'original_pixel_span':(map_span*scale).tolist(),'axes':'source-image left/right and top/bottom; compass not established'},'current_scene_inventory':str(scene_path.relative_to(ROOT)),'current_scene_inventory_sha256':hashlib.sha256(scene_path.read_bytes()).hexdigest(),'current_recipe':str(PLAN.relative_to(ROOT)),'current_recipe_sha256':hashlib.sha256(PLAN.read_bytes()).hexdigest(),'retail_nodes':rows,'facility_nodes':facilities,'semantic_relations':edges,'accepted_global_metric_registration':None,'site_accuracy_verified':False,'independent_field_observations':0,'priority_patch':'final-proposed-bounds.json; four entry-side tenants corrected by guide adjacency and fixed native bays','remaining_gates':['April guide identity differs from October leasing layer in top retail strip; retain epochs','Guide nodes alone cannot establish hidden room depth, wall heights/thickness or paid-access boundary','#13/#14/info native movement remains gated on architectural controls; no uniform warp accepted','Facility owner assignment remains separate; no fabricated native coordinates from icon centers']}
(OUT/'whole-2f-guide-semantic-layout.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2)+'\n')
# Source-backed occupation overlay, every node stays in image space.
im=Image.open(ASSET).convert('RGB').resize((2048,1153));d=ImageDraw.Draw(im)
for n,poly in polygons.items():
    d.line(poly+[poly[0]],fill='#00b7d0' if n not in [20,21,22,23] else '#ff351c',width=3)
im.save(EVIDENCE/'guide-occupation-overlay.png')
# Existing/native footprint map independently displayed, no guide warp.
canvas=Image.new('RGB',(1700,1600),'white');d=ImageDraw.Draw(canvas)
fontpath=ROOT/'Assets/ChooGuard/ThirdParty/Fonts/NotoSansCJKkr-Regular.otf';font=ImageFont.truetype(str(fontpath),17) if fontpath.exists() else ImageFont.load_default()
def px(u,v):return ((95-u)/190*1600+40,(103-v)/170*1450+40)
for u,v in plan['cells']:
    x,y=px(u,v);d.rectangle((x-2,y-2,x+2,y+2),fill='#e5e5e5')
for row in rows:
    if 'current' not in row:continue
    poly=row['current']['native_root_uv_polygon'];n=row['guide_number'];c='#d8402d' if n in [20,21,22,23] else '#263d4f';pts=[px(*uv) for uv in poly];d.line(pts+[pts[0]],fill=c,width=2);u,v=row['current']['native_root_uv_centroid'];d.text(px(u,v),str(n),fill=c,font=font)
proposals=json.loads((OUT/'final-proposed-bounds.json').read_text())
for row in proposals['units']:
    u0,u1=row['proposed_u'];v0,v1=row['proposed_v'];pts=[px(u0,v0),px(u1,v0),px(u1,v1),px(u0,v1)];d.line(pts+[pts[0]],fill='#008b49',width=3);d.text(px((u0+u1)/2,(v0+v1)/2),str(row['guide_number']),fill='#008b49',font=font)
q=plan['secondFloorExtras']['info'];uv=root_uv([[q['u'],q['v']]],q['frame'])[0];x,y=px(*uv);d.ellipse((x-12,y-12,x+12,y+12),outline='blue',width=3);d.text((x+14,y),'INFO',fill='blue',font=font)
d.text((40,1515),'Current recipe/native footprints; numbers reference April2026guide. Red = old entry-side shops; green = bounded candidate. No guide warp.',fill='black',font=font)
canvas.save(EVIDENCE/'whole2f-current-native-footprints.png')
assert len(rows)==32 and len(set(x['guide_number'] for x in rows))==32
assert all(np.isfinite(np.array(x['source_polygon_normalized'])).all() for x in rows)
print(json.dumps({'retail_nodes':len(rows),'facility_nodes':len(facilities),'named_wall_owner_matches':sum(bool(x.get('current',{}).get('named_wall_owners')) for x in rows),'bytes':ASSET.stat().st_size}))
