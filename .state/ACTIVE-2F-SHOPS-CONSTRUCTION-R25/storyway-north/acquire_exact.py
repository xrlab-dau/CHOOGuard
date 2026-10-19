from pathlib import Path
from bs4 import BeautifulSoup
import requests,json,concurrent.futures
from PIL import Image,ImageDraw
p=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/storyway-north/sources');headers={'User-Agent':'Mozilla/5.0'}
posts=[('jihui__','223586630991'),('spuoo01','223512272756'),('wnqkf517','223299896747'),('daytour_88','223486414067')]
rows=[]
for b,n in posts:
 url=f'https://m.blog.naver.com/{b}/{n}';resp=requests.get(url,headers=headers,timeout=30);(p/f'{b}-{n}.html').write_text(resp.text);s=BeautifulSoup(resp.text,'html.parser');cs=s.select('.se-component');seen=set(); picks=[]
 for i,c in enumerate(cs):
  if any(t in c.get_text(' ',strip=True) for t in ['스토리웨이','편의점']):
   for cc in cs[max(0,i-2):i+3]:
    for im in cc.select('img.se-image-resource'):
     u=im.get('data-src',im.get('src','')).split('?')[0]
     if u and u not in seen:seen.add(u);picks.append(u)
 print(b,len(picks))
 for i,u in enumerate(picks):rows.append({'post':url,'file':str(p/f'{b}-{i:02}.jpg'),'url':u+'?type=w966'})
def download(x):
 f=Path(x['file'])
 if not f.exists():
  rr=requests.get(x['url'],headers=headers,timeout=30);rr.raise_for_status();f.write_bytes(rr.content)
 try:Image.open(f).verify();return x
 except:return None
with concurrent.futures.ThreadPoolExecutor(max_workers=6) as ex:rows=[x for x in ex.map(download,rows) if x]
(p/'exact-photo-index.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2))
for start in range(0,len(rows),24):
 sub=rows[start:start+24];canvas=Image.new('RGB',(1200,225*((len(sub)+3)//4)),'white');d=ImageDraw.Draw(canvas)
 for i,x in enumerate(sub):
  im=Image.open(x['file']);im.thumbnail((296,200));cx=i%4*300;cy=i//4*225;canvas.paste(im,(cx,cy));d.text((cx,cy+201),str(start+i)+' '+Path(x['file']).stem,fill='black')
 canvas.save(p/f'exact-contact-{start:02}.jpg')
print('total',len(rows))
