"""Checks decision-document references and preserved inputs, not product behavior."""
from pathlib import Path
import hashlib,json,re,sys
from urllib.parse import urlparse
ROOT=Path(__file__).resolve().parents[1]
def load(p): return json.loads((ROOT/p).read_text(encoding='utf-8'))
def run():
 d=load('decisions.json'); s=load('sources.json'); manifest=load('basis-manifest.json')
 ep={x['epicId'] for x in load('basis/contracts/epics.json')['epics']}
 ur={x['id'] for x in load('basis/contracts/user-needs.json')['userNeeds']} | {x['id'] for x in d['additionalUserNeeds']}
 assets={x['id']:x for x in load('basis/free-assets.json')['records']}
 source_ids={x['id'] for x in s['sources']}
 checks=[]
 def check(name,condition): checks.append({'check':name,'result':'PASS' if condition else 'FAIL'})
 check('unique_decision_ids',len({x['id'] for x in d['decisions']})==len(d['decisions']))
 check('two_user_modes_shared_core',d['userModes']==['TUTORIAL','RANDOM_OPERATIONS_LAB'] and d['samePhysicsAndOperationsCore'] is True)
 check('existing_ep_refs',all(set(x['epicIds'])<=ep and x['epicIds'] for x in d['decisions']))
 check('user_requirement_refs',all(set(x['userNeedIds'])<=ur for x in d['decisions']))
 check('all_user_needs_mapped',ur==set().union(*(set(x['userNeedIds']) for x in d['decisions'])))
 check('source_refs',all(set(x['sourceIds'])<=source_ids for x in d['decisions']))
 text=(ROOT/'DECISIONS.md').read_text(encoding='utf-8')
 check('markdown_source_tokens',set(re.findall(r'\[(SRC-[A-Z0-9-]+)\]',text))<=source_ids)
 selected=[]
 for v in d['freeFirstSelection'].values():
  if isinstance(v,list): selected.extend(v)
 check('selected_assets_exist',set(selected)<=set(assets))
 check('selected_zero_cost_offers',all(assets[x].get('minimumAssetPurchaseUSD')==0 for x in selected))
 check('no_paid_mandatory_pack',d['freeFirstSelection']['paidMandatoryPackCount']==0)
 check('no_product_completion_claim',d['productAcceptance']=='NOT_EVALUATED' and d['unityRunThisTurn']==d['physicsRunThisTurn']=='NOT_RUN')
 check('no_claimed_remote_write',not d['githubModifiedThisTurn'] and not d['repoFilesModifiedThisTurn'])
 check('baseline_hashes_preserved',all((ROOT/f['path']).is_file() and (ROOT/f['path']).stat().st_size==f['bytes'] and hashlib.sha256((ROOT/f['path']).read_bytes()).hexdigest()==f['sha256'] for f in manifest['files']))
 check('source_urls_absolute_https',all(urlparse(x['url']).scheme=='https' and urlparse(x['url']).netloc for x in s['sources']))
 result={'scope':'DECISION_DOCUMENT_STRUCTURE_ONLY','runDate':'2026-09-19','checks':checks,'passed':sum(x['result']=='PASS' for x in checks),'failed':sum(x['result']=='FAIL' for x in checks),'notRun':['Unity','solver execution','institutional manual qualification','site measurement','new asset import','independent model/user study']}
 print(json.dumps(result,ensure_ascii=False,indent=2))
 return 0 if result['failed']==0 else 1
if __name__=='__main__': sys.exit(run())
