#!/usr/bin/env python3
"""Stage a source-observed low 1F metal ceiling correction without Unity mutation.

Coordinates, fitted pitch and relief are model-relative. Manual visible circle
centres have reflection/annotation uncertainty; relief depth is a visual estimate.
Native footprint, higher faces, fixture frames, colliders and lights are retained.
"""
from __future__ import annotations
import os
os.environ.setdefault('OMP_NUM_THREADS','2')
os.environ.setdefault('OPENBLAS_NUM_THREADS','2')
import hashlib,json,math,struct
from pathlib import Path
import cv2
import numpy as np
from PIL import Image,ImageDraw
from scipy.optimize import least_squares
from scipy.spatial import Delaunay
from shapely.geometry import Polygon,Point
from shapely.ops import unary_union,triangulate
import registered_structure_compare as geo

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'workers/twin/output/observed-ceiling-20261009'
EVIDENCE=ROOT/'workers/twin/evidence/observed-ceiling-20261009'
SCENE=ROOT/'.planning/2026-10-08-interior-runtime-completion/architecture/after-frontage/current-scene-structure.json'
SPEC=ROOT/'.planning/2026-09-24-interior-twin/specs/1f-detail.json'
CAMERA=ROOT/'.planning/2026-09-23-video-twin/metric-registration/registered-cameras.json'
SOURCE=ROOT/'asset-library/research-public/2026-10-08/architectural-recovery/frames'
PLANE_Y=2.31
RELIEF_DEPTH=.012
OBSERVATIONS={
 '000123':[[1035,167],[1280,168],[1050,233],[1240,241],[1060,281],[1215,290],[1070,315],[1195,320]],
 '000106':[[1092,315],[1272,312],[1096,351],[1245,354],[1106,382],[1232,385],[1112,400],[1214,408]],
}
# Independent, approximate centres on the far repeated row in the closer view.
# These deliberately remain outside the fit; glossy relief centres are ambiguous.
HOLDOUT={'000133':[[1030,202],[1010,267],[1012,312]]}
FIT_INDICES=np.array([[r,c] for r in range(4) for c in range(2)],float)
HOLDOUT_INDICES=np.array([[4,0],[5,0],[6,0]],float)


def save(path,value):
 path.write_text(json.dumps(value,ensure_ascii=False,indent=2,allow_nan=False)+'\n')

def binary_mesh(record):
 path=ROOT/record['binary'];assert geo.sha(path)==record['binary_sha256']
 data=path.read_bytes();nv,ni,nn=struct.unpack('<iii',data[:12]);assert len(data)==12+nv*12+ni*4+nn*12
 v=np.frombuffer(data, '<f4',count=nv*3,offset=12).reshape(-1,3).copy()
 ix=np.frombuffer(data,'<i4',count=ni,offset=12+nv*12).reshape(-1,3).copy()
 n=np.frombuffer(data,'<f4',count=nn*3,offset=12+nv*12+ni*4).reshape(-1,3).copy()
 return v,ix,n

def uv_pattern(p,ij):
 t=p[4];rot=np.array([[math.cos(t),-math.sin(t)],[math.sin(t),math.cos(t)]])
 return p[:2]+(np.asarray(ij)*p[2:4])@rot.T

def pattern_fit(cameras):
 def residual(p):
  uv=uv_pattern(p,FIT_INDICES);w=geo.uvy_to_world(np.c_[uv,np.full(len(uv),PLANE_Y)])
  return np.concatenate([(geo.project(w,cameras[f+'.jpg'])[0]-np.asarray(xy)/2).ravel() for f,xy in OBSERVATIONS.items()])
 fit=least_squares(residual,[15.7,-16.24,.6,.6,-.12],bounds=([15.,-17.,.4,.4,-.6],[16.5,-15.5,.9,.9,.6]),loss='soft_l1',f_scale=4,max_nfev=300)
 p=fit.x;checks=[]
 for f,observed in (OBSERVATIONS|HOLDOUT).items():
  ij=FIT_INDICES if f in OBSERVATIONS else HOLDOUT_INDICES
  uv=uv_pattern(p,ij);world=geo.uvy_to_world(np.c_[uv,np.full(len(uv),PLANE_Y)])
  predicted,depth=geo.project(world,cameras[f+'.jpg']);observed=np.asarray(observed)/2
  errors=np.linalg.norm(predicted-observed,axis=1)
  im=Image.open(SOURCE/(f+'.png')).convert('RGB');draw=ImageDraw.Draw(im)
  for n,(a,b) in enumerate(zip(observed*2,predicted*2)):
   draw.ellipse((a[0]-7,a[1]-7,a[0]+7,a[1]+7),outline='#FFD100',width=3)
   draw.ellipse((b[0]-7,b[1]-7,b[0]+7,b[1]+7),outline='#00DAFF',width=3)
   draw.line((*a,*b),fill='#FF5555',width=2)
   draw.text((a[0]+8,a[1]+3),str(n),fill='#FFD100')
  im.save(EVIDENCE/(f+'-pattern-centre-fit.png'))
  checks.append({'frame':f,'role':'training' if f in OBSERVATIONS else 'held_out_approximate_centres',
   'lattice_indices':ij.astype(int).tolist(),'observed_pixels_source_1920':(observed*2).tolist(),
   'projected_pixels_source_1920':(predicted*2).tolist(),'errors_pixels_960':errors.tolist(),
   'rms_pixels_960':float(np.sqrt(np.mean(errors**2))),'max_pixels_960':float(errors.max()),
   'annotation_uncertainty_source_pixels':12,'visible_depths_positive':bool((depth>0).all())})
 return p,{'method':'robust joint source-pixel fit of visible circle centres in 106 and 123 to the native Y2.31 plane; independent approximate 133 row retained as holdout',
  'source_pixel_scale_to_camera':.5,'camera_radial_handling':'retained SIMPLE_RADIAL polynomial and exact inverse; no extra root yaw',
  'origin_UV_circle_centre':p[:2].tolist(),'pitch_model_units':p[2:4].tolist(),'rotation_degrees_in_UV':float(math.degrees(p[4])),
  'source_pattern_depth_measured':False,'relief_depth_visual_estimate_model_units':RELIEF_DEPTH,
  'metric_or_survey_certified':False,'centre_residual_checks':checks,
  'held_out_conclusion':'133 pattern-centre correspondence / current plane registration is unresolved: centres are reflection-dependent and farther row assignment is approximate; do not claim a held-out pixel fit or field accuracy.'}


def source_parts(scene):
 meshes={m['id']:m for m in scene['meshes']}
 target=next(r for r in scene['renderers'] if meshes[r['mesh']]['asset'].endswith('/Kit/1F/Kit_Ceiling_Kit_Ceiling600.asset'))
 grid=next(r for r in scene['renderers'] if meshes[r['mesh']]['asset'].endswith('/Kit/1F/Kit_CeilingGrid.asset'))
 pv,pi,pn=binary_mesh(meshes[target['mesh']]);gv,gi,gn=binary_mesh(meshes[grid['mesh']])
 selected=np.max(np.abs(pv[pi][:,:,1]-PLANE_Y),axis=1)<2e-6
 assert selected.sum()==11,'low plane selection drifted'
 footprint=unary_union([Polygon(t[:,[2,0]]) for t in pv[pi[selected]]])
 assert isinstance(footprint,Polygon) and footprint.is_valid
 spec=geo.read(SPEC);element=next(e for e in spec['elements'] if e['id']=='1F-ceiling-approach-0')
 specpoly=Polygon(element['geometry']['polygon'],element['geometry']['holes'])
 assert footprint.symmetric_difference(specpoly).area<1e-4
 # Drop T-bars only. Fixture frames are the distinct 2.302 plane; perimeter
 # trim at 2.306 is moved, without vertex edits, to the smooth metal material.
 gt=gv[gi];bars=(np.abs(gt[:,:,1].min(1)-2.298)<2e-6)&(gt[:,:,1].max(1)<2.311)
 trim=(np.max(np.abs(gt[:,:,1]-2.306),axis=1)<2e-6)
 assert bars.sum()==234 and trim.sum()==26
 for triangle in gt[bars|trim]:
  assert footprint.buffer(.04).covers(Polygon(triangle[:,[2,0]]).centroid)
 fixtures=[]
 for row in scene['renderers']:
  if row['path']=='실내 트윈 마감/1F/Kit_Light_조명기구_발광':
   fv,fi,_=binary_mesh(meshes[row['mesh']]);ft=fv[fi];use=np.max(np.abs(ft[:,:,1]-2.304),axis=1)<2e-6
   fixtures=[Polygon(t[:,[2,0]]) for t in ft[use]]
 fixture_union=unary_union(fixtures)
 return target,grid,meshes[target['mesh']],meshes[grid['mesh']],pv,pi,gv,gi,selected,bars,trim,footprint,fixture_union


def build_relief(footprint,fixtures,p):
 # Circular pressed relief is actual geometry. A flat centre, rounded shoulder
 # and narrow flat border reproduce the confirmed style without hidden detail.
 pts=[(0.,0.)]
 for radius in [.22,.34,.40,.455]:
  pts.extend((radius*math.cos(t),radius*math.sin(t)) for t in np.arange(20)*2*math.pi/20)
 edge=np.linspace(-.5,.5,4)
 pts.extend((x,y) for x in edge for y in [-.5,.5]);pts.extend((x,y) for y in edge for x in [-.5,.5])
 pts=np.unique(np.round(pts,12),axis=0);template=Delaunay(pts).simplices
 # Lattice zero is a circle centre, so cell corners are at +/- .5.
 t=p[4];rot=np.array([[math.cos(t),-math.sin(t)],[math.sin(t),math.cos(t)]])
 xy=np.asarray(footprint.exterior.coords);ij=((xy-p[:2])@rot)/p[2:4]
 low=np.floor(ij.min(0)-1).astype(int);high=np.ceil(ij.max(0)+1).astype(int)
 vertices=[];uvs=[];indices=[];dedup={};cells=0;flatfixtures=fixtures.buffer(.025)
 def vertex(uv,cell_uv):
  key=tuple(np.round(uv,8))
  if key in dedup:return dedup[key]
  lattice=((uv-p[:2])@rot)/p[2:4];radius=np.linalg.norm(lattice-np.round(lattice))
  shoulder=np.clip((.455-radius)/(.455-.34),0,1);shoulder=shoulder*shoulder*(3-2*shoulder)
  edgefade=np.clip(footprint.boundary.distance(Point(uv))/.04,0,1)
  height=RELIEF_DEPTH*shoulder*edgefade
  if flatfixtures.covers(Point(uv)):height=0.
  n=len(vertices);dedup[key]=n;vertices.append([float(uv[1]),PLANE_Y+height,float(uv[0])]);uvs.append((lattice+.5).tolist());return n
 for a in range(low[0],high[0]+1):
  for b in range(low[1],high[1]+1):
   centre=np.array([a,b]);uv=(pts+centre)*p[2:4]@rot.T+p[:2]
   square=Polygon(uv_pattern(p,np.array([[-.5,-.5],[.5,-.5],[.5,.5],[-.5,.5]])+centre))
   if not footprint.intersects(square):continue
   cells+=1
   full=footprint.covers(square)
   for tri in template:
    poly=Polygon(uv[tri]) if full else Polygon(uv[tri]).intersection(footprint)
    if poly.is_empty or poly.area<1e-11:continue
    polygons=[poly] if isinstance(poly,Polygon) else [g for g in getattr(poly,'geoms',[]) if isinstance(g,Polygon)]
    for polygon in polygons:
     # Intersected triangles are convex except possible original boundary
     # notches. Triangulation is clipped again to preserve every notch/hole.
     for triangle in triangulate(polygon):
      if not polygon.covers(triangle.representative_point()):continue
      coords=np.asarray(triangle.exterior.coords)[:3]
      ab,ac=coords[1]-coords[0],coords[2]-coords[0]
      if ab[0]*ac[1]-ab[1]*ac[0]<0:coords=coords[[0,2,1]]
      ids=[vertex(u,centre) for u in coords];indices.append([ids[0],ids[2],ids[1]])
 v=np.asarray(vertices,'<f4');ix=np.asarray(indices,'<i4');uv=np.asarray(uvs,'<f4')
 norms=np.zeros_like(v,dtype=np.float64);normals=np.cross(v[ix[:,1]]-v[ix[:,0]],v[ix[:,2]]-v[ix[:,0]])
 for j in range(3):np.add.at(norms,ix[:,j],normals)
 norms/=np.linalg.norm(norms,axis=1)[:,None]
 assert np.isfinite(norms).all() and np.min(norms[:,1])<-.9
 # Independent footprint comparison is performed on generated faces, not bounds.
 projected=unary_union([Polygon(tri[:,[2,0]]) for tri in v[ix]])
 sym=projected.symmetric_difference(footprint).area
 assert sym<2e-4,(sym,footprint.area)
 return v,ix,norms.astype('<f4'),uv,{'vertices':len(v),'triangles':len(ix),'cells_intersected':cells,'native_footprint_area_model_units_squared':footprint.area,'generated_projected_area_model_units_squared':projected.area,'symmetric_difference_area':sym,'native_hole_count':len(footprint.interiors),'generated_hole_count':len(projected.interiors),'min_y':float(v[:,1].min()),'max_y':float(v[:,1].max()),'nominal_y':PLANE_Y,'relief_depth_independently_measured':False}


def textures():
 size=512;rgb=np.empty((size,size,3),np.uint8);rgb[:]=[186,187,186]
 # Seam width is appearance-only, .004 per .6 approximate panel = ~3.4 pixels.
 rgb[:2]=[62,65,66];rgb[-2:]=[62,65,66];rgb[:,:2]=[62,65,66];rgb[:,-2:]=[62,65,66]
 Image.fromarray(rgb).save(OUT/'ObservedMetalPanel_BaseColor.png')
 return {'name':'Observed_1F_PressedMetal','shader':'Universal Render Pipeline/Lit','base_color_rgba':[1,1,1,1],'base_map':'ObservedMetalPanel_BaseColor.png','metallic':.78,'smoothness':.76,'surface':0,'cull':2,'emission':False,'normal_map':None,'uv':'native local geometry has fitted pattern lattice UV; texture uses Repeat and trilinear filtering, sRGB true','parameters_status':'appearance estimates from public source imagery, not instrument measurements'}


def mesh_files(name,v,ix,n,uv=None):
 fn=OUT/(name+'.bin');fn.write_bytes(struct.pack('<iii',len(v),ix.size,len(n))+v.astype('<f4').tobytes()+ix.astype('<i4').tobytes()+n.astype('<f4').tobytes())
 record={'binary':geo.rel(fn),'binary_sha256':geo.sha(fn),'vertices':len(v),'indices':int(ix.size),'normals':len(n),'binary_format':'little endian int32 nv,ni,nn then float32 xyz vertices, int32 indices, float32 xyz normals'}
 if uv is not None:
  uvfn=OUT/(name+'.uv.bin');uvfn.write_bytes(uv.astype('<f4').tobytes());record['uv_binary']=geo.rel(uvfn);record['uv_binary_sha256']=geo.sha(uvfn);record['uv_binary_format']='float32 u,v per vertex, no header'
 return record


def main():
 OUT.mkdir(parents=True,exist_ok=True);EVIDENCE.mkdir(parents=True,exist_ok=True)
 scene=geo.read(SCENE);cameras=geo.read(CAMERA);p,fit=pattern_fit(cameras)
 target,grid,pm,gm,pv,pi,gv,gi,sel,bars,trim,footprint,fixtures=source_parts(scene)
 v,ix,n,uv,meshqa=build_relief(footprint,fixtures,p)
 relief=mesh_files('ObservedLowCeilingRelief',v,ix,n,uv)
 # Trim is an exact local-coordinate transfer, with no new hidden thickness.
 trimids=np.unique(gi[trim]);lookup=np.full(len(gv),-1);lookup[trimids]=np.arange(len(trimids));trimix=lookup[gi[trim]]
 _,_,gridnorm=binary_mesh(gm)
 metaltrim=mesh_files('ObservedLowCeilingPerimeterTrim',gv[trimids],trimix,gridnorm[trimids])
 mat=textures();save(OUT/'material.json',mat)
 save(EVIDENCE/'pattern-fit.json',fit);save(EVIDENCE/'geometry-qa.json',meshqa)
 preserve={'scene_sha256':geo.sha(SCENE),'scene_file_sha256_at_export':scene['scene_file_sha256'],'source_panel_mesh_sha256':pm['binary_sha256'],'source_grid_mesh_sha256':gm['binary_sha256'],'higher_panel_triangle_count_unchanged':int((~sel).sum()),'native_low_ceiling_triangles_replaced':int(sel.sum()),'low_exposed_tbar_triangles_removed':int(bars.sum()),'native_low_perimeter_trim_triangles_exact_transfer':int(trim.sum()),'low_fixture_frame_triangles_retained':int(((gv[gi][:,:,1].max(1)<2.311)&~bars&~trim).sum()),'all_colliders_untouched':True,'all_lights_and_light_meshes_untouched':True,'other_renderer_mutations':0,'other_floors_and_high_ceilings_untouched':True}
 save(EVIDENCE/'preservation-contract.json',preserve)
 manifest={'schema':'chooguard.observed-low-ceiling.patch.v1','target_component_id':'1F-ceiling-approach-0','production_scene_mutated':False,'asset_mutated':False,'basis':'geometry local P(U,V,Y)=(V,Y,U); attach under existing 1F transform; registered cameras already world','source_scene_export':geo.rel(SCENE),'source_scene_export_sha256':geo.sha(SCENE),'source_spec':geo.rel(SPEC),'source_spec_sha256':geo.sha(SPEC),'source_frames':[{'path':geo.rel(SOURCE/(f+'.png')),'sha256':geo.sha(SOURCE/(f+'.png'))} for f in ['000106','000123','000133']],'panel_native_asset':pm['asset'],'panel_native_guid':pm['asset_guid'],'panel_renderer_path':target['path'],'panel_renderer_global_id':target['global_object_id'],'panel_source_binary_sha256':pm['binary_sha256'],'panel_remove_triangle_ordinals':np.flatnonzero(sel).tolist(),'grid_native_asset':gm['asset'],'grid_native_guid':gm['asset_guid'],'grid_renderer_path':grid['path'],'grid_renderer_global_id':grid['global_object_id'],'grid_source_binary_sha256':gm['binary_sha256'],'grid_remove_tbar_triangle_ordinals':np.flatnonzero(bars).tolist(),'grid_transfer_trim_triangle_ordinals':np.flatnonzero(trim).tolist(),'relief_mesh':relief,'exact_perimeter_trim_mesh':metaltrim,'material':geo.rel(OUT/'material.json'),'perimeter_trim_material':{'shader':'Universal Render Pipeline/Lit','base_color_rgba':[.55,.56,.55,1.],'metallic':.78,'smoothness':.76,'emission':False,'texture':None},'preservation':preserve,'pattern_fit':fit,'geometry_qa':meshqa,'install_rule':'clone native panel and grid meshes preserving their vertices, normals, tangents, UVs, vertex colors, submesh materials; remove ONLY listed triangle ordinals. Existing renderer colliders must continue referencing their original mesh. Add relief and exact trim as new child renderers with identity local transform and no collider. Preserve standalone light renderers and their Light components. Never assign the new metal material globally to Kit_Ceiling600 or Kit_GridWhite.'}
 save(OUT/'patch-manifest.json',manifest)
 save(EVIDENCE/'patch-manifest-summary.json',{'output':geo.rel(OUT/'patch-manifest.json'),'fit':fit,'geometry':meshqa,'preservation':preserve})
 (OUT/'INSTALL.md').write_text('''# Observed low 1F pressed-metal ceiling patch\n\nStaged only. No Unity scene or Assets mutations have occurred. Read patch-manifest.json before installation.\n\n1. Resolve the exact panel/grid renderers by their full hierarchy paths and require their native asset GUIDs. Verify native low plane selection still matches the 11 source triangle ordinals.\n2. Clone each original Mesh with Object.Instantiate so all original UVs, tangents, colours and higher faces are preserved. Remove only the listed panel triangles, T-bar triangles and transferred perimeter-trim triangles from clone indices. Keep existing renderer materials. Existing MeshColliders keep their original sharedMesh.\n3. Import ObservedLowCeilingRelief.bin (header nv/ni/nn) and its UV sidecar. Use UInt32 indices if needed. Create a new child under the panel renderer with identity local transform, a MeshFilter and MeshRenderer, and no collider. Its fitted origin and pitch are already baked into local vertices/UVs. Do not apply root yaw again.\n4. Assign a dedicated URP/Lit material using material.json. Import the base map with Repeat, sRGB and trilinear. Use geometry normals; do not attach the old OfficeCeiling normal, emission or occlusion maps.\n5. Import ObservedLowCeilingPerimeterTrim.bin as another identity child. This is exactly the original trim geometry, with a dedicated untextured smooth metal material. Fixture frames remain in the original renderer, unchanged.\n6. Keep every light, fixture mesh, collider, higher ceiling and other-floor renderer unchanged. Retained original library lighting geometry stays at its original positions.\n7. Capture registered source views 106, 123 and 133 and inspect the metallic pressed-circle appearance, ceiling edge, fixtures and gate route. Geometry footprint QA has passed; held-out 133 circle alignment remains approximate. A source-photography pixel match is not certified.\n\nThe relief depth (0.012 model units), colour, metallic and roughness parameters are visual estimates. Pitch/phase/orientation are fitted to public source photographs on the retained model plane. They are not field measurements.\n''')
 print(json.dumps({'output':geo.rel(OUT/'patch-manifest.json'),'geometry':meshqa,'fit_pitch':fit['pitch_model_units'],'fit_rotation_degrees':fit['rotation_degrees_in_UV'],'frame_RMS_960':{c['frame']:c['rms_pixels_960'] for c in fit['centre_residual_checks']},'preservation':preserve}))

if __name__=='__main__':main()
