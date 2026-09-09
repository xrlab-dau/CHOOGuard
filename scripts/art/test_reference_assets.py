import copy
from contextlib import redirect_stdout
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

spec=importlib.util.spec_from_file_location('reference_assets',Path(__file__).with_name('validate_reference_assets.py'))
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)

class ReferenceAssetTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup);self.root=Path(self.tmp.name)
        path=self.root/'Assets/Bench.fbx';path.parent.mkdir();path.write_bytes(b'model-v1')
        (self.root/'generator.py').write_text('# authored source')
        sha=hashlib.sha256(path.read_bytes()).hexdigest()
        self.manifest={'assets':[{'id':'Bench','file':'Assets/Bench.fbx','sha256':sha,'sourceModule':'generator.py',
                                'components':{'TimberSlat':{'count':7},'SteelPedestal':{'count':2}}}]}
        self.registry={'version':1,'assets':[{'id':'Bench','kind':'station_specific','usage':'scene',
            'references':[{'pageUrl':'https://example.org/station','imageUrls':['https://example.org/bench.jpg'],
                           'observedFeatures':['Backless timber seat','Silver pedestal legs'],
                           'inspectedOn':'2026-09-07','imageSha256':['a'*64]}],
            'module':'generator.py','requiredComponents':['TimberSlat','SteelPedestal'],
            'dimensionsBasis':'synthetic_design_not_surveyed','stationInstallationVerified':False,
            'visualReview':{'status':'pending'}}]}
        self.inventory={'instances':[{'assetId':'Bench','path':'FoundationDemo/Bench','active':True}]}

    def check(self,strict=False):return art.validate(self.registry,self.manifest,self.root,self.inventory,strict)

    def test_references_do_not_imply_visual_or_facility_acceptance(self):
        report=self.check();self.assertEqual(report['errors'],[])
        self.assertEqual(report['referencedAssets'],1);self.assertEqual(report['visuallyReviewedAssets'],0)
        self.assertFalse(report['facilityFidelityValidated'])

    def test_missing_or_duplicate_asset_reference_fails(self):
        for rows in [[],self.registry['assets']*2]:
            self.registry['assets']=rows
            self.assertTrue(self.check()['errors'])

    def test_uninspected_or_image_free_reference_fails(self):
        reference=self.registry['assets'][0]['references'][0]
        for key,value in [('imageUrls',[]),('imageSha256',[]),('inspectedOn',''),('observedFeatures',[])]:
            previous=reference[key];reference[key]=value
            self.assertTrue(self.check()['errors'],key);reference[key]=previous

    def test_asset_reference_ids_are_nonempty_strings(self):
        for identifier in (None,{},[],42,'',' '):
            with self.subTest(identifier=identifier):
                self.registry['assets'][0]['id']=identifier
                self.assertTrue(self.check()['errors'])

    def test_malformed_asset_reference_rows_return_errors(self):
        for rows in (None,{},'asset',[None],[[]],['asset']):
            with self.subTest(rows=rows):
                self.registry['assets']=rows
                self.assertTrue(self.check()['errors'])

    def test_cli_asset_lookup_preserves_malformed_row_diagnostics(self):
        valid=copy.deepcopy(self.registry['assets'][0])
        for malformed in ({},None,[],{'id':[]},{'id':' '}):
            with self.subTest(row=malformed):
                self.registry['assets']=[malformed,valid]
                (self.root/'registry.json').write_text(json.dumps(self.registry))
                (self.root/'manifest.json').write_text(json.dumps(self.manifest))
                output=io.StringIO()
                argv=['validate_reference_assets.py','--registry','registry.json',
                      '--manifest','manifest.json','--asset','Bench']
                with patch.object(art,'ROOT',self.root),patch('sys.argv',argv),redirect_stdout(output):
                    status=art.main()
                report=json.loads(output.getvalue())
                self.assertEqual(status,1)
                self.assertTrue(report['errors'])
                self.assertEqual(report['assetCount'],1)
                self.assertEqual(report['assetContext']['id'],'Bench')

    def test_cli_explicit_null_inventory_cannot_skip_scene_validation(self):
        (self.root/'registry.json').write_text(json.dumps(self.registry))
        (self.root/'manifest.json').write_text(json.dumps(self.manifest))
        inventory_path=self.root/'inventory.json';inventory_path.write_text('null')
        output=io.StringIO()
        argv=['validate_reference_assets.py','--registry','registry.json','--manifest','manifest.json',
              '--inventory',str(inventory_path)]
        with patch.object(art,'ROOT',self.root),patch('sys.argv',argv),redirect_stdout(output):
            status=art.main()
        self.assertEqual(status,1)
        self.assertTrue(json.loads(output.getvalue())['errors'])

    def test_cli_cyclic_module_and_model_paths_return_asset_errors(self):
        script=self.root/'scripts/art/validate_reference_assets.py'
        script.parent.mkdir(parents=True);script.write_bytes(Path(art.__file__).read_bytes())
        (self.root/'registry.json').write_text(json.dumps(self.registry))
        (self.root/'manifest.json').write_text(json.dumps(self.manifest))
        command=[sys.executable,str(script),'--registry','registry.json','--manifest','manifest.json']
        valid=subprocess.run(command,capture_output=True,text=True,timeout=10)
        self.assertEqual(valid.returncode,0,valid.stderr)
        self.assertEqual(json.loads(valid.stdout)['errors'],[])
        for relative in ('generator.py','Assets/Bench.fbx'):
            with self.subTest(path=relative):
                path=self.root/relative;original=path.read_bytes();path.unlink()
                try:
                    path.symlink_to(path.name)
                    result=subprocess.run(command,capture_output=True,text=True,timeout=10)
                    self.assertEqual(result.returncode,1)
                    self.assertEqual(result.stderr,'')
                    report=json.loads(result.stdout)
                    self.assertTrue(any('Bench' in error and relative in error for error in report['errors']))
                finally:
                    path.unlink(missing_ok=True);path.write_bytes(original)

    def test_malformed_input_documents_return_errors(self):
        for name in ('registry','manifest','inventory'):
            original=getattr(self,name)
            for value in ([],42,'document',None):
                if name=='inventory' and value is None:continue
                with self.subTest(document=name,value=value):
                    setattr(self,name,value)
                    self.assertTrue(self.check()['errors'])
            setattr(self,name,original)

    def test_empty_asset_documents_cannot_pass_validation(self):
        for registry,manifest in (({'version':1},{}),
                                  ({'version':1,'assets':[]},{'assets':[]})):
            for strict in (False,True):
                with self.subTest(registry=registry,manifest=manifest,strict=strict):
                    report=art.validate(registry,manifest,self.root,{'instances':[]},strict)
                    self.assertTrue(report['errors'])
                    self.assertEqual(report['assetCount'],0)

    def test_cli_empty_export_is_an_error(self):
        (self.root/'registry.json').write_text(json.dumps({'version':1,'assets':[]}))
        (self.root/'manifest.json').write_text(json.dumps({'assets':[]}))
        inventory_path=self.root/'inventory.json'
        inventory_path.write_text(json.dumps({'instances':[]}))
        output=io.StringIO()
        argv=['validate_reference_assets.py','--registry','registry.json','--manifest','manifest.json',
              '--inventory',str(inventory_path),'--require-reviewed']
        with patch.object(art,'ROOT',self.root),patch('sys.argv',argv),redirect_stdout(output):
            status=art.main()
        self.assertEqual(status,1)
        self.assertTrue(json.loads(output.getvalue())['errors'])

    def test_model_rows_require_nonempty_unique_string_ids(self):
        original=copy.deepcopy(self.manifest['assets'][0])
        for rows in (None,{},'model',[None],[[]],['model'],[{}]):
            with self.subTest(rows=rows):
                self.manifest['assets']=rows
                self.assertTrue(self.check()['errors'])
        for identifier in (None,{},[],42,'',' '):
            with self.subTest(identifier=identifier):
                self.manifest['assets']=[dict(original,id=identifier)]
                self.assertTrue(self.check()['errors'])
        self.manifest['assets']=[dict(original,sha256='0'*64),original]
        self.assertTrue(any('Duplicate' in error for error in self.check()['errors']))

    def test_malformed_inventory_rows_return_errors(self):
        for rows in (None,{},'instance',[None],[[]],['instance'],[{}],
                     [{'assetId':[]}],[{'assetId':42}],[{'assetId':' '}]):
            with self.subTest(rows=rows):
                self.inventory['instances']=rows
                self.assertTrue(self.check()['errors'])

    def test_inventory_cannot_hide_unsupported_geometry_from_failed_export(self):
        self.inventory['unsupportedMeshes']=[]
        self.assertEqual(self.check()['errors'],[])
        path='FoundationDemo/Props/UnregisteredMesh'
        self.inventory['unsupportedMeshes']=[path]
        self.assertTrue(any(path in error for error in self.check()['errors']))
        for unsupported in (None,{},path,[None],[[]]):
            with self.subTest(unsupportedMeshes=unsupported):
                self.inventory['unsupportedMeshes']=unsupported
                self.assertTrue(self.check()['errors'])

    def test_source_dependencies_require_a_mapping(self):
        for dependencies in (None,[],'generator.py',42):
            with self.subTest(dependencies=dependencies):
                self.manifest['sourceModuleSha256']=dependencies
                self.assertTrue(self.check()['errors'])

    def test_malformed_reference_entries_return_errors(self):
        original=copy.deepcopy(self.registry['assets'][0]['references'])
        for references in (None,{},'source',[None],['source'],[42],[[]]):
            with self.subTest(references=references):
                self.registry['assets'][0]['references']=references
                self.assertTrue(self.check()['errors'])
        self.registry['assets'][0]['references']=original

    def test_reference_fields_require_typed_complete_image_evidence(self):
        original=copy.deepcopy(self.registry['assets'][0]['references'][0])
        cases=[('pageUrl',None),('pageUrl',42),('pageUrl',{}),
               ('imageUrls',None),('imageUrls','https://example.org/bench.jpg'),
               ('imageSha256',None),('imageSha256','a'*64),('imageSha256',[None]),
               ('inspectedOn',None),('inspectedOn',20260907),
               ('observedFeatures',None),('observedFeatures','not two observations'),
               ('observedFeatures',['',None]),
               ('imageUrls',['https://example.org/bench.jpg','https://example.org/side.jpg'])]
        for key,value in cases:
            with self.subTest(field=key,value=value):
                reference=copy.deepcopy(original);reference[key]=value
                self.registry['assets'][0]['references']=[reference]
                self.assertTrue(self.check()['errors'])

    def test_reference_urls_require_hostname_valid_port_and_no_literal_whitespace(self):
        original=copy.deepcopy(self.registry['assets'][0]['references'][0])
        urls=('https://:443/station','https://example.org:99999/station',
              'https://example.org:not-a-port/station','https://example.org:-1/station',
              'https://exa mple.org/station','https://exa\tmple.org/station',
              'https://exa\nmple.org/station',' https://example.org/station')
        for field in ('pageUrl','imageUrls'):
            for url in urls:
                with self.subTest(field=field,url=url):
                    reference=copy.deepcopy(original)
                    reference[field]=[url] if field=='imageUrls' else url
                    self.registry['assets'][0]['references']=[reference]
                    self.assertTrue(self.check()['errors'])

    def test_reference_urls_preserve_valid_unicode_ipv6_and_explicit_ports(self):
        original=copy.deepcopy(self.registry['assets'][0]['references'][0])
        urls=('https://example.org:443/station','https://예시.한국/역',
              'https://[2001:db8::1]:8443/station','https://127.0.0.1:8443/station')
        for field in ('pageUrl','imageUrls'):
            for url in urls:
                with self.subTest(field=field,url=url):
                    reference=copy.deepcopy(original)
                    reference[field]=[url] if field=='imageUrls' else url
                    self.registry['assets'][0]['references']=[reference]
                    self.assertEqual(self.check()['errors'],[])

    def test_inspection_date_requires_a_calendar_date_without_a_freshness_rule(self):
        reference=self.registry['assets'][0]['references'][0]
        for inspected in ('2026-99-99','2026-02-29','2026-04-31','0000-01-01'):
            with self.subTest(inspectedOn=inspected):
                reference['inspectedOn']=inspected
                self.assertTrue(self.check()['errors'])
        for inspected in ('2024-02-29','2026-09-07','2099-12-31'):
            with self.subTest(inspectedOn=inspected):
                reference['inspectedOn']=inspected
                self.assertEqual(self.check()['errors'],[])

    def test_named_detail_must_exist_in_generated_geometry_inventory(self):
        self.registry['assets'][0]['requiredComponents'].append('InventedBackrest')
        self.assertTrue(any('InventedBackrest' in e for e in self.check()['errors']))

    def test_authored_feature_bindings_require_named_component_records(self):
        row=self.registry['assets'][0]
        original=copy.deepcopy(row['requiredComponents'])
        for components in (None,'TimberSlat',{'TimberSlat':False},[None],[[]],[42],[' ']):
            with self.subTest(requiredComponents=components):
                row['requiredComponents']=components
                self.assertTrue(self.check()['errors'])
        row['requiredComponents']=original
        model=self.manifest['assets'][0]
        original=copy.deepcopy(model['components'])
        for components in (None,[]):
            with self.subTest(components=components):
                model['components']=components
                self.assertTrue(self.check()['errors'])
        for component in (None,1,{'count':'7'},{'count':True},{'count':1.5}):
            with self.subTest(component=component):
                model['components']=dict(original,TimberSlat=component)
                self.assertTrue(self.check()['errors'])

    def test_reference_classifications_reject_malformed_values(self):
        row=self.registry['assets'][0]
        for field in ('kind','usage'):
            original=row[field]
            for value in (None,{},[],42):
                with self.subTest(field=field,value=value):
                    row[field]=value
                    self.assertTrue(self.check()['errors'])
            row[field]=original

    def test_dimensions_basis_requires_a_nonblank_written_statement(self):
        row=self.registry['assets'][0]
        for basis in (None,{},[],{'not':'a stated basis'},['synthetic'],42,True,'',' '):
            with self.subTest(dimensionsBasis=basis):
                row['dimensionsBasis']=basis
                self.assertTrue(self.check()['errors'])

    def test_scene_asset_must_have_actual_usage_and_inventory_cannot_hide_unknown_family(self):
        self.inventory['instances']=[];self.assertTrue(self.check()['errors'])
        self.inventory['instances']=[{'assetId':'UnknownCube','path':'scene/object','active':True}]
        self.assertTrue(self.check()['errors'])

    def test_category_proxy_cannot_be_marked_confirmed_station_installation(self):
        self.registry['assets'][0]['kind']='category_proxy'
        self.registry['assets'][0]['stationInstallationVerified']=True
        self.assertTrue(self.check()['errors'])

    def test_virtual_guidance_is_never_counted_as_physical_station_object(self):
        row=self.registry['assets'][0];row['kind']='virtual_guidance';row['usage']='virtual'
        report=self.check();self.assertEqual(report['physicalSceneAssets'],0)
        self.assertEqual(report['virtualGuidanceAssets'],1)

    def test_review_requires_matching_asset_and_two_local_capture_digests(self):
        self.assertTrue(self.check(strict=True)['errors'])
        row=self.registry['assets'][0];row['visualReview']={'status':'reviewed','assetSha256':self.manifest['assets'][0]['sha256'],
            'captureSha256':['b'*64,'c'*64],'scope':'front and side geometry; no surveyed acceptance'}
        self.assertEqual(self.check(strict=True)['errors'],[])
        (self.root/'Assets/Bench.fbx').write_bytes(b'changed geometry')
        self.assertTrue(self.check(strict=True)['errors'])

    def test_visual_review_requires_typed_capture_evidence(self):
        row=self.registry['assets'][0]
        original={'status':'reviewed','assetSha256':self.manifest['assets'][0]['sha256'],
                  'captureSha256':['b'*64,'c'*64],'scope':'front and side'}
        for review in (None,[],42,'reviewed'):
            with self.subTest(review=review):
                row['visualReview']=review
                self.assertTrue(self.check(strict=True)['errors'])
        for field,values in (('captureSha256',(None,'b'*64,[[]],{'b'*64:None,'c'*64:None})),
                             ('scope',(None,{},['front','side'],42,' '))):
            for value in values:
                with self.subTest(field=field,value=value):
                    row['visualReview']=dict(original,**{field:value})
                    report=self.check(strict=True)
                    self.assertTrue(report['errors'])
                    self.assertEqual(report['visuallyReviewedAssets'],0)

    def test_path_escape_and_missing_authored_module_fail(self):
        for path in ['../outside.py','/private/source.py','missing.py']:
            self.registry['assets'][0]['module']=path
            self.assertTrue(self.check()['errors'])

    def test_changed_generator_dependency_requires_regeneration(self):
        self.manifest['sourceModuleSha256']={'generator.py':hashlib.sha256((self.root/'generator.py').read_bytes()).hexdigest()}
        self.assertEqual(self.check()['errors'],[])
        (self.root/'generator.py').write_text('# changed source without regenerating FBX')
        self.assertTrue(any('generator.py' in e for e in self.check()['errors']))

    def test_catalog_objects_cannot_be_counted_as_used_scene_assets(self):
        self.registry['assets'][0]['usage']='catalog';self.inventory['instances']=[]
        report=self.check();self.assertEqual(report['errors'],[])
        self.assertEqual(report['physicalSceneAssets'],0);self.assertEqual(report['catalogOnlyAssets'],1)

if __name__=='__main__':unittest.main()
