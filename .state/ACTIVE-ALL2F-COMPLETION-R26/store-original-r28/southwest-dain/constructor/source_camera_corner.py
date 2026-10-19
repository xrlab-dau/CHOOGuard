import numpy as np,cv2,json,math
from pathlib import Path
W=Path(__file__).resolve().parent
# Original camera EXIF: Galaxy S23, 23mm35-equivalent, 4000x3000 original field Aug7 2024.
f=5000*23/math.hypot(36,24);K=np.array([[f,0,2000],[0,f,1500],[0,0,1.]])
# Unwarped source actual glazing top/floor endjamb pixels. Coordinates in original4000x3000.
uv=np.array([[2046,1450],[2076,2255],[3368,1412],[3437,2215]],float)
# Flip packetY to actualqy to make right-handed OpenCV world coordinates.
xyz=np.array([[-15.55,9.4,40.22],[-15.55,7.0,40.22],[-15.48,9.4,34.55],[-15.48,7.0,34.55]],float)
okay,rv,tv=cv2.solvePnP(xyz,uv,K,None,flags=cv2.SOLVEPNP_ITERATIVE);RR=cv2.Rodrigues(rv)[0];C=-RR.T@tv;pr=cv2.projectPoints(xyz,rv,tv,K,None)[0].reshape(-1,2)
out={'source':'DAILY-FIELD-16.jpg','EXIF':{'field_time':'2024-08-07 09:01:08','camera':'Galaxy S23','focal35equiv_mm':23,'original_pixels':[4000,3000]},'intrinsic_source':'35mm equivalent focal and full original diagonal; distortion coefficients unavailable, zero-distortion approximation','conditional_corner_correspondence':'PICKED-mainface endjambs correspond to Native0 mainlongface interval y[-40.22,-34.55]; frontglass topheight2.40m from existing model/source-family is MODEL','chosen_pixel_points':uv.tolist(),'world_points_qx_worldY_actualqy':xyz.tolist(),'camera_position_actual_qx_worldY_actualqy':C.ravel().tolist(),'camera_position_packet_qx_worldY_minusqy':[float(C[0,0]),float(C[1,0]),float(-C[2,0])],'projection_residual_px':np.linalg.norm(pr-uv,axis=1).tolist(),'predicted_points':pr.tolist(),'candidate_return_corner_projections':[],'not_a_Scene_validation_or_changed_image':True,'qualification':'Source-only conditional pose; does not establish field surveyed geometry or unique current tenant subdivision.'}
# Compare two physically different native corner returns without moving their real coordinate lines.
for name,a,b in [('POSITIVE_QX_native_glazed_return',[-15.38,7,-34.50],[-12.20,7,-34.51]),('NEGATIVE_QX_EV_gap_north_return',[-15.53,7,-34.81],[-18.20,7,-34.81])]:
 pts=np.array([[a[0],a[1],-a[2]],[b[0],b[1],-b[2]],[b[0],9.4,-b[2]],[a[0],9.4,-a[2]]]);pix=cv2.projectPoints(pts,rv,tv,K,None)[0].reshape(-1,2);out['candidate_return_corner_projections'].append({'name':name,'floor_and_top_source_projection_pixels':pix.tolist()})
(W/'DAILY16-CONDITIONAL-CAMERA-AND-TWO-NATIVE-RETURN-CORNERS-R29.json').write_text(json.dumps(out,indent=2));print(json.dumps(out))
