#!/usr/bin/env python3
"""Install the reviewed published SDK pin locally. No login/inference/runtime start."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import urllib.request
import zipfile

COMMIT='810c483ae426344c2a5fee61025f9e3bd457cca9'
VERSION='1.0.22rc1'
WHEEL='github_copilot_sdk-1.0.22rc1-py3-none-any.whl'
SHA='ef122dd8385a1d8b8d62797afabd72f9ec0235564a48be76d3dac1c77d8debc7'
RUNTIME='1.0.98-1'


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--download-runtime',action='store_true',help='Provision the SDK-pinned official runtime, without starting it or logging in.')
    args=parser.parse_args();here=Path(__file__).resolve().parent;target=here/'.sdk-deps'
    metadata=json.load(urllib.request.urlopen('https://pypi.org/pypi/github-copilot-sdk/'+VERSION+'/json',timeout=30))
    artifact=next(f for f in metadata['urls'] if f['filename']==WHEEL and f['digests']['sha256']==SHA)
    with urllib.request.urlopen(artifact['url'],timeout=45) as response:data=response.read(5000001)
    if len(data)>5000000 or hashlib.sha256(data).hexdigest()!=SHA:raise SystemExit('SDK artifact SHA mismatch.')
    # The published wheel's client.py/session.py/protocol match the reviewed source
    # commit exactly. Its packaging injects the concrete official runtime version.
    reference=here.parents[2]/'.planning/2026-10-14-multi-provider-issue/primary-sources/copilot-sdk/python/copilot'
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        critical={n:hashlib.sha256(archive.read('copilot/'+n)).hexdigest() for n in ['client.py','session.py','_sdk_protocol_version.py']}
        for n,digest in critical.items():
            if (reference/n).is_file() and hashlib.sha256((reference/n).read_bytes()).hexdigest()!=digest:raise SystemExit('Reviewed SDK source mismatch: '+n)
    import importlib.metadata
    from packaging.specifiers import SpecifierSet
    for name,minimum in [('pydantic','>=2.11'),('httpx','>=0.24.0'),('python-dateutil','>=2.9.0.post0')]:
        if importlib.metadata.version(name) not in SpecifierSet(minimum):raise SystemExit('Install task-local compatible dependency: '+name+minimum)
    with tempfile.TemporaryDirectory(prefix='chooguard-copilot-sdk-') as temporary:
        path=Path(temporary)/WHEEL;path.write_bytes(data)
        subprocess.run([sys.executable,'-m','pip','install','--no-deps','--target',str(target),'--upgrade',str(path)],check=True)
    receipt={'source_commit':COMMIT,'distribution_version':VERSION,'wheel_sha256':SHA,'wheel_url':artifact['url'],
             'runtime_version':RUNTIME,'target':'.sdk-deps','reviewed_source_files':critical,
             'runtime_downloaded':False,'login_performed':False,'inference_performed':False}
    if args.download_runtime:
        import os
        env={k:v for k,v in os.environ.items() if not k.startswith(('COPILOT_','GITHUB_','GH_'))}
        env['PYTHONPATH']=str(target);env['COPILOT_CLI_EXTRACT_DIR']=str(here/'.sdk-runtime')
        subprocess.run([sys.executable,'-m','copilot','download-runtime','--version',RUNTIME],env=env,check=True)
        receipt['runtime_downloaded']=True
        receipt['runtime_files']={str(p.relative_to(here/'.sdk-runtime')):hashlib.sha256(p.read_bytes()).hexdigest() for p in (here/'.sdk-runtime').rglob('*') if p.is_file() and p.name in ('copilot-runtime','copilot-runtime.exe','runtime.node')}
    receipt['source_files']={str(p.relative_to(target/'copilot')):hashlib.sha256(p.read_bytes()).hexdigest() for p in (target/'copilot').rglob('*.py')}
    (here/'sdk-install-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
    print(json.dumps({k:v for k,v in receipt.items() if k!='source_files'},indent=2))

if __name__=='__main__':main()
