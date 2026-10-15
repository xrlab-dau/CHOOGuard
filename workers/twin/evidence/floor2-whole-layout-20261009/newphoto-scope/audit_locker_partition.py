from pathlib import Path
import json,hashlib,struct,copy,re
import numpy as np
O=Path('workers/twin/output/floor2-whole-layout-20261009/newphoto-scope/locker-withholding');E=Path('workers/twin/evidence/floor2-whole-layout-20261009/newphoto-scope/locker-withholding');O.mkdir(exist_ok=True);E.mkdir(exist_ok=True)
hb=lambda b:hashlib.sha256(b).hexdigest();sha=lambda p:hb(Path(p).read_bytes())
src=Path('workers/twin/evidence/floor2-whole-layout-20261009/seal_native_stair_channels.py').read_text();exec(src[src.index('def ints'):src.index('entries={')])
meta=json.load(open('.planning/2026-10-09-floor2-completion-pass2/locker-full-channels/metadata.json'));entries={e['role']:e for e in meta['entries']};data={k:read(e)for k,e in entries.items()};assert all(complete(d)==entries[k]['complete_channel_digest']for k,d in data.items())
spec=json.load(open('content/world/observed-interiors/2f-main-enhance.json'));bank=next(e for e in spec['elements']if e['id']=='2FM-LOCKERS');g=bank['geometry'];p=bank['params']
f=np.float32;V=lambda x:np.array(x,dtype=np.float32);up=V([0,1,0]);kp=lambda u,v,y:V([v,y,u]);a=kp(*g['edge'][0],g['y']);b=kp(*g['edge'][1],g['y']);ex=(b-a)/f(np.linalg.norm(b-a));n=V([-ex[2],0,ex[0]]);toward=kp(*p['faceToward'],g['y']);mid=(a+b)*f(.5)
if np.dot(toward-mid,n)<0:n=-n
L=f(np.linalg.norm(b-a));cw=f(p.get('columnWidth',.45));dp=f(p.get('depth',.55));pl=f(p.get('plinth',.1));rows=list(map(f,p.get('rows',[.46,.36,.36,.36])));cols=int(np.floor(f(L/cw+f(1e-3))));W=f(cols)*cw;rowsSum=f(0)
for r in rows:rowsSum=f(rowsSum+r)
H=f(f(pl+rowsSum)+f(.06));c0=mid+n*dp*f(.5);front=c0+n*dp*f(.5)
batches={k:[]for k in entries};primitivecounts={k:0 for k in entries}
def rect(k,o,x,y,w,h,norm,label):
 o=V(o);w=f(w);h=f(h);verts=V([o,o+x*w,o+x*w+y*h,o+y*h]);uv=V([[0,0],[w,0],[w,h],[0,h]]);wind=[0,1,2,0,2,3]if np.dot(np.cross(verts[1]-verts[0],verts[2]-verts[0]),norm)>=0 else[0,2,1,0,3,2];normal=norm/f(np.linalg.norm(norm));primitivecounts[k]+=1
 for tri in np.array(wind).reshape(-1,3):batches[k].append({'positions':verts[tri],'uv':uv[tri],'normal':normal,'primitive':label})
def rc(k,c,x,y,w,h,n,label):rect(k,c-x*f(w)*f(.5)-y*f(h)*f(.5),x,y,w,h,n,label)
def box(k,c,x,y,z,s,label,bottom=True,back=True):
 sx,sy,sz=map(f,s);hx=x*sx*f(.5);hy=y*sy*f(.5);hz=z*sz*f(.5)
 rect(k,c-hx-hy+hz,x,y,sx,sy,z,label+'/front')
 if back:rect(k,c-hx-hy-hz,x,y,sx,sy,-z,label+'/back')
 rect(k,c+hx-hy-hz,z,y,sz,sy,x,label+'/right');rect(k,c-hx-hy-hz,z,y,sz,sy,-x,label+'/left');rect(k,c-hx+hy-hz,x,z,sx,sz,y,label+'/top')
 if bottom:rect(k,c-hx-hy-hz,x,z,sx,sz,-y,label+'/bottom')
box('mesh-3231',c0+up*f(pl+f(H-pl)*f(.5)),ex,up,n,[W,f(H-pl),dp],'locker carcass')
box('mesh-1088',c0+up*pl*f(.5)-n*f(.03),ex,up,n,[f(W-f(.02)),pl,f(dp-f(.06))],'plinth',bottom=False)
for col in range(cols):
 cc=front+ex*f(f(f(col+.5)*cw)-f(W*f(.5)))
 if col==p.get('kiosk',-1):
  rc('mesh-1088',cc+up*f(pl+f(H-pl)*f(.5))+n*f(.002),ex,up,f(cw-f(.02)),f(f(H-pl)-f(.08)),n,'kiosk blackpanel')
  rc('mesh-0754',cc+up*f(1.3)+n*f(.004),ex,up,f(cw-f(.1)),f(.42),n,'kiosk screen')
  rc('mesh-2208',cc+up*f(.95)+n*f(.004),ex,up,f(.12),f(.06),n,'kiosk cyanbutton')
  rc('mesh-0054',cc+up*f(.8)+n*f(.004),ex,up,f(.14),f(.012),n,'kiosk whitelabel');continue
 yb=pl
 for row,rh in enumerate(rows):
  dc=cc+up*f(yb+rh*f(.5));box('mesh-3231',dc+n*f(.008),ex,up,n,[f(cw-f(.012)),f(rh-f(.012)),f(.016)],f'column{col}row{row}door',False,False)
  rc('mesh-1088',dc+n*f(.0165)+ex*f(cw*f(.5)-f(.07)),ex,up,f(.035),f(.06),n,f'column{col}row{row}lock')
  rc('mesh-0054',dc+n*f(.0165)+up*f(rh*f(.5)-f(.05))-ex*f(cw*f(.5)-f(.06)),ex,up,f(.05),f(.022),n,f'column{col}row{row}numberplate');yb=f(yb+rh)
box('mesh-2132',c0+up*f(H+f(.13))+n*f(dp*f(.5)-f(.03)),ex,up,n,[W,f(.26),f(.06)],'labelband')
box('mesh-3476',c0+up*H*f(.5),ex,up,n,[W,H,dp],'locker collision')
audit=[]
for k,d in data.items():
 assert len(d['submeshes'])==1 and d['submeshes'][0]['topology']==0
 actual=d['submeshes'][0]['indices'].reshape(-1,3);vv=d['vertices'][actual];uv=d['uv'][0]['data'][actual,:2];nn=d['normals'][actual];taken=set();matches=[]
 for expected in batches[k]:
  candidates=[]
  for offset in range(3):
   vp=np.roll(vv,offset,axis=1);uu=np.roll(uv,offset,axis=1);no=np.roll(nn,offset,axis=1)
   err=np.max(np.abs(vp-expected['positions']),axis=(1,2));uerr=np.max(np.abs(uu-expected['uv']),axis=(1,2));nerr=np.max(np.abs(no-expected['normal']),axis=(1,2))
   for ix in np.flatnonzero((err<1e-5)&(uerr<1e-6)&(nerr<1e-6)):
    candidates.append((int(ix),offset,float(err[ix]),float(uerr[ix]),float(nerr[ix])))
  assert len(candidates)==1,(k,expected['primitive'],candidates)
  ix,offset,ve,ue,ne=candidates[0];assert ix not in taken;taken.add(ix);matches.append({'triangle_ordinal':ix,'cyclic_offset':offset,'primitive':expected['primitive'],'position_maximum_difference':ve,'uv_maximum_difference':ue,'normal_maximum_difference':ne})
 selection=np.array(sorted(taken),dtype=int);keep=np.array([i for i in range(len(actual))if i not in taken],dtype=int);after={**d,'submeshes':[{'topology':0,'indices':actual[keep].astype('<i4').ravel()}]};expected=write(after,O/(k+'-withheld.bin'));selectedvertices=set(actual[selection].ravel().tolist());retainedvertices=set(actual[keep].ravel().tolist())
 assert not selectedvertices&retainedvertices
 audit.append({'role':k,'full_channel_export':entries[k],'selected_triangle_ordinals':selection.tolist(),'selected_triangle_count':len(selection),'retained_triangle_count':len(keep),'selected_indices':actual[selection].tolist(),'selected_index_sequence_sha256':hb(actual[selection].astype('<i4').tobytes()),'retained_index_sequence_sha256':hb(actual[keep].astype('<i4').tobytes()),'selected_vertex_indices':sorted(selectedvertices),'selected_and_retained_share_vertices':False,'disable_renderer':len(keep)==0,'matches':matches,'expected_output':expected})
packet={'schema':'chooguard.exact-locker-placement-withholding-draft.v1','scene_sha256':meta['scene_sha256'],'source_metadata':'.planning/2026-10-09-floor2-completion-pass2/locker-full-channels/metadata.json','source_metadata_sha256':sha('.planning/2026-10-09-floor2-completion-pass2/locker-full-channels/metadata.json'),'source_spec':'content/world/observed-interiors/2f-main-enhance.json','source_spec_sha256':sha('content/world/observed-interiors/2f-main-enhance.json'),'withheld_element':bank,'source_recipe_geometry_method':'Exact existing KitLockers/KBox/KRect/Face primitives independently reconstructed with float32; every selected oriented position+normal+UV triangle uniquely matched against exact actual full-channel source. Source triangle ordinals are then frozen; no inferred spatial cut.','recipe':{'columns':cols,'row_heights':list(map(float,rows)),'kiosk_column':p['kiosk'],'width':float(W),'height':float(H),'source_native_centre_VYU':c0.tolist(),'source_front_VYU':front.tolist(),'source_text_native_VYU':(front+up*f(H+f(.13))+n*f(.015)).tolist()},'meshes':audit,'new_placement':None,'site_dimensions_verified':False,'guide32_exact_native_owner_assigned':False,'policy':'Withhold only this unsupported inferred bank placement, preserving every original vertex attribute and all remaining index order. Retain the exact source element as evidence of prior inference; no new bank placement.'}
(O/'locker-withholding-packet.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2)+'\n')
print('MATCH PASS',[(x['role'],x['selected_triangle_count'],x['retained_triangle_count'],max(m['position_maximum_difference']for m in x['matches']))for x in audit]);print('recipe',packet['recipe'])
