"""Update the user-authorized local OpenDesign proposal project and read it back."""
from pathlib import Path
import urllib.request, urllib.parse, json, shutil, hashlib
BASE=Path(__file__).resolve().parent
PROJECT=Path('/Users/um-yunsang/Library/Application Support/Open Design/namespaces/release-stable/data/projects/chooguard-research-proposal-20261013')
URL='http://127.0.0.1:7456/api/projects/chooguard-research-proposal-20261013'
scene=json.loads((BASE/'scene.json').read_text())
expected={}
for name in scene['assets']:
 dst=PROJECT/'assets'/name;dst.parent.mkdir(exist_ok=True);shutil.copyfile(BASE/'assets'/name,dst);expected['assets/'+name]=(BASE/'assets'/name).read_bytes()
for name in ['regular','bold','mono']:
 dst=PROJECT/'fonts'/(name+'.ttf');dst.parent.mkdir(exist_ok=True);shutil.copyfile(BASE/'qa-fonts'/(name+'.ttf'),dst);expected['fonts/'+name+'.ttf']=dst.read_bytes()
files={'deck.html':(BASE/'deck-cloud.html').read_text()}
for name in ['PROPOSAL.md','EVALUATION_PROTOCOL.md','SEEDED_SIMULATION_DESIGN.md','INTERACTION_FIDELITY.md','SOURCE_LEDGER.md','README.md']:
 files[name]=(BASE.parent/name).read_text().replace('(presentation/assets/','(assets/')
files['PRESENTER_NOTES.md']=(BASE/'PRESENTER_NOTES.md').read_text()
for name,content in files.items():
 payload=json.dumps(dict(name=name,content=content,encoding='utf8',overwrite=True),ensure_ascii=False).encode()
 req=urllib.request.Request(URL+'/files',data=payload,headers={'Content-Type':'application/json'},method='POST')
 with urllib.request.urlopen(req,timeout=30) as response:response.read()
 expected[name]=content.encode()
checks=[]
for name,data in expected.items():
 with urllib.request.urlopen(URL+'/raw/'+urllib.parse.quote(name,safe='/'),timeout=30) as response:actual=response.read()
 checks.append(dict(file=name,exact=actual==data,sha256=hashlib.sha256(actual).hexdigest()))
assert all(x['exact'] for x in checks),checks
report=dict(round='r13-busan-station',slideCount=len(scene['slides']),imageCount=len(scene['assets']),files=checks,preview=URL+'/raw/deck.html',scope='Local user-authorized OpenDesign project; exact HTTP byte readback. No public deployment or submission.')
(BASE/'opendesign_verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2));print(json.dumps(dict(round=report['round'],slides=len(scene['slides']),files=len(checks),allExact=True),ensure_ascii=False))
