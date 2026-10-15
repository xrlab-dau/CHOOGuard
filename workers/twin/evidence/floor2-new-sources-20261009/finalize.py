import collections,datetime,hashlib,json,pathlib,re
from PIL import Image,ImageDraw
folder=pathlib.Path(__file__).parent;root=pathlib.Path('/Users/um-yunsang/CHOOGuard')
artifacts={};failures=[]
for f in sorted(folder.glob('receipts-*.json'),key=lambda p:p.stat().st_mtime):
 for r in json.loads(f.read_text()):
  if 'path' in r:artifacts[r['id']]=r
  else:failures.append({'receipt':str(f.relative_to(root)),**r})
sources={}
for r in artifacts.values():
 sid=r['source_id'];d=json.loads((folder/(sid+'.json')).read_text());body=d['text'];m=re.search(r'(20\d\d)\.\s*(\d+)\.\s*(\d+)\.\s*(\d+):(\d+)',body)
 date='-'.join([m[1],m[2].zfill(2),m[3].zfill(2)])+' '+m[4].zfill(2)+':'+m[5]+' Asia/Seoul' if m else 'not established from page text'
 if sid=='somidam-primary-instagram':date='2025-06-18 Asia/Seoul (page date)'
 if sid=='artbox-primary-instagram':date='2026-06-12 Asia/Seoul (page June12 + dated2026openingevent; year inferredfromsamepost)'
 capture='not independently verified; publication date does not establish capture date'
 if 'chulinbone' in sid:capture='author body date20260616; EXIF not independently verified'
 if sid=='yubu-aqsd':capture='title date2025-12-29 corroborated photographedreceipt2025-12-29; EXIF not independently verified'
 if sid=='yubu-mhjoo':capture='author title/body2014-12-27; historical, excluded from2026appearancevalidation'
 if sid=='somidam-primary-instagram':capture='operator openingdate2025-06-13; photographcapturedate not independently verified'
 r['publication_date']=date;r['capture_date_evidence']=capture;r['ai_status']='no AI-generation label observed on capturedpage; authenticity not independently verified'
 if 'primary-instagram' in sid:r['license']='operator Instagram photograph; reuse license not established; reference_only'
 sources[sid]={'url':d['url'],'title':d['title'],'publication_date':date,'capture_date_evidence':capture,'captured_page':str((folder/(sid+'.json')).relative_to(root))}
successful=list(artifacts.values());bysha=collections.defaultdict(list)
for r in successful:
 data=(root/r['path']).read_bytes();assert hashlib.sha256(data).hexdigest()==r['sha256'];assert len(data)==r['bytes'];assert list(Image.open(root/r['path']).size)==r['image_dimensions'];bysha[r['sha256']].append(r['id'])
baseline={hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/'asset-library/research-public/2026-10-09/floor2-guide-passages').glob('*') if p.is_file() and p.suffix.lower() in ['.jpg','.jpeg','.png','.mpo']}
stats={'downloaded_files':len(successful),'unique_sha256':len(bysha),'distinct_source_pages':len(sources),'bytes_total':sum(r['bytes'] for r in successful),'baseline_duplicate_sha_count':len(set(bysha)&baseline),'new_unique_sha_count':len(set(bysha)-baseline),'all_SHA_bytes_dimensions_verified':True,'historical_failure_attempts':len(failures),'remaining_failed_originals':[x['id'] for x in failures if x['id'] not in artifacts]}
(folder/'receipts.json').write_text(json.dumps({'schema':'chooguard.floor2-pass2-source-receipts.v1','reference_only':True,'physical_accuracy_verified':False,'stats':stats,'sources':sources,'artifacts':successful,'duplicate_sha_groups':{k:v for k,v in bysha.items() if len(v)>1},'failures':failures},ensure_ascii=False,indent=2))
sealed=json.loads((folder/'sign-controls-sealed.json').read_text())
for c in sealed['controls']:c['artifact']=artifacts[c['source_id']]
(folder/'sign-controls-sealed.json').write_text(json.dumps(sealed,ensure_ascii=False,indent=2))
structure_specs=[
('mainhall-roof-gallery','whole-station-amlee-000',['treebranchsteelcolumn','triangularrooftruss','highouterglazing','3Fgallerygreenrail','escalator/stairdownforeground','2Fhallbelowgallery'],['photo fromgallery mixes2F/3F; no independentroofheight/span','cannotproveoriginal17mcapabsencefromsingleperspective']),
('mainhall-wideboundaries','whole-station-amlee-004',['hallsteelcolumn/trusspattern','rightgallerygreenrail','seatingmainfloor','oppositesuspendedLEDboards','oppositeelevatedglazedfront'],['no calibratedcamera/metric dimensions','farwallpartialobservation']),
('lounge-stair-corner','lounge-chulinbone-003',['samephoto27frontcorner','charcoalribbedfascia','steelcornerpost','sideglazing','greenrail','stairflightdownRIGHTforeground'],['exact11facilitynativeobjectidentitynotestablished','stepcount/fulllandingpartlyoccluded']),
('lower-exit2-elevator','lounge-321fast-051',['yellow2나가는곳doors','rightdarkEVdoorswithglyph/indicator','metalcolumn','upperglazedshaft/balustrade','lower부산어묵windowbesideexit'],['photodownfrom2F;lower1Fdoorsnot2Fpairedshaftproof','051/052notcontinuoussamecapture']),
('upper-ticket-corridor','lounge-321fast-052',['ticketmachinesRIGHT','OliveYoungfarend','platform↖sign','toilet/parking←sign','lowcircularceiling','metalrectangularpillarLEFT'],['proseroutealonecannotassignguideEVsymbol','unverifiedcapturedate']),
('exit7-low-corridor','lounge-heyarchive-006',['yellow7나가는곳sign','Bus/Taxi/Metro/EurasiaPlatformdirections','guide19꽃들RIGHT','glassdoorsbehindflowerstore','lowcircular-panelceiling','metalrectangularpillars'],['separatephoto notprovencontinuouswithlounge','upperentranceguide-IDnotassigned']),
('lounge-high-low-boundary','lounge-heyarchive-008',['27openentryLEFT','27sideglassRIGHT','samecornerpost','greenhallrail/stairvoidRIGHT','lowcircularceilingtransitiontohighouterglazing'],['nometrictotalheight','nohiddenstair/EV geometryinference']),
('lounge-wide-hall','lounge-heyarchive-009',['27fullfrontacrosshall','metalpillarfrontRIGHT','overheadsign/lowceiling','ticketboundarysurroundings'],['MPOoriginalbytes4032×3024','noobject-IDnativecorrespondence'])
]
struct=[]
for id,src,controls,limits in structure_specs:
 struct.append({'id':id,'classification':'directly_supported','artifact':artifacts[src],'controls':controls,'limits':limits,'reference_only':True,'physical_accuracy_verified':False})
exclusions=[
{'scope':'guide20-Krispy','classification':'insufficient','reason':'Actualfronts retrieved but no sameframeouterentry/neighbors toassign20 ratherthanexisting7. rano001 iscentralhallunderLEDboards; phditebody saysfarfromentry/nearplatform. Fourextraqueries and twofinalsourcepages no20anchor. Do notapply7facadeto20.','evidence_pages':['krispy-inyung','krispy-rano','krispy-merry','krispy-phdite','krispy-silveragent','krispy-mjworld'],'logs':['search-krispy20.txt','pages-structural.txt','pages-final.txt']},
{'scope':'AI/search-summaries','reason':'GoogleAIoverviews andgeneratedrenders/listings are navigation only; not structureauthority. No AI-labelledphotographs accepted. Photographic authenticity not independentlyverified.'},
{'scope':'otherfloor/otherstation','reason':'whole-station-amlee020–028 includes1Fstorephotos/productdetails; lounge-miliyam006–011 areOsongstation. ExcludedfromBusan2Fgeometry.'},
{'scope':'historic-yubu/mjworld','reason':'2014Yubucapturepublished2020 and2022Krispy photos provide historicalnavigation/continuity only; not2026 facade or survey proof.'},
{'scope':'imagepeople/promotions/thumbnailduplicates','reason':'Only ownunobscuredsignquads textureeligible; peopleinteriors/floorpromotionoverlays not wholesale facade textures. duplicateSHA groups notindependent evidence.'},
{'scope':'11unresolvedfacilitynativepairings','reason':'Newphotos provide lounge/stair/exit2EV/exit7corridor boundaries but noindependentguide-symbol-to-native-objectone-to-one proof. Keep exactupperleft/toilet/entrypair identitiesinsufficient untilgeometrylane triangulates.'}
]
bundle={'schema':'chooguard.floor2-pass2-source-controls.v1','reference_only':True,'physical_accuracy_verified':False,'scene_baseline_sha256':'80a55958f086fd89ca524f1124643003a4df387eca05869dea61424650f15af9','integrity':stats,'sources':sealed['controls']+struct,'exclusions':exclusions,'sign_controls':str((folder/'sign-controls-sealed.json').relative_to(root)),'receipts':str((folder/'receipts.json').relative_to(root)),'new_video_downloads':0,'recommendation':'applyfiveobservedtenantfrontsignpatcheswithinexistinggeometryafterobjectcorrespondence; noglobalwarp/metricclaims'}
(folder/'source-controls.json').write_text(json.dumps(bundle,ensure_ascii=False,indent=2))
selected=[artifacts[c['source_id']] for c in sealed['controls']]+[artifacts[x[1]] for x in structure_specs]
tw,th,cols=400,340,4;sheet=Image.new('RGB',(cols*tw,((len(selected)+cols-1)//cols)*th),'white');dr=ImageDraw.Draw(sheet)
for j,r in enumerate(selected):
 im=Image.open(root/r['path']).convert('RGB');im.thumbnail((tw-10,th-40));x=(j%cols)*tw;y=(j//cols)*th;sheet.paste(im,(x,y));dr.text((x+4,y+th-34),r['id'],fill='black');dr.text((x+4,y+th-18),str(r['image_dimensions'])+' SHA '+r['sha256'][:12],fill='black')
sheet.save(folder/'selected-controls-contact-sheet.jpg',quality=90)
print(json.dumps(stats,ensure_ascii=False))
