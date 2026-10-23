from pathlib import Path
import json,requests,concurrent.futures,io
from urllib.parse import urlsplit,urlunsplit
from PIL import Image,ImageOps,ImageDraw
B=Path('.state/ACTIVE-ALL2F-COMPLETION-R26/public/photo-booth');D=B/'sources';D.mkdir(exist_ok=True)
f=Path('.state/ACTIVE-ALL2F-COMPLETION-R26/public/nursing-room/dorossi_life-224304316699/SOURCE-POST.json');p=json.loads(f.read_text());urls=[]
for u in p['images']:
 if 'mblog' not in u or u.split('?')[0] in [x.split('?')[0] for x in urls]:continue
 urls.append(u)
def get(t):
 i,u=t;u=u.split('?')[0]+'?type=w966';r=requests.get(u,headers={'User-Agent':'Mozilla/5.0','Referer':p['url']},timeout=25)
 try:im=Image.open(io.BytesIO(r.content));im.load()
 except:return {'index':i,'url':u,'status':r.status_code,'not_image':True}
 out=D/f'dorossi-{i:02}.jpg';im.convert('RGB').save(out,quality=94)
 return {'index':i,'url':u,'status':r.status_code,'file':str(out),'pixels':list(im.size)}
with concurrent.futures.ThreadPoolExecutor(max_workers=6) as ex:rows=list(ex.map(get,enumerate(urls)))
(D/'DOROSSI-SOURCE-INDEX.json').write_text(json.dumps({'source':p['url'],'images':rows},ensure_ascii=False,indent=2))
imgs=[r for r in rows if r.get('file')];w=400;h=325;sheet=Image.new('RGB',(w*5,h*((len(imgs)+4)//5)),'white');dr=ImageDraw.Draw(sheet)
for n,r in enumerate(imgs):
 im=Image.open(r['file']);im.thumbnail((390,295));x=(n%5)*w;y=(n//5)*h;sheet.paste(im,(x,y+25));dr.text((x+5,y+5),str(r['index']),fill='black')
sheet.save(D/'DOROSSI-SOURCE-CONTACT.jpg',quality=91)
print('downloaded',len(imgs))
