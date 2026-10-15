"""Current whole-world evidence; historical component receipts remain separate."""
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]

def read(path):
    return json.loads(path.read_text()) if path.is_file() else {}

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def aggregate(output):
    project=ROOT/'output/simulator/platform-ktx-unity'
    manifest_path=project/'Assets/Simulator/Resources/SourceManifest.json'
    manifest=read(manifest_path);snapshot=manifest['world_snapshot'];checks=[]
    def add(name,passed,evidence,details=None):
        checks.append(dict(check=name,status='PASS' if passed else 'PENDING_OR_FAIL',evidence=str(evidence),details=details))
    for name in ['last-test.json','unity-source-compile.json']:
        path=ROOT/'output/simulator/checks'/name;data=read(path)
        drift=[p for p,h in data.get('sources',{}).items() if not (ROOT/p).is_file() or sha(ROOT/p)!=h]
        add(name,data.get('exit_code')==0 and bool(data.get('sources')) and not drift and not data.get('source_changed_during_run',False) and not data.get('source_changed_during_compile',False),path,drift)
    provider_path=ROOT/'output/simulator/native-auth-bridge.json';provider=read(provider_path)
    provider_drift=[p for p,h in provider.get('source_hashes',{}).items() if not (ROOT/p).is_file() or sha(ROOT/p)!=h]
    add('multi_provider_subscription_contract',provider.get('passed') is True and provider.get('providers_tested')==2 and provider.get('selection_routing_passed') is True and provider.get('initial_and_npc_same_selection') is True and provider.get('mock_completed_inferences')==4 and provider.get('stale_session_prevented_extra_call') is True and provider.get('source_changed_during_run') is False and bool(provider.get('source_hashes')) and not provider_drift,provider_path,
        dict(source_drift=provider_drift,scope='Actual localhost C# HTTP with two mock OAuth/Responses providers, not real account subscription inference or Unity motion.'))
    npc_provider_path=ROOT/'output/simulator/provider-live/npc-runtime.json';npc_provider=read(npc_provider_path)
    add('actual_subscription_npc_inference_and_motion',npc_provider.get('live_provider_inference') is True and npc_provider.get('developer_api_key_used') is False and npc_provider.get('same_initial_and_npc_selection') is True and npc_provider.get('guarded_plan_applied') is True and npc_provider.get('native_movement_completed') is True and npc_provider.get('runtime_fingerprint')==manifest.get('runtime_fingerprint') and bool(manifest.get('runtime_fingerprint')),npc_provider_path,
        'Pending real end-user subscription NPC inference and actual movement under the current whole-world source. Unity and additional live subscription inference remain postponed.')
    drift=[]
    for path,digest in {**manifest.get('runtime_sources',{}),**manifest.get('editor_sources',{})}.items():
        source=ROOT/'workers/simulator'/path;staged=project/'Assets/Simulator'/path
        if not source.is_file() or not staged.is_file() or sha(source)!=digest or sha(staged)!=digest:drift.append(path)
    for source in [*(ROOT/'workers/simulator/Runtime').rglob('*.cs'),*(ROOT/'workers/simulator/Editor').glob('*.cs')]:
        relative=str(source.relative_to(ROOT/'workers/simulator'))
        if relative not in {**manifest.get('runtime_sources',{}),**manifest.get('editor_sources',{})}:drift.append('not_staged:'+relative)
    for name,digest in manifest.get('resource_sources',{}).items():
        path=project/'Assets/Simulator/Resources'/name
        if not path.is_file() or sha(path)!=digest:drift.append('resource:'+name)
    for source_name,resource_name in [('mechanical-profiles.json','MechanicalProfiles.json'),('response-profiles.json','ResponseProfiles.json'),('station-capability-profiles.json','StationCapabilityProfiles.json')]:
        source=ROOT/'workers/simulator/data'/source_name;staged=project/'Assets/Simulator/Resources'/resource_name
        if source.is_file() and (not staged.is_file() or sha(source)!=sha(staged)):drift.append('inference_catalog:'+resource_name)
    assets=[path for path,row in manifest.get('assets',{}).items() if not (project/path).is_file() or sha(project/path)!=row['sha256']]
    add('current_full_world_staging',bool(manifest.get('runtime_fingerprint')) and not manifest.get('provisional_import_check') and not drift and not assets,manifest_path,dict(source_drift=drift,asset_drift=assets,asset_count=len(manifest.get('assets',{}))))
    inventory_path=project/snapshot['inventory_asset_path'];geometry_path=project/snapshot['geometry_scene_asset_path']
    add('immutable_full_world_packet',inventory_path.is_file() and sha(inventory_path)==snapshot['inventory_sha256'] and geometry_path.is_file() and sha(geometry_path)==snapshot['geometry_scene_sha256'],inventory_path)
    geometry=read(project/'Verification/geometry-nav-probes.json');full=geometry.get('full_world_snapshot',{})
    current=geometry.get('staging_fingerprint')==manifest.get('runtime_fingerprint') and bool(manifest.get('runtime_fingerprint'))
    add('actual_native_all_object_import',current and full.get('objects')==4115 and full.get('source_train_meshes')==703 and full.get('raw_prefab_instances')==0 and full.get('legacy_behaviours')==0 and full.get('snapshot_sha256')==snapshot['inventory_sha256'],project/'Verification/geometry-nav-probes.json',full)
    add('actual_native_whole_world_replay',current and geometry.get('recorded_observation_replay',{}).get('passed') is True,project/'Verification/geometry-nav-probes.json',geometry.get('recorded_observation_replay'))
    station=full.get('station_places',{})
    places=station.get('places',[])
    add('actual_source_qualified_station_walk_places',current and bool(places) and all(p.get('source_support_object') and isinstance(p.get('native_triangle_index'),int) for p in places),project/'Verification/geometry-nav-probes.json',station)
    path=project/'Verification/wholeworld-playmode-probes.json';play=read(path)
    current_play=play.get('staging_fingerprint')==manifest.get('runtime_fingerprint') and play.get('play_mode') is True
    for name,key in [('actual_seed_and_registry','seed_and_registry'),('actual_granular_fps_manipulation','detailed_manipulation'),('actual_response_communication','response_communication'),('actual_station_npc_motion','station_npc_motion'),('actual_detailed_input_replay','detailed_recorded_replay')]:
        add(name,current_play and play.get(key,{}).get('passed') is True,path,play.get(key))
    review_path=project/'Verification/orchestrator-wholeworld-visual-review.json';review=read(review_path)
    images=review.get('images',{})
    add('actual_wholeworld_graphics_review',review.get('passed') is True and review.get('staging_fingerprint')==manifest.get('runtime_fingerprint') and len(images)>=3 and all((project/'Verification'/name).is_file() and sha(project/'Verification'/name)==digest for name,digest in images.items()),review_path)
    history=read(ROOT/'output/simulator/provider-live/final/subscription-live.json')
    result=dict(schema='chooguard-wholeworld-acceptance-v1',implemented_scope='Current saved station/platform/KTX snapshot with inferred detailed training mechanics and response',checks=checks,
        all_required_evidence_present=all(c['status']=='PASS' for c in checks),
        all_functional_checks_passed=all(c['status']=='PASS' for c in checks if c['check']!='actual_wholeworld_graphics_review'),
        source_snapshot_sha256=snapshot['source_scene_sha256'],
        historical_component_evidence=dict(subscription_generation=history,scope='Previous platform/KTX request. No additional subscription call was made to refresh a changed whole-world implementation.'),
        limitations=['Site-specific force, timing, role and response rules are INFERRED_TRAINING, not approved KORAIL procedures.','All saved objects are input; causal/operable profiles and navigation coverage are explicitly bounded.','Native physics observations are recorded for deterministic state replay; independent physics resimulation and training effectiveness are unverified.'],
        original_construction_scene_may_advance=True)
    output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(dict(passed=sum(c['status']=='PASS' for c in checks),total=len(checks),complete=result['all_required_evidence_present'],report=str(output))))
    return 0 if result['all_required_evidence_present'] else 2
