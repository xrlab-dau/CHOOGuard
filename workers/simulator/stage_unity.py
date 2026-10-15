#!/usr/bin/env python3
"""Stage an independent Unity project using read-only copies of supplied resource assets.

No shared Unity project file, source scene, GUID, material or credential is changed.
"""
import argparse
import hashlib
import json
import re
import shutil
from pathlib import Path

from world_inventory import build_snapshot as snapshot_source

ROOT = Path(__file__).resolve().parents[2]
DEFAULT = ROOT / "output/simulator/platform-ktx-unity"
WRITER_AUTHORITY = Path(".planning/2026-10-13-codex-claude-bd6-resume/WRITER-AUTHORITY.json")
ASSETS = [
    "Assets/ChooGuard/Art/Fps/KTX/ktx-single-10car-left-open.fbx",
    "Assets/ChooGuard/Art/OfficialBusanStation/OfficialBusanStation_PlatformsParking.fbx",
    "Assets/ChooGuard/Art/OfficialBusanStation/OfficialBusanStation_Layer0PlatformsDetails.fbx",
]

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def _local_path(base, relative):
    relative=Path(relative)
    target=(base/relative).resolve()
    if relative.is_absolute() or ".." in relative.parts or not target.is_relative_to(base.resolve()):
        raise ValueError("Invalid snapshot/source path: "+str(relative))
    return target


def verify_frozen_snapshot(snapshot):
    """Read-only packet integrity; this never grants CURRENT staging eligibility."""
    snapshot=Path(snapshot)
    receipt=json.loads((snapshot/"receipt.json").read_text())
    if (receipt.get("status")!="PASS" or receipt["sourceStability"]["stable"] is not True
            or receipt["sourceSceneAfterSha256"]!=receipt["sourceScene"]["sha256"]):
        raise ValueError("World snapshot has no successful stable-source receipt")
    inventory=_local_path(snapshot,receipt["inventory"]["path"])
    if sha(inventory)!=receipt["inventory"]["sha256"]:
        raise ValueError("Frozen world inventory hash mismatch")
    if json.loads(inventory.read_text())["sourceSnapshot"]["sceneSha256"]!=receipt["sourceScene"]["sha256"]:
        raise ValueError("Frozen inventory source identity mismatch")
    for row in receipt["packFiles"]:
        relative=Path(row["path"])
        if not relative.parts or relative.parts[0] not in ("Assets","Packages"):
            raise ValueError("Invalid world snapshot pack path")
        source=_local_path(snapshot/"pack",relative)
        if not source.is_file() or source.stat().st_size!=row["bytes"] or sha(source)!=row["sha256"]:
            raise ValueError("World snapshot packet changed: "+str(relative))
    return receipt,inventory


def verify_snapshot(snapshot, root=None):
    """Require comparable live authority and unchanged saved sources before copying."""
    root=Path(ROOT if root is None else root).resolve(); snapshot=Path(snapshot)
    guards={}

    def read(path, expected=None):
        data=snapshot_source.stable_read(path); digest=hashlib.sha256(data).hexdigest()
        if expected is not None and digest!=expected:
            raise ValueError("Current source/dependency differs from snapshot: "+str(path))
        if path in guards and guards[path]!=digest:
            raise ValueError("Source or authority changed during CURRENT verification: "+str(path))
        guards[path]=digest
        return data

    scene=root/snapshot_source.SCENE; scene_bytes=read(scene)
    scene_hash=guards[scene]
    receipt=json.loads(read(snapshot/"receipt.json"))
    if receipt["sourceScene"]["path"]!=snapshot_source.SCENE or receipt["sourceScene"]["sha256"]!=scene_hash:
        raise ValueError("Historical snapshot is not eligible for CURRENT saved source")
    meta=read(Path(str(scene)+'.meta'),receipt["geometryScene"]["metaSha256"])
    scene_guid_match=snapshot_source.GUID.search(meta)
    if not scene_guid_match or scene_guid_match[1].decode()!=receipt["sourceScene"]["guid"]:
        raise ValueError("Current source scene GUID is incomparable with snapshot")

    authority=json.loads(read(root/WRITER_AUTHORITY))
    if (authority.get("schema")!="chooguard.sole-writer-authority.v1"
            or authority.get("current_saved_scene_sha256")!=scene_hash):
        raise ValueError("Current saved-source authority is missing or incomparable")
    # An expired lease or Native_inflight=false cannot close started recovery work.
    if authority.get("Native_inflight") is not False or "staff135_recovery_phase" in authority:
        raise ValueError("Construction work/recovery unresolved; full recovery ACK and authority reconciliation required")
    tombstones=json.loads(read(_local_path(root,authority["tombstone_packet"])))
    deleted_ids=tombstones["tombstone_GO_ids"]; deleted_names=tombstones["tombstone_names"]
    if (not isinstance(deleted_ids,list) or not isinstance(deleted_names,list)
            or any(type(x) not in (int,str) or not re.fullmatch(r"-?\d+",str(x)) for x in deleted_ids)
            or any(not isinstance(x,str) for x in deleted_names)
            or len(set(map(str,deleted_ids)))!=len(deleted_ids) or len(set(deleted_names))!=len(deleted_names)
            or authority.get("tombstone_ID_count")!=len(deleted_ids)
            or authority.get("tombstone_name_count")!=len(deleted_names)
            or tombstones.get("no_restore_or_name_recreation") is not True):
        raise ValueError("Canonical tombstone authority is missing or incomparable")
    deleted_ids=set(map(str,deleted_ids)); deleted_names=set(deleted_names)

    receipt,inventory=verify_frozen_snapshot(snapshot)
    inv=json.loads(read(inventory,receipt["inventory"]["sha256"]))
    docs=snapshot_source.documents(scene_bytes)
    current={fid:d['fields']['m_Name'] for fid,d in docs.items() if d['classId']==1}
    objects=inv["objects"]
    if (deleted_ids.intersection(current) or deleted_names.intersection(current.values())
            or any(str(o['sceneFileId']) in deleted_ids or o['name'] in deleted_names for o in objects)):
        raise ValueError("CURRENT source/snapshot contains a canonical tombstoned object")
    if (authority.get("current_GO_count")!=len(current) or len(objects)!=len(current)
            or {str(o['sceneFileId']):o['name'] for o in objects}!=current
            or inv['sourceSnapshot']['sceneGuid']!=receipt['sourceScene']['guid']):
        raise ValueError("Current source object references are incomparable with snapshot/authority")
    if hashlib.sha256(snapshot_source.stripped_scene(docs)).hexdigest()!=receipt['geometryScene']['sha256']:
        raise ValueError("Frozen geometry does not preserve the current saved source")

    closure=json.loads(read(_local_path(snapshot,receipt['dependencyClosure']['path']),
                            receipt['dependencyClosure']['sha256']))
    if closure['unresolvedGuids'] or closure['excludedExecutableDependencies']:
        raise ValueError("Snapshot dependency closure is incomplete")
    derived={receipt['geometryScene']['path'],receipt['geometryScene']['path']+'.meta'}
    rows={row['path']:row for row in receipt['packFiles']}
    dependencies={row['path']:row for row in closure['files']}
    if (len(rows)!=len(receipt['packFiles']) or len(dependencies)!=len(closure['files'])
            or set(rows)!=set(dependencies)|derived
            or any(rows[path]!=row for path,row in dependencies.items())
            or rows[receipt['geometryScene']['path']]['sha256']!=receipt['geometryScene']['sha256']
            or rows[receipt['geometryScene']['path']+'.meta']['sha256']!=receipt['geometryScene']['metaSha256']):
        raise ValueError("Tracked dependency/source references are incomplete or inconsistent")
    for row in closure['files']:
        relative=Path(row['sourcePath'])
        if not relative.parts or (relative.parts[0] not in ('Assets','Packages')
                                  and relative.parts[:2]!=('Library','PackageCache')):
            raise ValueError("Incomparable tracked source dependency: "+str(relative))
        data=read(_local_path(root,relative),row['sha256'])
        if len(data)!=row['bytes']:
            raise ValueError("Current dependency size mismatch: "+str(relative))
    # Resolve the existing engine prerequisite by its recorded GUID; never install it.
    if closure['packagePrerequisites']!=receipt['packagePrerequisites']:
        raise ValueError("Engine prerequisite references are inconsistent")
    if closure['packagePrerequisites']:
        index,duplicates=snapshot_source.index_guids(root)
        for prerequisite in closure['packagePrerequisites']:
            guid=prerequisite['guid']; row=index.get(guid)
            if not row or guid in duplicates or row['path']!=prerequisite['path']:
                raise ValueError("Current engine prerequisite is missing or incomparable")
            read(row['sourcePath'],prerequisite['sha256']); read(row['metaPath'])
    for path,expected in list(guards.items()):
        read(path,expected)
    return receipt,inventory

def scene_materials():
    text = (ROOT / "Assets/ChooGuard/Scenes/FpsStation.unity").read_text()
    docs = {int(i): (typ, b) for typ, i, b in re.findall(r"--- !u!(\d+) &(\-?\d+)\n(.*?)(?=\n--- !u!|\Z)",text,re.S)}
    result = {}
    for fid, (typ, body) in docs.items():
        if typ != "1": continue
        match = re.search(r"  m_Name: (.*)",body)
        name = match.group(1) if match else ""
        if name.startswith('"'):
            try: name = json.loads(name)
            except ValueError: pass
        refs = [int(x) for x in re.findall(r"component: \{fileID: (\d+)\}",body)]
        for ref in refs:
            rt, rb = docs.get(ref,("",""))
            if rt != "23": continue
            a=rb.find("m_Materials:"); b=rb.find("m_StaticBatchInfo:",a)
            mats=[{"fileID":int(i),"guid":g} for i,g in re.findall(r"fileID: (\-?\d+), guid: ([a-f0-9]+)",rb[a:b])]
            if name.startswith("OfficialStation_"): result[name]=mats
    return result

def stage(destination, snapshot=None):
    destination=destination.resolve()
    if not destination.is_relative_to(ROOT/"output/simulator") or destination==ROOT/"output/simulator":
        raise ValueError("Only independent projects inside output/simulator are allowed")
    if snapshot is None:
        raise ValueError("CURRENT staging requires an explicit current world snapshot; no historical/component bypass")
    receipt=inventory=None
    if snapshot is not None:
        snapshot=snapshot.resolve()
        receipt,inventory=verify_snapshot(snapshot)
    destination.mkdir(parents=True,exist_ok=True)
    guid_paths={}
    for meta in (ROOT/"Assets").rglob("*.meta"):
        try: m=re.search(r"^guid: (\w+)",meta.read_text(),re.M)
        except (UnicodeDecodeError,OSError): continue
        if m: guid_paths[m.group(1)]=meta.with_suffix("")
    coverage_manifest=json.loads((ROOT/"workers/simulator/data/semantic-coverage.json").read_text())
    renderer_names={part["name"] for part in coverage_manifest["visual_parts"] if part["source_id"] in {"platforms","platform-details"}}
    materials={name:rows for name,rows in scene_materials().items() if name in renderer_names}
    copied={}
    def copy(path):
        path=Path(path)
        if not path.is_absolute():path=ROOT/path
        relative=path.relative_to(ROOT)
        if str(relative) in copied:return
        if not path.is_file():return
        target=destination/relative;target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(path,target)
        copied[str(relative)]={"sha256":sha(path),"bytes":path.stat().st_size}
        meta=Path(str(path)+".meta")
        if meta.exists():shutil.copy2(meta,Path(str(target)+".meta"))
        if path.suffix.lower() in [".mat",".asset",".shader",".hlsl"] or meta.exists():
            content=""
            if path.suffix.lower() in [".mat",".asset",".shader",".hlsl"]:
                try:content=path.read_text()
                except UnicodeDecodeError:pass
            if meta.exists():content+="\n"+meta.read_text()
            for guid in set(re.findall(r"guid: ([a-f0-9]{32})",content)):
                dependency=guid_paths.get(guid)
                if dependency is not None and dependency!=path:copy(dependency)
    for asset in ASSETS:copy(asset)
    # Platform imports have embedded source materials. Normalize isolated prefab references
    # to existing saved source-material assets by exact original renderer name.
    for name,mats in materials.items():
        for mat in mats:
            p=guid_paths.get(mat["guid"])
            if p is not None and "/Materials/" in str(p):copy(p)
    for p in (ROOT/"Assets/ChooGuard/Shaders").rglob("*"):
        if p.is_file() and p.suffix!=".meta":copy(p)
    frozen=None
    if snapshot is not None:
        for row in receipt["packFiles"]:
            relative=Path(row["path"])
            # Official package files are resolved by Unity's minimal package graph;
            # copying a partial Packages tree would create invalid embedded packages.
            if relative.parts[0]!="Assets":continue
            target=destination/relative;target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(snapshot/"pack"/relative,target)
            if target.suffix!=".meta":copied[str(relative)]={"sha256":row["sha256"],"bytes":row["bytes"]}
        frozen={"inventory_asset_path":"Assets/Simulator/Resources/WorldSnapshotInventory.json",
                "geometry_scene_asset_path":receipt["geometryScene"]["path"],
                "inventory_sha256":receipt["inventory"]["sha256"],
                "source_scene_sha256":receipt["sourceScene"]["sha256"],
                "geometry_scene_sha256":receipt["geometryScene"]["sha256"],
                "receipt_sha256":sha(snapshot/"receipt.json"),
                "package_files":{x["path"]:x["sha256"] for x in receipt["packFiles"] if x["path"].startswith("Packages/")},
                "runtime_roots":{"ktx":"unity:7518d664dafa749f2b6d5cba42bb798c:1685936680",
                    "platforms":["unity:7518d664dafa749f2b6d5cba42bb798c:866572249","unity:7518d664dafa749f2b6d5cba42bb798c:1101497418"],
                    "station_regions":["unity:7518d664dafa749f2b6d5cba42bb798c:293075613"],
                    "station_floors":[],"candidate_supports":["unity:7518d664dafa749f2b6d5cba42bb798c:1904553248"]},
                "qualification":"Saved source geometry; station places require actual upward collider hit, NavMesh and capsule clearance."}
    settings=destination/"ProjectSettings";settings.mkdir(exist_ok=True)
    # A dedicated stock URP asset is created by the isolated editor builder. Importing
    # shared Graphics/Quality assets could bring unrelated custom renderer scripts.
    for name in ["ProjectVersion.txt","ProjectSettings.asset","TagManager.asset","NavMeshAreas.asset"]:
        source=ROOT/"ProjectSettings"/name
        if not source.exists():continue
        shutil.copy2(source,settings/name)
        for guid in set(re.findall(r"guid: ([a-f0-9]{32})",source.read_text())):
            p=guid_paths.get(guid)
            if p is not None:copy(p)
    # Minimal package graph; no shared NuGet, connected services, editor plugins or remote agents.
    packages=destination/"Packages";packages.mkdir(exist_ok=True)
    deps={"com.unity.nuget.newtonsoft-json":"3.2.2","com.unity.inputsystem":"1.20.0","com.unity.render-pipelines.universal":"17.3.0","com.unity.ugui":"2.0.0","com.unity.modules.ai":"1.0.0","com.unity.modules.physics":"1.0.0","com.unity.modules.audio":"1.0.0","com.unity.modules.imgui":"1.0.0","com.unity.modules.jsonserialize":"1.0.0","com.unity.modules.imageconversion":"1.0.0"}
    (packages/"manifest.json").write_text(json.dumps({"dependencies":deps},indent=2)+"\n")
    runtime=destination/"Assets/Simulator/Runtime";runtime.mkdir(parents=True,exist_ok=True)
    for p in (ROOT/"workers/simulator/Runtime").rglob("*.cs"):
        dest=runtime/p.relative_to(ROOT/"workers/simulator/Runtime");dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
    (runtime/"ChooGuard.Seeded.Standalone.asmdef").write_text(json.dumps({"name":"ChooGuard.Seeded.Standalone","references":["Unity.InputSystem","Unity.Newtonsoft.Json","Unity.RenderPipelines.Universal.Runtime","Unity.RenderPipelines.Core.Runtime","UnityEngine.UI","Unity.TextMeshPro"],"autoReferenced":True},indent=2)+"\n")
    editor=destination/"Assets/Simulator/Editor";editor.mkdir(parents=True,exist_ok=True)
    for p in (ROOT/"workers/simulator/Editor").rglob("*.cs"):shutil.copy2(p,editor/p.name)
    (editor/"ChooGuard.Seeded.Standalone.Editor.asmdef").write_text(json.dumps({"name":"ChooGuard.Seeded.Standalone.Editor","references":["ChooGuard.Seeded.Standalone","Unity.Newtonsoft.Json","Unity.RenderPipelines.Universal.Runtime","Unity.RenderPipelines.Core.Runtime"],"includePlatforms":["Editor"]},indent=2)+"\n")
    resources=destination/"Assets/Simulator/Resources";resources.mkdir(parents=True,exist_ok=True)
    shutil.copy2(ROOT/"workers/simulator/data/platform-source.json",resources/"PlatformSource.json")
    shutil.copy2(ROOT/"workers/simulator/data/semantic-coverage.json",resources/"SemanticCoverage.json")
    shutil.copy2(ROOT/"workers/simulator/data/semantic-coverage-car04-candidate.json",resources/"Car04Candidate.json")
    shutil.copy2(ROOT/"workers/simulator/data/car04-access.json",resources/"Car04Access.json")
    if frozen is not None:
        shutil.copy2(inventory,resources/"WorldSnapshotInventory.json")
    inferred_resources=[]
    for source_name,resource_name in [("mechanical-profiles.json","MechanicalProfiles.json"),("response-profiles.json","ResponseProfiles.json"),("station-capability-profiles.json","StationCapabilityProfiles.json")]:
        source=ROOT/"workers/simulator/data"/source_name
        if source.is_file():
            shutil.copy2(source,resources/resource_name);inferred_resources.append(resource_name)
    runtime_sources={str(p.relative_to(ROOT/"workers/simulator")):sha(p) for p in (ROOT/"workers/simulator/Runtime").rglob("*.cs")}
    editor_sources={str(p.relative_to(ROOT/"workers/simulator")):sha(p) for p in (ROOT/"workers/simulator/Editor").rglob("*.cs")}
    resource_sources={name:sha(resources/name) for name in ["PlatformSource.json","SemanticCoverage.json","Car04Candidate.json","Car04Access.json"]}
    if frozen is not None:resource_sources["WorldSnapshotInventory.json"]=sha(resources/"WorldSnapshotInventory.json")
    for name in inferred_resources:resource_sources[name]=sha(resources/name)
    platform=json.loads((resources/"PlatformSource.json").read_text())
    coverage=json.loads((resources/"SemanticCoverage.json").read_text())
    scoped={"resource_sources":resource_sources,"assets":copied,"metas":{str(p.relative_to(destination)):sha(p) for p in (destination/"Assets").rglob("*.meta") if str(p.relative_to(destination))[:-5] in copied},"platform_materials":materials,"runtime_sources":runtime_sources,"editor_sources":editor_sources,"car04_candidate_sha256":sha(resources/"Car04Candidate.json"),"platform_geometry":{"train":platform["train"],"platforms":platform["platforms"]},"coverage_entities":coverage["physical_entities"],"coverage_navigation":coverage["navigation_points"],"frames":{"official_position":[-41.342197,0,-19.307789],"official_quaternion":[0,.9999927,0,-.003813778],"ktx_position":[88.91879,-.73000026,76.442825],"ktx_quaternion":[.12476154,.6960133,.6960134,-.12476152],"ktx_scale":[100,100,100]}}
    if frozen is not None:scoped["world_snapshot"]=frozen
    runtime_fingerprint=hashlib.sha256(json.dumps(scoped,ensure_ascii=False,sort_keys=True,separators=(",",":")).encode()).hexdigest()
    manifest={"schema":"chooguard-isolated-source-v1","runtime_fingerprint":runtime_fingerprint,"source_scene_sha256":sha(ROOT/"Assets/ChooGuard/Scenes/FpsStation.unity"),"assets":copied,"resource_sources":resource_sources,"runtime_sources":runtime_sources,"editor_sources":editor_sources,"official_position":[-41.342197,0,-19.307789],"official_quaternion":[0,.9999927,0,-.003813778],"ktx_position":[88.91879,-.73000026,76.442825],"ktx_quaternion":[.12476154,.6960133,.6960134,-.12476152],"ktx_scale":[100,100,100],"platform_materials":materials,"claim":"Supplied local visual resources; model-derived geometry and training parameters, not surveyed runtime fidelity or redistribution clearance."}
    if frozen is not None:
        manifest["world_snapshot"]=frozen
        manifest["source_scene_sha256"]=frozen["source_scene_sha256"]
    (resources/"SourceManifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n")
    (destination/"STAGING-RECEIPT.json").write_text(json.dumps({"source_root":str(ROOT),"project":str(destination),"source_files_copied":len(copied),"shared_files_modified":0,"asset_manifest_sha256":sha(resources/"SourceManifest.json"),"run":"Unity -batchmode -projectPath <this project> -executeMethod ChooGuard.Seeded.Unity.Editor.StandaloneSceneBuilder.Build -quit -logFile <log>"},indent=2)+"\n")
    print(json.dumps({"project":str(destination),"copied":len(copied),"manifest":str(resources/"SourceManifest.json")}))

if __name__=="__main__":
    parser=argparse.ArgumentParser();parser.add_argument("--project",type=Path,default=DEFAULT)
    parser.add_argument("--world-snapshot",type=Path,help="Immutable PASS world-snapshot packet directory")
    args=parser.parse_args();stage(args.project,args.world_snapshot)
