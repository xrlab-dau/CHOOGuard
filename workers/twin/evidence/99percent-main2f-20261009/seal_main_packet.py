import json,hashlib,struct
from pathlib import Path
from collections import Counter
import numpy as np,cv2
from shapely.geometry import Polygon,LineString,Point
ROOT=Path('/Users/um-yunsang/CHOOGuard');O=ROOT/'workers/twin/output/99percent-main2f-20261009';E=ROOT/'workers/twin/evidence/99percent-main2f-20261009'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(n):return json.loads((O/n).read_text())
def dump(n,x):(O/n).write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n')
P=read('main2f-source-architecture-trace.json');S=read('main2f-kit-correction-spec-draft.json');C=read('current-topology-comparison.json');B=read('east-service-generated-contributor-selection-draft.json');M=np.array(P['native_UV_to_source_pixels']);di={x['id']:x for x in C['source_run_diagnostics']}
checks=[]
def check(name,ok,detail):checks.append({'check':name,'pass':bool(ok),'detail':detail})
check('source bytes exact',sha(ROOT/P['source'])==P['source_sha256'],P['source_sha256'])
check('comparison/selection source epoch matches',C['source_packet_sha256']==sha(O/'main2f-source-architecture-trace.json')==B['source_packet_sha256'],'No stale trace hash after final eight-core revision.')
roundtrip=[]
for x in P['room_envelopes']:
 p=np.array(x['native_polygon_UV_reference']);q=np.c_[p,np.ones(len(p))]@M;roundtrip.append(float(np.max(np.abs(q-np.array(x['source_polygon_pixels'])))))
check('pixel/native inverse round trip',max(roundtrip)<1e-8,{'max_pixel_error':max(roundtrip),'meaning':'Numerical conversion only, not field accuracy.'})
polys=[Polygon(x['source_polygon_pixels'])for x in P['room_envelopes']];check('room floor polygons valid/nonoverlapping',all(p.is_valid and p.area>0 for p in polys)and all(a.intersection(b).area<1e-8 for i,a in enumerate(polys)for b in polys[i+1:]),'Eight traced service/WC polygons share boundaries only; all original floors preserved.')
# Source ink agreement is an explicit raster-reading diagnostic.
gray=cv2.imread(str(ROOT/P['source']),0);distance=cv2.distanceTransform((gray>=155).astype('uint8'),cv2.DIST_L2,5);ink=[]
for r in P['wall_and_boundary_runs']:
 if not r['kit_draft_candidate']:continue
 a,b=np.array(r['source_segment_pixels']);q=np.linspace(a,b,max(2,int(np.linalg.norm(b-a)*3)));ix=np.rint(q).astype(int);dd=distance[ix[:,1],ix[:,0]];ink.append({'id':r['id'],'fraction_within2pixels_of_dark_source_ink':float(np.mean(dd<=2)),'max_distance_pixels':float(dd.max())})
check('candidate wall controls follow visible ink',all(x['fraction_within2pixels_of_dark_source_ink']>=.9 for x in ink),ink)
# Ensure gaps are not filled by a later candidate wall run.
blocked=[]
for p in P['portals']:
 a,b=np.array(p['source_jamb_pixels']);mid=a*.5+b*.5
 hits=[r['id']for r in P['wall_and_boundary_runs']if r['kit_draft_candidate']and LineString(r['source_segment_pixels']).distance(Point(mid))<.2]
 if hits:blocked.append({'portal':p['id'],'midpoint_crossing_wall_ids':hits})
check('explicit portal midpoints remain open',not blocked,blocked)
check('no unsupported fixture counts/type creation',not any(x['type']in ['SourceDiagramWC','SourceDiagramStallDoor']for x in S['elements']),'No WC fixture or stall-door guesses in this2F architecture draft; visible envelope only.')
check('builder reuses existing source diagram helpers',all(('case "'+k+'"')in(ROOT/'AgentScripts/MajibangBuilder.cs').read_text()for k in set(x['type']for x in S['elements'])),'SourceDiagramWall/Floor and ServiceDoor already implemented; no new builder helper needed.')
check('all selected current assets match actual export',all(x['current_asset_matches_export']for x in B['selection']),'Twelve generated geometry channels, exact MF/MC IDs resolved from saved YAML same-GameObject components; MR IDs not used as MF.')
for x in B['selection']:
 n=x['mesh']['submeshes'] # actual flattened triangle bounds from native binary
 blob=(ROOT/x['mesh']['binary']).read_bytes();nv,ni,nn=struct.unpack_from('<iii',blob);ids=x['selected_inside_triangle_indices'];cross=x['boundary_crossing_triangle_indices'];ok=not(set(ids)&set(cross))and all(0<=i<ni//3 for i in ids+cross)
 check('bounded triangle indices '+str(x['exact_geometry_component_file_ids']),ok,{'inside':len(ids),'crossing':len(cross),'mesh_triangles':ni//3})
 x['exact_geometry_component_global_ids']=[x['observed_inventory_owner'].rsplit('-',2)[0]+'-'+str(i)+'-0'for i in x['exact_geometry_component_file_ids']]
# Do not blind-append source walls that already coincide with original shell.
stages=[]
for r in P['wall_and_boundary_runs']:
 if not r['kit_draft_candidate']:continue
 d=di[r['id']];native=d['native_section_within_2p5px_fraction'];generated=d['generated_service_section_within_2p5px_fraction']
 status='reuse/reconcile existing original boundary'if native>=.75 else 'source-backed missing boundary candidate'if native<.25 and generated<.25 else 'source-backed partial/replacement boundary candidate'
 stages.append({'id':'PDF2F-'+r['id'],'region':r['region'],'source_segment_pixels':r['source_segment_pixels'],'stage':status,'append_by_default':False,'native_section_near_fraction':native,'generated_section_near_fraction':generated,'required_before_install':'Exact contributor retention and native core collision review; source-visible topology must remain open at all recorded portals.'})
B['identity_convention']='Observed inventory renderer IDs remain evidence only. Exact geometry component IDs/global IDs are class33 MeshFilter or class64 MeshCollider derived from current saved YAML; no +1 arithmetic.';dump('east-service-generated-contributor-selection-draft.json',B)
receipts=json.loads((ROOT/'asset-library/research-public/2026-10-09/five-storey-architecture/official-meeting-rooms/download-receipts.json').read_text());loc=[x for x in receipts if x['room']in ['2F-201','2F-202']and x['alt']=='회의실 약도'];crosscheck={'sources':loc,'verified_source_hashes':[{'path':x['path'],'sha256':sha(ROOT/x['path']),'matches_receipt':sha(ROOT/x['path'])==x['sha256']}for x in loc],'directly_supported':['Official2F schematic shows meeting201 and202 at the same eastern terminal suite, past 철도사법경찰대; EV and Exit7 provide qualitative route context.','201 is drawn left/below202; this supports the candidate lower/upper pair, not exact architectural pixel labels.'],'insufficient':['Do not hard-bind201/202 names to a particular clean-plan polygon from this schematic alone.','No scale, structural dimensions or door widths can transfer from the locator drawing.'],'candidate_names':{'east-south-major':'201 candidate, semantic binding provisional','east-north-large':'202 candidate, semantic binding provisional'},'exact_source_labels_in_clean_architectural_raster':'unreadable/not present; draft room names remain unassigned'};dump('official-201-202-qualitative-crosscheck.json',crosscheck)
dump('source-correction-staging.json',{'stage':'reviewable worker-only draft; no production write','wall_staging':stages,'largest_regions':[{'region':'east-service','bounds_pixels':[461,273,610,336],'supported_correction':'Replace nine old generic Naver-based room profiles with clean-plan visible major dividers and portals; preserve eastern original stairs/lift core. Six new traced floor polygons; room numbers provisional.','bounded_contributor_packet':'east-service-generated-contributor-selection-draft.json','limits':'Small north cells, tiny fixture strokes and several door leaf counts unresolved.'},{'region':'central-core','bounds_pixels':[311,275,455,334],'supported_correction':'Add/reconcile source-visible central core-lobby/corridor returns, kept distinct from the adjoining stairs/lift footprint. Eight candidate wall segments.','limits':'Exact original core contributor selection still needed; no source stair operator duplication, no axis warp.'},{'region':'east-WC','bounds_pixels':[579,337,611,390],'supported_correction':'Plan supports two room envelopes, shared divider and open west approaches. Existing outer original wall retained.','limits':'No fixture/stall counts, subtype or gender inference. Engineering75pPDF lacks separate drawings according to root inspection.'},{'region':'west-slanted-wing','bounds_pixels':[58,303,143,414],'supported_correction':'Principal visible lines inventoried for later source comparison.','limits':'Trace-reference-only; fine fixture/room boundaries and entry endpoints insufficient for an installable replacement.'}],'source_scope':'MAIN2F floor-labeled2F only; MAIN below y275 and matched hall above y275 are separate source domains. User main-building5storeys is root all-storey research scope, not evidence that the matchinghall shares5storeys.','shared_seam':{'hall_outer_corner':[470,275],'main_inner_return':[464,276],'note':'Distinct observed ink junctions; one shared MAIN affine for new proposals, no duplicated E-top wall.'},'height_basis':{'floorY':7,'wallTop':10.44,'doorHeight':2.1,'wallThickness':.12,'status':'Inherited modeled values from existing2F service kit, unmeasured. All source geometry editable as separate generated contributors.'},'source_authority':'Direct plan-visible topology may correct the old unmeasured model. Independent main registration localizes it; it does not prove site dimensions or prevent correction where the old native model differs.','full_channel_export_present':False,'required_root_work':['Exact twelve-channel full export before any retained-face replacement, including crossing triangle clipping.','Resolve old service operators against source portals rather than deleting/reusing by position alone.','Review central core intersections before adding its return/lobby walls.','Actual runtime compile, floor support, collision and portal traversal after any installation.'],'production_mutated':False})
check('official locator exact bytes',all(x['matches_receipt']for x in crosscheck['verified_source_hashes']),'Official Korail locator source downloaded by root; photo/upload date not field date; license not independently verified.')
dump('offline-verification.json',{'all_checks_pass':all(x['pass']for x in checks),'checks':checks,'verification_scope':'Finite source reading, inverse registration arithmetic, topology openness, source hash/component/triangle integrity. No Unity/PlayMode/site validation.'})
print(json.dumps({'pass':all(x['pass']for x in checks),'failed':[x['check']for x in checks if not x['pass']],'elements':dict(Counter(x['type']for x in S['elements'])),'wall_stages':dict(Counter(x['stage']for x in stages))}))
