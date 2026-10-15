"""User explicitly authorized Python color-only correction on 2026-10-13."""
from pathlib import Path
import hashlib,json
import numpy as np
from PIL import Image
BASE=Path(__file__).resolve().parent
src=BASE/'assets/generated/object-interaction-alpha.png'
dst=BASE/'assets/generated/object-interaction-palette.png'
colors=['#AD5735','#F5F5F5','#D5D5D5','#222222']
palette=np.array([[int(h[i:i+2],16) for i in (1,3,5)] for h in colors],dtype=np.int32)
a=np.array(Image.open(src).convert('RGBA'));original_alpha=a[:,:,3].copy()
# Exact nearest-palette mapping, no dithering, no spatial/alpha changes.
rgb=a[:,:,:3].astype(np.int32);dist=((rgb[:,:,None,:]-palette[None,None,:,:])**2).sum(axis=3)
index=dist.argmin(axis=2);a[:,:,:3]=palette[index].astype(np.uint8)
Image.fromarray(a,'RGBA').save(dst)
out=np.array(Image.open(dst).convert('RGBA'))
assert out.shape==a.shape and np.array_equal(out[:,:,3],original_alpha)
actual=set(map(tuple,out[:,:,:3].reshape(-1,3).tolist()));allowed=set(map(tuple,palette.tolist()));assert actual<=allowed
assert np.array_equal(out[:,:,3]>0,original_alpha>0)
record=dict(source=src.name,output=dst.name,method='Nearest squared sRGB distance to the fixed four-token palette; no dithering; alpha and dimensions byte-identical',explicit_user_authorization='Python으로 네 색상 고정',palette=colors,dimensions=[out.shape[1],out.shape[0]],alpha_exact=True,nontransparent_mask_exact=True,rgb_values=sorted(actual),source_sha256=hashlib.sha256(src.read_bytes()).hexdigest(),output_sha256=hashlib.sha256(dst.read_bytes()).hexdigest(),scope='Color-only derivative of image2.5 concept. Original and alpha derivative retained. No geometry/semantic/efficacy claim.')
(BASE/'palette_correction_checks.json').write_text(json.dumps(record,ensure_ascii=False,indent=2));print(json.dumps(record,ensure_ascii=False))
p=BASE/'assets/generated/GENERATION_PROVENANCE.json';d=json.loads(p.read_text());entry={'asset':'object-interaction.png','color_derivative':dst.name,**record}
if isinstance(d,list):d.append(entry)
else:d.setdefault('user_authorized_color_corrections',[]).append(entry)
p.write_text(json.dumps(d,ensure_ascii=False,indent=2))
