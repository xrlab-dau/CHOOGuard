#!/usr/bin/env python3
"""Export or apply a recorded initial-world response using the real C# contracts. No network transport."""
import argparse,hashlib,importlib.util,json,os,shutil,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('mode',choices=['export','apply']);p.add_argument('--world',type=Path,required=True)
    p.add_argument('--basis',required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--completion',type=Path)
    p.add_argument('--restore-fixture-rain',action='store_true',help='Invert only the verified Unity offline fixture @world rain=.73 from its original raw tape')
    a=p.parse_args();a.output=a.output.resolve()
    if a.mode=='apply' and not a.completion:p.error('--completion required for apply')
    spec=importlib.util.spec_from_file_location('sim_checks',ROOT/'workers/simulator/run_checks.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    mono,compiler=module.toolchain();build=a.output/'native-build';build.mkdir(parents=True,exist_ok=True)
    sources=sorted([*(ROOT/'workers/simulator/Runtime/Core').glob('*.cs'),*(ROOT/'workers/simulator/Runtime/Planning').glob('*.cs'),HERE/'InitialWorldNative.cs'])
    hashes={str(f.relative_to(ROOT)):hashlib.sha256(f.read_bytes()).hexdigest() for f in sources}
    dll=next((ROOT/'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    shutil.copy2(dll,build/'Newtonsoft.Json.dll');exe=build/'InitialWorldNative.exe'
    subprocess.run([str(mono),str(compiler),'-nologo','-langversion:latest','-target:exe','-out:'+str(exe),'-r:'+str(dll),'-r:System.Net.Http.dll','-r:'+str(mono.parent.parent/'lib/mono/4.5/Facades/netstandard.dll'),*map(str,sources)],check=True,cwd=ROOT)
    result=subprocess.run([str(mono),str(exe),a.mode,str(a.output),str(a.world.resolve()),str(ROOT/'workers/simulator/data/semantic-coverage.json'),a.basis,str(a.completion.resolve()) if a.completion else '', 'restore-fixture-rain' if a.restore_fixture_rain else ''],cwd=ROOT)
    receipt={'mode':a.mode,'exit_code':result.returncode,'sources':hashes,'source_changed_during_run':any(hashlib.sha256((ROOT/f).read_bytes()).hexdigest()!=digest for f,digest in hashes.items()),'network_calls':0}
    (a.output/'native-tool-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');return result.returncode
if __name__=='__main__':raise SystemExit(main())
