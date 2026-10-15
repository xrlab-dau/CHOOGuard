"""Standalone bounded Unit8 source/native diagram. Read-only production."""
from pathlib import Path
import json,struct
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from shapely.geometry import Polygon,LineString,shape,box
from shapely.ops import unary_union
ROOT=Path('/Users/um-yunsang/CHOOGuard');BASE=ROOT/'workers/twin/output/floor1-multiview-next-20261009';OUT=BASE/'unit8-rebuild'
P=json.loads((OUT/'unit8-replacement-packet.json').read_text());G=json.loads((BASE/'guide-semantics.json').read_text());D=json.loads((ROOT/P['source_export']).read_text());MM={x['id']:x for x in D['meshes']}
font=lambda n:ImageFont.truetype('/Users/um-yunsang/Library/Fonts/NotoSansCJKkr-Regular.otf',n)
old=np.array(P['current_polygon_UV']);new=np.array(P['proposed_polygon_UV']);reg=np.array(G['registration']['pixel_homogeneous_row_to_UV_matrix']);topixel=lambda p:(np.array(p)-reg[2])@np.linalg.inv(reg[:2])
im=Image.new('RGB',(2000,1300),'white');dr=ImageDraw.Draw(im)
dr.text((40,20),'Unit 8 · constrained-guide reconstruction and preservation',font=font(40),fill='#152e44')
dr.text((40,86),'OFFLINE PASS  |  17 mesh masks  |  2,014 → 2,028 triangles  |  7 root motions  |  No production writes',font=font(25),fill='#00814f')
dr.text((40,138),'Public 1F guide · 양산국밥 8',font=font(25),fill='#152e44');dr.text((700,138),'Native station UV · exterior and cores preserved',font=font(25),fill='#152e44')
crop=(775,142,937,365);scale=3;im.paste(Image.open(ROOT/G['native_image']).crop(crop).resize((486,669)),(40,180));sp=lambda p:(40+(p[0]-crop[0])*scale,180+(p[1]-crop[1])*scale)
unit=next(u for u in G['units'] if u['boardNo']==8)
for pp,col in [(unit['source_polygon_pixels_approx'],'#d79200'),(topixel(old),'#bf2d3f'),(topixel(new),'#00814f')]:
 pp=list(map(sp,pp));dr.line(pp+[pp[0]],fill=col,width=4)
for i,(txt,col) in enumerate([('Gold: digitized source outline','#d79200'),('Red: existing 7-point footprint','#bf2d3f'),('Green: constrained 10-point target','#00814f')]):dr.text((40,869+32*i),txt,font=font(21),fill=col)
extent=box(-95,-40,-67,-16);npnt=lambda p:(730+(p[0]+95)*25,185+(-16-p[1])*25)
overlay=Image.new('RGBA',im.size);od=ImageDraw.Draw(overlay)
def geoms(p):return list(p.geoms) if p.geom_type in ['MultiPolygon','MultiLineString','GeometryCollection'] else [p]
def paint(poly,col,alpha=55):
 for p in geoms(poly.intersection(extent)):
  if p.geom_type=='Polygon' and not p.is_empty:
   xy=list(map(npnt,p.exterior.coords));od.polygon(xy,fill=col+(alpha,));od.line(xy,fill=col+(200,),width=2)
def line(coords,color,width=2,dashed=False):
 for p in geoms(LineString(coords).intersection(extent)):
  if p.geom_type!='LineString':continue
  pts=list(map(npnt,p.coords))
  if not dashed:od.line(pts,fill=color,width=width)
  else:
   for a,b in zip(pts,pts[1:]):
    a=np.array(a);b=np.array(b);length=np.linalg.norm(b-a)
    for x in np.arange(0,length,12):od.line([tuple(a+(b-a)*x/length),tuple(a+(b-a)*min(x+7,length)/length)],fill=color,width=width)
for f in P['floor_support']['native_top_floor_owners']:
 mid=f['mesh']['id'];r=f['collider'];b=(ROOT/MM[mid]['binary']).read_bytes();nv,ni,nn=struct.unpack_from('<iii',b);v=np.frombuffer(b,'<f4',nv*3,12).reshape(nv,3);t=np.frombuffer(b,'<i4',ni,12+nv*12).reshape(-1,3);m=np.array(r['world_matrix']);w=v@m[:3,:3].T+m[:3,3];q=np.column_stack([.278991133*w[:,0]+.9602937*w[:,2],.9602937*w[:,0]-.278991133*w[:,2]])
 p=unary_union([Polygon(q[t[i]]) for i in f['faces']]).intersection(Polygon(new));paint(p,(136,190,222) if 'ContextPlaza' in r['path'] else (174,180,186),130)
paint(Polygon(P['preserve']['end_core_approach_polygon_UV']),(161,131,188),55)
public=json.loads((ROOT/'content/world/observed-interiors/public-1f.json').read_text())
for e in public['elements']:
 if 'polygon' in e.get('geometry',{}) and e['type'] in ['Room','ToiletRoom']:paint(Polygon(e['geometry']['polygon']),(139,86,167),90)
sections=json.loads((ROOT/'.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/floor1-actual-segments.json').read_text())
for owner in sections['owners']:
 if '/MainShell/' in owner['path']:
  for s in owner['segments']:line(s,(52,60,71,255),3)
line(list(old)+[old[0]],(191,45,63,255),3,True);line(list(new)+[new[0]],(0,129,79,255),4)
for i,pt in enumerate(new):od.text(tuple(np.array(npnt(pt))+[5,-24]),str(i),font=font(19),fill=(0,101,61,255))
for fixture in P['fixture_placements']:
 hull=shape(fixture['footprint_UV']);paint(hull,(75,98,125),190);c=hull.representative_point();od.text(tuple(np.array(npnt([c.x,c.y]))+[-6,-14]),fixture['name'].split()[-1],font=font(18),fill='white')
dc=np.array(P['door']['new_centre_UV']);normal=np.array(P['door']['new_outward_normal_UV']);line(P['door']['nominal_aperture_endpoints_UV'],(255,166,0,255),10);line([dc-normal*1.5,dc+normal*1.5],(0,103,154,255),3)
im=Image.alpha_composite(im.convert('RGBA'),overlay).convert('RGB');dr=ImageDraw.Draw(im);dr.rectangle([npnt([-95,-16]),npnt([-67,-40])],outline='#a9afb7',width=1)
for u in range(-95,-66,5):
 x,y=npnt([u,-40]);dr.text((x-15,y+8),str(u),font=font(17),fill='#465363')
for v0 in range(-40,-15,4):
 x,y=npnt([-95,v0]);dr.text((x-45,y-12),str(v0),font=font(17),fill='#465363')
dr.text((1050,823),'Station U',font=font(20),fill='#465363');dr.text((640,470),'V',font=font(22),fill='#465363')
for i,(txt,col) in enumerate([('Black: original MainShell','#343c47'),('Purple: core + kept approach','#8b56a7'),('Blue: ContextPlaza top Y≈0','#548bad'),('Gray: Ground_Floor top Y0','#70777e'),('Orange: existing door, moved','#bd7900')]):dr.text((730,865+30*i),txt,font=font(20),fill=col)
dr.text((1490,188),'Exact QA',font=font(27),fill='#152e44')
side=['Zero protected-core overlap','Nearest core: 0.8355','Native long-wall crossings: 0','Granite finish Y .004','Ceiling Y 3.0','Fully open leaf gap: 1.769999','Visual post opening: 1.75','Y1 door approach ±1.5: clear','Radius .28; nearest: .884998','Native floor append: none','7 existing contents retained','8 + 10 downlights retained']
for i,t in enumerate(side):dr.text((1490,242+38*i),t,font=font(20),fill='#243d54')
dr.text((1490,742),'Door / leaf local fields, meshes,',font=font(19),fill='#465363');dr.text((1490,772),'slides, materials kept unchanged.',font=font(19),fill='#465363');dr.line([(40,1040),(1960,1040)],fill='#d9dde1',width=2)
cols=[(40,['Native Y≈0 support already covers target.','Raw residual area: 0.000001693.','Max required buffer: 0.00001108,','within existing 0.000020 tolerance.','Numerical sliver omitted; floors untouched.']),(700,['Six dining fixtures + one plant retained.','Five translated safely into old ∩ new.','Two fixed tables retain source AABB gap','0.04497 with zero overlap. Changed','placements keep >.10 pair clearance.']),(1350,['Guide author claims 2026-08-11; revision unknown.','Affine held-out errors: 2.69 / 3.41 units.','Public reference layout; no metric CAD claim.','Physical 2 cm accuracy remains unverified.','Unity runtime acceptance belongs to root.'])]
for x,lines in cols:
 for i,t in enumerate(lines):dr.text((x,1068+34*i),t,font=font(19),fill='#465363')
im.save(OUT/'unit8-source-native-review.png');im.save(OUT/'unit8-source-native-review.pdf',resolution=150)
print('Wrote unit8-source-native-review.png and PDF')
