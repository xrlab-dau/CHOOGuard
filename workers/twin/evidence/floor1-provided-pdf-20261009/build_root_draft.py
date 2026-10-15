"""Generate a reviewable builder extension/spec and bind actual complete-channel exports."""
import json, hashlib, struct
from pathlib import Path
import numpy as np
from shapely.geometry import LineString
ROOT=Path('/Users/um-yunsang/CHOOGuard');OUT=ROOT/'workers/twin/output/floor1-provided-pdf-20261009';EV=ROOT/'workers/twin/evidence/floor1-provided-pdf-20261009'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def dump(n,d):(OUT/n).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
packet=json.loads((OUT/'implementable-correction-packet.json').read_text());reg=json.loads((OUT/'native-topology-registration.json').read_text());A=np.array(reg['pixel_to_native_UV_affine'])
def project(q):q=np.atleast_2d(q);return (np.c_[q,np.ones(len(q))]@A).tolist()
selection=json.loads((OUT/'bounded-contributor-selection.json').read_text())
fullpath=ROOT/'.planning/2026-10-09-floor2-completion-pass2/floor1-detail-full-channels/metadata.json';full=json.loads(fullpath.read_text());by={x['role']:x for x in full['entries']}
actual=json.loads((ROOT/'.planning/2026-10-09-floor2-completion-pass2/floor1-detail-export-contributors.json').read_text());live={x['owner']['mesh']:x for x in actual['rows']+actual['collider_rows']}
for item in selection['owners']:
 mid=item['owner']['mesh'];item['actual_full_channel_export']=by[mid];assert sha(ROOT/by[mid]['path'])==by[mid]['binary_sha256']
 assert item['owner']['asset']==by[mid]['asset'];assert sha(ROOT/item['owner']['asset'])==live[mid]['source_live_file_sha256']
 item['owner']['asset_sha256']=live[mid]['source_live_file_sha256'];item['source_complete_channels_sha256']=by[mid]['complete_channel_digest']
 # Confirm exported current complete-channel positions and triangles are the same geometry used for selection.
 raw=(ROOT/by[mid]['path']).read_bytes();at=0
 def readi():
  global at
  n=struct.unpack_from('<i',raw,at)[0];at+=4;return n
 assert readi()==1
 def array(dim):
  global at
  n=readi();v=np.frombuffer(raw,'<f4',n*dim,at).reshape(n,dim);at+=n*dim*4;return v
 verts=array(3);array(3);array(4);array(4)
 for u in range(8):
  count=readi();dimension=readi();at+=count*16
 count=readi();assert count==1
 topology=readi();n=readi();indices=np.frombuffer(raw,'<i4',n,at);at+=n*4;assert at==len(raw)
 old=(ROOT/item['owner']['binary']).read_bytes();nv,ni,nn=struct.unpack_from('<iii',old)
 ov=np.frombuffer(old,'<f4',nv*3,12).reshape(nv,3);oi=np.frombuffer(old,'<i4',ni,12+nv*12)
 assert np.array_equal(verts,ov)and np.array_equal(indices,oi)
 item['selection_positions_indices_exactly_equal_actual_full_channel_export']=True
selection['actual_full_channel_scene_sha256']=full['scene_sha256'];selection['full_channel_metadata']=str(fullpath.relative_to(ROOT));selection['full_channel_metadata_sha256']=sha(fullpath)
selection['scene_sha256']=full['scene_sha256'];selection['full_channels_verified_offline']=True
dump('bounded-contributor-selection.json',selection)
partition_pixels={
 'right-upper':[[[910,132],[910,144]],[[917,132],[917,144]],[[924,132],[924,144]],[[931,132],[931,144]],[[938,132],[938,144]],[[945,132],[945,144]]],
 'right-lower':[[[908,208],[908,198]],[[914,208],[914,198]],[[920,208],[920,198]],[[927,208],[927,198]],[[933,208],[933,198]],[[940,208],[940,198]]],
 'left-upper':[[[54,214],[57,222]],[[61,210],[64,218]],[[66,207],[69,215]],[[72,203],[75,211]],[[77,199],[80,207]],[[83,195],[86,203]]],
 'left-lower':[[[73,233],[76,241]],[[79,229],[82,237]],[[84,225],[87,233]],[[90,221],[93,229]],[[95,217],[98,225]],[[101,213],[104,221]]]}
partitions=[]
for room,segs in partition_pixels.items():
 for i,seg in enumerate(segs):partitions.append({'id':room+'-stall-boundary-'+str(i),'room':room,'source_segment_pixels_approx':seg,'native_segment_UV':project(seg),'source_pixel_tolerance':3,'trace_class':'manual thin ink/cell-boundary approximation; low-resolution boundary gaps and WC subtype unresolved; no equal-spacing defaults','source_evidence':'stall-partition-traces-left.png or stall-partition-traces-right.png; original left/right-toilets-native8x.png','vertical_parameters':'stock existing KStall visual partition height1.85/thickness0.025, unmeasured'})
packet['stall_partition_traces']=partitions
packet['stall_interaction_limits']={'countable_regular_stall_rows':4,'regular_cells_per_row_minimum':5,'individual_hinge_arcs':'UNRESOLVED at raster resolution','production_leaf_orientation_claim':'NONE','draft_interaction_leaves':20,'draft_leaf_design_status':'Explicit optional surrogate positions on traced stall front edges. No field-authentic hinge/leaf dimensions; source does not establish these interaction parameters.'}
dump('implementable-correction-packet.json',packet)
elements=[];evidence=[str((OUT/'implementable-correction-packet.json').relative_to(ROOT)),str((OUT/'native-topology-registration.json').relative_to(ROOT))]
def elem(type,id,g,p):elements.append({'type':type,'id':id,'zone':'1F PDF source 20261009','tier':'b','frame':'root','evidence':evidence,'geometry':g,'params':p})
# All heights/material sections are retained or stock visual parameters, not source-derived measurements.
unique=set();seen_lines=[]
for s in packet['wall_segments']:
 if s['room_id'] in [w['id']for w in packet['main_WC_rooms']]:continue
 if 'existing original rear'in s['wall_status']:continue
 key=tuple(sorted(map(tuple,s['source_segment_pixels'])))
 if key in unique:continue
 candidate=LineString(s['source_segment_pixels'])
 if any(line.buffer(.01).covers(candidate)for line in seen_lines):continue
 unique.add(key);seen_lines.append(candidate);a,b=s['native_segment_UV'];ops=[]
 for d in packet['service_doors']:
  dp=np.array(d['native_jamb_UV']);ab=np.array(b)-a;length=np.linalg.norm(ab);direction=ab/length
  ss=(dp-np.array(a))@direction
  if np.max(np.abs(np.cross(direction,dp-np.array(a))))<.02 and ss.min()>=-.02 and ss.max()<=length+.02:ops.append([float(ss.min()),float(ss.max()),2.1])
 elem('SourceDiagramWall',s['id'],{'edge':[a,b],'y':0,'top':6.7},{'thickness':.12,'finish':'gypsum','openings':ops,'dimensionsStatus':'existing rear strip height6.7 and kit thickness.12 retained; unmeasured'})
for d in packet['service_doors']:
 edge=d['native_jamb_UV'];length=float(np.linalg.norm(np.array(edge[1])-edge[0]));normal=-np.array(d['native_swing_UV']);normal/=np.linalg.norm(normal)
 elem('ServiceDoor',d['id'],{'edge':edge,'y':0},{'width':length,'height':2.1,'leaves':2,'faceToward':(np.mean(edge,axis=0)+normal).tolist(),'plate':'','closer':False,'kickPlate':False,'label':next(r['label']for r in packet['service_rooms']if r['id']==d['room']),'sourceClaim':'4 double symbols/8 leaves; standard door hardware/height unmeasured','nativeRearWallCuts':'never cut original MainShell; existing rear passages must be reviewed'})
for room in packet['service_rooms']:
 elem('SourceDiagramFloor',room['id']+'-floor',{'polygon':room['native_polygon_UV'],'y':0},{'finish':'floorGranite','sourceSupport':'Source PDF room interior; structural support under previously omitted native surface unverified','originalGroundFloor':'preserved exactly'})
# Opening gaps are a bounded diagram reading. No WC gender labels or assumed accessible fixtures.
wcgaps={'right-upper':[[[897,144],[897,152]]],'right-lower':[[[897,185],[897,195]]], 'left-upper':[[[96,200],[102,207]]],'left-lower':[[[109,217],[115,227]]]}
for w in packet['main_WC_rooms']:
 ops=[];p=w['source_polygon_pixels_approx'];up=np.array(w['native_polygon_UV'])
 for gap in wcgaps[w['id']]:
  pp=np.array(project(gap));best=None
  for i,(a,b)in enumerate(zip(up,np.roll(up,-1,axis=0))):
   di=b-a;length=np.linalg.norm(di);di/=length;s=(pp-a)@di;perp=np.abs(np.cross(di,pp-a)).max()
   if best is None or perp<best[0]:best=(perp,i,s,length)
  _,edge,s,length=best;ops.append({'edge':edge,'at':float(np.mean(s)),'width':float(s.max()-s.min()),'height':2.3,'kind':'open','sourcePixels':gap,'sourcePixelTolerance':3,'doorArc':'unresolved; open passage only'})
 elem('SourceDiagramRoom',w['id'],{'polygon':w['native_polygon_UV'],'y':0,'y1':2.7},{'wallTop':w['existing_height_reference'],'wallThickness':.12,'wallFinish':'tile','floor':'tile','ceiling':'ceiling600','openings':ops,'floorCollision':True,'structuralFloorUnderFinish':'unverified where native Ground_Floor omitted; original floor mesh unchanged','gender':'unassigned'})
for f in packet['small_WC_symbols']:
 elem('SourceDiagramWC',f['id'],{'at':f['native_center_UV'],'y':0,'front':f['native_front_UV']},{'symbolCountSource':'only countable small WC pan symbol','standardVisualDimensions':'existing KWc stock helper; unmeasured'})
for p in partitions:
 # End boundaries touching native room walls can be omitted at integration if duplicate; explicit traces preserved.
 elem('SourceDiagramPartition',p['id'],{'edge':p['native_segment_UV'],'y':0,'height':1.85},{'thickness':.025,'sourcePixels':p['source_segment_pixels_approx'],'unmeasuredVertical':True})
spec={'zone':'1F PDF source 20261009','root':'실내 트윈 마감/1F PDF 원도 복원','basis':{'registration':evidence[1],'dimensions':'relative source diagram localization only; all heights/thickness/stock fixture sizes unmeasured','sourceGeometryOwnership':'original floor/walls/columns/cores unchanged','fixtureCount':'20 small WC symbols minimum; other sanitary counts unknown','productionInstalled':False,'stallDoorOperations':'No exact hinge/arc evidence. Separate optional interaction surrogate packet; never claim source-authentic hardware.'},'elements':elements}
dump('root-kit-spec-draft.json',spec)
optional=[]
for room,segs in partition_pixels.items():
 for i in range(5):
  edge=project([segs[i][1],segs[i+1][1]]);fw=np.array(project([segs[i][1]])[0])-project([segs[i][0]])[0];fw/=np.linalg.norm(fw)
  optional.append({'type':'SourceDiagramStallDoor','id':room+'-stall-leaf-'+str(i+1),'zone':'1F PDF source 20261009','tier':'b','frame':'root','geometry':{'edge':edge,'y':.15},'params':{'width':max(.4,float(np.linalg.norm(np.array(edge[1])-edge[0]))-.04),'height':1.70,'leaves':1,'faceToward':(np.mean(edge,axis=0)-fw).tolist(),'plate':'','closer':False,'kickPlate':False,'label':'WC stall '+str(i+1),'sourceClaim':'operation surrogate: hinge and leaf sizes UNVERIFIED; source confirms pan/stall row only'}})
dump('optional-stall-interaction-surrogates.json',{'production_included':False,'root_authorized_for_draft_integration':True,'reason':'Native raster cannot resolve 20 individual hinges/arcs. Root accepted stock operation surrogate with limitation recorded on2026-10-09.','elements':optional})
spec['elements']+=optional;spec['basis']['stallDoorOperations']='Root-authorized20 modeled interaction leaves, front positions sourced; hinge/type/height are unverified visual-operation parameters.';dump('root-kit-spec-draft.json',spec)
builder=(ROOT/'AgentScripts/MajibangBuilder.cs').read_text()
hooks='''
                case "SourceDiagramWall": KitSourceDiagramWall(id, g, p); break;
                case "SourceDiagramRoom": KitSourceDiagramRoom(id, g, p); break;
                case "SourceDiagramWC": KitSourceDiagramWC(id, g, p); break;
                case "SourceDiagramPartition": KitSourceDiagramPartition(id, g, p); break;
                case "SourceDiagramFloor": KitSourceDiagramFloor(id, g, p); break;
                case "SourceDiagramStallDoor": KitSourceDiagramStallDoor(id, g, p); break;
'''
helpers='''
    // Explicit source diagram runs. No automatic fixture count, spacing, gender or ceiling height inference.
    static void KitSourceDiagramWall(string id, JObject g, JObject p)
    {
        var e=KT(g,"edge",id);Vector2 a=KV(e[0]),b=KV(e[1]);float y=KN(g,"y",id),top=KN(g,"top",id),t=KF(p,"thickness",.12f);
        Vector3 A=KP(a,y),EX=(KP(b,y)-A).normalized,FW=Vector3.Cross(EX,Vector3.up).normalized;float len=Vector2.Distance(a,b);
        var gaps=new List<Vector2>();var openings=new List<Vector3>();
        if(p["openings"] is JArray ops)foreach(var o in ops){gaps.Add(new Vector2((float)o[0],(float)o[1]));openings.Add(new Vector3((float)o[0],(float)o[1],(float)o[2]));}
        Material m=KM(KS(p,"finish","gypsum"));Batch batch=B("Kit_PDFWall_"+m.name,m);
        foreach(var run in KRuns(len,gaps)){Vector3 c=A+EX*((run.x+run.y)*.5f)+Vector3.up*((top-y)*.5f);KBox(batch,c,EX,Vector3.up,FW,new Vector3(run.y-run.x,top-y,t));KColBox(c,EX,Vector3.up,FW,new Vector3(run.y-run.x,top-y,t));}
        foreach(var o in openings){float h=top-y-o.z;if(h<=0)continue;Vector3 c=A+EX*((o.x+o.y)*.5f)+Vector3.up*(o.z+h*.5f);KBox(batch,c,EX,Vector3.up,FW,new Vector3(o.y-o.x,h,t));KColBox(c,EX,Vector3.up,FW,new Vector3(o.y-o.x,h,t));}
    }
    static void KitSourceDiagramRoom(string id,JObject g,JObject p)
    {
        List<Vector2> poly=KOutline(KT(g,"polygon",id),id);float y=KN(g,"y",id),y1=KN(g,"y1",id),t=KF(p,"wallThickness",.12f),top=KF(p,"wallTop",y1);
        var ops=KOpeningsOf(p,poly,y,top,id);string finish=KS(p,"wallFinish","tile");
        var inner=KRoomWalls(poly,y,top,t,KM(finish),KM(finish),ops,.1f,true);
        Vector2 ax=(poly[1]-poly[0]).normalized;float rot=Mathf.Atan2(ax.y,ax.x)*Mathf.Rad2Deg;
        KRoomFloor(id,poly,y,KS(p,"floor","toiletFloor"),0,rot);
        KRoomCeiling(id,poly,inner,KInset(poly,t*.5f),y1,KS(p,"ceiling","ceiling600"),rot,ax,null,4000,2.4f);
        // A same-Y source-supported finish collision is explicit; original Ground_Floor remains unchanged.
        if(KB(p,"floorCollision",false))KFill(KCol(),poly,new List<List<Vector2>>(),y,Vector3.up,poly[0]);
    }
    static void KitSourceDiagramFloor(string id,JObject g,JObject p)
    {
        var poly=KOutline(KT(g,"polygon",id),id);float y=KN(g,"y",id);KRoomFloor(id,poly,y,KS(p,"finish","floorGranite"),0,0);
        KFill(KCol(),poly,new List<List<Vector2>>(),y,Vector3.up,poly[0]);
    }
    static void KitSourceDiagramWC(string id,JObject g,JObject p)
    {
        Vector2 uv=KV(KT(g,"at",id)),front=KV(KT(g,"front",id)).normalized;float y=KN(g,"y",id);
        Vector3 FW=KD(front).normalized,AX=Vector3.Cross(Vector3.up,FW).normalized,C=KP(uv,y);
        Material por=KM("#F4F5F2"),metal=KM("hairline"),dk=KM("#263036");
        KWc(B("Kit_PDFWC_"+por.name,por),B("Kit_PDFWC_"+metal.name,metal),B("Kit_PDFWC_"+dk.name,dk),C-FW*.4f,AX,FW);
    }
    static void KitSourceDiagramPartition(string id,JObject g,JObject p)
    {
        var e=KT(g,"edge",id);Vector2 a=KV(e[0]),b=KV(e[1]);float y=KN(g,"y",id),height=KN(g,"height",id),t=KF(p,"thickness",.025f);
        Vector3 AX=(KP(b,y)-KP(a,y)).normalized,FW=Vector3.Cross(AX,Vector3.up).normalized,C=KP((a+b)*.5f,y)+Vector3.up*(height*.5f);
        Material m=KM("#D9DEE0");KBox(B("Kit_PDFStall_"+m.name,m),C,AX,Vector3.up,FW,new Vector3(Vector2.Distance(a,b),height,t));KColBox(C,AX,Vector3.up,FW,new Vector3(Vector2.Distance(a,b),height,t));
    }
    static void KitSourceDiagramStallDoor(string id,JObject g,JObject p)
    {
        KitServiceDoor(id,g,p);if(doorNow!=null)doorNow.use="public";
    }
'''
assert 'case "SourceDiagramWC"'not in builder
builder=builder.replace('                case "ToiletRoom": KitToiletRoom(id, g, p); break;','                case "ToiletRoom": KitToiletRoom(id, g, p); break;'+hooks)
builder=builder.replace('    // ---- ToiletRoom (public toilet:',helpers+'\n    // ---- ToiletRoom (public toilet:')
builder=builder.replace('        int before = KitTris(), lightsBefore = kitLights; float areaBefore = kitFillArea;', '        string sourcePdfPriorPrefix = batchPrefix;\n        if ((string)e["zone"] == "1F PDF source 20261009") batchPrefix = "SourceTrace_" + id + "_";\n        int before = KitTris(), lightsBefore = kitLights; float areaBefore = kitFillArea;')
builder=builder.replace('        finally { if (box) ExitBox(); doorNow = null; leafNow = null; }','        finally { if (box) ExitBox(); doorNow = null; leafNow = null; batchPrefix = sourcePdfPriorPrefix; }')
(OUT/'MajibangBuilder.source-pdf-draft.cs').write_text(builder)
dump('draft-build-provenance.json',{'base_builder':'AgentScripts/MajibangBuilder.cs','base_builder_sha256':sha(ROOT/'AgentScripts/MajibangBuilder.cs'),'draft_sha256':sha(OUT/'MajibangBuilder.source-pdf-draft.cs'),'production_modified':False,'new_types':['SourceDiagramWall','SourceDiagramRoom','SourceDiagramWC','SourceDiagramPartition'],'reuses_existing_helpers':['KRoomWalls','KRoomFloor','KRoomCeiling','KWc','KBox','KColBox','ServiceDoor/StationDoor pipeline'],'root_integration_required':'Review/rebase builder hooks, bind exact current scene epoch, durable correction registry for retained collision mesh; draft has never run in Unity.'})
print(json.dumps({'full_channel_bindings':len(selection['owners']),'spec_elements':len(elements),'pan_symbols':len(packet['small_WC_symbols']),'partition_traces':len(partitions),'optional_surrogate_leaves':len(optional)}))
