from pathlib import Path
import requests,json,shutil
from bs4 import BeautifulSoup
from PIL import Image,ImageOps,ImageDraw
r=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25'); o=r/'storyway-north/sources'
p=r/'storyway-east/sources/djagytt-224289573890.html';shutil.copy2(p,o/p.name)
s=BeautifulSoup(p.read_text(),'html.parser'); ims=s.select('img.se-image-resource');rows=[]
for i,im in enumerate(ims[:13]):
 u=im.get('data-src',im.get('src','')).split('?')[0]+'?type=w966';f=o/f'djagytt-20260518-{i:02}.jpg'
 if not f.exists():
  rr=requests.get(u,headers={'User-Agent':'Mozilla/5.0'},timeout=30);rr.raise_for_status();f.write_bytes(rr.content)
 rows.append({'index':i,'url':u,'file':str(f)})
canvas=Image.new('RGB',(1200,260*((len(rows)+3)//4)), 'white');d=ImageDraw.Draw(canvas)
for i,x in enumerate(rows):
 im=Image.open(x['file']);im.thumbnail((296,232));cx=(i%4)*300;cy=(i//4)*260;canvas.paste(im,(cx,cy));d.text((cx,cy+234),str(i),fill='black')
canvas.save(o/'tour-first13-contact.jpg');(o/'tour-first13-index.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2))
