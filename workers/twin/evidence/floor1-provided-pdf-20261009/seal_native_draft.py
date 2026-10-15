"""Seal worker-only source/install draft and cross-check packet consistency."""
import json,hashlib,subprocess
from pathlib import Path
from PIL import Image,ImageDraw
from shapely.geometry import Polygon,Point
ROOT=Path('/Users/um-yunsang/CHOOGuard');OUT=ROOT/'workers/twin/output/floor1-provided-pdf-20261009';EV=ROOT/'workers/twin/evidence/floor1-provided-pdf-20261009'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def dump(n,d):(OUT/n).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
packet=json.loads((OUT/'implementable-correction-packet.json').read_text());selection=json.loads((OUT/'bounded-contributor-selection.json').read_text());old=json.loads((OUT/'correction-packet.json').read_text());spec=json.loads((OUT/'root-kit-spec-draft.json').read_text())
rooms={x['id']:Polygon(x['source_polygon_pixels_approx'])for x in packet['main_WC_rooms']}
assert all(rooms[f['source_room']].covers(Point(f['source_center_pixel']))for f in packet['small_WC_symbols'])
panels={}
for p in packet['stall_partition_traces']:panels.setdefault(p['room'],[]).append(p)
cellchecks=[]
for room,pp in panels.items():
 for i in range(5):
  a,b=pp[i]['source_segment_pixels_approx'],pp[i+1]['source_segment_pixels_approx'];cell=Polygon([a[0],b[0],b[1],a[1]])
  f=next(x for x in packet['small_WC_symbols']if x['id']==room+'-wc-'+str(i+1));inside=cell.buffer(.5).covers(Point(f['source_center_pixel']));assert inside,(room,i,f['source_center_pixel'])
  cellchecks.append({'pan_symbol':f['id'],'source_pixel':f['source_center_pixel'],'inside_its_explicit_source_cell_with_half_pixel_tolerance':inside})
public=json.loads((ROOT/'content/world/observed-interiors/public-1f.json').read_text());oldwc=[x for x in public['elements']if x['type']=='ToiletRoom'];outside=0
for f in packet['small_WC_symbols']:
 f['current_old_wc_membership']=[e['id']for e in oldwc if Polygon(e['geometry']['polygon']).covers(Point(f['native_center_UV']))]
 outside+=not bool(f['current_old_wc_membership'])
packet['old_wc_coverage_comparison']={'registered_pan_symbols':20,'inside_any_old_canonical_WC_polygon':20-outside,'outside_all_old_canonical_WC_polygons':outside,'registration':'independent original wall topology affine; reference-only unmeasured','supersedes':'Old11outside count from Naver scaffold is historical, not final.'}
packet['source_cell_consistency_checks']=cellchecks;dump('implementable-correction-packet.json',packet)
for region,box in [('left',(40,180,140,280)),('right',(890,125,950,215))]:
 im=Image.open(EV/'embedded-architecture-white.png').convert('RGB').crop(box).resize(((box[2]-box[0])*10,(box[3]-box[1])*10),Image.Resampling.NEAREST);dr=ImageDraw.Draw(im)
 def pt(p):return ((p[0]-box[0])*10,(p[1]-box[1])*10)
 for p in packet['stall_partition_traces']:
  if p['room'].startswith(region):dr.line([pt(z)for z in p['source_segment_pixels_approx']],fill=(190,0,170),width=2)
 for f in packet['small_WC_symbols']:
  if f['source_room'].startswith(region):
   x,y=pt(f['source_center_pixel']);dr.ellipse((x-12,y-12,x+12,y+12),outline=(0,80,230),width=2);dr.text((x+10,y),f['id'].split('-')[-1],fill=(0,80,230))
 im.save(EV/('stall-partition-traces-'+region+'.png'))
patches=[]
for x in selection['owners']:
 o=x['owner'];f=x['actual_full_channel_export'];patches.append({'source_asset':o['asset'],'source_file_sha256':o['asset_sha256'],'source_asset_guid':o['asset_guid'],'source_complete_channels_sha256':x['source_complete_channels_sha256'],'vertices':f['vertex_count'],'vertex_indices':[],'expected_positions':[],'replacement_positions':[],'removed_triangle_ordinals':x['selected_triangle_ordinals'],'owners':[o]if x['owner_component']=='MeshFilter'else[],'collider_owners':[o]if x['owner_component']=='MeshCollider'else[],'evidence':str((OUT/'bounded-contributor-selection.json').relative_to(ROOT))})
ev=[OUT/'implementable-correction-packet.json',OUT/'native-topology-registration.json',OUT/'bounded-contributor-selection.json',ROOT/selection['full_channel_metadata']]
ret={'schema':'chooguard.floor-guide-layout-packet.v1','production_installed':False,'floor':1,'batch_id':'floor1-pdf-detail-20261009','work_dir':'.planning/2026-10-09-floor2-completion-pass2/floor1-pdf-detail','scene_sha256':selection['scene_sha256'],'guide':old['source_pdf'],'guide_sha256':old['source_pdf_sha256'],'proposal':str((OUT/'implementable-correction-packet.json').relative_to(ROOT)),'proposal_sha256':sha(OUT/'implementable-correction-packet.json'),'rejected_owners':[],'mesh_patches':patches,'movable_transforms':[],'units':[],'additional_evidence':[{'path':str(p.relative_to(ROOT)),'sha256':sha(p)}for p in ev],'source_claim':'Relative source topology localization; no measured dimensions/field validation. Exact generated contributor retention only; native floor/wall/core geometry unchanged.'}
dump('root-retention-installer-packet.json',ret)
adapter=json.loads((OUT/'root-install-adapter-packet.json').read_text());adapter['evidence']=[{'path':str(p.relative_to(ROOT)),'sha256':sha(p)}for p in [OUT/'root-retention-installer-packet.json',OUT/'root-kit-spec-draft.json',OUT/'bounded-contributor-selection.json',OUT/'implementable-correction-packet.json',OUT/'native-topology-registration.json']]
adapter['root_review_required']=[x for x in adapter['root_review_required']if not x.startswith('Adapter uses two sequential')];adapter['root_review_required'].append('Adapter wraps the existing retention transaction with rollback of prior source MF/MC references, scene, two registries and new folders. Failure-path runtime remains untested; root owns runtime acceptance.')
dump('root-install-adapter-packet.json',adapter)
# Compile the draft with existing Unity reference assemblies. No editor startup.
d='/Applications/Unity/Unity-6000.3.23f1/Unity.app/Contents/Resources/Scripting/NetCoreRuntime/dotnet';c='/Applications/Unity/Unity-6000.3.23f1/Unity.app/Contents/Resources/Scripting/DotNetSdkRoslyn/csc.dll';p=subprocess.run([d,c,'@'+str(EV/'draft-compile.rsp')],text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT);(EV/'draft-compile.log').write_text(p.stdout);assert p.returncode==0,p.stdout
dump('draft-compile-receipt.json',{'status':'PASS','compiler':c,'compiler_sha256':sha(c),'unity_editor_called':False,'production_modified':False,'exit_code':p.returncode,'inputs':[{'path':str(f.relative_to(ROOT)),'sha256':sha(f)}for f in [OUT/'MajibangBuilder.source-pdf-draft.cs',OUT/'Floor1PdfInteriorInstallDraft.cs',ROOT/'AgentScripts/FloorGuideLayoutInstall.cs']],'runtime_validation':False})
proof={'schema':'chooguard.worker-source-draft-verification.v1','status':'OFFLINE_PASS_ROOT_RUNTIME_PENDING','source_pan_symbols':20,'located_main_WC_rooms':4,'excluded_unlocated_detail_rooms':2,'source_service_double_openings':4,'source_service_leaves':8,'modeled_unverified_stall_leaves':20,'new_door_operators':24,'spec_elements':len(spec['elements']),'thin_partition_traces':24,'all20pan_centres_in_source_rooms':True,'all20pan_centres_in_own_approximate_cells':True,'full_channel_targets':len(selection['owners']),'all_selection_positions_indices_equal_actual_full_exports':all(x['selection_positions_indices_exactly_equal_actual_full_channel_export']for x in selection['owners']),'outer_room_mesh2258_selected':False,'native_original_geometry_targeted':False,'no_unreadable_extra_fixtures_counted':True,'native_GroundFloor_coverage_changed':False,'source_supported_new_room_floor_collision_drafted':True,'runtime_walk_motion_legacy_door_preservation_verified':False,'source_pixel_residuals_are_metric_accuracy':False}
dump('handoff-verification.json',proof)
files=[]
for base in [EV,OUT]:
 for p in sorted(base.iterdir()):
  if p.is_file()and p.name!='artifact-receipts.json':files.append({'path':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':sha(p)})
dump('artifact-receipts.json',{'schema':'chooguard.worker-artifact-receipts.v2','worker':'floor1-provided-pdf-20261009','files':files,'production_modified':False,'runtime_installer_executed':False})
print(json.dumps(proof,ensure_ascii=False))
