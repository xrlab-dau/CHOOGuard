from pathlib import Path
import json,hashlib,re
out=Path('workers/twin/output/floor2-whole-layout-20261009/newphoto-scope');ev=Path('workers/twin/evidence/floor2-whole-layout-20261009/newphoto-scope')
def h(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
manifest=json.load(open(out/'namcheon-source-rebuild-manifest.json'));patch=json.load(open(out/'namcheon-patch-sources.json'));compiler=json.load(open(out/'compile/compile-receipt.json'))
checks=[]
def check(label,value):checks.append({'check':label,'pass':bool(value)})
check('clean offline compilation',compiler['status']=='PASS' and not Path(out/'compile/editor-compile.log').read_text().strip() and not Path(out/'compile/builders-compile.log').read_text().strip())
check('exact thirteen distinct source/output roles',len(manifest['entries'])==len({r['role']for r in manifest['entries']})==13)
check('eleven generated and two native roles',sum(not r['native']for r in manifest['entries'])==11 and sum(r['native']for r in manifest['entries'])==2)
check('seven old guide replacements preserved',sum(r['prior_registry']=='content/world/observed-interiors/floor-guide-layout.json'for r in manifest['entries'])==7)
check('both old native union/cut records preserved',sum(r['prior_registry']=='content/world/observed-interiors/platform-guide-openings.json'for r in manifest['entries'])==2)
check('exact two leaf baselines',len(manifest['door_leaves'])==2 and all(h(l['asset'])==l['file_sha256'] and h(l['asset']+'.meta')==l['meta_sha256']for l in manifest['door_leaves']))
for r in manifest['entries']:
 check('source/baseline hashes '+r['role'],h(r['source_asset'])==r['source_file_sha256']and h(r['source_asset']+'.meta')==r['source_meta_sha256']and h(r['baseline_asset'])==r['baseline_file_sha256']and h(r['baseline_asset']+'.meta')==r['baseline_meta_sha256'])
for d in manifest['installed_dependencies']:check('installed output file '+d['role'],h(d['asset'])==d['file_sha256'])
for r in patch['entries']:check('production source patch preimage unchanged '+r['source'],h(r['source'])==r['source_sha256'])
builder=Path(out/'patched-snapshots/MajibangBuilder.cs').read_text();reg=Path(out/'NamcheonSourceFrontRegistry.cs').read_text();guide=Path(out/'patched-snapshots/FloorGuideLayoutRegistry.cs').read_text()
check('baseline API precedes facade and Namcheon composition',builder.index('ObservedFacadeRegistry.ResolveForRebuild(\n                        baseline, path')<builder.index('NamcheonSourceFrontRegistry.ResolveForRebuild(\n                        correction'))
check('current outputs in mesh receipts', 'InstalledAssetsForRoot(rootPath)'in builder)
check('source and leaves excluded from stale deletion','!ChooGuard.Editor.Twin.NamcheonSourceFrontRegistry.HasCorrection(s)'in builder and '!ChooGuard.Editor.Twin.NamcheonSourceFrontRegistry.IsProtectedLeafAsset(s)'in builder)
check('public guide resolution returns composite','NamcheonSourceFrontRegistry.ResolveComposedForRebuild(ResolveBaselineForRebuild(generated, sourceAsset), sourceAsset)'in guide)
check('zero explicit StationDoor field assignments',not re.search(r'door\.(Centre|Normal|Along|Width|Height|Kind|Use|Label|Leaves|Sealed|ObservedOpen|Floor|CarId)\s*=(?!=)',reg))
check('strict source digest and layout checks','fresh complete baseline changed; ordinal rebase is forbidden' in reg and 'fresh baseline attribute layout/bounds changed'in reg)
check('native cooking/filter checks','collision cooking/filter settings changed'in reg)
receipt={'schema':'chooguard.offline-source-rebuild-draft-preflight.v1','status':'PASS'if all(c['pass']for c in checks)else'FAIL','checks':checks,'checks_passed':sum(c['pass']for c in checks),'checks_total':len(checks),'unity_editor_called':False,'fresh_generated_source_runtime_preflight_performed':False,'scene_or_asset_written':False}
(ev/'namcheon-rebuild-draft-preflight.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n')
assert receipt['status']=='PASS',receipt
files=[out/'NamcheonSourceFrontRegistry.cs',out/'namcheon-source-rebuild-manifest.json',out/'namcheon-source-rebuild.patch',out/'namcheon-patch-sources.json',out/'compile/compile-receipt.json',ev/'namcheon-rebuild-draft-preflight.json']
hand={'schema':'chooguard.namcheon-source-rebuild-root-handoff.v1','status':'COMPILED_OFFLINE_DRAFT_FOR_ROOT_INTEGRATION','installed_scene_at_seal_sha256':h('Assets/ChooGuard/Scenes/FpsStation.unity'),'packet_sha256':manifest['packet_sha256'],'files':[{'path':str(p),'sha256':h(p)}for p in files],'patch_source_preimages':patch['entries'],'acceptance_owner':'root','unity_editor_called':False,'source_scene_canonical_written':False,'complete_runtime_rebuild_performed':False,'offline_checks':str(ev/'namcheon-rebuild-draft-preflight.json'),'root_integration_steps':['Copy registry draft into Assets/ChooGuard/Editor/Twin/NamcheonSourceFrontRegistry.cs; guarded-apply patch only to exact preimages or review changed hunks.','Recompile actual Editor and MajibangBuilder/SourceOverrides in root Unity lane.','Run NamcheonSourceFrontRegistry.VerifyInstalled; verify all13 composites and door local channels/fields.','Run MajibangBuilder.VerifyShopCorrections and MajibangBuilder.KitVerifyCorrections with current2f-main spec/root, read accepted exact generated complete-channel comparisons before permitting source writes.','Authorize actual rebuild only after those runtime preflights pass. Post-rebuild VerifyInstalled and source/file preservation audit. Preserve original dependency assets and canonical prior correction rows.'],'limits':['Offline compile confirms symbol/API/type compatibility; it is not actual Unity mesh regeneration or a saved-scene rebuild.','Existing certified guide reorder proofs remain enforced by the existing registry. This adapter adds no new ordinal mapping or equivalence certificate.','A future material/source/owner change requires a reviewed reseal; the adapter rejects it.','Native legacy SourceOverrides actions are blocked for the protected owner; explicit native replay requires an exact complete prior guide-cut baseline.']}
(out/'namcheon-source-rebuild-handoff.json').write_text(json.dumps(hand,ensure_ascii=False,indent=2)+'\n')
print(receipt['status'],len(checks),'checks; handoff',h(out/'namcheon-source-rebuild-handoff.json'))
