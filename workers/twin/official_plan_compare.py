#!/usr/bin/env python3
"""Read-only actual native architecture/official-plan comparison; no scene or asset mutations."""
from pathlib import Path
import json,hashlib,struct,argparse
import numpy as np
from PIL import Image,ImageDraw,ImageFont
import cv2
OUT=Path('workers/twin/output/official-plan-compare-20261009');EV=Path('workers/twin/evidence/official-plan-compare-20261009')
AUDIT=Path('.planning/2026-10-08-interior-runtime-completion/architecture/after-frontage/current-scene-structure.json')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def write(p,data):Path(p).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
def readbin(m):
 b=Path(m['binary']).read_bytes();assert sha(m['binary'])==m['binary_sha256'];nv,ni,nn=struct.unpack_from('<iii',b)
 return np.frombuffer(b,'<f4',count=nv*3,offset=12).reshape(nv,3),np.frombuffer(b,'<i4',count=ni,offset=12+nv*12).reshape(-1,3)
def cut(tri,y):
 crossing=(tri[:,:,1].min(1)<y-1e-6)&(tri[:,:,1].max(1)>y+1e-6);tri=tri[crossing];p=[];valid=[]
 for a,b in [(0,1),(1,2),(2,0)]:
  va,vb=tri[:,a],tri[:,b];d=vb[:,1]-va[:,1];ok=(abs(d)>1e-9)&(((va[:,1]<=y)&(vb[:,1]>=y))|((vb[:,1]<=y)&(va[:,1]>=y)))
  f=np.divide(y-va[:,1],d,out=np.zeros(len(d)),where=abs(d)>1e-9);p.append(va+(vb-va)*f[:,None]);valid.append(ok)
 p=np.stack(p,1);valid=np.stack(valid,1);segments=[]
 for a,b in [(0,1),(0,2),(1,2)]:
  ok=valid[:,a]&valid[:,b]&(np.linalg.norm(p[:,a]-p[:,b],axis=1)>1e-7)
  segments.append(np.stack([p[ok,a][:,[0,2]],p[ok,b][:,[0,2]]],1))
 return np.concatenate(segments) if segments else np.empty((0,2,2))
def native_section(y=8.25):
 s=json.loads(AUDIT.read_text());meshes={m['id']:m for m in s['meshes']};cache={};rows=[];selected=[]
 for r in s['renderers']:
  if not r['active'] or not r['renderer_enabled'] or r['mesh'] is None:continue
  mn,mx=r['world_bounds_min'],r['world_bounds_max']
  if mn is None or not(mn[1]<y<mx[1]):continue
  if mx[0]<-80 or mn[0]>150 or mx[2]<-100 or mn[2]>110:continue
  m=meshes[r['mesh']]
  if not m['readable']:continue
  if m['id'] not in cache:cache[m['id']]=readbin(m)
  v,t=cache[m['id']];wm=np.array(r['world_matrix']);world=v@wm[:3,:3].T+wm[:3,3];seg=cut(world[t],y)
  if not len(seg):continue
  rows.append({'path':r['path'],'native_mesh':m['id'],'native_geometry_sha256':m['binary_sha256'],'global_object_id':r['global_object_id'],'segments':len(seg),'segment_start':sum(x['segments'] for x in rows),'materials':r['materials']});selected.append(seg)
 segments=np.concatenate(selected);np.save(OUT/'native-section-segments.npy',segments);write(EV/'native-section-provenance.json',{'audit':str(AUDIT),'audit_sha256':sha(AUDIT),'scene_sha256':s['scene_file_sha256'],'y':y,'rows':rows,'field_accuracy_verified':False})
 minimum=np.array([-85.,-105.]);maximum=np.array([160.,115.]);scale=7.;size=(int((maximum[0]-minimum[0])*scale),int((maximum[1]-minimum[1])*scale))
 image=Image.new('RGB',size,'white');draw=ImageDraw.Draw(image)
 for x in np.arange(-80,161,20):draw.line([(int((x-minimum[0])*scale),0),(int((x-minimum[0])*scale),size[1])],fill=(226,230,235))
 for z in np.arange(-100,116,20):draw.line([(0,int((maximum[1]-z)*scale)),(size[0],int((maximum[1]-z)*scale))],fill=(226,230,235))
 for row in rows:
  colour=(15,45,70) if row['path'].startswith('FPSWorld/공식 자료 부산역 역사') else (190,70,30)
  for seg in segments[row['segment_start']:row['segment_start']+row['segments']]:
   points=[((p[0]-minimum[0])*scale,(maximum[1]-p[1])*scale) for p in seg];draw.line(points,fill=colour,width=1)
 image.save(OUT/'native-2f-section-topview.png');write(OUT/'topview-frame.json',{'world_x_min':minimum[0],'world_z_max':maximum[1],'pixels_per_model_unit':scale,'image_size':size,'section_y_model':y})
 print('SECTION',len(rows),len(segments),flush=True)
 return segments
if __name__=='__main__':native_section()

def registration_candidates():
    import itertools
    plan=Path('asset-library/research-public/2026-10-09/architectural-plans/primary-floorplan-search/busan-2f-native-architectural-background-631x496.jpeg')
    image=Image.open(plan).convert('RGB');a=np.array(image);mask=(cv2.cvtColor(a,cv2.COLOR_RGB2GRAY)<150).astype(np.uint8);distance=cv2.distanceTransform(1-mask,cv2.DIST_L2,5)
    p=np.array([[174.,155.],[253.,155.],[253.,214.],[174.,214.]])
    ring=json.loads((EV/'ring-column-native-centres.json').read_text());centres=np.array(ring['centres_world_xz']);mean=centres.mean(0);order=np.argsort(np.arctan2(centres[:,1]-mean[1],centres[:,0]-mean[0]));centres=centres[order]
    segments=np.load(OUT/'native-section-segments.npy');provenance=json.loads((EV/'native-section-provenance.json').read_text());original=np.concatenate([segments[r['segment_start']:r['segment_start']+r['segments']] for r in provenance['rows'] if r['path'].startswith('FPSWorld/공식 자료 부산역 역사')])
    samples=original.mean(axis=1)
    candidates=[];tiles=[]
    for reverse in [False,True]:
      w=centres[::-1] if reverse else centres
      for shift in range(4):
       w=np.roll(w,shift,axis=0);matrix=np.linalg.lstsq(np.c_[w,np.ones(4)],p,rcond=None)[0];mapped=np.c_[samples,np.ones(len(samples))]@matrix
       inside=(mapped[:,0]>=0)&(mapped[:,0]<631)&(mapped[:,1]>=0)&(mapped[:,1]<496);v=mapped[inside].astype(int);d=distance[v[:,1],v[:,0]]
       score=float(np.median(d));entry={'id':len(candidates),'source_ring_world':w.tolist(),'plan_ring_pixels':p.tolist(),'model_xz_to_native_plan_px':matrix.tolist(),'inside_original_section_sample_fraction':float(inside.mean()),'median_original_section_distance_to_plan_ink_px':score,'p95_original_section_distance_to_plan_ink_px':float(np.percentile(d,95)), 'fit_kind':'four_ring_affine_hypothesis_only_not_independent_verified_registration'}
       candidates.append(entry);tile=image.copy();draw=ImageDraw.Draw(tile)
       lines=np.concatenate([segments[:,:,0:2],np.ones((len(segments),2,1))],axis=2)@matrix
       for line in lines:
        if np.all((line[:,0]>-631)&(line[:,0]<1262)&(line[:,1]>-496)&(line[:,1]<992)):draw.line([tuple(q) for q in line],fill=(225,40,35),width=1)
       for point in p:draw.ellipse([point[0]-4,point[1]-4,point[0]+4,point[1]+4],outline=(0,170,50),width=2)
       draw.rectangle([0,0,631,22],fill='white');draw.text((4,4),'Hypothesis '+str(entry['id'])+' median='+str(round(score,2))+' px | ring affine only',fill='black');tile.save(OUT/('hypothesis-'+str(entry['id'])+'.png'));tiles.append(tile)
    write(EV/'ring-hypotheses.json',candidates);canvas=Image.new('RGB',(631*4,496*2),'white')
    for i,t in enumerate(tiles):canvas.paste(t,((i%4)*631,(i//4)*496))
    canvas.save(OUT/'ring-hypothesis-contact-sheet.png');print('RANKED',[(c['id'],c['median_original_section_distance_to_plan_ink_px'],c['p95_original_section_distance_to_plan_ink_px']) for c in sorted(candidates,key=lambda c:c['median_original_section_distance_to_plan_ink_px'])],flush=True)
    return candidates

def find_native_ring_controls():
    from scipy.cluster.vq import kmeans2
    provenance=json.loads((EV/'native-section-provenance.json').read_text());segments=np.load(OUT/'native-section-segments.npy')
    rows=[r for r in provenance['rows'] if r['path'].endswith('Box_Hall_TreeColumn_Shaft')]
    if len(rows)!=1:raise ValueError('Exact native four-column owner missing or ambiguous')
    row=rows[0];points=segments[row['segment_start']:row['segment_start']+row['segments']].reshape(-1,2)
    centres,labels=kmeans2(points,4,minit='++',seed=4)
    radii=[]
    for i,c in enumerate(centres):
        r=np.linalg.norm(points[labels==i]-c,axis=1)
        if len(r)<20 or r.max()>.451 or r.min()<.446:raise ValueError('Native ring column geometry differs')
        radii.append([float(r.min()),float(r.max())])
    write(EV/'ring-column-native-centres.json',{'path':row['path'],'mesh_id':row['native_mesh'],'mesh_sha256':row['native_geometry_sha256'],'centres_world_xz':centres.tolist(),'native_radii_model_units':radii})

if __name__=='__main__':
    find_native_ring_controls()
    registration_candidates()
