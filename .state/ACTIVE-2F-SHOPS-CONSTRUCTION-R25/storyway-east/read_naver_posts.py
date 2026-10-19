from urllib.request import Request,urlopen
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from html import unescape
import json,re
P=Path(__file__).parent/'sources'
urls=json.loads((P/'naver-specific-result-links.json').read_text())
def get(u):
 try:
  u=u.replace('https://blog.naver.com/','https://m.blog.naver.com/')
  s=urlopen(Request(u,headers={'User-Agent':'Mozilla/5.0'}),timeout=12).read().decode('utf-8')
  text=unescape(re.sub('<[^>]+>',' ',s));text=re.sub(r'\s+',' ',text)
  title=re.search('<title>(.*?)</title>',s).group(1)
  ctx=[text[max(0,m.start()-120):m.start()+260] for m in list(re.finditer('편의점|스토리웨이|Storyway|StoryWay',text))[:8]]
  images=list(dict.fromkeys(re.findall(r'https://[^\s"<>]+pstatic\.net[^\s"<>]+',s)))
  key=u.split('/')[-2]+'-'+u.split('/')[-1]
  if ctx:(P/(key+'.html')).write_text(s)
  return {'key':key,'url':u,'title':title,'context':ctx,'images':images[:50]}
 except Exception as e:return {'url':u,'error':str(e)}
with ThreadPoolExecutor(max_workers=6) as ex:a=list(ex.map(get,urls[:12]))
(P/'naver-specific-posts.json').write_text(json.dumps(a,ensure_ascii=False,indent=2))
for x in a:print(x.get('key'),x.get('title'),json.dumps(x.get('context',[]),ensure_ascii=False)[:1300])
