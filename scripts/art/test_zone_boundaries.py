import importlib.util
import json
from pathlib import Path
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
        self.assertEqual((report['zones'],report['portals'],report['crossBundlePortals']),(13,12,3))
        self.assertEqual(report['metricVerifiedZones'],0);self.assertFalse(report['facilityFidelityValidated'])

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

if __name__=='__main__':unittest.main()
