import json,datetime,hashlib
from pathlib import Path
R=Path(__file__).resolve().parent
B=Path.cwd()
faces=json.loads((R/'EXACT-CURRENT-HOST-FACES-R28.json').read_text())
anchors=[
 {'source_original_px':[305,281],'source_feature':'western staff-core outer native pier at nursery/office return','current_object_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-1195977420-0','name':'NATIVE_COL_002_2F_MODEL','current_body_bbox_center_actual_qx_qy':[-3.453,-14.562],'qualification':'source corresponding pier identity proposed from topology; source pixels approximate +/-2px; exact current footprint immutable'},
 {'source_original_px':[332,330],'source_feature':'first counter-band structural support on public-front row','current_object_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-918840566-0','name':'C177_UPPER_018','current_body_bbox_center_actual_qx_qy':[4.914,-.547],'qualification':'local row correspondence candidate; source pixel not a metric fit'},
 {'source_original_px':[359,330],'source_feature':'left retained ticket-counter structural support','current_object_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-100282045-0','name':'C177_UPPER_019','current_body_bbox_center_actual_qx_qy':[13.132,-.465],'qualification':'local structural row correspondence; current exact face offsets differ from drawn centers'},
 {'source_original_px':[386,330],'source_feature':'middle ticket-counter structural support','current_object_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-1839616863-0','name':'C177_UPPER_020','current_body_bbox_center_actual_qx_qy':[20.396,-.046],'qualification':'local structural row correspondence; no source-image distance used as metric'},
 {'source_original_px':[412,330],'source_feature':'right counter terminal pier / public Track passage T-corner','current_object_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-544407206-0','name':'C177_UPPER_021','current_body_bbox_center_actual_qx_qy':[27.48,-.012],'qualification':'right end of same counter row; exact wall sideface obtained from current wall1331305054, not source stroke'}
]
(R/'SOURCE-TO-CURRENT-STRUCTURAL-POINT-CORRESPONDENCES-R28.json').write_text(json.dumps(anchors,ensure_ascii=False,indent=2))
record={
 'schema':'chooguard.police.four_plan_local_face_migration.r28.v1','utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
 'author':'classic:/root/r28_police_multiplan','status':'BOUNDED_EXISTING_FRONT_PROPOSAL_AND_TVM_RETURN_FACE_CORRESPONDENCES_COMPLETE_READ_ONLY',
 'user_instruction':'Use original2F + specialist plan + 2F guide + actual vector CAD in overlap correspondence. No renewed user location question.',
 'Scene_Assets_mutated':False,'agent_QA_Play_probe_reopen_capture':False,
 'source_viewing_and_production_CAD_analysis_only':True,
 'sources':[
  {'id':'ORIGINAL2F_CLEAN','path':str(R.relative_to(B)/'OFFICIAL2026-EMBED-2.jpeg'),'source_pdf':'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/krispy-southwest/sources/R26-OFFICIAL100083825-MAP.pdf','xref':36,'dimensions_px':[631,496],'qualification':'Direct clean source image extraction, no user annotation or replacement reconstructed. Same underlying source as specialist plan, NOT an independent corroborating drawing.'},
  {'id':'SHOP20261014','path':'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/krispy-southwest/sources/R26-OFFICIAL100083825-MAP.pdf','url':'https://www.korailretail.com/board/boardDetail.do?boardTypeNum=4&boardNum=100083825','epoch':'2026-10-14','role':'tenant identity/neighborhood only, rectangular marks not metric'},
  {'id':'GUIDE20260419','path':'.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/research/root/current-guide-2f-original-20260419.jpg','epoch':'field2026-04-19','role':'Namcheon26 -> railwaypolice service band; stationroom/nursery/TVM west return; publicTrack routes remain open'},
  {'id':'ACTUAL_CAD007','path':'.state/ACTIVE-ALL2F-COMPLETION-R26/cad/output-007/CURRENT-LIVE-2F-OBJECT-VECTOR-CAD-R26-NO-TEXT.json','role':'Actual current native/wall/column metric faces, qx,-qy metres. Snapshot precedes user TVM removal. Candidate CAD drawings omit rejected TVM banks.'},
  {'id':'ORIGINAL2022_VECTOR_BASE','path':'.state/ACTIVE-ALL2F-COMPLETION-R26/original-design-discovery/storyway-public-contracts/operator-archive-r27/transport-mirror-r28/specialist2022-2f-original-drawings-FILE00.pdf','page1based':5,'role':'Independent dimensioned structural/full staff/stair/WC context. PDFrotation270 must be applied to vector path coordinates. Preserve source epoch; no global source warp into CAD.'},
  {'id':'KORAIL_OFFICIAL_202_MAP','url':'https://www.korail.com/docs/2009/htm/htm21000/w_htm21327.jsp','path':str(R.relative_to(B)/'KORAIL-MEETING-201202-OFFICIAL.html'),'map':str(R.relative_to(B)/'KORAIL-OFFICIAL-MEETING-2F-MAP.jpg'),'qualification':'Official currently retrievable legacy page, publication/asbuilt epoch unknown. Says 2F exit7 -> right railwayjudicialpolice next to meeting202. Map shows police then201 then202 on stepped return; not two adjacent independent201/202 entryfronts.'},
  {'id':'POLICE_2013_FRONT','path':'.state/ACTIVE-ALL2F-COMPLETION-R26/public/rail-police/sources/GC042P24075-public-original.jpg','epoch':'2013-10-26','role':'Left opaquegraysinglepolicedoor + barredwindow + publicplaque; shared frosted201/202glassaccess immediatelyright. Historical visible shape, not current2026 finish/notice identity proof.'}
 ],
 'new_exact_source_fact':'Official KORAIL202 map and historical front show police -> 201/common201202 access -> stepped202. The old R14 first+second fronts were only boundedfunctional room MODELS and never established individual201/202 identities. Therefore their two-room names cannot exclude police from the existing first front.',
 'police_candidate':{
  'host':faces[0],'source_family_assignment':'First EXISTING south/public-facing front immediately after retained Namcheon-side Track passage, before existing next conference-access front.',
  'candidate_current_span_actual_qx_qy':[[59.449273,0.048212],[66.694072,0.262288]],
  'outward_normal_approximately_actual_qx_qy':[-.004705, .999989],
  'existing_not_new_closure':True,
  'classification':'BOUNDED_SOURCE_TO_CURRENT_GROUP_INFERENCE; individual current2026 door/asbuilt owner not independently proven',
  'door_aperture_exact_metric':None,
  'door_shape_source':'2013 graysingleleaf left, barredwindow right; fit only own host if root elects source-qualified MODEL authoring. No unmeasured physical width is asserted.',
  'protected_open_portal_actual_qx_qy':[[51.106,-6.65],[58.933,-.257]],
  'preserve_no_new_front_at_qx51_59_qy0':True,
  'existing_next_front_reference':faces[1],
  'far_east_return_reference':faces[2],
  'not_established':'An exact2026 police-door location is not proved by source naming alone. This is a concrete bounded owner proposal supported by local topology and new official map, not a license to move fixed walls/columns/native geometry or close the portal.'
 },
 'rejected_alternative':{
  'owner':'989813836','bounds_actual_qx_qy':[51.028,-9.305,58.977,-9.087],
  'reason':'Rear internal stair/service corridor face; current official public-guide assigns police to the main service band and primary meeting-source route reaches publicfront from exit7. No matching2013front-to-current internalunderstairface established. Do not substitute this merely because qx aligns.'
 },
 'TVM_source_to_face_addendum':{
  'track9_four':{'host':faces[3],'source_original_locator_segment_px':[[306,305],[306,328]],'public_normal_actual_qx_qy':[-1,0],'role':'WEST-facing continuation on the same staff/nursery side return, NOT a bar across the public Track portal. Preserve south T-junction access.'},
  'separate_five':{'host':faces[4],'source_original_locator_segment_px':[[412,305],[412,330]],'public_normal_actual_qx_qy':[1,0],'role':'EAST-facing counter terminal return bordering the portal, not front-facing closure beyond counterend. Exact row terminal/native support determines the offset; count/detailed placement use actual field source, not map icons.'},
  'structural_point_pairs':str(R.relative_to(B)/'SOURCE-TO-CURRENT-STRUCTURAL-POINT-CORRESPONDENCES-R28.json'),
  'exact_side_face_vertices':str(R.relative_to(B)/'EXACT-CURRENT-HOST-FACES-R28.json'),
  'source_pixels_are_identity_only':True,'bank_physical_extents_must_be_fit_inside_actual_host_faces':True
 },
 'artifact':str(R.relative_to(B)/'FOUR-PLAN-LOCAL-FACE-MIGRATION-R28-NO-ADDED-TEXT.pdf'),
 'overlay_style':'Actual/current/reference geometry achromatic; orange only proposed existing police front or requested TVM hostfaces. No added visible labels/title/legend/page numbers. Source preexisting print labels retained.',
 'source_original_ink_not_globally_warped':True,'fixed_CAD_native_positions_unchanged':True,
 'missing_information_question_required':False,
 'whole2F_complete_claim':False,
 'stop_condition':'Read-only concrete correspondence/host proposal delivered; root owns apply decision and remaining whole2F work.'
}
(R/'POLICE-FOUR-PLAN-HOST-PROPOSAL-R28.json').write_text(json.dumps(record,ensure_ascii=False,indent=2))
(R/'WORKER-COMPLETION-R28.json').write_text(json.dumps({'status':record['status'],'report':str(R.relative_to(B)/'POLICE-FOUR-PLAN-HOST-PROPOSAL-R28.json'),'overlay_pdf':record['artifact'],'new_primary':'Officialkorailmeeting202mapandrouteactuallyacquired','police_owner_candidate':'1117893400','candidate_not_unqualified_current2026_field_proof':True,'TVM_host_faces':['288332569','1331305054'],'Scene_Assets_mutated':False,'next':'Root owns source-qualified construction or stronger individual binding; no repeated user question.'},ensure_ascii=False,indent=2))
(R/'task_plan.md').write_text('# Police four-plan source binding R28\n\n1. All four source families plus authentic2022vectorbase inspected. COMPLETE\n2. Local structural correspondence and existing front/portal topology derived. COMPLETE\n3. New official KORAIL202map acquired; concrete bounded host proposal and source/CAD production overlay delivered. COMPLETE\n\nRead-only lane. No Scene/Assets mutation or agent QA. Individualcurrent2026policedoor/asbuilt owner remains qualified, not falsely asserted exact.\n')
with (R/'findings.md').open('a') as f:f.write('\nBREAKTHROUGH: currently retrievable official KORAIL202 page supplies explicitpolicenextdoorroute and old source map police→201→stepped202. This contradicts treating R14 two side-by-side functionalMODELS as individuallyproven201+202. First existing front1117893400, qx59.449..66.694/qy0.048..0.262, is a bounded policehostproposal; openportal51..59 stays open. Existing nextfront1528514897mayservecommonconferenceentry. Current2026singleleafmetric/doorowner remains qualified. All four planfamilies included in5p productionPDF; TVM returnhosts288332569WEST and1331305054EAST have exactactualfaces and5 source-pointcorrespondences.\n')
with (R/'progress.md').open('a') as f:f.write('\nDelivered 5p four-plan production overlay with no added labels; exact5hostfaces,5 local structuralpointpairs,cleanoriginal image,primarymeetingmap/HTML/sourceindex and qualifiedpoliceproposal. CurrenteditingScene/Assets untouched. No agentQA. Read-only scope complete.\n')
print('complete',R/'WORKER-COMPLETION-R28.json')
