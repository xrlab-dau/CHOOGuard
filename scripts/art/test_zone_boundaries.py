import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

spec=importlib.util.spec_from_file_location('zone_boundaries',Path(__file__).with_name('validate_zone_boundaries.py'))
zones=importlib.util.module_from_spec(spec);spec.loader.exec_module(zones)
ROOT=Path(__file__).resolve().parents[2]

class ZoneBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.coverage=json.loads((ROOT/zones.COVERAGE).read_text(encoding='utf-8'))
        self.boundaries=json.loads((ROOT/zones.BOUNDARIES).read_text(encoding='utf-8'))
        self.sha=zones.digest(ROOT/zones.COVERAGE)

    def check(self,coverage_sha=None):return zones.validate(self.boundaries,self.coverage,coverage_sha or self.sha)
    def zone(self,identifier):return next(z for z in self.boundaries['zones'] if z['id']==identifier)
    def portal(self,identifier):return next(p for p in self.boundaries['portals'] if p['id']==identifier)
    def assertRejected(self,fragment,coverage_sha=None):
        errors=self.check(coverage_sha)['errors']
        self.assertTrue(any(fragment in e for e in errors),errors)

    def test_tracked_boundaries_are_valid_and_never_claim_fidelity(self):
        report=self.check();self.assertEqual(report['errors'],[])
        self.assertEqual(report['result'],'pass');self.assertEqual(report['unratable'],[])
        self.assertEqual((report['zones'],report['portals'],report['crossBundlePortals']),(13,12,3))
        self.assertEqual(report['metricVerifiedZones'],0);self.assertFalse(report['facilityFidelityValidated'])

    def test_counts_are_counted_not_asserted(self):
        self.assertEqual(len(self.coverage['zones']),13)
        self.assertEqual(len(self.coverage['connections']),12)
        self.assertEqual(len(self.boundaries['bundles']),3)
        self.assertEqual(len(self.boundaries['zones']),13)
        self.assertEqual(len(self.boundaries['portals']),12)
        self.assertEqual(sum(1 for p in self.boundaries['portals'] if p['crosses_bundles']),3)
        self.assertEqual(len(self.boundaries['open_questions']),7)

    def test_recorded_coverage_hash_matches_the_real_file(self):
        self.assertEqual(self.boundaries['coverage_source']['sha256'],zones.digest(ROOT/zones.COVERAGE))
        report=self.check('0'*64)
        self.assertEqual(report['result'],'fail')
        self.assertTrue(any('reread' in e for e in report['errors']),report['errors'])

    def test_coverage_change_requires_reread_before_reuse(self):
        self.assertRejected('reread',coverage_sha='0'*64)

    def test_every_coverage_zone_is_recorded_exactly_once(self):
        rows=self.boundaries['zones']
        self.boundaries['zones']=rows[1:];self.assertRejected('Zone coverage differs')
        self.boundaries['zones']=rows+rows[:1];self.assertRejected('Duplicate zone')

    def test_zone_belongs_to_exactly_one_bundle(self):
        first,second=self.boundaries['bundles'][:2];second['zones'].append(first['zones'][0])
        self.assertRejected('exactly one bundle')

    def test_unknown_source_or_use_is_rejected(self):
        entry=self.zone('station_ticket_area')['inputs'][0]
        entry['ref']='P99';self.assertRejected('Unknown source reference')
        entry['ref']='R06';entry['allowed_use']=['metric_survey'];self.assertRejected('Unknown allowed use')

    def test_adjacency_must_match_coverage_connections(self):
        self.zone('metro_platforms')['adjacent_zones'].append('station_concourse_2f')
        self.assertRejected('Adjacency differs')

    def test_portals_mirror_coverage_connections_one_to_one(self):
        rows=self.boundaries['portals']
        self.boundaries['portals']=rows[1:];self.assertRejected('Portal coverage differs')
        invented=dict(rows[0],id='portal.metro_platforms--rail_platforms_mainline',**{'from':'metro_platforms','to':'rail_platforms_mainline'})
        self.boundaries['portals']=rows+[invented];self.assertRejected('Portal coverage differs')
        self.boundaries['portals']=rows;rows[0]['id']='portal.renamed';self.assertRejected('Portal ID')

    def test_cross_bundle_portal_geometry_goes_to_pm_integration(self):
        self.portal('portal.rail_platforms_mainline--station_concourse_2f')['geometry_owner']='rail_train'
        self.assertRejected('geometry owner')

    def test_operator_dimension_only_where_coverage_publishes_it(self):
        concourse=self.zone('station_concourse_2f')
        concourse['measurement_basis']='operator_published';concourse['numeric_constraint_refs']=['connector_operator_dimensions']
        self.assertRejected('Numeric constraint')

    def test_unverified_zone_or_portal_cannot_drop_provisional(self):
        self.zone('underground_connector')['provisional']=False;self.assertRejected('provisional')
        self.zone('underground_connector')['provisional']=True
        self.portal('portal.metro_concourse--metro_platforms')['provisional']=False;self.assertRejected('provisional')

    def test_portal_dimension_needs_published_opening_basis(self):
        self.portal('portal.underground_connector--underground_shopping_passage')['opening_width_m']=8.0
        self.assertRejected('Portal dimension')

    def test_unverified_zone_lists_gaps_and_production_issue(self):
        zone=self.zone('rail_tracks_mainline')
        zone['gaps']=[];self.assertRejected('gap')
        zone['gaps']=['x'];zone['production_issues']=[];self.assertRejected('production')

    def test_supplementary_sources_are_unique_and_used(self):
        sources=self.boundaries['sources']
        sources.append(dict(sources[0]));self.assertRejected('Duplicate source')
        sources[-1]=dict(sources[0],id='X99');self.assertRejected('Unused source')

    def test_unratable_payloads_are_not_rule_violations(self):
        for payload in ([1,2,3],None,'zone-boundaries',42,{'zones':'not-a-list'},{'portals':None}):
            report=zones.validate(payload,self.coverage,self.sha)
            self.assertEqual(report['result'],'not_run',payload)
            self.assertEqual(report['errors'],[],payload)
            self.assertTrue(report['unratable'],payload)
        for payload in ([1,2,3],None,{'zones':{}}):
            report=zones.validate(self.boundaries,payload,self.sha)
            self.assertEqual(report['result'],'not_run',payload)
            self.assertEqual(report['errors'],[],payload)

    def test_unratable_file_inputs_are_reported_as_not_run(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory=Path(temporary)
            missing=directory/'missing.json'
            truncated=directory/'truncated.json';truncated.write_text('{"schema_version":"1.0"',encoding='utf-8')
            binary=directory/'binary.json';binary.write_bytes(b'{"schema_version":"\xff\xfe"}')
            for path,match in ((missing,'not found'),(truncated,'not valid JSON'),(binary,'not valid UTF-8')):
                payload,reason=zones.load_document(path,'zone-boundaries')
                self.assertIsNone(payload,path);self.assertIn(match,reason,path)

    def test_exit_codes_match_the_documented_contract(self):
        script=Path(__file__).with_name('validate_zone_boundaries.py')
        clean=subprocess.run([sys.executable,str(script)],cwd=ROOT,capture_output=True,text=True)
        self.assertEqual(clean.returncode,zones.EXIT_PASS,clean.stdout+clean.stderr)
        with tempfile.TemporaryDirectory() as temporary:
            array=Path(temporary)/'array.json';array.write_text('[1,2,3]',encoding='utf-8')
            unratable=subprocess.run([sys.executable,str(script),'--data',str(array)],cwd=ROOT,capture_output=True,text=True)
            self.assertEqual(unratable.returncode,zones.EXIT_NOT_RUN,unratable.stdout+unratable.stderr)
            self.assertEqual(json.loads(unratable.stdout)['result'],'not_run')
            short=Path(temporary)/'short.json'
            short.write_text(json.dumps({**self.boundaries,'zones':self.boundaries['zones'][1:]}),encoding='utf-8')
            violating=subprocess.run([sys.executable,str(script),'--data',str(short)],cwd=ROOT,capture_output=True,text=True)
            self.assertEqual(violating.returncode,zones.EXIT_FAIL,violating.stdout+violating.stderr)
            errors=json.loads(violating.stdout)['errors']
            self.assertTrue(any('Zone coverage differs' in e for e in errors),errors)

    def test_pm_questions_are_classified_not_silently_resolved(self):
        questions=self.boundaries['open_questions']
        self.assertEqual({q['classification'] for q in questions},{'open','followup_issue'})
        self.assertEqual(sum(1 for q in questions if q['classification']=='answered'),0)
        del questions[0]['followup_issue'];self.assertRejected('Follow-up PM question')
        questions[0]['classification']='answered';self.assertRejected('decision source')
        questions[0]['decision_source']='PM comment on #70 (recheck before accept)'
        self.assertEqual(self.check()['errors'],[])
        self.assertEqual(self.check()['pmQuestionsAnswered'],1)
        blocker=questions[1]['open_blocker'];del questions[1]['open_blocker'];self.assertRejected('explicit blocker')
        questions[1]['open_blocker']=blocker
        classification=questions[2]['classification'];questions[2]['classification']='maybe'
        self.assertRejected('answered/open/followup_issue')
        questions[2]['classification']=classification
        self.boundaries['open_questions']=questions[:6];self.assertRejected('PM question count')

    def test_proposal_declaration_cannot_self_promote(self):
        self.assertEqual(self.boundaries['mode'],'isolated_proposal')
        self.assertFalse(self.boundaries['canonical_write_allowed'])
        self.boundaries['canonical_write_allowed']=True;self.assertRejected('canonical_write_allowed')
        self.boundaries['canonical_write_allowed']=False
        self.boundaries['mode']='canonical';self.assertRejected('isolated_proposal')
        self.boundaries['mode']='isolated_proposal'
        self.boundaries['status']='accepted';self.assertRejected('may not claim status')

    def test_provenance_records_the_base_commit_and_drift(self):
        provenance=self.boundaries['provenance']
        self.assertEqual(provenance['base_commit'],'c1a7b78d895bd87216b64a6e5ca5d26eb6d85b3e')
        self.assertTrue(provenance['drift'])
        provenance['base_commit']='c1a7b78';self.assertRejected('40-hex base commit')
        provenance['base_commit']='c1a7b78d895bd87216b64a6e5ca5d26eb6d85b3e'
        provenance['drift']=['not-an-entry'];self.assertRejected('drift')
        del self.boundaries['provenance'];self.assertRejected('provenance record')

    def test_synthetic_basis_needs_recorded_values(self):
        zone=self.zone('station_concourse_2f')
        self.assertEqual(zone['measurement_basis'],'none')
        zone['measurement_basis']='synthetic_estimate';self.assertRejected('synthetic values')
        zone['synthetic_estimates']=[{'dimension':'ceiling_height_m','value_m':4.5,'rationale':'design placeholder only'}]
        self.assertEqual(self.check()['errors'],[])

    def test_portal_dimension_must_match_a_published_value(self):
        portal=self.portal('portal.underground_connector--underground_shopping_passage')
        portal['opening_width_m']=8.0;self.assertRejected('operator-published opening value')
        self.coverage['numeric_constraints'].append({'id':'portal_opening','zone':'underground_connector','opening_width_m':8.0})
        self.assertEqual(self.check()['errors'],[])
        portal['opening_width_m']=9.0;self.assertRejected('disagrees with the published value')

    def test_malformed_entries_report_instead_of_crashing(self):
        self.boundaries['sources'][0]['id']=[]
        self.boundaries['bundles'][0]['zones']=[5]
        self.boundaries['zones'][0]['inputs'][0]['allowed_use']=[['x']]
        self.boundaries['zones'][0]['adjacent_zones']=[[]]
        self.boundaries['zones'][0]['numeric_constraint_refs']=[[1]]
        self.boundaries['portals'][0]['level_change']=[]
        self.boundaries['open_questions'][0]['classification']=[]
        report=self.check()
        self.assertEqual(report['result'],'fail')
        for fragment in ('Source IDs must be non-empty strings','Bundle zone entries must be strings',
                         'Unknown allowed use','Zone adjacency entries must be strings',
                         'Unknown portal level change','answered/open/followup_issue'):
            self.assertTrue(any(fragment in e for e in report['errors']),fragment)

    def test_entry_type_mistakes_are_violations_not_unratable(self):
        self.boundaries['zones'][0]=5;self.assertRejected('Zone entry must be a JSON object')
        self.boundaries=json.loads((ROOT/zones.BOUNDARIES).read_text(encoding='utf-8'))
        self.boundaries['portals'][0]=5;self.assertRejected('Portal entry must be a JSON object')

if __name__=='__main__':unittest.main()
