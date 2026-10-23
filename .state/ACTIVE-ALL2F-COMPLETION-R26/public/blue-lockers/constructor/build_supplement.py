from pathlib import Path
import json,copy,re,hashlib,cv2,numpy as np,datetime
ROOT=Path('/Users/um-yunsang/CHOOGuard');P=Path(__file__).resolve().parent;SRC=P/'sources'
base=json.loads((P/'BLUE-LOCKER-PACKET-R26.json').read_text());p=copy.deepcopy(base)
p['objects']=[];p['source_imports']=[]
p['mesh_library']={k:v for k,v in p['mesh_library'].items() if not k.startswith('Number_')}
ui=cv2.imread(str(SRC/'fldpfl753/original-11.jpg'))
H=cv2.getPerspectiveTransform(np.float32([[332,144],[570,121],[564,695],[327,676]]),np.float32([[0,0],[479,0],[479,779],[0,779]]))
screen=cv2.warpPerspective(ui,H,(480,780));f=SRC/'additional-bank-actual-runtime-menu.png';cv2.imwrite(str(f),screen)
p['source_imports'].append({'source':str(f),'destination':'UI/additional-bank-actual-runtime-menu.png','sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
p['materials']['screen']['texture_asset']='Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/BlueLockersSupplement002/UI/additional-bank-actual-runtime-menu.png'
attr=SRC/'SUPPLEMENT-ATTRIBUTION.txt';attr.write_text((SRC/'ATTRIBUTION.txt').read_text()+' Supplement002 reuses observed equipment types in independently source-visible faces/operating groups. Unreadable serials remain blank. Additional geometry is photo-proportioned MODEL, not all article equipment.\n')
p['source_imports'].append({'source':str(attr),'destination':'ATTRIBUTION.txt','sha256':hashlib.sha256(attr.read_bytes()).hexdigest()})
# UnitA keeps all existing435 object positions. The shared endcaps/hood are adapted
# through independent own derivatives only, to join the observed opposed face.
doublehood=copy.deepcopy(p['mesh_library']['top_hood_cross_section'])
for v in doublehood['vertices']:v[0]=v[0]*(1.58/.78)-.40
p['mesh_library']['double_sided_hood']=doublehood
for nm in['EndCapWhiteArtwork','EndCapDarkWaves']:
 if nm in p['mesh_library']:
  d=copy.deepcopy(p['mesh_library'][nm])
  for v in d['vertices']:v[0]=v[0]*(1.53/.73)+.40
  p['mesh_library'][nm+'_double']=d
units=[
 {'id':'A_HALL_FACE','qx':37.70,'qy':-23.0,'opposed':True,'double':False,'source':'Actual opposed face of existingA; source08 andOct10photo10'},
 {'id':'B_WINDOW_FACE','qx':38.50,'qy':-31.76,'opposed':False,'double':True,'source':'Second simultaneously photographed payment/cabinet group, joined toA along the same row'},
 {'id':'B_HALL_FACE','qx':37.70,'qy':-31.76,'opposed':True,'double':False,'source':'Other source-visible face of that second operating group'},
 {'id':'C_SOURCE_VISIBLE_FACE','qx':38.50,'qy':-13.24,'opposed':False,'double':False,'source':'Source07 independently near-facing pitched endcap and visible longitudinal face beyond foregroundrow; photo-proportioned type-model extent, serials not readable'}
]
def anonymize(s):
 for i in range(13):
  s=s.replace(f'Compartments_{901+2*i}_{902+2*i}',f'CompartmentPair_{i+1:02d}')
  for tier in range(2):s=s.replace(f'Locker_{901+2*i+tier}',f'Door_{i+1:02d}_{"Upper"if tier==0 else"Lower"}')
 return s
for unit in units:
 own=[]
 for spec in base['objects']:
  if spec['id'].startswith('Number_'):continue
  if unit['opposed'] and spec['group'] in ['Hood','EndCaps']:continue
  o=copy.deepcopy(spec);oldn=o['q'][0]-38.5;oldu=23.0-o['q'][1]
  if unit['opposed']:
   o['q']=[unit['qx']-oldn,-unit['qy']+oldu];o['yaw']=(o.get('yaw',0)+180)%360
  else:o['q']=[unit['qx']+oldn,-unit['qy']-oldu]
  o['id']=unit['id']+'__'+anonymize(o['id']);o['group']=unit['id']+'/'+anonymize(o['group'])
  # Generic builder uses single-level editable group names, preserve identity there.
  o['group']=o['group'].replace('/','__')
  o['source_ids']=['SNOWRUN_20261001_08_10','FLDPFL753_20261010_09_10_11','YONILEE_20260928_07','FNNEWS_KORAIL_PROVIDED_20260726']
  o['qualification']='SOURCE_VISIBLE_BANK_FACE_PHOTO_PROPORTIONED_MODEL; INDIVIDUAL_SERIAL_UNKNOWN_BLANK'
  if unit['id']=='C_SOURCE_VISIBLE_FACE' and spec['id'].endswith('_BaseRail'):
   # Native6 has a real10mm stone transition atqy-14.37. The new equipment
   # plinth clears it; the original floor is never flattened/cut.
   o['size'][1]=.078;o['y']=.061
  if unit['double']:
   if spec['id']=='PitchedHood':o['mesh_key']='double_sided_hood'
   elif spec['id'].startswith('EndCap_'):
    o['size'][2]=1.58;o['q'][0]-=.40
   elif spec['id'] in ['EndCapWhiteArtwork','EndCapDarkWaves']:o['mesh_key']=spec['id']+'_double'
  own.append(o)
 if unit['id']=='C_SOURCE_VISIBLE_FACE':
  # Actual near-facing cap is visible in source07. Its opposite/unseen face is not
  # populated with a guessed second26door array.
  for nm in['EndCapWhiteArtwork','EndCapDarkWaves']:
   o=next(copy.deepcopy(x)for x in own if x['id'].endswith('__'+nm));o['id']+='__NearFacingSourceCap';o['q'][1]=-unit['qy']+4.351;o['yaw']=0;own.append(o)
  for i in range(13):
   u=-4.33+.62*(i+.5)+(.60 if i>=6 else 0)
   for du in[-.15,.15]:
    for n in[-.26,.26]:
     own.append({'id':f'C_NATIVE_TRANSITION_ADJUSTMENT_PAD_{i}_{du}_{n}','kind':'cube','group':'C_SOURCE_VISIBLE_FACE__FloorSupports','q':[38.5+n,-unit['qy']-u-du],'y':.011,'size':[.040,.022,.050],'yaw':90,'material':'darksteel','collider':True,'qualification':'MODEL_SUPPORT_CLEARING_ACTUAL10MM_NATIVE_FLOOR_TRANSITION; NO_FLOOR_CHANGE','source_ids':['CURRENT_NATIVE6_FLOOR_TRANSITION_QY_MINUS14P37']})
 p['objects'].extend(own)
p['store_key']='blue-lockers-supplement'
p['source_packet']={
 'source_bank_graph':'SOURCE-BANK-GRAPH-SUPPLEMENT-R26.json',
 'additional_editable_faces':units,'additional_compartments':104,'additional_kiosks':4,
 'existing_compartments_kept':26,'total_current_source_model_compartments':130,
 'source_count_qualification':'A901..926 exact actualUI. Other operating group/face layout uses directly photographed two-tier13column equipment silhouette and separate terminals; serial values unavailable. C body extent is a bounded photo-proportioned MODEL, not an independently read26serial range.',
 'C_width_face_evidence':{'source':'source07 actual pitched endcap nearface plus receding longitudinal side','source966px_endcap_approx_corners':[[637,323],[709,328],[712,457],[633,454]],'source966px_side_far_vertices':[[727,337],[730,425]],'side_height_perspective_ratio':.68,'physical_longitudinal_model_extent_m':8.66,'basis':'Photo apparent height attenuation/visible side and same actual cabinet height, constrained to fixed hall terminal cell before nativefloorboundaryqy-7.2. Not article count or a pure family duplication.','unseen_opposed_C_face_created':False},
 'article144_150_used_for_counts':False,'all2F_complete':False,'no_Light_Text_TMP':True,
 'runtime_UI':'Source photographed idle equipment menu, no copied901..926 map for unidentified units',
 'free_asset':'Same acquired CC-BY adventurer hollow cabinet source; originalAssetsand1FScene untouched'
}
(P/'BLUE-LOCKERS-SUPPLEMENT-PACKET-002.json').write_text(json.dumps(p,ensure_ascii=False,separators=(',',':')))
print(json.dumps({'new_parts':len(p['objects']),'new_compartments':104,'new_kiosks':4,'old435positions_fixed':True,'source_model_total_compartments':130,'unseen_C_opposed_face_built':False}))
