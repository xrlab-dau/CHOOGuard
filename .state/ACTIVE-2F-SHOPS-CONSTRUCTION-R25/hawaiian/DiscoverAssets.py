from pathlib import Path
import requests,json,gzip,concurrent.futures
p=Path(__file__).parent
mapping=json.load(gzip.open(p.parent/'free-assets/objaverse-object-paths.json.gz','rt'))
def get(q):
 d=requests.get('https://api.sketchfab.com/v3/search',params={'type':'models','q':q,'downloadable':'true','count':20},timeout=30).json()
 (p/'sources'/('ASSET-SEARCH-'+q.replace(' ','-')+'.json')).write_text(json.dumps(d))
 return [{'uid':x['uid'],'name':x['name'],'author':x['user']['displayName'],'mirror':mapping.get(x['uid']),'license':x.get('license',{}).get('slug'),'triangles':x['faceCount'],'preview':x.get('thumbnails',{}).get('images',[])[-1].get('url')} for x in d.get('results',[])]
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as ex:
 for q,r in zip(['rattan chair','bentwood chair','wicker pendant'],ex.map(get,['rattan chair','bentwood chair','wicker pendant'])):print(q,json.dumps(r,ensure_ascii=False))
with (p/'findings.md').open('a') as f:f.write('\n- Full tkvlf14 04/05/06/07/08/11/17 viewed. Correct field furniture: black/cream woven bistro chairs plus curved bentwood chairs; circular pedestal tables; multiple rattan hanging shades; long open wood bakery island, not a fully glass-enclosed display. Commercial open refrigerated wall cabinet, espresso machine, visible basins and products. Side rear passage is actual Exit10 access and must not be closed.\n- Broad web original-design search returned no exact branch floor/ceiling/elevation/furniture plan; source photos are branch/date-bound. Metadata-based asset preview paths differ from source-preview convention; corrected to actual preview-large.jpg.\n')
