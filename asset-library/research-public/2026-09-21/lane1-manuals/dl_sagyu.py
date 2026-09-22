import re,urllib.request,urllib.parse,os,json,time
UA={'User-Agent':'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36'}
tg=json.load(open('sagyu-targets.json'))
os.makedirs('korail-sagyu',exist_ok=True)
man=[]
for ntt,info in tg.items():
    for fid in info['files']:
        u=f"https://info.korail.com/info/downloadBbsFile.do?atchmnflNo={fid}"
        try:
            r=urllib.request.urlopen(urllib.request.Request(u,headers=UA),timeout=90)
            data=r.read(); cd=r.headers.get('Content-Disposition','')
            m=re.search(r'filename="?([^";]+)',cd)
            fn=urllib.parse.unquote(m.group(1)).strip() if m else f'{fid}.bin'
            fn=re.sub(r'[/\\]','_',fn)
            path=f'korail-sagyu/{ntt}_{fid}_{fn}'
            open(path,'wb').write(data)
            man.append({'nttNo':ntt,'atchmnflNo':fid,'filename':fn,'bytes':len(data),'path':path})
            print(f'{len(data):>9} {path}')
        except Exception as e: print('FAIL',ntt,fid,e)
        time.sleep(0.15)
json.dump(man,open('korail-sagyu/manifest.json','w'),ensure_ascii=False,indent=1)
print('TOTAL',len(man),'files',sum(x['bytes'] for x in man),'bytes')
