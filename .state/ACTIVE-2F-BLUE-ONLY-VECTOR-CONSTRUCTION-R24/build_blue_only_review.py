"""R24: only user-blue regions; exact CAD constructions, no other proposals."""
from pathlib import Path
import json,hashlib,datetime,re
import numpy as np
import pymupdf
from shapely.geometry import Polygon,LineString,Point,shape,mapping,box
from shapely import union_all,set_precision
from shapely.ops import split

ROOT=Path('/Users/um-yunsang/CHOOGuard')
WORK=ROOT/'.state/ACTIVE-2F-BLUE-ONLY-VECTOR-CONSTRUCTION-R24'
BASE=ROOT/'.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/cad/r19/CURRENT-LIVE-2F-OBJECT-VECTOR-CAD-R19-NO-TEXT.json'
cad=json.loads(BASE.read_text())
struct=json.loads((ROOT/'.state/ACTIVE-2F-SHOP-LOCATION-REVIEW-R20/geometry-lane/STRUCTURAL-ANCHORS-AND-CELLS-R20.json').read_text())
floor=shape(struct['floor']['geometry'])
anno=json.loads((WORK/'inputs/USER-ANNOTATIONS-LATEST-R24.json').read_text())

def pieces(g,kind='Polygon'):
    if g.is_empty:return []
    if g.geom_type==kind:return [g]
    return [p for c in getattr(g,'geoms',[]) for p in pieces(c,kind)]
def arr(p):return np.array(p,dtype=float)
def cross(a,b):return a[0]*b[1]-a[1]*b[0]
def intersect(p,d,q,e):
    p,d,q,e=map(arr,[p,d,q,e]);return p+cross(q-p,e)/cross(d,e)*d
def line(p,d,extent=250):
    p,d=arr(p),arr(d);d=d/np.linalg.norm(d);return LineString([p-d*extent,p+d*extent])
def ray_boundary(origin,direction,geom):
    p,d=arr(origin),arr(direction);d=d/np.linalg.norm(d)
    hit=geom.boundary.intersection(LineString([p+d*.001,p+d*250]))
    pts=pieces(hit,'Point')
    if not pts:
        for ln in pieces(hit,'LineString'):pts.extend(Point(x) for x in [ln.coords[0],ln.coords[-1]])
    forward=[x for x in pts if np.dot(arr(x.coords[0])-p,d)>1e-5]
    return arr(min(forward,key=lambda x:x.distance(Point(p))).coords[0])

# Existing segment extension followed by two actual-object connection datums.
P0=arr([32.4064,-30.8655])
source_floor_edge=[P0.tolist(),[20.5556,-30.8655]]
columns=struct['generated_2F_columns_UNCONFIRMED']
col075=next(a for a in columns if '/C177_UPPER_075_' in a['path'])
col077=next(a for a in columns if '/C177_UPPER_077_' in a['path'])
P1=arr([col075['centroid_qx_minus_qy'][0],P0[1]])
P2=arr(col077['centroid_qx_minus_qy'])
native_upper_segment=arr([[88.523,-24.538],[91.353,-24.552]])
u2=native_upper_segment[1]-native_upper_segment[0]
east_qx=92.272
P3=intersect(P2,u2,[east_qx,0],[0,1])
front=[P0.tolist(),P1.tolist(),P2.tolist(),P3.tolist()]
raw=Polygon(front+[[P3[0],-70],[P0[0],-70]])
gross=union_all([p for p in pieces(raw.intersection(floor)) if p.area>.05])

# First normal starts on the actual092east face, rather than at a pen endpoint.
col092=next(a for a in columns if '/C177_UPPER_092_' in a['path'])
x0=shape(col092['geometry']).bounds[2]
L0=line([x0,P0[1]],[0,-1])

# The next normal is perpendicular to a REAL lower facade segment. P1 is the
# intersection of the actual075structural axis and the extended old floor edge.
rear_reference=arr([[51.70857,-43.23619],[56.10335,-42.16487]])
rt=rear_reference[1]-rear_reference[0]
n1=arr([rt[1],-rt[0]])
L1=line(P1,n1)

# Third separator: exact existing floor bend + normal to the matching native
# upper-facade direction, keeping the slight actual structural inclination.
rear_bend=arr([62.00819,-40.63989])
n2=arr([u2[1],-u2[0]])
L2=line(rear_bend,n2)

# Stepped separator: existing native0453west-face axis intersects H1. Follow its
# exact front normal, bisect the actual front-to-rear span, then return parallel
# to H2 and intersect the original axis. No arbitrary pen kink length is used.
col0453=next(a for a in columns if 'O5-NATIVE-COL-COMP-0453_2F_NATIVE_EXTENSION' in a['path'])
x3=shape(col0453['geometry']).bounds[0]
t1=P2-P1
A=intersect(P1,t1,[x3,0],[0,1])
n3=arr([t1[1],-t1[0]]);n3=n3/np.linalg.norm(n3)
far=ray_boundary(A,n3,gross)
B=(A+far)/2
C=intersect(B,u2,[x3,0],[0,1])
D=ray_boundary(C,n2,gross)
L3=LineString([(A-n3*3).tolist(),B.tolist(),C.tolist(),(D+n2/np.linalg.norm(n2)*3).tolist()])
dividers=[L0,L1,L2,L3]

cells=pieces(gross)
for divider in dividers:
    cells=[part for p in cells for part in pieces(split(p,divider)) if part.area>.05]
cells=sorted(cells,key=lambda p:p.centroid.x)
solids=union_all([shape(a['geometry']) for a in [*columns,*struct['fixed_saved_walls'],*struct['transport_matching_level_profiles']] if a.get('geometry')])
net=gross.difference(solids)

constructions=[
 {'id':'H0','operation':'EXTEND_EXISTING_SEGMENT_TO_OBJECT_AXIS','source_segment':source_floor_edge,'object':col075['object_id'],'intersection':P1.tolist()},
 {'id':'H1','operation':'CONNECT_ACTUAL_OBJECT_DATUMS','start':P1.tolist(),'end':P2.tolist(),'end_object':col077['object_id']},
 {'id':'H2','operation':'PARALLEL_TO_EXISTING_SEGMENT_THROUGH_OBJECT_INTERSECT_EAST_AXIS','source_segment':native_upper_segment.tolist(),'start':P2.tolist(),'end':P3.tolist()},
 {'id':'L0','operation':'NORMAL_TO_H0_THROUGH_ACTUAL_092_EAST_FACE','object':col092['object_id'],'point':[x0,P0[1]],'line':mapping(L0)},
 {'id':'L1','operation':'NORMAL_TO_ACTUAL_REAR_FACADE_THROUGH_H0_H1_INTERSECTION','source_segment':rear_reference.tolist(),'point':P1.tolist(),'line':mapping(L1)},
 {'id':'L2','operation':'NORMAL_TO_NATIVE_H2_THROUGH_ACTUAL_FLOOR_BEND','floor_bend':rear_bend.tolist(),'line':mapping(L2)},
 {'id':'L3','operation':'OBJECT_AXIS_INTERSECTION_FRONT_NORMAL_BISECTOR_PARALLEL_RETURN','object':col0453['object_id'],'object_west_qx':x3,
  'front_intersection_A':A.tolist(),'normal_rear_intersection':far.tolist(),'equal_division_B':B.tolist(),
  'return_axis_intersection_C':C.tolist(),'rear_intersection_D':D.tolist(),'line':mapping(L3),
  'equal_halves_MODEL_m':[float(np.linalg.norm(A-B)),float(np.linalg.norm(B-far))]},
]

west_path=WORK/'west-blue-lane/WEST-BLUE-PRECISE-VECTORS-R24.json'
west=json.loads(west_path.read_text()) if west_path.exists() else None
groups=[{'id':'USER_BLUE_SOUTHEAST','geometry':mapping(net),'gross_geometry':mapping(gross),
         'construction':constructions,'partition_work_cells':[mapping(p.difference(solids)) for p in cells],
         'semantic_scope':'User blue work region only; no tenant identity or exact leased-area assertion'}]
if west:
    groups.append({'id':'USER_BLUE_WEST','geometry':west['geometry'],
                   'construction':{'segments':west.get('segments'),'anchors':west.get('anchors'),'formulas':west.get('formulas')},
                   'source_packet':str(west_path.relative_to(ROOT))})

# Preserve the latest PDF and every nonblue annotation, removing ALL references
# to old blue appearances (one old annotation is duplicated in the Annots array).
source=WORK/'inputs/USER-MARKED-R23-LATEST.pdf'
doc=pymupdf.open(source);page=doc[0]
blue_ids={a['xref'] for a in anno['annotations'] if a['blue']}
keep=[a['xref'] for a in anno['annotations'] if not a['blue']]
doc.xref_set_key(page.xref,'Annots','['+' '.join(f'{x} 0 R' for x in keep)+']')
page=doc.reload_page(page)
scale,dx,dy,ph=anno['scale'],anno['dx'],anno['dy'],anno['pdf_height']
def pp(xy):
    x,y=xy;return pymupdf.Point(x*scale+dx,ph-(y*scale+dy))

draw=page.new_shape()
for group in groups:
    for poly in pieces(shape(group['geometry'])):
        for ring in [poly.exterior,*poly.interiors]:draw.draw_polyline([pp(p) for p in ring.coords])
draw.finish(color=(0.015,.20,1),fill=None,width=.75,closePath=False)
draw.commit(overlay=True)
draw=page.new_shape()
for divider in dividers:
    for ln in pieces(divider.intersection(gross),'LineString'):
        draw.draw_polyline([pp(p) for p in ln.coords])
draw.finish(color=(0.015,.20,1),fill=None,width=.75,closePath=False);draw.commit(overlay=True)

# Closeups contain only original gray CAD plus the corrected blue vector work;
# page1 keeps all untouched red/green annotations editable in their original slots.
for bounds in [(30,-47,94,-19),(-66,29,-59,36)]:
    x0,y0,x1,y1=bounds
    z=min(1150.55/(x1-x0),801.89/(y1-y0))
    zx=(1190.55-(x1-x0)*z)/2-x0*z;zy=(841.89-(y1-y0)*z)/2-y0*z
    def zz(p):return pymupdf.Point(p[0]*z+zx,841.89-(p[1]*z+zy))
    target=doc.new_page(width=1190.55,height=841.89)
    area=box(*bounds)
    # Redraw actual gray vectors with a constant paper line width on closeups.
    # This changes no coordinates and avoids magnifying pen width into a wall.
    surf=target.new_shape();cuts=target.new_shape()
    for obj in cad['objects']:
        for ring in obj['surface_rings']:
            for ln in pieces(LineString(ring).intersection(area),'LineString'):
                surf.draw_polyline([zz(p) for p in ln.coords])
        seg=obj['sections'].get('8.40000',[])
        if not seg and obj['kind']=='CURRENT':
            low=[(float(k),v) for k,v in obj['sections'].items() if 7.02<float(k)<8.4]
            if low:seg=max(low,key=lambda x:x[0])[1]
        for a,b in seg:
            if max(a[0],b[0])<x0 or min(a[0],b[0])>x1 or max(a[1],b[1])<y0 or min(a[1],b[1])>y1:continue
            for ln in pieces(LineString([a,b]).intersection(area),'LineString'):
                cuts.draw_polyline([zz(p) for p in ln.coords])
    surf.finish(color=(.7,.7,.7),width=.22,closePath=False);surf.commit()
    cuts.finish(color=(.4,.4,.4),width=.32,closePath=False);cuts.commit()
    work=target.new_shape()
    for group in groups:
        for poly in pieces(shape(group['geometry']).intersection(area)):
            for ring in [poly.exterior,*poly.interiors]:work.draw_polyline([zz(p) for p in ring.coords])
    for divider in dividers:
        for ln in pieces(divider.intersection(gross).intersection(area),'LineString'):
            work.draw_polyline([zz(p) for p in ln.coords])
    work.finish(color=(.015,.20,1),width=1.0,closePath=False);work.commit(overlay=True)
stem=WORK/'BLUE-ONLY-CAD-LINE-CONSTRUCTION-R24-NO-TEXT'
doc.set_metadata({'title':'Blue marked regions only - exact CAD constructions R24'})
doc.save(stem.with_suffix('.pdf'),garbage=4,deflate=True)

record={'schema':'chooguard.r24.blue_only_exact_CAD_construction.v1','utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'status':'USER_REVIEW_ONLY_NOT_SCENE_APPLIED','source_user_pdf':str(source.relative_to(ROOT)),
        'source_user_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
        'gray_reference':str(BASE.relative_to(ROOT)),'gray_reference_sha256':hashlib.sha256(BASE.read_bytes()).hexdigest(),
        'only_blue_regions_changed':True,'nonblue_user_annotations_preserved':len(keep),
        'old_blue_appearances_replaced':len(blue_ids),'groups':groups,
        'southeast_divider_count':4,'southeast_work_cells':len(cells),
        'unmarked_older_orange_proposals_included':False,'source_pen_shape_used_as_final_metric':False,
        'added_visible_text':False,'Scene_Assets_changed':False,'Scene_QA_or_new_captures':False,
        'pdf_pages':len(doc),'Preview_opened':False}
stem.with_suffix('.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')

# Base DXF entity payload stays verbatim; append only current blue constructions.
dxf=BASE.with_suffix('.dxf').read_text();footer='0\nENDSEC\n0\nEOF\n'
if not dxf.endswith(footer):raise ValueError('Unexpected base DXF footer')
entities=[];layers=[]
def emit(*v):entities.extend(str(x) for x in v)
def polyline(coords,layer,closed):
    emit(0,'POLYLINE',8,layer,62,5,66,1,70,int(closed),10,0,20,0,30,0)
    seq=list(coords)
    if closed and seq[0]==seq[-1]:seq=seq[:-1]
    for x,y in seq:emit(0,'VERTEX',8,layer,62,5,10,f'{x:.9f}',20,f'{y:.9f}',30,0)
    emit(0,'SEQEND',8,layer)
for group in groups:
    layer='R24_'+group['id'];layers += [0,'LAYER',2,layer,70,0,62,5,6,'CONTINUOUS']
    for poly in pieces(shape(group['geometry'])):
        for ring in [poly.exterior,*poly.interiors]:polyline(ring.coords,layer,True)
    if group['id']=='USER_BLUE_SOUTHEAST':
        for divider in dividers:
            for ln in pieces(divider.intersection(gross),'LineString'):polyline(ln.coords,layer,False)
tokens=dxf.splitlines();start=tokens.index('LAYER');tokens[start+2]=str(int(tokens[start+2])+len(groups))
end=next(i for i in range(start,len(tokens)-1) if tokens[i]=='0' and tokens[i+1]=='ENDTAB')
tokens[end:end]=[str(x) for x in layers];dxf='\n'.join(tokens)+'\n'
stem.with_suffix('.dxf').write_text(dxf[:-len(footer)]+'\n'.join(entities)+'\n'+footer)
print(json.dumps({'pdf':str(stem.with_suffix('.pdf').relative_to(ROOT)),'blue_groups':len(groups),'preserved_nonblue':len(keep),
                  'work_cells_SE':len(cells),'half_lengths':constructions[-1]['equal_halves_MODEL_m'],'Scene_Assets_changed':False},ensure_ascii=False))
