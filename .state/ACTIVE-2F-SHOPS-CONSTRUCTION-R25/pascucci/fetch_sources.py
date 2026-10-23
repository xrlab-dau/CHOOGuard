import requests,bs4,re,json,pathlib,concurrent.futures
from PIL import Image,ImageOps,ImageDraw
p=pathlib.Path(__file__).parent/'sources'
urls=json.loads((p/'branch-blog-urls.json').read_text())
urls=[urls[i] for i in [0,3,4,5]]
def fetch(pair):
 i,u=pair; u=u.replace('https://blog.','https://m.blog.'); t=requests.get(u,headers={'User-Agent':'Mozilla/5.0'},timeout=30).text;(p/f'interior-{i}.html').write_text(t);s=bs4.BeautifulSoup(t,'html.parser');main=s.select_one('.se-main-container') or s
 print(i,u,main.get_text(' ',strip=True)[:2500]);photos=[]
 for im in main.find_all('img'):
  z=im.get('data-src') or im.get('src','')
  if 'postfiles' in z or 'blogfiles' in z:
   z=z.split('?')[0]+'?type=w1600'
   if z not in photos:photos.append(z)
 for n,z in enumerate(photos):
  try:
   b=requests.get(z,headers={'User-Agent':'Mozilla/5.0'},timeout=20).content;(p/f'interior-{i}-{n:02d}.jpg').write_bytes(b)
  except Exception as e: print('download',str(e))
 (p/f'interior-{i}-index.json').write_text(json.dumps({'url':u,'photos':photos},ensure_ascii=False,indent=2))
 cells=[]
 for n in range(len(photos)):
  try:
   im=Image.open(p/f'interior-{i}-{n:02d}.jpg').convert('RGB');im.thumbnail((300,220));c=Image.new('RGB',(310,245),'#eeeeee');c.paste(im,((310-im.width)//2,20));ImageDraw.Draw(c).text((6,4),f'{i}-{n:02d}',fill='black');cells.append(c)
  except:pass
 if cells:
  out=Image.new('RGB',(310*4,245*((len(cells)+3)//4)),'white')
  for n,c in enumerate(cells):out.paste(c,((n%4)*310,(n//4)*245))
  out.save(p/f'interior-{i}-contact.jpg')
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as e:list(e.map(fetch,enumerate(urls)))
