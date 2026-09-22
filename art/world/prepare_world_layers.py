#!/usr/bin/env python3
"""Derive visual-only city surfaces from immutable OSM, in source metres."""
import json, math, hashlib, collections
from pathlib import Path
import xml.etree.ElementTree as ET
from shapely.geometry import LineString, Polygon, Point, box
from shapely.ops import unary_union, polygonize
from shapely import constrained_delaunay_triangles
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'asset-library/space-references/busan-reconstruction'
data=json.loads((SRC/'busan-openworld.json').read_text()); b=data['requestedBounds']; bounds=box(b['minX'],b['minZ'],b['maxX'],b['maxZ'])
r=ET.parse(SRC/'osm-busan-openworld.xml').getroot()
nodes={n.get('id'):(float(n.get('lon')),float(n.get('lat'))) for n in r.findall('node')}
def xy(v): return ((v[0]-data['originLon'])*111320*math.cos(math.radians(data['originLat'])),(v[1]-data['originLat'])*111320)
# Reuse exported coordinates exactly wherever available.
known={}
ways={w.get('id'):w for w in r.findall('way')}
for f in data['features']:
 w=ways.get(f['id'])
 if w is not None and len(w.findall('nd'))==len(f['points']):
  for n,p in zip(w.findall('nd'),f['points']): known[n.get('ref')]=(p['x'],p['z'])
def pts(w): return [known.get(n.get('ref'),xy(nodes[n.get('ref')])) for n in w.findall('nd') if n.get('ref') in nodes]
def tags(w): return {t.get('k'):t.get('v') for t in w.findall('tag')}
coasts=[LineString(pts(w)) for w in ways.values() if tags(w).get('natural')=='coastline' and len(pts(w))>1]
segments=[(a,c,LineString([a,c])) for line in coasts for a,c in zip(line.coords,list(line.coords)[1:]) if a!=c]
cells=list(polygonize(unary_union([bounds.boundary]+[x.intersection(bounds) for x in coasts])))
land=[];sea=[]
for cell in cells:
 p=cell.representative_point(); a,c,_=min(segments,key=lambda s:s[2].distance(p)); cross=(c[0]-a[0])*(p.y-a[1])-(c[1]-a[1])*(p.x-a[0]); (land if cross>0 else sea).append(cell)
land=unary_union(land); sea=unary_union(sea)
assert not land.is_empty and not sea.is_empty,'Coast partition failed'
assert abs(land.area+sea.area-bounds.area)<.1,'Incomplete coastline coverage'
terrainReceipt=json.loads((SRC/'terrain/receipt.json').read_text())
tiles={(t['x'],t['y']):Image.open(ROOT/t['path']).convert('RGB') for t in terrainReceipt['tiles']}
def raw_height(x,z):
 lon=data['originLon']+x/(111320*math.cos(math.radians(data['originLat'])));lat=data['originLat']+z/111320
 gx=(lon+180)/360*8192*256-.5;gy=(1-math.asinh(math.tan(math.radians(lat)))/math.pi)/2*8192*256-.5
 ix,iy=math.floor(gx),math.floor(gy);fx,fy=gx-ix,gy-iy
 def at(px,py):
  rgb=tiles[(px//256,py//256)].getpixel((px%256,py%256));return rgb[0]*256+rgb[1]+rgb[2]/256-32768
 return (at(ix,iy)*(1-fx)+at(ix+1,iy)*fx)*(1-fy)+(at(ix,iy+1)*(1-fx)+at(ix+1,iy+1)*fx)*fy
pads=[]
for id in ['165346389','165346394','368597686','382696296']:
 if id in ways:
  p=Polygon(pts(ways[id])).buffer(60); minx,minz,maxx,maxz=p.bounds;pads.append({'minX':minx,'minZ':minz,'maxX':maxx,'maxZ':maxz})
offset=raw_height(0,0);step=40.;nx=math.ceil((b['maxX']-b['minX'])/step)+1;nz=math.ceil((b['maxZ']-b['minZ'])/step)+1
raw=[raw_height(b['minX']+i*step,b['minZ']+j*step) for j in range(nz) for i in range(nx)]
heights=[]
coastGeometry=unary_union(coasts)
for j in range(nz):
 for i in range(nx):
  x=b['minX']+i*step;z=b['minZ']+j*step;factor=min(1,coastGeometry.distance(Point(x,z))/40)
  for pad in pads:
   distance=math.hypot(max(pad['minX']-x,0,x-pad['maxX']),max(pad['minZ']-z,0,z-pad['maxZ']));t=min(1,distance/60);factor=min(factor,t*t*(3-2*t))
  heights.append(max(0,raw[j*nx+i]-offset)*factor)
heightfield={'minX':b['minX'],'minZ':b['minZ'],'step':step,'nx':nx,'nz':nz,'heights':heights,'pads':pads,'datumOffsetMeters':offset,'padBlendMeters':60}
out={'sourceScale':1,'heightfield':heightfield,'layers':[],'buildings':[],'props':[],'receipt':{}}
def polygons(g):
 if g.is_empty:return []
 return [g] if g.geom_type=='Polygon' else [p for child in getattr(g,'geoms',[]) for p in polygons(child)]
def emit(kind,g):
 clipped=g.intersection(bounds)
 if kind!='water':
  cells=[]
  for sourcePolygon in polygons(clipped):
   x0,z0,x1,z1=sourcePolygon.bounds
   for ix in range(max(0,int((x0-b['minX'])//step)),min(nx-1,int((x1-b['minX'])//step)+1)):
    for iz in range(max(0,int((z0-b['minZ'])//step)),min(nz-1,int((z1-b['minZ'])//step)+1)):
     x=b['minX']+ix*step;z=b['minZ']+iz*step
     for cell in [Polygon([(x,z),(x+step,z),(x+step,z+step)]),Polygon([(x,z),(x+step,z+step),(x,z+step)])]:cells.extend(polygons(sourcePolygon.intersection(cell)))
 else:cells=polygons(clipped)
 for p in cells:
  ts=list(constrained_delaunay_triangles(p).geoms)
  assert abs(sum(t.area for t in ts)-p.area)<.01,'Triangle coverage mismatch'
  if ts:out['layers'].append({'kind':kind,'points':[{'x':round(x,3),'z':round(z,3)} for t in ts for x,z in list(t.exterior.coords)[:3]]})
emit('land',land);emit('water',sea)
buildings=[];roads=[];asphalt=[];parks=[];quays=[]
for f in data['features']:
 w=ways.get(f['id']); t=tags(w) if w is not None else {}; ps=[(p['x'],p['z']) for p in f['points']]
 if f['kind']=='building' and len(ps)>3:
  poly=Polygon(ps).buffer(0).intersection(bounds)
  if poly.is_empty:continue
  buildings.append(poly);c=poly.centroid; name=t.get('name','');typ=t.get('building',''); h=float(t.get('height','0').replace(' m','')) if t.get('height','0').replace(' m','').replace('.','',1).isdigit() else 0
  archetype=5 if h>=55 else 4 if typ in ['warehouse','industrial'] or c.x>450 else 3 if typ in ['public','civic','school','hospital','train_station'] else 2 if typ in ['office','commercial'] else 1 if typ in ['retail','shop'] or c.y< -1000 else 0
  out['buildings'].append({'id':f['id'],'archetype':archetype,'district': 'port' if c.x>450 else 'nampo' if c.y< -1300 else 'jungang' if c.y< -350 else 'choryang','name':name})
 if f['kind']=='road' and len(ps)>1:
  foot=f['subtype'] in ['footway','path','steps','pedestrian']; width=2.8 if foot else 16 if f['subtype'] in ['primary','trunk'] else 12 if f['subtype']=='secondary' else 6
  line=LineString(ps).intersection(bounds);
  if not foot: asphalt.append(line.buffer(width/2,resolution=2))
  roads.append(line.buffer(width/2+2,resolution=2));emit('walk' if foot else 'sidewalk',line.buffer(width/2+1.5,resolution=2).intersection(land))
emit('asphalt',unary_union(asphalt))
emit('curb',unary_union(asphalt).buffer(.3,resolution=2).difference(unary_union(asphalt)).intersection(land))
for w in ways.values():
 t=tags(w); ps=pts(w)
 if len(ps)<3:continue
 if ps[0]==ps[-1] and (t.get('leisure') in ['park','garden'] or t.get('landuse') in ['grass','recreation_ground','forest'] or t.get('natural') in ['wood','scrub']):
  p=Polygon(ps).buffer(0).intersection(land); parks.append(p);emit('park',p)
 if t.get('man_made')=='quay':quays.append(LineString(ps))
 for_kind='plaza' if t.get('highway')=='pedestrian' and t.get('area')=='yes' else None
 if for_kind and ps[0]==ps[-1]:emit(for_kind,Polygon(ps).buffer(0).intersection(land))
obstacles=unary_union(buildings+roads).buffer(1)
# Park points are deterministic; never place vegetation on source buildings/roads/water.
for p in polygons(unary_union(parks).difference(obstacles)):
 minx,minz,maxx,maxz=p.bounds
 for x in range(math.ceil(minx/24)*24,math.floor(maxx/24)*24+1,24):
  for z in range(math.ceil(minz/24)*24,math.floor(maxz/24)*24+1,24):
   if len(out['props'])>=650:break
   if p.contains(Point(x,z)):out['props'].append({'kind':'TreePine' if (x+z)%5==0 else 'TreeBroadleaf','x':x,'z':z,'size':5.5,'angle':(x*13+z*7)%360})
# Source road frontage lamps, bounded and outside carriageway/buildings.
for f in data['features']:
 if f['kind']!='road' or f['subtype'] not in ['primary','secondary']:continue
 line=LineString([(p['x'],p['z']) for p in f['points']]); offset=11 if f['subtype']=='primary' else 9
 for distance in range(30,int(line.length),100):
  if sum(p['kind']=='StreetLamp' for p in out['props'])>=180:break
  a=line.interpolate(distance);c=line.interpolate(min(distance+1,line.length));dx,dz=c.x-a.x,c.y-a.y;l=math.hypot(dx,dz)
  if l<.001:continue
  p=Point(a.x-dz/l*offset,a.y+dx/l*offset)
  if bounds.contains(p) and land.contains(p) and not obstacles.contains(p):out['props'].append({'kind':'StreetLamp','x':round(p.x,3),'z':round(p.y,3),'size':6,'angle':math.degrees(math.atan2(dx,dz))})
for id,kind,size in [('480601129','BusanTower',120),('368597686','PortTerminal',25),('382696296','JagalchiMarket',24)]:
 f=next((x for x in data['features'] if x['id']==id),None)
 if f is None and id in ways: f={'points':[{'x':x,'z':z} for x,z in pts(ways[id])]}
 if f:
  p=Polygon([(v['x'],v['z']) for v in f['points']]).centroid;out['props'].append({'kind':kind,'x':round(p.x,3),'z':round(p.y,3),'size':size,'angle':0,'sourceId':id,'positionEvidence':'commercial-area centroid proxy, not building footprint' if id=='502017125' else 'source building centroid','footprintWidth':round(Polygon([(v['x'],v['z']) for v in f['points']]).bounds[2]-Polygon([(v['x'],v['z']) for v in f['points']]).bounds[0],3),'footprintDepth':round(Polygon([(v['x'],v['z']) for v in f['points']]).bounds[3]-Polygon([(v['x'],v['z']) for v in f['points']]).bounds[1],3)})
out['receipt']={'sourceSha256':hashlib.sha256((SRC/'osm-busan-openworld.xml').read_bytes()).hexdigest(),'coastWays':len(coasts),'partitionCells':len(cells),'landAreaM2':round(land.area,2),'waterAreaM2':round(sea.area,2),'triangles':sum(len(x['points'])//3 for x in out['layers']),'props':dict(collections.Counter(x['kind'] for x in out['props'])),'archetypes':dict(collections.Counter(x['archetype'] for x in out['buildings'])),'source':'OSM footprints, directed coastlines, tagged park polygons, road paths and landmark centroids','derived':'Palette, fallback heights, surface widths, furniture, tree grid and landmark visual dimensions; no solver geometry','culvertStreamsRendered':0,'topology':'polygonized original coastline plus requested boundary; land lies left of directed coastline','terrain':{'source':'Mapzen Terrarium four tiles, receipt.json','decode':'R*256+G+B/256-32768 metres','rawMin':min(raw),'rawMax':max(raw),'datumOffsetMeters':offset,'gridStepMeters':step,'gridPoints':len(heights),'flatPads':'station/terminal/market bounding envelopes plus60m apron,60m smooth transition; visual only','verticalScale':1,'accuracy':'not surveyed, visual sampled relief only'},'geometryValidation':{'coverageErrorM2':abs(land.area+sea.area-bounds.area),'overlapM2':land.intersection(sea).area,'landValid':land.is_valid,'waterValid':sea.is_valid}}
(ROOT/'art/world/world-layers.json').write_text(json.dumps(out,separators=(',',':'),ensure_ascii=False)+'\n')
print(json.dumps(out['receipt'],indent=2,ensure_ascii=False))
# Reproducible URP metallic/smoothness packing from authored material receipt.
from PIL import ImageOps
manifest=json.loads((ROOT/'art/blender/world-set-manifest.json').read_text())
material_folder=ROOT/'Assets/ChooGuard/Art/WorldSurfaces';material_folder.mkdir(parents=True,exist_ok=True)
assert len(manifest['models'])==9 and all((ROOT/m['fbx']).exists() for m in manifest['models'])
for material in manifest['materials']:
 roughness=material.get('textures',{}).get('roughness')
 if roughness:
  image=Image.open(ROOT/roughness).convert('L');metal=Image.new('L',image.size,round(material['metallic']*255));zero=Image.new('L',image.size,0)
  Image.merge('RGBA',(metal,zero,zero,ImageOps.invert(image))).save(material_folder/(material['name']+'-metallic-smoothness.png'))
