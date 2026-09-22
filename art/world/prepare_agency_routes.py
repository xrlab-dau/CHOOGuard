#!/usr/bin/env python3
"""Source-node directed roads; stdlib Dijkstra, no geometric intersection joins."""
import json, math, hashlib, heapq, collections
from pathlib import Path
import xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[2]; S=R/'asset-library/space-references/busan-reconstruction'
world=json.loads((S/'busan-openworld.json').read_text()); catalog=json.loads((S/'agency-catalog.json').read_text()); h=json.loads((R/'art/world/world-layers.json').read_text())['heightfield']
xml=ET.parse(S/'osm-busan-openworld.xml').getroot(); ways={w.get('id'):w for w in xml.findall('way')}
def project(lon,lat): return ((lon-world['originLon'])*111320*math.cos(math.radians(world['originLat'])),(lat-world['originLat'])*111320)
nodes={n.get('id'):project(float(n.get('lon')),float(n.get('lat'))) for n in xml.findall('node')}
for f in world['features']:
 w=ways.get(f['id'])
 if w is not None and len(w.findall('nd'))==len(f['points']):
  for n,p in zip(w.findall('nd'),f['points']):nodes[n.get('ref')]=(p['x'],p['z'])
def point(p):
 x,z=p;fx=max(0,min((x-h['minX'])/h['step'],h['nx']-1.001));fz=max(0,min((z-h['minZ'])/h['step'],h['nz']-1.001));ix,iz=int(fx),int(fz);fx-=ix;fz-=iz
 a,b,c,d=[h['heights'][k] for k in (iz*h['nx']+ix,iz*h['nx']+ix+1,(iz+1)*h['nx']+ix+1,(iz+1)*h['nx']+ix)]
 y=a+(b-a)*fx+(c-b)*fz if fx>=fz else a+(c-d)*fx+(d-a)*fz
 return dict(x=round(x,3),y=round(y+.18,3),z=round(z,3))
def dist(a,b):return math.dist(a,b)
graph=collections.defaultdict(list); roadtags={};allowed={'motorway','motorway_link','trunk','trunk_link','primary','primary_link','secondary','secondary_link','tertiary','tertiary_link','residential','unclassified','service','living_street'}
for wid,w in ways.items():
 t={x.get('k'):x.get('v') for x in w.findall('tag')}
 if t.get('highway') not in allowed or any(t.get(k) in ('no','private') for k in ('access','vehicle','motor_vehicle')):continue
 # Conditional restrictions and non-ground structures are conservatively unavailable.
 if any('conditional' in k for k in t) or t.get('bridge','no')!='no' or t.get('tunnel','no')!='no' or t.get('layer','0')!='0':continue
 refs=[n.get('ref') for n in w.findall('nd')];roadtags[wid]=t;one=t.get('oneway','yes' if t.get('junction')=='roundabout' else 'no')
 for a,b in zip(refs,refs[1:]):
  if a not in nodes or b not in nodes:continue
  length=dist(nodes[a],nodes[b])
  if one!='-1':graph[a].append((b,length,wid))
  if one not in ('yes','1','true'):graph[b].append((a,length,wid))
allnodes=set(graph)|{e[0] for edges in graph.values() for e in edges}
def nearest(p):return min(allnodes,key=lambda n:dist(p,nodes[n]))
def route(start,end):
 q=[(0,start)];best={start:0};prev={}
 while q:
  cost,n=heapq.heappop(q)
  if cost!=best[n]:continue
  if n==end:break
  for v,d,w in graph[n]:
   if cost+d<best.get(v,float('inf')):best[v]=cost+d;prev[v]=(n,w);heapq.heappush(q,(cost+d,v))
 if end not in best:return None
 ns=[end];ws=[]
 while ns[-1]!=start:n,w=prev[ns[-1]];ns.append(n);ws.append(w)
 return list(reversed(ns)),list(reversed(ws)),best[end]
station=(-52,-58) # authored exterior staging connector, not surveyed entrance
sn=nearest(station)
base_ids=['busan-jungbu-central-119','busan-jungbu-choryang-119']
receipt_path=R/'art/world/building-depot-placement-receipt.json'
depot_receipt=json.loads(receipt_path.read_text()) if receipt_path.exists() else {'depots':[]}
depots={d['name']:d for d in depot_receipt['depots']}
depot_names=dict(zip(base_ids,['Central119Restored','Choryang119Restored']))
def vadd(a,b,scale=1):return {k:round(a[k]+b[k]*scale,3) for k in ('x','y','z')}
buildings={'busan-jungbu-central-119':'825455699','busan-jungbu-choryang-119':'825456385','busan-jungbu-yeongju-police':'825455453','busan-maryknoll-emergency':'759425307'}
def polygon(wid):return [point(nodes[n.get('ref')]) for n in ways[wid].findall('nd') if n.get('ref') in nodes]
result={'version':2,'sourceAgencyCount':len(catalog['agencies']),'speedMetersPerSecond':8,'memberHeadwayMeters':18,'departureHeadwaySeconds':8,'maxQueuedJobsPerTeam':3,'agencies':[], 'routes':[], 'teams':[], 'tasks':[{'id':'evacuation-support','label':'대피 지원','channel':'fire-1','automaticReturn':True,'actions':['warn','evacuate']},{'id':'medical-support','label':'의료 지원','channel':'medical-1','automaticReturn':False,'actions':['medical']}], 'targets':[{'id':'busan-station-reference','label':'부산역 참조 대합실','point':{'x':-37,'y':5.05,'z':-38},'polygon':polygon('165346389'),'scope':'Station building click selects the authored 30x20m reference-hall task target only; no whole-station solver claim'}], 'assumptions':['Six operational teams and eight visual members are authored initial training allocations, not real fleet counts or a runtime cap','Published regional fleet counts are not per-center assignments','8 m/s travel,18m member headway,8s group departure headway are design assumptions','Representative building/station road connectors and parking offsets are authored, not verified garage entrances','Only ground-level public motor roads; bridge/tunnel/nonzero layer/conditional restrictions excluded','OSM turn restrictions are not modeled; training geometry, not legal navigation','Each group owns one shared road progress and task; members have no independent task brains']}
for a in catalog['agencies']:
 result['agencies'].append({'id':a['id'],'label':a['label'],'coordinateReady':a['lat'] is not None,'dispatchSupported':a['id'] in base_ids,'point':point(project(a['lon'],a['lat'])) if a['lat'] is not None else dict(x=0,y=0,z=0),'polygon':polygon(buildings[a['id']])})
 if a['id'] not in base_ids:continue
 depot=depots.get(depot_names[a['id']]);base_point=point(project(a['lon'],a['lat']))
 if depot:
  common=dict(depot['streetHandoffAnchors'][1]);common['y']+=.18
  base_point=common;record=result['agencies'][-1];record['point']['y']=depot['garageFloorY'];record['pickCenter']=depot['modelBoundsCenter'];record['pickSize']=depot['modelBoundsSize'];record['parkingForward']=depot['outward'];record['departureConnectors']=[]
  for slot in range(4):
   bay=slot if slot<3 else 1;entry=dict(depot['garageEntryAnchors'][bay]);park=vadd(entry,depot['outward'],-5 if slot<3 else -15)
   record['departureConnectors'].append({'points':[park,entry,depot['apronStagingAnchors'][bay],depot['streetHandoffAnchors'][bay],common]})
  record['parkingProvenance']='Authored training parking behind inferred garage bay centers; fourth member uses second depth row; not observed fleet/garage capacity. Inferred bay→apron→street connector includes nonsurveyed elevation transition.'
 base=(base_point['x'],base_point['z']);bn=nearest(base);prefix='central119' if a['id']==base_ids[0] else 'choryang119'
 for role,label,count,visual,task in [('evacuation-response','대피 대응팀',2,'fire','evacuation-support'),('evacuation-relief','대피 지원팀',1,'fire','evacuation-support'),('medical-support','의료 지원팀',1,'ambulance','medical-support')]:
  result['teams'].append({'id':prefix+'-'+role,'agencyId':a['id'],'label':label,'memberCount':count,'visual':visual,'workTypes':[task]})
 for name,start,end,p,q in [('outbound',bn,sn,base,station),('return',sn,bn,station,base)]:
  path=route(start,end);rid=a['id']+':'+name
  if path is None:result['routes'].append({'id':rid,'agencyId':a['id'],'direction':name,'available':False,'reason':'연결된 도로 경로 없음','points':[]});continue
  ns,ws,d=path;points=[dict(base_point) if name=='outbound' else point(p)]+[point(nodes[n]) for n in ns]+[dict(base_point) if name=='return' else point(q)]
  result['routes'].append({'id':rid,'agencyId':a['id'],'direction':name,'available':True,'points':points,'nodeIds':ns,'wayIds':ws,'roadDistanceMeters':round(d,3),'connectorMeters':round(dist(p,nodes[start])+dist(q,nodes[end]),3),'distanceMeters':round(sum(math.dist(tuple(x.values()),tuple(y.values())) for x,y in zip(points,points[1:])),3),'wayTags':{w:roadtags[w] for w in set(ws)}})
result['sourceHashes']={str(p.relative_to(R)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [S/'agency-catalog.json',S/'osm-busan-openworld.xml',S/'busan-openworld.json',R/'art/world/world-layers.json']}
result['depotGroundingReady']=len(depots)==2
if receipt_path.exists():result['sourceHashes'][str(receipt_path.relative_to(R))]=hashlib.sha256(receipt_path.read_bytes()).hexdigest()
out=R/'Assets/ChooGuard/Art/AgencyDispatch/agency-routes.json';out.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');(out.parent/'agency-catalog.json').write_bytes((S/'agency-catalog.json').read_bytes())
print(json.dumps({'logicalTeams':len(result['teams']),'visualMembers':sum(t['memberCount'] for t in result['teams']),'routes':[{k:v for k,v in r.items() if k in ('id','available','roadDistanceMeters','connectorMeters','distanceMeters')} for r in result['routes']]},ensure_ascii=False))
