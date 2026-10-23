import pathlib,requests,json,re,concurrent.futures
from bs4 import BeautifulSoup
p=pathlib.Path(__file__).parent/'sources';urls=json.loads((p/'naver-2026-exactbranch-urls.json').read_text());results=[]
def get(z):
 i,u=z;u=u.replace('https://blog.','https://m.blog.');r=requests.get(u,headers={'User-Agent':'Mozilla/5.0'},timeout=25);s=BeautifulSoup(r.text,'html.parser');main=s.select_one('.se-main-container');t=main.get_text(' ',strip=True) if main else ''
 title=s.title.get_text() if s.title else '';d={'i':i,'url':u,'title':title,'length':len(t),'branch_mentions':t.count('부산역'),'text_excerpt':t[:500]};
 if '부산역' in t and '파스쿠찌' in t:
  (p/f'followup-candidate-{i:02d}.html').write_text(r.text);(p/f'followup-candidate-{i:02d}.txt').write_text(t)
  d['matches']=[t[max(0,m.start()-80):m.end()+180] for m in re.finditer('부산역',t)][:5]
 print(json.dumps(d,ensure_ascii=False));return d
with concurrent.futures.ThreadPoolExecutor(max_workers=8) as ex:results=list(ex.map(get,enumerate(urls)))
(p/'followup-2026-candidates.json').write_text(json.dumps(results,ensure_ascii=False,indent=2))
