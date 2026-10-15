#!/usr/bin/env python3
"""Current provider/codec contracts with guarded fresh receipts in a new lane."""
from pathlib import Path
import hashlib,json,os,shutil,subprocess,time
from run_checks import HERE,ROOT,toolchain

def main():
    mono,compiler=toolchain();output=ROOT/'output/simulator/all-domains-provider/portable'
    run=output/(str(time.time_ns())+'-'+str(os.getpid()));run.mkdir(parents=True)
    sources=sorted([*HERE.glob('Runtime/Core/*.cs'),*HERE.glob('Runtime/Planning/*.cs')])
    tests=[HERE/('Tests/'+n+'.cs') for n in ['PlanningTests','SubscriptionTests','ProviderSelectionTests','NativeProviderControlTests','MaterialStoreTests','EmergenceGenerationTests','EmergenceGraphCodecTests']]
    tests += [HERE/'model_service/InitialWorldNative.cs',HERE/'model_service/InitialWorldNativeTests.cs',HERE/'model_service/LocalChoiceEvaluation.cs']
    tracked=[*sources,*tests,Path(__file__)];hashes={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in tracked}
    probe=run/'AllDomainsProviderProbe.cs';probe.write_text('''using System;using ChooGuard.Seeded.Tests;
public static class AllDomainsProviderProbe {public static int Main(){try{PlanningTests.Run();SubscriptionTests.Run();ProviderSelectionTests.Run();NativeProviderControlTests.Run();MaterialStoreTests.Run();EmergenceGenerationTests.Run();EmergenceGraphCodecTests.Run();InitialWorldNativeTests.Run();Console.WriteLine("PASS_ALL_DOMAINS_PROVIDER_SOURCE_CONTRACTS");return 0;}catch(Exception e){Console.Error.WriteLine(e.ToString());return 1;}}}''')
    dll=next((ROOT/'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'));shutil.copy2(dll,run/dll.name)
    facade=mono.parent.parent/'lib/mono/4.5/Facades/netstandard.dll';exe=run/'AllDomainsProviderProbe.exe'
    c=subprocess.run([str(mono),str(compiler),'-nologo','-langversion:latest','-main:AllDomainsProviderProbe','-out:'+str(exe),'-r:'+str(dll),'-r:'+str(facade),'-r:System.Net.Http.dll',str(probe),*map(str,sources),*map(str,tests)],cwd=ROOT,capture_output=True,text=True)
    r=c if c.returncode else subprocess.run([str(mono),str(exe)],cwd=ROOT,capture_output=True,text=True,timeout=60)
    drift=[str(p.relative_to(ROOT)) for p in tracked if hashlib.sha256(p.read_bytes()).hexdigest()!=hashes[str(p.relative_to(ROOT))]]
    receipt={'schema':'all-domains-provider-portable-v1','passed':r.returncode==0,'exit_code':r.returncode,'source_hashes':hashes,
        'source_changed_during_run':bool(drift),'current_drift':drift,'stdout':r.stdout,'stderr':r.stderr,
        'real_provider_requests':0,'login_catalog_requests':0,'unity_engine_started':False,'active_helper_restarted':False,
        'claim':'Eight source/portable suites only; full-domain runtime/entitlement/quality/cost gates remain separate.'}
    text=json.dumps(receipt,ensure_ascii=False,indent=2)+'\n';(run/'receipt.json').write_text(text);(output/'receipt.json').write_text(text)
    print(r.stdout,end='');print(r.stderr,end='');print('receipt',str(run/'receipt.json'));return r.returncode
if __name__=='__main__':raise SystemExit(main())
