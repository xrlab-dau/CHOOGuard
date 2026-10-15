import sys,pathlib,json,zlib,struct,hashlib,re
import olefile
from PIL import Image,ImageOps,ImageDraw
E=pathlib.Path(__file__).resolve().parent
source=E/sys.argv[1];out=E/(source.stem+'-extracted');out.mkdir(exist_ok=True)
assert olefile.isOleFile(source)
ole=olefile.OleFileIO(source);header=ole.openstream('FileHeader').read();compressed=bool(struct.unpack_from('<I',header,36)[0]&1)
texts=[];streams=[];images=[]
for parts in ole.listdir():
 name='/'.join(parts);raw=ole.openstream(parts).read();row={'name':name,'bytes':len(raw)}
 if name.startswith('BodyText/'):
  b=zlib.decompress(raw,-15) if compressed else raw;i=0
  while i+4<=len(b):
   h=struct.unpack_from('<I',b,i)[0];i+=4;tag=h&1023;size=(h>>20)&4095
   if size==4095:
    if i+4>len(b):break
    size=struct.unpack_from('<I',b,i)[0];i+=4
   content=b[i:i+size];i+=size
   if tag==67:texts.append(content.decode('utf-16le',errors='replace'))
  row['uncompressed_bytes']=len(b)
 elif name.startswith('BinData/') or name=='PrvImage':
  try:b=zlib.decompress(raw,-15)
  except zlib.error:b=raw
  target=out/name.replace('/','-');target.write_bytes(b);row.update(extracted=str(target),sha256=hashlib.sha256(b).hexdigest())
  try:
   with Image.open(target) as im:
    row['image_pixels']=list(im.size);images.append((target,im.convert('RGB').copy()))
  except Exception:pass
 streams.append(row)
t='\n'.join(texts);t=re.sub(r'[\x00-\x08\x0b\x0c\x0e-\x1f]','',t);(out/'body-text.txt').write_text(t)
hits=[]
for m in re.finditer(r'위치도|평면도|도면|설계도|4층|건물|면적|층고|단면|도면목록|과업내용|별첨',t):
 hits.append({'term':m.group(),'context':t[max(0,m.start()-90):m.end()+170]})
result={'path':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'bytes':source.stat().st_size,'container':'PASS OLE','compressed':compressed,'text_length':len(t),'streams':streams,'image_count':len(images),'term_hits':hits}
(E/(source.stem+'-inspection.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
if images:
 thumb_w,thumb_h=350,260;cols=3;rows=(len(images)+cols-1)//cols
 sheet=Image.new('RGB',(cols*thumb_w,rows*thumb_h),'white');d=ImageDraw.Draw(sheet)
 for i,(path,im) in enumerate(images):
  im.thumbnail((thumb_w-12,thumb_h-26));x=(i%cols)*thumb_w;y=(i//cols)*thumb_h;sheet.paste(im,(x+(thumb_w-im.width)//2,y+22));d.text((x+5,y+4),path.name,fill='black')
 sheet.save(E/(source.stem+'-images-contact-sheet.jpg'),quality=90)
print(json.dumps({'file':source.name,'sha256':result['sha256'],'bytes':result['bytes'],'images':len(images),'text_length':len(t),'hits':hits[:20]},ensure_ascii=False,indent=2))
