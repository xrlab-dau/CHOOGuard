from pathlib import Path
from bs4 import BeautifulSoup
import requests,json,hashlib,concurrent.futures
from PIL import Image,ImageDraw
B=Path(__file__).parent;P=B/'field-20260518-2F';P.mkdir(exist_ok=True)
s=BeautifulSoup((B/'storyway-east/sources/djagytt-224289573890.html').read_text(),'html.parser');body=s.select_one('.se-main-container');imgs=body.select('img.se-image-resource');ixs=list(range(5,13))+list(range(21,39))
def f(i):
 im=imgs[i];u=(im.get('data-lazy-src') or im.get('data-src') or im.get('src')).split('?')[0]+'?type=w966';r=requests.get(u,headers={'User-Agent':'Mozilla/5.0'},timeout=30);r.raise_for_status();p=P/f'field-{i:02}.jpg';p.write_bytes(r.content);return {'post_image_index':i,'url':u,'file':str(p),'bytes':len(r.content),'sha256':hashlib.sha256(r.content).hexdigest()}
rs=[]
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as ex:
 for v in ex.map(f,ixs):rs.append(v)
for name,ids in [('BNC-WEST-HWANG-FRONT-ROOMS',list(range(5,13))),('NORTH-HALL-FRONTAGES',list(range(21,39)))]:
 out=Image.new('RGB',(1000,((len(ids)+3)//4)*170),'white');d=ImageDraw.Draw(out)
 for n,i in enumerate(ids):
  im=Image.open(P/f'field-{i:02}.jpg');im.thumbnail((245,145));x=n%4*250;y=n//4*170;out.paste(im,(x,y));d.text((x,y+148),str(i),fill='black')
 out.save(P/(name+'-contact.jpg'))
(P/'SOURCE-INDEX.json').write_text(json.dumps({'source_url':'https://m.blog.naver.com/djagytt/224289573890','published':'2026-05-18','images':rs,'text_semantic_groups':{'5-12':'Author explicitly2F: BNC/Hwang immediatelyfrom1Fescalator; publicexitFlowers/Boksoon; outside2FChungGimbap','21-30':'2FmainhallshopfrontagesGijang/Donas/Pharmacy/Hawaiian/Krispy/Pascucci/Somidam/retiringKakaoFriends','31-38':'2FNorthPortconnectionStoryLamant/WeenyBeeny/StarlightSand/OliveYoung'},'source_epoch_not_current_allstores':'KakaoFriendsclosedMay2026, replacedbyArtboxcurrentJune-July; do notreviveoldbrand','no_metric_placement_authority':True},ensure_ascii=False,indent=2))
print('Common exact2F fieldwalk photos staged',len(rs),sum(x['bytes'] for x in rs))
