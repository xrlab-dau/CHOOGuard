import requests,json,concurrent.futures,pathlib,urllib.parse
from bs4 import BeautifulSoup
base=pathlib.Path(__file__).parent
queries=['부산역 다인명가 경주빵 찰보리빵','부산역 다인명가 꽃들 부산오뎅','부산역 다인 명가 2026','부산역 2층 맞이방 다인명가 경주빵 찰보리빵','부산역 다인명가 매장 사진','부산역 다인명가 폐점 크리스피']
def query(q):
 r=requests.get('https://search.naver.com/search.naver',params={'query':q,'where':'web'},headers={'User-Agent':'Mozilla/5.0'},timeout=25)
 soup=BeautifulSoup(r.text,'html.parser');out=[];seen=set()
 for a in soup.select('a[href]'):
  u=a['href'];t=a.get_text(' ',strip=True)
  if not t or u in seen or not u.startswith('http') or any(x in u for x in ['search.naver','adcr.naver','nid.naver','shopping.naver','help.naver','policy.naver','help.naver','ssl.pstatic.net']):continue
  seen.add(u);out.append({'url':u,'title':t[:650]})
 return {'query':q,'status':r.status_code,'links':out}
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as p:results=list(p.map(query,queries))
(base/'DAIN-EXACT-NAVER-SEARCH-R26.json').write_text(json.dumps(results,ensure_ascii=False,indent=2))
for r in results:
 print('\nQUERY',r['query'])
 for x in r['links'][:24]:print(x['url'],x['title'][:180])
