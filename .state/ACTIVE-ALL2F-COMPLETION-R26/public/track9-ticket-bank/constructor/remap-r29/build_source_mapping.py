from pathlib import Path
import json,datetime,numpy as np
from reportlab.pdfgen import canvas
from reportlab.lib.colors import Color
B=Path(__file__).resolve().parent;ROOT=Path('/Users/um-yunsang/CHOOGuard');CAD=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad/output-007/CURRENT-LIVE-2F-OBJECT-VECTOR-CAD-R26-NO-TEXT.json';j=json.loads(CAD.read_text());objects=j['objects'];deleted=json.loads((B.parent/'USER-DELETED-REJECTED-TRACK9-BANK-R29.json').read_text());dead={x['object_id']for x in deleted['exact_tombstones']}
def sections(row):
 sec=row.get('sections',{});out=[]
 if sec:
  key=min(sec,key=lambda k:abs(float(k)-8.4));out=sec[key]
 return [[[p[0],-p[1]]for p in a]for a in out]
def local_rows(box):
 lo,hi=np.array(box[0]),np.array(box[1]);rows=[]
 for r in objects:
  if r['object_id']in dead:continue
  lines=sections(r)
  if not lines:continue
  pts=np.asarray(lines).reshape(-1,2)
  if (pts.max(0)<lo).any()or(pts.min(0)>hi).any():continue
  # Keep all fixed/current structural references; dense product details only when actual local shopfront is requested.
  if r['source_kind']=='CURRENT_R25_MODEL_NOT_NEW_USER_CONFIRMATION' and not any(t in r['path'].lower()for t in ['booth','wall','glass','entry','door','frame','fascia']):continue
  rows.append({'object_id':r['object_id'],'path':r['path'],'source_kind':r['source_kind'],'actual_q_min':pts.min(0).tolist(),'actual_q_max':pts.max(0).tolist(),'lines':lines})
 return rows
nurse=local_rows([[-17,-22],[8,8]]);east=local_rows([[19,-17],[46,8]])
(B/'ACTUAL-CAD007-NURSERY-CORNER-STRUCTURAL-CONTEXT.json').write_text(json.dumps(nurse,ensure_ascii=False,indent=2));(B/'ACTUAL-CAD007-EAST5-BAY-STRUCTURAL-CONTEXT.json').write_text(json.dumps(east,ensure_ascii=False,indent=2))
# Proposed narrow installation along retained nursery-side face only. No Scene construction.
qylo=-7.46143129179382;qyhi=-.9804406299018297;cy=(qylo+qyhi)/2
candidate={'status':'UNAPPROVED_SOURCE_TOPOLOGY_CANDIDATE_ONLY_NOT_SCENE_APPLIED','source_guide_identity':'Automatic ticket machine bank is on same west-facing staff/nursery return before turning onto staffed-counter transverse frontage.','longitudinal_source_axis':'actual qY, not qX','public_face_normal_actual_q':[-1,0],'current_backing_wall_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-288332569-0','current_backing_face_actual_qX':-3.575980917919692,'bounded_end1_native0791_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-1688583679-0','bounded_end2_native017_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-32779078-0','actual_clear_qY':[qylo,qyhi],'candidate_equipment_qY_MODEL':[cy-2.8,cy+2.8],'candidate_footprint_actual_qX_qY_MODEL':[[-4.24,cy-2.8],[-3.59,cy-2.8],[-3.59,cy+2.8],[-4.24,cy+2.8]],'machine_count_from_actual2026source':4,'source_identity_qualification':'Individual pier-to-world aliases remain source-topology MODEL via existing nurse field-to-current alignment, not a surveyed unique individual visual identity. Root must reconcile all map identities before any placement.','former_placement_rejected':'Former frontnormal+qY bar across clearfrontbay atqY0 is withdrawn, not geometry or route authority.'}
(B/'FOUR-TVM-CORRECTED-SIDE-AXIS-CANDIDATE-R29.json').write_text(json.dumps(candidate,ensure_ascii=False,indent=2))
def page(c,rows,box,proposal=None):
 w,h=1100,900;lo=np.array(box[0]);hi=np.array(box[1]);k=min((w-80)/(hi[0]-lo[0]),(h-80)/(hi[1]-lo[1]));ox=(w-k*(hi[0]-lo[0]))/2;oy=(h-k*(hi[1]-lo[1]))/2
 def pt(p):return ox+(p[0]-lo[0])*k,oy+(p[1]-lo[1])*k
 c.saveState();c.setLineWidth(.30)
 for r in rows:
  official=r['source_kind']=='OFFICIAL_NATIVE_EXISTING_GEOMETRY' or '/공식 자료' in r['path'];c.setStrokeColorRGB(.30,.30,.30)if official else c.setStrokeColorRGB(.65,.65,.65)
  for a in r['lines']:
   if len(a)<2:continue
   aa=np.array(a)
   if (aa.max(0)<lo).any()or(aa.min(0)>hi).any():continue
   p=c.beginPath();p.moveTo(*pt(a[0]));[p.lineTo(*pt(v))for v in a[1:]];c.drawPath(p)
 if proposal:
  p=c.beginPath();p.moveTo(*pt(proposal[0]));[p.lineTo(*pt(v))for v in proposal[1:]];p.close();c.setFillColor(Color(1,.42,.04,alpha=.24));c.setStrokeColorRGB(1,.36,.02);c.setLineWidth(1);c.drawPath(p,stroke=1,fill=1)
 c.restoreState();c.showPage()
c=canvas.Canvas(str(B/'TICKET-BANK-SOURCE-REMAP-CANDIDATE-R29-NO-TEXT.pdf'),pagesize=(1100,900));page(c,nurse,[[-17,-22],[8,8]],candidate['candidate_footprint_actual_qX_qY_MODEL']);page(c,east,[[19,-17],[46,8]]);c.save()
record={'schema':'chooguard.r29.ticket_bank.reconciled_source_investigation.v1','utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scope':'RESEARCH_AND_CAD_CANDIDATE_ONLY','deletion_complete':str(B.parent/'APPLIED-REMOVE-REJECTED-TRACK9-003.json'),'actual_CAD_source':str(CAD.relative_to(ROOT)),'snapshot_caveat':'CAD007 predates user deletion; exact310tombstonedGameObjects excluded. All remaining geometry remains reference, not user confirmation of either bank.','sources_read':{'original2F':'.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/research/boksoondoga-entry/sources/LIVING-PDF-2F-STRUCTURAL-ORIGINAL-XREF36.jpeg','full_original_vector_2022':'.state/ACTIVE-ALL2F-COMPLETION-R26/original-design-discovery/storyway-public-contracts/operator-archive-r27/transport-mirror-r28/specialist2022-2f-original-drawings-FILE00.pdf PAGE5;35958vectorpaths','latest_shopplan':'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/krispy-southwest/sources/R26-OFFICIAL100083825-MAP-page1.png','actual_guide20260419':'.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/research/root/current-guide-2f-original-20260419.jpg','actual_modern4bank_photo':'.state/ACTIVE-ALL2F-COMPLETION-R26/public/service-fronts/sources/baitfishingrod-224287119584/SOURCE-06.jpg','same_post_nurse_route_photos':['SOURCE-05.jpg','SOURCE-07.jpg'],'2023_fivebank':'.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/research/self-ticket/selected-source-frames/source_240p0s.jpg'},'source_fact_corrections':[{'classification':'directly_supported','finding':'The yellow9 near nursery is EXIT9: sign explicitly lists BusanHarborBridgeview, InternationalPassengerTerminal, NorthPortParking. It is NOT train boarding Track9.','proof':'baitfishingrod SOURCE07 full source text sign and same post route instructions.'},{'classification':'directly_supported','finding':'Guide places TVMs below nursery on same west-facing vertical office return, before turn onto transverse staffed-counter frontage. Therefore longitudinal axis follows staffreturn, not bar across its adjacent opening.','proof':'Apr2026 photographed guide literal 수유방/자동발매기 above 매표창구.'},{'classification':'directly_supported','finding':'Original vector2F page5 exposes same staff-return/corner/open circulation topology; its structural line may be followed, source guide colored boxes are not metric footprints.','proof':'PRIMARY-2022-FULL-2F-STAFF-CORNER-SOURCE-CROP.png retained.'},{'classification':'near_match_only','finding':'Current retained wall288 between0791 and017 piers supplies a6.48m longitudinal sideface. A5.6m source-scaled4machine bank would fit along it facingnegativeqX; individual visualpier identity remains unqualified.','proof':'CAD007 actual section lines and existing nurse correspondence, not a field survey.'}],'four_machine_candidate':candidate,'east5_bank':{'prior_placement_user_confirmed':False,'current_unmodified_scope':'Do not move/delete until root explicitlyextendsauthority.','current_actual_xspan':[27.875890887951087,34.834027290892024],'current_front_line_actual_qY':'approx0.1; equipmentcarrierbehind~-.95','old_argument_withdrawn_as_sufficient':'Acceptedcounterend + firstemptybay does not establish correct bankface/circulation semantics. Zero geometryintersections in installationenvelope does not establish zero route obstruction.','source_video_uncertainty':'218/222showcounterqueue,226viewturntoNamcheon,240fivebankfront; need individual matching jamb/portal and camera-turn orientation. Existing5bank/sourcecount is not locationgroundtruth.','conclusion':'UNRESOLVED portal identity; source/graygeometryalone insufficient to declare itclearorblocked. Do not claim matching because fitted into an empty C11/C12 bay.','actual_geometry_context':'ACTUAL-CAD007-EAST5-BAY-STRUCTURAL-CONTEXT.json'},'proposed_overlay':'TICKET-BANK-SOURCE-REMAP-CANDIDATE-R29-NO-TEXT.pdf','no_scene_Asset_changes_after_deletion':True,'native_calls_for_mapping':0,'no_new_placement':True,'user_validation_no_agent_QA':True}
(B/'TWO-TICKET-BANK-SOURCE-RECONCILIATION-R29.json').write_text(json.dumps(record,ensure_ascii=False,indent=2));print('sourcecontext rows',len(nurse),len(east),'candidate only',candidate['candidate_equipment_qY_MODEL'])
