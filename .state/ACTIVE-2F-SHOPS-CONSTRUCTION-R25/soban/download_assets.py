import json,gzip,urllib.request,concurrent.futures
from pathlib import Path
P=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/soban/downloaded-models');M=json.load(gzip.open('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/free-assets/objaverse-object-paths.json.gz','rt'))
assets=[('Restaurant-Kitchen-Set-Part1','129dd2f84879477ea261b422d21491f4'),('RiceCooker-Supor','a9bf09f4f333493580d75b4bde98f9cb'),('RiceCooker-Candidate2','3f70c8b7cf704bdb952e09c0d5f5ab65')]
def get(u):return urllib.request.urlopen(urllib.request.Request(u,headers={'User-Agent':'Mozilla/5.0'}),timeout=40).read()
def fetch(a):
 n,u=a;out={'name':n,'uid':u,'path':M[u]}
 try:
  d=json.loads(get('https://api.sketchfab.com/v3/models/'+u));(P/(n+'-METADATA.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2));out.update(license=d.get('license'),triangles=d.get('faceCount'),credit=d.get('user',{}).get('displayName'));thumb=max(d['thumbnails']['images'],key=lambda x:x.get('size',0))['url'];(P/(n+'-preview.jpg')).write_bytes(get(thumb));(P/(n+'.glb')).write_bytes(get('https://huggingface.co/datasets/allenai/objaverse/resolve/main/'+M[u]));out['bytes']=(P/(n+'.glb')).stat().st_size
 except Exception as e:out['error']=str(e)
 return out
with concurrent.futures.ThreadPoolExecutor(max_workers=3)as ex:out=list(ex.map(fetch,assets))
(P/'ACQUISITION-ADDITIONAL-R25.json').write_text(json.dumps(out,ensure_ascii=False,indent=2));print(json.dumps(out,ensure_ascii=False,indent=2))
