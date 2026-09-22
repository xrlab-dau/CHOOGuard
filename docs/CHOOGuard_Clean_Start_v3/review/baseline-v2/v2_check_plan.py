"""Validate the new planning contracts only; does not inspect or change a repository."""
from __future__ import annotations
import json
import re
import sys
from collections import defaultdict
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
FORBIDDEN = re.compile(r'\bEP\d{2}(?:-S\d{2})?\b|com\.xrlab\.chooguard|AuthoritativeShift|ICommitSink|work-orders/|aae4867c|github\.com/xrlab-dau/CHOOGuard')

def load(root: Path = ROOT) -> dict:
    def read(p: str):
        return json.loads((root / p).read_text(encoding='utf-8'))
    return {'baseline': read('contracts/baseline.json'), 'epics': read('contracts/epics.json')['epics'],
            'tasks': read('contracts/tasks.json')['tasks'], 'requirements': read('contracts/requirements.json')['requirements'],
            'tests': read('contracts/product-acceptance.json')['tests'], 'sources': read('reference/sources.json')['sources']}

def validate(d: dict) -> list[str]:
    errors = []
    def require(condition, message):
        if not condition: errors.append(message)
    b = d['baseline']
    require(b.get('developmentMode') == 'GREENFIELD', 'baseline: not greenfield')
    for field in ['legacyCompatibilityRequired','legacyCodeRequired','priorEpicGraphInherited','priorArtifactAcceptanceInherited','fixedRegionIdsInherited','remoteMutationsPerformed','resetPerformed']:
        require(b.get(field) is False, 'baseline: '+field)
    require(b.get('productModeIds') == ['TUTORIAL','RANDOM_OPERATIONS_LAB'], 'baseline: user modes')
    es = {e['id']: e for e in d['epics']}; ts = {t['id']: t for t in d['tasks']}; rs = {r['id']: r for r in d['requirements']}
    ats = {a['id']:a for a in d['tests']}; ss = {s['id'] for s in d['sources']}
    require(len(es) == len(d['epics']) == 11, 'epic IDs/cardinality')
    require(len(ts) == len(d['tasks']) == 45, 'task IDs/cardinality')
    require(set(rs) == {f'REQ-{i:03}' for i in range(1,92)}, 'product requirement coverage')
    require(set(ats) == {f'AT-{i:03}' for i in range(1,92)}, 'product test coverage')
    paths = defaultdict(list)
    for t in d['tasks']:
        require(t['epicId'] in es, 'unknown epic: '+t['id'])
        require(t['status'] == 'SPECIFIED_NOT_IMPLEMENTED' and t['acceptanceState'] == 'NOT_RUN', 'false implementation status: '+t['id'])
        require(t['artifact']['id'] == 'A-'+t['id'] and t['artifact']['status']=='NOT_PRODUCED', 'artifact identity/status: '+t['id'])
        require(len(t['acceptance']) >= 2 and all(x['status']=='NOT_RUN' for x in t['acceptance']), 'task tests status: '+t['id'])
        require(len(t['algorithm']) >= 3 and bool(t['contract']), 'underspecified task: '+t['id'])
        require(set(t['sourceIds']) <= ss, 'missing source: '+t['id'])
        require(t['assignee'] is None, 'unexpected personal assignment: '+t['id'])
        for path in t['plannedFiles']+[t['verificationFile']]:
            require(not path.startswith('/') and '..' not in Path(path).parts, 'invalid new path: '+path)
            paths[path].append(t['id'])
        for dep in t['dependsOn']:
            require(dep['taskId'] in ts, 'missing dependency: '+dep['taskId'])
            require(dep['artifactId'] == 'A-'+dep['taskId'], 'wrong artifact dependency: '+t['id'])
            require(dep['consumerStage'] in ['integration','qualification'], 'invalid consuming stage: '+t['id'])
        require(set(t['requirementIds']) == {r['id'] for r in rs.values() if r['taskId']==t['id']}, 'task requirement binding: '+t['id'])
    for path, owners in paths.items():
        require(len(owners)==1, 'duplicate write owner: '+path)
    for e in es.values():
        require(set(e['taskIds']) == {t['id'] for t in ts.values() if t['epicId']==e['id']}, 'epic membership: '+e['id'])
    for r in rs.values():
        require(r['taskId'] in ts, 'missing requirement owner: '+r['id'])
        if r['taskId'] in ts: require(r['epicId']==ts[r['taskId']]['epicId'], 'requirement epic mismatch: '+r['id'])
        require(r['acceptanceTestId'] in ats, 'missing product test: '+r['id'])
        require(set(r['sourceIds']) <= ss, 'missing requirement source: '+r['id'])
    for a in ats.values():
        require(a['result']=='NOT_RUN', 'false product pass: '+a['id'])
        require(a['taskId'] in ts, 'test owner missing: '+a['id'])
    colors = {}
    def visit(id):
        if colors.get(id)==1:
            errors.append('dependency cycle: '+id); return
        if colors.get(id)==2: return
        colors[id]=1
        for dep in ts[id]['dependsOn']:
            if dep['taskId'] in ts: visit(dep['taskId'])
        colors[id]=2
    for id in ts: visit(id)
    require(FORBIDDEN.search(json.dumps(d,ensure_ascii=False)) is None, 'legacy execution dependency present')
    return errors

def check_files(root: Path = ROOT) -> list[str]:
    errors=[]
    for path in root.rglob('*'):
        if not path.is_file() or path.suffix not in ['.json','.md']: continue
        text=path.read_text(encoding='utf-8')
        if FORBIDDEN.search(text): errors.append('legacy token in '+str(path.relative_to(root)))
        if path.suffix=='.md':
            for target in re.findall(r'\]\(([^)]+)\)',text):
                if target.startswith(('https://','http://','#','mailto:')): continue
                local=(path.parent / target.split('#')[0])
                if not local.exists(): errors.append('broken document link '+str(path.relative_to(root))+' -> '+target)
    data=load(root)
    for task in data['tasks']:
        path=root/'tasks'/f"{task['id']}.json"
        if not path.exists() or json.loads(path.read_text(encoding='utf-8'))!=task:
            errors.append('task packet drift: '+task['id'])
    return errors

def main() -> int:
    try:
        d=load(); errors=validate(d)+check_files()
    except (OSError,ValueError,KeyError) as exc:
        print(json.dumps({'ok':False,'error':str(exc)},ensure_ascii=False)); return 2
    print(json.dumps({'ok':not errors,'scope':'DOCUMENT_CONTRACT_CHECK_ONLY','epics':len(d['epics']),'tasks':len(d['tasks']),
                      'productRequirements':len(d['requirements']),'futureProductTests':len(d['tests']),
                      'productExecution':'NOT_RUN','errors':errors},ensure_ascii=False,indent=2))
    return int(bool(errors))
if __name__=='__main__': sys.exit(main())
