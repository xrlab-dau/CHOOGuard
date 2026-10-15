from pathlib import Path
import subprocess,json,hashlib
O=Path('workers/twin/output/floor2-whole-layout-20261009/newphoto-scope/locker-withholding');W=O/'compile';W.mkdir(exist_ok=True)
base=Path('Library/Bee/artifacts/200b0aE.dag/ChooGuard.Editor.rsp').read_text().splitlines();mapping=json.load(open(O/'locker-patch-sources.json'))['entries'];swap={x['source']:x['snapshot']for x in mapping}
dotnet='/Applications/Unity/Unity-6000.3.23f1/Unity.app/Contents/Resources/Scripting/NetCoreRuntime/dotnet';csc='/Applications/Unity/Unity-6000.3.23f1/Unity.app/Contents/Resources/Scripting/DotNetSdkRoslyn/csc.dll'
editor=[]
for line in base:
 if line.startswith('-out:'):line='-out:"'+str(W/'ChooGuard.Editor.LockerDraft.dll')+'"'
 elif line.startswith('-refout:'):line='-refout:"'+str(W/'ChooGuard.Editor.LockerDraft.ref.dll')+'"'
 elif line.startswith(('/additionalfile:','-analyzer:')):continue
 elif line.strip('"')in swap:line='"'+swap[line.strip('"')]+'"'
 editor.append(line)
editor.append('"'+str(O/'LockerPlacementWithholdingRegistry.cs')+'"');(W/'editor.rsp').write_text('\n'.join(editor)+'\n')
r=subprocess.run([dotnet,csc,'@'+str(W/'editor.rsp')],text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT);(W/'editor-compile.log').write_text(r.stdout);print('editor exit',r.returncode,r.stdout[-6500:])
if r.returncode:raise SystemExit(r.returncode)
opts=[l for l in base if l.startswith(('-r:','-define:','-langversion:','-nostdlib:'))or l in ['/nologo','/unsafe','/utf8output']];opts=[l for l in opts if 'ChooGuard.Editor'not in l];opts+=['-r:"Library/ScriptAssemblies/Unity.RenderPipelines.Universal.Editor.dll"','-r:"Library/ScriptAssemblies/Unity.RenderPipelines.Core.Editor.dll"','-r:"'+str(W/'ChooGuard.Editor.LockerDraft.dll')+'"','-target:library','-out:"'+str(W/'LockerBuildersDraft.dll')+'"','"'+swap['AgentScripts/MajibangBuilder.cs']+'"','"'+str(O/'LockerPlacementWithholdingDraft.cs')+'"'];(W/'builders.rsp').write_text('\n'.join(opts)+'\n')
r2=subprocess.run([dotnet,csc,'@'+str(W/'builders.rsp')],text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT);(W/'builders-compile.log').write_text(r2.stdout);print('builders exit',r2.returncode,r2.stdout[-6500:])
h=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();receipt={'schema':'chooguard.offline-locker-roslyn-compile.v1','status':'PASS'if r2.returncode==0 else 'FAIL','editor_exit_code':r.returncode,'builders_exit_code':r2.returncode,'unity_editor_called':False,'source_scene_asset_canonical_writes':False,'inputs':[{'path':x['snapshot'],'sha256':h(x['snapshot'])}for x in mapping]+[{'path':str(O/n),'sha256':h(O/n)}for n in ['LockerPlacementWithholdingRegistry.cs','LockerPlacementWithholdingDraft.cs']],'compiler':csc,'compiler_sha256':h(csc),'runtime_mesh_generation_performed':False};(W/'compile-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');raise SystemExit(r2.returncode)
