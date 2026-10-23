from pathlib import Path
import json,math
from PIL import Image,ImageDraw
W=Path(__file__).resolve().parent
room=[[-19.90,-40.25],[-15.55,-40.25],[-15.53,-34.65],[-15.38,-34.50],[-12.20,-34.51],[-12.18,-30.01],[-18.18,-30.01],[-18.20,-34.81],[-19.88,-34.81],[-19.90,-40.25]]
record={'status':'L_SHELL_INTERIOR_NORMALS_SOURCE_MIGRATION_NOT_NEW_OCCUPANCY_APPROVAL','basis':'Authentic central2017 architecture page5 전문점2 L shell; actual Native0 matching-level mainfront/orthogonalreturn/EVrearline/endjamb directions','long_front':{'line_q':[[-15.55,-40.22],[-15.53,-34.65]],'public_outward_normal_q':[.999992,-.003912],'room_inward_normal_q':[-.999992,.003912]},'orthogonal_return':{'line_q':[[-15.378469,-34.495796],[-12.205946,-34.508189]],'public_outward_normal_q':[-.003912,-.999992],'room_inward_normal_q':[.003912,.999992]},'source_camera_location_family':'Outside long mainfront at qx>-15.5, and outside return at packetY<-34.5; Oden source image-left is decreasing packetY, Dain pastry source image-right is increasing qx','conditional_same_corner_correspondence':'If DAILY16 visible fascia/glazing corner is actual Native0 (-15.38,-34.50) endjamb, source pastry counter is behind return at packetY>-34.5, not in lower-leg extension atY<-34.5. Source photo alone does not authorize shifting the return corner away from actual Native0 to evade existing Flowers refrigeration.','existing_Flowers_conflict':{'actual_fixed_refrigerators':[{'q':[-14.08,-33.93],'fit_size':[.83,2.05,.70]},{'q':[-13.22,-33.93],'fit_size':[.83,2.05,.70]}],'reason':'Both currently occupy the source-return interior side; preserve them. Source physical corner identity vs historical2017 shell/current generated Flowers fitout subdivision remains a cross-owner correspondence issue.'},'lower_leg_physical_fitout_status':'Independently anchored to actual Dain/Oden longfront, rear wall and EV end; separate café tables/chairs/coffee/fridge/main sign may be applied as partial, with pastry required and held.','no_scene_mutation':True}
(W/'L-ROOM-INTERIOR-NORMALS-AND-FIXED-OCCUPANCY-R29.json').write_text(json.dumps(record,ensure_ascii=False,indent=2))
im=Image.new('RGB',(1200,1500),'white');dr=ImageDraw.Draw(im);fn=lambda p:((p[0]+22)*85+40,(-28-p[1])*85+40)
for x in range(-22,-8):
 dr.line([fn([x,-28]),fn([x,-44])],fill='#dddddd');dr.text(fn([x,-28]),str(x),fill='black')
for y in range(-44,-27):
 dr.line([fn([-22,y]),fn([-9,y])],fill='#dddddd');dr.text(fn([-22,y]),str(y),fill='black')
dr.polygon([fn(q) for q in room],fill='#f2e4d6',outline='black')
for x in [-14.08,-13.22]:
 a=fn([x-.415,-33.93+.35]);b=fn([x+.415,-33.93-.35]);dr.rectangle([a,b],fill='#a8a8a8',outline='black');dr.text(fn([x-.35,-33.94]),'Flowers fridge',fill='black')
def arrow(a,b,c):
 aa=fn(a);bb=fn(b);dr.line([aa,bb],fill=c,width=5);v=(bb[0]-aa[0],bb[1]-aa[1]);l=math.hypot(*v);u=(v[0]/l,v[1]/l);base=(bb[0]-u[0]*18,bb[1]-u[1]*18);dr.polygon([bb,(base[0]-u[1]*8,base[1]+u[0]*8),(base[0]+u[1]*8,base[1]-u[0]*8)],fill=c)
arrow([-15.54,-37.3],[-17.1,-37.3],'#008055');dr.text(fn([-18.0,-37.1]),'Native mainfront inward',fill='#008055')
arrow([-13.7,-34.5],[-13.7,-32.9],'#008055');dr.text(fn([-16.0,-32.7]),'Native return inward = +packetY',fill='#008055')
arrow([-15.54,-37.3],[-14.1,-37.3],'#0050b0');arrow([-13.7,-34.5],[-13.7,-36.0],'#0050b0');dr.text(fn([-13.4,-35.2]),'Public side',fill='#0050b0')
dr.ellipse([fn([-11.0,-41.5]),fn([-10.6,-41.9])],fill='#0050b0');dr.text(fn([-13,-42.1]),'DAILY16 camera direction family',fill='#0050b0')
dr.text((30,1450),'Internal source correspondence: actual qx,-qy; original native L shell and fixed current Flowers bodies',fill='black');im.save(W/'NATIVE0-L-ROOM-INTERIOR-NORMALS-R29.png')
