"""Standalone annotated QA plates; source image bytes are never modified."""
exec(compile((__import__('pathlib').Path(__file__).parent/'build_guide_packet.py').read_text(), 'build_guide_packet.py','exec'))
from PIL import ImageDraw,ImageFont
import base64,html
fontpath='/Users/um-yunsang/Library/Fonts/NotoSansCJKkr-Regular.otf'
def font(n):return ImageFont.truetype(fontpath,n)
crop=(220,145,925,395);scale=2
def point(p,offset=0):return ((p[0]-crop[0])*scale+15,(p[1]-crop[1])*scale+65+offset)
def background(title):
 im=Image.new('RGB',(1440,645),'white');im.paste(Image.open(IMAGE).crop(crop).resize((1410,500)),(15,65));ImageDraw.Draw(im).text((15,12),title,font=font(25),fill='#111111');return im
plate=background('1F guide: retail footprints, through passages, circulation cores')
draw=ImageDraw.Draw(plate)
for n,p in units.items():
 pts=[point(x) for x in p];draw.line(pts+[pts[0]],fill='#2171b5',width=3);c=np.mean(p,axis=0);draw.text(point(c),str(n),font=font(18),fill='#084594',stroke_width=1,stroke_fill='white')
for f in features+gaps:
 x0,y0,x1,y1=f['bbox'];color='#e6550d' if f in gaps else '#7a0177';draw.rectangle([point([x0,y0]),point([x1,y1])],outline=color,width=2);draw.text(point([x0,y0-10]),f['id'],font=font(12),fill=color,stroke_width=1,stroke_fill='white')
draw.text((16,577),'16 retail units + locker 17. Purple: circulation/toilet symbols. Orange: guide passages P1–P7.',font=font(19),fill='#111111')
draw.text((16,608),'Author claims 2026-08-11. P7 end-wing access is schematic. Reference layout; physical accuracy unverified.',font=font(17),fill='#555555')
plate.save(OUT/'guideplate-annotated.png')

panels=[]
for i in [0,1]:
 im=background('Current 1F: rear shop row compresses the long public aisle' if not i else 'Proposed 1F: rear shop row restored inside the existing floor/shell')
 overlay=Image.new('RGBA',im.size,(0,0,0,0));dr=ImageDraw.Draw(overlay)
 for u in spec['units']:
  n=u['boardNo'];p=u['polygon']
  if i and n in range(9,16):
   op=next(o for o in proposal['operations'] if o['boardNo']==n);p=op.get('shared_partition_safe_proposed_polygon_UV',op['proposed_polygon_UV'])
  color=((32,160,82) if i else (215,48,39)) if n in range(9,16) else (33,102,172)
  pp=[point(x) for x in to_px(p)];dr.polygon(pp,fill=color+(48,));dr.line(pp+[pp[0]],fill=color+(255,),width=3)
  c=Polygon(p).representative_point();cp=point(to_px([[c.x,c.y]])[0]);dr.text(cp,str(n),font=font(18),fill=color+(255,),stroke_width=1,stroke_fill=(255,255,255,255))
 im=Image.alpha_composite(im.convert('RGBA'),overlay).convert('RGB');dr=ImageDraw.Draw(im)
 for j,p in enumerate(px):
  x,y=point(p)
  if j in holdout:dr.ellipse((x-7,y-7,x+7,y+7),outline='black',width=2)
  else:dr.line([(x-8,y),(x+8,y)],fill='black',width=2);dr.line([(x,y-8),(x,y+8)],fill='black',width=2)
 dr.text((16,576),'Red current / green proposed rear row 9–15. Blue units 1–8 and 16 are preserved.',font=font(18),fill='#111111')
 dr.text((16,608),'Approximate affine guide scaffold: held-out anchor residuals 2.69 / 3.41 model units. No metric/survey claim.',font=font(17),fill='#555555')
 panels.append(im)
combined=Image.new('RGB',(1440,1290),'white');combined.paste(panels[0],(0,0));combined.paste(panels[1],(0,645));combined.save(OUT/'layout-diff-annotated.png')

# Native coordinate plot has no pixel-registration dependency.
secs=json.loads((ROOT/'.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/floor1-actual-segments.json').read_text())
public=json.loads((ROOT/'.planning/2026-09-24-interior-twin/specs/full/public-1f.json').read_text())
def nativept(p):return (30+(78-p[0])*8,70+(14-p[1])*8)
nativepanels=[]
for i in [0,1]:
 im=Image.new('RGB',(1440,640),'white');dr=ImageDraw.Draw(im);dr.text((15,12),'Current 1F footprints and MainShell Y=1.5 section' if not i else 'Proposed 1F footprints: floor support and protected cores preserved',font=font(25),fill='#111111')
 for o in secs['owners']:
  if '/MainShell/' in o['path']:
   for s in o['segments']:dr.line([nativept(x) for x in s],fill='#444444',width=1)
 overlay=Image.new('RGBA',im.size,(0,0,0,0));od=ImageDraw.Draw(overlay)
 for u in spec['units']:
  n=u['boardNo'];p=u['polygon']
  if i and n in range(9,16):
   op=next(o for o in proposal['operations'] if o['boardNo']==n);p=op.get('shared_partition_safe_proposed_polygon_UV',op['proposed_polygon_UV'])
  color=((32,160,82) if i else (215,48,39)) if n in range(9,16) else (33,102,172)
  pts=[nativept(x) for x in p];od.polygon(pts,fill=color+(48,));od.line(pts+[pts[0]],fill=color+(255,),width=2)
  c=Polygon(p).representative_point();od.text(nativept([c.x,c.y]),str(n),font=font(18),fill=color+(255,))
 for e in public['elements']:
  if 'polygon' in e.get('geometry',{}):
   pp=[nativept(x) for x in e['geometry']['polygon']];od.line(pp+[pp[0]],fill=(122,1,119,255),width=2)
  if e['id']=='1Fp-boh-uminus-5479':
   pp=e['geometry']['polyline'][:]
   if i:pp[-1]=[-42.6,-9.378]
   od.line([nativept(x) for x in pp],fill=(224,130,20,255),width=4)
 im=Image.alpha_composite(im.convert('RGBA'),overlay).convert('RGB');dr=ImageDraw.Draw(im)
 dr.text((15,578),'Black original MainShell; purple protected Room/ToiletRoom; orange BOH cladding. Coordinates: existing station UV.',font=font(17),fill='#333333')
 dr.text((15,606),'Proposed 9–15: zero unsupported floor area, zero protected-core overlap, zero MainShell long-wall crossings.',font=font(17),fill='#333333');nativepanels.append(im)
combined=Image.new('RGB',(1440,1280),'white');combined.paste(nativepanels[0],(0,0));combined.paste(nativepanels[1],(0,640));combined.save(OUT/'native-containment-before-proposed.png')

# Editable vector plate retains the exact native source photograph via embedded data URI.
svg=['<svg xmlns="http://www.w3.org/2000/svg" width="966" height="725" viewBox="0 0 966 725">',
 '<image href="data:image/jpeg;base64,'+base64.b64encode(IMAGE.read_bytes()).decode()+'" x="0" y="0" width="966" height="725"/>']
for n,p in units.items():svg.append('<polygon points="'+' '.join(f'{x},{y}' for x,y in p)+'" fill="none" stroke="#2171b5" stroke-width="1.5" data-board-no="'+str(n)+'"/>')
for f in features+gaps:
 x0,y0,x1,y1=f['bbox'];color='#e6550d' if f in gaps else '#7a0177';svg.append(f'<rect x="{x0}" y="{y0}" width="{x1-x0}" height="{y1-y0}" fill="none" stroke="{color}" data-id="{html.escape(f["id"])}"/>')
svg.append('</svg>');(OUT/'guideplate-editable.svg').write_text('\n'.join(svg))
print('Created guideplate, layoutdiff, native containment PNGs and editable SVG.')
