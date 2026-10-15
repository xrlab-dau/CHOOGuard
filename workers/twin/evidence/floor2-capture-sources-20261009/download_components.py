import json,urllib.request,hashlib,datetime,io,sys
from pathlib import Path
from PIL import Image,ImageDraw
p=Path(sys.argv[1]);label=sys.argv[2];limit=int(sys.argv[3]) if len(sys.argv)>3 else 24
s=p.read_text();d=json.loads(next(x for x in s.splitlines() if x.startswith('{')))
p.with_suffix('.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
out=Path('asset-library/research-public/2026-10-09/floor2-guide-passages');rp=out/'receipts.json';rec=json.loads(rp.read_text());imgs=[]
for c in d.get('components',[]):
 for i in c.get('images',[]): imgs.append({'component':c['n'],**i})
if not imgs:
 for i in d.get('images',[]):imgs.append(i)
for n,i in enumerate(imgs[:limit]):
 src=i.get('full') or i.get('src');url=src.split('?')[0].replace('mblogthumb-phinf.pstatic.net','blogfiles.pstatic.net')
 try:
  req=urllib.request.Request(url,headers={'User-Agent':'Mozilla/5.0'})
  with urllib.request.urlopen(req,timeout=20) as res:data=res.read();ct=res.headers.get('Content-Type','')
  im=Image.open(io.BytesIO(data));dest=out/f'{label}-{n:03}.jpg';dest.write_bytes(data)
  rec.append({'source_url':d['url'],'url':url,'source_image_dom_url':src,'path':str(dest),'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data),'content_type':ct,'acquired_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'license':'public personal/operator blog photo; reuse license not established','role':'reference_only','image_dimensions':list(im.size),'component':i.get('component'),'publication_date_evidence':d.get('text','')[:600],'capture_date_evidence':'not independently validated'})
  print(n,im.size,len(data))
 except Exception as e:print('SKIP',n,type(e).__name__,str(e)[:100])
rp.write_text(json.dumps(rec,ensure_ascii=False,indent=2)+'\n')
files=sorted(out.glob(label+'-*.jpg'));tiles=[]
for f in files:
 im=Image.open(f).convert('RGB');im.thumbnail((310,230));tile=Image.new('RGB',(330,265),'white');tile.paste(im,((330-im.width)//2,0));ImageDraw.Draw(tile).text((5,240),f.name,fill='black');tiles.append(tile)
canvas=Image.new('RGB',(330*4,265*((len(tiles)+3)//4)),(220,220,220))
for n,t in enumerate(tiles):canvas.paste(t,((n%4)*330,(n//4)*265))
if files: canvas.save(p.parent/(label+'-contact-sheet.jpg'),quality=88)
print('files',len(files))
