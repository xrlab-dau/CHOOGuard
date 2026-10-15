#!/usr/bin/env python3
"""Boundary fixtures and full frozen source correspondence checks."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

import build_snapshot as snap

with mock.patch.object(sys, 'path', [str(Path(__file__).resolve().parents[1]), *sys.path]):
    import stage_unity as staging


def load_frozen_metadata(snapshot_dir):
    """Check frozen metadata integrity without asserting current-source eligibility."""
    path = Path(snapshot_dir)
    receipt = json.loads((path/'receipt.json').read_text())
    source_hash = receipt['sourceScene']['sha256']
    if (receipt.get('status') != 'PASS'
            or receipt.get('sourceStability', {}).get('stable') is not True
            or receipt.get('sourceSceneAfterSha256') != source_hash):
        raise ValueError(f'Snapshot has no successful stable-source receipt: {path}')
    inventory_bytes = (path/receipt['inventory']['path']).read_bytes()
    if snap.sha(inventory_bytes) != receipt['inventory']['sha256']:
        raise ValueError(f'Frozen inventory hash mismatch: {path}')
    inventory = json.loads(inventory_bytes)
    if inventory['sourceSnapshot']['sceneSha256'] != source_hash:
        raise ValueError(f'Frozen inventory source identity mismatch: {path}')
    return path, receipt, inventory


def select_current_snapshot(root, snapshot_dir=None):
    """Require the current saved epoch, including for an explicit packet target."""
    root = Path(root)
    source = root/snap.SCENE
    source_meta = Path(str(source)+'.meta')
    source_hash = snap.sha(snap.stable_read(source))
    meta_hash = snap.sha(snap.stable_read(source_meta))
    path = (Path(snapshot_dir) if snapshot_dir is not None else
            root/'output/simulator/world-snapshot'/source_hash[:16])
    if not (path/'receipt.json').is_file():
        raise FileNotFoundError(
            f'No CURRENT snapshot for saved source {source_hash}: {path}; '
            'older packets are not a fallback. Export is a separate authorized action.')
    path, receipt, inventory = load_frozen_metadata(path)
    if receipt['sourceScene']['sha256'] != source_hash:
        raise ValueError(
            f'Historical snapshot {receipt["sourceScene"]["sha256"]} is not eligible '
            f'for CURRENT saved source {source_hash}: {path}')
    if receipt['geometryScene']['metaSha256'] != meta_hash:
        raise ValueError(f'Current scene meta differs from the frozen snapshot: {path}')
    if (snap.sha(snap.stable_read(source)) != source_hash
            or snap.sha(snap.stable_read(source_meta)) != meta_hash):
        raise RuntimeError('Current saved source changed during snapshot selection')
    return path, receipt, inventory


def fixture():
    return snap.documents(b'''%YAML 1.1
--- !u!1 &1
GameObject:
  m_Name: parent
  m_IsActive: 0
  m_Component:
  - component: {fileID: 11}
  - component: {fileID: 12}
--- !u!4 &11
Transform:
  m_GameObject: {fileID: 1}
  m_LocalPosition: {x: 10, y: 0, z: 3}
  m_LocalRotation: {x: 0, y: 0, z: 0.7071067811865475, w: 0.7071067811865476}
  m_LocalScale: {x: 2, y: 3, z: 1}
  m_Father: {fileID: 0}
  m_Children:
  - {fileID: 21}
--- !u!114 &12
MonoBehaviour:
  m_GameObject: {fileID: 1}
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 3}
  PrivateToken: MUST_NOT_EXPORT
  ArbitraryField: MUST_NOT_EXPORT
--- !u!1 &2
GameObject:
  m_Name: child
  m_IsActive: 1
  m_Component:
  - component: {fileID: 21}
  - component: {fileID: 22}
--- !u!4 &21
Transform:
  m_GameObject: {fileID: 2}
  m_LocalPosition: {x: 1, y: 2, z: 0}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Father: {fileID: 11}
  m_Children: []
--- !u!65 &22
BoxCollider:
  m_GameObject: {fileID: 2}
  m_Enabled: 1
  m_IsTrigger: 0
  m_Center: {x: 0, y: 0, z: 0}
  m_Size: {x: 2, y: 2, z: 2}
''')


class FixtureTests(unittest.TestCase):
    def inventory(self):
        return snap.build_inventory(fixture(), 'a'*32, 'b'*64, {}, 'fixture')

    def test_inactive_parent_blocks_active_child_and_collider(self):
        inv=self.inventory(); child=inv['objects'][1]
        self.assertTrue(child['activeSelf']); self.assertFalse(child['activeInHierarchy']); self.assertFalse(child['physicalEligible'])
        self.assertEqual([],snap.validate_inventory(inv))

    def test_world_matrix_and_rotated_nonuniform_bounds(self):
        child=self.inventory()['objects'][1]
        for actual,expected in zip(child['worldTransform']['position'],[4,2,3]): self.assertAlmostEqual(actual,expected)
        bounds=child['components'][1]['worldBounds']
        for actual,expected in zip(bounds['min'],[1,0,2]): self.assertAlmostEqual(actual,expected)
        for actual,expected in zip(bounds['max'],[7,4,4]): self.assertAlmostEqual(actual,expected)

    def test_arbitrary_behaviour_values_and_execution_removed(self):
        data=json.dumps(self.inventory()); geometry=snap.stripped_scene(fixture())
        self.assertNotIn('MUST_NOT_EXPORT',data); self.assertNotIn('ArbitraryField',data)
        self.assertNotIn(b'MonoBehaviour',geometry); self.assertNotIn(b'fileID: 12',geometry)
        self.assertEqual({'1','2','11','21','22'},set(snap.documents(geometry)))

    def test_invalid_active_and_parent_and_matrix_are_detected(self):
        for mutation,marker in [(lambda o:o.update(activeInHierarchy=True),'active-hierarchy'),
                                (lambda o:o.update(parentId='absent'),'dangling-parent'),
                                (lambda o:o['worldTransform']['localToWorldMatrix4x4'].__setitem__(3,999),'world-matrix')]:
            inv=self.inventory(); mutation(inv['objects'][1]); self.assertTrue(any(marker in e for e in snap.validate_inventory(inv)))

    def test_duplicate_file_id_and_prefab_fail_closed(self):
        with self.assertRaises(ValueError): snap.documents(b'--- !u!4 &1\nTransform: {}\n--- !u!4 &1\nTransform: {}\n')
        with self.assertRaises(ValueError): snap.documents(b'--- !u!1001 &1\nPrefabInstance: {}\n')

    def test_dependency_follows_meta_includes_and_preserves_bytes(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td); assets=root/'Assets'; assets.mkdir(); pack=root/'pack'
            (assets/'a.shader').write_text('#include "inner.hlsl"\n')
            (assets/'a.shader.meta').write_text('guid: '+'a'*32+'\n')
            (assets/'inner.hlsl').write_text('// exact inert shader text\n')
            index,dupes=snap.index_guids(root)
            closure=snap.dependency_closure(root,('guid: '+'a'*32).encode(),index,pack)
            self.assertEqual({},closure['unresolvedGuids']); self.assertEqual(3,len(closure['files']))
            for row in closure['files']: self.assertEqual(row['sha256'],snap.sha((pack/row['path']).read_bytes()))


class SnapshotTargetTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root/snap.SCENE
        self.source.parent.mkdir(parents=True)
        self.source.write_bytes(b'current')
        self.meta = Path(str(self.source)+'.meta')
        self.meta.write_bytes(b'guid: '+b'a'*32+b'\n')

    def packet(self, source_bytes, path=None):
        source_hash = snap.sha(source_bytes)
        path = path or self.root/'output/simulator/world-snapshot'/source_hash[:16]
        path.mkdir(parents=True)
        inventory_bytes = json.dumps({
            'sourceSnapshot': {'sceneSha256': source_hash}, 'objects': []}).encode()
        (path/'world-objects.json').write_bytes(inventory_bytes)
        receipt = {
            'status': 'PASS', 'sourceScene': {'sha256': source_hash},
            'sourceSceneAfterSha256': source_hash, 'sourceStability': {'stable': True},
            'geometryScene': {'metaSha256': snap.sha(self.meta.read_bytes())},
            'inventory': {'path': 'world-objects.json', 'sha256': snap.sha(inventory_bytes)}}
        (path/'receipt.json').write_text(json.dumps(receipt))
        return path

    def test_default_targets_current_even_when_old_sorts_later(self):
        current = self.packet(b'current')
        old = self.packet(b'old')
        self.assertLess(current.name, old.name)
        self.assertEqual(current, select_current_snapshot(self.root)[0])

    def test_missing_current_does_not_fall_back_to_old(self):
        self.packet(b'old')
        with self.assertRaisesRegex(FileNotFoundError, 'No CURRENT snapshot'):
            select_current_snapshot(self.root)

    def test_explicit_current_target_outside_default_parent(self):
        self.packet(b'old')
        chosen = self.packet(b'current', self.root/'chosen-packet')
        self.assertEqual(chosen, select_current_snapshot(self.root, chosen)[0])

    def test_historical_metadata_integrity_does_not_grant_current_eligibility(self):
        old = self.packet(b'old')
        self.assertEqual(snap.sha(b'old'), load_frozen_metadata(old)[1]['sourceScene']['sha256'])
        with self.assertRaisesRegex(ValueError, 'Historical snapshot.*not eligible'):
            select_current_snapshot(self.root, old)

    def test_old_receipt_in_current_hash_directory_is_rejected(self):
        self.packet(b'old', self.root/'output/simulator/world-snapshot'/snap.sha(b'current')[:16])
        with self.assertRaisesRegex(ValueError, 'Historical snapshot.*not eligible'):
            select_current_snapshot(self.root)

    def test_mutated_inventory_is_rejected(self):
        path = self.packet(b'current')
        (path/'world-objects.json').write_bytes(b'{}')
        with self.assertRaisesRegex(ValueError, 'inventory hash mismatch'):
            select_current_snapshot(self.root)

    def test_rebound_inventory_with_different_source_is_rejected(self):
        path = self.packet(b'current')
        inventory = json.dumps({'sourceSnapshot': {'sceneSha256': snap.sha(b'old')}}).encode()
        (path/'world-objects.json').write_bytes(inventory)
        receipt = json.loads((path/'receipt.json').read_text())
        receipt['inventory']['sha256'] = snap.sha(inventory)
        (path/'receipt.json').write_text(json.dumps(receipt))
        with self.assertRaisesRegex(ValueError, 'inventory source identity mismatch'):
            select_current_snapshot(self.root)

    def test_failed_or_unstable_receipt_is_rejected(self):
        path = self.packet(b'current')
        receipt_path = path/'receipt.json'
        receipt = json.loads(receipt_path.read_text())
        for mutation in [dict(status='HOLD'), dict(sourceStability={'stable': False}),
                         dict(sourceSceneAfterSha256=snap.sha(b'old'))]:
            with self.subTest(mutation=mutation):
                receipt_path.write_text(json.dumps({**receipt, **mutation}))
                with self.assertRaisesRegex(ValueError, 'stable-source receipt'):
                    select_current_snapshot(self.root)

    def test_current_meta_change_is_rejected(self):
        self.packet(b'current')
        self.meta.write_bytes(b'guid: '+b'b'*32+b'\n')
        with self.assertRaisesRegex(ValueError, 'Current scene meta differs'):
            select_current_snapshot(self.root)

    def test_source_change_during_selection_is_rejected(self):
        self.packet(b'current')
        with mock.patch.object(snap, 'stable_read', side_effect=[
                b'current', self.meta.read_bytes(), b'changed during selection']):
            with self.assertRaisesRegex(RuntimeError, 'changed during snapshot selection'):
                select_current_snapshot(self.root)

    def test_missing_current_is_a_suite_error_not_a_skip(self):
        result = unittest.TestResult()
        suite = unittest.TestSuite([
            FrozenSnapshotTests('test_inventory_integrity_and_source_identity')])
        with mock.patch(__name__+'.select_current_snapshot',
                        side_effect=FileNotFoundError('No CURRENT snapshot')):
            suite.run(result)
        self.assertFalse(result.wasSuccessful())
        self.assertEqual(1, len(result.errors))
        self.assertEqual([], result.skipped)

    def test_cli_forwards_explicit_target_and_unittest_arguments(self):
        with mock.patch.object(FrozenSnapshotTests, 'snapshot_dir', None), \
                mock.patch.object(unittest, 'main') as run:
            main(['--snapshot-dir', str(self.root/'chosen'), 'FixtureTests', '-v'])
            self.assertEqual(self.root/'chosen', FrozenSnapshotTests.snapshot_dir)
            self.assertEqual(['FixtureTests', '-v'], run.call_args.kwargs['argv'][1:])


class StagingFreshnessTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.source = self.root/snap.SCENE
        self.source.parent.mkdir(parents=True)
        self.source_meta = Path(str(self.source)+'.meta')
        self.source_meta.write_bytes(b'guid: '+b'1'*32+b'\n')
        text = '%YAML 1.1\n'+''.join(
            f"--- !u!{d['classId']} &{fid}\n"+d['raw'] for fid,d in fixture().items())
        text = text.replace('  - component: {fileID: 22}\n',
                            '  - component: {fileID: 22}\n  - component: {fileID: 23}\n')
        text += ('--- !u!33 &23\nMeshFilter:\n  m_GameObject: {fileID: 2}\n'
                 '  m_Mesh: {fileID: 4300000, guid: '+ 'c'*32+', type: 2}\n')
        self.source.write_text(text)
        self.mesh = self.root/'Assets/fixture.asset'
        self.mesh.write_text('%YAML 1.1\n--- !u!43 &4300000\nMesh:\n  m_Name: fixture\n')
        self.mesh_meta = Path(str(self.mesh)+'.meta')
        self.mesh_meta.write_text('guid: '+'c'*32+'\n')
        scene_hash = snap.sha(self.source.read_bytes())
        docs = snap.documents(self.source.read_bytes())
        inventory = snap.build_inventory(docs, '1'*32, scene_hash, snap.index_guids(self.root)[0], 'fixture')
        self.packet = self.root/'packet'
        self.packet.mkdir()
        (self.packet/'world-objects.json').write_text(json.dumps(inventory))
        geometry_path = 'Assets/WorldSnapshot/FpsStation.geometry.unity'
        rows = []
        for path in [self.mesh, self.mesh_meta]:
            relative = path.relative_to(self.root).as_posix()
            data = path.read_bytes()
            target = self.packet/'pack'/relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
            rows.append({'path':relative, 'sourcePath':relative,
                         'sha256':snap.sha(data), 'bytes':len(data), 'via':'fixture'})
        self.closure = {'files':rows, 'unresolvedGuids':{},
                        'excludedExecutableDependencies':[], 'packagePrerequisites':[]}
        (self.packet/'dependency-closure.json').write_text(json.dumps(self.closure))
        pack_rows = list(rows)
        geometry = snap.stripped_scene(docs)
        for relative,data in [(geometry_path,geometry),
                              (geometry_path+'.meta',self.source_meta.read_bytes())]:
            target = self.packet/'pack'/relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
            pack_rows.append({'path':relative, 'sha256':snap.sha(data), 'bytes':len(data)})
        self.receipt = {
            'status':'PASS', 'sourceScene':{'path':snap.SCENE, 'sha256':scene_hash, 'guid':'1'*32},
            'sourceSceneAfterSha256':scene_hash, 'sourceStability':{'stable':True},
            'inventory':{'path':'world-objects.json', 'sha256':staging.sha(self.packet/'world-objects.json')},
            'geometryScene':{'path':geometry_path, 'sha256':snap.sha(geometry),
                             'metaSha256':staging.sha(self.source_meta)},
            'dependencyClosure':{'path':'dependency-closure.json',
                                 'sha256':staging.sha(self.packet/'dependency-closure.json')},
            'packFiles':pack_rows, 'packagePrerequisites':[]}
        self.save_receipt()
        self.authority_path = self.root/staging.WRITER_AUTHORITY
        self.authority_path.parent.mkdir(parents=True)
        self.tombstone_path = self.root/'.planning/current-tombstones.json'
        self.tombstones = {'tombstone_GO_ids':['99'], 'tombstone_names':['retired'],
                           'no_restore_or_name_recreation':True}
        self.authority = {
            'schema':'chooguard.sole-writer-authority.v1',
            'current_saved_scene_sha256':scene_hash, 'current_GO_count':len(inventory['objects']),
            'Native_inflight':False, 'tombstone_packet':self.tombstone_path.relative_to(self.root).as_posix(),
            'tombstone_ID_count':1, 'tombstone_name_count':1}
        self.save_authority()

    def save_receipt(self):
        (self.packet/'receipt.json').write_text(json.dumps(self.receipt))

    def save_closure(self):
        path = self.packet/'dependency-closure.json'
        path.write_text(json.dumps(self.closure))
        self.receipt['dependencyClosure']['sha256'] = staging.sha(path)
        self.save_receipt()

    def save_authority(self):
        self.authority_path.write_text(json.dumps(self.authority))
        self.tombstone_path.write_text(json.dumps(self.tombstones))

    def verify(self):
        return staging.verify_snapshot(self.packet, root=self.root)

    def test_matching_current_source_and_dynamic_authority_are_read_only(self):
        before = {p.relative_to(self.root):p.read_bytes() for p in self.root.rglob('*') if p.is_file()}
        with mock.patch.object(staging.shutil, 'copy2', side_effect=AssertionError('No copying in verification')):
            receipt, inventory = self.verify()
        self.assertEqual(self.receipt, receipt)
        self.assertEqual(self.packet/'world-objects.json', inventory)
        self.assertEqual(before, {p.relative_to(self.root):p.read_bytes()
                                  for p in self.root.rglob('*') if p.is_file()})

    def test_stale_scene_is_rejected_before_destination_creation_or_copy(self):
        self.source.write_bytes(self.source.read_bytes()+b'\n')
        destination = self.root/'output/simulator/current'
        with mock.patch.object(staging, 'ROOT', self.root), \
                mock.patch.object(staging.shutil, 'copy2') as copy_file:
            with self.assertRaisesRegex(ValueError, 'Historical snapshot'):
                staging.stage(destination, self.packet)
            copy_file.assert_not_called()
        self.assertFalse(destination.exists())

    def test_current_tombstone_ids_and_names_reject_existing_objects(self):
        for ids,names in [(['2'],['retired']), (['99'],['child'])]:
            with self.subTest(ids=ids,names=names):
                self.tombstones.update(tombstone_GO_ids=ids,tombstone_names=names)
                self.save_authority()
                with self.assertRaisesRegex(ValueError, 'canonical tombstoned object'):
                    self.verify()

    def test_missing_source_dependency_meta_or_authority_cannot_pass(self):
        for path in [self.source,self.source_meta,self.mesh,self.mesh_meta,
                     self.authority_path,self.tombstone_path]:
            with self.subTest(path=path.relative_to(self.root)):
                data = path.read_bytes()
                path.unlink()
                try:
                    with self.assertRaises(FileNotFoundError):
                        self.verify()
                finally:
                    path.write_bytes(data)

    def test_changed_source_meta_mesh_and_importer_meta_are_rejected(self):
        for path in [self.source_meta,self.mesh,self.mesh_meta]:
            with self.subTest(path=path.relative_to(self.root)):
                data = path.read_bytes()
                path.write_bytes(data+b'\n')
                try:
                    with self.assertRaisesRegex(ValueError, 'Current source/dependency differs'):
                        self.verify()
                finally:
                    path.write_bytes(data)

    def test_incomparable_source_and_tombstone_authority_are_rejected(self):
        original = dict(self.authority)
        for change in [{'schema':'unknown'}, {'current_saved_scene_sha256':snap.sha(b'old')},
                       {'current_GO_count':3}, {'tombstone_ID_count':0}]:
            with self.subTest(change=change):
                self.authority = {**original,**change}
                self.save_authority()
                with self.assertRaisesRegex(ValueError, 'authority|references'):
                    self.verify()

    def test_expiry_and_false_inflight_do_not_close_started_recovery(self):
        self.authority.update(staff135_recovery_phase='DIRTY_DIAGNOSTIC_SAVE_COPY_ONLY',
                              lease_expires_at='2000-01-01T00:00:00Z')
        self.save_authority()
        with self.assertRaisesRegex(ValueError, 'recovery unresolved'):
            self.verify()

    def test_unknown_or_active_native_work_cannot_pass(self):
        for inflight in [None,True]:
            with self.subTest(inflight=inflight):
                self.authority['Native_inflight'] = inflight
                self.save_authority()
                with self.assertRaisesRegex(ValueError, 'Construction work/recovery unresolved'):
                    self.verify()

    def test_tracked_dependency_cannot_be_omitted_from_closure(self):
        self.closure['files'] = self.closure['files'][:-1]
        self.save_closure()
        with self.assertRaisesRegex(ValueError, 'Tracked dependency/source references'):
            self.verify()

    def test_historical_integrity_does_not_require_live_sources_or_allow_staging(self):
        self.authority_path.unlink()
        self.assertEqual('PASS', staging.verify_frozen_snapshot(self.packet)[0]['status'])
        with self.assertRaises(FileNotFoundError):
            self.verify()

    def test_no_snapshot_is_not_a_staging_bypass(self):
        destination = self.root/'output/simulator/current'
        with mock.patch.object(staging, 'ROOT', self.root), \
                mock.patch.object(staging.shutil, 'copy2') as copy_file:
            with self.assertRaisesRegex(ValueError, 'requires an explicit current world snapshot'):
                staging.stage(destination)
            copy_file.assert_not_called()
        self.assertFalse(destination.exists())

    def test_source_or_authority_changes_during_verification_are_rejected(self):
        stable_read = staging.snapshot_source.stable_read
        for changed in [self.source,self.authority_path]:
            with self.subTest(changed=changed.relative_to(self.root)):
                original = changed.read_bytes()
                mutated = False

                def read(path, *args, **kwargs):
                    nonlocal mutated
                    data = stable_read(path,*args,**kwargs)
                    if path==self.mesh and not mutated:
                        changed.write_bytes(original+b'\n')
                        mutated = True
                    return data

                try:
                    with mock.patch.object(staging.snapshot_source, 'stable_read', side_effect=read):
                        with self.assertRaisesRegex(ValueError, 'Current source/dependency differs'):
                            self.verify()
                    self.assertTrue(mutated)
                finally:
                    changed.write_bytes(original)

    def test_existing_engine_prerequisite_is_verified_without_installing(self):
        prerequisite = self.root/'Packages/com.unity.fixture/Editor/Version.cs'
        prerequisite.parent.mkdir(parents=True)
        prerequisite.write_text('// existing engine prerequisite\n')
        Path(str(prerequisite)+'.meta').write_text('guid: '+'e'*32+'\n')
        requirements = [{'guid':'e'*32, 'path':prerequisite.relative_to(self.root).as_posix(),
                         'sha256':staging.sha(prerequisite)}]
        self.closure['packagePrerequisites'] = requirements
        self.receipt['packagePrerequisites'] = requirements
        self.save_closure()
        self.verify()
        prerequisite.write_text('// changed prerequisite\n')
        with self.assertRaisesRegex(ValueError, 'Current source/dependency differs'):
            self.verify()

    def test_current_suite_requires_authority_after_successful_source_selection(self):
        class CurrentFixture(FrozenSnapshotTests):
            source_root = self.root
            snapshot_dir = self.packet

        def run():
            result = unittest.TestResult()
            unittest.TestSuite([
                CurrentFixture('test_inventory_integrity_and_source_identity')]).run(result)
            return result

        self.assertTrue(run().wasSuccessful())
        self.authority_path.unlink()
        result = run()
        self.assertFalse(result.wasSuccessful())
        self.assertEqual(1, len(result.errors))
        self.assertEqual([], result.skipped)


class FrozenSnapshotTests(unittest.TestCase):
    source_root = Path(__file__).resolve().parents[3]
    snapshot_dir = None

    @classmethod
    def setUpClass(cls):
        cls.root=cls.source_root
        cls.path, cls.receipt, cls.inv = select_current_snapshot(cls.root, cls.snapshot_dir)
        staging.verify_snapshot(cls.path, root=cls.root)

    def test_inventory_integrity_and_source_identity(self):
        self.assertEqual([],snap.validate_inventory(self.inv)); self.assertEqual('PASS',self.receipt['status'])
        self.assertEqual(self.receipt['sourceScene']['sha256'],snap.sha((self.root/snap.SCENE).read_bytes()))
        self.assertEqual(self.receipt['inventory']['sha256'],snap.sha((self.path/'world-objects.json').read_bytes()))

    def test_exact_current_gameobjects_components_and_safe_scene_refs(self):
        raw=snap.documents((self.root/snap.SCENE).read_bytes()); safe=snap.documents((self.path/'pack'/self.receipt['geometryScene']['path']).read_bytes())
        gos={k for k,d in raw.items() if d['classId']==1}
        self.assertEqual(gos,{o['sceneFileId'] for o in self.inv['objects']}); self.assertEqual(gos,{k for k,d in safe.items() if d['classId']==1})
        self.assertFalse(any(d['classId']==114 for d in safe.values()))
        expected_components=sum(len(d['fields'].get('m_Component',[])) for d in raw.values() if d['classId']==1)
        self.assertEqual(expected_components,sum(len(o['components']) for o in self.inv['objects']))
        for d in safe.values():
            if d['classId'] not in {1,1660057539}:
                self.assertEqual(raw[d['fileId']]['raw'],d['raw'],'geometry component bytes altered')
            for m in snap.REF.finditer(d['raw']):
                if m[2] is None and m[1]!='0': self.assertIn(m[1],safe, f'dangling local ref {m[1]}')

    def test_every_packed_byte_and_meta_matches_original_hash(self):
        for row in self.receipt['packFiles']:
            self.assertEqual(row['sha256'],snap.sha((self.path/'pack'/row['path']).read_bytes()),row['path'])
            if 'sourcePath' in row: self.assertEqual(row['sha256'],snap.sha((self.root/row['sourcePath']).read_bytes()),row['sourcePath'])

    def test_explicit_alias_bindings_preserve_capability_boundary(self):
        ids={o['id'] for o in self.inv['objects']}; bindings=self.inv['sourceBindings']
        self.assertGreater(len(bindings),8)
        for b in bindings:
            self.assertIn(b['objectId'],ids); self.assertEqual([],b['capabilities']); self.assertIn(b['relation'],['alias','part-of'])


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--snapshot-dir', type=Path,
                        help='Exact packet directory; CURRENT tests still require the current saved source.')
    options, unittest_args = parser.parse_known_args(argv)
    FrozenSnapshotTests.snapshot_dir = options.snapshot_dir
    return unittest.main(argv=[sys.argv[0], *unittest_args], verbosity=2)


if __name__=='__main__': main()
