from pathlib import Path
from bs4 import BeautifulSoup
from concurrent.futures import ThreadPoolExecutor
import requests,json,re,sys,hashlib,datetime
p=Path(__file__).parent
args=json.loads(sys.argv[1])
def run(x):
 num,cnd,word,page=x;params={'bbsId':'BBSMSTR_000000000027','searchCnd':str(cnd),'searchWrd':word,'pageIndex':str(page)}
 try:
  r=requests.get('https://www.korailretail.com/bbs/selectBbsList.do',params=params,headers={'User-Agent':'Mozilla/5.0'},timeout=28)
  f=p/f'FILTER-{num:02}-{cnd}-{page}.html';f.write_bytes(r.content);s=BeautifulSoup(r.text,'html.parser');items=[]
  for a in s.select('a[href]'):
   z=re.search(r"inquire_articledetail\('([^']+)',\s*'([^']+)'\)",a.get('href',''))
   if z:
    parent=a.parent
    for k in range(5):
     t=parent.get_text(' ',strip=True)
     if re.search(r'20\d\d-\d\d-\d\d',t):break
     if parent.parent is None:break
     parent=parent.parent
    ds=re.findall(r'20\d\d-\d\d-\d\d',t)
    items.append({'title':a.get_text(' ',strip=True),'nttId':z[2],'date':ds[0] if ds else None,'row':t[:600]})
  t=s.get_text(' ',strip=True);m=re.search(r'총\s*게시물\s*:\s*(\d+)',t)
  return {'number':num,'params':params,'url':r.url,'status':r.status_code,'bytes':len(r.content),'sha256':hashlib.sha256(r.content).hexdigest(),'total':int(m[1]) if m else None,'file':str(f),'items':items}
 except Exception as e:return {'number':num,'params':params,'error':str(e)}
res=list(ThreadPoolExecutor(max_workers=3).map(run,args))
old=[];idx=p/'FILTERED-ARCHIVE-INDEX.json'
if idx.exists():old=json.loads(idx.read_text())
old.extend(res);idx.write_text(json.dumps(old,ensure_ascii=False,indent=2))
for r in res:
 print('FILTER',r['number'],r['params'],'TOTAL',r.get('total'),'STATUS',r.get('status',r.get('error')))
 for it in r.get('items',[]):print(it['nttId'],it['date'],it['title'])
with (p/'progress.md').open('a') as f:f.write('\nRetrieved targeted archive filter requests '+','.join(str(x[0]) for x in args)+'. Saved actual HTML and extracted title/date/notice index.\n')
