import json, math, hashlib
from pathlib import Path
from shapely.geometry import shape, box, Polygon
from shapely.ops import triangulate
D=Path(__file__).parent
A=json.loads((D/'APPROVED-STORE-WORK-REGION.json').read_text()); region=shape(A['geometry_qx_minus_qy'])
X,Y=43.3742,-30.8655
objects=[]
materials={
 'navy':{'color':[.135,.145,.19,1],'smoothness':.4},
 'navyrib':{'color':[.19,.20,.255,1],'smoothness':.42},
 'ivory':{'color':[.91,.915,.90,1],'smoothness':.28},
 'tile':{'color':[.92,.925,.92,1],'smoothness':.18},
 'white_metal':{'color':[.9,.91,.90,1],'metallic':.52,'smoothness':.54},
 'steel':{'color':[.53,.55,.57,1],'metallic':.86,'smoothness':.7},
 'dark':{'color':[.055,.06,.068,1],'smoothness':.5},
 'glass':{'color':[.72,.86,.86,.10],'transparent':True,'smoothness':.97},
 'coolerglass':{'color':[.60,.79,.82,.16],'transparent':True,'smoothness':.95},
 'lime':{'color':[.64,.82,.16,1],'smoothness':.32},
 'lime_light':{'color':[.70,.90,.23,1],'emission':[.30,.43,.07,1],'smoothness':.4},
 'blue':{'color':[.20,.47,.66,1],'smoothness':.4},
 'yellow':{'color':[.92,.67,.18,1],'smoothness':.35},
 'orange':{'color':[.91,.28,.055,1],'smoothness':.4},
 'oak':{'color':[.61,.43,.26,1],'smoothness':.25},
 'oak_dark':{'color':[.23,.16,.10,1],'smoothness':.3},
 'red':{'color':[.77,.025,.04,1],'smoothness':.44},
 'pale_pink':{'color':[.93,.68,.73,1],'smoothness':.3},
 'green_bottle':{'color':[.08,.24,.12,1],'smoothness':.72},
 'brown_bottle':{'color':[.22,.13,.07,1],'smoothness':.68},
 'water_blue':{'color':[.48,.73,.80,1],'smoothness':.75},
 'paper':{'color':[.94,.93,.86,1],'smoothness':.15},
 'LED':{'color':[.96,.98,1,1],'emission':[.9,.95,1,1],'smoothness':.3},
}
SRC=['KTODO-2F-FRONT','WALK2023-268P5','WALK2023-272P5']
def q(u,d):return [X+u,Y-d]
def part(kind,id,group,u,d,y,material=None,**kw):
 o={'kind':kind,'id':id,'group':group,'q':q(u,d),'y':y,'source_ids':SRC,'qualification':'SOURCE_OBSERVED_MORPHOLOGY_WITH_MODEL_METRIC_FIT_INSIDE_APPROVED_R24'}
 if material:o['material']=material
 o.update(kw);objects.append(o);return o
def cube(id,g,u,d,y,sx,sy,sz,m,**kw):return part('box',id,g,u,d,y,m,size=[sx,sy,sz],**kw)
def cyl(id,g,u,d,y,diam,h,m,**kw):return part('cylinder',id,g,u,d,y,m,size=[diam,h/2,diam],**kw)
def edge(id,g,a,b,bottom,h,t,m,**kw):
 return part('edge',id,g,0,0,0,m,a=a,b=b,bottom=bottom,height=h,thickness=t,**kw)
def text(id,g,t,u,d,y,w,h,col=(.92,.94,.91,1),yaw=0):return part('text',id,g,u,d,y,text=t,width=w,height=h,color=list(col),yaw=yaw,collider=False)
def prism(id,g,poly,bottom,top,m):
 # Explicit closed prism; geometric points remain inside exact approved polygon.
 vs=[];ts=[]
 def tri(a,b,c):
  i=len(vs);vs.extend([a,b,c]);ts.extend([i,i+1,i+2])
 for tr in triangulate(poly):
  if not poly.covers(tr.representative_point()):continue
  pts=list(tr.exterior.coords)[:3]
  aa=[[xx-X,bottom,yy-Y] for xx,yy in pts];bb=[[xx-X,top,yy-Y] for xx,yy in pts]
  tri(aa[0],aa[1],aa[2]);tri(bb[2],bb[1],bb[0])
 ring=list(poly.exterior.coords)
 for (ax,ay),(bx,by) in zip(ring,ring[1:]):
  a=[ax-X,bottom,ay-Y];b=[bx-X,bottom,by-Y];c=[bx-X,top,by-Y];d=[ax-X,top,ay-Y];tri(a,b,c);tri(a,c,d)
 return part('mesh',id,g,0,0,0,m,vertices=vs,triangles=ts,collider=False)
# Exact approved boundary retained. Native curved facade remains the rear enclosure; no duplicate outer wall.
# New side partition bodies are entirely inward from their construction boundary.
edge('left_party_partition','Booth_Partitions',q(.065,.065),q(.065,13.78),0,3.42,.12,'ivory')
# right flank follows actual R24 angled divider, offset inward, joining source-derived frontage.
edge('right_party_partition','Booth_Partitions',[50.90,Y-.12],[53.72,-42.63],0,3.42,.12,'ivory')
W=7.64355046546757
cube('navy_fascia_main','Front_Fascia',W/2,.115,2.96,W-.10,.88,.19,'navy')
for i in range(7):cube('continuous_upper_fascia_rib_%02d'%i,'Front_Fascia',W/2,.055,3.02+i*.052,W-.12,.021,.035,'navyrib',collider=False)
# Full glass wall; automatic door parked open at each side of a 2.6m public opening.
for key,a,b in [('left',.15,2.17),('right',4.97,W-.12)]:
 edge('fixed_clear_glass_'+key,'Front_Glass',q(a,.12),q(b,.12),.06,2.41,.015,'glass',cast_shadows=False)
 for u in [a,b]:cube('glass_stile_'+key+'_'+str(u),'Front_Frames',u,.12,1.26,.045,2.52,.065,'steel')
 cube('glass_bottom_rail_'+key,'Front_Frames',(a+b)/2,.12,.06,b-a,.045,.065,'steel')
cube('automatic_door_header','Automatic_Entry',W/2,.135,2.49,W-.2,.12,.17,'steel')
for key,u in [('left',1.48),('right',5.66)]:
 cube('sliding_leaf_glass_'+key,'Automatic_Entry',u,.205,1.245,1.32,2.34,.012,'glass',cast_shadows=False)
 for du in [-.66,.66]:cube('sliding_leaf_vertical_'+key+'_'+str(du),'Automatic_Entry',u+du,.205,1.245,.035,2.38,.045,'steel')
 cube('sliding_leaf_bottom_'+key,'Automatic_Entry',u,.205,.065,1.355,.035,.045,'steel')
 cube('sliding_leaf_top_'+key,'Automatic_Entry',u,.205,2.42,1.355,.035,.045,'steel')
cube('automatic_sensor','Automatic_Entry',W/2,.065,2.53,.16,.07,.08,'dark',collider=False)
# Signs are modeled typography and physical icon parts, no source photograph billboard.
text('physical_StoryWay_wordmark','Modeled_Signs','<b>StoryWay</b>',W/2+.28,.030,2.94,3.07,.47,(.72,.91,.29,1))
for i in range(14):
 angle=math.radians(205+i*10.1);u=1.72+math.cos(angle)*.225;h=2.96+math.sin(angle)*.225
 cyl('smile_arc_dot_%02d'%i,'Modeled_Signs',u,.052,h,.057,.035,'lime_light',euler=[90,0,0],collider=False)
for i in range(5):
 angle=math.radians(20+i*35);u=1.72+math.cos(angle)*.23;h=2.96+math.sin(angle)*.23
 cyl('smile_upper_dot_%02d'%i,'Modeled_Signs',u,.052,h,.065,.035,'lime_light',euler=[90,0,0],collider=False)
# Left window round convenience identifier, clearly seen in frontal source.
cyl('convenience_round_backing','Modeled_Signs',.92,.082,1.96,.72,.018,'dark',euler=[90,0,0],collider=False)
text('convenience_round_lettering','Modeled_Signs','편의점',.92,.060,2.02,.61,.19)
text('convenience_round_storyway','Modeled_Signs','StoryWay',.92,.059,1.80,.47,.10)
# Ceiling follows approved polygon; 600mm fine T-bar tiles are clipped individually.
prism('own_ceiling_base_exact_R24','Own_Ceiling',region.buffer(-.07),3.06,3.10,'ivory')
for ix in range(17):
 for iz in range(24):
  u=.4+ix*.6;d=.4+iz*.6;cell=box(X+u-.295,Y-d-.295,X+u+.295,Y-d+.295).intersection(region.buffer(-.09))
  if cell.is_empty or cell.area<.02:continue
  mat='LED' if ix in [2,6,10,14] and iz in [2,6,10,14,18] else 'tile'
  if cell.geom_type=='Polygon':prism('tile_%02d_%02d'%(ix,iz),'Own_Ceiling_Tiles',cell,3.018,3.045,mat)
for u,d in [(1.6,2.2),(5.2,2.2),(1.6,6.4),(5.2,6.4),(3.4,10)]:part('light','interior_observed_LED_'+str(u)+'_'+str(d),'Own_Observed_Lighting',u,d,2.98,color=[.97,.985,1,1],range=4,intensity=.32)
# White 5-tier centre gondola: same observed long central rack, not uniform cloned store layouts.
G='Central_Observed_Gondola';gu,gd=3.80,4.65
cube('gondola_plinth',G,gu,gd,.105,1.25,.21,4.2,'white_metal')
cube('gondola_mid_back',G,gu,gd,.92,.055,1.52,4.14,'ivory')
for side in [-1,1]:
 for bay in range(4):
  bd=3.08+bay*1.04
  cube('gondola_upright_%s_%s'%(side,bay),G,gu,bd,.94,.045,1.64,.045,'white_metal')
  for tier in range(5):
   h=.28+tier*.305
   cube('gondola_shelf_%s_%s_%s'%(side,bay,tier),G,gu+side*.31,bd,h,.61,.025,.99,'white_metal')
   cube('gondola_price_rail_%s_%s_%s'%(side,bay,tier),G,gu+side*.605,bd,h+.025,.025,.05,.99,'paper',collider=False)
   # Sample visible stock only: bags/tubes/cartons from actual view, no fabricated SKU/brand labels.
   for item in range(3):
    pd=bd-.30+item*.29;m=['red','yellow','blue','green_bottle','pale_pink','paper'][(tier+bay+item)%6]
    cube('pack_body_%s_%s_%s_%s'%(side,bay,tier,item),'Visible_Stock_Individually_Editable',gu+side*.33,pd,h+.115,.20,.19,.18,m,collider=True)
    cube('pack_label_%s_%s_%s_%s'%(side,bay,tier,item),'Visible_Stock_Individually_Editable',gu+side*.438,pd,h+.13,.008,.068,.13,'paper',collider=False)
# Source free fixture reused as independent endcap model; source original left unmodified.
part('asset','free_existing_metal_endcap','Central_Observed_Endcap',gu,2.25,.02,asset='Assets/ChooGuard/Art/StationInterior/Props/StoreShelf.fbx',fit_size=[1.18,1.51,.44],material='white_metal',yaw=0)
# Rear food wall and side beverage bank, with individually framed glass doors and product shelves.
for id,u,d,w,face,band in [('rear_food',3.8,11.75,6.55,0,'lime'),('right_drink',7.75,7.5,5.7,90,'blue')]:
 # u/d centre. Local basis follows wall; doorface -d forrear, -u forright.
 if face==0:
  cube(id+'_body','Refrigeration_Bank',u,d,1.13,w,2.26,.78,'ivory')
  cube(id+'_top_brandband','Refrigeration_Bank',u,d,2.48,w,.32,.76,band)
  text(id+'_category','Modeled_Interior_Signs','FAST FOOD',u,d-.397,2.45,w*.7,.21,(.9,.91,.88,1),yaw=0)
 else:
  cube(id+'_body','Refrigeration_Bank',u,d,1.13,.78,2.26,w,'ivory')
  cube(id+'_top_brandband','Refrigeration_Bank',u,d,2.48,.76,.32,w,band)
  text(id+'_category','Modeled_Interior_Signs','COOL DRINK',u-.40,d,2.45,w*.7,.21,(.9,.91,.88,1),yaw=270)
 n=7 if face==0 else 6;dw=(w-.12)/n
 for j in range(n):
  offset=-w/2+dw*(j+.5)+.06
  if face==0:
   uu,dd=u+offset,d-.411
   cube(id+'_clear_door_'+str(j),'Refrigeration_Doors',uu,dd,1.22,dw-.038,2.00,.012,'coolerglass',cast_shadows=False)
   for sx in [-1,1]:cube(id+'_door_stile_'+str(j)+'_'+str(sx),'Refrigeration_Doors',uu+sx*dw*.5,dd,1.2,.028,2.10,.055,'steel')
   cube(id+'_handle_'+str(j),'Refrigeration_Doors',uu+dw*.36,dd-.045,1.1,.023,.46,.055,'dark')
  else:
   uu,dd=u-.411,d+offset
   cube(id+'_clear_door_'+str(j),'Refrigeration_Doors',uu,dd,1.22,.012,2.00,dw-.038,'coolerglass',cast_shadows=False)
   for sz in [-1,1]:cube(id+'_door_stile_'+str(j)+'_'+str(sz),'Refrigeration_Doors',uu,dd+sz*dw*.5,1.2,.055,2.10,.028,'steel')
   cube(id+'_handle_'+str(j),'Refrigeration_Doors',uu-.045,dd+dw*.36,1.1,.055,.46,.023,'dark')
  for tier in range(4):
   h=.37+tier*.43
   if face==0:cube(id+'_shelf_%s_%s'%(j,tier),'Refrigeration_Shelves',uu,d,h,dw-.04,.025,.65,'white_metal')
   else:cube(id+'_shelf_%s_%s'%(j,tier),'Refrigeration_Shelves',u,dd,h,.65,.025,dw-.04,'white_metal')
   for item in range(2):
    m=['water_blue','green_bottle','paper','yellow'][(j+tier+item)%4]
    su=uu+((item-.5)*dw*.4 if face==0 else .15);sd=d if face==0 else dd+(item-.5)*dw*.4
    cyl(id+'_stock_body_%s_%s_%s'%(j,tier,item),'Visible_Chilled_Stock',su,sd,h+.14,.10,.25,m,collider=False)
    cyl(id+'_stock_cap_%s_%s_%s'%(j,tier,item),'Visible_Chilled_Stock',su,sd,h+.28,.045,.025,'paper',collider=False)
# Timber wine fixture on public-facing right just inside glass; this visibly differs from central white fixture.
wu,wd=6.34,1.10
cube('timber_wine_back','Timber_Wine_Gift_Unit',wu,wd,1.04,1.35,1.95,.075,'oak')
for u in [wu-.65,wu+.65]:cube('timber_side_'+str(u),'Timber_Wine_Gift_Unit',u,wd-.24,1.03,.075,1.96,.56,'oak')
for tier in range(4):
 h=.17+tier*.43;cube('wine_timber_shelf_'+str(tier),'Timber_Wine_Gift_Unit',wu,wd-.24,h,1.32,.065,.56,'oak')
 for j in range(5):
  u=wu-.48+j*.24;cyl('wine_body_%s_%s'%(tier,j),'Wine_Stock_Individually_Editable',u,wd-.27,h+.17,.095,.27,'brown_bottle' if j%2 else 'green_bottle',collider=True)
  cyl('wine_neck_%s_%s'%(tier,j),'Wine_Stock_Individually_Editable',u,wd-.27,h+.34,.042,.10,'green_bottle',collider=False)
  cube('wine_label_%s_%s'%(tier,j),'Wine_Stock_Individually_Editable',u,wd-.322,h+.17,.078,.115,.004,'paper',collider=False)
# Actual red fire extinguisher at right entry; all parts individually editable.
cyl('red_extinguisher_body','Observed_Safety_Equipment',5.14,.42,.285,.18,.43,'red')
cube('extinguisher_stand','Observed_Safety_Equipment',5.14,.42,.045,.28,.09,.28,'red')
cube('extinguisher_label','Observed_Safety_Equipment',5.14,.323,.30,.11,.16,.007,'paper',collider=False)
cyl('extinguisher_neck','Observed_Safety_Equipment',5.14,.42,.53,.05,.08,'steel')
cube('extinguisher_handle','Observed_Safety_Equipment',5.15,.42,.59,.14,.035,.065,'dark',collider=False)
# Observed capsule dispensers belong inside approved store work region, not public corridor.
for index,(u,m) in enumerate([(5.85,'orange'),(6.58,'blue')]):
 cube('capsule_base_'+str(index),'Observed_Capsule_Dispensers',u,.57,.08,.59,.16,.44,m)
 for h in [.45,.94]:
  cube('capsule_module_%s_%s'%(index,h),'Observed_Capsule_Dispensers',u,.57,h,.57,.43,.40,m)
  cube('capsule_clear_face_%s_%s'%(index,h),'Observed_Capsule_Dispensers',u,.354,h+.05,.46,.24,.012,'glass',collider=False,cast_shadows=False)
  cyl('capsule_turn_%s_%s'%(index,h),'Observed_Capsule_Dispensers',u,.325,h-.11,.09,.035,'steel',euler=[90,0,0],collider=False)
# Bounds are construction arithmetic guards, not Scene QA. Never clip fixed source geometry.
def footprint(o):
 if o['kind'] in ['text','light','mesh']:return None
 if o['kind']=='edge':
  from shapely.geometry import LineString
  return LineString([o['a'],o['b']]).buffer(o['thickness']/2,cap_style=2)
 s=o.get('size') or o.get('fit_size');x,z=o['q'];sx,sz=s[0],s[2]
 if o['kind']=='cylinder' and o.get('euler')==[90,0,0]:sz=s[1]*2
 return box(x-sx/2,z-sz/2,x+sx/2,z+sz/2)
# No expansive clipping of objects: shrink or leave incompletely qualified pieces pending.
kept=[];held=[]
for o in objects:
 f=footprint(o)
 if f is not None and not region.buffer(.002).covers(f):
  held.append({'id':o['id'],'reason':'Proposed local fixture would exceed approved R24 region; left unbuilt.'})
 else:kept.append(o)
P={'schema':'R25builder1','store_key':'storyway-east','origin_q':[X,Y],'floor_world_y':7,'source_packet':str(D/'SOURCE-EVIDENCE-R25.json'),'materials':materials,'objects':kept,'approved_geometry':A['geometry_qx_minus_qy'],'held_objects':held,'limitations':['Original floor/ceiling/elevation/furniture construction drawings not located; no as-built/99% claim.','Back-of-house and cashier/POS exact location not observed; no fabricated hidden kitchen or guessed cashier placement.','Detailed refrigerator/gondola/wine proportions MODEL fit to approved current CAD bay.','Stock is visible product morphology, not claimed liveSKU inventory.','Only qualified main sliding entry constructed; no additional unobserved entrance invented.']}
path=D/'STORE-CONSTRUCTION-PACKET-R25.json';path.write_text(json.dumps(P,ensure_ascii=False,indent=2));sha=hashlib.sha256(path.read_bytes()).hexdigest()
t=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/StoreBuilderR25.template.cs').read_text()
for k,v in {'__STORE_KEY__':'storyway-east','__WORKER_THREAD__':'classic:/root/r25_east_storyway','__EXACT_SCOPE__':'C180_R25_STORYWAY_EAST_APPLY_001','__PACKET_PATH__':str(path.relative_to(Path.cwd())),'__PACKET_SHA256__':sha}.items():t=t.replace(k,v)
(D/'ApplyStoreR25.cs').write_text(t)
print(json.dumps({'objects':len(kept),'held':len(held),'packet_sha256':sha,'held_ids':[x['id'] for x in held[:20]]},ensure_ascii=False))
