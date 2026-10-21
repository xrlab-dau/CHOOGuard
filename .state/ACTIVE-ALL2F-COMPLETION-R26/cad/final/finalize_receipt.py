#!/usr/bin/env python3
"""Finalize CAD production provenance only; does not inspect or mutate the Scene."""
from pathlib import Path
import collections,datetime,hashlib,json
ROOT=Path('/Users/um-yunsang/CHOOGuard');BASE=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad';OWN=BASE/'final';TAG='007'
def sha(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
 return h.hexdigest()
def main():
 out=BASE/('output-'+TAG);rp=out/'CURRENT-CAD-CONSTRUCTION-RECEIPT-R26.json';r=json.loads(rp.read_text());s=json.loads((BASE/('snapshot-'+TAG)/'CURRENT-2F-MESH-SNAPSHOT.json').read_text())
 name='r26-final-current2f-cad-source-'+TAG
 result_paths=list((ROOT/'.state/SCENE-WORKER-C180/results').glob(name+'--*.json'))
 release_paths=list((ROOT/'.state/SCENE-WORKER-C180/releases').glob(name+'--*.json'))
 register=json.loads((BASE.parent/'WORK-REGISTER-R26.json').read_text())
 deltas=[]
 for t in register['tasks']:
  if t['id']=='CAD-CURRENT':continue
  row={k:t[k] for k in ('id','status','actual_parts','actual_count','machines','actual_added_objects','actual_lockers_MODEL','actual_kiosks','remaining_source_pose_conflicts') if k in t}
  for key in ('actual_receipt','actual_completion','actual_source_packet','additional_native_shape_result'):
   p=ROOT/t[key] if t.get(key) else None
   if p and p.is_file():row[key]={'path':t[key],'sha256':sha(p)}
  deltas.append(row)
 r.update({'status':'FINAL_ACTUAL_CURRENT_2F_GEOMETRY_VECTOR_CAD_CREATED','author':'classic:/root/r26_final_cad','scope':'C180_R26_FINAL_CAD_SNAPSHOT_'+TAG,'final_snapshot_tag':TAG,'source_scene_dirty_before':s['scene_dirty_before'],'source_scene_dirty_after':s['scene_dirty_after'],'source_scene_sha256_before':s['scene_sha256_before'],'source_scene_sha256_after':s['scene_sha256_after'],'snapshot_kind':'ACTUAL_CURRENT_EDITING_GEOMETRY_WITH_DIRTY_STATE_PRESERVED','saved_scene_file_hash_is_not_editing_state_persistence_proof':True,'snapshot_source_root_index':str((OWN/'CURRENT-MODEL-SOURCE-ROOT-INDEX-R26-FINAL.json').relative_to(ROOT)),'source_mesh_binary':s['mesh_binary'],'source_mesh_binary_sha256':s['mesh_binary_sha256'],'native_result_receipts':[str(p.relative_to(ROOT)) for p in result_paths],'native_release_receipts':[str(p.relative_to(ROOT)) for p in release_paths],'source_scope_registry':' .state/ACTIVE-ALL2F-COMPLETION-R26/WORK-REGISTER-R26.json'.strip(),'actual_saved_delta_provenance':deltas,'source_geometry_semantics':'Actual active renderer/shared-mesh/matrix/material CURRENT EDITING geometry at final snapshot time. Scene dirty was true before and after the export. source_scene_sha256 identifies the existing SAVED Scene file only; equal before/after hashes do not prove the current dirty editing geometry has been persisted or source-byte identity. No extra Scene save/reopen/probe occurred. Current models are not new user confirmation, surveyed reality or a whole-2F completion claim.','rerunnable_native_exporter':str((OWN/'export_final_snapshot.py').relative_to(ROOT)),'actual_native_exporter':str((OWN/'ExportFinal2FGeometryR26.cs').relative_to(ROOT)),'rerunnable_vector_builder':str((BASE/'build_current_2f_cad_r26.py').relative_to(ROOT)),'compact_renderer':str((BASE/'compact_render_cad_r26.py').relative_to(ROOT)),'root_writers_settled_trigger':str((OWN/'ALL-WRITERS-SETTLED-TRIGGER.json').relative_to(ROOT)),'native_settled':all(json.loads(p.read_text()).get('settled') is True for p in result_paths) if result_paths else False,'lease_release_receipts_present':bool(release_paths),'Scene_Assets_changed':False,'global_current_pointers_changed':False,'QA_Play_tests_probe_reopen_capture':False,'whole2F_complete':False,'new_user_confirmation_claim':False,'finalized_utc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
 for obsolete in ('initial_snapshot_only','final_fresh_snapshot_pending_root_after_saved_deltas','rerunnable_geometry_exporter'):r.pop(obsolete,None)
 rp.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
 (OWN/'WORKER-COMPLETION-FINAL-CAD-R26.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
 print(json.dumps({'status':r['status'],'receipt':str(rp.relative_to(ROOT)),'source_objects':r['source_objects'],'unique_source_meshes':r['unique_source_meshes'],'source_scene_sha256':r['source_scene_sha256'],'pdf':r['pdf'],'dxf':r['dxf'],'data':r['data'],'native_settled':r['native_settled'],'lease_release_receipts_present':r['lease_release_receipts_present']},ensure_ascii=False))
if __name__=='__main__':main()
