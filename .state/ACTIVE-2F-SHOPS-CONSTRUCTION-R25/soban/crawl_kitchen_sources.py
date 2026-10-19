from pathlib import Path
import urllib.request,json,concurrent.futures
from bs4 import BeautifulSoup
from PIL import Image,ImageDraw
p=Path(__file__).parent
posts=[('jin_n_y','223369303888'),('moontop43','224005481634')]
def get(url):return urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'Mozilla/5.0','Referer':'https://blog.naver.com/'}),timeout=12).read()
def post(pair):
 who,id=pair;key=who+'-'+id;u=f'https://m.blog.naver.com/{who}/{id}'
 try:
  s=get(u).decode();(p/f'{key}.html').write_text(s);b=BeautifulSoup(s,'html.parser');c=b.select_one('.se-main-container');(p/f'{key}.txt').write_text(c.get_text('\n',strip=True));a=[]
  for t in c.select('img'):
   x=t.get('data-src')or t.get('src')
   if x and 'pstatic.net' in x:
    x=x.replace('mblogthumb-phinf.pstatic.net','blogfiles.pstatic.net').split('?')[0]
    if x not in a:a.append(x)
  (p/f'{key}-images.json').write_text(json.dumps({'url':u,'images':a},ensure_ascii=False,indent=2))
  for n,x in enumerate(a[:35]):
   try:(p/f'{key}-{n:02}.jpg').write_bytes(get(x))
   except Exception:pass
  return {'key':key,'url':u,'images':len(a)}
 except Exception as e:return {'key':key,'error':str(e)}
with concurrent.futures.ThreadPoolExecutor(max_workers=4)as ex:out=list(ex.map(post,posts))
(p/'NEW-BRANCH-SOURCES.json').write_text(json.dumps(out,ensure_ascii=False,indent=2));print(out)
for who,id in posts:
 key=who+'-'+id;fs=sorted(p.glob(key+'-??.jpg'));can=Image.new('RGB',(1000,max(190,((len(fs)+3)//4)*190)),'white');d=ImageDraw.Draw(can)
 for i,f in enumerate(fs):
  try:
   im=Image.open(f);im.thumbnail((245,165));x=(i%4)*250;y=(i//4)*190;can.paste(im,(x,y+20));d.text((x,y),f.name[-6:],fill='black')
  except:pass
 can.save(p/f'{key}-contact.jpg')
