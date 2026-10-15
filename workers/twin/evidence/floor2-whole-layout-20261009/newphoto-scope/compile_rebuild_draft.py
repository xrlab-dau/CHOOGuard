from pathlib import Path
import subprocess,json,hashlib,time
out=Path('workers/twin/output/floor2-whole-layout-20261009/newphoto-scope');work=out/'compile';work.mkdir(exist_ok=True)
base=Path('Library/Bee/artifacts/200b0aE.dag/ChooGuard.Editor.rsp').read_text().splitlines()
dotnet='/Applications/Unity/Unity-6000.3.23f1/Unity.app/Contents/Resources/Scripting/NetCoreRuntime/dotnet'
csc='/Applications/Unity/Unity-6000.3.23f1/Unity.app/Contents/Resources/Scripting/DotNetSdkRoslyn/csc.dll'
patch=json.load(open(out/'namcheon-patch-sources.json'))['entries'];swap={r['source']:r['snapshot']for r in patch}
editor=[]
for l in base:
 if l.startswith('-out:'):l='-out:"'+str(work/'ChooGuard.Editor.NamcheonDraft.dll')+'"'
 elif l.startswith('-refout:'):l='-refout:"'+str(work/'ChooGuard.Editor.NamcheonDraft.ref.dll')+'"'
 elif l.startswith('/additionalfile:')or l.startswith('-analyzer:'):continue
 elif l.strip('"')in swap:l='"'+swap[l.strip('"')]+'"'
 editor.append(l)
editor.append('"'+str(out/'NamcheonSourceFrontRegistry.cs')+'"')
(work/'editor.rsp').write_text('\n'.join(editor)+'\n')
r=subprocess.run([dotnet,csc,'@'+str(work/'editor.rsp')],text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
(work/'editor-compile.log').write_text(r.stdout);print('editor exit',r.returncode);print(r.stdout[-8000:])
if r.returncode:raise SystemExit(r.returncode)
opts=[l for l in base if l.startswith(('-r:','-define:','-langversion:','-nostdlib:'))or l in ['/nologo','/unsafe','/utf8output']]
opts=[l for l in opts if 'ChooGuard.Editor'not in l]
opts+=['-r:"'+str(work/'ChooGuard.Editor.NamcheonDraft.dll')+'"','-target:library','-out:"'+str(work/'NamcheonBuildersDraft.dll')+'"','"'+swap['AgentScripts/MajibangBuilder.cs']+'"','"'+swap['AgentScripts/SourceOverrides.cs']+'"']
(work/'builders.rsp').write_text('\n'.join(opts)+'\n')
r2=subprocess.run([dotnet,csc,'@'+str(work/'builders.rsp')],text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
(work/'builders-compile.log').write_text(r2.stdout);print('builders exit',r2.returncode);print(r2.stdout[-8000:])
def h(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
receipt={'schema':'chooguard.offline-unity-roslyn-compile.v1','unity_editor_called':False,'assets_scene_canonical_written':False,'compiler':csc,'compiler_sha256':h(csc),'editor_exit_code':r.returncode,'builders_exit_code':r2.returncode,'status':'PASS'if r2.returncode==0 else 'FAIL','inputs':[{'path':r['snapshot'],'sha256':h(r['snapshot'])}for r in patch]+[{'path':str(out/'NamcheonSourceFrontRegistry.cs'),'sha256':h(out/'NamcheonSourceFrontRegistry.cs')}],'outputs':[{'path':str(p),'sha256':h(p)}for p in [work/'ChooGuard.Editor.NamcheonDraft.dll',work/'NamcheonBuildersDraft.dll']if p.exists()],'runtime_source_rebuild_performed':False,'runtime_scene_acceptance':False}
(work/'compile-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
raise SystemExit(r2.returncode)
