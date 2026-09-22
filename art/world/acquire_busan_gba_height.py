#!/usr/bin/env python3
"""Acquire only the exact Busan ZIP member with strict HTTP ranges and CRC."""
import hashlib,io,json,struct,urllib.request,zipfile,zlib,os,concurrent.futures
from pathlib import Path
OUT=Path('asset-library/space-references/busan-reconstruction/building-height-source')
URL='https://dataserv.ub.tum.de/public.php/dav/files/m1782307/Height/asiaeast/e125_n40_e130_n35.zip'
SIZE=84522991612
NAME='129.0_35.2_129.2_35.0_sr_ss.tif'
existing_receipt=OUT/'gba-acquisition-receipt.json'
if existing_receipt.exists() and (OUT/NAME).exists():
 saved=json.loads(existing_receipt.read_text())
 if saved.get('strictRangeAndCrcVerified') and hashlib.sha256((OUT/NAME).read_bytes()).hexdigest()==saved.get('tiffSha256'):
  print('Existing verified TIFF retained; no download required.')
  raise SystemExit(0)
receipts=[];total=32_000_000 # conservative budget charge for interrupted initial stream

def request(start,n):
 global total
 if total+n>256_000_000: raise RuntimeError('Transfer cap')
 req=urllib.request.Request(URL,headers={'Range':f'bytes={start}-{start+n-1}','Accept-Encoding':'identity'})
 r=urllib.request.urlopen(req,timeout=45)
 expected=f'bytes {start}-{start+n-1}/{SIZE}'
 if r.status!=206 or r.headers.get('Content-Range')!=expected:
  r.close();raise RuntimeError('Range not honored')
 total+=n
 receipts.append({'status':r.status,'contentRange':r.headers.get('Content-Range'),'contentLength':r.headers.get('Content-Length'),'etag':r.headers.get('ETag')})
 return r
class Remote(io.RawIOBase):
 def __init__(self):self.pos=0
 def seekable(self):return True
 def seek(self,o,w=0):self.pos=o if w==0 else self.pos+o if w==1 else SIZE+o;return self.pos
 def tell(self):return self.pos
 def read(self,n=-1):
  n=SIZE-self.pos if n<0 else min(n,SIZE-self.pos)
  if n<=0:return b''
  with request(self.pos,n) as r:data=r.read(n+1)
  if len(data)!=n:raise RuntimeError('Bytecount mismatch')
  self.pos+=n;return data
remote=Remote()
with zipfile.ZipFile(remote) as archive:
 info=archive.getinfo(NAME)
 assert info.compress_size==195322823 and info.file_size==217887184 and info.compress_type==8
 remote.seek(info.header_offset);header=remote.read(30)
 vals=struct.unpack('<4s5H3I2H',header)
 assert vals[0]==b'PK\x03\x04' and not vals[2]&1 and vals[3]==8
 extra=remote.read(vals[-2]+vals[-1]);assert extra[:vals[-2]].decode()==NAME
 start=info.header_offset+30+vals[-2]+vals[-1]
 target=OUT/NAME;temp=OUT/(NAME+'.partial')
 crc=0;written=0;compressed_hash=hashlib.sha256();raw_hash=hashlib.sha256();decoder=zlib.decompressobj(-15)
 compressed=OUT/(NAME+'.deflate')
 with compressed.open('wb') as f:f.truncate(info.compress_size)
 def fetch_chunk(offset,n):
  with request(start+offset,n) as response:
   data=response.read(n+1)
   if len(data)!=n:raise RuntimeError('Chunk bytecount mismatch')
  with compressed.open('r+b') as f:f.seek(offset);f.write(data)
  print(f'chunk {offset} {n} complete',flush=True)
 with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
  futures=[pool.submit(fetch_chunk,o,min(8_000_000,info.compress_size-o)) for o in range(0,info.compress_size,8_000_000)]
  for future in futures:future.result()
 with compressed.open('rb') as response,temp.open('wb') as f:
  while block:=response.read(1048576):
   compressed_hash.update(block)
   raw=decoder.decompress(block);written+=len(raw)
   if written>info.file_size:raise RuntimeError('Inflated size overflow')
   f.write(raw);raw_hash.update(raw);crc=zlib.crc32(raw,crc)
  raw=decoder.flush();f.write(raw);written+=len(raw);raw_hash.update(raw);crc=zlib.crc32(raw,crc)
 if not decoder.eof or decoder.unused_data or written!=info.file_size or crc!=info.CRC:raise RuntimeError('ZIP integrity failure')
 temp.rename(target)
 evidence={'url':URL,'member':NAME,'compressedBytes':info.compress_size,'uncompressedBytes':written,'memberDataOffset':start,'crc32':f'{crc:08x}','expectedCrc32':f'{info.CRC:08x}','compressedSha256':compressed_hash.hexdigest(),'tiffSha256':raw_hash.hexdigest(),'rangeReceipts':receipts,'transferBudgetAccountedBytes':total,'measuredSuccessfulAttemptBytes':total-32000000,'interruptedAttemptConservativeBudgetChargeBytes':32000000,'strictRangeAndCrcVerified':True}
 (OUT/'gba-acquisition-receipt.json').write_text(json.dumps(evidence,indent=2)+'\n');print(json.dumps(evidence,indent=2))
