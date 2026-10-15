"""Test actual photographic landmark correspondence to existing scene geometry.

Existing model UV/vertices are model controls, NOT independent field controls.
No Unity mutation, invented point, metric certificate or production placement.
"""
from __future__ import annotations
import argparse, json, re, struct
from collections import Counter
from pathlib import Path
import cv2
import numpy as np
import structure_reconstruction as structure

ROOT=structure.ROOT
SOURCE=structure.SOURCE
EVIDENCE=structure.EVIDENCE/'registration'
INVENTORY=ROOT/'.planning/2026-10-08-interior-runtime-completion/architecture/current-scene-structure.json'
TARGETS=[
 {'id':'BOKSOONDOGA','owner_suffix':'/2F 본관/Kit_Lightbox_Kit_Tex_boksoondoga_fascia',
  'texture':'Assets/ChooGuard/Art/StationInterior/Majibang/Textures/MainRecovered/boksoondoga_fascia.png',
  'blocks':['retail-corridor','corridor-connector','information-corridor-static'],
  'identity_basis':'actual 복순도가 lettering, same recovered photographic fascia texture'},
 {'id':'KKOTDEUL','owner_suffix':'/2F 본관/Kit_Lightbox_Kit_Tex_kkotdeul_fascia',
  'texture':'Assets/ChooGuard/Art/StationInterior/Majibang/Textures/MainRecovered/kkotdeul_fascia.png',
  'blocks':['retail-corridor','corridor-connector','information-corridor-static'],
  'identity_basis':'actual 꽃들 logo/lettering, recovered photographic fascia texture'},
 {'id':'PASCUCCI','owner_suffix':'/2F 박스 점포/Kit_Lightbox_Kit_Tex_fascia_CAFFE_PASCUCCI',
  'texture':'Assets/ChooGuard/Art/StationInterior/Majibang/Textures/Ads/fascia_CAFFE_PASCUCCI.png',
  'blocks':['retail-corner'], 'identity_basis':'actual CAFFE PASCUCCI lettering, photographic fascia texture'},
 {'id':'STORYWAY','owner_suffix':'/2F 박스 점포/Box frame/Box_Kit_Lightbox_Kit_Tex_fascia_StoryWay',
  'texture':'Assets/ChooGuard/Art/StationInterior/Majibang/Textures/Ads/fascia_StoryWay.png',
  'blocks':['retail-corner'], 'minimum_texture_span_x':.20,
  'identity_basis':'actual StoryWay logo opposite PASCUCCI, box shop renderer (other StoryWays excluded); lettering occupies only middle third of retained strip'},
]


def parse_mesh_asset_uv(path, binary_path):
    """Read exact uncompressed UVs from serialized Unity Mesh, not assumed UV order."""
    text=path.read_text();part=text.split('m_VertexData:',1)[1].split('m_CompressedMesh:',1)[0]
    count=int(re.search(r'm_VertexCount: (\d+)',part)[1])
    channels=[tuple(int(x) for x in m) for m in re.findall(r'- stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)',part)]
    assert len(channels)>=5 and count==4
    assert all(s==0 and fmt==0 for s,off,fmt,dim in channels if dim)
    stream,offset,fmt,dim=channels[4];assert dim==2
    stride=max(off+dim*4 for s,off,fmt,dim in channels)
    data=bytes.fromhex(re.search(r'_typelessdata: ([0-9a-f]+)',part)[1]);assert len(data)==count*stride
    position=np.array([struct.unpack_from('<fff',data,i*stride+channels[0][1]) for i in range(count)])
    uv=np.array([struct.unpack_from('<ff',data,i*stride+offset) for i in range(count)])
    actual=binary_path.read_bytes();nv,ni,nn=struct.unpack_from('<iii',actual);assert nv==count
    binary_position=np.frombuffer(actual,'<f4',offset=12,count=nv*3).reshape(-1,3)
    assert np.array_equal(position,binary_position),'Serialized asset differs from actual sampled scene mesh'
    return position,uv


def audit_targets():
    inventory=json.loads(INVENTORY.read_text());meshes={m['id']:m for m in inventory['meshes']}
    audited=[]
    for source in TARGETS:
        matches=[r for r in inventory['renderers'] if r['path'].endswith(source['owner_suffix'])]
        assert len(matches)==1,(source['id'],len(matches))
        owner=matches[0];mesh=meshes[owner['mesh']]
        path=ROOT/mesh['asset'];binary=ROOT/mesh['binary'];assert structure.digest(binary)==mesh['binary_sha256']
        position,uv=parse_mesh_asset_uv(path,binary)
        matrix=np.array(owner['world_matrix']);world=(np.c_[position,np.ones(len(position))]@matrix.T)[:,:3]
        # UV → exact sampled current-world plane coordinates, including real renderer hierarchy.
        affine=np.linalg.lstsq(np.c_[uv,np.ones(len(uv))],world,rcond=None)[0]
        assert np.abs(np.c_[uv,np.ones(len(uv))]@affine-world).max()<1e-5
        material=ROOT/owner['materials'][0];texture=ROOT/source['texture']
        guid=re.search(r'guid: ([0-9a-f]+)',Path(str(texture)+'.meta').read_text())[1]
        assert guid in material.read_text(),'Target texture is not the sampled renderer material'
        image=cv2.imread(str(texture));assert image is not None
        audited.append(dict(source,owner=owner,mesh=mesh,source_asset_sha256=structure.digest(path),
             material_sha256=structure.digest(material),texture_sha256=structure.digest(texture),
             texture_width=image.shape[1],texture_height=image.shape[0],
             exact_local_vertices=position.tolist(),exact_uv=uv.tolist(),exact_world_vertices=world.tolist(),
             world_from_uv_affine=affine.tolist(),field_measurement=False))
    structure.write_json(EVIDENCE/'source-owner-uv-audit.json',{'schema':'chooguard.actual-model-control-owners.v1',
         'inventory':str(INVENTORY.relative_to(ROOT)),'inventory_sha256':structure.digest(INVENTORY),
         'scene_sampled_utc':inventory['sampled_utc'],'scene_snapshot_sha256':inventory['scene_file_sha256'],
         'site_accuracy_verified':False,'targets':audited})
    return audited


def image_matches(targets, step=3):
    cv2.setNumThreads(2);cv2.setRNGSeed(20261008)
    sift=cv2.SIFT_create(nfeatures=2200,contrastThreshold=.015)
    matcher=cv2.BFMatcher(cv2.NORM_L2)
    source_cache={};records=[]
    for target in targets:
        texture=cv2.imread(str(ROOT/target['texture']),cv2.IMREAD_GRAYSCALE)
        tkey,tdesc=sift.detectAndCompute(texture,None)
        print('TARGET',target['id'],'texture SIFT',len(tkey),flush=True)
        tested=[]
        for block in target['blocks']:
            poses=json.loads((SOURCE/block/'camera-poses.relative.json').read_text())['cameras']
            for pose in poses[::step]:
                path=ROOT/pose['undistorted_image'];key=(block,pose['image'])
                if key not in source_cache:
                    image=cv2.imread(str(path),cv2.IMREAD_GRAYSCALE);source_cache[key]=sift.detectAndCompute(image,None)
                skey,sdesc=source_cache[key]
                pairs=matcher.knnMatch(tdesc,sdesc,k=2) if tdesc is not None and sdesc is not None else []
                good=[a for a,b in pairs if a.distance<.76*b.distance]
                if len(good)<6:continue
                a=np.array([tkey[m.queryIdx].pt for m in good],np.float32)
                b=np.array([skey[m.trainIdx].pt for m in good],np.float32)
                homography,mask=cv2.findHomography(a,b,cv2.RANSAC,3.0,maxIters=3000,confidence=.999)
                if homography is None:continue
                inliers=mask.ravel().astype(bool);count=int(inliers.sum())
                if count<6:continue
                span=np.ptp(a[inliers],axis=0)/np.array([texture.shape[1],texture.shape[0]])
                projected=cv2.perspectiveTransform(a.reshape(1,-1,2),homography)[0]
                error=np.linalg.norm(projected-b,axis=1)
                # Broad enough appearance coverage to avoid matching only one repeated letter.
                valid=bool(count>=8 and span[0]>=target.get('minimum_texture_span_x',.35) and span[1]>=.20)
                hcorners=cv2.perspectiveTransform(np.array([[[0,0],[texture.shape[1]-1,0],
                      [texture.shape[1]-1,texture.shape[0]-1],[0,texture.shape[0]-1]]],np.float32),homography)[0]
                row={'target':target['id'],'block':block,'image':pose['image'],
                     'source_image':pose['undistorted_image'],'source_image_sha256':structure.digest(path),
                     'ratio_matches':len(good),'inlier_matches':count,'texture_span_fraction':span.tolist(),
                     'homography_rms_pixels':float(np.sqrt((error[inliers]**2).mean())),
                     'homography':homography.tolist(),'projected_texture_corners':hcorners.tolist(),
                     'valid_appearance_support':valid,
                     'inliers':[{'texture_feature_index':int(m.queryIdx),'texture_pixel':a[i].tolist(),
                          'observed_pixel':b[i].tolist(),'sift_distance':float(m.distance)}
                           for i,m in enumerate(good) if inliers[i]]}
                tested.append(row)
        tested.sort(key=lambda r:(r['valid_appearance_support'],r['inlier_matches'],-r['homography_rms_pixels']),reverse=True)
        best=tested[:6]
        for i,row in enumerate(best):
            image=cv2.imread(str(ROOT/row['source_image']));quad=np.array(row['projected_texture_corners'],dtype=np.int32)
            cv2.polylines(image,[quad],True,(0,255,255),2)
            for match in row['inliers']:cv2.circle(image,tuple(np.rint(match['observed_pixel']).astype(int)),3,(0,0,255),-1)
            cv2.putText(image,f"{target['id']} / {row['inlier_matches']} inliers / {'candidate' if row['valid_appearance_support'] else 'insufficient'}",(8,25),cv2.FONT_HERSHEY_SIMPLEX,.6,(255,255,0),2)
            destination=EVIDENCE/f"{target['id'].lower()}-{row['block']}-{row['image'][:-4]}-appearance.jpg"
            cv2.imwrite(str(destination),image,[cv2.IMWRITE_JPEG_QUALITY,94]);row['appearance_overlay']=str(destination.relative_to(ROOT))
        records.append({'target':target['id'],'texture_feature_count':len(tkey),'tested_match_candidates':len(tested),
                        'valid_appearance_candidates':sum(r['valid_appearance_support'] for r in tested),'best_candidates':best})
        print('MATCH',target['id'],[(r['block'],r['image'],r['inlier_matches'],r['valid_appearance_support']) for r in best],flush=True)
    structure.write_json(EVIDENCE/'photographic-feature-correspondences.json',{'schema':'chooguard.photographic-model-feature-matches.v1',
             'role':'reference_only','field_measurements':False,'method':'SIFT ratio .76 + planar RANSAC 3px; target real photographic texture',
             'targets':records})
    return records


def relative_matrix(block):
    if block in ['corridor-connector','retail-corner']:
        return np.eye(4)
    proof=json.loads((structure.EVIDENCE.parent/'interior-blocks-20261008/corridor-relative-alignment.json').read_text())
    row=next(x for x in proof['comparisons'] if x['source_block']==block)
    assert row['status']=='relative_overlap_aligned'
    return np.array(row['target_from_source_relative_matrix'])


def transform(matrix,points):
    return (np.c_[np.atleast_2d(points),np.ones(len(np.atleast_2d(points)))]@matrix.T)[:,:3]


def observed_controls(targets,matches):
    cache={};groups={};observations=[]
    for row in matches:
        target=next(t for t in targets if t['id']==row['target'])
        affine=np.array(target['world_from_uv_affine'])
        for candidate in row['best_candidates']:
            if not candidate['valid_appearance_support']:continue
            block=candidate['block']
            if block not in cache:
                v,uv,f,tf=structure.load_obj(SOURCE/block/'reference_observed_surfaces.obj')
                poses=json.loads((SOURCE/block/'camera-poses.relative.json').read_text())['cameras']
                cache[block]=(v,f,{p['image']:p for p in poses})
            v,f,poses=cache[block];pose=poses[candidate['image']];matrix=relative_matrix(block)
            input_hash=structure.digest(SOURCE/block/'images'/candidate['image'])
            for m in candidate['inliers']:
                hit=structure.ray_surface(v,f,pose,m['observed_pixel'])
                texture=np.array(m['texture_pixel'])
                uv=np.array([texture[0]/(target['texture_width']-1),1-texture[1]/(target['texture_height']-1)])
                target_world=np.r_[uv,1]@affine
                local=transform(matrix,hit['position_relative'])[0].tolist() if hit else None
                record={'owner':target['id'],'block':block,'image':candidate['image'],
                     'observed_pixel':m['observed_pixel'],'texture_pixel':m['texture_pixel'],
                     'texture_feature_index':m['texture_feature_index'],'source_triangle_ray':hit,
                     'nearest_mesh_ray_point_common_frame':local,'target_current_model_world':target_world.tolist(),
                     'original_source_frame_sha256':input_hash,'source_camera':pose,
                     'common_from_source_matrix':matrix.tolist(),'field_measurement':False}
                observations.append(record)
                key=(target['id'],tuple(np.round(texture,3)))
                groups.setdefault(key,[]).append(record)
    controls=[]
    for key,items in groups.items():
        # Camera models differ across overlapping reconstructions; identical source image is one view.
        distinct={r['original_source_frame_sha256']:r for r in items}
        if len(distinct)<2:continue
        common_poses={};annotations=[]
        for i,r in enumerate(distinct.values()):
            name=str(i);pose=dict(r['source_camera'])
            wc=np.array(r['common_from_source_matrix'])@np.vstack([np.array(pose['world_from_camera_cv']),[0,0,0,1]])
            pose['world_from_camera_cv']=wc[:3].tolist();common_poses[name]=pose
            annotations.append((name,r['observed_pixel']))
        centre,reprojection=triangulate_annotations(common_poses,annotations)
        origins=np.array([np.array(p['world_from_camera_cv'])[:,3] for p in common_poses.values()])
        directions=centre-origins;directions/=np.linalg.norm(directions,axis=1)[:,None]
        parallax=float(np.rad2deg(np.arccos(np.clip(directions@directions.T,-1,1))).max())
        actual_rays=np.array([r['nearest_mesh_ray_point_common_frame'] for r in distinct.values() if r['nearest_mesh_ray_point_common_frame'] is not None])
        disagreement=float(np.linalg.norm(actual_rays-centre,axis=1).max()) if len(actual_rays) else None
        eligible=max(r['error_pixels'] for r in reprojection)<=3 and parallax>=1
        controls.append({'id':key[0]+'-feature-'+str(items[0]['texture_feature_index']),
                         'owner':key[0],'kind':'same_photographed_feature_multi_camera_triangulation',
                         'local_point':centre.tolist(),'target_model_world':items[0]['target_current_model_world'],
                         'unique_observed_source_images':len(distinct),'actual_feature_triangulation_reprojection':reprojection,
                         'maximum_camera_ray_parallax_deg':parallax,'nearest_mesh_ray_max_disagreement_relative':disagreement,
                         'nearest_mesh_ray_used_as_feature_geometry':False,'fit_eligible':bool(eligible),
                         'control_status':'photo_feature_triangulated' if eligible else 'rejected_low_parallax_or_reprojection_control',
                         'observations':list(distinct.values()),'field_measurement':False})
    structure.write_json(EVIDENCE/'actual-local-model-controls.json',{'schema':'chooguard.actual-photographic-model-controls.v1',
              'role':'reference_only','field_measurements':False,
              'geometry_basis':'Direct calibrated multi-camera triangulation of same photographed glyph features. Nearest retained mesh ray intersections are diagnostic comparisons only; a ray may hit behind a hole.',
              'photographic_feature_observations':observations,'aggregated_controls':controls})
    return controls


def project(pose,point):
    wc=np.vstack([np.array(pose['world_from_camera_cv']),[0,0,0,1]])
    camera=np.linalg.solve(wc,np.r_[point,1])[:3]
    k=np.array(pose['calibration_matrix']);pixel=k@camera
    return pixel[:2]/pixel[2],float(camera[2])


def triangulate_annotations(poses,annotations):
    system=[]
    for name,pixel in annotations:
        pose=poses[name];wc=np.vstack([np.array(pose['world_from_camera_cv']),[0,0,0,1]])
        p=np.array(pose['calibration_matrix'])@np.linalg.inv(wc)[:3]
        system.extend([pixel[0]*p[2]-p[0],pixel[1]*p[2]-p[1]])
    _,_,v=np.linalg.svd(np.array(system));point=v[-1,:3]/v[-1,3]
    errors=[]
    for name,pixel in annotations:
        predicted,depth=project(poses[name],point)
        errors.append({'image':name,'pixel':pixel,'reprojected_pixel':predicted.tolist(),
                       'error_pixels':float(np.linalg.norm(predicted-pixel)),'depth_relative':depth})
    assert all(r['depth_relative']>0 for r in errors)
    return point,errors


def column_corner_controls():
    inventory=json.loads(INVENTORY.read_text());owner=next(r for r in inventory['renderers'] if r['path']=='실내 트윈 마감/2F 본관 보강/Kit_Trim_trim_stainless_hairline')
    mesh=next(m for m in inventory['meshes'] if m['id']==owner['mesh']);data=(ROOT/mesh['binary']).read_bytes()
    count,ni,nn=struct.unpack_from('<iii',data);vertices=np.frombuffer(data,'<f4',offset=12,count=count*3).reshape(-1,3)
    assert structure.digest(ROOT/mesh['binary'])==mesh['binary_sha256']
    poses={p['image']:p for p in json.loads((SOURCE/'retail-corridor/camera-poses.relative.json').read_text())['cameras']}
    # Exact observed skirting-footprint corner identity, not logos standing in for corners.
    annotations=[
       ('CAFE-STONE-NEAR-FOOT',5,[('000001.jpg',[519,477]),('000002.jpg',[591,498]),('000003.jpg',[695,527])]),
       ('CAFE-STONE-SIDE-FOOT',4,[('000001.jpg',[401,430]),('000002.jpg',[456,443]),('000003.jpg',[520,464])]),
       ('CAFE-STONE-FRONT-FAR-FOOT',1,[('000001.jpg',[753,443]),('000002.jpg',[846,456])]),
    ]
    records=[];rng=np.random.default_rng(20261008);common=relative_matrix('retail-corridor')
    for name,index,pixels in annotations:
        local,errors=triangulate_annotations(poses,pixels)
        jitter=[]
        for _ in range(200):
            changed=[(image,(np.array(pixel)+rng.normal(0,2,len(pixel))).tolist()) for image,pixel in pixels]
            changed_point,_=triangulate_annotations(poses,changed);jitter.append(np.linalg.norm(changed_point-local))
        world=transform(np.array(owner['world_matrix']),vertices[index])[0]
        record={'id':name,'owner':'CAFE-CRAFT-STONE','kind':'manual_multi_camera_corner_triangulation',
            'local_point':transform(common,local)[0].tolist(),'target_model_world':world.tolist(),
            'target_source_owner':owner,'target_mesh':mesh,'target_vertex_index':index,
            'target_mesh_local_vertex':vertices[index].tolist(),
            'identity_basis':'same stone-column visible base/skirting corner in retained cameras and actual current mesh vertex',
            'original_block_local_point':local.tolist(),'annotations_and_reprojection':errors,
            'annotation_assumed_sigma_pixels':2,'jitter_p95_distance_relative':float(np.percentile(jitter,95)),
            'field_measurement':False,'observations':[{'block':'retail-corridor','image':image,'observed_pixel':pixel,
                'source_camera':poses[image],'common_from_source_matrix':common.tolist(),
                'source_image_sha256':structure.digest(ROOT/poses[image]['undistorted_image'])} for image,pixel in pixels]}
        record['fit_eligible']=max(r['error_pixels'] for r in errors)<=3
        record['control_status']='provisional_matched_corner_for_model_fit' if record['fit_eligible'] else 'rejected_annotation_camera_pose_disagreement'
        record['rejection_reason']=None if record['fit_eligible'] else 'Maximum triangulation reprojection exceeds diagnostic 3px annotation gate.'
        records.append(record)
    for image in sorted({p[0] for _,_,pairs in annotations for p in pairs}):
        source=ROOT/poses[image]['undistorted_image'];picture=cv2.imread(str(source))
        for name,index,pairs in annotations:
            for frame,pixel in pairs:
                if frame==image:
                    cv2.drawMarker(picture,tuple(pixel),(0,0,255),cv2.MARKER_CROSS,16,2)
                    cv2.putText(picture,name.replace('CAFE-STONE-',''),(pixel[0]-70,pixel[1]-14),cv2.FONT_HERSHEY_SIMPLEX,.36,(0,255,255),1)
        cv2.imwrite(str(EVIDENCE/(image[:-4]+'-actual-column-corners.jpg')),picture,[cv2.IMWRITE_JPEG_QUALITY,95])
    structure.write_json(EVIDENCE/'column-matched-corner-controls.json',{'schema':'chooguard.actual-column-corner-controls.v1',
             'role':'reference_only','field_measurements':False,'controls':records})
    return records


def fit_similarity(controls,handedness=-1):
    x=np.array([r['local_point'] for r in controls]);y=np.array([r['target_model_world'] for r in controls])
    owners=[r['owner'] for r in controls];weights=np.array([1/owners.count(owner) for owner in owners]);weights/=weights.sum()
    xc=np.average(x,axis=0,weights=weights);yc=np.average(y,axis=0,weights=weights)
    a=x-xc;b=y-yc;cov=(b.T*weights)@a
    u,s,vt=np.linalg.svd(cov);flip=np.ones(3)
    flip[-1]=handedness*np.linalg.det(u@vt)
    q=u@np.diag(flip)@vt
    scale=float((s*flip).sum()/np.sum(weights*np.sum(a*a,axis=1)))
    matrix=np.eye(4);matrix[:3,:3]=q*scale;matrix[:3,3]=yc-scale*q@xc
    residual=np.linalg.norm(transform(matrix,x)-y,axis=1)
    return matrix,{'scale_model_units_per_relative_unit':scale,'orthogonal_determinant':float(np.linalg.det(q)),
          'source_control_rank':int(np.linalg.matrix_rank(a,tol=1e-6)),'target_control_rank':int(np.linalg.matrix_rank(b,tol=1e-6)),
          'source_singular_values':np.linalg.svd(a,compute_uv=False).tolist(),
          'owner_balanced_rms_model_units':float(np.sqrt(np.sum(weights*residual**2))),
          'median_model_units':float(np.median(residual)),'max_model_units':float(residual.max()),
          'controls':len(controls),'owners':sorted(set(owners))}


def errors_for_controls(matrix,controls):
    inverse=np.linalg.inv(matrix);rows=[]
    for control in controls:
        predicted=transform(matrix,control['local_point'])[0]
        back=transform(inverse,control['target_model_world'])[0]
        reprojection=[]
        for obs in control['observations']:
            source_point=transform(np.linalg.inv(np.array(obs['common_from_source_matrix'])),back)[0]
            pixel,depth=project(obs['source_camera'],source_point)
            reprojection.append({'block':obs['block'],'image':obs['image'],'observed_pixel':obs['observed_pixel'],
                  'predicted_model_pixel':pixel.tolist(),'error_pixels':float(np.linalg.norm(pixel-obs['observed_pixel'])),'depth_relative':depth})
        rows.append({'control':control['id'],'owner':control['owner'],
                     'residual_model_units':float(np.linalg.norm(predicted-control['target_model_world'])),
                     'reprojected_observations':reprojection})
    flat=[p['error_pixels'] for r in rows for p in r['reprojected_observations']]
    return {'point_residual_median_model_units':float(np.median([r['residual_model_units'] for r in rows])) if rows else None,
            'reprojection_median_pixels':float(np.median(flat)) if flat else None,
            'reprojection_p95_pixels':float(np.percentile(flat,95)) if flat else None,'controls':rows}


def plane_disagreement(matrix,controls,targets):
    rows=[]
    for owner in sorted(set(r['owner'] for r in controls)):
        points=np.array([r['local_point'] for r in controls if r['owner']==owner])
        if len(points)<3:continue
        normal,offset,centre,eigenvalues=structure.plane_fit(points,np.ones(len(points)))
        target=next((t for t in targets if t['id']==owner),None)
        if target is None:continue
        uvmap=np.array(target['world_from_uv_affine']);model_normal=np.cross(uvmap[0],uvmap[1]);model_normal/=np.linalg.norm(model_normal)
        model_offset=-model_normal@uvmap[2]
        mapped_normal=np.linalg.solve(matrix[:3,:3].T,normal);mapped_normal/=np.linalg.norm(mapped_normal)
        mapped=transform(matrix,points)
        votes=Counter();candidate_planes={}
        for c in controls:
            if c['owner']!=owner:continue
            for obs in c['observations']:
                if not obs.get('source_triangle_ray'):continue
                block=obs['block'];directory=structure.OUTPUT/block
                if block not in candidate_planes:
                    pjson=json.loads((directory/'structural-planes.relative.json').read_text())
                    candidate_planes[block]=[(p,set(json.loads((directory/(p['id']+'.source-face-ids.json')).read_text())['face_indices_zero_based'])) for p in pjson['planes']]
                for p,ids in candidate_planes[block]:
                    if obs['source_triangle_ray']['source_face_index_zero_based'] in ids:votes[(block,p['id'])]+=1
        structural=None
        if votes:
            (block,pid),count=votes.most_common(1)[0]
            plane=next(p for p,ids in candidate_planes[block] if p['id']==pid)
            eq=plane['equation_relative'];source_equation=np.r_[eq['normal'],eq['offset']]
            common=np.linalg.inv(relative_matrix(block)).T@source_equation
            model_equation=np.linalg.inv(matrix).T@common
            mesh_plane_mapped_normal=model_equation[:3]/np.linalg.norm(model_equation[:3])
            structural={'block':block,'plane_id':pid,'actual_ray_face_vote_count':count,
                    'actual_plane_observed_triangles':plane['observed_triangle_count'],
                    'mapped_normal':mesh_plane_mapped_normal.tolist(),'angle_to_exact_current_quad_deg':structure.angle_degrees(mesh_plane_mapped_normal,model_normal),
                    'feature_surface_correspondence_verified':False,
                    'basis':'Diagnostic retained-mesh plane selected by nearest ray face. Rays can pass through missing fascia surfaces and hit a background plane; this does not identify the physical feature surface.'}
        rows.append({'owner':owner,'observed_control_plane_normal_relative':normal.tolist(),
            'observed_control_plane_offset_relative':float(offset),'current_model_quad_normal':model_normal.tolist(),
            'mapped_plane_normal':mapped_normal.tolist(),'normal_angle_disagreement_deg':structure.angle_degrees(mapped_normal,model_normal),
            'mapped_observation_to_model_plane_rms_model_units':float(np.sqrt(np.mean((mapped@model_normal+model_offset)**2))),
            'feature_point_plane_eigenvalues_relative_squared':eigenvalues.tolist(),
            'sparse_feature_plane_orientation_is_conditioned_on_small_letter_extent':True,
            'matched_observed_mesh_plane':structural,
            'plane_basis':'Direct multi-camera triangulated photographed feature points vs exact current scene authored photographic UV quad; narrow letter extent/low parallax can make feature-plane orientation weak.',
            'field_measurement':False})
    return rows


def builder_provenance():
    builder=ROOT/'AgentScripts/MajibangBuilder.cs';spec=ROOT/'.planning/2026-09-24-interior-twin/specs/2f-main.json'
    elements=json.loads(spec.read_text())['elements']
    main_receipt=ROOT/'Assets/ChooGuard/Art/StationInterior/Majibang/Textures/MainRecovered/receipts.json'
    ads_receipt=ROOT/'Assets/ChooGuard/Art/StationInterior/Majibang/Textures/Ads/receipts.json'
    m=json.loads(main_receipt.read_text());a=json.loads(ads_receipt.read_text())
    provenance={'schema':'chooguard.model-appearance-control-provenance.v1',
       'builder':str(builder.relative_to(ROOT)),'builder_sha256':structure.digest(builder),
       'current_quad_builder':{'method':'KitStorefront','method_line':4055,'fascia_lines':[4130,4142],
           'span_rule':'min(signWidth or edgeLength-2*inset, edgeLength-2*inset)',
           'height_rule':'fascia.height-2*inset','default_inset':.06,
           'texture_mapped_to_full_authored_quad':True},
       'legacy_aspect_preserving_path':{'method':'PhotoFascia','line':1188,
           'height_rule':'min(.62,width*.92*sourceHeight/sourceWidth)',
           'span_rule':'height*sourceWidth/sourceHeight',
           'current_audited_owners_are_Kit_Lightbox_not_this_legacy_path':True},
       'source_photo_aspect_policy':m['aspectPolicy'],
       'source_photo_policy_sha256':structure.digest(main_receipt),
       'parameters':[{'id':e['id'],'spec':str(spec.relative_to(ROOT)),'spec_sha256':structure.digest(spec),
             'geometry':e['geometry'],'params':e['params']} for e in elements
             if any(s in str(e) for s in ['boksoondoga_fascia','kkotdeul_fascia'])],
       'main_source_photo_receipts':[e for e in m['items'] if e['shopName'] in ['복순도가','꽃들']],
       'ads_source_photo_receipts':[e for e in a if isinstance(e,dict) and e.get('texture','').endswith(('fascia_CAFFE_PASCUCCI.png','fascia_StoryWay.png'))],
       'ads_source_receipt_sha256':structure.digest(ads_receipt),
       'interpretation':'Same photographed glyphs establish rendered-model appearance controls. Perspective/visual rectification, authored quad dimensions and historical placement can prevent rigid/isotropic 3D registration. Texture aspect ratio alone does not establish physical sign width/height or prove architecture dimensions wrong.',
       'field_measurements':False}
    structure.write_json(EVIDENCE/'builder-photo-control-provenance.json',provenance)
    return provenance


def unregistered_extent_audit():
    inventory=json.loads(INVENTORY.read_text());meshes={r['id']:r for r in inventory['meshes']};records=[]
    for block,selector,reason in [
      ('food-court',lambda r:'FoodCourt' in r['path'],
       'Seven actual FoodCourt renderers were inspected. The retained camera shows interior exposed ceiling/counters; source owners contain generic room/front/glass/sign batches without a uniquely matched photographed vertex/UV corner. Region-name identity does not determine a 3D point.'),
      ('ticketing-wall',lambda r:r['path'].startswith('실내 트윈 마감/2F 본관 보강/') and any(t in r['path'] for t in ['Kit_Clad_Kit_Paint_E6E5E0','board_Departure_green','Kit_Board_Canopy_Dark']),
       'Actual ticket-wall cladding and departure-board owners inspected. The repeated green board batch has four texture quads; unique panel identity/corner observation not established. One wall plane alone leaves in-plane translation/full Sim3 underconstrained.')]:
        owners=[dict(r,mesh_metadata=meshes[r['mesh']]) for r in inventory['renderers'] if selector(r)]
        for owner in owners:assert structure.digest(ROOT/owner['mesh_metadata']['binary'])==owner['mesh_metadata']['binary_sha256']
        source=json.loads((structure.OUTPUT/block/'structural-planes.relative.json').read_text())
        records.append({'block':block,'status':'examined_existing_model_but_no_unique_numeric_control',
                        'reason':reason,'actual_owner_count':len(owners),'actual_model_owners':owners,
                        'actual_relative_source_planes':len(source['planes']),
                        'retained_source_camera_count':source['input']['retained_camera_count'],
                        'numeric_fit_forced':False,'field_measurements':False})
    structure.write_json(EVIDENCE/'unregistered-block-model-extents.json',{'schema':'chooguard.unregistered-model-extent-audit.v1',
        'inventory_sha256':structure.digest(INVENTORY),'blocks':records})
    return records


def export_diagnostic_geometry(name,matrix,controls):
    x=np.array([r['local_point'] for r in controls]);y=np.array([r['target_model_world'] for r in controls]);fitted=transform(matrix,x)
    directory=EVIDENCE/'geometry';directory.mkdir(exist_ok=True)
    structure.write_cloud(directory/(name+'.actual-local-controls.ply'),x,np.tile([20,220,80],(len(x),1)))
    structure.write_cloud(directory/(name+'.existing-model-controls.ply'),y,np.tile([230,50,50],(len(y),1)))
    structure.write_cloud(directory/(name+'.fitted-controls.model-world.ply'),fitted,np.tile([30,80,230],(len(y),1)))
    path=directory/(name+'.residual-lines.model-world.obj')
    with path.open('w') as stream:
        stream.write('# diagnostic rejected registration residuals; not architectural objects\n')
        for point in np.vstack([y,fitted]):stream.write('v '+' '.join(f'{v:.9g}' for v in point)+'\n')
        for i in range(len(y)):stream.write(f'l {i+1} {len(y)+i+1}\n')
    return str(path.relative_to(ROOT))


def save_fit_overlays(name,matrix,controls):
    groups={}
    for row in errors_for_controls(matrix,controls)['controls']:
        for obs in row['reprojected_observations']:groups.setdefault((obs['block'],obs['image']),[]).append(obs)
    saved=[]
    for (block,image),observations in list(groups.items())[:12]:
        pose=next(p for p in json.loads((SOURCE/block/'camera-poses.relative.json').read_text())['cameras'] if p['image']==image)
        picture=cv2.imread(str(ROOT/pose['undistorted_image']))
        for obs in observations:
            a=np.rint(obs['observed_pixel']).astype(int);b=np.rint(obs['predicted_model_pixel']).astype(int)
            if np.abs(b).max()>10000 or obs['depth_relative']<=0:continue
            cv2.circle(picture,tuple(a),3,(0,255,0),-1);cv2.drawMarker(picture,tuple(b),(0,0,255),cv2.MARKER_TILTED_CROSS,9,1)
            cv2.line(picture,tuple(a),tuple(b),(0,255,255),1)
        cv2.putText(picture,name+' / observed green, model predicted red',(8,25),cv2.FONT_HERSHEY_SIMPLEX,.46,(255,255,0),1)
        path=EVIDENCE/(name+'-'+block+'-'+image[:-4]+'-fit-overlay.jpg');cv2.imwrite(str(path),picture,[cv2.IMWRITE_JPEG_QUALITY,95]);saved.append(str(path.relative_to(ROOT)))
    return saved


def fit_and_evaluate():
    targets=json.loads((EVIDENCE/'source-owner-uv-audit.json').read_text())['targets']
    matches=json.loads((EVIDENCE/'photographic-feature-correspondences.json').read_text())['targets']
    controls=observed_controls(targets,matches);columns=column_corner_controls();trials=[]
    for name,owners in [('retail-corner',['PASCUCCI','STORYWAY']),('corridor',['BOKSOONDOGA','CAFE-CRAFT-STONE'])]:
        selected=[r for r in controls+columns if r['owner'] in owners and r.get('fit_eligible',True)]
        if len(selected)<4 or len(set(r['owner'] for r in selected))<2:
            trials.append({'frame':name,'status':'insufficient_actual_correspondences','available_controls':len(selected),'owners':sorted(set(r['owner'] for r in selected))});continue
        for handedness in [-1,1]:
            matrix,stats=fit_similarity(selected,handedness)
            full=errors_for_controls(matrix,selected)
            train=[r for i,r in enumerate(selected) if i%4!=0];held=[r for i,r in enumerate(selected) if i%4==0]
            hmatrix,hstats=fit_similarity(train,handedness);withheld=errors_for_controls(hmatrix,held)
            leaveout=[]
            for owner in owners:
                fit=[r for r in selected if r['owner']!=owner];test=[r for r in selected if r['owner']==owner]
                om,ostats=fit_similarity(fit,handedness)
                leaveout.append({'withheld_owner':owner,'training_stats':ostats,'test':errors_for_controls(om,test),
                                 'model_world_from_relative_matrix':om.tolist(),
                                 'training_geometrically_underconstrained':ostats['source_control_rank']<2})
            extent=float(np.linalg.norm(np.ptp(np.array([r['target_model_world'] for r in selected]),axis=0)))
            up_source=np.array(json.loads((structure.OUTPUT/('retail-corner' if name=='retail-corner' else 'corridor-connector')/'structural-planes.relative.json').read_text())['orientation']['up_relative'])
            up=matrix[:3,:3]@up_source;up/=np.linalg.norm(up)
            ratios=[]
            for owner in owners:
                own=[r for r in selected if r['owner']==owner];_,single=fit_similarity(own,handedness);ratios.append(single['scale_model_units_per_relative_unit'])
            scale_ratio=max(ratios)/min(ratios)
            reasons=[]
            if stats['owner_balanced_rms_model_units']/extent>.025:reasons.append('model_control_fit_residual_exceeds_2point5percent_extent')
            if withheld['reprojection_median_pixels'] is None or withheld['reprojection_median_pixels']>10:reasons.append('withheld_texture_feature_reprojection_exceeds_10px')
            if up[1]<.95:reasons.append('mapped_camera_up_disagrees_with_existing_model_vertical')
            if scale_ratio>1.25:reasons.append('independent_landmark_owners_require_incompatible_isotropic_scales')
            if any(not l['training_geometrically_underconstrained'] and l['test']['point_residual_median_model_units']/extent>.10 for l in leaveout):reasons.append('withheld_owner_location_disagrees_over_10percent_extent')
            if stats['source_control_rank']<3:reasons.append('all_controls_have_insufficient_spatial_rank')
            trial={'frame':name,'handedness':handedness,'status':'rejected_estimated_model_registration' if reasons else 'estimated_model_registration_for_review',
                'model_world_from_relative_matrix':matrix.tolist(),'field_measurements':False,'metric_verified':False,'production_placement_allowed':False,
                'coordinate_handedness_note':'Both orthogonal parities tested; Unity model/world may require coordinate parity conversion from COLMAP.',
                'fit':stats,'full_reprojection':full,'held_out_feature_test':{'train':hstats,'test':withheld,'test_features':len(held)},
                'leave_one_owner_out':leaveout,'target_landmark_extent_model_units':extent,'mapped_camera_up':up.tolist(),
                'owner_independent_fit_scale_ratio':scale_ratio,'rejection_reasons':reasons,
                'observed_current_model_plane_disagreement':plane_disagreement(matrix,selected,targets),
                'source_camera_fit_overlays':save_fit_overlays(name+('-parity-minus' if handedness<0 else '-parity-plus'),matrix,selected)}
            trial['actual_control_diagnostic_geometry']=export_diagnostic_geometry(name+('-parity-minus' if handedness<0 else '-parity-plus'),matrix,selected)
            trials.append(trial)
            print('REGISTRATION',name,handedness,trial['status'],'RMS',round(stats['owner_balanced_rms_model_units'],4),'heldpix',round(withheld['reprojection_median_pixels'],1),'scaleratio',round(scale_ratio,3),flush=True)
    aspect=[]
    for target in targets:
        a=np.array(target['world_from_uv_affine']);model_aspect=np.linalg.norm(a[0])/np.linalg.norm(a[1]);image_aspect=target['texture_width']/target['texture_height']
        aspect.append({'owner':target['id'],'texture_aspect':image_aspect,'current_model_quad_aspect':float(model_aspect),
                       'model_texture_aspect_stretch_ratio':float(model_aspect/image_aspect),'field_measurement':False,
                       'physical_sign_aspect_ratio_not_established':True})
    result={'schema':'chooguard.tested-model-structural-registration.v1','role':'reference_only','metric_verified':False,
             'production_placement_allowed':False,'actual_model_owners_audited':len(targets),'field_control_points':0,
             'actual_feature_control_points':len(controls),'actual_multi_camera_column_corner_points':len(columns),
             'eligible_direct_feature_controls':sum(r.get('fit_eligible',True) for r in controls),
             'excluded_corner_controls':[{'id':r['id'],'reason':r['rejection_reason']} for r in columns if not r['fit_eligible']],
             'acceptance_gate_note':'Conservative model-review diagnostic gates chosen for this investigation, not a physical accuracy specification.',
             'photo_control_limitation':'Direct triangulated controls map matched photographic glyphs into authored model UV quads; original perspective/visual rectification and authored display scale are not independently calibrated site geometry. Nearest mesh rays may hit wrong background surfaces and are not feature positions. Failed fit does not uniquely establish incorrect physical architecture dimensions.',
             'texture_aspect_disagreement':aspect,'trials':trials,
             'unsupported_blocks':[{'block':'food-court','reason':'No exact individual photographed architectural feature to current mesh-point mapping in retained authority; no numeric fit forced.'},
                                   {'block':'ticketing-wall','reason':'Wall plane identity exists but one plane does not determine translation along plane or complete similarity; repeated departure-board image requires unique panel owner/UV proof.'}],
             'next_needed_visual_correspondences':['same physical stone-column top/skirting corners in a wider non-occluded camera view',
                    '꽃들 same-epoch logo/letter close view or independently identified doorway corners',
                    'ticket-wall unique board/column owner feature coordinates, not repeated texture identity alone',
                    'food-court uniquely matched fixed counter/column corners']}
    result['direct_feature_control_quality']=[{'owner':owner,'controls':len(rows),
        'unique_camera_counts':[r['unique_observed_source_images'] for r in rows],
        'maximum_triangulation_reprojection_pixels':max(p['error_pixels'] for r in rows for p in r['actual_feature_triangulation_reprojection']),
        'parallax_degrees_range':[min(r['maximum_camera_ray_parallax_deg'] for r in rows),max(r['maximum_camera_ray_parallax_deg'] for r in rows)],
        'nearest_mesh_ray_max_disagreement_relative':max(r['nearest_mesh_ray_max_disagreement_relative'] or 0 for r in rows),
        'nearest_mesh_rays_used_for_feature_geometry':False,'field_measurements':False}
        for owner in sorted(set(r['owner'] for r in controls))
        for rows in [[r for r in controls if r['owner']==owner]]]
    structure.write_json(EVIDENCE/'tested-registration-result.json',result)
    builder_provenance()
    unregistered_extent_audit()
    qa={'schema':'chooguard.model-registration-qa.v1','role':'reference_only','field_measurements':False,
        'metric_verified':False,'production_placement_allowed':False,'tested_numeric_similarity_parities':len(trials),
        'matched_photo_feature_controls':len(controls),'column_annotation_controls':len(columns),
        'column_annotation_controls_excluded':sum(not r['fit_eligible'] for r in columns),
        'all_direct_photo_features_triangulated_with_multiple_distinct_source_images':all(r['unique_observed_source_images']>=2 for r in controls),
        'all_photo_feature_camera_depths_positive':all(p['depth_relative']>0 for r in controls for p in r['actual_feature_triangulation_reprojection']),
        'nearest_mesh_rays_used_as_photo_feature_points':False,
        'all_trial_matrices_finite':all(np.isfinite(t['model_world_from_relative_matrix']).all() for t in trials if 'model_world_from_relative_matrix' in t),
        'all_tested_transform_statuses':[t['status'] for t in trials],
        'source_owner_vertices_match_actual_scene_binary':True,
        'original_geometry_modified':False,'worker_sha256':structure.digest(Path(__file__)),
        'artifacts':[{'path':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':structure.digest(p)}
                      for p in sorted(EVIDENCE.rglob('*')) if p.is_file() and p.suffix in ['.json','.jpg','.ply','.obj'] and p.name!='registration-qa.json']}
    assert qa['all_trial_matrices_finite']
    assert all(t['production_placement_allowed']==False for t in trials if 'production_placement_allowed' in t)
    structure.write_json(EVIDENCE/'registration-qa.json',qa)
    return result


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--match-step',type=int,default=3)
    parser.add_argument('--fit-only',action='store_true')
    args=parser.parse_args();EVIDENCE.mkdir(parents=True,exist_ok=True)
    if not args.fit_only:
        targets=audit_targets();image_matches(targets,args.match_step)
    fit_and_evaluate()


if __name__=='__main__':main()
