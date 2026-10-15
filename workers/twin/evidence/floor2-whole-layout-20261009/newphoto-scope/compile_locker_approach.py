from pathlib import Path
import subprocess,json,hashlib
O=Path('workers/twin/output/floor2-whole-layout-20261009/newphoto-scope/locker-withholding');W=O/'test-compile';W.mkdir(exist_ok=True)
base=Path('Library/Bee/artifacts/200b0aE.dag/ChooGuard.PlayModeTests.rsp');dotnet='/Applications/Unity/Unity-6000.3.23f1/Unity.app/Contents/Resources/Scripting/NetCoreRuntime/dotnet';csc='/Applications/Unity/Unity-6000.3.23f1/Unity.app/Contents/Resources/Scripting/DotNetSdkRoslyn/csc.dll'
opts=[]
for line in base.read_text().splitlines():
 if line.startswith(('-out:','-refout:','-analyzer:','/additionalfile:')):continue
 if not line.startswith(('-','/')):continue
 opts.append(line)
opts+=['-out:"'+str(W/'LockerApproachPlayTests.dll')+'"','"'+str(O/'LockerApproachPlayTests.cs')+'"'];(W/'test.rsp').write_text('\n'.join(opts)+'\n');r=subprocess.run([dotnet,csc,'@'+str(W/'test.rsp')],text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT);(W/'compile.log').write_text(r.stdout);print('compile exit',r.returncode);print(r.stdout[-6000:])
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();receipt={'schema':'chooguard.locker-approach-offline-compile.v1','status':'PASS'if r.returncode==0 else'FAIL','exit_code':r.returncode,'test':str(O/'LockerApproachPlayTests.cs'),'test_sha256':sha(O/'LockerApproachPlayTests.cs'),'compiler':csc,'compiler_sha256':sha(csc),'unity_assembly_options':str(base),'assembly_options_sha256':sha(base),'unity_editor_called':False,'playmode_executed':False,'scene_asset_canonical_writes':False,'output_sha256':sha(W/'LockerApproachPlayTests.dll')if r.returncode==0 else None,'compile_log_sha256':sha(W/'compile.log')};(W/'compile-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');raise SystemExit(r.returncode)
