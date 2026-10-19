import requests,json,gzip,hashlib,concurrent.futures,shutil
from pathlib import Path
R=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/flowers');S=R/'sources';rec=[]
def get(url,p):
 p.parent.mkdir(parents=True,exist_ok=True)
 r=requests.get(url,headers={'User-Agent':'Mozilla/5.0'},timeout=120);r.raise_for_status();p.write_bytes(r.content)
 return {'url':url,'path':str(p.resolve()),'bytes':len(r.content),'sha256':hashlib.sha256(r.content).hexdigest()}
jobs=[]
mp=json.load(gzip.open('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/free-assets/objaverse-object-paths.json.gz'))
for i,key in [('f1a984ec17b94cdd860b5e7c4fe40170','rose_bouquet'),('f7fde2608214417da63ed65351fa69b7','white_shelf'),('dec75836e8c14c179be89e4420fa7a97','modern_shelf'),('149916856f534f409974c85192d1e23b','display_cabinet')]:
 if i not in mp: print('NO_PUBLIC_MIRROR',key);continue
 if not (S/(i+'-metadata.json')).exists():
  d=requests.get('https://api.sketchfab.com/v3/models/'+i,timeout=30).json();(S/(i+'-metadata.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2))
 d=json.load(open(S/(i+'-metadata.json')));print(key,d.get('license',{}).get('slug'))
 if d.get('license',{}).get('slug') not in ['by','cc0']:continue
 jobs.append(('https://huggingface.co/datasets/allenai/objaverse/resolve/main/'+mp[i],S/key/(key+'.glb')))
 thumbs=d.get('thumbnails',{}).get('images',[])
 if thumbs:jobs.append((thumbs[-1]['url'],S/key/'source_preview.jpg'))
for n in ['potted_plant_01','potted_plant_04','ceramic_vase_01','wicker_basket_02']:
 d=json.load(open(S/(n+'-files.json')));b=d['fbx']['1k']['fbx'];print(n,b['url'],b['size']);jobs.append((b['url'],S/n/(n+'.fbx')))
 for name,v in b.get('include',{}).items():
  if '_diff_' in name or '_alpha_' in name or '_nor_gl_' in name:jobs.append((v['url'],S/n/Path(name).name))
 jobs.append(('https://cdn.polyhaven.com/asset_img/primary/'+n+'.png?height=400',S/n/'source_preview.png'))
with concurrent.futures.ThreadPoolExecutor(max_workers=6) as ex:
 fs={ex.submit(get,url,p):(url,p) for url,p in jobs}
 for f in concurrent.futures.as_completed(fs):
  try:r=f.result();rec.append(r);print('DOWNLOADED',Path(r['path']).name,r['bytes'],flush=True)
  except Exception as e:print('ERROR',fs[f],str(e),flush=True)
(R/'DOWNLOAD-RECEIPTS.json').write_text(json.dumps(rec,ensure_ascii=False,indent=2))
for name in ['RYOKOU-20240315-FLOWERS-FRONT.jpg','PRIMARY-VIDEO-LOCAL-335s.jpg','PRIMARY-VIDEO-LOCAL-328s.jpg']:
 shutil.copy2(Path('.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/research/flowers-entry/sources')/name,S/name)
print('DONE')
