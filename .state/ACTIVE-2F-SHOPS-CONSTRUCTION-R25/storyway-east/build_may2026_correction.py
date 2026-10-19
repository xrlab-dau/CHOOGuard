from pathlib import Path
import json,math,hashlib
from shapely.geometry import box
D=Path(__file__).parent
base=(D/'build_storyway_packet.py').read_text();exec(base.split('# Exact approved boundary')[0])
SRC=['NAVER-DJAGYTT-20260518-15'];objects=[]
# Latest independently identified southeast store: door right of left glazed/chiller face.
W=7.64355046546757
for key,a,b in [('left',.12,3.32),('right',6.45,W-.12)]:
 edge('MAY26_fixed_glass_'+key,'MAY26_Front_Glass',q(a,.13),q(b,.13),.06,2.41,.014,'glass',cast_shadows=False)
 for u in [a,b]:cube('MAY26_glass_stile_'+key+'_'+str(u),'MAY26_Front_Frames',u,.13,1.25,.044,2.50,.065,'dark')
 cube('MAY26_bottom_rail_'+key,'MAY26_Front_Frames',(a+b)/2,.13,.06,b-a,.045,.065,'dark')
cube('MAY26_door_header','MAY26_Automatic_Entry',W/2,.135,2.49,W-.2,.12,.17,'dark')
for key,u in [('left',2.50),('right',7.01)]:
 cube('MAY26_open_leaf_glass_'+key,'MAY26_Automatic_Entry',u,.215,1.245,1.44,2.34,.012,'glass',cast_shadows=False)
 for du in [-.72,.72]:cube('MAY26_open_leaf_stile_'+key+'_'+str(du),'MAY26_Automatic_Entry',u+du,.215,1.245,.035,2.38,.045,'dark')
 cube('MAY26_leaf_bottom_'+key,'MAY26_Automatic_Entry',u,.215,.065,1.47,.035,.045,'dark')
cube('MAY26_left_frosted_logo_panel','MAY26_Front_Glass',1.32,.14,1.26,1.10,1.96,.014,'ivory',collider=False)
text('MAY26_StoryWay_frost_logo','MAY26_Source_Signs','StoryWay',1.32,.115,1.76,.90,.18,(.9,.29,.045,1),yaw=180)
text('MAY26_white_StoryWay_wordmark','MAY26_Source_Signs','<b>StoryWay</b>',W/2+.28,.03,2.94,3.07,.47,(.99,.99,.97,1),yaw=180)
for i in range(16):
 angle=math.radians(205+i*9.1);u=1.72+math.cos(angle)*.225;h=2.96+math.sin(angle)*.225
 cyl('MAY26_white_smile_arc_'+str(i),'MAY26_Source_Signs',u,.052,h,.057,.035,'LED',euler=[90,0,0],collider=False)
for i,m in enumerate(['blue','lime','yellow','ivory','white_metal']):
 angle=math.radians(20+i*35);u=1.72+math.cos(angle)*.23;h=2.96+math.sin(angle)*.23
 cyl('MAY26_smile_top_dot_'+str(i),'MAY26_Source_Signs',u,.052,h,.065,.035,m,euler=[90,0,0],collider=False)
# Directly visible white OPEN chilled cabinet, not closed refrigerator doors.
u,d,w=2.85,3.1,4.55
cube('MAY26_fastfood_chiller_base','MAY26_Open_Chilled_Display',u,d,.15,w,.30,.90,'dark')
cube('MAY26_fastfood_chiller_back','MAY26_Open_Chilled_Display',u,d+.36,1.18,w,2.10,.11,'ivory')
for side in [-1,1]:cube('MAY26_fastfood_chiller_end_'+str(side),'MAY26_Open_Chilled_Display',u+side*(w/2-.045),d,1.18,.09,2.10,.90,'ivory')
cube('MAY26_fastfood_chiller_top','MAY26_Open_Chilled_Display',u,d,2.23,w,.11,.90,'ivory')
cube('MAY26_FAST_FOOD_orange_header','MAY26_Open_Chilled_Display',u,d+.03,2.57,w,.27,.86,'orange')
text('MAY26_FAST_FOOD_physical_letters','MAY26_Source_Signs','FAST FOOD',u,d-.42,2.56,2.0,.18,(.99,.99,.97,1),yaw=180)
for tier in range(6):
 h=.38+tier*.30
 cube('MAY26_open_chiller_shelf_'+str(tier),'MAY26_Open_Chilled_Display',u,d,h,w-.2,.024,.74,'white_metal')
 cube('MAY26_open_chiller_price_'+str(tier),'MAY26_Open_Chilled_Display',u,d-.38,h+.028,w-.2,.046,.020,'paper',collider=False)
 cube('MAY26_chiller_shelf_LED_'+str(tier),'MAY26_Open_Chilled_Display',u,d-.32,h+.055,w-.28,.016,.022,'LED',collider=False)
 for j in range(20):
  bu=u-w/2+.20+j*(w-.4)/19
  m=['water_blue','green_bottle','yellow','red','orange','paper'][(j//3+tier)%6]
  cyl('MAY26_bottle_%02d_%02d'%(tier,j),'MAY26_Observed_Chilled_Products',bu,d-.06,h+.12,.105,.215,m,collider=False)
  cyl('MAY26_bottle_cap_%02d_%02d'%(tier,j),'MAY26_Observed_Chilled_Products',bu,d-.06,h+.235,.05,.027,'paper',collider=False)
# White narrow peg display on the right end of the case: small carded electronic/travel goods.
pu,pd=5.34,3.03
cube('MAY26_white_peg_display_foot','MAY26_White_Peg_Display',pu,pd,.06,.46,.12,.66,'white_metal')
cube('MAY26_white_peg_display_back','MAY26_White_Peg_Display',pu,pd,1.05,.055,1.92,.60,'ivory')
for j in range(4):
 for k in range(4):
  dd=pd-.23+k*.15;hh=.47+j*.38
  cube('MAY26_peg_hook_%s_%s'%(j,k),'MAY26_White_Peg_Display',pu+.11,dd,hh,.25,.012,.012,'steel',collider=False)
  cube('MAY26_hanging_card_%s_%s'%(j,k),'MAY26_Travel_Accessory_Cards',pu+.20,dd,hh-.09,.018,.19,.11,'paper',collider=False)
  cube('MAY26_clear_phone_case_%s_%s'%(j,k),'MAY26_Travel_Accessory_Cards',pu+.22,dd,hh-.12,.012,.125,.078,'glass',collider=False,cast_shadows=False)
# Low aisle shelf visible at extreme right of source. Discrete observed row, not long invented gondola.
for tier in range(4):
 h=.26+tier*.30
 cube('MAY26_right_low_shelf_'+str(tier),'MAY26_Entry_Right_Low_Stock',6.92,2.00,h,1.0,.025,.66,'white_metal')
 for j in range(6):cube('MAY26_right_low_pack_%s_%s'%(tier,j),'MAY26_Entry_Right_Stock',6.53+j*.155,1.98,h+.105,.12,.18,.30,['red','paper','yellow','blue'][j%4],collider=False)
# Rear service counter role is visible at photo-right; only visible face/equipment profile, no hidden kitchen/storage.
cube('MAY26_observed_counter_visible_face','MAY26_Visible_Service_Face',6.78,7.9,.48,1.05,.96,1.70,'ivory')
cube('MAY26_observed_counter_dark_worktop','MAY26_Visible_Service_Face',6.78,7.9,1.00,1.11,.075,1.76,'dark')
# Unreadable monitor silhouette separated from counter, never counterfeit UI.
cube('MAY26_cashier_small_visible_black_device','MAY26_Visible_Service_Face',6.74,7.43,1.20,.30,.27,.13,'dark',collider=False)
cube('MAY26_cashier_device_stand','MAY26_Visible_Service_Face',6.74,7.43,1.07,.085,.12,.085,'dark',collider=False)
# Rear blue mural is observed, but omitted exact palm image rather than invented photographic billboard.
edge('MAY26_observed_blue_rear_wall_finish','MAY26_Observed_Rear_Surface',q(5.48,9.1),q(7.86,9.1),.05,2.96,.025,'blue',collider=False)
# Actual capsule pair is behind left glass, with public doorway right of this pair.
for ix,(cu,m) in enumerate([(2.5,'orange'),(3.05,'blue')]):
 cube('MAY26_capsule_foot_'+str(ix),'MAY26_Capsule_Dispensers',cu,.60,.065,.48,.13,.43,m)
 for h in [.43,.89]:
  cube('MAY26_capsule_module_%s_%s'%(ix,h),'MAY26_Capsule_Dispensers',cu,.60,h,.46,.41,.40,m)
  cube('MAY26_capsule_clear_%s_%s'%(ix,h),'MAY26_Capsule_Dispensers',cu,.385,h+.045,.37,.24,.013,'glass',collider=False,cast_shadows=False)
  cyl('MAY26_capsule_knob_%s_%s'%(ix,h),'MAY26_Capsule_Dispensers',cu,.35,h-.105,.085,.035,'steel',euler=[90,0,0],collider=False)
# Same visible red extinguisher now relocated to case end/aisle rear at newest source location.
cyl('MAY26_red_extinguisher','MAY26_Observed_Safety',5.8,4.8,.28,.18,.43,'red')
cube('MAY26_extinguisher_stand','MAY26_Observed_Safety',5.8,4.8,.04,.25,.08,.25,'red')
cyl('MAY26_extinguisher_neck','MAY26_Observed_Safety',5.8,4.8,.53,.045,.08,'steel')
cube('MAY26_extinguisher_handle','MAY26_Observed_Safety',5.8,4.8,.59,.14,.03,.06,'dark',collider=False)
P={'schema':'R25builder1','store_key':'storyway-east','origin_q':[X,Y],'floor_world_y':7,'source_packet':str(D/'SOURCE-EVIDENCE-R25.json'),'materials':materials,'objects':objects,'approved_geometry':A['geometry_qx_minus_qy'],'source_revision':'2026-05-18 precise southeast shop photo15 supersedes unbound generic2F Koreatodo interior','limitations':['Individual measurements remain MODEL within approved R24 coordinates','Exact palm mural image and unseen BOH not fabricated','POS profile visible but screen/circuit unspecified']}
p=D/'STORE-MAY2026-CORRECTION-PACKET-R25.json';p.write_text(json.dumps(P,ensure_ascii=False,indent=2));sha=hashlib.sha256(p.read_bytes()).hexdigest()
t=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/StoreBuilderR25.template.cs').read_text()
for k,v in {'__STORE_KEY__':'storyway-east','__WORKER_THREAD__':'classic:/root/r25_east_storyway','__EXACT_SCOPE__':'C180_R25_STORYWAY_EAST_PHOTO_MAY26_004','__PACKET_PATH__':str(p.relative_to(Path.cwd())),'__PACKET_SHA256__':sha}.items():t=t.replace(k,v)
t=t.replace('public static class StoreBuilderR25','public static class CorrectStorywayPhotoR25')
t=t.replace('if(File.Exists(Root+"/"+path))throw new Exception("Ownmaterialreplay:"+path);','if(File.Exists(Root+"/"+path)){Materials[prop.Name]=AssetDatabase.LoadAssetAtPath<Material>(path);continue;}')
t=t.replace('if(GameObject.Find(ParentPath+"/"+name)!=null)throw new Exception("Ownstorealreadyexists; no automaticdelete/replay");','if(GameObject.Find(ParentPath+"/"+name)==null)throw new Exception("Exact own saved root required");')
start='var parent=Ensure(ParentPath);var own=new GameObject(name);';end='var groups=new Dictionary<string,Transform>();'
i=t.index(start);j=t.index(end,i)
t=t[:i]+'var own=GameObject.Find(ParentPath+"/"+name);\n   '+t[j:]
# Exact original font block replaced by own complete source-generated font; source font stays untouched.
i=t.index('if(localFont==null){');j=t.index('var text=go.AddComponent<TextMeshPro>();',i)
t=t[:i]+'if(localFont==null){localFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(folder+"/OWN_COMPLETE_REQUIRED_CHARACTERS_FONT_R25.asset");if(localFont==null)throw new Exception("Own complete font required");}\n      '+t[j:]
groups=['Front_Glass','Front_Frames','Automatic_Entry','Modeled_Signs','Central_Observed_Gondola','Visible_Stock_Individually_Editable','Central_Observed_Endcap','Refrigeration_Bank','Refrigeration_Doors','Refrigeration_Shelves','Visible_Chilled_Stock','Timber_Wine_Gift_Unit','Wine_Stock_Individually_Editable','Observed_Capsule_Dispensers','Observed_Safety_Equipment','Modeled_Interior_Signs']
kill='foreach(string groupName in new string[]{'+','.join('"'+g+'"' for g in groups)+'}){var retired=own.transform.Find(groupName);if(retired!=null)Undo.DestroyObjectImmediate(retired.gameObject);}\n   '
t=t.replace('AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);',kill+'AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);')
t=t.replace('/APPLIED-STORE-R25.json','/APPLIED-MAY2026-SOURCE-CORRECTION-R25.json')
(D/'ApplyPhotoCorrectionR25.cs').write_text(t)
print({'objects':len(objects),'packetSHA':sha,'deleteOwnGroupsOnly':len(groups)})
