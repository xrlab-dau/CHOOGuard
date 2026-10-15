import json,hashlib
from pathlib import Path
R=Path('/Users/um-yunsang/CHOOGuard');O=R/'workers/twin/output/99percent-main2f-20261009';E=R/'workers/twin/evidence/99percent-main2f-20261009'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
D=json.loads((R/'workers/twin/evidence/floor2-architectural-pdf-search-20261009/download-receipts.json').read_text());receipt=next(x for x in D if x.get('id')=='100083811');P=json.loads((O/'main2f-source-architecture-trace.json').read_text());cross=json.loads((O/'official-201-202-qualitative-crosscheck.json').read_text())
manifest={'primary_original_pdf':receipt,'pdf_hash_verified':sha(R/receipt['path'])==receipt['sha256'],'clean_raster':{'path':P['source'],'resolution':[631,496],'sha256':sha(R/P['source']),'derivation':'Page1 original embedded image extracted by root; source pixels unchanged. Worker QA crops/overlays are derivative.'},'official_meeting_locator_sources':cross['sources'],'dates':{'source_announcement':'2026-10-07 according to official attachment/announcement','architectural_background_age':'Same embedded background as March2026; October announcement does not prove October field survey.','meeting_locator_field_capture':'Unknown; upload date/URL is not field capture date.'},'authenticity':'Public first-party KorailRetail PDF and Korail meeting-room locator downloaded by root through Aside browser; original bytes/hashes retained. This worker made no network call.','license':'No explicit reuse/redistribution license verified. Architectural reference only, not license-cleared distributable scene texture.','source_coverage':'Page1 MAIN2F and matchinghall share a drawing. Worker owns MAIN y>=275; parent researches other main-building storeys. No five-storey matchinghall inference.','production_mutated':False};(O/'source-authenticity-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
(O/'README.md').write_text('''# MAIN2F architectural draft, sealed finite pass

Official page1 MAIN2F inspection produced141 wall/boundary runs, eight service/WC envelopes, thirteen portal/approach controls, eight existing vertical-core footprints and54 reviewed open-hall column glyphs. Tiny fixture ink and several door distinctions remain unresolved. This covers source-labeled2F, not the other four main-building storeys or a99% site reconstruction.

The largest correction is the east service suite: nine current Naver-based room profiles differ from clean-plan dividers and adjacency. `main2f-kit-correction-spec-draft.json` contains40 wall candidates, eight floor finishes and one clear double-door candidate. `source-correction-staging.json` separates20missing/18partial candidates and two original-boundary reuse cases. Central U/level/railing lines and canopy Xs never become opaque walls. West slanted rooms remain trace-only. Official201/202 schematic supports the terminal east-suite relation; exact polygon names remain provisional.

`main-current-topology-source-overlay.png`: blue original section, orange generated service geometry, red source wall candidates. Independent original-main fit has six held-out columns, RMS2.7959pixels, relative localization only. Same MAIN affine is shared with matchinghall proposals. Hall outer corner[470,275] and MAIN inner return[464,276] are distinct. No floor/axis warp.

The east contributor draft resolves eleven exact MeshFilters and one MeshCollider by current saved YAML, all twelve asset hashes current:1876inside and39crossing triangles. MR inventory IDs are evidence; actual ownership uses class33MF/class64MC, never ID arithmetic. Root must export full channels, review clipped retention, old service-door correspondence and central-core contacts. No wholesale owner removal or production installation is authorized by this draft.

FloorY7, wallTop10.44, thickness.12 and doorHeight2.1 are inherited modeled kit values. Floor finishes/collision stay inside traced polygons; structural floor below gaps is unverified. No WC fixture totals, gender, hidden rooms or field hinge dimensions are invented. Root inspected the75-page2026 WC-design PDF and reports separate architectural drawings absent.

Offline source-byte, inverse-conversion, valid/nonoverlapping polygon, source-ink wall, open portal, builder-contract and exact bounded component/index checks PASS. Ink QA caught an overlong upper-WC top edge; it was shortened to visible[591,338]→[609,338], retaining the open approach. No Unity/PlayMode/site validation performed. All worker files are frozen at receipt seal.
''')
(O/'task_plan.md').write_text('''# MAIN2F architectural reconstruction draft

Planning-with-files finite pass complete. No nested delegation. Worker-only writes; parent owns production/browser.

1. Original source/independent registration/context — complete.
2. Source-visible boundaries/envelopes/portals/cores/columns — finite trace complete; uncertainties explicit.
3. Current comparison and bounded exact generated contributor draft — complete; root full-channel export/retention pending.
4. Offline validation, sealed receipt and handoff — complete.

Only MAIN2F y>=275; adjoining hall shares MAIN affine. Other storeys remain parent research scope. No metric/site/99% claim. Source plan may correct unmeasured native disagreement. No Assets/canonical/Unity/Aside writes.
''')
with (O/'findings.md').open('a') as f:f.write('\nFinal141runs/8envelopes/13approaches/8cores/54hall glyphs. East nine old room profiles differ from source. Twenty missing/eighteen partial candidates and two existing boundaries reused. ElevenMF+oneMC,1876inside/39crossing triangles; all selected asset hashes current. Corrected source-WC top edge after ink validation. 201/202 semantic polygon binding provisional. G2B75pPDF root inspection found no separate plans. Final49element draft uninstalled.\n')
with (O/'progress.md').open('a') as f:f.write('\nSource/native/generated comparison and bounded contributor packet complete. MF/MC identities derived from sameGO YAML, no MR/MF conflation. All twelve assets current. Offline validation PASS after upper-WC edge correction. Two local compressed-ternary syntax errors and receipt get(id) mismatch fixed without production effects. Sealed worker-only packet; handoff and stop.\n')
files=[]
for folder in [O,E]:
 for p in sorted(folder.rglob('*')):
  if p.is_file()and p.name!='artifact-receipts.json':files.append({'path':str(p.relative_to(R)),'bytes':p.stat().st_size,'sha256':sha(p)})
seal={'schema':'chooguard.main2f-source-architecture-receipt.v1','owned_output':str(O.relative_to(R)),'owned_evidence':str(E.relative_to(R)),'source_packet_sha256':sha(O/'main2f-source-architecture-trace.json'),'kit_spec_sha256':sha(O/'main2f-kit-correction-spec-draft.json'),'offline_verification_sha256':sha(O/'offline-verification.json'),'all_offline_checks_pass':json.loads((O/'offline-verification.json').read_text())['all_checks_pass'],'files':files,'production_mutated':False,'remaining_holds':['Root exact full-channel export/retention and crossing clipping','Old service operator correspondence and central-core collision review','Actual runtime/site validation unperformed']};p=O/'artifact-receipts.json';p.write_text(json.dumps(seal,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'receipt':str(p.relative_to(R)),'sha256':sha(p),'files':len(files),'offline_pass':seal['all_offline_checks_pass']}))
