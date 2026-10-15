import json,hashlib,re,pathlib
out=pathlib.Path('workers/twin/output/floor2-whole-layout-20261009/newphoto-scope')
def h(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
p=json.load(open(out/'namcheon-source-front-packet.json'))
f=json.load(open('content/world/observed-interiors/floor-guide-layout.json'))
n=json.load(open('content/world/observed-interiors/platform-guide-openings.json'))
a=json.load(open('content/world/observed-interiors/structural-corrections.json'))
s=pathlib.Path('Assets/ChooGuard/Scenes/FpsStation.unity').read_text()
def block(i):return re.search(r'^--- !u!\d+ &'+str(i)+r'\n(.*?)(?=^--- !u!|\Z)',s,re.M|re.S).group(1)
def field(b,k):return re.search(r'^  '+k+r': (.*)$',b,re.M).group(1)
def vec(b,k):return [float(x)for x in re.findall(r': ([^,}]+)',field(b,k))]
def string(b,k):
 v=field(b,k);return json.loads(v)if v.startswith('"')else v
rows=[]
for r in p['mesh_pose_patches']+p['native_wall_clips']:
 b=r['full_channel_export']['asset'];native='role'in r;reg=n['meshes']if native else f['meshes'];matches=[x for x in reg if x['replacement_asset']==b]
 assert len(matches)<=1
 old=matches[0]if matches else None;raw=old['source_asset']if old else b
 sourceguid=re.search(r'^guid: (\w+)',pathlib.Path(raw+'.meta').read_text(),re.M).group(1)
 rows.append({'role':r.get('mesh_id',r.get('role')),'native':native,'source_asset':raw,'source_guid':sourceguid,'source_file_sha256':h(raw),'source_meta_sha256':h(raw+'.meta'),'source_complete_digest':old.get('source_digest')if old else r['full_channel_export']['complete_channel_digest'],'baseline_asset':b,'baseline_guid':r['full_channel_export']['guid'],'baseline_file_sha256':r['source_live_asset_sha256'],'baseline_meta_sha256':r['source_meta_sha256'],'baseline_complete_digest':r['full_channel_export']['complete_channel_digest'],'output_complete_digest':r['expected_output']['expected_complete_channel_digest'],'prior_registry':'content/world/observed-interiors/platform-guide-openings.json'if native else 'content/world/observed-interiors/floor-guide-layout.json'if old else None,'prior_registry_row':old,'architectural_row':next((x for x in a['entries']if x['source_asset']==raw),None),'owner_paths':[(o['owner']['path'])for o in r.get('render_owners',r.get('collider_owners',[r.get('owner_receipt')]))]})
b=block(p['door_parent_pose']['station_component']['component_id'])
door={k:string(b,k)for k in ['Element','Label','Sealed','Floor','CarId']}
door.update({k:int(field(b,k))for k in ['Index','Kind','Use']})
door.update({k:float(field(b,k))for k in ['Width','Height','ObservedOpen']})
door.update({k:vec(b,k)for k in ['Centre','Normal','Along','CabinSize']})
leaves=[]
for l in p['door_parent_pose']['leaf_receipts']:
 receipt=l['full_channel_export'];tb=block(l['transform_component']['component_id']);go=l['gameobject_id'];bc=re.search(r'^--- !u!65 &(\d+)\n(.*?)(?=^--- !u!|\Z)',s,re.M|re.S)
 cb=next(m.group(2)for m in re.finditer(r'^--- !u!65 &(\d+)\n(.*?)(?=^--- !u!|\Z)',s,re.M|re.S)if f'm_GameObject: {{fileID: {go}}}'in m.group(2))
 pivot=l['transform_component']['component_id'];mb=re.search(r'- Pivot: \{fileID: '+str(pivot)+r'\}\n(.*?)(?=  - Pivot:|  Floor:)',b,re.S).group(1)
 leaves.append({'name':l['owner']['path'].split('/')[-1],'asset':receipt['asset'],'guid':receipt['guid'],'file_sha256':h(receipt['asset']),'meta_sha256':h(receipt['asset']+'.meta'),'complete_digest':receipt['complete_channel_digest'],'position':vec(tb,'m_LocalPosition'),'rotation':vec(tb,'m_LocalRotation'),'scale':vec(tb,'m_LocalScale'),'slide':[float(x)for x in re.findall(r': ([^,}]+)',re.search(r'Slide: (.*)',mb).group(1))],'swing':float(re.search(r'Swing: (.*)',mb).group(1)),'length':float(re.search(r'Length: (.*)',mb).group(1)),'box_center':vec(cb,'m_Center'),'box_size':vec(cb,'m_Size'),'box_is_trigger':bool(int(field(cb,'m_IsTrigger'))),'box_enabled':bool(int(field(cb,'m_Enabled'))),'materials':l['owner']['materials']})
manifest={'schema':'chooguard.namcheon-source-rebuild-manifest.v1','packet':str(out/'namcheon-source-front-packet.json'),'packet_sha256':h(out/'namcheon-source-front-packet.json'),'entries':rows,'installed_dependencies':json.load(open('content/world/observed-interiors/namcheon-source-front-alignment.json'))['dependencies'],'collider_settings':[{'role':r.get('mesh_id',r.get('role')),'owner':o['owner']['path'],'cooking_options':int(field(block(o['renderer_or_collider_component']['component_id']),'m_CookingOptions')),'include_layers':0,'exclude_layers':0,'priority':0,'provides_contacts':False,'material_is_null':True}for r in p['mesh_pose_patches']+p['native_wall_clips']for o in (r.get('collider_owners',[])if 'mesh_id'in r else[r['owner_receipt']]if r['role'].endswith('collider')else[])],'door_fields':door,'door_leaves':leaves,'policy':'Fresh exact generated baseline after existing registries; no ordinal remapping or source writes; persistent installed output exact or strict rejection. Door parent delta once; zero StationDoor field edits. Native legacy replacement rejected.'}
(out/'namcheon-source-rebuild-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
print('manifest',h(out/'namcheon-source-rebuild-manifest.json'),'roles',len(rows),'leaves',len(leaves))
