from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from bs4 import BeautifulSoup
from urllib.parse import urlencode
import requests,json,re,hashlib,zipfile,io,sys
p=Path(__file__).parent
ids=json.loads(sys.argv[1])
def run(notice):
 out=[];s=requests.Session();u='https://www.korailretail.com/bbs/selectBbsDetail.do';r=s.get(u,params={'bbsId':'BBSMSTR_000000000027','nttId':notice,'pageIndex':'1'},headers={'User-Agent':'Mozilla/5.0'},timeout=28)
 (p/f'NOTICE-{notice}.html').write_bytes(r.content);soup=BeautifulSoup(r.text,'html.parser');txt=soup.get_text(' ',strip=True)
 links=[]
 for a in soup.select('a[href]'):
  t=a.get_text(' ',strip=True);h=a['href']
  if not ('도면' in t or '설계' in t):continue
  m=re.search(r"fn_egov_downFile\('([^']+)'\s*,\s*'([^']+)'\)",h)
  if m:link='https://www.korailretail.com/cmm/fms/FileDown.do?'+urlencode({'atchFileId':m[1],'fileSn':m[2]})
  else:
   m=re.search(r"downFile\('([^']+)'\s*,\s*'([^']+)'\s*,\s*'([^']+)'\)",h)
   if not m:continue
   link='https://www.korailretail.com/board/fileDown2.do?'+urlencode({'dirName':m[1],'fileName':m[2],'originName':m[3]},safe='/')
  links.append({'title':t,'url':link,'source_javascript':h})
 for k,a in enumerate(links):
  try:
   z=s.get(a['url'],headers={'User-Agent':'Mozilla/5.0','Referer':r.url},timeout=45)
   rec={'notice':notice,'notice_url':r.url,'attachment':a,'status':z.status_code,'bytes':len(z.content),'content_type':z.headers.get('content-type'),'magic_hex':z.content[:8].hex(),'sha256':hashlib.sha256(z.content).hexdigest(),'extracted':[]}
   if z.content.startswith(b'PK'):
    folder=p/f'package-{notice}';folder.mkdir(exist_ok=True);fp=folder/f'attachment-{k:02}.zip';fp.write_bytes(z.content);rec['file']=str(fp)
    zz=zipfile.ZipFile(io.BytesIO(z.content))
    for i,zi in enumerate(zz.infolist()):
     if zi.is_dir():continue
     name=zi.filename
     try:name=name.encode('cp437').decode('cp949')
     except (UnicodeEncodeError,UnicodeDecodeError):pass
     ext=Path(name).suffix.lower()
     if ext not in ['.pdf','.dwg','.dxf','.hwp','.hwpx','.xls','.xlsx','.jpg','.jpeg','.png']:continue
     data=zz.read(zi);f=folder/f'FILE-{i:02}{ext}';f.write_bytes(data)
     rec['extracted'].append({'original_name':name,'file':str(f),'bytes':len(data),'magic_hex':data[:8].hex(),'sha256':hashlib.sha256(data).hexdigest()})
   else:rec['qualification']='Attachment request returned non-ZIP; no plan file claimed.'
   out.append(rec)
  except Exception as e:out.append({'notice':notice,'attachment':a,'error':str(e)})
 return {'nttId':notice,'notice_url':r.url,'notice_bytes':len(r.content),'bid_numbers':sorted(set(re.findall(r'20\d{9}(?:-\d{3})?|R2[0-9][A-Z]{2}\d+',txt))),'notice_relevant_excerpt':txt[max(txt.find('공사명')-100,0):max(txt.find('공사명')-100,0)+2700],'attachments':out}
res=list(ThreadPoolExecutor(max_workers=3).map(run,ids));idx=p/'ACQUIRED-PACKAGES.json';old=json.loads(idx.read_text()) if idx.exists() else [];old.extend(res);idx.write_text(json.dumps(old,ensure_ascii=False,indent=2))
for x in res:
 print('NOTICE',x['nttId'],'BID',x['bid_numbers'])
 for a in x['attachments']:
  print('  ATTACHMENT',a.get('status'),a.get('bytes'),a.get('qualification',a.get('error','')),a['attachment']['title'])
  for z in a.get('extracted',[]):print('    ',z['original_name'],z['bytes'],z['file'])
with (p/'progress.md').open('a') as f:f.write('\nInspected exact matched notice packages '+','.join(ids)+'; recorded file magic/SHA/source notice URLs, extracting only relevant document/model formats.\n')
