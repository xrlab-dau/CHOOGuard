"""Finite source-only MAIN2F architecture trace, reusing source diagram kit types.
Primary source pixels remain authoritative; unmeasured source/native differences are classified, not vetoed.
"""
import json,hashlib,struct,numpy as np,cv2
from pathlib import Path
from PIL import Image,ImageDraw
from shapely.geometry import Polygon,Point,LineString
from shapely.ops import unary_union
ROOT=Path('/Users/um-yunsang/CHOOGuard');OUT=ROOT/'workers/twin/output/99percent-main2f-20261009';EV=ROOT/'workers/twin/evidence/99percent-main2f-20261009';SRC=ROOT/'workers/twin/evidence/floor2-architectural-pdf-search-20261009/official-100083811-store-plan-page-1-embedded-2.jpeg'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def dump(n,x):(OUT/n).write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n')
regpath=ROOT/'workers/twin/output/floor2-official-pdf-registration-20261009/architectural-registration.json';reg=json.loads(regpath.read_text());M=np.array(reg['main']['native_UV_to_raster_pixels']);IM=np.linalg.inv(M[:2]);
def uv(p):return ((np.asarray(p,dtype=float)-M[2])@IM).tolist()
def pix(p):return (np.c_[np.atleast_2d(p),np.ones(len(np.atleast_2d(p)))]@M).tolist()
# Each uninterrupted source ink run is separate. Explicit gaps are never bridged by convenience.
runs=[]
def run(id,pts,region,kind='wall',confidence='clear main boundary',install=True):
 for i,(a,b)in enumerate(zip(pts,pts[1:])):
  if np.linalg.norm(np.array(a)-b)<.6:continue
  runs.append({'id':id+(f'-{i+1}'if len(pts)>2 else''),'region':region,'source_segment_pixels':[a,b],'native_segment_UV_reference':uv([a,b]),'kind':kind,'source_tolerance_pixels':2 if confidence.startswith('clear')else 3,'source_confidence':confidence,'kit_draft_candidate':install,'height_source':'existing2F services wallTop10.44/floorY7, modeled/unmeasured'if kind=='wall'else'unresolved; no full-height wall inferred'})
# Main outside envelope and front canopy belt: retain/reconcile, not blindly duplicate existing shell.
run('outer-east',[[610,277],[610,396],[607,405],[609,415]],'outer-shell',install=False)
run('outer-front',[[609,415],[577,429],[540,443],[500,453],[458,463],[408,473],[365,479],[328,482],[248,482],[225,480],[184,477],[144,471],[105,465],[87,460]],'outer-shell',install=False)
run('outer-west',[[58,303],[70,324],[84,347],[98,370],[111,391],[122,414],[112,460]],'outer-shell',install=False)
run('north-west-hall',[[94,306],[148,306],[148,294],[252,294],[252,280],[311,280]],'seam',install=False)
# Main-side continuation of the central vertical core and its corridor.
# Existing stairs/lifts are footprint controls; separate visible walls can be reconciled.
run('C-west-screen',[[311,280],[311,305],[311,334]],'central-core',kind='wall-or-glazed-screen',install=False)
run('C-upper-room',[[320,275],[320,301],[334,301],[334,275]],'central-core',kind='core-envelope',install=False)
run('C-corridor-west-return',[[311,305],[320,305],[320,323],[337,323]],'central-core')
run('C-corridor-south',[[341,323],[348,323],[349,325],[414,325],[414,330],[455,330]],'central-core',confidence='visible continuous boundary; material and height unresolved',install=False)
run('C-core-north',[[362,285],[414,285]],'central-core',kind='core-envelope',install=False)
run('C-core-west',[[362,285],[362,304]],'central-core',kind='core-envelope',install=False)
run('C-core-south',[[362,304],[414,304],[414,300]],'central-core',kind='core-envelope',install=False)
run('C-lobby-west',[[362,304],[362,318],[376,318]],'central-core')
run('C-lobby-front-mid',[[383,318],[398,318]],'central-core')
run('C-lobby-front-east',[[403,318],[414,318],[414,304]],'central-core')
run('C-lobby-divider',[[389,304],[389,318]],'central-core',confidence='visible interior boundary; room versus lift enclosure unresolved',install=False)
# East upper service suite: directly visible major boundaries, small fixture marks left as source ink.
run('E-top',[[464,276],[563,276]],'east-service')
run('E-left-top',[[464,276],[464,285]],'east-service')
run('E-left-lower',[[464,293],[464,305]],'east-service')
run('E-top-west-divider',[[464,285],[516,285]],'east-service')
for x in [469,477,489]:run(f'E-north-cell-{x}',[[x,276],[x,285]],'east-service',confidence='short visible compartment boundary',install=False)
run('E-top-main-split',[[516,276],[516,300]],'east-service')
run('E-top-east-return',[[558,276],[558,300]],'east-service')
run('E-top-right-outer',[[563,276],[563,300],[542,300]],'east-service')
run('E-top-south-left',[[516,300],[534,300]],'east-service')
run('E-west-room-top',[[464,294],[487,294]],'east-service')
run('E-west-room-bottom',[[464,305],[491,305]],'east-service')
run('E-west-room-split',[[485,294],[485,305]],'east-service')
run('E-small-service-row',[[490,294],[516,294]],'east-service',confidence='short visible compartment boundary',install=False)
for x in [491,503]:run(f'E-small-cell-{x}',[[x,294],[x,300]],'east-service',confidence='short visible compartment boundary',install=False)
run('E-bottom-top-left',[[464,307],[491,307]],'east-service')
run('E-bottom-top-main',[[495,307],[541,307]],'east-service')
run('E-bottom-top-right',[[546,307],[563,307]],'east-service')
run('E-bottom-west',[[464,307],[464,323]],'east-service')
run('E-bottom-west-cell',[[473,307],[473,320]],'east-service')
run('E-bottom-west-front',[[464,320],[491,320],[491,308]],'east-service')
run('E-bottom-vestibule',[[497,313],[516,313]],'east-service')
run('E-bottom-major-split',[[516,307],[516,328]],'east-service')
run('E-bottom-major-floorline',[[516,333],[546,333]],'east-service')
run('E-bottom-small-east',[[550,307],[550,333],[563,333],[563,307]],'east-service')
run('E-east-small-divider',[[550,322],[563,322]],'east-service')
run('E-north-vertical-core',[[579,276],[630,276],[630,296],[611,296],[611,335],[579,335],[579,276]],'east-core',kind='core-envelope',install=False)
run('E-front-small-room',[[471,336],[471,326],[478,323],[491,323],[491,332],[474,332]],'east-service',confidence='visible boundary with unresolved entry gaps',install=False)
# East WC envelope pair: explicit west entry gaps and no inferred fixture subtypes/counts.
run('E-WC-outer',[[609,338],[609,390],[579,390]],'east-WC')
run('E-WC-upper-top',[[591,338],[609,338]],'east-WC')
run('E-WC-upper-west',[[579,342],[579,349]],'east-WC')
run('E-WC-upper-split',[[579,349],[609,349]],'east-WC',confidence='visible boundary; fixture versus partition unresolved',install=False)
run('E-WC-common-wall',[[579,368],[609,368]],'east-WC')
run('E-WC-lower-west',[[579,375],[579,390]],'east-WC')
run('E-WC-lower-service',[[588,368],[588,390]],'east-WC',confidence='visible small service/cubicle boundary',install=False)
# West slanted wing: principal enclosure runs only; internal small fixture strokes remain unresolved.
run('W-service-top',[[58,303],[94,303],[94,318]],'west-service')
run('W-service-bottom',[[72,320],[94,320]],'west-service')
run('W-service-mid',[[79,330],[103,330],[108,335]],'west-service')
run('W-service-lower-front',[[109,339],[113,346]],'west-service')
run('W-WC-front-upper',[[115,351],[123,365]],'west-WC',confidence='visible slanted room front; entry endpoints approximate')
run('W-WC-front-middle',[[126,373],[133,383]],'west-WC',confidence='visible slanted room front; entry endpoints approximate')
run('W-WC-front-lower',[[137,392],[143,402]],'west-WC',confidence='visible slanted room front; entry endpoints approximate')
run('W-cross-upper',[[89,350],[111,337]],'west-WC',confidence='visible diagonal boundary; room versus fixture ambiguity',install=False)
run('W-cross-middle',[[102,373],[124,360]],'west-WC',confidence='visible diagonal boundary; room versus fixture ambiguity',install=False)
run('W-cross-lower',[[113,393],[136,379]],'west-WC',confidence='visible diagonal boundary; room versus fixture ambiguity',install=False)
# Front banks and central entry - exact visible envelope controls, reuse original operators.
for id,pts in [('front-left-core',[[198,415],[252,415],[252,436],[232,436],[232,440],[224,440],[224,435],[198,435],[198,415]]),('front-right-core',[[326,415],[365,415],[365,435],[342,435],[342,440],[332,440],[332,435],[326,435],[326,415]])]:run(id,pts,'front-cores',kind='core-envelope',install=False)
run('front-centre-entry',[[252,419],[263,419]],'front-entry',kind='entry-screen',install=False)
run('front-centre-entry-east',[[310,419],[326,419]],'front-entry',kind='entry-screen',install=False)
run('front-lobby-south',[[248,457],[328,457]],'front-entry',kind='boundary-height-unresolved',install=False)
# Major continuous hall U line and canopy Xs are inventoried, never converted to opaque walls by default.
run('central-U',[[288,332],[288,395],[479,395],[479,369]],'central-hall',kind='balustrade-or-level-boundary',install=False)
run('central-inner',[[301,341],[351,341],[351,395]],'central-hall',kind='balustrade-or-level-boundary',install=False)
run('canopy-west-X1',[[121,416],[225,479]],'front-canopy',kind='roof-projection-hatch',install=False)
run('canopy-west-X2',[[112,460],[224,435]],'front-canopy',kind='roof-projection-hatch',install=False)
run('canopy-east-X1',[[365,435],[425,474]],'front-canopy',kind='roof-projection-hatch',install=False)
run('canopy-east-X2',[[342,479],[422,417]],'front-canopy',kind='roof-projection-hatch',install=False)
rooms=[
 ('east-north-large',[[516,276],[558,276],[558,300],[516,300]],'east-service','unlabelled service room'),
 ('east-north-west',[[464,294],[485,294],[485,305],[464,305]],'east-service','unlabelled service room'),
 ('east-south-west',[[464,307],[491,307],[491,320],[464,320]],'east-service','unlabelled service room'),
 ('east-south-major',[[516,307],[550,307],[550,333],[516,333]],'east-service','unlabelled service room'),
 ('east-south-side-upper',[[550,307],[563,307],[563,322],[550,322]],'east-service','unlabelled service room'),
 ('east-south-side-lower',[[550,322],[563,322],[563,333],[550,333]],'east-service','unlabelled service room'),
 ('east-WC-upper',[[579,338],[609,338],[609,368],[579,368]],'east-WC','WC envelope, gender unassigned'),
 ('east-WC-lower',[[579,368],[609,368],[609,390],[579,390]],'east-WC','WC envelope, gender unassigned')]
portals=[('E-north-large-door',[[534,300],[542,300]],'east-service','double visible arc'),('E-north-small-gap',[[487,294],[491,294]],'east-service','visible gap; type unresolved'),('E-south-corridor-gap',[[491,307],[495,307]],'east-service','visible gap; type unresolved'),('E-south-north-door',[[541,307],[546,307]],'east-service','visible small arc'),('E-south-major-west-gap',[[516,328],[516,333]],'east-service','visible gap; type unresolved'),('E-south-major-front-gap',[[546,333],[550,333]],'east-service','visible gap/arc low resolution'),('E-WC-upper-entry',[[579,349],[579,355]],'east-WC','open approach, no leaf asserted'),('E-WC-lower-entry',[[579,368],[579,375]],'east-WC','open approach, no leaf asserted'),('W-WC-upper-entry',[[113,346],[115,351]],'west-WC','open approach, endpoint approximate'),('W-WC-lower-entry',[[123,365],[126,373]],'west-WC','open approach, endpoint approximate')]
portals.append(('E-WC-upper-north-open',[[579,338],[591,338]],'east-WC','visible open top approach; exact WC inner circulation unresolved'))
portals.extend([('C-lobby-front-west-gap',[[376,318],[383,318]],'central-core','visible gap/arc; leaf count unresolved'),('C-lobby-front-east-gap',[[398,318],[403,318]],'central-core','visible gap/arc; leaf count unresolved')])
corepolys=[('main-left-stair-bank',[[198,369],[251,369],[251,396],[198,396]]),('main-right-stair-bank',[[451,350],[489,350],[489,369],[451,369]]),('front-left-lift-stair-bank',[[198,415],[252,415],[252,439],[198,439]]),('front-right-lift-stair-bank',[[326,415],[365,415],[365,439],[326,439]]),('west-upper-lift-stair-bank',[[58,303],[94,303],[94,330],[72,330]]),('central-north-stair-bank',[[362,285],[414,285],[414,304],[362,304]]),('central-north-vertical-core',[[320,275],[334,275],[334,301],[320,301]])]
corepolys.append(('east-north-lift-stair-bank',[[579,276],[630,276],[630,296],[611,296],[611,335],[579,335]]))
# CV locates closed rectangular column glyphs only in free-hall search polygons; inspection controls, not new columns.
hall=Polygon([[94,305],[310,305],[310,332],[479,332],[479,337],[578,337],[578,395],[608,395],[608,414],[425,455],[425,417],[365,417],[365,439],[326,439],[326,418],[252,418],[252,439],[198,439],[198,417],[122,417],[114,399],[95,364]])
gray=cv2.imread(str(SRC),0);mask=(gray<145).astype('uint8')*255;contours,hierarchy=cv2.findContours(mask,cv2.RETR_TREE,cv2.CHAIN_APPROX_SIMPLE);columns=[]
for c in contours:
 x,y,w,h=cv2.boundingRect(c)
 if not(2<=w<=8 and 2<=h<=8 and .6<=w/h<=1.6):continue
 cx=x+(w-1)/2;cy=y+(h-1)/2
 if not hall.contains(Point(cx,cy)):continue
 if any(Polygon(p).buffer(2).contains(Point(cx,cy))for _,p in corepolys):continue
 if any(np.linalg.norm(np.array([cx,cy])-r['source_center_pixel'])<4 for r in columns):continue
 columns.append({'id':f'main-source-column-{len(columns)+1:03d}','source_center_pixel':[cx,cy],'source_glyph_bounds':[x,y,x+w-1,y+h-1],'native_center_UV_reference':uv([cx,cy]),'classification':'closed rectangular source glyph in open hall; visual control, no duplicate operator/column automatic creation','source_pixel_tolerance':2})
# Visual review rejects fixture marks and tiny artifacts that passed the CV box filter.
rejected_ids={10,12,24,43,44,45,46,47,48,49,51,52}
rejected_columns=[c for c in columns if int(c['id'].rsplit('-',1)[1]) in rejected_ids]
columns=[c for c in columns if int(c['id'].rsplit('-',1)[1]) not in rejected_ids]
for i,p in enumerate([[146.5,348],[223,348],[350.5,348.5],[350.5,371]],1):
 columns.append({'id':f'main-source-column-manual-{i:02d}','source_center_pixel':p,'source_glyph_bounds':None,'native_center_UV_reference':uv(p),'classification':'visually reviewed column glyph joining other ink; CV closed-contour filter missed it','source_pixel_tolerance':2})
for r in runs:
 if r['region'] in ['west-service','west-WC']:
  r['kit_draft_candidate']=False
  r['review_status']='source trace only; original slanted core/fixture boundaries need finer source before replacement'
# Whole graph is pixel-authoritative. Only high-confidence wall segments form initial kit candidate.
elements=[]
for r in runs:
 if not r['kit_draft_candidate']or r['kind']!='wall':continue
 elements.append({'type':'SourceDiagramWall','id':'PDF2F-'+r['id'],'zone':'MAIN2F PDF architecture20261009','tier':'b','frame':'root','batchPrefix':'SourceTrace2FMain_'+r['id']+'_','geometry':{'edge':r['native_segment_UV_reference'],'y':7.0,'top':10.44},'params':{'thickness':.12,'finish':'gypsum'},'evidence':[str(SRC.relative_to(ROOT)),str(regpath.relative_to(ROOT))],'source_pixel_controls':r['source_segment_pixels'],'source_tolerance_pixels':r['source_tolerance_pixels'],'height_basis':r['height_source']})
for id,p,region,kind in rooms:
 elements.append({'type':'SourceDiagramFloor','id':'PDF2F-floor-'+id,'zone':'MAIN2F PDF architecture20261009','tier':'b','frame':'root','batchPrefix':'SourceTrace2FMain_floor_'+id+'_','geometry':{'polygon':uv(p),'y':7},'params':{'finish':'toiletFloor'if'WC'in id else'vinyl'},'source_polygon_pixels':p,'source_role':kind,'structural_floor_under_finish':'unverified where original floor is absent; preserve native floor and label source-supported finish separately','evidence':[str(SRC.relative_to(ROOT))]})
# Only unambiguous arc count gets a proposed operator; height/hinge operation remains modeled.
for id,e,region,kind in portals:
 if kind != 'double visible arc':continue
 a,b=np.array(uv(e));center=(a+b)/2;n=np.array([-(b-a)[1],(b-a)[0]]);n/=np.linalg.norm(n)
 elements.append({'type':'ServiceDoor','id':'PDF2F-'+id,'zone':'MAIN2F PDF architecture20261009','tier':'b','frame':'root','batchPrefix':'SourceTrace2FMain_'+id+'_','geometry':{'edge':[a.tolist(),b.tolist()],'y':7},'params':{'width':float(np.linalg.norm(a-b)),'height':2.1,'leaves':2 if kind.startswith('double')else 1,'faceToward':(center+n).tolist(),'plate':'','label':'방 이름 미확인'},'source_pixels':e,'operation_basis':'Visible arc topology; modeled leaf height, thickness, hinge and operating parameters unmeasured; root must review collision/swing before actual installation.'})
packet={'schema':'chooguard.main2f-source-architecture-trace.v1','source':str(SRC.relative_to(ROOT)),'source_sha256':sha(SRC),'raster_size':[631,496],'floor_scope':'Official PDF page1 labels2F. MAIN2F below drawingy275; not proof of other4storeys or matchinghall floor numbering.','registration':str(regpath.relative_to(ROOT)),'registration_sha256':sha(regpath),'native_UV_to_source_pixels':M.tolist(),'new_geometry_coordinate_basis':'One main affine shared with matchinghall proposals, pixel-primary. Existing box fit retained only as old-model diagnostic; no protected floor/axis warp.','source_native_registration_heldout':reg['main']['heldout_controls'],'heldout_pixel_rms':reg['main']['heldout_residual_pixels_rms'],'site_accuracy_verified':False,'wall_and_boundary_runs':runs,'room_envelopes':[{'id':id,'source_polygon_pixels':p,'native_polygon_UV_reference':uv(p),'region':region,'role':kind,'source_tolerance_pixels':2}for id,p,region,kind in rooms],'portals':[{'id':id,'source_jamb_pixels':e,'native_jamb_UV_reference':uv(e),'region':region,'source_description':kind,'source_height_unmeasured':True}for id,e,region,kind in portals],'core_footprint_controls':[{'id':id,'source_polygon_pixels':p,'native_polygon_UV_reference':uv(p),'reuse_existing_source_assemblies':True,'new_operator_created':False}for id,p in corepolys],'column_glyph_controls':columns,'remaining_source_ink_inventory':'complete-main-source-line-candidates.json','ambiguous_linework_classes':['central U line: balustrade/level/void boundary versus opaque wall unresolved','front crossed rectangles: canopy/roof projection, not new walls','small fixture/cubicle marks and west internal cross-lines remain trace-reference-only','source tenant colored envelopes label lease/shop areas, not necessarily opaque full-height partitions'],'source_authority':'Plan-visible corrections are valid source-backed reconstruction candidates even when old native geometry differs. Metric/site precision and architectural elevation remain unverified.','production_mutated':False,'completion_claim':'Finite plan trace/draft only, no99% real-world reconstruction claim.'}
dump('main2f-source-architecture-trace.json',packet);dump('main2f-kit-correction-spec-draft.json',{'zone':'MAIN2F official PDF architectural correction draft','root':'실내 트윈 마감','basis':'source2D topology, relative unmeasured registration','production_installed':False,'source_epoch':'Official October7 page1 2F; same background as March','elements':elements,'requires_bounded_old_contributor_retention':True,'protected_original_floor_axes_not_transformed':True})
# Clear visual QA overlay: walls red, reference boundaries blue, room floors faint green, glyph controls dots.
im=Image.open(SRC).convert('RGB').resize((2524,1984),Image.Resampling.NEAREST);d=ImageDraw.Draw(im)
for r in runs:
 col='#d33342'if r['kit_draft_candidate']else'#1c70a5';d.line([tuple(np.array(p)*4)for p in r['source_segment_pixels']],fill=col,width=2)
for x in columns:p=np.array(x['source_center_pixel'])*4;d.ellipse((p[0]-4,p[1]-4,p[0]+4,p[1]+4),outline='#206b35',width=1)
for id,e,_,_ in portals:d.line([tuple(np.array(p)*4)for p in e],fill='#11a579',width=3)
im.crop((300,1060,2524,1984)).save(OUT/'main-source-trace-QA-overlay.png')
print(json.dumps({'runs':len(runs),'room_envelopes':len(rooms),'portal_controls':len(portals),'cores':len(corepolys),'column_glyphs':len(columns),'kit_elements':len(elements)}))
