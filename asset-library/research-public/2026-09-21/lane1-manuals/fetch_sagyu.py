import re,html,urllib.request,urllib.parse,os,json,time,sys
UA={'User-Agent':'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36'}
def get(u,t=60,raw=False):
    for _ in range(3):
        try:
            r=urllib.request.urlopen(urllib.request.Request(u,headers=UA),timeout=t)
            return (r.read(),dict(r.headers)) if raw else r.read().decode('utf-8','replace')
        except Exception as e: err=e; time.sleep(1)
    return (b'',{'ERR':str(err)}) if raw else 'ERR '+str(err)
TARGETS=[1220,1219,2501,2614,2641,2598,1135,1158,1124,2493,17860,1189,1211,2565,1100,
         1146,2573,14688,15415,1177,1233,1179,1181,1180,2602,20169,2604,25028,1142,1130,1156,1159,1098,1086]
out={}
for ntt in TARGETS:
    u=f"https://info.korail.com/info/selectBbsNttView.do?key=1341&bbsNo=197&nttNo={ntt}&searchCtgry=&searchCnd=all&searchKrwd=&integrDeptCode=&pageIndex=1"
    s=get(u)
    if s.startswith('ERR'): print(ntt,'PAGEERR'); continue
    files=re.findall(r'downloadBbsFile\.do\?atchmnflNo=(\d+)',s)
    # title
    m=re.search(r'<title>(.*?)</title>',s,re.S)
    # try to get the post title from the detail table
    m2=re.search(r'(?:규정명|제목)[^<]*</th>\s*<td[^>]*>(.*?)</td>',s,re.S)
    t=re.sub(r'<[^>]+>','',m2.group(1)) if m2 else ''
    t=html.unescape(t).strip()[:80]
    out[ntt]={'title':t,'files':sorted(set(files),key=int)}
    print(ntt,t,'FILES:',out[ntt]['files'])
json.dump(out,open('sagyu-targets.json','w'),ensure_ascii=False,indent=1)
