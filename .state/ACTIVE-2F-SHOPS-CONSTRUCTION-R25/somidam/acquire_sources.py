import requests,json,hashlib,gzip,concurrent.futures
from pathlib import Path
p=Path(__file__).parent/'sources';p.mkdir(exist_ok=True)
s=requests.Session();s.headers['User-Agent']='Mozilla/5.0'
j=json.loads((p/'somidam-lsknim.json').read_text());jobs=[]
for i in [3,4,5,7,8,9,10,11,12,13]:
 im=j['images'][i];u=im.get('full',im['src']).split('?')[0]+'?type=w1600';jobs.append((u,f'lsknim-original-{i:03}.jpg'))
j=json.loads((p/'somidam-primary-instagram.json').read_text())
for i in [1,2]:jobs.append((j['images'][i]['full'],f'official-open-{i}.jpg'))
jobs += [('https://cdn.imweb.me/thumbnail/20260803/6b98d2a046ad3.png','official-logo.png'),('https://api.sketchfab.com/v3/models/867570f94d654385bb9750248849892b','case-author-license.json'),('https://huggingface.co/datasets/allenai/objaverse/resolve/main/glbs/000-004/867570f94d654385bb9750248849892b.glb','Cake-Display.glb')]
m=json.load(gzip.open(p.parent.parent/'free-assets/objaverse-object-paths.json.gz'))
uid='bab05bc7805d4b0dbc2cdcbb48441956';jobs.append(('https://huggingface.co/datasets/allenai/objaverse/resolve/main/'+m[uid],'Coffee-Machine.glb'))
def get(v):
 u,n=v
 try:
  r=s.get(u,timeout=90);r.raise_for_status();(p/n).write_bytes(r.content)
  return {'url':u,'file':str(p/n),'bytes':len(r.content),'sha256':hashlib.sha256(r.content).hexdigest()}
 except Exception as e:return {'url':u,'error':str(e)}
with concurrent.futures.ThreadPoolExecutor(max_workers=5) as ex:r=list(ex.map(get,jobs))
(p/'ACQUISITION-R25.json').write_text(json.dumps(r,indent=2));print(json.dumps(r,indent=2))
