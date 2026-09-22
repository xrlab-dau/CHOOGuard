import re,html,urllib.request,json,time,sys
UA={'User-Agent':'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36'}
def get(u,t=40):
    for _ in range(3):
        try: return urllib.request.urlopen(urllib.request.Request(u,headers=UA),timeout=t).read().decode('utf-8','replace')
        except Exception as e:
            err='ERR '+str(e); time.sleep(1)
    return err
rows=[]
for p in range(1,34):
    u=f"https://info.korail.com/info/selectBbsNttList.do?key=1341&bbsNo=197&searchCtgry=&searchCnd=all&searchKrwd=&integrDeptCode=&pageIndex={p}"
    s=get(u)
    if s.startswith('ERR'): print('page',p,s); continue
    # each row: link with nttNo then title text
    for m in re.finditer(r'selectBbsNttView\.do[^"\']*?nttNo=(\d+)[^"\']*?["\'][^>]*>(.*?)</a>',s,re.S):
        ntt=m.group(1); title=re.sub(r'<[^>]+>','',m.group(2)); title=html.unescape(title).strip()
        title=re.sub(r'\s+',' ',title)
        if title: rows.append((int(ntt),title))
    sys.stderr.write(f'p{p}:{len(rows)} ')
seen={}
for n,t in rows: seen.setdefault(n,t)
print('TOTAL UNIQUE', len(seen))
json.dump(seen,open('sagyu-index.json','w'),ensure_ascii=False,indent=0)
for n,t in sorted(seen.items()): print(n,t)
