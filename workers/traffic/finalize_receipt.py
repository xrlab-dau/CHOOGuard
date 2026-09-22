import ctypes as C, pathlib,json,csv,math,subprocess
root=pathlib.Path(__file__).resolve().parents[2]; out=root/'workers/traffic/reference-network'; r=json.loads((out/'receipt.json').read_text())
lib=next((root/'.tools/sumo/.venv/lib').glob('python*/site-packages/sumo_data/.libs/libproj*.dylib')); p=C.CDLL(str(lib))
p.proj_create.argtypes=[C.c_void_p,C.c_char_p];p.proj_create.restype=C.c_void_p
class Coord(C.Union): _fields_=[('v',C.c_double*4)]
p.proj_trans.argtypes=[C.c_void_p,C.c_int,Coord];p.proj_trans.restype=Coord
pr=p.proj_create(None,r['location']['projParameter'].encode());assert pr
off=list(map(float,r['location']['netOffset'].split(',')))
rows=list(csv.DictReader((out/'trajectory.csv').open()));checks={}
for name,a,row in [('start',r['sourceAnchors']['start'],rows[0]),('target',r['sourceAnchors']['target'],rows[-1])]:
 c=Coord();c.v[:]=[math.radians(float(a['lon'])),math.radians(float(a['lat'])),0,0];xy=p.proj_trans(pr,1,c).v
 x,y=xy[0]+off[0],xy[1]+off[1]
 checks[name]={'sourceProjectedLocalMetres':[x,y],'sampleLocalMetres':[float(row['x']),float(row['y'])],'sampleDistanceToSourceAnchorMetres':math.hypot(float(row['x'])-x,float(row['y'])-y),'sampleTime':row['time']}
report=json.loads((root/'.tools/sumo/install-report.json').read_text())
packages=[{'name':v['metadata']['name'],'version':v['metadata']['version'],'url':v['download_info']['url'],'sha256':v['download_info']['archive_info']['hashes']['sha256']} for v in report['install']]
versions={n:subprocess.check_output([str(root/'.tools/sumo/.venv/bin'/n),'--version'],text=True).splitlines()[0] for n in ['sumo','netconvert','duarouter']}
g={'status':'INSTALLED_AND_USED_HEADLESS','scope':'Independent next traffic-engine foundation; no Unity/core modifications or integration claim','packages':packages,'verifiedBinaryVersions':versions,'primarySources':['https://sumo.dlr.de/docs/Downloads.php','https://pypi.org/pypi/eclipse-sumo/1.27.1/json','https://pypi.org/pypi/sumo-data/1.27.1/json','https://sumo.dlr.de/docs/Networks/Import/OpenStreetMap.html','https://sumo.dlr.de/docs/Simulation/Emergency.html'],'receipt':'workers/traffic/reference-network/receipt.json','reproduction':'workers/traffic/reproduce.py','network':'workers/traffic/reference-network/busan.net.xml','trajectory':['workers/traffic/reference-network/trajectory.xml','workers/traffic/reference-network/trajectory.csv'],'sourceSha256':r['sourceSha256'],'location':r['location'],'actualCoordinateInspection':checks,'result':r['tripinfo'],'trajectorySamples':r['trajectorySamples'],'maxObservedSpeedMps':r['maxObservedSpeedMps'],'validation':'Actual netconvert, duarouter and SUMO runs exit 0; tripinfo arrival positive; 169 FCD records; speed <=8.001. Software fixture evidence only.','limitations':r['heuristics']+r['limits']+['Original OSM bound includes outlying relation nodes; actual converted extent is convBoundary. Import warnings retained.','Departure and arrival use mapped road edge positions, not exact institution or stop coordinate; coordinate distances retained.'],'plan':{'install':'complete','networkImport':'complete','singleVehicleRun':'complete'},'probeNotes':['Optional coordinate CRS database probe failed because bundled PROJ database was not configured. Direct native projection string transform succeeded without database; no simulation rerun required.']}
(root/'.planning/2026-09-21-official-guide-benchmark/sumo-tooling-graph.json').write_text(json.dumps(g,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(checks,indent=2))
