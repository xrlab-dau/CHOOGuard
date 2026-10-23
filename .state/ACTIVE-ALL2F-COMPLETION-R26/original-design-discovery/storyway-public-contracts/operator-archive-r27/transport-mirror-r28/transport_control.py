from pathlib import Path
from urllib.parse import quote
from bs4 import BeautifulSoup
import requests,json,re,hashlib,zipfile,io
p=Path(__file__).parent;base='https://www.korailretail.com';rows=[];s=requests.Session()
headers={'User-Agent':'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36','Accept':'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8','Accept-Language':'ko-KR,ko;q=0.9,en-US;q=0.8,en;q=0.7','Upgrade-Insecure-Requests':'1','Sec-Fetch-Dest':'document','Sec-Fetch-Mode':'navigate','Sec-Fetch-Site':'same-origin','Connection':'keep-alive'}
def get(key,u,ref=None):
 h=dict(headers)
 if ref:h['Referer']=ref
 r=s.get(u,headers=h,timeout=28)
 z={'number':len(rows)+1,'key':key,'url':u,'status':r.status_code,'bytes':len(r.content),'mime':r.headers.get('content-type'),'magic_hex':r.content[:12].hex(),'sha256':hashlib.sha256(r.content).hexdigest(),'session_cookie_present':any(c.name=='JSESSIONID' for c in s.cookies),'method':'Same requests.Session; actual current-page JS URL; raw slash dirname; modern Chrome full document/Accept/Accept-Language/Sec-Fetch headers, versus prior minimal Mozilla/5.0.'}
 if r.content.startswith(b'PK'):
  f=p/(key+'.zip');f.write_bytes(r.content);z['file']=str(f);z['members']=[];zz=zipfile.ZipFile(io.BytesIO(r.content))
  for i,x in enumerate(zz.infolist()):
   if x.is_dir():continue
   n=x.filename
   try:n=n.encode('cp437').decode('cp949')
   except (UnicodeEncodeError,UnicodeDecodeError):pass
   data=zz.read(x);ext=Path(n).suffix.lower()
   if ext not in ['.pdf','.dwg','.dxf','.hwp','.hwpx','.xls','.xlsx']:continue
   f=p/f'{key}-FILE{i:02}{ext}';f.write_bytes(data);z['members'].append({'original_name':n,'file':str(f),'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest(),'magic_hex':data[:12].hex()})
 if r.content.startswith(b'%PDF'):z['valid_pdf']=True
 rows.append(z);(p/'TRANSPORT-CONTROL-RESPONSES.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2))
 print(key,z['status'],z['bytes'],z['mime'],z['magic_hex'],flush=True)
 return r
ctrl=base+'/board/boardDetail.do?boardTypeNum=4&boardNum=100083825'
r=get('current-control-page',ctrl);(p/'CURRENT-CONTROL-PAGE.html').write_bytes(r.content)
js=[]
for x in BeautifulSoup(r.text,'html.parser').find_all('script'):
 t=x.get_text()
 for term in ['function downFile','function fn_egov_downFile']:
  i=t.find(term)
  if i>=0:js.append(t[i:t.find('}',i)+1])
(p/'ACTUAL-CURRENT-DOWNLOAD-JAVASCRIPT.json').write_text(json.dumps({'source_url':ctrl,'functions':js,'inference':'Published JS does raw dirname/fileName concatenation and encodeURI(originName), not extra/double encoding; matches previous endpoints. Minimal-UA versus full document request is the only new transport method tested.'},ensure_ascii=False,indent=2))
u=base+'/board/fileDown2.do?dirName=upload/recru/4/202609/&fileName=202610140217538275.pdf&originName='+quote('[붙임3]대상매장위치도.pdf')
r=get('current-valid-control-map',u,ctrl)
if r.content.startswith(b'%PDF'):
 existing=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/krispy-southwest/sources/R26-OFFICIAL100083825-MAP.pdf');rows[-1]['existing_retained_control_file']=str(existing);rows[-1]['equal_existing_bytes']=existing.read_bytes()==r.content
notice=base+'/bbs/selectBbsDetail.do?bbsId=BBSMSTR_000000000027&nttId=100042164&pageIndex=1'
r=get('north2018-parent',notice,ctrl)
soup=BeautifulSoup(r.text,'html.parser');a=next((a for a in soup.select('a[href]') if '도면' in a.get_text() and 'downFile' in a['href']),None)
if a:
 m=re.search(r"downFile\('([^']+)'\s*,\s*'([^']+)'\s*,\s*'([^']+)'\)",a['href']);url=base+'/board/fileDown2.do?dirName='+m[1]+'&fileName='+quote(m[2])+'&originName='+quote(m[3]);get('north2018-full-browser-headers',url,notice)
notice=base+'/bbs/selectBbsDetail.do?bbsId=BBSMSTR_000000000027&nttId=100079965&pageIndex=1'
r=get('lounge2023-parent',notice,ctrl);soup=BeautifulSoup(r.text,'html.parser');a=next((a for a in soup.select('a[href]') if '도면' in a.get_text() and 'downFile' in a['href']),None)
if a:
 m=re.search(r"fn_egov_downFile\('([^']+)'\s*,\s*'([^']+)'\)",a['href']);url=base+'/cmm/fms/FileDown.do?atchFileId='+m[1]+'&fileSn='+m[2];get('lounge2023-full-browser-headers',url,notice)
(p/'TRANSPORT-CONTROL-RESPONSES.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2))
with (p/'progress.md').open('a') as f:f.write('\nExecuted '+str(len(rows))+' documented new full-browser-header control/old-attachment requests, with actual current JS and existing PDF byte comparison; saved MIME/magic/SHA and no cookie values.\n')
