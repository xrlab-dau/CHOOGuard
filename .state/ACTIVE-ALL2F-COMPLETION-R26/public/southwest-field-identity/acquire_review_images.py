from pathlib import Path
import requests,json,concurrent.futures,urllib.parse
from PIL import Image,ImageDraw,ImageOps
b=Path(__file__).parent/'sources/naver-place-serp';d=json.load(open(b/'PHOTO-IMAGE-INDEX.json'));us=[]
for x in d:
 u=urllib.parse.parse_qs(urllib.parse.urlparse(x['url']).query).get('src',[''])[0]
 if ('pup-review-phinf.pstatic.net' in u or 'ldb-phinf.pstatic.net' in u) and u not in us:us.append(u)
def job(x):
 i,u=x;p=b/f'REVIEW-FIELD-{i:02}.jpg'
 try:
  r=requests.get(u,headers={'User-Agent':'Mozilla/5.0'},timeout=20);p.write_bytes(r.content)
  im=Image.open(p);return {'index':i,'url':u,'path':str(p),'status':r.status_code,'dimensions':im.size}
 except Exception as e:return {'index':i,'url':u,'error':str(e)}
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:out=list(pool.map(job,enumerate(us)))
(b/'DOWNLOADED-REVIEW-ORIGINALS.json').write_text(json.dumps(out,ensure_ascii=False,indent=2))
cols=4;w=1200;h=((len(out)+cols-1)//cols)*260;canvas=Image.new('RGB',(w,h),'white');draw=ImageDraw.Draw(canvas)
for x in out:
 if 'path' not in x:continue
 i=x['index'];im=Image.open(x['path']).convert('RGB');im.thumbnail((295,232));xx=(i%cols)*300;yy=(i//cols)*260;canvas.paste(im,(xx,yy+24));draw.text((xx+5,yy+5),f"{i:02} {x['dimensions']}",fill='black')
canvas.save(b/'ALL-REVIEW-FIELD-SOURCE-CONTACT.jpg')
print('downloaded',len(out),sum('error' in x for x in out),'errors')
