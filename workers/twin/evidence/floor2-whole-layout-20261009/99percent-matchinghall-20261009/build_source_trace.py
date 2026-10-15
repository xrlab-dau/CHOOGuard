from pathlib import Path
import json,hashlib,numpy as np,math
from PIL import Image,ImageDraw,ImageFont
E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-whole-layout-20261009/99percent-matchinghall-20261009')
R=Path('workers/twin/evidence/floor2-architectural-pdf-search-20261009/official-100083811-store-plan-page-1-embedded-2.jpeg');reg=json.loads(Path('workers/twin/output/floor2-official-pdf-registration-20261009/architectural-registration.json').read_text());T=np.array(reg['main']['native_UV_to_raster_pixels']);A=T[:2];offset=T[2];I=np.linalg.inv(A);F=np.array(reg['native_station_frame']);h=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
def uv(p):return ((np.array(p)-offset)@I).tolist()
def world(p,y=7):u,v=uv(p);return (F@np.array([v,y,u])).tolist()
features=[]
def line(id,category,pts,level='directly_supported',note='',install=False):
 features.append({'id':id,'category':category,'source_pixels':pts,'native_UV':[uv(p)for p in pts],'world_at_modeled_floorY7':[world(p)for p in pts],'evidence_class':level,'annotation_tolerance_pixels':2,'note':note,'proposed_first_kit':install})
# Entire assigned upper drawing: visible envelope, clipped west external approach, stair/core outlines and platform corridor.
for id,pts in [
 ('hall-north-west',[[66,6],[249,6]]),('hall-north-east',[[281,6],[313,6]]),
 ('hall-west-upper',[[66,30],[66,71]]),('hall-west-middle',[[66,89],[66,146]]),
 ('hall-west-lower-return',[[67,166],[67,169],[83,197],[94,197],[94,205]]),
 ('hall-west-diagonal',[[94,205],[135,274]]),
 ('west-north-vestibule',[[46,0],[95,0],[95,31],[67,31]]),
 ('west-approach-upper-edge',[[0,6],[20,6],[20,14],[46,14]]),
 ('west-approach-lower-edge',[[20,29],[64,29]]),
 ('platform-outer-north',[[364,11],[397,11],[405,16],[456,16],[456,68],[470,70],[470,275]]),
 ('platform-inner-north-return',[[407,28],[446,28],[446,63],[409,63],[409,75]]),
 ('platform-inner-middle',[[410,164],[410,207]]),
 ('platform-inner-lower',[[410,223],[410,275]]),
 ('east-core-north-outline',[[313,6],[363,6],[363,101],[330,101],[330,74],[313,74],[313,6]]),
 ('east-core-north-west-return',[[313,18],[320,18],[320,43],[313,43]]),
 ('east-core-north-bay-partitions',[[313,53],[320,53],[320,62],[313,62]]),
 ('east-core-north-lower-bay',[[313,63],[320,63],[320,73]]),
 ('east-core-north-lower-return',[[330,74],[337,74],[337,100]]),
 ('east-core-middle-outline',[[330,145],[361,145],[361,198],[310,198],[310,177],[329,177],[329,145]]),
 ('east-core-middle-west-room',[[310,177],[325,177],[325,198]]),
 ('east-core-middle-west-return',[[325,177],[325,185],[330,185],[330,163]]),
 ('east-core-middle-shaft',[[313,151],[324,151],[324,163],[313,163],[313,151]]),
 ('east-core-middle-service-box',[[307,166],[318,166],[318,176],[307,176],[307,166]]),
 ('east-core-south-outline',[[304,205],[336,205],[336,222],[313,222],[313,275]]),
 ('east-core-south-east-boundary',[[361,201],[361,275]]),
 ('east-core-south-west-room1',[[320,227],[335,227],[335,244]]),
 ('east-core-south-west-room2-upper',[[320,227],[320,246],[335,246]]),
 ('east-core-south-west-room2-lower',[[320,248],[320,260],[335,260],[335,248]]),
 ('east-core-south-west-room3',[[320,262],[320,270],[332,270]]),
 ('east-core-south-central-return',[[343,201],[343,244]]),
 ('east-core-south-central-partition',[[343,219],[361,219]]),
 ('east-core-south-central-partition2',[[343,232],[361,232]]),
 ('east-core-south-east-room1',[[343,250],[343,256],[347,256]]),
 ('east-core-south-east-room1-back',[[348,257],[361,257]]),
 ('east-core-south-east-room2',[[343,261],[343,270],[361,270]]),
 ('east-core-south-east-room2-mouthreturn',[[343,265],[346,265]]),
 ('east-core-south-southwest',[[320,270],[320,275]])
]:line('MH2F-'+id,'wall_centerline',pts,note='Visible architectural line only. Tiny cell/fixture marks omitted. Sourcepixel endpoints are inspectable annotations, not survey dimensions.',install=id.startswith('east-core-south'))
# Narrow line in hall is drawn lighter than walls; do not turn it into an opaque wall.
line('MH2F-west-light-line','drawing_line_ambiguous',[[110,33],[110,79],[99,148]],'insufficient','Light double line may be an overhead/gallery edge; no opaque wall or slab generated.')
# Door banks and large open corridor connections.
for id,pts,note in[
 ('north-entry-bank',[[249,6],[281,6]],'Door bank at north edge; individual leaf widths not inferred.'),
 ('west-platform-mouth-upper',[[20,72],[67,72],[67,88],[20,88]],'Stair/ESC outline. No numbered platform binding.'),
 ('west-platform-mouth-lower',[[66,148],[102,148],[102,165],[66,165]],'Stair/ESC outline. No direction or replacement ramp inferred.'),
 ('central-hall-core',[[223,76],[256,76],[256,96],[223,96],[223,76]],'Native core footprint comparator only; no additional vertical operator.'),
 ('east-core-north-openpassage',[[351,102],[361,102],[361,144],[351,144]],'Open hall/platform passage between two service clusters. Keep open.'),
 ('east-platform-mouth-upper',[[361,75],[411,75],[411,88],[361,88]],'Native stairs/ESC require current-source pairing before adjustment.'),
 ('east-platform-mouth-middle',[[361,146],[412,146],[412,164],[361,164]],'Native stairs/ESC require current-source pairing before adjustment.'),
 ('east-platform-mouth-lower',[[361,208],[413,208],[413,223],[361,223]],'Native stairs/ESC require current-source pairing before adjustment.'),
 ('east-platform-mouth-seam',[[361,282],[412,282],[412,300],[361,300]],'Crosses y275 boundary; shared drawing control only. MAIN owner owns lower implementation.'),
 ('hall-main-shared-edge',[[135,278],[149,295],[250,295],[250,281],[305,281]],'Below y275 is MAIN-owned. Drawing edge traced for continuity, never double-generated.'),
 ('platform-main-turn',[[470,275],[464,275],[464,305],[414,305]],'Outercorner470,275 andinnerreturn464,275 are distinct visible source outlines. MAIN owner owns y≥275.')
]:line('MH2F-'+id,'portal_or_core_footprint',pts,note=note)
# Outer rings establish footprint centers; upper architecture/photo does not certify seven identical tree-column forms.
for i,(x,y,r)in enumerate([(147.5,81.5,4),(174,81.5,4),(200.5,81.5,4),(174.5,155,5),(253,155,5),(174.5,214.5,5),(253,214.5,5)],1):
 features.append({'id':f'MH2F-ring-column-{i:02}','category':'large_circular_support_symbol','center_sourcepixel':[x,y],'radius_sourcepixel':r,'native_UV':uv([x,y]),'world_at_modeled_floorY7':world([x,y]),'evidence_class':'directly_supported_footprint_only','annotation_tolerance_pixels':2,'proposed_first_kit':False,'note':'Footprint symbol is explicit. Do not infer tree branches/lantern morphology or create a duplicate of an existing native support.'})
# Original square/circular small column markers across hall and platform strip. Ambiguous furniture rectangles excluded.
columns=[(122,14),(148,14),(174,14),(201,14),(227,14),(306,14),(121,31),(148,31),(174,31),(201,31),(227,31),(253,31),(280,31),(306,31),(95,81),(121,81),(69,70),(69,140),(279,145),(279,163),(279,198),(279,213),(307,145),(307,163),(307,213),(307,281),(280,281),(253,281),(122,277),(148,277),(174,277),(201,277),(227,277),(438,74),(465,74),(465,89),(465,146),(465,164),(465,207),(465,222),(438,275),(465,275)]
for i,p in enumerate(columns,1):features.append({'id':f'MH2F-small-support-{i:02}','category':'small_support_symbol','center_sourcepixel':list(p),'native_UV':uv(p),'world_at_modeled_floorY7':world(p),'evidence_class':'directly_supported_symbol_only','annotation_tolerance_pixels':2,'proposed_first_kit':False,'note':'No section shape, height or material assigned from tiny plan symbol.'})
# Platform islands are visibly beyond hall outer boundary; trace all four y<275 for completeness, no hall-floor extension.
for i,(a,b)in enumerate([(14,44),(72,102),(144,174),(201,231)],1):line(f'MH2F-isolated-platform-core-{i}','external_platform_core',[[575,a],[628,a],[628,b],[575,b],[575,a]],note='Separate platform stair/island footprint. Hall slab must not be extended to this island.')
trace={'schema':'chooguard.official2f-matchinghall-trace.v1','source_pdf':reg['source_pdf'],'source_pdf_sha256':reg['source_pdf_sha256'],'source_raster':str(R),'source_raster_sha256':h(R),'source_dimensions':[631,496],'primary_geometry_authority':'official2026architectural_sourcepixel_outlines','assignment':'Hall and platform corridors y<275; shared seam controls below y275 only. Far platforms separately traced.','pixel_to_native_UV':'(pixel-main_translation) @ inverse(main_linear)','native_UV_to_sourcepixel':T.tolist(),'native_local_xyz':'V,Y,U','world_matrix3x3':F.tolist(),'accepted_field_metric':False,'modeled_floorY_reference':7,'annotation_tolerance_pixels':2,'native_model_role':'current-contributor localization and modeled height reference; never veto directly evidenced plan topology','main_seam_shared_with':'workers/twin/output/99percent-main2f-20261009/','features':features,'remaining_limits':['Lowresolution2pixel uncertainty is significant; cells, fixture counts and gender are not promoted.','Top-view plan supplies no ceiling/roof elevations; retain current source vertical assemblies until independent section/photo proof.','Sourcepixel hall map extends beyond some native current supports under single seam scaffold; runtime floor/collider checks must reject unsupported spans.','No native operation axes or station point endpoints moved by this draft.']}
(O/'source-pixel-trace.json').write_text(json.dumps(trace,ensure_ascii=False,indent=2)+'\n')
# Full drawing and assigned area shown together, with source pixels preserved and numbered controls.
base=Image.open(R).convert('RGB').resize((2524,1984));d=ImageDraw.Draw(base);colors={'wall_centerline':'#00a473','portal_or_core_footprint':'#0477d0','drawing_line_ambiguous':'#b06fb1','external_platform_core':'#7c6cb7'}
for f in features:
 if 'source_pixels'in f:
  pp=[(p[0]*4,p[1]*4)for p in f['source_pixels']];d.line(pp,fill=colors[f['category']],width=3);p=pp[0];d.text((p[0]+3,p[1]+3),f['id'].replace('MH2F-',''),fill=colors[f['category']])
 else:
  x,y=f['center_sourcepixel'];rr=f.get('radius_sourcepixel',1.3);d.ellipse(((x-rr)*4,(y-rr)*4,(x+rr)*4,(y+rr)*4),outline='#d02667',width=3)
d.line((0,1100,2524,1100),fill='#db6120',width=3);d.text((1760,1106),'y275 MAIN/HALL ownership seam',fill='#db6120');base.save(O/'full-area-source-trace.png');base.crop((1160,770,1500,1120)).resize((1020,1050)).save(O/'priority-east-strip-trace.png')
# Current inferred room footprints are explicit existing source rows, not plan room identities.
services=json.loads(Path('content/world/observed-interiors/services-2f.json').read_text());ids=['WC-2F-B1-M','WC-2F-B1-F','WC-2F-B2-A','RM-2F-B-역무실','RM-2F-B-수유방'];existing=[]
for e in services['elements']:
 if e['id']in ids:
  poly=np.array(e['geometry']['polygon']);pix=np.c_[poly,np.ones(len(poly))]@T;existing.append({'id':e['id'],'source_row':e,'sourcepixel_current_MAIN_scaffold':pix.tolist(),'sourcepixel_current_BOX_diagnostic':(np.c_[poly,np.ones(len(poly))]@np.array(reg['box']['native_UV_to_raster_pixels'])).tolist(),'semantic_native_pairing_accepted':False})
(O/'existing-qualitative-room-selection.json').write_text(json.dumps({'source':'content/world/observed-interiors/services-2f.json','source_sha256':h('content/world/observed-interiors/services-2f.json'),'builder_sha256':h('AgentScripts/MajibangBuilder.cs'),'entries':existing,'exact_triangle_selection_requires_root_current_contributor_export':True},ensure_ascii=False,indent=2)+'\n')
compare=Image.open(R).convert('RGBA').resize((2524,1984));d=ImageDraw.Draw(compare)
for f in features:
 if f['category']=='wall_centerline':d.line([(x*4,y*4)for x,y in f['source_pixels']],fill='#0da76c',width=3)
for e in existing:
 pix=e['sourcepixel_current_MAIN_scaffold'];d.line([(x*4,y*4)for x,y in pix+[pix[0]]],fill='#d5273d',width=5);d.text((pix[0][0]*4+5,pix[0][1]*4+5),e['id'],fill='#d5273d')
compare.save(O/'current-five-envelopes-vs-source.png')
# Implementable finite source-derived wall geometry: lower east strip only, zero floor/roof/fixture creation.
kit=[];mapping=[]
for f in features:
 if not f.get('proposed_first_kit'):continue
 pts=f['source_pixels']
 for k,(a,b)in enumerate(zip(pts,pts[1:])):
  au,bu=uv(a),uv(b);eid=f['id']+f'-run{k+1}';kit.append({'type':'SourceDiagramWall','id':eid,'zone':'2F matching hall source PDF','tier':'b','frame':'root','evidence':[str(O/'source-pixel-trace.json')+'#'+f['id']],'geometry':{'edge':[au,bu],'y':7,'top':10},'params':{'thickness':.12,'finish':'gypsum'},'note':'Plan-supported horizontal centerline; y7/top10/thickness.12 are existing modeled service-wall conventions. No slab/roof/furniture/operators.'});mapping.append({'id':eid,'trace_id':f['id'],'sourcepixel_edge':[a,b],'native_UV_edge':[au,bu],'world_edge_at_Y7':[world(a),world(b)]})
proposal={'zone':'2F matching hall source PDF','root':'실내 트윈 마감/2F PDF 맞이방 원도 복원','basis':{'sourcepixel_trace':str(O/'source-pixel-trace.json'),'sourcepixel_trace_sha256':h(O/'source-pixel-trace.json'),'native_UV_to_raster':T.tolist(),'world_frame':'station_yaw16.2_localXYZ(V,Y,U)','modeled_vertical_reference':'Current existing room Y7/top10.0; plan supplies no measured vertical dimensions.','draft_only':True},'elements':kit}
(O/'matchinghall-east-strip-kit-draft.json').write_text(json.dumps(proposal,ensure_ascii=False,indent=2)+'\n');(O/'matchinghall-east-strip-mapping.json').write_text(json.dumps({'schema':'chooguard.matchinghall-source-wall-mapping.v1','sourcepixel_trace_sha256':h(O/'source-pixel-trace.json'),'elements':mapping,'first_scope':'lower east-strip wall runs y201..275. Upper toilet/middle room clusters are trace-only pending higherresolution source.','replacement_policy':'Replace exact five inferred envelope contributors only where new architecture overlaps; retain unrelated source batches and positively observed office-glass appearance through a scoped replay. Do not duplicate native opaque faces: partition any intersection before installation.','actual_apply_ready':False},ensure_ascii=False,indent=2)+'\n')
print('FEATURES',len(features),'WALLRUNS',len(kit),'TRACE',h(O/'source-pixel-trace.json'))
