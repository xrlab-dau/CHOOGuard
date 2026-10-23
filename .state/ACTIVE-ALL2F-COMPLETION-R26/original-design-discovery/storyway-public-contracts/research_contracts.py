from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from bs4 import BeautifulSoup
import requests,json,sys,datetime
p=Path(__file__).parent
queries=[
'부산역 직영편의점 리뉴얼공사 설계도서',
'부산역 스토리웨이 리모델링 전기 설계',
'부산역 편의점 환경개선 도면 코레일유통',
'부산역 신맞이방 편의점 리뉴얼 공사',
'부산역 부산맞이 스토리웨이 설계 공사',
'부산역 스토리웨이 리뉴얼공사 나라장터',
'부산역 편의점 리모델링 site:korailretail.com',
'부산역 편의점 설계 site:g2b.go.kr',

'부산역 편의점 리뉴얼 코레일유통 2024',
'부산역 스토리웨이 공사 코레일유통 2025 2026',
'부산역 맞이방 편의점 전기내선공사',
'부산역 신맞이방 편의점 환경개선 설계도서',
'부산 2층 신맞이 직영편의점 설계 공사',
'부산역 편의점 리뉴얼공사 site:korailretail.com/bbs',
'부산역 편의점 공사 site:g2b.go.kr 2024 2025',
'부산역 편의점 설계도서 코레일유통 2026',
'부산역 맞이방 파스쿠찌 편의점 리뉴얼 공사',
'부산역 스토리웨이 매장 환경개선 입찰 도면',
'부산역 직영편의점 전기공사 도면 2024 2025 2026',
'부산역 직영편의점 리뉴얼 설계 코레일유통 입찰공고'
]
a,b=map(int,sys.argv[1:3])
def fetch(i):
 q=queries[i]
 try:
  r=requests.get('https://search.naver.com/search.naver',params={'query':q,'where':'web'},headers={'User-Agent':'Mozilla/5.0'},timeout=22)
  soup=BeautifulSoup(r.text,'html.parser');out=[];seen=set()
  for n in soup.select('a[href]'):
   u=n['href'];t=n.get_text(' ',strip=True)
   if len(t)<7 or u in seen or not u.startswith('http') or any(k in u for k in ['adcr.naver','search.naver.com','nid.naver','help.naver','search.shopping']):continue
   seen.add(u);out.append({'title':t[:330],'url':u})
  return {'number':i+1,'query':q,'status':r.status_code,'results':out[:45]}
 except Exception as e:return {'number':i+1,'query':q,'error':str(e)}
res=list(ThreadPoolExecutor(max_workers=4).map(fetch,range(a,b)))
fn=p/f'QUERIES-{a+1:02}-{b:02}.json';fn.write_text(json.dumps(res,ensure_ascii=False,indent=2))
for x in res:
 print('\nQUERY',x['number'],x['query'])
 for z in x.get('results',[])[:12]: print(z['title'][:125],z['url'])
with (p/'progress.md').open('a') as f:f.write(f'\nRan qualified public operator/procurement queries {a+1}..{b}; saved {fn.name}.\n')
