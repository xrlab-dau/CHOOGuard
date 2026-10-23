from pathlib import Path
import requests,json,concurrent.futures
from bs4 import BeautifulSoup
from PIL import Image,ImageDraw
b=Path(__file__).parent/'sources';s=requests.Session();s.headers['User-Agent']='Mozilla/5.0'
for name,url in [('bbumpbum','https://m.blog.naver.com/bbumpbum/222725829482'),('ellie','https://m.blog.naver.com/ellie0116/224433385586')]:
 t=s.get(url,timeout=35).text;(b/f'{name}.html').write_text(t);h=BeautifulSoup(t,'html.parser');ims=[]
 for im in h.select('img.se-image-resource'):
  u=im.get('data-lazy-src') or im.get('src','');u=u.split('?')[0]+'?type=w966'
  if u not in ims:ims.append(u)
 (b/f'{name}-urls.json').write_text(json.dumps(ims,indent=2));(b/f'{name}.txt').write_text(h.get_text(' ',strip=True))
 def dl(a):
  i,u=a;r=s.get(u,timeout=30);(b/f'{name}-{i:02}.jpg').write_bytes(r.content)
 with concurrent.futures.ThreadPoolExecutor(max_workers=8) as ex:list(ex.map(dl,enumerate(ims)))
 out=Image.new('RGB',(1000,((len(ims)+3)//4)*170),'white');d=ImageDraw.Draw(out)
 for i in range(len(ims)):
  try:
   im=Image.open(b/f'{name}-{i:02}.jpg');im.thumbnail((244,146));x=(i%4)*250;y=(i//4)*170;out.paste(im,(x,y));d.text((x,y+148),str(i),fill='black')
  except:pass
 out.save(b/f'{name}-contact.jpg');print(name,len(ims),h.get_text(' ',strip=True)[-7000:])
