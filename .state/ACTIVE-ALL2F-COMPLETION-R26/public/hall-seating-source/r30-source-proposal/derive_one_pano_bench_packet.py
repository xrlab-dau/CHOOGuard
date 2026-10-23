from pathlib import Path
import json,numpy as np
from scipy.optimize import least_squares
B=Path(__file__).resolve().parent;W=11968;H=5984
# Source azimuth controls are large-column stem centers, not exterior C113 vent pixels.
ids=['024','022','028','032'];axes=np.array([[-45.2784910027,-50.4336009452],[-20.0787890431,-50.3104794654],[-19.9713599700,-72.3002158701],[-45.1710553178,-72.4233275808]])
sx=np.array([1850,4334,7480,10890]);az=sx/W*2*np.pi
# Topology: source B-C is restaurant-mezzanine row; source C is column beside restaurant-escalator shoulder.
def fun(p):
 v=axes-p[:2];d=np.arctan2(v[:,1],v[:,0])-p[2]+az;return np.arctan2(np.sin(d),np.cos(d))
r=least_squares(fun,[-32,-61,3.2]);cam=r.x[:2];rot=r.x[2];res=np.degrees(fun(r.x))
baseB=np.array([4334,3182]);distanceB=np.linalg.norm(axes[1]-cam);height=distanceB*np.tan((baseB[1]/H-.5)*np.pi)
# Invert the perspective extraction exactly: yaw=0.45*2pi, pitch=-25, h/v FOV75, 1400square.
def prays(px):
 p=np.asarray(px,float);f=700/np.tan(np.radians(75/2));x=(p[:,0]-700)/f;y=-(p[:,1]-700)/f;z=np.ones(len(p));pitch=np.radians(-25);yaw=.45*2*np.pi;yy=y*np.cos(pitch)+z*np.sin(pitch);zz=z*np.cos(pitch)-y*np.sin(pitch);xx=x*np.cos(yaw)+zz*np.sin(yaw);zf=zz*np.cos(yaw)-x*np.sin(yaw);phi=np.arctan2(xx,zf)%(2*np.pi);theta=np.arctan2(yy,np.hypot(xx,zf));return phi,theta
def ptoq(px,level_above_floor=0):
 phi,theta=prays(px);rad=(height-level_above_floor)/(-np.tan(theta));ang=rot-phi;return cam+np.column_stack([rad*np.cos(ang),rad*np.sin(ang)])
footpx=[[377,1013],[1066,1238]];footq=ptoq(footpx);center=footq.mean(0);d=footq[1]-footq[0];d/=np.linalg.norm(d)
cornerpx=[[387,752],[1142,988],[1171,1125],[238,879]];corners=ptoq(cornerpx,.43)
phi,theta=prays(footpx)
print('cam',cam,'r',rot,'heightfromB',height,'rms',np.sqrt(np.mean(res**2)),'res',res,'feet',footq,'cent',center,'axis',d,'footdist',np.linalg.norm(footq[1]-footq[0]),'corners',corners)
data={'schema':'chooguard.r30.one-public-bench-source-model-pointmigration.v1','status':'READONLY_SOURCE_CONSTRUCTION_PROPOSAL_NOT_SCENE_AUTHORITY','source':'source-evidence/retained-work-originals/workers/twin/output/architecture-resume-20261010/MAIN1F-native-panorama-reference-20261011/native/google-pano-01-provider-s0.jpg','source_camera':'ArashiVision insta360x3','exif_DateTime':'2023:12:01 20:49:18','DateTimeOriginal_independently_established':False,'source_pixel_dimensions':[W,H],'source_epoch_current_2026_inventory_claim':False,'fixed_q_axes_by_source_order_ABCD':[{'column':ids[i],'q':axes[i].tolist(),'source_stem_x':int(sx[i])}for i in range(4)],'column_identity_basis':['Source B/C are the restaurant-mezzanine side of four structural axes; source C is at actual restaurant-escalator column shoulder; A/D are opposite hall-side pair.','Native structural row and escalator/facade topology fix cyclic identity; current generated information desk is not used as identity evidence.'],'projection_scope':'Qualified source MODEL point migration into current accepted axes; official row ratios are not a physically calibrated room. No source warp or fixed-geometry change.','bearing_pose_q':cam.tolist(),'q_angle_minus_negative_equirect_azimuth_rad':float(rot),'column_bearing_residual_degrees':res.tolist(),'column_bearing_rms_degrees':float(np.sqrt(np.mean(res**2))),'bearing_residual_qualifier':'Residual is source-to-current-geometry ratio mismatch; not AAA or surveyed exactness. No forced eye height.','camera_height_above_floor_derived_from_B_base_m':float(height),'B_visible_floor_ring_center_source_px':baseB.tolist(),'single_bank_source_view':'PANO01-SOURCE-BENCH-FAMILY.jpg','single_bank_identity':'PANO01 foreground priority-design backless wood bank, source visible 3 seat zones and two gray divider rails','pedestal_floor_contacts_in_source_perspective_1400':footpx,'pedestal_floor_contacts_equirect_px':np.column_stack([phi/(2*np.pi)*W,(.5-theta/np.pi)*H]).tolist(),'pedestal_floor_contacts_q':footq.tolist(),'proposed_center_q':center.tolist(),'proposed_long_axis_q_unit':d.tolist(),'projected_seat_top_corners_q':corners.tolist(),'seat_top_height_above_floor_source_MODEL_m':.43,'source_dimensions_not_field_measurements':True,'Scene_Assets_changed':False,'native_vent_panels_rejected_as_interior_controls':True,'native_floor_opening_column_info024_circulation_changes_authorized':False}
(B/'PANO01-ONE-BANK-POINT-MIGRATION-R30.json').write_text(json.dumps(data,ensure_ascii=False,indent=2))
