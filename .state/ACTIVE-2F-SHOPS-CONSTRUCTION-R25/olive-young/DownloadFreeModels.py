import requests,json,gzip,concurrent.futures,hashlib
from pathlib import Path
b=Path(__file__).parent/'sources/free-models'; paths=json.load(gzip.open(b.parents[2]/'free-assets/objaverse-object-paths.json.gz','rt'))
ids={'makeup':'97038c2b5b7d44b1851ba9e33fbd20d7','pump':'2be81dcbd5824aa6a5c6f596ca7c9a63','lipstick':'3a22a4359df74375a297d962926f0359','foil-pouch':'1e639d03d97045f6a3b6ca1954e762bf'}
s=requests.Session();s.headers['User-Agent']='Mozilla/5.0'
def work(item):
 key,uid=item;p=b/key;p.mkdir(exist_ok=True);r=s.get('https://api.sketchfab.com/v3/models/'+uid,timeout=30);r.raise_for_status();d=r.json();(p/'SKETCHFAB-SOURCE.json').write_text(json.dumps(d,ensure_ascii=False,indent=2));
 assert d['isDownloadable'] and d['license']['slug'] in ['by','cc0']
 thumb=max(d['thumbnails']['images'],key=lambda x:x['width']);rr=s.get(thumb['url'],timeout=30);(p/'source-preview.jpg').write_bytes(rr.content)
 rr=s.get('https://huggingface.co/datasets/allenai/objaverse/resolve/main/'+paths[uid],timeout=150);rr.raise_for_status();(p/(key+'.glb')).write_bytes(rr.content)
 return {'key':key,'name':d['name'],'author':d['user']['displayName'],'license':d['license']['url'],'bytes':len(rr.content),'sha256':hashlib.sha256(rr.content).hexdigest()}
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as ex:
 for out in ex.map(work,ids.items()):print(json.dumps(out,ensure_ascii=False),flush=True)
