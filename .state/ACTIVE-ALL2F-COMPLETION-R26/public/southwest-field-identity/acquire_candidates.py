import requests,json,pathlib,concurrent.futures,re
from bs4 import BeautifulSoup
base=pathlib.Path(__file__).parent
jobs=[('busan-menu','https://menu.busan.go.kr/shop/info/CD_SHP_001186?langSn=1&tp=si'),('siksin-dain','https://www.siksinhot.com/P/1585899'),('diningcode-dain','https://www.diningcode.com/profile.php?rid=f7u8zHeSvhtu'),('naver-hydestocker','https://m.blog.naver.com/hydestocker/223458733873'),('naver-bomi0306','https://m.blog.naver.com/bomi0306/223232405825'),('naver-yw1388na','https://m.blog.naver.com/yw1388na/223316372232'),('naver-youp6141','https://m.blog.naver.com/youp6141/224347259624'),('naver-mely1004','https://m.blog.naver.com/mely1004/224042458325')]
def job(x):
 name,url=x;out=base/'sources'/name;out.mkdir(parents=True,exist_ok=True)
 try:
  r=requests.get(url,headers={'User-Agent':'Mozilla/5.0'},timeout=25);(out/'source.html').write_text(r.text)
  soup=BeautifulSoup(r.text,'html.parser');body=soup.select_one('.se-main-container') or soup
  txt=body.get_text(' ',strip=True);(out/'text.txt').write_text(txt)
  ims=[]
  for im in body.select('img'):
   u=im.get('data-src') or im.get('src') or ''
   if 'blog' in name or 'naver' in name:
    if not ('postfiles' in u or 'blogfiles' in u):continue
   if u and u not in ims:ims.append(u)
  dates=[m.get('content') for m in soup.select('meta[property]') if 'time' in m.get('property','') or 'date' in m.get('property','')]
  res={'id':name,'url':url,'status':r.status_code,'title':soup.title.get_text() if soup.title else '', 'dates':dates,'images':ims,'text':txt[:20000]}
  (out/'SOURCE-INDEX.json').write_text(json.dumps(res,ensure_ascii=False,indent=2))
  return res
 except Exception as e:return {'id':name,'error':str(e)}
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as p:results=list(p.map(job,jobs))
(base/'CANDIDATE-ACQUISITION-R26.json').write_text(json.dumps(results,ensure_ascii=False,indent=2))
for r in results:
 print('\n',r['id'],r.get('status'),r.get('dates'),r.get('title'),'images',len(r.get('images',[])))
 print(r.get('text','')[:5000])
