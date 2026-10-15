from pathlib import Path
import hashlib,json,re,subprocess
E=Path('workers/twin/evidence/floor2-platform-passages-20261009')
P=Path('workers/twin/output/floor2-platform-passages-20261009/Floor2NavigationVerify.cs')
sealed=Path('workers/twin/output/floor2-platform-passages-20261009/four-mouths-physical-walk-spines.json')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
s=P.read_text();packet=json.loads(sealed.read_text());checks=[]
def check(name,ok):checks.append({'name':name,'passed':bool(ok)})
check('sealed_spine_packet_unchanged',sha(sealed)=='333ef0e109ea910324f4e7bec85685e3c3a539a7a325d326187120315b2d77ff')
check('reads_four_mouths_and_fifteen_axes',len(packet['paths'])==4 and len(packet['native_vertical_paths'])==15 and 'mouths.Count != 4 || verticals.Count != 15' in s)
check('dense_sample_source_is_actual_sealed_296_floor_samples',sum(len(a['floor_support_samples'])for a in packet['paths'])==296 and 'mouth["floor_support_samples"]' in s)
check('saved_scene_editmode_clean_guard','scene.isDirty' in s and 'EditorApplication.isPlayingOrWillChangePlaymode' in s and 'EditorApplication.isCompiling' in s)
check('saved_navmesh_asset_loaded','AssetDatabase.LoadAssetAtPath<NavMeshData>(StationNavigationBuilder.NavMeshPath)' in s)
check('no_foreign_registered_graph_contamination','baseline.indices.Length != 0' in s)
check('bounded_nav_sampling_radius_and_y','SampleRadius = .55f, SameFloorTolerance = .30f' in s)
check('local_mouth_length_limit','* 1.4f + 2f' in s)
check('actual_nav_status_corners_lengths_reported','NavMeshPathStatus.PathComplete' in s and 'actual_corners_world' in s and 'actual_corner_length_m' in s)
check('four_actual_entry_door_ids',all(a in s for a in ['2F-main-front-14-다인명가','2F-main-front-15-호두나니&부산오뎅','2F-main-front-17-청도할매김밥','2F-main-front-18-반월당닭강정']))
check('entry_door_paths_bounded_three_m','PathQuery(a, b, 3f)' in s and 'PathQuery(b, a, 3f)' in s)
check('real_downward_physics_queries','Physics.RaycastAll(origin, Vector3.down, 10f' in s and 'nearest_upward_surface' in s and 'triangle_index' in s)
check('no_bake_asset_scene_or_actor_mutation',not re.search(r'\b(?:Bake|SaveAssets|SaveScene|SetDirty|SetMode|CreateAsset|AddComponent|CreateScene|OpenScene|LoadScene|DestroyImmediate)\s*\(',s) and 'new GameObject' not in s)
check('temporary_registered_data_cleanup','finally { if (instance.valid) instance.Remove(); }' in s)
check('sole_new_result_file_and_bounded_planning_path','FileMode.CreateNew' in s and 'output.StartsWith(allowed, StringComparison.Ordinal)' in s and not re.search(r'File\.(?:Write|Append|Delete|Move|Copy)',s))
check('explicit_no_physics_walk_or_field_pass_claim','["actual_character_controller_test_performed"] = false' in s and '["physical_field_accuracy_verified"] = false' in s)
check('source_state_hash_and_registration_preservation','scene_navmesh_packet_and_registered_graph_preserved' in s and 'Sha(PacketPath) == PacketSha' in s)
unity=Path('/Applications/Unity/Unity-6000.3.23f1/Unity.app/Contents/Resources/Scripting')
mono=unity/'MonoBleedingEdge/bin/mono';csc=unity/'MonoBleedingEdge/lib/mono/4.5/csc.exe'
managed=unity/'Managed'
refs=sorted((managed/'UnityEngine').glob('*.dll'))+[managed/'Newtonsoft.Json.dll']
refs+=sorted(Path('Library/ScriptAssemblies').glob('*.dll'))
refs+=sorted((unity/'MonoBleedingEdge/lib/mono/4.5/Facades').glob('*.dll'))
refs=list(dict.fromkeys(p.resolve()for p in refs))
dll=E/'Floor2NavigationVerify.dry-source-qa.dll';rsp=E/'navigation-dry-compile.rsp'
rsp.write_text('\n'.join(['-nologo','-target:library','-langversion:latest','-out:"'+str(dll.resolve())+'"']+['-r:"'+str(p)+'"'for p in refs]+['"'+str(P.resolve())+'"'])+'\n')
compile=subprocess.run([str(mono),str(csc),'@'+str(rsp.resolve())],capture_output=True,text=True)
(E/'navigation-dry-compile.log').write_text(compile.stdout+compile.stderr)
check('external_csharp_compile_against_current_unity_and_project_dlls',compile.returncode==0)
check('sealed_packet_still_unchanged_after_compile',sha(sealed)=='333ef0e109ea910324f4e7bec85685e3c3a539a7a325d326187120315b2d77ff')
receipt={'schema':'chooguard.floor2-navigation-verifier-dry-source-qa.v1','script':str(P),'script_sha256':sha(P),'checks':checks,'passed':all(a['passed']for a in checks),'external_compile_exit_code':compile.returncode,'compiler_log':str(E/'navigation-dry-compile.log'),'unity_editor_or_connector_executed':False,'actual_navmesh_runtime_queries_executed':False,'unity_or_asset_files_changed_by_lane':False,'root_execution_required':True}
(E/'navigation-verifier-dry-source-qa.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2))
print(json.dumps(receipt,ensure_ascii=False,indent=2));print(compile.stdout+compile.stderr)
if not receipt['passed']:raise SystemExit(1)
