"""Static draft contract and real stored-channel fixture checks; no Unity execution."""
from pathlib import Path
import copy,hashlib,json,re,struct
ROOT=Path('/Users/um-yunsang/CHOOGuard');OUT=ROOT/'workers/twin/evidence/floor1-rebuild-lineage-20261009/patch-draft'
def sha(p):return hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
receipt=json.loads((OUT/'migration-receipt.json').read_text());live=json.loads((ROOT/receipt['canonical_registry']).read_text());before=copy.deepcopy(live)
key=receipt['entry_key'];matches=[r for r in live['meshes'] if all(r.get(k)==v for k,v in key.items())];assert len(matches)==1
entry=matches[0];preserved=copy.deepcopy(entry);entry.update(copy.deepcopy(receipt['set_fields']))
assert all(entry[k]==v for k,v in preserved.items() if k!='source_asset')
assert len(live['meshes'])==len(before['meshes'])
assert all(x==y for x,y in zip(live['meshes'],before['meshes']) if y!=preserved)
root='실내 트윈 마감/점포 내부 1F';packets={}
def sources(reg):
 result=[]
 for row in reg['meshes']:
  pp=row['packet'];p=packets.setdefault(pp,json.loads((ROOT/pp).read_text()));assert sha(pp)==row['packet_sha256']
  matches=[x for x in p['mesh_patches'] if x['source_asset']==row.get('patch_baseline_asset',row['baseline_asset'])];assert len(matches)==1
  owners=matches[0].get('owners',[])+matches[0].get('collider_owners',[])
  if any(x['path'].startswith(root+'/') for x in owners):result.append(row['source_asset'])
 return set(result)
assert len(sources(before))==len(sources(live))==23
assert sources(live)==set(receipt['expected_source_assets_after'])
policy=entry['source_preprocess']
for path,field in [(policy['raw_source_asset'],'raw_source_file_sha256'),(policy['baseline_asset'],'baseline_file_sha256'),(policy['ordinal_source'],'ordinal_source_sha256'),(policy['canonical_spec'],'canonical_spec_sha256')]:assert sha(path)==policy[field]
# Load only the existing independent YAML decoder definitions, not its top-level proof writer.
namespace={};exec((OUT.parent/'prove_lineage.py').read_text().split('registry=js(',1)[0],namespace);mesh=namespace['mesh']
raw=mesh(policy['raw_source_asset']);baseline=mesh(policy['baseline_asset']);target=mesh(entry['replacement_asset'])
assert raw['complete_digest']==policy['raw_source_complete_digest'];assert baseline['complete_digest']==entry['source_digest'];assert target['complete_digest']==entry['replacement_digest']
ordinals=json.loads((ROOT/policy['ordinal_source']).read_text());kept=ordinals['preserve_remaining_triangle_ordinals'];removed=ordinals['duplicate_kit_triangle_ordinals'];assert len(removed)==252 and len(kept)==132
assert tuple(x for n in kept for x in raw['indices'][n*3:n*3+3])==baseline['indices']
def classify(m):
 if m['complete_digest']==entry['source_digest']:return 'accepted_exact_saved_baseline'
 if m['complete_digest']==entry['replacement_digest']:return 'accepted_exact_saved_target'
 if m['vertices']==768 and m['triangles']==384 and m['complete_digest']==policy['raw_source_complete_digest'] and m['indices']==raw['indices']:return 'accepted_exact_raw_then_partition'
 return 'rejected_unreviewed_generated_channels'
fixtures=[{'name':'stored_raw_source','decision':classify(raw)},{'name':'stored_fixture_baseline','decision':classify(baseline)},{'name':'live_guide_target','decision':classify(target)}]
assert all(x['decision'].startswith('accepted_') for x in fixtures)
# Keep topology unchanged and perturb a real UV0 byte in a bounded copy of the YAML source.
for name,p in [('raw_uv_drift',policy['raw_source_asset']),('processed_uv_drift',policy['baseline_asset']),('target_uv_drift',entry['replacement_asset'])]:
 text=(ROOT/p).read_text();m=re.search(r'_typelessdata: ([0-9a-f]+)',text);data=bytearray.fromhex(m.group(1));struct.pack_into('<f',data,40,struct.unpack_from('<f',data,40)[0]+.125)
 badpath=OUT/(name+'.yaml.fixture');badpath.write_text(text[:m.start(1)]+data.hex()+text[m.end(1):]);bad=mesh(str(badpath.relative_to(ROOT)))
 original=mesh(p);assert bad['vertices']==original['vertices'] and bad['indices']==original['indices'];assert bad['complete_digest']!=original['complete_digest'];assert classify(bad)=='rejected_unreviewed_generated_channels';fixtures.append({'name':name,'decision':classify(bad),'same_topology':True,'original_complete_digest':original['complete_digest'],'perturbed_complete_digest':bad['complete_digest']})
registry=(OUT/'modified/FloorGuideLayoutRegistry.cs').read_text();builder=(OUT/'modified/MajibangBuilder.cs').read_text();installer=(OUT/'modified/FloorGuideLayoutInstall.cs').read_text()
pre=registry[registry.index('        static Mesh PreprocessForRebuild'):registry.index('        static bool ProvenTriangleReorder')]
assert pre.index('Pinned shop partition authority changed')<pre.index('generatedDigest==(string)row["source_digest"]')<pre.index('Fresh raw shop collision differs')
assert pre.index('Pinned shop fixture index order changed')<pre.index('generatedDigest==(string)row["source_digest"]')
assert registry.index('generated = PreprocessForRebuild')<registry.index('ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated) == (string)row["replacement_digest"]')
assert builder.count('NeedsRetainedShopEnvelopeReplay(')==2 and builder.count('kitRetainedShopEnvelopeReplay = savedRetainedShopEnvelopeReplay')==2
assert builder.index('foreach (Batch b in batches.Values) b.Save();\n            if (kitRetainedShopEnvelopeReplay)')>0
assert 'var entry=guidePrior!=null?(JObject)guidePrior.DeepClone():new JObject();' in installer
# Layering retains the preprocessing object while changing only downstream guide fields.
layered=copy.deepcopy(entry);layered.update({'replacement_asset':'fixture-only-test-next-guide','replacement_digest':'test-next-digest','patch_baseline_asset':entry['replacement_asset']});assert layered['source_preprocess']==policy and layered['source_asset']==entry['source_asset'] and layered['baseline_asset']==entry['baseline_asset']
compile_errors=[line for line in (OUT/'compile.log').read_text().splitlines() if 'error CS' in line];assert not compile_errors;assert (OUT/'draft-compilation.dll').exists()
report={'schema':'chooguard.floor1-draft-static-guards.v1','canonical_registry_rows':len(before['meshes']),'two_field_overlay_preserves_every_other_live_field':True,'unique_expected_root_sources_before':23,'unique_expected_root_sources_after':23,'exact_generated_digest_state_fixtures':fixtures,'authority_checks_precede_reviewed_saved_state_acceptance':True,'preprocessing_precedes_target_shortcut':True,'shared_replay_setup_count':2,'replay_state_restored_in_both_entrypoints':True,'withheld_after_batch_save':True,'installer_layering_preserves_preprocessing':True,'runtime_guard_execution':False,'csharp_compile_errors':compile_errors,'standalone_csharp_dll_exists':(OUT/'draft-compilation.dll').exists(),'unity_calls':0,'canonical_writes':0}
(OUT/'static-guard-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:v for k,v in report.items() if k!='exact_generated_digest_state_fixtures'},indent=2));print('channel fixtures',len(fixtures),'passed')
