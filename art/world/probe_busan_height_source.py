#!/usr/bin/env python3
"""Bounded public GBA ZIP central-directory probe; never downloads full archive."""
import io,json,subprocess,zipfile
from pathlib import Path
OUT=Path('asset-library/space-references/busan-reconstruction/building-height-source')
URL='ftp://m1782307:m1782307@dataserv.ub.tum.de/Height/asiaeast/e125_n40_e130_n35.zip'
SIZE=84522991612
class Remote(io.RawIOBase):
 def __init__(self): self.pos=0;self.receipts=[];self.total=0
 def seekable(self):return True
 def seek(self,o,w=0):self.pos=o if w==0 else self.pos+o if w==1 else SIZE+o;return self.pos
 def tell(self):return self.pos
 def read(self,n=-1):
  n=SIZE-self.pos if n<0 else min(n,SIZE-self.pos)
  if n<=0:return b''
  if self.total+n>40_000_000:raise RuntimeError('40 MB data budget')
  start=self.pos
  data=subprocess.check_output(['curl','--fail','-sS','--max-time','35','-r',f'{start}-{start+n-1}',URL])
  if len(data)!=n:raise RuntimeError(f'Range mismatch {len(data)} != {n}')
  self.pos+=n;self.total+=n;self.receipts.append(dict(start=start,length=n))
  return data
r=Remote()
with zipfile.ZipFile(r) as z:
 matches=[i for i in z.infolist() if '129.0_35.2_129.2_35.0' in i.filename]
 result={'archiveUrl':URL,'archiveBytes':SIZE,'matches':[dict(name=i.filename,compressedBytes=i.compress_size,uncompressedBytes=i.file_size,compression=i.compress_type,headerOffset=i.header_offset) for i in matches]}
 result['rangeReads']=r.receipts;result['bytesFetched']=r.total
 (OUT/'gba-archive-probe.json').write_text(json.dumps(result,indent=2)+'\n')
 print(json.dumps(result,indent=2))
