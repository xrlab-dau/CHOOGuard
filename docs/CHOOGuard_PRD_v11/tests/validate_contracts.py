"""Validate PRD declarations; this never runs or approves the simulator."""
from __future__ import annotations
import copy
import json
import re
from collections import Counter
from pathlib import Path
from typing import Any
from jsonschema import Draft202012Validator

ROOT=Path(__file__).resolve().parents[1]
FILES={'req':'requirements','tests':'acceptance-tests','epics':'epics','sources':'sources','modes':'modes','world':'world-profile','rules':'rule-catalog','free':'free-assets','study':'study-protocol','time':'time-metrics','gates':'gate-policy','critique':'critique-map','needs':'user-needs','ontology':'ontology'}

def load_bundle(root:Path=ROOT)->dict[str,Any]:
    b={}
    for k,name in FILES.items():
        b[k]=json.loads((root/'contracts'/(name+('.jsonld' if k=='ontology' else '.json'))).read_text())
    b['_legacy_req']=json.loads((root/'history/requirements_v10.json').read_text())
    b['_legacy_sources']=json.loads((root/'history/v10-sources.json').read_text())
    b['_legacy_world']=json.loads((root/'history/v10-world-profile.json').read_text())
    b['_legacy_rules']=json.loads((root/'history/v10-rule-catalog.json').read_text())
    b['_legacy_free']=json.loads((root/'history/free-assets_previous.json').read_text())
    b['_schemas']={p.stem.replace('.schema',''):json.loads(p.read_text()) for p in (root/'schemas').glob('*.json')}
    return b

def ids(items:list)->set:
    return {i if isinstance(i,str) else i.get('id',i.get('regionId')) for i in items}

def validate(b:dict[str,Any])->dict[str,Any]:
    errors=[]
    def need(ok:bool,code:str,detail:str=''):
        if not ok: errors.append({'code':code,'detail':detail})
    for key,name in FILES.items():
        if name in b['_schemas']:
            for er in Draft202012Validator(b['_schemas'][name]).iter_errors(b[key]):
                errors.append({'code':'SCHEMA','detail':f'{name}: {list(er.path)} {er.message}'})
    reqs=b['req']['requirements'];tests=b['tests']['tests'];epics=b['epics']['epics'];sources=b['sources']['sources'];cs=b['critique']['critiques']
    rid=ids(reqs);tid=ids(tests);eid={e['epicId'] for e in epics};sid=ids(sources);cid=ids(cs);uid=ids(b['needs']['userNeeds'])
    need(len(rid)==len(reqs),'DUPLICATE_REQ')
    need(len(tid)==len(tests),'DUPLICATE_TEST')
    need(len(sid)==len(sources),'DUPLICATE_SOURCE')
    need(ids(b['_legacy_req']['requirements'])<=rid,'MISSING_LEGACY_REQ')
    need(eid=={f'EP{x:02d}' for x in range(12)},'PRESERVE_EP_IDS')
    need(cid=={f'C{x:02d}' for x in range(1,13)},'PRESERVE_CRITIQUE_IDS')
    tmap={t['id']:t for t in tests};rmap={r['id']:r for r in reqs};emap={e['epicId']:e for e in epics};smap={s['id']:s for s in sources}
    for r in reqs:
        need(set(r['sourceIds'])<=sid,'BAD_SOURCE_REF',r['id'])
        need(set(r['acceptanceTestIds'])<=tid,'BAD_TEST_REF',r['id'])
        need(set(r['userRequirementIds'])<=uid,'BAD_USER_REF',r['id'])
        need(r['epicId'] in eid,'BAD_EP_REF',r['id'])
        need(set(r['critiqueIds'])<=cid,'BAD_CRITIQUE_REF',r['id'])
        need(r['implementationStatus']=='NOT_VERIFIED_IN_THIS_REVISION','NO_IMPLEMENTATION_CLAIM',r['id'])
        for a in r['acceptanceTestIds']:
            if a in tmap: need(r['id'] in tmap[a]['requirementIds'],'ASYMMETRIC_REQ_TEST',r['id'])
    for t in tests:
        need(set(t['requirementIds'])<=rid,'BAD_REQ_REF',t['id'])
        need(t['result']=='NOT_RUN','PRODUCT_TEST_NOT_EXECUTED',t['id'])
        need(bool(t.get('evidenceRequired')),'MISSING_TEST_EVIDENCE',t['id'])
    for c in cs:
        need(bool(c['requirementIds']) and set(c['requirementIds'])<=rid,'CRITIQUE_UNMAPPED',c['id'])
        need(c['empiricalResolution']=='NOT_TESTED','CRITIQUE_UNTESTED',c['id'])
        need(set(c['testIds'])<=tid,'BAD_TEST_REF',c['id'])
    need(b['modes']['modes']==['TUTORIAL','RANDOM_OPERATIONS_LAB'],'TWO_MODES_ONLY')
    need(b['modes']['sameCoreAcrossModes'] and not b['modes']['contentChangesPhysics'],'SAME_CORE_REQUIRED')
    need(not b['study']['comparison']['conditionsAreProductModes'],'ABC_NOT_PRODUCT_MODE')
    need(b['study']['comparison']['allAttemptsDenominator'],'ALL_ATTEMPTS_REQUIRED')
    need(b['study']['primaryTargets']['totalPersonHoursReductionPercent']['proposed']==20,'TARGET_REVISED_TO_20')
    need(b['study']['primaryTargets']['totalPersonHoursReductionPercent']['measured'] is None,'NO_MEASUREMENT_IN_DELIVERY')
    need(not b['time']['unattendedComputeInPersonTime'] and len(b['time']['clocks'])==4,'FOUR_CLOCKS_DISTINCT')
    need(all(b['time'][k] for k in ['includeDeveloperAssistance','includeInitialDataAndReview','includePostExportEditing','includeMaintenance']),'FULL_EFFORT_BOUNDARY')
    need(b['time']['samePersonOverlapPolicy']=='UNION_INTERVALS','SAME_PERSON_UNION')
    need(not b['gates']['developmentRequiresPaidContract'],'NO_PURCHASE_GLOBAL_BLOCKER')
    need(not b['gates']['documentTestsCountAsProductEvidence'],'NO_DOCUMENT_APPROVAL')
    need(b['gates']['independentAxes'] and not b['gates']['singleOverallScoreAllowed'],'INDEPENDENT_AXES')
    need(all(v=='NOT_EVALUATED' for v in b['gates']['axisStates'].values()),'NO_PRODUCT_QUALIFICATION_CLAIM')
    profiles={p['id']:p for p in b['gates']['profiles']}
    need(profiles['NAMED_EXERCISE_USE']['conditionalAxes'][0]['conditionCannotBeDisabledToAvoidMissingEvidence'],'NO_SCOPE_BYPASS')
    need('TWIN_SYNC' in profiles['TWIN_QUALIFIED']['requiredAxes'],'TWIN_REQUIRES_SYNC')
    need(ids(b['world']['rootRegions'])==ids(b['_legacy_world']['rootRegions']),'LEGACY_WORLD_IDS')
    need(ids(b['rules']['rules'])==ids(b['_legacy_rules']['rules']),'LEGACY_RULE_IDS')
    need(ids(b['free']['records'])==ids(b['_legacy_free']['records']),'FREE_CATALOG_PRESERVED')
    need('REQ-064' in rmap and 'FREE-001' in rmap['REQ-064']['sourceIds'] and 'AST-SYNTY' not in rmap['REQ-064']['sourceIds'],'FREE_FIRST_DEFAULT')
    for s in b['_legacy_sources']['sources']:
        need(s['id'] in smap and s['url']==smap[s['id']]['url'],'LEGACY_SOURCE_URL',s['id'])
    graph={e['epicId']:[] for e in epics}
    def checked_edges(e,edges):
        for edge in edges:
            p=edge.get('producer'); need(p in emap,'BAD_PRODUCER',str(p))
            if p in emap: need(set(edge['artifactGroup'])<=set(emap[p]['outputs']),'BAD_ARTIFACT',e['epicId'])
            need(edge.get('producerPhase') in ['candidate','accept'],'BAD_PHASE',e['epicId'])
            need(edge.get('consumerPhase') in ['candidate','accept'],'BAD_PHASE',e['epicId'])
    for e in epics:
        checked_edges(e,e['candidateInputs'])
        graph[e['epicId']]=[x['producer'] for x in e['candidateInputs'] if x['producer'] in emap]
        for lane in e.get('lanes',{}).values():checked_edges(e,lane.get('additionalInputs',[]))
        need(set(e['requirementIds'])=={r['id'] for r in reqs if r['epicId']==e['epicId']},'PACKET_REQ_DRIFT',e['epicId'])
    seen=set();stack=set()
    def visit(n):
        if n in stack:return False
        if n in seen:return True
        stack.add(n)
        ok=all(visit(x) for x in graph[n]); stack.remove(n);seen.add(n);return ok
    need(all(visit(n) for n in graph),'DEPENDENCY_CYCLE')
    if 'EP10' in emap:
        e=emap['EP10']; need(not e['lanes']['user_study'].get('requiresAllPhysics',True),'STUDY_NOT_ALL_PHYSICS')
        need(not any(x['producer'] in ['EP07','EP08'] for x in e['candidateInputs']),'STUDY_NOT_ALL_PHYSICS')
    return dict(scope='DOCUMENT_CONTRACT_CHECK_ONLY',ok=not errors,errors=errors,counts={'requirements':len(reqs),'futureAcceptanceTests':len(tests),'epics':len(epics),'critiques':len(cs),'sourceRecords':len(sources),'freeCandidateRecords':len(b['free']['records'])},productExecution='NOT_RUN',fieldValidation='NOT_RUN')

if __name__=='__main__':
    result=validate(load_bundle()); print(json.dumps(result,ensure_ascii=False,indent=2));raise SystemExit(0 if result['ok'] else 1)
