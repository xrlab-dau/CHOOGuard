"""Freeze proposal evidence and render actual exported PDF for independent review."""
from pathlib import Path
import hashlib,json,shutil,sys
import pymupdf
from PIL import Image,ImageDraw
BASE=Path(__file__).resolve().parent
ROOT=BASE.parents[3]
round_name=sys.argv[1]
target=ROOT/'.planning/2026-10-13-proposal-aaa'/round_name
if target.exists():raise SystemExit('Refusing to overwrite a frozen round')
target.mkdir();(target/'slides').mkdir();(target/'assets').mkdir();(target/'presentation/assets').mkdir(parents=True)
files={}
sources=[(BASE.parent/'PROPOSAL.md','PROPOSAL.md'),(BASE.parent/'SOURCE_LEDGER.md','SOURCE_LEDGER.md'),(BASE.parent/'EVALUATION_PROTOCOL.md','EVALUATION_PROTOCOL.md'),(BASE/'scene.json','scene.json'),(BASE.parent/'CHOOGuard_VR_Proposal.pdf','proposal.pdf'),(BASE/'deck.html','deck.html'),(BASE/'build_deck.py','build_deck.py'),(BASE/'audience_revision.py','audience_revision.py'),(BASE/'accessible_controller.js','accessible_controller.js'),(BASE/'PRESENTER_NOTES.md','PRESENTER_NOTES.md'),(BASE/'pdf_accessibility_checks.json','pdf_accessibility_checks.json'),(BASE/'browser_accessibility_checks.json','browser_accessibility_checks.json'),(BASE/'assets/generated/GENERATION_PROVENANCE.json','GENERATION_PROVENANCE.json')]
sources.append((BASE/'palette_correction_checks.json','palette_correction_checks.json'))
delivery_plan=ROOT/'.planning/2026-10-13-proposal-aaa/delivery_validation.md'
simulation_design=BASE.parent/'SEEDED_SIMULATION_DESIGN.md'
if simulation_design.exists():sources.append((simulation_design,'SEEDED_SIMULATION_DESIGN.md'))
interaction_design=BASE.parent/'INTERACTION_FIDELITY.md'
if interaction_design.exists():sources.append((interaction_design,'INTERACTION_FIDELITY.md'))
if delivery_plan.exists():sources.append((delivery_plan,'delivery_validation.md'))
for src,name in sources:
    shutil.copyfile(src,target/name);files[name]=hashlib.sha256((target/name).read_bytes()).hexdigest()
scene=json.loads((BASE/'scene.json').read_text())
for name in scene['assets']:
    shutil.copyfile(BASE/'assets'/name,target/'assets'/name);files['assets/'+name]=hashlib.sha256((target/'assets'/name).read_bytes()).hexdigest()
    shutil.copyfile(BASE/'assets'/name,target/'presentation/assets'/name);files['presentation/assets/'+name]=files['assets/'+name]
doc=pymupdf.open(target/'proposal.pdf');sheet=Image.new('RGB',(1500,((len(doc)+2)//3)*306),'#D5D5D5')
for i,page in enumerate(doc):
    dst=target/'slides'/f'slide-{i+1:02d}.png';page.get_pixmap(matrix=pymupdf.Matrix(.5,.5),alpha=False).save(dst)
    shutil.copyfile(dst,BASE/'previews'/dst.name)
    tile=Image.new('RGB',(500,306),'#F5F5F5');tile.paste(Image.open(dst).resize((480,270)),(10,26));ImageDraw.Draw(tile).text((10,8),f'{i+1:02d}',fill='#222222');sheet.paste(tile,((i%3)*500,(i//3)*306))
sheet.save(target/'contact-sheet.png');shutil.copyfile(target/'contact-sheet.png',BASE/'previews/contact-sheet.png')
(target/'snapshot.json').write_text(json.dumps(dict(round=round_name,files=files,scope='Frozen manuscript, protocol, semantic scene, actual tagged PDF, exact-font HTML, images and checks. No Unity screenshots; generated conceptual imagery is not implementation evidence.'),ensure_ascii=False,indent=2))
print(json.dumps(dict(round=round_name,path=str(target),files=len(files),scene=files['scene.json'],pdf=files['proposal.pdf']),ensure_ascii=False))
