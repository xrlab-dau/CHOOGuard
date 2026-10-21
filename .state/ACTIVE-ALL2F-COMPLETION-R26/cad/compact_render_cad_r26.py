#!/usr/bin/env python3
"""Compact derived CAD within root-authorized0.1mm/1mm physical error bounds."""
import argparse,datetime,hashlib,json,time
from pathlib import Path
from collections import Counter
import numpy as np
from shapely.geometry import Polygon, MultiLineString
from shapely.ops import linemerge
from reportlab.pdfgen import canvas
ROOT=Path('/Users/um-yunsang/CHOOGuard')
def sha(p):
 h=hashlib.sha256()
 with Path(p).open('rb') as f:
  for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
 return h.hexdigest()
def utc():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def iter_rings(gs):
 for p in gs:
  yield p['exterior']
  yield from p['holes']
def simplified_shapes(shapes,tolerance):
 out=[]
 for shape in shapes:
  p=Polygon(shape['exterior'],shape['holes']).simplify(tolerance,preserve_topology=True)
  if p.is_empty:continue
  ps=[p] if p.geom_type=='Polygon' else list(p.geoms)
  for p in ps:
   if p.geom_type=='Polygon':out.append({'exterior':np.round(p.exterior.coords,6).tolist(),'holes':[np.round(h.coords,6).tolist() for h in p.interiors]})
 return out
def simplified_sections(segments,tolerance):
 if len(segments)<3:return segments
 g=linemerge(MultiLineString(segments)).simplify(tolerance,preserve_topology=True)
 gs=[g] if g.geom_type=='LineString' else list(g.geoms)
 out=[]
 for line in gs:
  if line.geom_type!='LineString':continue
  a=np.round(line.coords,6)
  out.extend([[p.tolist(),q.tolist()] for p,q in zip(a[:-1],a[1:]) if np.linalg.norm(q-p)>1e-9])
 return out
def compact_record(record):
 removed=0;before=record['section_segments'];t=time.time()
 for ix,obj in enumerate(record['objects']):
  budget=obj.get('derived_vector_physical_error_bound_m',1e-4)
  for key in ['actual_band_projected_footprint','matching_y7_surface_reference']:
   obj[key]=simplified_shapes(obj[key],budget/4)
  if budget>=1e-3 and obj['actual_band_projected_footprint']:
   obj['dense_detail_sections_represented_by_actual_outer_footprint']=sorted(map(float,obj['sections']))
   removed+=sum(map(len,obj['sections'].values()));obj['sections']={}
  else:obj['sections']={level:simplified_sections(ss,budget/4) for level,ss in obj['sections'].items()}
  if ix%3000==0:print(json.dumps({'compact_processed':ix,'total':len(record['objects']),'seconds':round(time.time()-t,1)}),flush=True)
 record['section_segments']=sum(len(ss) for o in record['objects'] for ss in o['sections'].values())
 record['compact_drawing']={'architectural_physical_error_bound_m':1e-4,'dense_goods_furniture_physical_error_bound_m':1e-3,'boundary_simplification_tolerance':'one quarter of per-object allowed error; topology preserved','dense_goods_furniture':'actual projected outer contour retained; internal/multiple-height microdetail sections suppressed only when real footprint exists; original full3D shared mesh snapshot immutable','sections_before':before,'sections_after':record['section_segments'],'dense_section_segments_suppressed':removed,'hull_or_bbox_used':False,'Scene_Assets_changed':False}
 return record
def render_record(record,source,out,started=None):
 t0=time.time() if started is None else started
 source=Path(source);source=source if source.is_absolute() else ROOT/source
 data=json.loads(source.read_text());out=Path(out);out=out if out.is_absolute() else ROOT/out;out.mkdir(parents=True,exist_ok=True)
 layers=record['objects'];counts=Counter(record['counts']);segcount=record['section_segments'];meshes=data['meshes'];floor_bounds=[]
 for obj in layers:
  if '/MAIN2F_APPROVED_' in obj['path']:
   for ring in iter_rings(obj['matching_y7_surface_reference']):
    a=np.asarray(ring);floor_bounds.append((*a.min(axis=0),*a.max(axis=0)))
 basename='CURRENT-LIVE-2F-OBJECT-VECTOR-CAD-R26-NO-TEXT'
 output_json=out/(basename+'.json');
 with output_json.open('w') as stream:json.dump(record,stream,ensure_ascii=False,separators=(',',':'));stream.write('\n')
 dxf=out/(basename+'.dxf')
 with dxf.open('w') as f:
  def put(*x):f.write('\n'.join(map(str,x))+'\n')
  put(0,'SECTION',2,'HEADER',9,'$ACADVER',1,'AC1009',9,'$INSUNITS',70,6,9,'$MEASUREMENT',70,1,0,'ENDSEC',0,'SECTION',2,'TABLES',0,'TABLE',2,'LAYER',70,len(layers)+1,0,'LAYER',2,'0',70,0,62,8,6,'CONTINUOUS')
  for obj in layers:put(0,'LAYER',2,obj['indexed_cad_layer'],70,4,62,8,6,'CONTINUOUS')
  put(0,'ENDTAB',0,'ENDSEC',0,'SECTION',2,'ENTITIES')
  for obj in layers:
   name=obj['indexed_cad_layer']
   # DXF uses physical footprint plus section levels as actual lines, on this object's locked layer.
   # Keep one copy per coordinate pair even if several levels coincide.
   seen=set()
   for ss in obj['sections'].values():
    for a,b in ss:
     key=tuple(sorted((tuple(a),tuple(b))))
     if key in seen:continue
     seen.add(key);put(0,'LINE',8,name,10,f'{a[0]:.6f}',20,f'{a[1]:.6f}',30,0,11,f'{b[0]:.6f}',21,f'{b[1]:.6f}',31,0)
   for ring in iter_rings(obj['actual_band_projected_footprint']+obj['matching_y7_surface_reference']):
    put(0,'POLYLINE',8,name,66,1,70,1,10,0,20,0,30,0)
    for x,y in ring[:-1]:put(0,'VERTEX',8,name,10,f'{x:.6f}',20,f'{y:.6f}',30,0)
    put(0,'SEQEND',8,name)
  put(0,'ENDSEC',0,'EOF')
 pdf=out/(basename+'.pdf');c=canvas.Canvas(str(pdf),pagesize=(1190.55,841.89),pageCompression=1);c.setTitle('Current actual 2F object vector CAD R26')
 coords=[]
 for obj in layers:
  for ss in obj['sections'].values():
   if ss:coords.append(np.asarray(ss).reshape(-1,2))
  for ring in iter_rings(obj['actual_band_projected_footprint']+obj['matching_y7_surface_reference']):coords.append(np.asarray(ring))
 allxy=np.concatenate(coords);b=(*allxy.min(axis=0)-3,*allxy.max(axis=0)+3)
 if floor_bounds:
  fb=np.asarray(floor_bounds);focus=(fb[:,0].min()-3,fb[:,1].min()-3,fb[:,2].max()+3,fb[:,3].max()+3)
 else:focus=b
 x0,y0,x1,y1=focus;mx=(x0+x1)/2;my=(y0+y1)/2
 views=[(b,'all'),(focus,'plan')]
 # Detailed pages preserve actual geometry at useful scale without extra printed labels.
 for lo,hi in [(x0,mx+2),(mx-2,x1)]:
  for bot,top in [(y0,my+2),(my-2,y1)]:views.append(((lo,bot,hi,top),'plan'))
 views.extend([(focus,'7.15'),(focus,'10.5'),(focus,'13.5')])
 for region,mode in views:
  minx,miny,maxx,maxy=region;scale=min(1150.55/(maxx-minx),801.89/(maxy-miny));dx=(1190.55-(maxx-minx)*scale)/2-minx*scale;dy=(841.89-(maxy-miny)*scale)/2-miny*scale
  c.saveState();clip=c.beginPath();clip.rect(18,18,1154.55,805.89);c.clipPath(clip,stroke=0,fill=0)
  for obj in layers:
   role=obj['source_kind'];native=role=='OFFICIAL_REFERENCE';c.setStrokeColorRGB(*((.56,)*3 if native else (.33,)*3));c.setLineWidth(.19 if native else .23)
   if mode in ['all','plan']:
    shapes=obj['actual_band_projected_footprint']+obj['matching_y7_surface_reference'];ss=obj['sections'].get('8.40000',[])
    if not ss and obj['sections']:
     yy=min(obj['sections'],key=lambda y:abs(float(y)-8.4));ss=obj['sections'][yy]
   else:shapes=obj['matching_y7_surface_reference'];ss=obj['sections'].get(f'{float(mode):.5f}',[])
   for ring in iter_rings(shapes):
    r=np.asarray(ring)
    if r[:,0].max()<minx or r[:,0].min()>maxx or r[:,1].max()<miny or r[:,1].min()>maxy:continue
    p=c.beginPath();p.moveTo(r[0,0]*scale+dx,r[0,1]*scale+dy)
    for x,y in r[1:]:p.lineTo(x*scale+dx,y*scale+dy)
    c.drawPath(p,stroke=1,fill=0)
   pts=[]
   for a,bp in ss:
    if max(a[0],bp[0])<minx or min(a[0],bp[0])>maxx or max(a[1],bp[1])<miny or min(a[1],bp[1])>maxy:continue
    pts.append((a[0]*scale+dx,a[1]*scale+dy,bp[0]*scale+dx,bp[1]*scale+dy))
   if pts:c.lines(pts)
  c.restoreState();c.showPage()
 c.save()
 receipt={'status':'ACTUAL_CURRENT_2F_GEOMETRY_VECTOR_CAD_CREATED','utc':utc(),'source_snapshot':record['source_snapshot'],'source_snapshot_sha256':record['source_snapshot_sha256'],'source_scene':record['source_scene'],'source_scene_sha256':record['source_scene_sha256'],'source_snapshot_utc':record['source_snapshot_utc'],'source_scene_dirty':record['source_scene_dirty'],'derived_CAD_precision':record['precision'],'data':str(output_json.relative_to(ROOT)),'pdf':str(pdf.relative_to(ROOT)),'dxf':str(dxf.relative_to(ROOT)),'hashes':{p.suffix[1:]:sha(p) for p in [output_json,pdf,dxf]},'sizes_bytes':{p.suffix[1:]:p.stat().st_size for p in [output_json,pdf,dxf]},'counts':dict(counts),'section_segments':segcount,'pdf_pages':len(views),'unique_source_meshes':len(meshes),'source_objects':len(layers),'unreadable_meshes':data['unreadable_meshes'],'source_sha_unchanged_within_native_call':data['scene_sha256_before']==data['scene_sha256_after'],'physical_LOD1_excluded_registry_count':data['alternative_LOD1_registry_count'],'actual_excluded_renderers':data['actual_excluded_renderers'],'Scene_Assets_changed':False,'QA_Play_probe_reopen_capture':False,'global_current_pointers_changed':False,'all2F_completion_claim':False,'new_user_confirmation_claim':False,'rerunnable_geometry_exporter':'.state/ACTIVE-ALL2F-COMPLETION-R26/cad/ExportCurrent2FGeometryR26.cs','rerunnable_vector_builder':str(Path(__file__).relative_to(ROOT)),'whole_run_seconds':round(time.time()-t0,1),'compact_drawing':record.get('compact_drawing')}
 (out/'CURRENT-CAD-CONSTRUCTION-RECEIPT-R26.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n');print(json.dumps(receipt,ensure_ascii=False),flush=True)

def main():
 p=argparse.ArgumentParser();p.add_argument('--prepared-data',required=True);p.add_argument('--out',required=True);a=p.parse_args()
 record=json.loads(Path(a.prepared_data).read_text());record=compact_record(record)
 render_record(record,ROOT/record['source_snapshot'],Path(a.out))
if __name__=='__main__':main()
