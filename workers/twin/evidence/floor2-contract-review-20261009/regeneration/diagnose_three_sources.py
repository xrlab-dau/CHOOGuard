#!/usr/bin/env python3
from pathlib import Path
from collections import Counter,defaultdict
import json,hashlib,struct
import numpy as np
import yaml
ROOT=Path.cwd();OUT=ROOT/'workers/twin/evidence/floor2-contract-review-20261009/regeneration';BASE=ROOT/'.planning/2026-10-08-interior-runtime-completion/floor-guide-layout'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def exact_counter(x,full=True):
 v=np.asarray(x['vertices'],dtype='<f4');uv=np.asarray(x['uv'],dtype='<f4');n=np.asarray(x['normals'],dtype='<f4');ta=np.asarray(x['tangents'],dtype='<f4');indices=np.asarray(x['indices'],dtype=np.int64).reshape(-1,3);assert np.isfinite(v).all() and np.isfinite(uv).all() and np.isfinite(n).all() and np.isfinite(ta).all()
 values=np.c_[v,uv,n,ta] if full else np.c_[v,uv]
 counter=Counter();owners=defaultdict(list)
 for ordinal,tri in enumerate(indices):
  corners=[values[i].astype('<f4').tobytes() for i in tri];rotations=[tuple(corners[k:]+corners[:k]) for k in range(3)];key=min(rotations);counter[key]+=1;owners[key].append(ordinal)
 return counter,owners
reports=[]
for name in ['Kit_Frame_Canopy_Dark','Kit_Glass','Kit_Trim_Kit_Hairline']:
 probe_path=BASE/('rebuild-'+name+'.json');p=json.loads(probe_path.read_text());a=p['original'];b=p['generated'];ca,oa=exact_counter(a,True);cb,ob=exact_counter(b,True);pa,op=exact_counter(a,False);pb,bop=exact_counter(b,False);missing=ca-cb;extra=cb-ca
 r={'name':name,'probe_path':str(probe_path.relative_to(ROOT)),'probe_sha256':sha(probe_path),'source_asset':p['source_asset'],'source_file_sha256':sha(p['source_asset']),'original_complete_digest':p['original_complete_digest'],'generated_complete_digest':p['generated_complete_digest'],'full_exact_oriented_float32_triangle_multiset_equal':ca==cb,'position_uv_exact_oriented_float32_triangle_multiset_equal':pa==pb,'unmatched_full_original':sum(missing.values()),'unmatched_full_generated':sum(extra.values()),'unmatched_original_triangle_ordinals':[i for k in missing for i in oa[k]],'unmatched_generated_triangle_ordinals':[i for k in extra for i in ob[k]],'unmatched_original_position_uv_ordinals':[i for k in pa-pb for i in op[k]],'unmatched_generated_position_uv_ordinals':[i for k in pb-pa for i in bop[k]],'triangle_count':len(a['indices'])//3}
 reports.append(r);print(json.dumps(r,ensure_ascii=False))
(OUT/'initial-diagnosis.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2)+'\n')
