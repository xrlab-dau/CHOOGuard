"""Exact final-segment source face isolation and shortened reference replacement."""
exec(compile((__import__('pathlib').Path(__file__).parent/'check_guide_proposal.py').read_text(), 'check_guide_proposal.py','exec'))
from shapely.geometry import Point
a=np.array([-43.64,-9.42]);b=np.array([-42.6,-17.1]);ap=a.copy();bp=np.array([-42.6,-9.378])
direction=(b-a)/np.linalg.norm(b-a);normal=np.array([-direction[1],direction[0]])
newdirection=(bp-ap)/np.linalg.norm(bp-ap);newnormal=np.array([-newdirection[1],newdirection[0]])
batch=[]
for r in data['renderers']:
    if not r['path'].startswith('실내 트윈 마감/1F 공용/Kit_') or any(x in r['path'] for x in ['RoomWall','Glyph','Door','Screen','Sign','Light','Locker','Floor','Ceiling','Frame','Trim']):continue
    v,t=binary(r['mesh']);p=v[:,[2,0]]
    projected=(p-a)@direction;offset=(p-a)@normal
    faceids=[]
    for i,face in enumerate(t):
        if all(-.035 < projected[j] < np.linalg.norm(b-a)+.035 and abs(offset[j]) < .13 for j in face):faceids.append(i)
    if not faceids:continue
    # Keep first source profile samples, then supply a simple end-to-end box profile.
    # Sheet/joint/shadow end materials remain separate owners. Original recipes determine material.
    vertexids=sorted(set(int(j) for i in faceids for j in t[i]))
    o=offset[vertexids];ys=v[vertexids,1];n0=float(min(o));n1=float(max(o));y0=float(min(ys));y1=float(max(ys))
    pts=[]
    for yy in [y0,y1]:
        for endpoint in [ap,bp]:
            for off in [n0,n1]:
                uvp=endpoint+newnormal*off;pts.append([float(uvp[1]),yy,float(uvp[0])])
    # Box corners: y0 start0,start1,end0,end1 then y1 equivalents.
    tri=[[0,4,5],[0,5,1],[2,3,7],[2,7,6],[0,2,6],[0,6,4],[1,5,7],[1,7,3],[0,1,3],[0,3,2],[4,6,7],[4,7,5]]
    batch.append({'owner':identity(r),'old_segment_face_ordinals':faceids,
      'keep_source_face_ordinals':[i for i in range(len(t)) if i not in set(faceids)],
      'affected_vertex_indices':vertexids,'source_profile_normal_offsets':[n0,n1],'source_profile_y':[y0,y1],
      'replacement_box_localXYZ':pts,'replacement_box_triangles':tri,
      'replacement_note':'Reference stub box preserves this owner material and source thickness/height. For Joint owner, omit old joints in this removed segment instead of filling a solid strip; first retained neighboring joints remain.'})
    if '/Kit_Joint' in r['path']:
        batch[-1]['operation']='remove_last_segment_joints_no_replacement'
        batch[-1]['replacement_box_localXYZ']=[]
        batch[-1]['replacement_box_triangles']=[]
    else:
        batch[-1]['operation']='replace_last_segment_with_short_plain_clad_profile'
packet2={'schema':'chooguard.floor1-boh-final-segment-replacement.v1','source_segment_UV':[a.tolist(),b.tolist()],
 'proposed_segment_UV':[ap.tolist(),bp.tolist()],'old_length_model_units':float(np.linalg.norm(b-a)),
 'new_length_model_units':float(np.linalg.norm(bp-ap)),'reference_only':True,'metric_accuracy_verified':False,
 'preserve_previous_polyline_vertex':True,'preserve_Room_owner_899526584_and_all_room_faces':True,
 'selection_note':'Exact same segment parametrization, offset tolerance0.13 and endpoint0.035. Inspect per owner; replacement triangles are new faces, unrelated source face IDs remain unchanged.',
 'owners':batch,'public_collider_mesh':'mesh-3460',
 'collider_note':'Do not collapse whole public batch. Check segment-specific collider component; if none intersects old segment, preserve entire source public collider unchanged.'}
(OUT/'boh-segment-replacement.json').write_text(json.dumps(packet2,ensure_ascii=False,indent=2)+'\n')
# The public collider contains this final segment as a separate two-sided quad.
mid='mesh-3460';cv,ct=binary(mid);faceids=[12,13,14,15]
mapped=[]
for j in sorted(set(int(j) for i in faceids for j in ct[i])):
    uvp=cv[j,[2,0]];fraction=np.dot(uvp-a,direction)/np.linalg.norm(b-a)
    off=np.dot(uvp-a,normal);uvnew=ap+fraction*(bp-ap)+off*newnormal
    mapped.append({'index':j,'current_localXYZ':cv[j].tolist(),
      'proposed_localXYZ':[float(uvnew[1]),float(cv[j,1]),float(uvnew[0])],
      'segment_fraction':float(fraction),'profile_offset':float(off)})
packet2['collider_update']={'owner':next(c for c in data['colliders'] if c.get('mesh')==mid),
 'mesh_binary':meshes[mid]['binary'],'mesh_binary_sha256':meshes[mid]['binary_sha256'],
 'source_segment_faces':[{'face_ordinal':i,'indices':list(map(int,ct[i])), 'current_localXYZ':cv[ct[i]].tolist()} for i in faceids],
 'mapped_source_vertices':mapped,'keep_source_face_ordinals':[i for i in range(len(ct)) if i not in faceids],
 'topology':'Preserve these source collider face indices and map only endpoint vertices; no new triangles needed.'}
packet2['collider_note']='Exact old final-segment collider faces12..15 identified; update their source vertices with the same new endpoints and preserve all other collider faces.'
(OUT/'boh-segment-replacement.json').write_text(json.dumps(packet2,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'old_length':packet2['old_length_model_units'],'new_length':packet2['new_length_model_units'],'owners':[(x['owner']['mesh'],len(x['old_segment_face_ordinals'])) for x in batch]}))
