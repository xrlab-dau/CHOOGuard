#!/usr/bin/env python3
"""Evidence-led, model-relative 1F architecture correction; no Unity mutation."""
from __future__ import annotations
import os
os.environ.setdefault('OMP_NUM_THREADS','2');os.environ.setdefault('OPENBLAS_NUM_THREADS','2')
import argparse,json,hashlib
from pathlib import Path
import numpy as np
import cv2
from PIL import Image,ImageDraw
import registered_structure_compare as geo
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'workers/twin/evidence/floor1-architecture-20261009'
SCENE=ROOT/'.planning/2026-10-08-interior-runtime-completion/architecture/after-frontage/current-scene-structure.json'
SOURCE=ROOT/'asset-library/research-public/2026-10-08/architectural-recovery/frames'
FRAMES=('000106','000123','000133')
def save(path,obj):path.write_text(json.dumps(obj,ensure_ascii=False,indent=2,allow_nan=False)+'\n')
def mutual(a,b):
    matcher=cv2.BFMatcher(cv2.NORM_L2)
    forward=matcher.knnMatch(a,b,k=2);back=matcher.knnMatch(b,a,k=2)
    good={m.queryIdx:m.trainIdx for m,n in back if m.distance<.75*n.distance}
    return {m.queryIdx:m.trainIdx for m,n in forward if m.distance<.75*n.distance and good.get(m.trainIdx)==m.queryIdx}
def triangulate(cams,pixels):
    origins=np.array([c['position'] for c in cams]);dirs=np.array([geo.pixel_rays(np.array([p]),c)[0] for p,c in zip(pixels,cams)])
    constraints=np.eye(3)[None]-dirs[:,:,None]*dirs[:,None,:]
    xyz=np.linalg.solve(constraints.sum(0),np.einsum('nij,nj->i',constraints,origins))
    errors=[float(np.linalg.norm(geo.project(np.array([xyz]),c)[0][0]-p)) for c,p in zip(cams,pixels)]
    depths=np.einsum('ij,ij->i',xyz-origins,dirs)
    return xyz,errors,depths

def tracks():
    OUT.mkdir(parents=True,exist_ok=True);cv2.setNumThreads(2)
    allcams=geo.read(ROOT/'.planning/2026-09-23-video-twin/metric-registration/registered-cameras.json')
    cams=[allcams[fn+'.jpg'] for fn in FRAMES]
    sift=cv2.SIFT_create(nfeatures=6000);features=[];images=[]
    for fn in FRAMES:
        image=cv2.imread(str(SOURCE/(fn+'.png')));image=cv2.resize(image,(960,540));gray=cv2.cvtColor(image,cv2.COLOR_BGR2GRAY)
        kp,d=sift.detectAndCompute(gray,None);features.append((kp,d));images.append(image)
    m12=mutual(features[0][1],features[1][1]);m13=mutual(features[0][1],features[2][1]);rows=[]
    for index in sorted(set(m12)&set(m13)):
        pixels=np.array([features[0][0][index].pt,features[1][0][m12[index]].pt,features[2][0][m13[index]].pt])
        xyz,errors,depths=triangulate(cams,pixels)
        if max(errors)>10.0 or min(depths)<.3 or max(depths)>100:continue
        rows.append({'id':len(rows),'pixels_960':pixels.tolist(),'world':xyz.tolist(),'UVY':geo.world_to_uvy(xyz).tolist(),'reprojection_errors_960':errors,'depths_source_units':depths.tolist()})
    scene=geo.read(SCENE);g=geo.geometry(scene)
    for k,cam in enumerate(cams):
        tree,_=geo.projection_index(g['triangles'],cam)
        samples=np.array([row['pixels_960'][k] for row in rows]);rayhits=geo.hits(samples,cam,g,tree,True)
        for row,h in zip(rows,rayhits):
            row.setdefault('current_first_hits',[])
            if h['hit']:
                hit=h['hit'];owner=g['owners'][hit['owner_index']]
                row['current_first_hits'].append({'owner':owner['owner'],'mesh':owner['mesh'],'triangle':hit['triangle_ordinal'],'depth_source_units':hit['distance_source_units'],'occlusion_gap_source_units':row['depths_source_units'][k]-hit['distance_source_units']})
            else:row['current_first_hits'].append(None)
    conflicts=[]
    for row in rows:
        selected=[h for h in row['current_first_hits'] if h and '1F 공용/Kit_RoomWall_벽_내장' in h['owner'] and h['occlusion_gap_source_units']>.3]
        if len(selected)>=2:conflicts.append(row)
    result={'schema':'chooguard.floor1.multiview-structure.v1','scene':geo.rel(SCENE),'scene_sha256':geo.sha(SCENE),'production_scene_sha256':scene['scene_file_sha256'],'source_frames':[{'frame':fn,'path':geo.rel(SOURCE/(fn+'.png')),'sha256':geo.sha(SOURCE/(fn+'.png'))} for fn in FRAMES],'camera_source':'.planning/2026-09-23-video-twin/metric-registration/registered-cameras.json','metric_certified':False,'camera_recapture_mismatch_note':'Retained original cameras versus recovered player PNGs show coherent 7-9px residual even for true stationary three-view caption features. This is an approximate discrepancy audit, not native camera recalibration or permission to move a whole wall.','match_ratio':.75,'max_reprojection_error_pixels_960':10.0,'all_tracks':rows,'inferred_room_wall_conflicts':conflicts}
    save(OUT/'multiview-structure-tracks.json',result)
    for k,(fn,im) in enumerate(zip(FRAMES,images)):
        rgb=Image.fromarray(cv2.cvtColor(im,cv2.COLOR_BGR2RGB));d=ImageDraw.Draw(rgb)
        for row in conflicts:
            p=row['pixels_960'][k];d.ellipse((p[0]-3,p[1]-3,p[0]+3,p[1]+3),outline='red',width=2);d.text((p[0]+3,p[1]),str(row['id']),fill='yellow')
        rgb.save(OUT/(fn+'-room-wall-conflict-tracks.png'))
    print(json.dumps({'all_static_geometry_candidates':len(rows),'room_wall_conflicts':len(conflicts),'output':str(OUT/'multiview-structure-tracks.json')}))
if __name__=='__main__':tracks()
