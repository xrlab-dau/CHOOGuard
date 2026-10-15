import pathlib,json,struct,io,re,hashlib,collections,sys
import olefile
from PIL import Image,ImageDraw
E=pathlib.Path(__file__).resolve().parent;prefix=sys.argv[1];f=E/(prefix+'.xls');out=E/(prefix+'-extracted');out.mkdir(exist_ok=True)
o=olefile.OleFileIO(f);b=o.openstream('Workbook').read();records=[];i=0
while i+4<=len(b):
 tag,n=struct.unpack_from('<HH',b,i);records.append((tag,i,b[i+4:i+4+n]));i+=4+n
class Chunks:
 def __init__(self,chunks):self.chunks=chunks;self.i=0;self.p=0
 def raw(self,n):
  out=b''
  while n:
   if self.p==len(self.chunks[self.i]):self.i+=1;self.p=0
   take=min(n,len(self.chunks[self.i])-self.p);out+=self.chunks[self.i][self.p:self.p+take];self.p+=take;n-=take
  return out
 def chars(self,n,wide):
  out=[]
  while n:
   if self.p==len(self.chunks[self.i]):self.i+=1;self.p=0;wide=bool(self.raw(1)[0]&1)
   avail=(len(self.chunks[self.i])-self.p)//(2 if wide else 1)
   take=min(n,avail);assert take
   out.append(self.raw(take*(2 if wide else 1)).decode('utf-16le' if wide else 'latin1'));n-=take
  return ''.join(out)
sst=[];sheets=[];groups=[]
for ix,(tag,offset,data) in enumerate(records):
 if tag==0x85:
  bof=struct.unpack_from('<I',data)[0];ln=data[6];wide=bool(data[7]&1);name=data[8:8+ln*(2 if wide else 1)].decode('utf-16le' if wide else 'latin1');sheets.append({'name':name,'bof':bof,'cells':[],'drawing_records':0})
 if tag in (0xfc,0xeb):
  chunks=[data];j=ix+1
  while j<len(records) and records[j][0]==0x3c:chunks.append(records[j][2]);j+=1
  if tag==0xeb:groups.append(b''.join(chunks));continue
  r=Chunks(chunks);total,unique=struct.unpack('<II',r.raw(8))
  for _ in range(unique):
   ln=struct.unpack('<H',r.raw(2))[0];flags=r.raw(1)[0];runs=struct.unpack('<H',r.raw(2))[0] if flags&8 else 0;ext=struct.unpack('<I',r.raw(4))[0] if flags&4 else 0
   sst.append(r.chars(ln,bool(flags&1)));r.raw(runs*4+ext)
  assert len(sst)==unique
by_bof={s['bof']:s for s in sheets};current=None
def rk(v):
 n=struct.unpack('<i',struct.pack('<I',v))[0]>>2 if v&2 else struct.unpack('<d',struct.pack('<II',0,v&0xfffffffc))[0]
 return n/100 if v&1 else n
for tag,offset,d in records:
 if offset in by_bof:current=by_bof[offset]
 if current is None:continue
 if tag==0xec:current['drawing_records']+=1
 if tag==0xfd:
  row,col,xf,idx=struct.unpack('<HHHI',d);current['cells'].append({'row':row+1,'col':col+1,'value':sst[idx]})
 elif tag==0x203:
  row,col=struct.unpack_from('<HH',d);current['cells'].append({'row':row+1,'col':col+1,'value':struct.unpack_from('<d',d,6)[0]})
 elif tag==0x27e:
  row,col=struct.unpack_from('<HH',d);current['cells'].append({'row':row+1,'col':col+1,'value':rk(struct.unpack_from('<I',d,6)[0])})
 elif tag==0xbd:
  row,col=struct.unpack_from('<HH',d)
  for k,at in enumerate(range(4,len(d)-2,6)):current['cells'].append({'row':row+1,'col':col+k+1,'value':rk(struct.unpack_from('<I',d,at+2)[0])})
 elif tag==6 and len(d)>=14 and d[12:14]!=b'\xff\xff':
  row,col=struct.unpack_from('<HH',d);current['cells'].append({'row':row+1,'col':col+1,'cached_formula_value':struct.unpack_from('<d',d,6)[0]})
images=[];hashes=set()
for gidx,g in enumerate([b''.join(groups)]):
 for m in re.finditer(b'\x89PNG\r\n\x1a\n|\xff\xd8\xff',g):
  try:
   with Image.open(io.BytesIO(g[m.start():])) as im:
    im.load();fmt=im.format;buf=io.BytesIO();im.save(buf,format=fmt);blob=buf.getvalue();h=hashlib.sha256(blob).hexdigest()
    if h in hashes:continue
    hashes.add(h);target=out/('image'+str(len(images)+1)+('.png' if fmt=='PNG' else '.jpg'));target.write_bytes(blob);images.append({'path':str(target),'pixels':list(im.size),'sha256':h,'group':gidx,'source_offset':m.start(),'note':'Decoded and reserialized image; parent XLS SHA is original authority.'})
  except Exception:pass
hits=[]
for sh in sheets:
 for c in sh['cells']:
  v=c.get('value')
  if isinstance(v,str) and re.search('부산역|[1-5]층|위치도|평면도|단면도|입면도|도면|별첨|철골|내화|층고|천장고|EL[. ]|FL[. ]',v):hits.append({'sheet':sh['name'],**c})
result={'path':str(f),'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'bytes':f.stat().st_size,'container':'PASS OLE','ole_streams':o.listdir(),'biff_record_count':len(records),'sst_unique':len(sst),'sheets':sheets,'images':images,'scope_hits':hits,'caution':'Read BIFF cached records only; no macros or formulas executed. Images reserialized for inspection; raw drawing groups also retained.'}
for ix,g in enumerate(groups):(out/f'drawing-group-{ix}.bin').write_bytes(g)
(E/(prefix+'-inspection.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
(E/(prefix+'-strings.json')).write_text(json.dumps(sst,ensure_ascii=False,indent=2)+'\n')
if images:
 w,h=400,310;cols=3;sheet=Image.new('RGB',(cols*w,((len(images)+cols-1)//cols)*h),'white');draw=ImageDraw.Draw(sheet)
 for i,item in enumerate(images):
  im=Image.open(item['path']).convert('RGB');im.thumbnail((w-10,h-25));x=(i%cols)*w;y=(i//cols)*h;sheet.paste(im,(x+(w-im.width)//2,y+23));draw.text((x+5,y+4),pathlib.Path(item['path']).name,fill='black')
 sheet.save(E/(prefix+'-images-contact-sheet.jpg'),quality=90)
print(json.dumps({'sha256':result['sha256'],'sheets':[(x['name'],len(x['cells']),x['drawing_records']) for x in sheets],'images':images,'scope_hits':hits[:40]},ensure_ascii=False,indent=2))
