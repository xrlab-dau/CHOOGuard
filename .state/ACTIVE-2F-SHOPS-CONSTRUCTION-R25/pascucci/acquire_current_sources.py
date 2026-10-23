import pathlib,json,requests,re,concurrent.futures,datetime
from bs4 import BeautifulSoup
from PIL import Image,ImageDraw
p=pathlib.Path(__file__).parent/'sources'
for source_id in [0,15,20]:
 html=(p/f'focused-{source_id:02d}.html').read_text();s=BeautifulSoup(html,'html.parser');main=s.select_one('.se-main-container');links=[]
 for a in main.select('a[data-linktype=img]'):
  d=json.loads(a['data-linkdata']);u=d['src']+'?type=w800'
  if u not in links:links.append(u)
 date=re.search(r"postWriteDate\s*=\s*'([0-9]+)'",html);date=datetime.datetime.fromtimestamp(int(date.group(1))/1000,datetime.timezone.utc).isoformat() if date else 'notfound'
 def get(z):
  j,u=z;r=requests.get(u,headers={'User-Agent':'Mozilla/5.0','Referer':'https://m.blog.naver.com/'},timeout=25);(p/f'current2026-{source_id:02d}-{j:02d}.jpg').write_bytes(r.content)
 with concurrent.futures.ThreadPoolExecutor(max_workers=8) as e:list(e.map(get,enumerate(links)))
 url=[x['url'] for x in json.loads((p/'focused-candidates-index.json').read_text()) if x['i']==source_id][0]
 (p/f'current2026-{source_id:02d}-index.json').write_text(json.dumps({'url':url,'published_utc':date,'urls':links},ensure_ascii=False,indent=2))
 out=Image.new('RGB',(1280,250*((len(links)+3)//4)),'white')
 for j in range(len(links)):
  im=Image.open(p/f'current2026-{source_id:02d}-{j:02d}.jpg').convert('RGB');im.thumbnail((310,225));out.paste(im,((j%4)*320,25+(j//4)*250));ImageDraw.Draw(out).text(((j%4)*320+5,(j//4)*250+5),f'{source_id:02d}-{j:02d}',fill='black')
 out.save(p/f'current2026-{source_id:02d}-contact.jpg');print(source_id,date,len(links))
