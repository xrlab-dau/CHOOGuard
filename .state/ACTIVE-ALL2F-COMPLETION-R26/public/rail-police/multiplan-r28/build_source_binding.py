import json, hashlib, datetime, math
from pathlib import Path
from PIL import Image, ImageDraw
import pymupdf
R=Path(__file__).resolve().parent
BASE=Path.cwd()
def load(p): return json.loads((BASE/p).read_text())
D=load('.state/ACTIVE-ALL2F-COMPLETION-R26/cad/output-007/CURRENT-LIVE-2F-OBJECT-VECTOR-CAD-R26-NO-TEXT.json')
selected=['1117893400','1528514897','1728101011','989813836','1104302546','1119110515','1331305054','288332569','1460485774','1748468989','1472280575','1711778570']
O={o['object_id'].split('-')[-2]:o for o in D['objects'] if o['object_id'].split('-')[-2] in selected}

def render_cad(name, box, highlight):
 scale=28; w=round((box[2]-box[0])*scale)+40;h=round((box[3]-box[1])*scale)+40
 im=Image.new('RGB',(w,h),'white');dr=ImageDraw.Draw(im)
 def P(q):return(20+(q[0]-box[0])*scale,20+(q[1]-box[1])*scale)
 filtered=0
 for o in D['objects']:
  if any(k in o['path'] for k in ['R26_TRACK9_FOUR_MODERN','R26_EAST_FIVE','SELF_TICKET','SelfTicket','R26_SELF_TICKET']):filtered+=1;continue
  for pol in o.get('actual_band_projected_footprint',[]):
   xy=[(x,-y) for x,y in pol['exterior']]
   if max(p[0] for p in xy)<box[0] or min(p[0] for p in xy)>box[2] or max(p[1] for p in xy)<box[1] or min(p[1] for p in xy)>box[3]:continue
   native='OfficialStation' in o['path'];wall='WALL-FOOTPRINT' in o['path'];col='C177_UPPER' in o['path'] or 'NATIVE_COL' in o['path']
   color='#666666' if native or wall or col else '#cccccc'
   dr.line([P(v) for v in xy],fill=color,width=2 if wall else 1)
   for hole in pol.get('holes',[]):dr.line([P((x,-y)) for x,y in hole],fill=color,width=1)
 for oid in highlight:
  o=O[oid]
  for pol in o.get('actual_band_projected_footprint',[]):dr.line([P((x,-y)) for x,y in pol['exterior']],fill='#ff7d00',width=5)
 im.save(R/name)
 return im
police=render_cad('POLICE-EXISTING-FIRST-FRONT-CANDIDATE-NO-TEXT.png',(37,-18,94,12),['1117893400'])
tvms=render_cad('TWO-TVM-SIDE-FACES-NO-TEXT.png',(-10,-24,40,10),['288332569','1331305054'])
# Clean original acquired directly from current specialist PDF, unchanged elsewhere.
original=Image.open(R/'OFFICIAL2026-EMBED-2.jpeg').convert('RGB')
original4=original.resize((2524,1984));od=ImageDraw.Draw(original4)
# Whole existing east staff frontage family; approximate source identity locator only.
od.line([(501*4,331*4),(527*4,331*4)],fill='#ff7d00',width=8)
original4.crop((280*4,260*4,620*4,365*4)).save(R/'ORIGINAL2F-LOCAL-POLICE-IDENTITY-LOCATOR-NO-TEXT.png')
# Source points are identity anchors. Their stroke or source distance is not a metric CAD target.
originaltv=original.resize((2524,1984));td=ImageDraw.Draw(originaltv)
td.line([(306*4,305*4),(306*4,328*4)],fill='#ff7d00',width=7)
td.line([(412*4,305*4),(412*4,330*4)],fill='#ff7d00',width=7)
originaltv.crop((285*4,270*4,445*4,345*4)).save(R/'ORIGINAL2F-TVM-RETURN-LOCATORS-NO-TEXT.png')
meeting=Image.open(R/'KORAIL-OFFICIAL-MEETING-2F-MAP.jpg').convert('RGB').resize((1624,1140));md=ImageDraw.Draw(meeting);md.rectangle((267*4,53*4,300*4,80*4),outline='#ff7d00',width=7);meeting.save(R/'OFFICIAL-MEETING-POLICE-SEQUENCE-NO-ADDED-TEXT.png')
# Multipanel PDF retains source's existing text, adds no labels, page numbers or legends.
pages=[
 [R/'ORIGINAL2F-LOCAL-POLICE-IDENTITY-LOCATOR-NO-TEXT.png',Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/krispy-southwest/sources/R26-OFFICIAL100083825-MAP-page1.png'),Path('.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/research/root/current-guide-2f-original-20260419.jpg'),R/'POLICE-EXISTING-FIRST-FRONT-CANDIDATE-NO-TEXT.png'],
 [R/'POLICE-EXISTING-FIRST-FRONT-CANDIDATE-NO-TEXT.png'],
 [R/'OFFICIAL-MEETING-POLICE-SEQUENCE-NO-ADDED-TEXT.png',Path('.state/ACTIVE-ALL2F-COMPLETION-R26/public/rail-police/sources/GC042P24075-public-original.jpg')],
 [R/'ORIGINAL2F-TVM-RETURN-LOCATORS-NO-TEXT.png',R/'TWO-TVM-SIDE-FACES-NO-TEXT.png'],
 [R/'SOURCE2022-EAST-ROOMS-9X.png',R/'CLEAN-ORIGINAL-EAST-BAND.png']]
out=pymupdf.open()
for ims in pages:
 p=out.new_page(width=1190,height=842);n=len(ims)
 for i,path in enumerate(ims):
  if n==4:rect=pymupdf.Rect(15+(i%2)*590,15+(i//2)*410,585+(i%2)*590,410+(i//2)*410)
  elif n==2:rect=pymupdf.Rect(20,20+i*410,1170,410+i*410)
  else:rect=pymupdf.Rect(20,20,1170,822)
  p.insert_image(rect,filename=str(path),keep_proportion=True)
out.save(R/'FOUR-PLAN-LOCAL-FACE-MIGRATION-R28-NO-ADDED-TEXT.pdf',deflate=True)
# Save exact current wall geometry, no source-raster metric substitution.
faces=[]
for k,role in [('1117893400','police public-front FIRST EXISTING BAY proposal'),('1528514897','conference201/202 common-access NEXT EXISTING BAY semantic reference'),('1728101011','far east returned-front structural reference; NOT individual202proof'),('288332569','nursery-side Track9 TVM WEST return'),('1331305054','counter end EAST return separate five TVMs')]:
 o=O[k];coords=[xy for p in o['actual_band_projected_footprint'] for xy in p['exterior']];xs,ys=zip(*coords)
 faces.append({'owner_object_id':o['object_id'],'path':o['path'],'role':role,'bounds_actual_qx_qy_m':[min(xs),-max(ys),max(xs),-min(ys)],'footprints_in_actual_CAD_qx_minus_qy':o['actual_band_projected_footprint'],'source_geometry_asset':o['source_mesh_asset'],'Scene_Assets_mutated':False})
(R/'EXACT-CURRENT-HOST-FACES-R28.json').write_text(json.dumps(faces,ensure_ascii=False,indent=2))
print('PDF',R/'FOUR-PLAN-LOCAL-FACE-MIGRATION-R28-NO-ADDED-TEXT.pdf')
print('hostfaces',len(faces))
