import json,gzip,requests,concurrent.futures
from pathlib import Path
B=Path(__file__).resolve().parent;S=B/'sources';A=S/'models';A.mkdir(exist_ok=True)
M=json.load(gzip.open(B.parent/'free-assets/objaverse-object-paths.json.gz','rt'))
ids={'tray':'6551259392dc4ee19c6064d9b51d936a','chicken':'511db88f24e04eb1be8b0c0c080e1e5e','spatula':'bc5b0e14e5454d1db4bfc5f8027bde6f'}
def get(k,uid):
 p=A/k;p.mkdir(exist_ok=True);d=json.loads((S/(uid+'-MODEL.json')).read_text());(p/'AUTHOR-METADATA.json').write_text(json.dumps(d,ensure_ascii=False,indent=2))
 im=max(d['thumbnails']['images'],key=lambda x:x['width']);r=requests.get(im['url'],timeout=45);r.raise_for_status();(p/'source-preview.jpg').write_bytes(r.content)
 u='https://huggingface.co/datasets/allenai/objaverse/resolve/main/'+M[uid];r=requests.get(u,timeout=90);r.raise_for_status();(p/(k+'.glb')).write_bytes(r.content)
 (p/'ACQUISITION.json').write_text(json.dumps({'source_url':d['viewerUrl'],'uid':uid,'author':d['user']['displayName'],'license':d['license'],'public_mirror':u,'source_preserved':True},ensure_ascii=False,indent=2));print(k,len(r.content),flush=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as e:list(e.map(lambda x:get(*x),ids.items()))
