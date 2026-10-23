import requests,json,gzip,hashlib,concurrent.futures
from pathlib import Path
p=Path(__file__).parent/'sources/models';p.mkdir(exist_ok=True)
mp=json.load(gzip.open(Path(__file__).parent.parent/'free-assets/objaverse-object-paths.json.gz','rt'))
ids=['8d2fb8b8215f41fba59d0ec4606fb6b3','e76cb526238a493c90255ad4a900f83d','bab05bc7805d4b0dbc2cdcbb48441956']
def get(uid):
 d=requests.get('https://api.sketchfab.com/v3/models/'+uid,timeout=30).json();folder=p/uid;folder.mkdir(exist_ok=True);(folder/'metadata.json').write_text(json.dumps(d))
 ims=d['thumbnails']['images'];im=max(ims,key=lambda a:a.get('width',0));r=requests.get(im['url'],timeout=30);r.raise_for_status();(folder/'preview.jpg').write_bytes(r.content)
 lic=d.get('license',{});print(uid,d['name'],d['user']['displayName'],lic,flush=True)
 if lic.get('slug') not in ['by','cc0']:return
 url='https://huggingface.co/datasets/allenai/objaverse/resolve/main/'+mp[uid];r=requests.get(url,timeout=90);r.raise_for_status();fn=folder/(uid+'.glb');fn.write_bytes(r.content)
 (folder/'ACQUISITION.json').write_text(json.dumps({'model':d['name'],'author':d['user']['displayName'],'source_url':d['viewerUrl'],'api':'https://api.sketchfab.com/v3/models/'+uid,'license':lic,'download_url':url,'bytes':len(r.content),'sha256':hashlib.sha256(r.content).hexdigest()},ensure_ascii=False,indent=2))
 print('DOWNLOADED',uid,len(r.content),flush=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as ex:list(ex.map(get,ids))
