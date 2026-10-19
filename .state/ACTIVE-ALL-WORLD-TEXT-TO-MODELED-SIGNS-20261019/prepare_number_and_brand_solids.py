import json,math
from pathlib import Path
from shapely import constrained_delaunay_triangles
from shapely.geometry import Polygon
from shapely.geometry.polygon import orient
from shapely.ops import unary_union
R=Path('/Users/um-yunsang/CHOOGuard');D=R/'.state/ACTIVE-ALL-WORLD-TEXT-TO-MODELED-SIGNS-20261019'
font=json.loads((D/'ACTUAL-NUMBER-GLYPH-OUTLINES.json').read_text())
inventory=json.loads((D/'CURRENT-WORLD-TEXT-AND-SIGN-INPUT.json').read_text())
def rings(commands):
 out=[];line=[];last=None
 for c in commands:
  if c[0]=='M':line=[tuple(c[1:3])];last=line[-1]
  elif c[0]=='L':last=tuple(c[1:3]);line.append(last)
  elif c[0] in ['Q','C']:
   p0=last;ps=[tuple(c[i:i+2]) for i in range(1,len(c),2)];length=sum(math.dist(a,b) for a,b in zip([p0,*ps],ps));steps=max(3,min(45,math.ceil(length/24)))
   for i in range(1,steps+1):
    t=i/steps;q=1-t
    if c[0]=='Q':p=tuple(q*q*p0[k]+2*q*t*ps[0][k]+t*t*ps[1][k] for k in range(2))
    else:p=tuple(q**3*p0[k]+3*q*q*t*ps[0][k]+3*q*t*t*ps[1][k]+t**3*ps[2][k] for k in range(2))
    line.append(p)
   last=ps[-1]
  elif c[0]=='Z':
   if len(line)>=3:out.append(line)
   line=[]
 return out
glyphs={}
for digit,g in font['digits'].items():
 p=None
 for r in rings(g['commands']):
  ring=Polygon(r).buffer(0);p=ring if p is None else p.symmetric_difference(ring)
 glyphs[digit]=p
def solid(polys):
 vs=[];ts=[];cache={}
 def idx(p):
  key=tuple(round(float(v),9) for v in p)
  if key not in cache:cache[key]=len(vs);vs.append(key)
  return cache[key]
 def tri(a,b,c):ts.extend([idx(a),idx(b),idx(c)])
 for p in polys:
  p=orient(p,sign=1)
  for t in constrained_delaunay_triangles(p).geoms:
   a,b,c=list(t.exterior.coords)[:3];area=(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
   if area<0:b,c=c,b
   tri([*a,1],[*b,1],[*c,1]);tri([*c,0],[*b,0],[*a,0])
  for ring in [p.exterior,*p.interiors]:
   pts=list(ring.coords)
   for a,b in zip(pts,pts[1:]):tri([*a,0],[*b,0],[*b,1]);tri([*a,0],[*b,1],[*a,1])
 return {'vertices':vs,'triangles':ts}
from shapely.affinity import translate,affine_transform
arts={}
for e in inventory['world_texts']:
 if not e['active_in_hierarchy'] or not e['text'].isdigit():continue
 text=e['text'];cursor=0;gs=[]
 for digit in text:gs.append(translate(glyphs[digit],xoff=cursor));cursor+=font['digits'][digit]['advance']
 p=unary_union(gs);lo=p.bounds;w=lo[2]-lo[0];h=lo[3]-lo[1];p=affine_transform(p,[1/w,0,0,1/h,-lo[0]/w,-lo[1]/h]);data=solid([p] if p.geom_type=='Polygon' else list(p.geoms));data.update(key='number_'+text,aspect=w/h,color=e['color'],source_font=font['source_font'],method='Direct Bezier-outline solid geometry of existing MODEL numeric marks, not a text component or atlas quad. Number sequence retained, not independently site-confirmed.')
 arts[text]=data
old=json.loads((R/'.state/ACTIVE-1F-STORE-SIGN-GRAPHICS-20261019/ACTUAL-2F-SIGN-ART-PACKET.json').read_text())['arts']['soban_word'];groups=[]
allv=[v for g in old['groups'] for v in g['vertices']];minx=min(v[0] for v in allv);maxx=max(v[0] for v in allv);miny=min(v[1] for v in allv);maxy=max(v[1] for v in allv)
for g in old['groups']:
 data={'name':g['name'],'color':g['color'],'vertices':[[(v[0]-minx)/(maxx-minx),(v[1]-miny)/(maxy-miny),-v[2]/.025] for v in g['vertices']],'triangles':g['triangles']};groups.append(data)
kiosk={'key':'soban_actual_source_kiosk_brand','aspect':(maxx-minx)/(maxy-miny),'source':old['source'],'palette':groups,'method':'Same actual printed Soban custom brand contours directly extruded; existing kiosk position retained as MODEL.'}
out={'numbers':arts,'kiosk':kiosk,'source_number_font':font['source_font'],'world_text_inventory':'CURRENT-WORLD-TEXT-AND-SIGN-INPUT.json'}
(D/'NUMBER-AND-KIOSK-SOLIDS.json').write_text(json.dumps(out,separators=(',',':')))
print('Prepared',len(arts),'direct numeric reliefs plus actual-source Soban kiosk brand; no source assets changed.')
