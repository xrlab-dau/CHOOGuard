"""Bounded ZIP central-directory budget for resolved official-tool wheels."""
import concurrent.futures
import json
from pathlib import Path
import struct
import time
import urllib.error
import urllib.request

DIRECTORY=Path(__file__).resolve().parent


def inspect(item):
    url=item['download_info']['url']
    result={'package':item['metadata']['name'],'version':item['metadata']['version'],'url':url}
    try:
        request=urllib.request.Request(url,headers={'Range':'bytes=-65536'})
        try:
            response=urllib.request.urlopen(request,timeout=15)
        except urllib.error.HTTPError as error:
            if error.code!=416:raise
            # This CDN rejects suffix ranges larger than small files.
            response=urllib.request.urlopen(url,timeout=15)
        with response:
            if response.status==206:
                total=int(response.headers['Content-Range'].split('/')[-1])
            elif response.status==200:
                total=int(response.headers.get('Content-Length','1000000000'))
                assert total<=65536,'Unbounded full wheel refused'
            else:raise RuntimeError('No bounded HTTP Range support')
            tail=response.read(65537);assert len(tail)<=65536
        pos=tail.rfind(b'PK\x05\x06');assert pos>=0
        end=struct.unpack_from('<4s4H2IH',tail,pos)
        offset,size=end[6],end[5]
        assert size<8_000_000,'Central metadata outside bounded budget'
        start=total-len(tail)
        if offset>=start:directory=tail[offset-start:offset-start+size]
        else:
            request=urllib.request.Request(url,headers={'Range':f'bytes={offset}-{offset+size-1}'})
            with urllib.request.urlopen(request,timeout=15)as response:
                if response.status!=206:raise RuntimeError('No bounded HTTP Range support')
                directory=response.read(size+1);assert len(directory)==size
        cursor=0;expanded=0;entries=0
        while cursor<len(directory):
            row=struct.unpack_from('<4s6H3I5H2I',directory,cursor)
            assert row[0]==b'PK\x01\x02'
            assert row[9]!=0xffffffff,'ZIP64 entry not accounted'
            expanded+=row[9];entries+=1
            cursor+=46+row[10]+row[11]+row[12]
        result.update(compressed_bytes=total,expanded_bytes=expanded,entries=entries,
            metadata_bytes_received=len(tail)+(size if offset<start else 0),status='boundedzipmetadataread')
    except Exception as error:
        result.update(status='failed',error=type(error).__name__+':'+str(error))
    return result


def main():
    source=json.loads((DIRECTORY/'nerfstudio-python311-install-plan.json').read_text())
    start=time.monotonic()
    with concurrent.futures.ThreadPoolExecutor(max_workers=2)as pool:
        rows=list(pool.map(inspect,source['install']))
    result={'schema':'chooguard.isolated-tool-wheel-budget.v1','python':'existing Blender4.5 CPython3.11',
        'host':'macOS26.5 arm64','package_count':len(rows),'elapsed_seconds':time.monotonic()-start,
        'packages':rows,'success_count':sum(x['status']=='boundedzipmetadataread'for x in rows),
        'compressed_total_bytes':sum(x.get('compressed_bytes',0)for x in rows),
        'expanded_total_bytes':sum(x.get('expanded_bytes',0)for x in rows),
        'metadata_bytes_received':sum(x.get('metadata_bytes_received',0)for x in rows),
        'packages_installed':False,'scope':'HTTP Range ZIP-directory only; no full wheel or package install'}
    (DIRECTORY/'nerfstudio-python311-wheel-budget.json').write_text(json.dumps(result,indent=2)+'\n')
    print({key:value for key,value in result.items()if key!='packages'})
    print('largest',[(x['package'],round(x.get('expanded_bytes',0)/1024**2,1))
        for x in sorted(rows,key=lambda x:x.get('expanded_bytes',0),reverse=True)[:8]])


if __name__=='__main__':main()
