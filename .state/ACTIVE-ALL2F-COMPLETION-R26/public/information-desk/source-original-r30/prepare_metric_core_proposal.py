from pathlib import Path
import json, math, copy, importlib.util, hashlib
import numpy as np,cv2
R=Path.cwd();P=Path(__file__).parent
old=json.loads((P.parent/'constructor/INFORMATION-DESK-PACKET-R26.json').read_text());receipt=json.loads((P.parent/'constructor/APPLIED-INFORMATION-DESK-R26.json').read_text());ids={o['id']:o['object_id'] for o in receipt['objects']};orig={o['id']:o for o in old['objects']};snap={o['path'].split('/')[-1]:o for o in json.loads((P/'CURRENT-OWN-OBJECTS-SNAPSHOT007.json').read_text())};newmeshes={};changes=[]
def write_mesh(name,vs,ts,reason,newmat=None):
 o=orig[name];newmeshes[name]={'vertices':vs,'triangles':ts}
 d={'operation':'replace_only_own_mesh_derivative','id':name,'object_id':ids[name],'path':receipt['root_path']+'/'+o['group']+'/'+name,'expected_mesh_asset':snap[name]['source_mesh_asset'],'expected_world_matrix_column_major':snap[name]['world_matrix_column_major'],'new_mesh_key':name,'reason':reason,'old_local_TRS':{k:o[k] for k in ['position','euler','scale']},'new_local_TRS':'unchanged'}
 if newmat:d['new_material_from_existing_own_packet_key']=newmat
 changes.append(d)
def ring(ri,ro,y0,y1,a0=-151,a1=151,n=100):
 v=[];t=[]
 for i in range(n+1):
  a=math.radians(a0+(a1-a0)*i/n);v.extend([[r*math.sin(a),y,r*math.cos(a)]for r,y in [(ro,y0),(ro,y1),(ri,y0),(ri,y1)]])
 for i in range(n):
  k=i*4;l=k+4
  for q in [[k,l,l+1,k+1],[k+3,l+3,l+2,k+2],[k+1,l+1,l+3,k+3],[k+2,l+2,l,k]]:t.extend([q[0],q[1],q[2],q[0],q[2],q[3]])
 if abs(a1-a0)<359.9:
  for q in [[0,1,3,2],[n*4+2,n*4+3,n*4+1,n*4]]:t.extend([q[0],q[1],q[2],q[0],q[2],q[3]])
 return v,t
# Original p13 source metrics: countercap7000dia, lower6660dia,1200top fromFFL.
# Existingrear angular access is preserved; originaltwo700/750gaps are NOT imposed onoccludedcurrentrear.
vs=[];ts=[];N=100
for i in range(N+1):
 a=math.radians(-151+302*i/N)
 for r,y in [(3.33,.30),(3.47,1.15),(3.0,.30),(3.0,1.15)]:vs.append([r*math.sin(a),y,r*math.cos(a)])
for i in range(N):
 k=i*4;l=k+4
 for q in [[k,l,l+1,k+1],[k+3,l+3,l+2,k+2],[k+1,l+1,l+3,k+3],[k+2,l+2,l,k]]:ts.extend([q[0],q[1],q[2],q[0],q[2],q[3]])
for q in [[0,1,3,2],[N*4+2,N*4+3,N*4+1,N*4]]:ts.extend([q[0],q[1],q[2],q[0],q[2],q[3]])
write_mesh('Curved_Public_Counter_White_Enclosure',vs,ts,'Samewavecounter assembly corroborated by2016front+Nov2025field; restore outward-flaredfront(bottom6660dia/top7000cap) instead ofverticalcylinder. Diametricvalues originaldesigndatum, not fieldmeasurement.')
write_mesh('White_Curved_Countertop',*ring(3.0,3.5,1.15,1.20),'OriginalR3500/R3000 publicworkring, top1.20m fromp13completefront; keepactualcurrentfinish/rearcoverage.')
write_mesh('Counter_Public_Curved_Charcoal_Plinth',*ring(3.28,3.33,.035,.30),'Sourcep13base6660dia/300high; currentBDIphoto confirmsmetalbase, not charcoalpaint. Useexistingownsteelmaterial; doNOTcopy oldhorizontalgrillepattern overcurrentpunchedmetal.',newmat='steel')
write_mesh('Staff_Inner_Worktop',*ring(2.5,3.0,.80,.85,-139,139,80),'OriginalR2500/R3000 internalworkring andp15B850highfurniture. QualifiedoriginaldesignMODEL: currentrearoccluded; do not claim2025measured cabinetry.')
# Preserve current wave angular shape; fit height/radius to sameflaredpublicface.
w=copy.deepcopy(old['mesh_library']['Source_Counter_Charcoal_Wave_Front']);vs=[]
for x,y,z in w['vertices']:
 r=math.hypot(x,z);u=np.array([x,z])/r;ny=.30+(y-.275)*(.60/(.74-.275));nr=3.33+(3.47-3.33)*(ny-.30)/(.85)+.008+(r-3.129)
 vs.append([float(nr*u[0]),float(ny),float(nr*u[1])])
write_mesh('Source_Counter_Charcoal_Wave_Front',vs,w['triangles'],'Keepactualcurrentcharcoalwave angularpattern; carry samewave onto flaredcounterface. OriginalandNov2025bothshow thisdistinctivefront, no wholesale historicfinishswap.')
# Outer canopy correction is radial only.2025blacktruss/displayverticaldesign differs from2016whiteflatcanopy.
write_mesh('Black_Annular_Upper_Fascia',*ring(2.38,2.5,3.35,3.68,-180,180,96),'Sourcecanopy5000dia andfieldphotocanopyvisiblynarrowerthan7000counter. Preserve currentblackfascia/thickness120mm and ALLcurrentverticallevels; do not apply old3100overall to changedtruss/screens.')
write_mesh('Black_Canopy_Lower_Ring',*ring(2.35,2.40,3.15,3.20,-180,180,96),'Outer canopy radius minus560mm while preservinglower-ring50mmthickness/currentY.')

def trs(name,position=None,euler=None,scale=None,reason=''):
 o=orig[name];d={'operation':'adjust_only_own_local_TRS','id':name,'object_id':ids[name],'path':receipt['root_path']+'/'+o['group']+'/'+name,'expected_old_local_TRS':{k:o[k] for k in ['position','euler','scale']},'new_local_TRS':{'position':list(position if position is not None else o['position']),'euler':list(euler if euler is not None else o['euler']),'scale':list(scale if scale is not None else o['scale'])},'mesh_materials_unchanged':True,'reason':reason}
 if name in snap:d['expected_world_matrix_column_major']=snap[name]['world_matrix_column_major']
 changes.append(d)
def beam(name,a,b,width):
 a=np.array(a,float);b=np.array(b,float);v=b-a;length=np.linalg.norm(v);v/=length
 trs(name,(a+b)/2,[math.degrees(math.acos(np.clip(v[1],-1,1))),math.degrees(math.atan2(v[0],v[2])),0],[width,length/2,width],'Onlyouterradialendpoint reduced560mm; fixedinner1.00/1.05mhub,currentverticals and currenttrussstyle unchanged.')
for i in range(20):
 a=2*math.pi*i/20;b=2*math.pi*(i+1)/20
 beam(f'Radial_Upper_Chord_{i}',[math.sin(a),3.39,math.cos(a)],[2.41*math.sin(a),3.39,2.41*math.cos(a)],.035)
 beam(f'Radial_Lower_Chord_{i}',[math.sin(a),3.17,math.cos(a)],[2.37*math.sin(a),3.17,2.37*math.cos(a)],.035)
 beam(f'Radial_Diagonal_Web_{i}',[1.05*math.sin(a),3.39,1.05*math.cos(a)],[2.37*math.sin(a),3.17,2.37*math.cos(a)],.024)
 beam(f'Outer_Diagonal_Web_{i}',[2.38*math.sin(a),3.18,2.38*math.cos(a)],[2.39*math.sin(b),3.39,2.39*math.cos(b)],.022)
# Move existingactualsourcelettermesh withitsring, retaining glyph geometry/counterholes/material.
trs('Source_KORAIL_Station_Annular_Wordmark',[0,3.52,2.508],reason='Follownewringradius only, existingclosedglyphvertices/fontshape/size/currentY unchanged.')
# Move exactexistingdisplayassembliesinwardwithradius; currentY/heights/sourceglyphs unchanged.
for i,x in enumerate([-1.95,1.68]):
 z=math.sqrt(2.72**2-x*x);delta=np.array([x,z])*(-.56/2.72)
 for name in [f'Information_Display_Physical_Casing_{i}',f'Information_Display_Actual_Blue_Panel_{i}',f'Information_Display_Suspension_{i}',f'Source_Display_Visible_Content_Contour_{i}']:
  q=np.array(orig[name]['position']);q[[0,2]]+=delta;trs(name,q,reason='Coherentradialtranslation ofcompletecurrentdisplay+suspension+closedsourcecontent withcanopy; NOverticalhistoricshrink or glyphresize.')
# Put terminals onsupportednewpubliccountertop;oldradius2.28wasinsidecurrenttopinner2.40.
for i in range(2):
 name=f'Downloaded_Licensed_Staff_Terminal_{i}';q=np.array(orig[name]['position']);q[[0,2]]*=3.22/np.linalg.norm(q[[0,2]]);q[1]=1.203;trs(name,q,reason='Photoobservedmonitors mustsit onactualpublictop. Originalexistingradius2.28/2.29 lies insideoldcountertopinner2.40 and aboveoldworktop. Moveownparent only; licensedsourcegeometry remains unchanged.')
for i,a in enumerate([-90,-50,45,95]):
 ar=math.radians(a);delta=np.array([math.sin(ar),math.cos(ar)])*(3.25-2.77)
 for name in [f'Clear_Counter_Service_Shield_{i}',f'Clear_Shield_Weighted_Foot_{i}_0',f'Clear_Shield_Weighted_Foot_{i}_1']:
  q=np.array(orig[name]['position']);q[[0,2]]+=delta;q[1]+=.125;trs(name,q,reason='Retaincurrentfield-observedshield/feetgeometry; carrycompleteassembly onto newworkring and1200top.')
for i in range(4):
 oldname=f'Source_Notice_Acrylic_Pocket_{i}';q0=np.array(orig[oldname]['position']);delta=q0[[0,2]]*(3.25/np.linalg.norm(q0[[0,2]])-1)
 for name in [oldname,f'Source_Notice_White_Paper_{i}']:
  q=np.array(orig[name]['position']);q[[0,2]]+=delta;q[1]+=.125;trs(name,q,reason='Preserveexistingpaper/pocket/sourceownedsolids; coherentworktopcontacttranslationonly,no newnoticewords or photo planes.')
# Stronglycorroborated missingfrontKORAIL: exists inoriginalfrontANDcurrentphoto, absent fromown150partinventory.
syspec=importlib.util.spec_from_file_location('solid',R/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/photo_outline_to_solid.py');module=importlib.util.module_from_spec(syspec);syspec.loader.exec_module(module)
image=cv2.imread(str(P.parent/'BDI-P91-SOURCE-XREF694.jpeg'));crop=image[222:234,241:281];up=cv2.resize(crop,(800,240),interpolation=cv2.INTER_CUBIC);mask=np.uint8(np.min(up,axis=2)>135)*255;mask[:3]=0;mask[-3:]=0;mask[:,:3]=0;mask[:,-3:]=0
cv2.imwrite(str(P/'ActualCounterKORAIL-source-reference.jpg'),up);cv2.imwrite(str(P/'ActualCounterKORAIL-closed-glyph-mask.png'),mask);glyph=module.closed_solid_from_mask(mask,.46,.138,.003,minimum_area=35,epsilon=.3)
newmeshes['ActualCounterFrontKORAILClosedSourceGlyphs']={k:glyph[k] for k in ['vertices','triangles','source_polygons']};newglyph={'operation':'new_own_closed3D_sourceglyph_mesh','id':'OriginalAndCurrentCounterFrontKORAIL','group':'Actual_Source_Contour_Signs','mesh_key':'ActualCounterFrontKORAILClosedSourceGlyphs','material_from_existing_own_packet_key':'white','position_local':[0,.45,3.363],'euler_local':[0,0,0],'scale_local':[1,1,1],'source_current_file':str((P.parent/'BDI-P91-SOURCE-XREF694.jpeg').relative_to(R)),'source_crop_px':[241,222,281,234],'source_original':'authentic2016p13frontKORAILmark','real_glyph_counterholes':True,'no_raster_Text_TMP_fontatlas_or_explanationlabel':True,'facing':'samecurrentfrontdirectionasexistingannularwordmark','qualification':'Actualtwo-epochmarkidentity; physicalsize/height/frontangularplacementqualifiedMODEL. Position followsproposedfrontwave3.33..3.47profile, notafloorobject.'}
changes.append(newglyph)
packet={'schema':'chooguard.r30.information_desk_original_current_core_proposal.v1','status':'READONLY_SOURCE_QUALIFIED_CORRECTION_PACKET_AWAITING_ROOT_SCOPED_APPLY','source_original_file':'.state/ACTIVE-ALL2F-COMPLETION-R26/original-design-discovery/storyway-public-contracts/operator-archive-r27/transport-mirror-r28/hall2016-chain-information-original-drawings-FILE00.pdf','source_pages1based':[13,14,15,16],'actual_current_photo_epoch':'BDIresearcherNov2025; report2026March','current_own_root_id':receipt['root_object_id'],'current_own_root_path':receipt['root_path'],'fixed_native024_id':receipt['reused_fixed_column_id'],'fixed_world_xz':receipt['column_axis_world_xz'],'root_TRS_change':False,'source_plan_metric_core_m':{'countertop_diameter':7.0,'counter_lower_diameter':6.660,'canopy_diameter':5.0,'publicworkring_radii':[3.0,3.5],'privateworkring_radii':[2.5,3.0],'countertop_from_FFL':1.2,'privateworktop_nominal':.85,'source_original_total_canopy_height_NOT_APPLIED':3.1},
 'photographic_metric_equality_proven':False,'joint_source_support':'Currentphotoandoriginalshareuniqueoffcenterfallingwavecounter/centeredcolumn/cap/counterfrontKORAIL. Currentwholephoto694canopyprojectedwidth~200px vs counter~320px showscanopymarkedlysmaller; currentmodel6.12dia vs6.44capdia nearlyequal is incompatiblewiththatmorphology. Source5000/7000metrics are source-qualifiedMODELvalues, notcalibratedphoto measurements.',
 'original_vertical_reading':'p13frontcountertop1.20m = floor->plinth300 + plinth->top900. Canopy3100 = bottomplinth300 + middle2500 + topband300; middle2500 includescounter900+gap1600. Do notsum p16nestedgeneric1200withanother300 toclaim1500; p16detaildatum isnot a newcompletefront survey.',
 'historical_design_not_replayed':['whiteflat2016canopyfinish replacedbyactualcurrentblackannulartruss','original3100overallwouldlowercurrenthangingdisplays/clearanceswithoutcurrentfieldmetricproof; preserveALLcurrentcanopy/display/capverticals','2016oldbefore3280counter unrelated','originaltwo700/750staffgaps notimposedonoccludedcurrentrear; current-151..151angularcoverage fixed','oldhorizontalmetalgalley notsubstitutedforcurrentpunchedmetalbase','eightstaffchairs/hiddencabinetcontents notaddedfromsource alone'],
 'preserve_exact':['native024/rootaxis/worldTRS','floor','columnwrapandwhiteflaredcapcurrentshape','currentallcanopy/displayverticalpositions','sourceglyphmeshletters/counterholes/materials; onlyannularwordmarkanddisplayglyphcoherenttranslation','otherpublicequipment','currentrearstaffopeningangularcoverage','licensedterminalsourceAssets'],
 'changes':changes,'mesh_library':newmeshes,'current_actual_snapshot':'.state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-007/CURRENT-2F-MESH-SNAPSHOT.json','production_source_analysis_only_noSceneQA':True,'Scene_Assets_mutated':False,'Light_Text_TMP_photo_only_sign_plane':False,'whole2F_complete_claim':False}
(P/'ORIGINAL-CURRENT-CORE-CORRECTION-PACKET-R30.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
print(json.dumps({'changes':len(changes),'mesh_derivatives':sum(x['operation']=='replace_only_own_mesh_derivative' for x in changes),'own_TRS':sum(x['operation']=='adjust_only_own_local_TRS' for x in changes),'newphysicalsourceglyphs':glyph['source_polygons'],'countertopradiusfactor':3.5/3.22,'canopyradiusfactor':2.5/3.06,'packetbytes':(P/'ORIGINAL-CURRENT-CORE-CORRECTION-PACKET-R30.json').stat().st_size}))
