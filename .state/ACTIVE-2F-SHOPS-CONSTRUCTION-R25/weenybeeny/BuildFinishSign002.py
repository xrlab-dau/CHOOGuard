from pathlib import Path
import json,math,hashlib,sys,datetime
import numpy as np,cv2

ROOT=Path('/Users/um-yunsang/CHOOGuard');BASE=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25';OWN=BASE/'weenybeeny';A=OWN/'authoring'
sys.path.insert(0,str(BASE))
from photo_outline_to_solid import closed_solid_from_mask
B=json.loads((OWN/'MODEL-PLACEMENT-BASIS-R25.json').read_text());O=np.array(B['origin_q']);U=np.array(B['u_front_right']);V=np.array(B['v_into_store']);W=B['width_m_MODEL'];D=B['depth_m_MODEL'];yaw=math.degrees(math.atan2(V[0],V[1]))
def q(x,z):return (O+x*U+z*V).tolist()
def sha(f):return hashlib.sha256(Path(f).read_bytes()).hexdigest()
p={'store_key':'weenybeeny','scope':'C180_R25_WEENYBEENY_SOURCE_FINISH_SIGN_002','origin_q':O.tolist(),'materials':{'checker_cream':{'color':[.88,.87,.81,1],'smoothness':.61},'checker_charcoal':{'color':[.045,.041,.038,1],'smoothness':.57}},'mesh_library':{},'objects':[]}
def obj(id,kind,x,z,y,mat,**kw):
 a={'id':id,'kind':kind,'q':q(x,z),'y':y,'yaw':yaw,'material':mat,'collider':False,**kw};p['objects'].append(a);return a
def meshobj(id,mesh,x,z,y,mat,**kw):
 p['mesh_library'][id]=mesh;return obj(id,'mesh',x,z,y,mat,mesh_key=id,**kw)

# Only an independent 2mm source-pattern interior finish. No Main/source floor edit or collider.
left,right,front,rear=.09,W-.085,.12,D-.095
pitch=.45
ix=0
x=left
while x<right-.0001:
 z=front;iz=0;xx=min(x+pitch,right)
 while z<rear-.0001:
  zz=min(z+pitch,rear)
  obj('CHECKER_FITOUT_2MM_%02d_%02d'%(ix,iz),'box',(x+xx)/2,(z+zz)/2,.002,'checker_cream' if (ix+iz)%2==0 else 'checker_charcoal',size=[xx-x-.0005,.002,zz-z-.0005],group='SourceCheckerIndependentNoncolliderFinish',source_ids=['SEP2026_03_15_16_VISIBLE_CHECKER'],structural_floor=False)
  iz+=1;z=zz
 ix+=1;x=xx

# Actual full01 front face; full07 confirms depth, red body and the inside-facing same two-line mark.
shape=cv2.imread(str(A/'secondary-bean-clean-physical-face-mask.png'),0)
shape=cv2.GaussianBlur(shape,(7,7),1.5);shape=(shape>127).astype('uint8')*255
glyphs=cv2.imread(str(A/'secondary-bean-two-line-glyph-mask.png'),0)
cv2.imwrite(str(A/'secondary-bean-final-physical-outline-mask.png'),shape)
width,height=.98,.825
body=closed_solid_from_mask(shape,width,height,.072,minimum_area=25,epsilon=.75)
meshobj('SECONDARY_BEAN_CLOSED_RED_BODY',body,1.22,.19,2.06,'red',yaw=180+yaw,group='SourceSecondaryPhysicalSign',source_ids=['SEP2026_01_FULL_BEAN_CONTOUR','SEP2026_07_DEPTH_INTERIOR_FACE'])
rim=cv2.subtract(cv2.dilate(shape,np.ones((5,5),'uint8')),shape)
rimmesh=closed_solid_from_mask(rim,width,height,.078,minimum_area=20,epsilon=.7)
meshobj('SECONDARY_BEAN_CLOSED_CHROME_PERIMETER',rimmesh,1.22,.187,2.06,'steel',yaw=180+yaw,group='SourceSecondaryPhysicalSign')
# Letter glyphs are closed solids with actual holes, independently editable on both faces.
n,labels,stats,centers=cv2.connectedComponentsWithStats(glyphs)
count=0
for i in range(1,n):
 if stats[i,cv2.CC_STAT_AREA]<20:continue
 mask=(labels==i).astype('uint8')*255;mesh=closed_solid_from_mask(mask,width,height,.008,minimum_area=15,epsilon=.4)
 for side,z,angle in [('HALL_FRONT',.184,180+yaw),('INTERIOR_BACK',.268,yaw)]:
  meshobj('SECONDARY_BEAN_%s_SOURCE_GLYPH_%02d'%(side,count),mesh,1.22,z,2.06,'logo_light',yaw=angle,group='SourceSecondaryPhysicalSign',source_ids=['SEP2026_01_ACTUAL_TWO_LINE_GLYPHS','SEP2026_07_INTERIOR_TWO_LINE_GLYPHS'])
 count+=1
obj('SECONDARY_BEAN_RED_SUSPENSION_BAR','box',1.04,.22,2.60,'red',size=[.025,.32,.035],group='SourceSecondaryPhysicalSign')
obj('SECONDARY_BEAN_FINE_SUSPENSION_WIRE','box',1.47,.22,2.61,'steel',size=[.004,.31,.004],group='SourceSecondaryPhysicalSign')
obj('SECONDARY_BEAN_UPPER_CEILING_BRACKET','box',1.04,.22,2.765,'steel',size=[.09,.015,.08],group='SourceSecondaryPhysicalSign')

authority={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scope':p['scope'],'root_followup':'Root explicitly allows2-3mm independent noncollider source-pattern finish only inside own booth as interior fitout; existing Main/official floor mesh/TRS/material/collider/holes fixed. Supplement actual secondary redbean sign using full01/07 and other source photographs, closed shape/frame/both faces/thickness; no text/photo-plane.','actual_floor_mutation':False,'no_QA_Play_tests_probes_reopen_capture':True,'source_newly_directly_viewed':['ROOT-zzang2mama-224404805472-01.jpg','ROOT-pig9951-224292041260-17.jpg'],'actual_native_cutout_relation':'Own interior finish is strictly between east native wall westface and064eastface; rear limit064northface precedes EV_B envelope at70.3068 by about0.5m. Actual floor/exterior/openings unchanged; finish excludes perimeter and stays behind own front portal plane.','finish_bounds_uv':[left,front,right,rear],'finish_thickness_m':.002,'noncollider':True,'tile_pitch_m_MODEL':pitch,'secondary_sign_dimensions_m_MODEL':[width,height,.072],'source_shape_not_surveyed':True,'source_mask_cleanup':'Red-face threshold isolated from actual full01; only small lower reflection/bracket contamination removed, subpixel noise smoothed. Actual entire face is visible; source projection distortion not surveyed.','secondary_glyph_components':count}
(OWN/'USER-AUTHORITY-FINISH-SIGN002.json').write_text(json.dumps(authority,ensure_ascii=False,indent=2))
(OWN/'FINISH-SIGN-PACKET002.json').write_text(json.dumps(p,ensure_ascii=False,separators=(',',':')))
stats={'objects':len(p['objects']),'checker_tiles':sum(x['group']=='SourceCheckerIndependentNoncolliderFinish' for x in p['objects']),'sign_closed_mesh_objects':sum(x['kind']=='mesh' for x in p['objects']),'glyphs_per_face':count,'packet_sha256':sha(OWN/'FINISH-SIGN-PACKET002.json')}
(OWN/'AUTHORING-FINISH-SIGN002.json').write_text(json.dumps(stats,indent=2))
cs=(OWN/'ApplyFinishSign002.template.cs').read_text().replace('__PACKET_SHA256__',stats['packet_sha256'])
(OWN/'ApplyFinishSign002.cs').write_text(cs)
print(json.dumps(stats,indent=2))
