"""Tests for the PRD's machine-readable contracts, not the product runtime."""
import copy
import unittest
from validate_contracts import load_bundle, validate

class ContractRegressionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls): cls.base=load_bundle()
    def rejected(self, mutate, expected):
        b=copy.deepcopy(self.base);mutate(b)
        self.assertIn(expected, {e['code'] for e in validate(b)['errors']})
    def test_valid_bundle(self): self.assertEqual(validate(self.base)['errors'], [])
    def test_duplicate_requirement(self): self.rejected(lambda b:b['req']['requirements'].append(copy.deepcopy(b['req']['requirements'][0])), 'DUPLICATE_REQ')
    def test_original_id_deleted(self): self.rejected(lambda b:b['req']['requirements'].pop(0), 'MISSING_LEGACY_REQ')
    def test_bad_source(self): self.rejected(lambda b:b['req']['requirements'][0]['sourceIds'].append('MISSING-SOURCE'), 'BAD_SOURCE_REF')
    def test_bad_test(self): self.rejected(lambda b:b['req']['requirements'][0]['acceptanceTestIds'].append('AT-999'), 'BAD_TEST_REF')
    def test_test_without_req(self): self.rejected(lambda b:b['tests']['tests'][0]['requirementIds'].append('REQ-999'), 'BAD_REQ_REF')
    def test_product_fake_pass(self): self.rejected(lambda b:b['tests']['tests'][0].update(result='PASS'), 'PRODUCT_TEST_NOT_EXECUTED')
    def test_third_product_mode(self): self.rejected(lambda b:b['modes']['modes'].append('STUDY_B'), 'TWO_MODES_ONLY')
    def test_guidance_changes_physics(self): self.rejected(lambda b:b['modes'].update(contentChangesPhysics=True), 'SAME_CORE_REQUIRED')
    def test_abc_not_a_mode(self): self.rejected(lambda b:b['study']['comparison'].update(conditionsAreProductModes=True), 'ABC_NOT_PRODUCT_MODE')
    def test_success_only_analysis(self): self.rejected(lambda b:b['study']['comparison'].update(allAttemptsDenominator=False), 'ALL_ATTEMPTS_REQUIRED')
    def test_saving_target_30_not_current(self): self.rejected(lambda b:b['study']['primaryTargets']['totalPersonHoursReductionPercent'].update(proposed=30), 'TARGET_REVISED_TO_20')
    def test_measured_saving_fabricated(self): self.rejected(lambda b:b['study']['primaryTargets']['totalPersonHoursReductionPercent'].update(measured=20), 'NO_MEASUREMENT_IN_DELIVERY')
    def test_unattended_person_hours(self): self.rejected(lambda b:b['time'].update(unattendedComputeInPersonTime=True), 'FOUR_CLOCKS_DISTINCT')
    def test_developer_input_omitted(self): self.rejected(lambda b:b['time'].update(includeDeveloperAssistance=False), 'FULL_EFFORT_BOUNDARY')
    def test_critique_fake_resolved(self): self.rejected(lambda b:b['critique']['critiques'][0].update(empiricalResolution='RESOLVED'), 'CRITIQUE_UNTESTED')
    def test_critique_no_requirement(self): self.rejected(lambda b:b['critique']['critiques'][0].update(requirementIds=[]), 'CRITIQUE_UNMAPPED')
    def test_missing_producer(self): self.rejected(lambda b:b['epics']['epics'][1]['candidateInputs'][0].update(producer='EP99'), 'BAD_PRODUCER')
    def test_missing_artifact(self): self.rejected(lambda b:b['epics']['epics'][1]['candidateInputs'][0]['artifactGroup'].append('invented'), 'BAD_ARTIFACT')
    def test_dependency_cycle(self):
        self.rejected(lambda b:b['epics']['epics'][0]['candidateInputs'].append({'producer':'EP01','producerPhase':'candidate','consumerPhase':'candidate','artifactGroup':['production-schema-bundle']}), 'DEPENDENCY_CYCLE')
    def test_study_all_physics_blocker(self): self.rejected(lambda b:b['epics']['epics'][10]['lanes']['user_study'].update(requiresAllPhysics=True), 'STUDY_NOT_ALL_PHYSICS')
    def test_common_input_reintroduces_physics(self):
        self.rejected(lambda b:b['epics']['epics'][10]['candidateInputs'].append({'producer':'EP08','producerPhase':'candidate','consumerPhase':'candidate','artifactGroup':['qoi-validation-report']}), 'STUDY_NOT_ALL_PHYSICS')
    def test_paid_contract_global_blocker(self): self.rejected(lambda b:b['gates'].update(developmentRequiresPaidContract=True), 'NO_PURCHASE_GLOBAL_BLOCKER')
    def test_document_pass_is_not_product(self): self.rejected(lambda b:b['gates'].update(documentTestsCountAsProductEvidence=True), 'NO_DOCUMENT_APPROVAL')
    def test_single_score(self): self.rejected(lambda b:b['gates'].update(singleOverallScoreAllowed=True), 'INDEPENDENT_AXES')
    def test_model_scope_bypass(self):
        self.rejected(lambda b:b['gates']['profiles'][3]['conditionalAxes'][0].update(conditionCannotBeDisabledToAvoidMissingEvidence=False), 'NO_SCOPE_BYPASS')
    def test_twin_no_sync(self): self.rejected(lambda b:b['gates']['profiles'][4]['requiredAxes'].remove('TWIN_SYNC'), 'TWIN_REQUIRES_SYNC')
    def test_original_root_deleted(self): self.rejected(lambda b:b['world']['rootRegions'].pop(), 'LEGACY_WORLD_IDS')
    def test_rule_catalog_deleted(self): self.rejected(lambda b:b['rules']['rules'].pop(), 'LEGACY_RULE_IDS')
    def test_free_catalog_deleted(self): self.rejected(lambda b:b['free']['records'].pop(), 'FREE_CATALOG_PRESERVED')
    def test_paid_default_reintroduced(self): self.rejected(lambda b:b['req']['requirements'][63].update(sourceIds=['AST-SYNTY']), 'FREE_FIRST_DEFAULT')
    def test_source_url_silently_changed(self): self.rejected(lambda b:b['sources']['sources'][0].update(url='https://example.org/'), 'LEGACY_SOURCE_URL')

if __name__=='__main__': unittest.main(verbosity=2)
