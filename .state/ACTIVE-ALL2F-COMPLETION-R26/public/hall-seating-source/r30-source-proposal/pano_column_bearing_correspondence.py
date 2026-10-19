from pathlib import Path
import numpy as np,json,itertools
from scipy.optimize import least_squares
B=Path(__file__).resolve().parent
xy=np.array([[1850,0],[4300,0],[7480,0],[10890,0]],float); bearing=xy[:,0]/11968*2*np.pi
axes={'024':[-45.2784910027,-50.4336009452],'022':[-20.0787890431,-50.3104794654],'028':[-19.9713599700,-72.3002158701],'032':[-45.1710553178,-72.4233275808]}; keys=list(axes)
rows=[]
for mirrored in [False,True]:
 ks=keys if not mirrored else keys[::-1]
 for shift in range(4):
  ids=ks[shift:]+ks[:shift];pts=np.array([axes[k]for k in ids])
  def fun(p):
   v=pts-p[:2];pred=np.arctan2(v[:,1],v[:,0])-p[2];d=pred-bearing;return np.arctan2(np.sin(d),np.cos(d))
  best=None
  for rot in np.arange(-np.pi,np.pi,np.pi/4):
   r=least_squares(fun,[-32,-61,rot],max_nfev=200);cost=np.sqrt(np.mean(fun(r.x)**2))
   if best is None or cost<best[0]:best=(cost,r)
  cost,r=best;rows.append({'sourceABCDis_columns':ids,'camera_q':r.x[:2].tolist(),'q_angle_minus_equirect_azimuth_rad':r.x[2],'angular_rms_degrees':float(np.degrees(cost)),'per_column_degrees':np.degrees(fun(r.x)).tolist(),'mirrored':mirrored})
rows.sort(key=lambda x:x['angular_rms_degrees']);(B/'PANO01-FOUR-AXIS-BEARING-CANDIDATES.json').write_text(json.dumps({'source_axis_x_pixels_11968':xy[:,0].tolist(),'source_y_not_used_for_azimuths':True,'hypotheses':rows,'status':'construction correspondence candidates, source mezzanine/escalator adjacency chooses cyclic identity; not a surveyed camera or QA verdict'},indent=2));print(json.dumps(rows,indent=2))
