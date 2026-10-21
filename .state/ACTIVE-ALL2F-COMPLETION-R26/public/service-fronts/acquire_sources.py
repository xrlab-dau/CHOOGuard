from pathlib import Path
from urllib.parse import urlsplit,urlunsplit
from concurrent.futures import ThreadPoolExecutor
import requests,bs4,json,hashlib,io,re
from PIL import Image,ImageDraw,ImageOps
P=Path(__file__).parent
posts=[('immoanaa','224022655155'),('yungong_124','224001198372'),('daonyoung_','224410014493'),('baitfishingrod','224287119584'),('bsc3924','223947218762')]
def acquire(row):
 user,pid=row;p=P/'sources'/f'{user}-{pid}';p.mkdir(parents=True,exist_ok=True)
 url=f'https://m.blog.naver.com/{user}/{pid}';r=requests.get(url,headers={'User-Agent':'Mozilla/5.0'},timeout=25);b=bs4.BeautifulSoup(r.text,'html.parser');main=b.select_one('.se-main-container') or b
 title=(b.select_one('title').get_text(' ',strip=True) if b.select_one('title') else '')
 t=main.get_text('\n',strip=True);(p/'POST-TEXT.txt').write_text(t)
 urls=[]
 for im in main.select('img'):
  u=im.get('data-src') or im.get('src') or ''
  if 'pstatic.net' not in u or u in urls:continue
  u=urlunsplit((*urlsplit(u)[:3],'type=w966',''));urls.append(u)
 rec={'post':url,'title':title,'text':t,'images':[]}
 for i,u in enumerate(urls[:35]):
  try:
   rr=requests.get(u,headers={'User-Agent':'Mozilla/5.0','Referer':url},timeout=20);rr.raise_for_status();im=Image.open(io.BytesIO(rr.content)); ext='.png' if im.format=='PNG' else '.jpg';fn=f'SOURCE-{i:02d}{ext}';(p/fn).write_bytes(rr.content)
   rec['images'].append({'index':i,'source_url':u,'file':str(p/fn),'bytes':len(rr.content),'sha256':hashlib.sha256(rr.content).hexdigest(),'dimensions':im.size,'publisher_resampled':True})
  except Exception as e:rec['images'].append({'index':i,'source_url':u,'error':str(e)})
 ims=[x for x in rec['images'] if 'file'in x]; cols=4;tw=310;th=250;out=Image.new('RGB',(cols*tw,((len(ims)+cols-1)//cols)*th),'white');d=ImageDraw.Draw(out)
 for j,x in enumerate(ims):
  im=Image.open(x['file']).convert('RGB');im.thumbnail((tw,th-22));xx=(j%cols)*tw;yy=(j//cols)*th;out.paste(im,(xx+(tw-im.width)//2,yy+22));d.text((xx+5,yy+3),str(x['index']),fill='black')
 if ims:out.save(p/'SOURCE-CONTACT.jpg',quality=85)
 (p/'SOURCE-INDEX.json').write_text(json.dumps(rec,ensure_ascii=False,indent=2));return {'post':url,'title':title,'images':len(ims),'text':t[:7000]}
res=list(ThreadPoolExecutor(max_workers=5).map(acquire,posts));(P/'ACQUISITION-INDEX.json').write_text(json.dumps(res,ensure_ascii=False,indent=2))
for a in res: print(json.dumps(a,ensure_ascii=False))
with (P/'findings.md').open('a') as f:f.write('\nNaver public search yielded direct lost-property visits (immoanaa/yungong_124/daonyoung_) and broader station facility visit (bsc3924); original mobile post texts and source images retained with hashes. Subway/Seomyeon sources excluded as wrong station/function.\n')
