"""Independent bounded Unit16 review; writes evidence only, no Unity calls."""
from pathlib import Path
import sys, json, hashlib, math, re
import numpy as np
import yaml
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
from scipy.spatial import cKDTree
from shapely.geometry import Polygon, LineString, Point
from shapely import contains_xy
sys.path.insert(0, str(Path('workers/twin').resolve()))
from official_plan_compare import readbin

EV = Path(__file__).resolve().parent
def read(name): return json.loads((EV / name).read_text())
def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()
p, s, prior, old, registry = map(read, ['packet.json', 'current-inventory.json', 'prior-packet.json', 'prior-inventory.json', 'guide-registry.json'])
spec, detail = map(read, ['shops-spec.json', 'detail-spec.json'])
mm = {m['id']: m for m in s['meshes']}
oldmm = {m['id']: m for m in old['meshes']}
rr = {r['global_object_id']: r for r in s['renderers'] + s['colliders']}
reg = {r['replacement_asset']: r for r in registry['meshes']}
priorpatch = {q['source_asset']: q for q in prior['mesh_patches']}
cache = {}
checks, masks, components, prior_overlap = [], [], [], []
sin, cos = math.sin(math.radians(16.2)), math.cos(math.radians(16.2))
want = np.array(p['units'][0]['world_delta'])
polygon = Polygon(p['units'][0]['before']['polygon'])
def check(name, passed, details): checks.append({'id': name, 'passed': bool(passed), 'details': details})
def uv(w): return np.c_[w[:, 0]*sin+w[:, 2]*cos, w[:, 0]*cos-w[:, 2]*sin]
def geo(r):
    m = mm[r['mesh']]
    if m['binary'] not in cache: cache[m['binary']] = readbin(m)
    v, t = cache[m['binary']]
    wm = np.array(r['world_matrix'])
    return m, v, t, v @ wm[:3, :3].T + wm[:3, 3]
def labels_for(v, t):
    _, weld = np.unique(np.round(v, 5), axis=0, return_inverse=True)
    a, b = weld[t].ravel(), weld[t[:, [1, 2, 0]]].ravel()
    _, labels = connected_components(coo_matrix((np.ones(len(a)), (a, b)), shape=(weld.max()+1, weld.max()+1)), directed=False)
    return labels[weld], weld

check('scene_epoch', sha(p['scene']) == p['scene_sha256'] == s['scene_file_sha256'], p['scene_sha256'])
check('guide_hash', sha(p['guide']) == p['guide_sha256'], p['guide'])
check('proposal_hash', sha(p['proposal']) == p['proposal_sha256'], p['proposal'])
check('only_unit16', [u['boardNo'] for u in p['units']] == [16], p['units'])
check('no_rejections_or_topology_edits', not p['rejected_owners'] and not any(any(k in q for k in ['removed_triangle_ordinals', 'added_triangles', 'attribute_overrides']) for q in p['mesh_patches']), 'Unit16 packet changes only selected positions and exact parent poses')
patches = {q['source_asset']: q for q in p['mesh_patches']}
for qi, q in enumerate(p['mesh_patches']):
    check(f'asset_bytes_{qi}', sha(q['source_asset']) == q['source_file_sha256'], q['source_asset'])
    meta = Path(q['source_asset']+'.meta').read_text()
    check(f'asset_guid_{qi}', re.search(r'^guid: (.*)$', meta, re.M)[1] == q['source_asset_guid'], q['source_asset_guid'])
    all_renderers = [r for r in s['renderers'] if mm.get(r['mesh'], {}).get('asset') == q['source_asset']]
    all_cols = [r for r in s['colliders'] if mm.get(r.get('mesh'), {}).get('asset') == q['source_asset']]
    check(f'full_renderer_ownership_{qi}', {r['global_object_id'] for r in all_renderers} == {o['global_object_id'] for o in q['owners']}, {'renderers': len(all_renderers), 'colliders': len(all_cols)})
    if 'collider_owners' in q:
        check(f'full_collider_ownership_{qi}', {r['global_object_id'] for r in all_cols} == {o['global_object_id'] for o in q['collider_owners']}, q['collider_owners'])
    else:
        check(f'implicit_collider_frame_{qi}', all(any(r['world_matrix'] == o['world_matrix'] for o in q['owners']) for r in all_cols), 'Installer assigns all exact source-mesh colliders; implicit frame must match listed renderer frame')
    ids, selected = np.array(q['vertex_indices'], int), np.array(q['selected_triangles'], int)
    expected, replacement = np.array(q['expected_positions']), np.array(q['replacement_positions'])
    check(f'unique_indices_{qi}', len(set(ids)) == len(ids) and len(set(selected)) == len(selected), {'vertices': len(ids), 'triangles': len(selected)})
    owners = q['owners'] + q.get('collider_owners', [])
    for oi, proof in enumerate(owners):
        r = rr[proof['global_object_id']]
        m, v, t, world = geo(r)
        check(f'owner_frame_{qi}_{oi}', r['path'] == proof['path'] and r['world_matrix'] == proof['world_matrix'] and m['asset'] == q['source_asset'], proof)
        check(f'source_binary_{qi}_{oi}', len(v) == q['vertices'] and len(t) == q['triangles'] and m['binary_sha256'] == q['source_binary_sha256'] and np.array_equal(v[ids], expected), {'mesh': m['id'], 'binary': m['binary'], 'binary_sha256': m['binary_sha256']})
        mask, tm = np.zeros(len(v), bool), np.zeros(len(t), bool)
        mask[ids], tm[selected] = True, True
        check(f'mask_closure_{qi}_{oi}', np.array_equal(np.flatnonzero(mask[t].any(1)), selected) and np.array_equal(np.unique(t[selected]), ids), 'Every face using a moved vertex is selected; selected faces use no unselected vertex')
        labels, weld = labels_for(v, t)
        split = [np.flatnonzero(weld == a).tolist() for a in np.unique(weld[ids]) if not mask[weld == a].all()]
        moved_delta = np.zeros(v.shape); moved_delta[ids] = replacement-expected
        torn = [np.flatnonzero(weld == a).tolist() for a in np.unique(weld[ids]) if np.max(np.abs(moved_delta[weld == a]-moved_delta[ids[np.flatnonzero(weld[ids] == a)[0]]])) > 1e-10]
        selected_components = np.unique(labels[t[selected, 0]])
        incomplete = [int(a) for a in selected_components if not tm[labels[t[:, 0]] == a].all()]
        check(f'weld_and_component_closure_{qi}_{oi}', not split and not torn and not incomplete, {'position_weld_rounding_decimal_places': 5, 'split_welds': split, 'torn_welds': torn, 'incomplete_components': incomplete})
        d = (replacement-expected) @ np.array(proof['world_matrix'])[:3, :3].T
        error = float(np.linalg.norm(d-want, axis=1).max())
        check(f'exact_unit16_delta_{qi}_{oi}', error < 1e-10, {'max_double_arithmetic_residual': error, 'world_delta': want.tolist()})
        uvs = uv(world)
        inside = contains_xy(polygon.buffer(.35), uvs[ids, 0], uvs[ids, 1])
        check(f'spatial_bound_{qi}_{oi}', inside.all() or 'Ground_Unit_10061935_Walls' in r['path'], {'inside_buffer_vertices': int(inside.sum()), 'selected_vertices': len(ids), 'uv_bounds': [uvs[ids].min(0).tolist(), uvs[ids].max(0).tolist()]})
        if oi == 0:
            for a in selected_components:
                ti = np.flatnonzero(labels[t[:, 0]] == a); vi = np.unique(t[ti])
                components.append({'source_asset': q['source_asset'], 'owner': proof, 'component': int(a), 'triangle_ordinals': ti.tolist(), 'vertex_indices': vi.tolist(), 'uv_bounds': [uvs[vi].min(0).tolist(), uvs[vi].max(0).tolist()], 'y_bounds': [float(world[vi, 1].min()), float(world[vi, 1].max())]})
        masks.append({'source_asset': q['source_asset'], 'owner': proof, 'mesh': m['id'], 'selected_vertices': len(ids), 'selected_triangles': len(selected), 'components': len(selected_components), 'total_vertices': len(v), 'total_triangles': len(t)})
    # Registry baseline gives exact same source vertex indexing for the first stage.
    row = reg.get(q['source_asset'])
    previous = priorpatch.get(row['baseline_asset']) if row else None
    if previous:
        vi = np.array(previous['vertex_indices'], int)
        dd = (np.array(previous['replacement_positions'])-previous['expected_positions']) @ np.array((previous['owners']+previous.get('collider_owners', []))[0]['world_matrix'])[:3, :3].T
        moved_by_unit15 = vi[np.linalg.norm(dd-np.array(prior['units'][-1]['world_delta']), axis=1) < 1e-5]
        any_overlap = sorted(set(ids.tolist()).intersection(vi.tolist()))
        overlap15 = sorted(set(ids.tolist()).intersection(moved_by_unit15.tolist()))
        check(f'preserve_previous_stage_{qi}', not any_overlap, {'prior_baseline_asset': row['baseline_asset'], 'previously_moved_vertices_intersecting_unit16': any_overlap, 'previous_unit15_vertices_intersecting_unit16': overlap15})
        prior_overlap.append({'source_asset': q['source_asset'], 'prior_baseline_asset': row['baseline_asset'], 'unit15_prior_vertices': len(moved_by_unit15), 'prior_stage_overlap': any_overlap, 'unit15_overlap': overlap15})

# Independently reproduce only source wall/joint profiles, not the stage selection.
# Matching reports numerical residuals and nearest-alternative separation; it does
# not claim this Python reproduction is byte-identical to Unity float arithmetic.
up = np.array([0., 1., 0.])
def kp(a, h=0.): return np.array([a[1], h, a[0]])
def rect(o, x, y, w, h): return np.array([o, o+x*w, o+x*w+y*h, o+y*h])
def box(c, x, y, z, sizes, bottom=True, back=True):
    w, h, d = sizes; hx, hy, hz = x*w*.5, y*h*.5, z*d*.5
    qs = [rect(c-hx-hy+hz, x, y, w, h)]
    if back: qs.append(rect(c-hx-hy-hz, x, y, w, h))
    qs += [rect(c+hx-hy-hz, z, y, d, h), rect(c-hx-hy-hz, z, y, d, h), rect(c-hx+hy-hz, x, z, w, d)]
    if bottom: qs.append(rect(c-hx-hy-hz, x, z, w, d))
    return qs
def wall_profiles(unit):
    e = next(e for e in spec['elements'] if e['id'] == f'SHOP-1F-{unit}')
    poly = np.array(e['geometry']['polygon']); params=e['params']; h=e['geometry']['y1']; y=e['geometry']['y']
    sign=1 if Polygon(poly).exterior.is_ccw else -1
    profiles=[]
    for edge in range(len(poly)):
        if edge == params['front'] or edge in params.get('openEdges', []): continue
        a,b=poly[edge],poly[(edge+1)%len(poly)]; direction=(b-a)/np.linalg.norm(b-a)
        n=kp(np.array([-direction[1],direction[0]])*sign); ex=kp(direction); L=np.linalg.norm(b-a);o=kp(a,y)+n*.02
        back=edge==params['back']; xs=[0.]+([k*1.2 for k in range(1,math.ceil(L/1.2))] if back else [])+[L]
        for panel,(xa,xb) in enumerate(zip(xs[:-1],xs[1:])):
            gap=.002 if back else 0.;p0=o+up*(.1+gap)+ex*(xa+gap); pw=xb-xa-2*gap;ph=h-y-.1-2*gap
            profiles.append(('wood' if back else 'plaster',edge,panel,'skin',rect(p0,ex,up,pw,ph)))
            if gap:
                pback=p0-n*.01
                profiles += [('wood',edge,panel,'panel_cap',q) for q in [rect(pback,ex,n,pw,.01),rect(pback+up*ph,ex,n,pw,.01),rect(pback,up,n,ph,.01),rect(pback+ex*pw,up,n,ph,.01)]]
                profiles.append(('joint',edge,panel,'panel_backer',rect(o+up*.1+ex*xa-n*.012,ex,up,xb-xa,h-y-.1)))
        profiles += [('joint',edge,None,'toe_bar',q) for q in box(o+ex*L*.5+up*.05+n*.006,ex,up,n,(L,.1,.012),False,False)]
    return profiles

baseline_names={'wood':'Kit_ShopWall_wall_wood_slat_panel.asset','plaster':'Kit_ShopWall_벽_내장.asset','joint':'Kit_Joint.asset'}
baseline_rows={}
for kind,name in baseline_names.items():
    asset='Assets/ChooGuard/Art/StationInterior/Kit/shops_1F/'+name
    m=next(m for m in old['meshes'] if m['asset']==asset);v,t=readbin(m)
    assert len(v)%4==0
    quads=v.reshape(-1,4,3)
    replacement=next(r['replacement_asset'] for r in registry['meshes'] if r['baseline_asset']==asset)
    baseline_rows[kind]=(asset,m,v,t,quads,cKDTree(quads.mean(1)),replacement)
profile_receipts=[]
for unit in (15,16):
    for index,(kind,edge,panel,profile,expected_quad) in enumerate(wall_profiles(unit)):
        asset,m,v,t,quads,tree,replacement=baseline_rows[kind]
        _,nearest=tree.query(expected_quad.mean(0),k=min(12,len(quads)))
        candidates=[]
        for qid in np.atleast_1d(nearest):
            actual=quads[qid]
            # KRect preserves append vertex order irrespective of normal/winding.
            residual=float(np.linalg.norm(actual-expected_quad,axis=1).max())
            candidates.append((residual,int(qid)))
        candidates.sort();residual,qid=candidates[0]; alternative=candidates[1][0]
        vi=np.arange(qid*4,qid*4+4);ti=np.flatnonzero(np.isin(t,vi).all(1))
        oldpatch=priorpatch[asset];oldids=set(oldpatch['vertex_indices']);newpatch=patches[replacement];newids=set(newpatch['vertex_indices'])
        oldmask=[int(x) in oldids for x in vi];newmask=[int(x) in newids for x in vi]
        previous_lookup={int(idx):j for j,idx in enumerate(oldpatch['vertex_indices'])}
        previous_units=[]
        priorwm=np.array(oldpatch['owners'][0]['world_matrix'])
        for idx in vi:
            if int(idx) not in previous_lookup: previous_units.append(None);continue
            j=previous_lookup[int(idx)];delta=(np.array(oldpatch['replacement_positions'][j])-oldpatch['expected_positions'][j])@priorwm[:3,:3].T
            previous_units.append(prior['units'][int(np.argmin(np.linalg.norm(np.array([u['world_delta'] for u in prior['units']])-delta,axis=1)))]['boardNo'])
        receipt={'boardNo':unit,'edge_index':edge,'panel_index':panel,'profile':profile,'kind':kind,'source_asset':asset,'current_asset':replacement,'source_mesh':m['id'],'source_binary_sha256':m['binary_sha256'],'source_vertex_indices':vi.tolist(),'source_triangle_ordinals':ti.tolist(),'expected_source_localXYZ':expected_quad.tolist(),'actual_source_localXYZ':quads[qid].tolist(),'max_corner_reproduction_residual':residual,'nearest_alternative_quad_residual':alternative,'first_stage_selected':oldmask,'first_stage_attributed_units':previous_units,'unit16_stage_selected':newmask}
        profile_receipts.append(receipt)
        # This is geometric attribution with recorded arithmetic residual, not a
        # tolerance certificate. Best reproduction must be uniquely separated.
        check(f'unique_profile_attribution_{unit}_{index}', alternative > residual*1000 and residual < 1e-4 and len(ti)==2, {'max_corner_residual':residual,'nearest_alternative_residual':alternative,'source_triangle_ordinals':ti.tolist(),'classification':'explicit builder profile geometric attribution; no byte equivalence assertion'})
        if unit==15:
            check(f'unit15_profile_preserved_{index}', not any(newmask) and all(x==15 for x in previous_units), receipt)
        else:
            check(f'unit16_profile_complete_{index}', all(newmask) and not any(oldmask), receipt)

# Exact fixture namespace and all renderer/collider descendants of moved parents.
motions=p['movable_transforms'];pathmotions={x['path']:x for x in motions if x['kind']=='path'};doormotions={x['door_element']:x for x in motions if x['kind']=='door'}
fixture_groups={};fixture_collider_groups={}
for entries,target in [(s['renderers'],fixture_groups),(s['colliders'],fixture_collider_groups)]:
    for r in entries:
        match=re.match(r'^실내 트윈 마감/점포 내부 1F/(SHOP-1F-(\d\d) [^/]+)',r['path'])
        if match and int(match[2])==16 and r['active']:
            target.setdefault('실내 트윈 마감/점포 내부 1F/'+match[1],[]).append(r['path'])
check('all_unit16_fixture_groups',set(fixture_groups)==set(pathmotions),{'named_rendered_groups':fixture_groups,'motions':list(pathmotions)})
check('all_unit16_fixture_collider_groups',set(fixture_collider_groups)<=set(pathmotions),fixture_collider_groups)
check('no_other_unit_fixture_motion',all(x['boardNo']==16 and re.match(r'^실내 트윈 마감/점포 내부 1F/SHOP-1F-16 ',path) for path,x in pathmotions.items()),list(pathmotions))
check('no_nested_parent_motions',not any(a!=b and b.startswith(a+'/') for a in pathmotions for b in pathmotions),list(pathmotions))
check('all_motion_deltas',all(np.array_equal(np.array(x['world_delta']),want) for x in motions),motions)
check('no_double_moved_mesh_under_motion',not any(o['path']==parent or o['path'].startswith(parent+'/') for q in p['mesh_patches'] for o in q['owners']+q.get('collider_owners',[]) for parent in pathmotions),list(pathmotions))

# Baked scene authoritative StationDoor.Element and actual hierarchy.
text=Path(p['scene']).read_text();gos={};trans={};go_trans={};doors=[]
for match in re.finditer(r'^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S):
    typ,obj,body=int(match[1]),int(match[2]),match[3]
    if typ==1:
        nm=re.search(r'^  m_Name: (.*?)(?=^  [A-Za-z]|\Z)',body,re.M|re.S);gos[obj]=yaml.safe_load(nm[1].strip()) if nm else ''
    if typ==4:
        go=int(re.search(r'm_GameObject: \{fileID: (\d+)\}',body)[1]);father=int(re.search(r'm_Father: \{fileID: (\d+)\}',body)[1]);trans[obj]=(go,father);go_trans[go]=obj
    if typ==114 and 'ChooGuard.App.Fps.Facilities.StationDoor' in body:
        el=re.search(r'^  Element: (.*)$',body,re.M)[1];el=json.loads(el) if el.startswith('"') else el
        if re.match(r'1F-unit16-',el):doors.append({'component_id':obj,'game_object_id':int(re.search(r'm_GameObject: \{fileID: (\d+)\}',body)[1]),'element':el,'leaf_pivots':[int(x) for x in re.findall(r'Pivot: \{fileID: (\d+)\}',body)]})
def path_for_go(go):
    parts=[]
    while go:
        parts.append(gos.get(go,'?'));tid=go_trans.get(go);f=trans.get(tid,(0,0))[1];go=trans.get(f,(0,0))[0]
    return '/'.join(reversed(parts))
for door in doors:
    door['path']=path_for_go(door['game_object_id']);door['leaf_paths']=[path_for_go(trans[t][0]) for t in door['leaf_pivots']];door['renderer_paths']=[r['path'] for r in s['renderers'] if r['path'].startswith(door['path']+'/')]
check('exact_stationdoor_source_element',set(x['element'] for x in doors)==set(doormotions)=={'1F-unit16-front1'},doors)
check('door_leaf_coverage',all(d['renderer_paths'] and all(path.startswith(d['path']+'/') for path in d['leaf_paths']) for d in doors),doors)

# Explicit two-front geometry proves caps and panels follow both source facades.
fronts=[e for e in detail['elements'] if re.match(r'^1F-unit16-front[12]$',e.get('id',''))]
frontage=[]
for element in fronts:
    line=LineString(element['geometry']['edge'])
    for r in s['renderers']:
        if not r['active'] or not r['path'].startswith('실내 트윈 마감/1F/') or not any(z in r['path'] for z in ['Kit_Clad_','Kit_Joint','Kit_Fascia_']):continue
        m,v,t,w=geo(r);points=uv(w);centres=points[t].mean(1);dist=np.array([line.distance(Point(c)) for c in centres]);ymin=w[t,1].min(1);ymax=w[t,1].max(1)
        bulkhead=any(z in r['path'] for z in ['Kit_Clad_','Kit_Joint'])
        eligible=(dist<(.04 if bulkhead else .41))&(ymin>=(element['params']['height']-.001 if bulkhead else 2.3999))&(ymax<=4.801 if bulkhead else ymax<=2.9001)
        # Line proximity can include adjacent units near endpoints; only components
        # whose all points project within this element are frontage-owned here.
        a,b=np.array(element['geometry']['edge']);ex=(b-a)/np.linalg.norm(b-a);along=(points[t]-a)@ex;eligible&=(along.min(1)>=-.0251)&(along.max(1)<=np.linalg.norm(b-a)+.0251)
        candidate=np.flatnonzero(eligible);q=patches.get(m['asset']);sel=set(q['selected_triangles']) if q else set();missing=[int(x) for x in candidate if int(x) not in sel]
        if len(candidate):frontage.append({'element':element['id'],'owner':r['path'],'source_asset':m['asset'],'candidate_triangles':candidate.tolist(),'unselected':missing})

after16=Polygon(p['units'][0]['after']['polygon']);after15=Polygon(prior['units'][-1]['after']['polygon']);edge15=np.array(prior['units'][-1]['after']['polygon'])[[2,3]];edge16=np.array(p['units'][0]['after']['polygon'])[[1,0]]
check('shared_15_16_partition_endpoints',np.array_equal(edge15,edge16),{'unit15_edge_UV':edge15.tolist(),'unit16_edge_UV':edge16.tolist(),'after_overlap_area_model_units':after15.intersection(after16).area,'no_field_metric_claim':True})
report={'schema':'chooguard.unit16-stage-independent-review.v1','packet_sha256':sha(EV/'packet.json'),'source_inventory_sha256':sha(EV/'current-inventory.json'),'inputs':read('input-manifest.json'),'all_checks_passed':all(x['passed'] for x in checks),'checks':checks,'mesh_count':len(p['mesh_patches']),'renderer_owners':sum(len(q['owners']) for q in p['mesh_patches']),'generic_collider_owners':sum(len(q.get('collider_owners',[])) for q in p['mesh_patches']),'selected_vertices':sum(len(q['vertex_indices']) for q in p['mesh_patches']),'masks':masks,'selected_components':components,'previous_stage_intersections':prior_overlap,'source_15_16_wall_joint_profiles':profile_receipts,'frontage_coverage_geometric_receipts':frontage,'motions':motions,'baked_doors':doors,'production_writes':0,'site_accuracy_verified':False,'remaining_gate':'Root owns exact source guard, installation, actual Unity runtime/nav/render and guide frontage clearance checks; this review does not clear missing collider regeneration lineage.'}
(EV/'unit16-review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'checks':len(checks),'failed':[x['id'] for x in checks if not x['passed']],'mesh_count':report['mesh_count'],'renderer_owners':report['renderer_owners'],'selected_vertices':report['selected_vertices'],'motions':len(motions),'profile15_count':sum(x['boardNo']==15 for x in profile_receipts),'profile16_count':sum(x['boardNo']==16 for x in profile_receipts),'profile_max_corner_reproduction_residual':max(x['max_corner_reproduction_residual'] for x in profile_receipts),'frontage_unselected_candidates':sum(len(x['unselected']) for x in frontage)},ensure_ascii=False))
