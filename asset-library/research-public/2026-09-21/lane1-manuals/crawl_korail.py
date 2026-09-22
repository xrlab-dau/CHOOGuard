import re,html,urllib.request,urllib.parse,json,sys,time
UA={'User-Agent':'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36'}
def get(u,t=30):
    try:
        r=urllib.request.Request(u,headers=UA)
        return urllib.request.urlopen(r,timeout=t).read().decode('utf-8','replace')
    except Exception as e:
        return 'ERR '+str(e)
# 1. find 사규 공개 menu page key
for key in [1341,1342,1340,1343,1339]:
    u=f"https://info.korail.com/info/selectBbsNttList.do?bbsNo=197&key={key}"
    s=get(u)
    ti=re.search(r'<title>(.*?)</title>',s,re.S)
    print(key, (ti.group(1).strip()[:80] if ti else s[:80]))
