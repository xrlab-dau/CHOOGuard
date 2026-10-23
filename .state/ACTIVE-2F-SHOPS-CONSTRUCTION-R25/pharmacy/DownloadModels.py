import requests,json,gzip,concurrent.futures
from pathlib import Path
b=Path(__file__).parent/'sources';s=requests.Session();s.headers['User-Agent']='Mozilla/5.0';m=json.load(gzip.open('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/free-assets/objaverse-object-paths.json.gz','rt'))
models=[('store-shelves','8406794ee19b4af49e0ccc02d1132915'),('medicine-carton','024cb1b771c649cebfa940d894d018c8'),('medicines','c071c69e73b0495692703cae47d21611')]
for name,uid in models:
 p=b/name;p.mkdir(exist_ok=True);j=s.get(f'https://api.sketchfab.com/v3/models/{uid}',timeout=30).json();(p/'AUTHOR-METADATA.json').write_text(json.dumps(j,indent=2));thumb=max(j['thumbnails']['images'],key=lambda a:a['width'])['url'];(p/'source-preview.jpg').write_bytes(s.get(thumb,timeout=30).content)
 u='https://huggingface.co/datasets/allenai/objaverse/resolve/main/'+m[uid];r=s.get(u,timeout=60);(p/(name+'.glb')).write_bytes(r.content)
 (p/'ACQUISITION.json').write_text(json.dumps({'title':j['name'],'author':j['user']['displayName'],'source':j['viewerUrl'],'license':j['license'],'downloadable':j.get('isDownloadable'),'mirror':u,'bytes':len(r.content),'status':r.status_code},indent=2));print(name,r.status_code,len(r.content),j.get('license'))
