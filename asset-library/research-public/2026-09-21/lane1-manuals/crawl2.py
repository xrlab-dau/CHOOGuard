import re,html,urllib.request
UA={'User-Agent':'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36'}
def get(u,t=40):
    try:
        return urllib.request.urlopen(urllib.request.Request(u,headers=UA),timeout=t).read().decode('utf-8','replace')
    except Exception as e: return 'ERR '+str(e)
s=get("https://info.korail.com/info/selectBbsNttList.do?bbsNo=197&key=1341")
open('sagyu-open-list.html','w',encoding='utf-8').write(s)
print('len',len(s))
# find js func calls / links in list area
links=set(re.findall(r'(?:href|onclick)=["\']([^"\']+)["\']',s))
inter=[l for l in links if any(k in l for k in ['nttNo','Ntt','download','atchmnfl','javascript:fn'])]
for l in sorted(set(inter))[:50]: print(' L:',l)
t=re.sub(r'<script.*?</script>','',s,flags=re.S|re.I); t=re.sub(r'<style.*?</style>','',t,flags=re.S|re.I)
t=re.sub(r'<[^>]+>','|',t); t=html.unescape(t); t=re.sub(r'(\|\s*)+','|',t)
i=t.find('사규 공개')
seg=t[i:i+4000] if i>0 else t[-4000:]
print('BODY>>>',seg[:3500])
