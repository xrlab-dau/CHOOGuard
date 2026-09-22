#!/usr/bin/env python3
"""Run from repo root with .tools/sumo/.venv/bin/python workers/traffic/reproduce.py."""
import csv, hashlib, json, pathlib, subprocess, xml.etree.ElementTree as ET
ROOT=pathlib.Path(__file__).resolve().parents[2]
OUT=ROOT/'workers/traffic/reference-network'; OUT.mkdir(parents=True,exist_ok=True)
BIN=ROOT/'.tools/sumo/.venv/bin'
SOURCE=ROOT/'asset-library/space-references/busan-reconstruction/osm-busan-openworld.xml'
commands=[]
def run(name,args):
 cmd=[str(BIN/name),*map(str,args)]
 p=subprocess.run(cmd,cwd=ROOT,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=180)
 (OUT/(name+'.log')).write_text(p.stdout)
 commands.append({'argv':cmd,'exitCode':p.returncode})
 if p.returncode: raise RuntimeError(p.stdout[-3000:])
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
run('netconvert',['--osm-files',SOURCE,'--output-file',OUT/'busan.net.xml','--geometry.remove','--output.original-names','true','--keep-edges.by-vclass','passenger'])
agency=json.loads((ROOT/'asset-library/space-references/busan-reconstruction/agency-catalog.json').read_text())['agencies'][0]
osm=ET.parse(SOURCE).getroot()
# Explicit public Busan Station surface bus stop; not a guessed rail-building entrance.
target=next(n for n in osm.findall('node') if n.get('id')=='5004274563')
routes=ET.Element('routes')
ET.SubElement(routes,'vType',id='rescue',vClass='emergency',maxSpeed='8',speedFactor='1',sigma='0')
ET.SubElement(routes,'trip',id='central119-busan-station-source-check',type='rescue',depart='0',fromLonLat=f"{agency['lon']},{agency['lat']}",toLonLat=f"{target.get('lon')},{target.get('lat')}")
ET.ElementTree(routes).write(OUT/'emergency.trips.xml',encoding='utf-8',xml_declaration=True)
run('duarouter',['--net-file',OUT/'busan.net.xml','--route-files',OUT/'emergency.trips.xml','--output-file',OUT/'emergency.rou.xml','--seed','4242'])
run('sumo',['--net-file',OUT/'busan.net.xml','--route-files',OUT/'emergency.rou.xml','--step-length','1','--seed','4242','--end','3600','--time-to-teleport','-1','--fcd-output',OUT/'trajectory.xml','--tripinfo-output',OUT/'tripinfo.xml','--duration-log.statistics','true','--no-step-log','true'])
net=ET.parse(OUT/'busan.net.xml').getroot()
trip=ET.parse(OUT/'tripinfo.xml').getroot().findall('tripinfo')
assert len(trip)==1 and float(trip[0].get('arrival'))>0,'Vehicle did not complete'
rows=[]
for step in ET.parse(OUT/'trajectory.xml').getroot().findall('timestep'):
 for v in step.findall('vehicle'): rows.append({'time':step.get('time'),**v.attrib})
assert rows and max(float(r['speed']) for r in rows)<=8.001
with (OUT/'trajectory.csv').open('w') as f:
 w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
route=ET.parse(OUT/'emergency.rou.xml').getroot().find('vehicle/route').get('edges').split()
edges={e.get('id'):e for e in net.findall('edge')}
receipt={'status':'completed_actual_headless_run','source':str(SOURCE.relative_to(ROOT)),'sourceSha256':sha(SOURCE),'sourceCRS':'EPSG:4326 WGS84','location':net.find('location').attrib,'commands':commands,'sourceAnchors':{'start':agency,'target':{'osmNode':'5004274563','name':'Busan Station surface bus stop','lon':target.get('lon'),'lat':target.get('lat')}},'routeEdges':route,'routeLaneShapes':[{ 'edge':eid,'lanes':[l.attrib for l in edges[eid].findall('lane')]} for eid in route],'tripinfo':trip[0].attrib,'trajectorySamples':len(rows),'maxObservedSpeedMps':max(float(r['speed']) for r in rows),'networkEdgeCount':len(edges),'heuristics':['Default OSM typemap supplies missing speeds and lane counts; lane connections and signal programs are generated, not field calibrated.','No ramps.guess or tls.guess-signals enabled.','Coordinate endpoints snap to imported passenger-road network; no validated apparatus bay or station entrance.'],'limits':['One synthetic vehicle; no measured traffic, real emergency ETA or site calibration.','This is an independent SUMO foundation; Unity agency Dijkstra gameplay does not use this process.'],'artifacts':{p.name:sha(p) for p in OUT.iterdir() if p.is_file() and p.name!='receipt.json'}}
(OUT/'receipt.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:receipt[k] for k in ['status','tripinfo','trajectorySamples','networkEdgeCount']},indent=2))
