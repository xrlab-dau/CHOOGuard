"""Direct multiview architectural-corner registration, without sign UV controls.

The retained station owner is an authored class-b public-reference column, not a
survey datum. Rejected diagnostic transforms never authorize scene mutation.
"""
from __future__ import annotations

import hashlib
import json
import struct
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'workers/twin/evidence/architectural-anchor-20261009'
OUTPUT = ROOT / 'workers/twin/output/architectural-anchor-20261009'
INVENTORY = ROOT / '.planning/2026-10-08-interior-runtime-completion/architecture/current-scene-structure.json'
CAMERAS = ROOT / 'workers/twin/output/interior-blocks-20261008/retail-corridor/camera-poses.relative.json'
RELATIVE = ROOT / 'workers/twin/evidence/interior-blocks-20261008/corridor-relative-alignment.json'

# Actual brick/cladding-floor and cladding-ceiling junctions. The earlier markers
# on a blue floor stripe are excluded. Frame 3 was not used in triangulation.
OBSERVATIONS = [
    ('near-foot', 5, {'000001.jpg':[519,467], '000002.jpg':[589,485], '000003.jpg':[693,514]}),
    ('side-foot', 4, {'000001.jpg':[411,420], '000002.jpg':[453,424], '000003.jpg':[515,439]}),
    ('front-far-foot', 1, {'000001.jpg':[749,439], '000002.jpg':[843,452]}),
    ('side-top', 27, {'000001.jpg':[415,144], '000002.jpg':[456,115], '000003.jpg':[515,102]}),
]
FIT_IMAGES = ('000001.jpg','000002.jpg')
SIGMA_PIXELS = 3.0


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write(path, data):
    Path(path).parent.mkdir(parents=True,exist_ok=True)
    Path(path).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')


def transform(matrix, points):
    points=np.atleast_2d(points)
    return (np.c_[points,np.ones(len(points))]@matrix.T)[:,:3]


def project(camera, point):
    world_from_camera=np.eye(4);world_from_camera[:3,:]=camera['world_from_camera_cv']
    camera_point=np.linalg.solve(world_from_camera,np.r_[point,1])[:3]
    projected=np.array(camera['calibration_matrix'])@camera_point
    return projected[:2]/projected[2],float(camera_point[2])


def triangulate(cameras,pairs):
    equations=[]
    for name,pixel in pairs:
        c=cameras[name];matrix=np.eye(4);matrix[:3,:]=c['world_from_camera_cv']
        projection=np.array(c['calibration_matrix'])@np.linalg.inv(matrix)[:3,:]
        equations.extend([pixel[0]*projection[2]-projection[0],pixel[1]*projection[2]-projection[1]])
    _,_,v=np.linalg.svd(np.array(equations));p=v[-1,:3]/v[-1,3]
    return p


def similarity(x,y,parity):
    xc=x.mean(axis=0);yc=y.mean(axis=0);a=x-xc;b=y-yc
    u,s,vt=np.linalg.svd(b.T@a);flip=np.ones(3);flip[-1]=parity*np.linalg.det(u@vt)
    rotation=u@np.diag(flip)@vt;scale=(s*flip).sum()/(a*a).sum()
    matrix=np.eye(4);matrix[:3,:3]=scale*rotation;matrix[:3,3]=yc-scale*rotation@xc
    residual=np.linalg.norm(transform(matrix,x)-y,axis=1)
    return matrix,{'scale_model_units_per_relative_unit':float(scale),'rotation_parity':parity,
                   'rms_model_units':float(np.sqrt(np.mean(residual**2))),
                   'per_corner_residual_model_units':residual.tolist()}


def write_points(path, points):
    lines=['ply','format ascii 1.0',f'element vertex {len(points)}','property float x','property float y','property float z','end_header']
    lines.extend(' '.join(f'{x:.9g}' for x in p) for p in points)
    Path(path).write_text('\n'.join(lines)+'\n')


def main():
    cv2.setNumThreads(2);EVIDENCE.mkdir(parents=True,exist_ok=True);OUTPUT.mkdir(parents=True,exist_ok=True)
    inventory=json.loads(INVENTORY.read_text());owners=[r for r in inventory['renderers'] if r['path']=='실내 트윈 마감/2F 본관 보강/Kit_Trim_trim_stainless_hairline']
    assert len(owners)==1
    owner=owners[0];mesh=next(m for m in inventory['meshes'] if m['id']==owner['mesh'])
    data=(ROOT/mesh['binary']).read_bytes();assert digest(ROOT/mesh['binary'])==mesh['binary_sha256']
    nv,ni,nn=struct.unpack_from('<iii',data);vertices=np.frombuffer(data,'<f4',offset=12,count=nv*3).reshape(-1,3)
    cameras={c['image']:c for c in json.loads(CAMERAS.read_text())['cameras']}
    common=np.array(next(c['target_from_source_relative_matrix'] for c in json.loads(RELATIVE.read_text())['comparisons'] if c['source_block']=='retail-corridor'))
    rng=np.random.default_rng(20261009);controls=[];bootstrap=[]
    for identifier,index,observations in OBSERVATIONS:
        pairs=[(n,p) for n,p in observations.items() if n in FIT_IMAGES]
        point=triangulate(cameras,pairs)
        p=transform(common,point)[0];target=transform(np.array(owner['world_matrix']),vertices[index])[0]
        errors=[]
        for image,pixel in observations.items():
            predicted,depth=project(cameras[image],point)
            errors.append({'image':image,'kind':'fit' if image in FIT_IMAGES else 'heldout',
                'pixel':pixel,'reprojected_pixel':predicted.tolist(),'residual_pixels':float(np.linalg.norm(predicted-pixel)),
                'positive_depth':depth>0,'source_image_sha256':digest(ROOT/cameras[image]['undistorted_image'])})
        jitter=np.array([transform(common,triangulate(cameras,[(n,np.array(v)+rng.normal(0,SIGMA_PIXELS,2)) for n,v in pairs]))[0] for _ in range(500)])
        bootstrap.append(jitter)
        controls.append({'id':identifier,'relative_common_point':p.tolist(),'raw_block_point':point.tolist(),
            'target_model_world':target.tolist(),'target_vertex_index':index,'target_mesh_local_vertex':vertices[index].tolist(),
            'reprojection':errors,'point_jitter_p95_relative_units':float(np.percentile(np.linalg.norm(jitter-p,axis=1),95))})
    x=np.array([c['relative_common_point'] for c in controls]);y=np.array([c['target_model_world'] for c in controls])
    front=x[2]-x[0];side=x[1]-x[0];up=x[3]-x[1]
    model_front=y[2]-y[0];model_side=y[1]-y[0];model_up=y[3]-y[1]
    ratio=float(np.linalg.norm(front)/np.linalg.norm(side));model_ratio=float(np.linalg.norm(model_front)/np.linalg.norm(model_side))
    jitter_ratios=[];jitter_angles=[]
    for i in range(500):
        jf=bootstrap[2][i]-bootstrap[0][i];js=bootstrap[1][i]-bootstrap[0][i]
        jitter_ratios.append(float(np.linalg.norm(jf)/np.linalg.norm(js)))
        jitter_angles.append(float(np.degrees(np.arccos(np.clip(np.dot(jf,js)/np.linalg.norm(jf)/np.linalg.norm(js),-1,1)))))
    intrinsic={'observed_front_side_ratio':ratio,'retained_model_front_side_ratio':model_ratio,
        'observed_ratio_annotation_jitter_95_interval':np.percentile(jitter_ratios,[2.5,97.5]).tolist(),
        'observed_edge_angle_degrees':float(np.degrees(np.arccos(np.clip(np.dot(front,side)/np.linalg.norm(front)/np.linalg.norm(side),-1,1)))),
        'observed_angle_annotation_jitter_95_interval_degrees':np.percentile(jitter_angles,[2.5,97.5]).tolist(),
        'individually_required_scales':{'front':float(np.linalg.norm(model_front)/np.linalg.norm(front)),
            'side':float(np.linalg.norm(model_side)/np.linalg.norm(side)), 'height':float(np.linalg.norm(model_up)/np.linalg.norm(up))},
        'observed_vertical_axis_relative':(up/np.linalg.norm(up)).tolist(),
        'retained_model_vertical_axis':(model_up/np.linalg.norm(model_up)).tolist(),
        'model_corner_ordering_provisional':True,'uncertainty_is_annotation_only_not_field_accuracy':True}
    trials=[]
    for parity in [-1,1]:
        matrix,metrics=similarity(x,y,parity);predicted_relative=transform(np.linalg.inv(matrix),y)
        predicted_raw=transform(np.linalg.inv(common),predicted_relative)
        errors=[]
        for index,(identifier,vi,observations) in enumerate(OBSERVATIONS):
            for image,pixel in observations.items():
                px,depth=project(cameras[image],predicted_raw[index]);errors.append({'corner':identifier,'image':image,
                    'kind':'fit' if image in FIT_IMAGES else 'heldout','predicted_model_pixel':px.tolist(),
                    'residual_pixels':float(np.linalg.norm(px-pixel)),'positive_depth':depth>0})
        held=[e['residual_pixels'] for e in errors if e['kind']=='heldout']
        metrics['heldout_median_pixels']=float(np.median(held));metrics['heldout_max_pixels']=float(np.max(held))
        metrics['all_reprojection_median_pixels']=float(np.median([e['residual_pixels'] for e in errors]))
        supported=metrics['rms_model_units']<=.10 and metrics['heldout_max_pixels']<=6 and all(e['positive_depth'] for e in errors)
        trial={'parity':parity,'status':'supported_public_reference_registration' if supported else 'rejected_column_geometry_registration',
            'station_world_from_common_relative_matrix':matrix.tolist(),'fit':metrics,'model_reprojection':errors,
            'production_installation_allowed':supported,'field_certified':False}
        trials.append(trial)
        write_points(OUTPUT/f'parity-{parity}.fitted-corners.diagnostic-model-world.ply',transform(matrix,x))
        write_points(OUTPUT/f'parity-{parity}.retained-model-corners.ply',y)
        for image in ['000001.jpg','000002.jpg','000003.jpg']:
            picture=cv2.imread(str(ROOT/cameras[image]['undistorted_image']))
            for index,(identifier,vi,observations) in enumerate(OBSERVATIONS):
                if image not in observations:continue
                px,_=project(cameras[image],predicted_raw[index]);pixel=observations[image]
                cv2.drawMarker(picture,tuple(pixel),(0,255,0),cv2.MARKER_CROSS,14,2)
                if np.all(np.isfinite(px)) and np.max(np.abs(px))<100000:
                    target=tuple(np.rint(px).astype(int));cv2.drawMarker(picture,target,(0,0,255),cv2.MARKER_TILTED_CROSS,14,2);cv2.line(picture,tuple(pixel),target,(0,0,255),1)
                cv2.putText(picture,identifier,(pixel[0]-60,pixel[1]-10),cv2.FONT_HERSHEY_SIMPLEX,.35,(0,255,255),1)
            cv2.imwrite(str(EVIDENCE/f'parity-{parity}-{image[:-4]}-structural-fit.jpg'),picture,[cv2.IMWRITE_JPEG_QUALITY,95])
    report={'schema':'chooguard.direct-architectural-anchor-registration.v1','field_certified':False,'metric_verified':False,
        'basis':'actual structural junction pixels, retained multi-camera pose, actual current mesh world matrix; no sign UV controls',
        'scene_baseline_sha256':inventory['scene_file_sha256'],'inventory_sha256':digest(INVENTORY),
        'retained_owner_provenance':'authored class-b column public-reference geometry; not original SKP or independent survey',
        'owner':owner,'mesh':mesh,'fit_images':list(FIT_IMAGES),'heldout_images':['000003.jpg'],
        'annotation_assumed_sigma_pixels':SIGMA_PIXELS,'controls':controls,'intrinsic_geometry_comparison':intrinsic,'trials':trials,
        'production_installation_allowed':any(t['production_installation_allowed'] for t in trials),
        'diagnostic_acceptance_gates':{'rms_model_units':.10,'heldout_max_pixels':6},
        'gates_are_review_tolerances_not_field_accuracy':True,
        'supported_relative_blocks':['retail-corridor','information-corridor-static','corridor-connector'],
        'preserve_current_geometry':True,
        'next_unique_architectural_anchors':['Same left clock-column footprint plus uniquely ordered base and top corners, distinct from the café column',
            'Left corridor shop jamb/soffit corner at the 2F lounge, with known current mesh owner and three separated camera observations',
            'Stationary ticket-board wall junction or escalator landing slab corner with unambiguous current original-source owner'],
        'limitations':['Column corner ordering must be independently checked; rejected geometry does not authorize inferred dimensions.',
            'The side foot partly borders a trim strip; annotation-only bootstrap does not include unknown camera calibration or temporal remodeling.',
            'A relative whole-block registration can succeed only after supported physical architectural controls match, not by deforming the corridor.']}
    write(EVIDENCE/'direct-architectural-registration.json',report)
    write(OUTPUT/'station-registration.json',{'schema':'chooguard.architectural-registration-export.v1',
        'station_world_from_relative_matrix':next((t['station_world_from_common_relative_matrix'] for t in trials if t['production_installation_allowed']),None),
        'production_installation_allowed':report['production_installation_allowed'],'field_certified':False,
        'status':'supported_public_reference_registration' if report['production_installation_allowed'] else 'rejected_column_geometry_registration',
        'relative_block_merge_receipt':str(RELATIVE.relative_to(ROOT)),'report':str((EVIDENCE/'direct-architectural-registration.json').relative_to(ROOT))})
    write(EVIDENCE/'qa.json',{'actual_owner_binary_sha_verified':True,'all_control_depths_positive':all(e['positive_depth'] for c in controls for e in c['reprojection']),
        'no_photographic_uv_controls':True,'no_original_geometry_modified':True,'heldout_frame_excluded_from_fit':True,
        'worker_sha256':digest(__file__),'source_camera_file_sha256':digest(CAMERAS),'relative_alignment_sha256':digest(RELATIVE)})
    print(json.dumps({'intrinsic':intrinsic,'trials':[{k:t[k] for k in ['parity','status','fit']} for t in trials]},indent=2))


if __name__=='__main__':
    main()
