"""Exact oriented triangle certificate; no Assets mutation or equivalence tolerance."""
from pathlib import Path
from collections import Counter,defaultdict
import json,hashlib,struct,math
import numpy as np
from scipy.optimize import linear_sum_assignment
EV=Path('workers/twin/evidence/floor2-architecture-next-20261009/review-floor1-stage/regeneration');BASE=Path('.planning/2026-10-08-interior-runtime-completion/floor-guide-layout')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def extract(x):
 v=np.array(x['vertices'],float);uv=np.array(x['uv'],float);normal=np.array(x['normals'],float);tangent=np.array(x['tangents'],float);t=np.array(x['indices'],int).reshape(-1,3);groups=defaultdict(list)
 assert np.isfinite(v).all() and np.isfinite(uv).all() and np.isfinite(normal).all() and np.isfinite(tangent).all()
 assert np.min(t)>=0 and np.max(t)<len(v) and len(uv)==len(normal)==len(tangent)==len(v)
 for ordinal,indices in enumerate(t):
  corners=[tuple(v[i])+tuple(uv[i]) for i in indices];choices=[tuple(corners[k:]+corners[:k]) for k in range(3)];k=min(range(3),key=lambda j:choices[j]);ordered=np.roll(indices,-k);groups[choices[k]].append({'triangle':ordinal,'ordered_vertex_indices':ordered.tolist(),'normals':normal[ordered],'tangents':tangent[ordered]})
 areas=np.linalg.norm(np.cross(v[t[:,1]]-v[t[:,0]],v[t[:,2]]-v[t[:,0]]),axis=1)/2
 return groups,{'vertices':len(v),'triangles':len(t),'referenced_vertices':len(np.unique(t)),'zero_area_triangles':int((areas==0).sum()),'position_bounds':[v.min(0).tolist(),v.max(0).tolist()]}
def digest_counter(c):
 h=hashlib.sha256()
 for key,count in sorted(c.items()):h.update(struct.pack('<15dI',*[n for point in key for n in point],count))
 return h.hexdigest()
def attrs_key(row):return tuple(np.c_[row['normals'],row['tangents']].ravel())
for name in ['Kit_Glass','Kit_Trim_Kit_Hairline']:
 source=BASE/('rebuild-'+name+'.json');probe=json.loads(source.read_text());a,am=extract(probe['original']);b,bm=extract(probe['generated']);ca=Counter({k:len(x) for k,x in a.items()});cb=Counter({k:len(x) for k,x in b.items()});missing_a=ca-cb;missing_b=cb-ca
 pairs=[];duplicates=[]
 for key in sorted(set(a)&set(b)):
  aa,bb=a[key],b[key]
  if len(aa)>1 or len(bb)>1:duplicates.append({'key':key,'original_count':len(aa),'generated_count':len(bb)})
  if len(aa)==1 and len(bb)==1:indices=[(0,0)]
  else:
   costs=np.array([[np.linalg.norm(x['normals']-y['normals'])+np.linalg.norm(x['tangents']-y['tangents']) for y in bb] for x in aa]);r,c=linear_sum_assignment(costs);indices=list(zip(r,c))
  for i,j in indices:pairs.append((key,aa[i],bb[j]))
 nmax={'l2':-1};tmax={'l2':-1};n_component_max=0.;t_component_max=0.;angle_max=0.;changed_normal_corners=0;changed_tangent_corners=0;attribute_triangle_exact=True
 for key,x,y in pairs:
  if not np.array_equal(x['normals'],y['normals']) or not np.array_equal(x['tangents'],y['tangents']):attribute_triangle_exact=False
  for corner in range(3):
   na,nb=x['normals'][corner],y['normals'][corner];ta,tb=x['tangents'][corner],y['tangents'][corner];nr=float(np.linalg.norm(na-nb));tr=float(np.linalg.norm(ta-tb));n_component_max=max(n_component_max,float(abs(na-nb).max()));t_component_max=max(t_component_max,float(abs(ta-tb).max()));changed_normal_corners+=int(not np.array_equal(na,nb));changed_tangent_corners+=int(not np.array_equal(ta,tb))
   if not np.array_equal(na,nb) and np.linalg.norm(na)>0 and np.linalg.norm(nb)>0:angle_max=max(angle_max,math.degrees(math.acos(float(np.clip(np.dot(na,nb)/(np.linalg.norm(na)*np.linalg.norm(nb)),-1,1)))))
   detail={'original_triangle':x['triangle'],'generated_triangle':y['triangle'],'canonical_corner':corner,'position_uv_key':key[corner]}
   if nr>nmax['l2']:nmax={'l2':nr,'original':na.tolist(),'generated':nb.tolist(),**detail}
   if tr>tmax['l2']:tmax={'l2':tr,'original':ta.tolist(),'generated':tb.tolist(),**detail}
 # Exact full provided-channel multiset handles duplicates without relying on an assignment policy.
 attrca=Counter((key,attrs_key(x)) for key,rows in a.items() for x in rows);attrcb=Counter((key,attrs_key(x)) for key,rows in b.items() for x in rows)
 proof={'schema':'chooguard.two-source-regeneration-triangle-certificate.v1','source_name':name,'probe_path':str(source),'probe_sha256':sha(source),'source_asset':probe['source_asset'],'source_asset_file_sha256':sha(probe['source_asset']),'provided_original_complete_digest':probe['original_complete_digest'],'provided_generated_complete_digest':probe['generated_complete_digest'],'complete_digest_equal':probe['original_complete_digest']==probe['generated_complete_digest'],'method':'Exact raw JSON numeric equality. Each key holds three(positionXYZ,UV0) corners; only cyclic rotations are canonicalized. Reversed orientation is never canonicalized. Multiplicity is preserved. No rounding or tolerance.','original_counts':am,'generated_counts':bm,'exact_oriented_position_UV_triangle_multiset_equal':ca==cb,'original_oriented_triangle_multiset_sha256':digest_counter(ca),'generated_oriented_triangle_multiset_sha256':digest_counter(cb),'unmatched_original_triangles':sum(missing_a.values()),'unmatched_generated_triangles':sum(missing_b.values()),'unmatched_original_examples':[{'key':k,'count':count} for k,count in list(missing_a.items())[:10]],'unmatched_generated_examples':[{'key':k,'count':count} for k,count in list(missing_b.items())[:10]],'duplicate_position_UV_triangle_keys':len(duplicates),'matched_triangle_count':len(pairs),'exact_provided_normal_tangent_triangle_multiset_equal':attrca==attrcb,'normal_residual':{'maximum_l2':nmax,'maximum_absolute_component':n_component_max,'maximum_angle_degrees_from_normalized_dot':angle_max,'changed_matched_corner_occurrences':changed_normal_corners},'tangent_residual':{'maximum_l2':tmax,'maximum_absolute_component':t_component_max,'changed_matched_corner_occurrences':changed_tangent_corners},'scope_limit':'This certifies only the supplied oriented position/UV triangle multiset and reports supplied normal/tangent differences. Complete-channel digests differ; omitted channel equivalence and generator guard acceptance remain root decisions.','tolerance_used':None,'asset_mutations':0,'verdict':'EXACT oriented triangle geometry/UV equivalent' if ca==cb else 'REFUTED oriented triangle geometry/UV equivalence'}
 path=EV/(name+'-certificate.json');path.write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'source':name,'exact_position_uv':ca==cb,'exact_position_uv_normal_tangent':attrca==attrcb,'duplicate_keys':len(duplicates),'triangles':am['triangles'],'normal_l2':nmax['l2'],'normal_component':n_component_max,'normal_angle':angle_max,'tangent_l2':tmax['l2'],'tangent_component':t_component_max,'certificate_sha256':sha(path)},ensure_ascii=False))
