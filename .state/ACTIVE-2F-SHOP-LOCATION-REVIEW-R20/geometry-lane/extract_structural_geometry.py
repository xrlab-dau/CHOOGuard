from pathlib import Path
import json,re,math,hashlib,datetime
import numpy as np
from shapely import union_all,set_precision,make_valid
from shapely.geometry import Polygon,LineString,box,mapping,GeometryCollection
from shapely.ops import polygonize
from scipy.spatial import Delaunay
from PIL import Image,ImageDraw
ROOT=Path('/Users/um-yunsang/CHOOGuard');OUT=ROOT/'.state/ACTIVE-2F-SHOP-LOCATION-REVIEW-R20/geometry-lane'
pointer=json.loads((ROOT/'.state/CURRENT-2F-VECTOR-CAD-R19.json').read_text());data=json.loads((ROOT/pointer['data']).read_text());source=json.loads((ROOT/pointer['source']).read_text());Q=np.array(data['worldXZ_to_q']);sm={x['object_id']:x for x in source['objects']}
def polys(g):
 if g.is_empty:return []
 if g.geom_type=='Polygon':return [g]
 return [p for gg in getattr(g,'geoms',[]) for p in polys(gg)]
def geom_rings(rings):
 g=GeometryCollection()
 for ring in sorted(rings,key=lambda r:abs(Polygon(r).area),reverse=True):
  p=make_valid(Polygon(ring));g=g.symmetric_difference(p)
 return make_valid(g)
def recgeom(g):
 return {'type':'MultiPolygon','coordinates':[[[list(c) for c in p.exterior.coords]]+[[list(c) for c in h.coords] for h in p.interiors] for p in polys(g)]}
def prof(obj,level='8.40000'):
 seg=obj['sections'].get(level,[])
 if not seg:return GeometryCollection()
 lines=[LineString([[round(v,4) for v in a],[round(v,4) for v in b]]) for a,b in seg]
 return union_all(list(polygonize(union_all(lines,grid_size=.0001))),grid_size=.0001)
def objrecord(o,g,authority):
 s=sm[o['object_id']]
 return {'object_id':o['object_id'],'path':o['path'],'mesh_asset':o['mesh_asset'],'kind':o['kind'],'authority':authority,'bounds_world':[s['bounds_min_world'],s['bounds_max_world']],'geometry':recgeom(g),'centroid_qx_minus_qy':list(g.centroid.coords)[0] if not g.is_empty else None,'bounds_qx_minus_qy':list(g.bounds) if not g.is_empty else None,'area_m2':g.area}
objects=data['objects'];floors=[];official6=[];native_columns=[];gen_columns=[];nativecompact=[];trans=[];walls=[];wallgeoms=[];sections=[];existingregions=[]
for o in objects:
 path=o['path'];s=sm[o['object_id']];g=geom_rings(o['surface_rings']) if o['surface_rings'] else GeometryCollection()
 if o['kind']=='CURRENT' and re.search(r'/MAIN2F_(APPROVED_|SOURCE_BOUND_MISSING_FLOOR)',path):floors.append((o,g))
 if o['kind']=='OFFICIAL' and re.search(r'OfficialStation_-부산역_6(?:/|$)',path):official6.append((o,g,prof(o)))
 if o['kind']=='OFFICIAL' and 'COMPLETE_NATIVE_BODY_C116' in path:native_columns.append(objrecord(o,g,'FIXED_ACTUAL_1F_BODY_TOP_PROFILE; projection_reference_only_not_upper_body_proof'))
 if o['kind']=='CURRENT' and any(k in path for k in ['MAIN2F_NATIVE_COLUMNS_SPLIT','MAIN2F_FROM_CURRENT_1F_COLUMNS','MAIN2F_OFFICIAL5_NATIVE_COLUMNS_EXTENDED','MAIN2F_USER_MARKED_MISSING_COLUMNS']):
  pg=prof(o);gen_columns.append(objrecord(o,pg,'CURRENT_GENERATED_2F_COLUMN_POSE_UNCONFIRMED_NOT_CONFIRMED_BASE_ANCHOR'))
 if o['kind']=='TRANSPORT' or any(k in path for k in ['MAIN2F_FIVE_ELEVATORS','MAIN2F_NATIVE_BOARDING_ELEVATOR_EXTERIORS','MAIN2F_C97_OFFICIAL_DONOR_CIRCULATION']):
  pg=prof(o,'7.15000')
  if pg.is_empty:pg=prof(o)
  if not pg.is_empty:trans.append((o,pg))
 if o['kind']=='OFFICIAL' and '/MainShell/' in path:
  pg=prof(o)
  for j,p in enumerate(polys(pg)):
   b=p.bounds;ratio=max(b[2]-b[0],b[3]-b[1])/max(.001,min(b[2]-b[0],b[3]-b[1]));compact=4*math.pi*p.area/max(p.length*p.length,1e-9)
   if .15<p.area<12 and ratio<2.8 and compact>.35:
    nativecompact.append({'owner_object_id':o['object_id'],'owner_path':path,'profile_index':j,'section_worldY':8.4,'centroid_qx_minus_qy':list(p.centroid.coords)[0],'profile':recgeom(p),'area_m2':p.area,'classification':'ACTUAL_NATIVE_COMPACT_SECTION; column_identity_not_individually_qualified'})
  # Individual native enclosed cells, actual matching-level only; never allheight hull.
  for j,p in enumerate(polys(pg)):
   if p.area>12:existingregions.append({'owner_object_id':o['object_id'],'owner_path':path,'section_worldY':8.4,'geometry':recgeom(p),'area_m2':p.area,'function':'EXISTING_NATIVE_SECTION_REGION; occupancy_unknown; fixed_no_reconstruction'})
 if o['kind']=='CURRENT' and any(k in path for k in ['MAIN2F_USER_MARKED_WALLS_R11','MAIN2F_MARKED_FROM_ACTUAL_LOWER_WALLS_R5']):
  pg=prof(o)
  if not pg.is_empty:
   walls.append(objrecord(o,pg,'CURRENT_SAVED_GENERATED_WALL_FIXED_REFERENCE'));wallgeoms.append(pg)
  sections.append(o)
floor=union_all([g for _,g in floors],grid_size=.00001);official6floor=union_all([g for _,g,_ in official6],grid_size=.00001);official6walls=union_all([g for _,_,g in official6],grid_size=.0001);transport=union_all([g for _,g in trans],grid_size=.0001)
# Protected WC bounds derive actual matching-level geometry, not a root/world bounding box.
wcparts=[]
for o in objects:
 if 'MAIN2F_REPLACED_FROM_CURRENT_1F_WCS' in o['path']:
  pg=prof(o)
  if not pg.is_empty:wcparts.append(pg)
wcgeom=union_all(wcparts,grid_size=.0001)
# Polygonized rooms/cells arise only from existing wall, native sections and exact currentfloor boundary. No source-image fit, no invented retail strips.
linework=[]
for o in objects:
 if o['kind']=='OFFICIAL' and '/MainShell/' in o['path'] or o in sections:
  for level in ['8.40000','10.50000']:
   for a,b in o['sections'].get(level,[]):linework.append(LineString([[round(v,3) for v in a],[round(v,3) for v in b]]))
linework.extend([p.exterior for p in polys(floor)])
merged=union_all(linework,grid_size=.001);cells=list(polygonize(merged));candidates=[];rejected=[]
for cell in cells:
 if not 15<cell.area<650:continue
 ratio=cell.intersection(floor).area/cell.area
 if ratio<.97:continue
 if cell.intersection(official6floor).area>.05 or cell.intersection(official6walls).area>.05:continue
 if cell.intersection(transport).area>.25 or cell.intersection(wcgeom).area>.25:continue
 compact=4*math.pi*cell.area/max(cell.length*cell.length,1e-9)
 if compact<.08:continue
 # A thin wall slab is never a retail cell.
 rect=cell.minimum_rotated_rectangle;cc=list(rect.exterior.coords);edges=[math.dist(cc[i],cc[i+1]) for i in range(4)]
 if min(edges)<2.0:continue
 boundary=cell.boundary
 nearbywalls=[w['object_id'] for w,g in zip(walls,wallgeoms) if boundary.distance(g)<.05]
 nearbyfixed=[c['object_id'] for c in native_columns if not c['geometry']['coordinates']==[] and boundary.distance(geom_rings([ring for poly in c['geometry']['coordinates'] for ring in poly]))<1.5]
 candidates.append({'cell_id':f'R20_STRUCTURAL_CELL_{len(candidates)+1:03d}','geometry':recgeom(cell),'area_m2':cell.area,'centroid_qx_minus_qy':list(cell.centroid.coords)[0],'bounds_qx_minus_qy':list(cell.bounds),'minimum_rectangle_spans_m':[min(edges),max(edges)],'current_floor_coverage_fraction':ratio,'boundary_wall_object_ids':nearbywalls,'nearby_fixed_1F_native_column_ids':nearbyfixed,'status':'STRUCTURALLY_ENCLOSED_REVIEW_CELL_NOT_VACANCY_OR_RETAIL_IDENTITY_AUTHORITY','occupancy_verified':False,'source_shop_identity':None,'frontage_to_public_circulation':'not_yet_qualified'})
# Exact floor holes and exterior-connected recesses are exported without filling.
holes=[Polygon(r) for p in polys(floor) for r in p.interiors]
anchors={'schema':'chooguard.r20.structural_review_geometry.v1','coordinate_convention':{'units':'metres','plot_coordinates':'[qx,-qy]','worldXZ_to_q':Q.tolist(),'worldXZ_to_plot':[Q[0].tolist(),(-Q[1]).tolist()],'worldY_2F_floor_reference':7.0,'inverse_plot_to_worldXZ':np.linalg.inv(np.array([Q[0],-Q[1]])).tolist()},'frozen_authority':pointer,'source_sha256_actual':hashlib.sha256((ROOT/pointer['source']).read_bytes()).hexdigest(),'cad_sha256':hashlib.sha256((ROOT/pointer['data']).read_bytes()).hexdigest(),'R17_trim_preserved':True,'render_only_LOD1_excluded':72,'floor':{'geometry':recgeom(floor),'area_m2':floor.area,'contributing_object_ids':[o['object_id'] for o,_ in floors],'exterior_rings_count':len(polys(floor)),'interior_openings':[recgeom(p) for p in holes],'opening_semantics':'Actualcurrent absence only; transport identity/root association requires spatial evidence; exterior-connected recesses remain perimeter topology.'},'official6_protected':{'object_ids':[o['object_id'] for o,_,_ in official6],'actual_Y7_surface_geometry':recgeom(official6floor),'actual_Y8p4_section_geometry':recgeom(official6walls),'allheight_bbox_used_as_occupancy':False,'rule':'Fixed existing geometry. No shop/wall/column proposal overlapping actual protected matching-level regions. No inferred replacement staff occupancy.'},'fixed_native_1F_column_top_profiles':native_columns,'generated_2F_columns_UNCONFIRMED':gen_columns,'native_compact_Y8p4_sections_UNQUALIFIED_IDENTITY':nativecompact,'fixed_saved_walls':walls,'transport_matching_level_profiles':[objrecord(o,g,'CURRENT_FIXED_TRANSPORT_REFERENCE') for o,g in trans],'existing_native_enclosed_section_regions':existingregions,'structurally_bounded_review_cells':candidates,'limitations':['Source does not include full original1F C141column meshes; C177 lineage names are not live1F source readback.','Nativecompact section loops are actual geometry but are not automatically confirmed column identities.','No vacant retail occupancy or currentstore identity can be established from empty-looking floor alone.','FrozenR19contains no explicitly named individual2Fstore shell hierarchy; actual native closed regions and generated wallreturns are retained as existing geometry references.','No source colored-box dimensions/centres or floor-shape inference used.'],'Scene_Assets_changed':False,'currentCAD_changed':False}
(OUT/'STRUCTURAL-ANCHORS-AND-CELLS-R20.json').write_text(json.dumps(anchors,ensure_ascii=False,separators=(',',':'))+'\n')
# Gray-only overview: all native/current sections, actualfloor and candidatecell outlines; no visible text.
minx,miny,maxx,maxy=floor.bounds
scale=min(1900/(maxx-minx+6),1400/(maxy-miny+6)); W=int((maxx-minx+6)*scale);H=int((maxy-miny+6)*scale)
im=Image.new('RGB',(W,H),'white');dr=ImageDraw.Draw(im)
def pixel(p):return ((p[0]-minx+3)*scale,H-(p[1]-miny+3)*scale)
for p in polys(floor):
 dr.polygon([pixel(v) for v in p.exterior.coords],fill=(245,245,245),outline=(100,100,100))
 for h in p.interiors:dr.polygon([pixel(v) for v in h.coords],fill='white',outline=(120,120,120))
for p in polys(official6floor):dr.polygon([pixel(v) for v in p.exterior.coords],fill=(210,210,210))
for o in objects:
 if o['kind']=='OFFICIAL' and '/Context' in o['path']:continue
 for a,b in o['sections'].get('8.40000',[]):dr.line([pixel(a),pixel(b)],fill=(110,110,110) if o['kind']=='OFFICIAL' else (170,170,170),width=1)
for c in candidates:
 for poly in c['geometry']['coordinates']:dr.line([pixel(v) for v in poly[0]],fill=(30,30,30),width=2)
im.save(OUT/'GRAY-STRUCTURAL-OVERVIEW-R20.png')
summary={'fixed_native1Fcolumn_profiles':len(native_columns),'unconfirmed_generated2Fcolumns':len(gen_columns),'actual_native_compact_sections_not_confirmedcolumns':len(nativecompact),'wall_profiles':len(walls),'transport_profiles':len(trans),'existing_native_enclosed_regions':len(existingregions),'structural_review_cells':len(candidates),'floor_area_m2':floor.area,'floor_bounds':floor.bounds,'floor_holes':len(holes),'candidate_cells':[{k:c[k] for k in ['cell_id','area_m2','centroid_qx_minus_qy','minimum_rectangle_spans_m','boundary_wall_object_ids']} for c in candidates]}
(OUT/'SUMMARY.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n');print(json.dumps(summary,ensure_ascii=False))
