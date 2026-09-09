import copy
import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest

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

    def test_named_detail_must_exist_in_generated_geometry_inventory(self):
        self.registry['assets'][0]['requiredComponents'].append('InventedBackrest')
        self.assertTrue(any('InventedBackrest' in e for e in self.check()['errors']))

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
