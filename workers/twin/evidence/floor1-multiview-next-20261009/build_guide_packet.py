"""Manual source-pixel scaffold and standalone architectural QA figures."""
import json, hashlib
from pathlib import Path
import numpy as np
from PIL import Image
from shapely.geometry import Polygon, LineString

ROOT=Path('/Users/um-yunsang/CHOOGuard')
OUT=ROOT/'workers/twin/output/floor1-multiview-next-20261009'
IMAGE=ROOT/'asset-library/research-public/2026-10-09/floor1-multiview-next/source05-yoonsscom/img_2.jpg'
spec=json.loads((ROOT/'.planning/2026-09-23-video-twin/1f-implementation/interior-spec.json').read_text())
proposal=json.loads((OUT/'conservative-proposals.json').read_text())
units={
 1:[[268,309],[312,315],[326,331],[326,351],[263,339]],
 2:[[351,348],[372,328],[387,329],[387,362],[351,355]],
 3:[[407,301],[435,301],[440,369],[411,365]],
 4:[[466,301],[500,301],[499,371],[468,370]],
 5:[[526,334],[541,334],[562,355],[562,365],[523,367]],
 6:[[622,312],[693,312],[702,345],[626,358]],
 7:[[693,312],[751,312],[765,330],[702,345]],
 8:[[817,238],[878,238],[878,277],[902,277],[902,299],[815,331],[808,329]],
 9:[[701,203],[735,201],[737,249],[701,249]],
 10:[[667,203],[701,203],[701,237],[667,237]],
 11:[[646,203],[667,203],[667,237],[646,237]],
 12:[[627,203],[646,203],[646,237],[627,237]],
 13:[[592,203],[627,203],[627,248],[592,248]],
 14:[[556,203],[592,203],[592,248],[556,248]],
 15:[[524,203],[556,203],[556,265],[524,265]],
 16:[[464,239],[493,239],[493,203],[524,203],[524,265],[464,265]]}
guide_names={1:'모리종',2:'편의점',3:'비엔씨도넛',4:'아트박스',5:'환공어묵',6:'버거앤타코',7:'편의점',8:'양산국밥',9:'복순도가',10:'아트센트',11:'포레일',12:'비엔씨박스앤컵',13:'채선당 행복가마솥밥',14:'개미집',15:'새벽집국밥국수',16:'파리바게뜨',17:'물품보관함'}
gaps=[{'id':'P1','bbox':[327,346,351,358],'confidence':'clear diagram gap'},
      {'id':'P2','bbox':[388,329,410,366],'confidence':'clear passage with circulation core'},
      {'id':'P3','bbox':[440,301,466,372],'confidence':'clear through passage'},
      {'id':'P4','bbox':[501,334,524,372],'confidence':'clear passage beside circulation core'},
      {'id':'P5','bbox':[562,348,622,373],'confidence':'clear wide through passage'},
      {'id':'P6','bbox':[765,329,813,344],'confidence':'clear diagram gap'},
      {'id':'P7','bbox':[239,288,267,313],'confidence':'schematic end-wing access, exact door leaf unconfirmed'}]
features=[{'id':'core-front-left','kind':'elevator/stair cluster','bbox':[340,299,407,329]},
          {'id':'core-front-center','kind':'elevator/stair cluster','bbox':[498,300,551,334]},
          {'id':'core-rear-left','kind':'escalator/stair symbol','bbox':[422,240,437,257]},
          {'id':'core-rear-right','kind':'escalator/stair symbol','bbox':[729,203,752,223]},
          {'id':'WC-left-upper','kind':'toilet','bbox':[249,238,268,256]},
          {'id':'WC-left-lower','kind':'toilet','bbox':[259,263,279,281]},
          {'id':'WC-right-upper','kind':'toilet','bbox':[875,166,907,188]},
          {'id':'WC-right-lower','kind':'toilet','bbox':[875,202,908,224]},
          {'id':'storage17','kind':'locker label (not physical footprint)','bbox':[351,254,410,277]}]

# Schematic affine scaffold anchored on front access gaps and end toilets.
# These correspondences are approximate diagram landmarks, not surveyed points.
px=np.array([[339,354],[398,365],[453,371],[511,371],[591,367],[788,336],[258,253],[885,193]],float)
uv=np.array([[41.5,-35.5],[25.85,-40.57],[9.95,-41.56],[-4.49,-42.28],[-21.1,-41.55],[-64.58,-36.05],[60.51,-12.5],[-87.6,-13.5]])
fit_idx=[0,2,4,5,6,7]; holdout=[1,3]
X=np.column_stack([px,np.ones(len(px))]); M=np.linalg.lstsq(X[fit_idx],uv[fit_idx],rcond=None)[0]
err=np.linalg.norm(X@M-uv,axis=1)
def to_uv(p): return (np.column_stack([np.asarray(p,float),np.ones(len(p))])@M).tolist()
def to_px(p): return ((np.asarray(p,float)-M[2])@np.linalg.inv(M[:2])).tolist()

semantics={'schema':'chooguard.floor1-guide-semantic-scaffold.v1','source_url':'https://m.blog.naver.com/yoonsscom/224376213780',
 'source_observation_date_claim':'2026-08-11 according to author title; independently authenticated drawing revision unknown',
 'source_posted':'2026-08-12T13:34+09:00','floor':'1F (visible printed panel)','reference_only':True,'physical_accuracy_verified':False,
 'native_image':str(IMAGE.relative_to(ROOT)),'native_image_sha256':hashlib.sha256(IMAGE.read_bytes()).hexdigest(),'native_image_size':[966,725],
 'guide_extent_pixels_approx':[220,154,916,374],'pixel_digitization_tolerance_approx':3,
 'units':[{'boardNo':n,'guide_name':guide_names[n],'source_polygon_pixels_approx':p,'scaffold_polygon_UV_approx':to_uv(p),'native_match':next(({'mid':u['mid'],'current_name':u['name']} for u in spec['units'] if u['boardNo']==n),None)} for n,p in units.items()],
 'storage17':features[-1],'front_access_gaps':gaps,'circulation_toilets':features[:-1],
 'adjacency':{'front_row':[1,2,3,4,5,6,7,8],'rear_row':[16,15,14,13,12,11,10,9],
 'public_food_aisle':'Continuous open area between rear-row shop fronts and front-row backs; ends connect to toilets/end-wing areas.',
 'guide_shop_11_depth_pixels_approx':34,'guide_11_to_6_aisle_pixels_approx':75,
 'guide_aisle_over_shop_depth_ratio_approx':75/34,'note':'Schematic proportions; do not equate this ratio to surveyed wall distances.'},
 'registration':{'method':'Affine schematic scaffold in existing station UV; no metric calibration',
 'pixel_homogeneous_row_to_UV_matrix':M.tolist(),'anchors':[{'source_pixel':p.tolist(),'estimated_native_UV':u.tolist(),'role':'withheld' if i in holdout else 'fit','residual_model_units':float(err[i])} for i,(p,u) in enumerate(zip(px,uv))],
 'withheld_residual_model_units':err[holdout].tolist(),'max_residual_over_native_width_148':float(max(err)/148),
 'model_unit_display_tolerance':3.5,'limitation':'Native correspondences use estimated gap midpoints/toilet centers. Withheld anchors test scaffold consistency only. They do not validate metric wall positions.'}}
(OUT/'guide-semantics.json').write_text(json.dumps(semantics,ensure_ascii=False,indent=2)+'\n')

