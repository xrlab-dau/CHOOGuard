import concurrent.futures,datetime,hashlib,io,json,pathlib,re,sys,urllib.parse,urllib.request
from PIL import Image,ImageDraw
folder=pathlib.Path(__file__).parent
root=pathlib.Path('/Users/um-yunsang/CHOOGuard')
dest=root/'asset-library/research-public/2026-10-09/floor2-completion-pass2'
dest.mkdir(parents=True,exist_ok=True)
jobs=[]
for id in sys.argv[1:]:
 d=json.loads((folder/(id+'.json')).read_text());seen=set()
 for im in d['images']:
  url=im['full']
  if url in seen:continue
  seen.add(url)
  if 'primary-instagram' in id and im['n'] not in [1,2]:continue
  if not ('mblogthumb-phinf.pstatic.net' in url or 'blogfiles.pstatic.net' in url or 'k.kakaocdn.net' in url or 'blog.kakaocdn.net' in url or 't1.daumcdn.net' in url or 'cdninstagram.com' in url):continue
  if 'profile' in url.lower():continue
  if 'mblogthumb-phinf.pstatic.net' in url:url=url.replace('mblogthumb-phinf.pstatic.net','blogfiles.pstatic.net').split('?')[0]
  if 'blog.kakaocdn.net' in url or 't1.daumcdn.net' in url:
   parsed=urllib.parse.urlparse(url); q=urllib.parse.parse_qs(parsed.query)
   if q.get('fname'):url=q['fname'][0]
  jobs.append({'id':id+'-'+str(im['n']).zfill(3),'source_id':id,'source_url':d['url'],'source_image_dom_url':im['full'],'url':url,'publication_text':d['text'][:650]})
def get(job):
 try:
  req=urllib.request.Request(job['url'],headers={'User-Agent':'Mozilla/5.0','Referer':job['source_url']})
  with urllib.request.urlopen(req,timeout=25) as r:data=r.read();ctype=r.headers.get('Content-Type');final=r.url
  im=Image.open(io.BytesIO(data));fmt=im.format.lower();ext='jpg' if fmt=='jpeg' else fmt
  path=dest/(job['id']+'.'+ext);path.write_bytes(data)
  return {**job,'path':str(path.relative_to(root)),'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data),'image_dimensions':list(im.size),'content_type':ctype,'final_url':final,'acquired_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'license':'public personal blog photograph; reuse license not established','role':'reference_only','capture_date_evidence':'not independently verified'}
 except Exception as ex:return {**job,'error':str(ex)}
with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:results=list(pool.map(get,jobs))
out=folder/('receipts-'+ '-'.join(sys.argv[1:])+'.json');out.write_text(json.dumps(results,ensure_ascii=False,indent=2))
for id in sys.argv[1:]:
 rows=[r for r in results if r['source_id']==id and 'path' in r]
 tw,th=300,270;cols=4;sheet=Image.new('RGB',(cols*tw,((len(rows)+cols-1)//cols)*th),'white');dr=ImageDraw.Draw(sheet)
 for j,r in enumerate(rows):
  im=Image.open(root/r['path']).convert('RGB');im.thumbnail((tw-10,th-35));x=(j%cols)*tw;y=(j//cols)*th;sheet.paste(im,(x,y));dr.text((x+4,y+th-30),r['id']+' '+str(r['image_dimensions']),fill='black')
 if rows:sheet.save(folder/(id+'-contact-sheet.jpg'),quality=85)
 print(id,'downloaded',len(rows),'errors',len([r for r in results if r['source_id']==id and 'error' in r]))
print(out)
