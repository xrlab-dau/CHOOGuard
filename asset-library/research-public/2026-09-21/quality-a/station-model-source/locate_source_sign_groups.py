import ctypes as C,pathlib,json,numpy as np,itertools
D=pathlib.Path.cwd()/'asset-library/research-public/2026-09-21/quality-a/station-model-source'
class Ref(C.Structure):_fields_=[('ptr',C.c_void_p)]
class Point(C.Structure):_fields_=[('x',C.c_double),('y',C.c_double),('z',C.c_double)]
class Box(C.Structure):_fields_=[('min',Point),('max',Point)]
class Transform(C.Structure):_fields_=[('values',C.c_double*16)]
lib=C.CDLL(str(D/'importer-isolated/sketchup_importer/SketchUpAPI.framework/Versions/A/SketchUpAPI'))
def signature(name,args):f=getattr(lib,name);f.argtypes=args;f.restype=C.c_int;return f
create=signature('SUModelCreateFromFile',[C.POINTER(Ref),C.c_char_p]);entities=signature('SUModelGetEntities',[Ref,C.POINTER(Ref)]);bbox=signature('SUEntitiesGetBoundingBox',[Ref,C.POINTER(Box)]);gentities=signature('SUGroupGetEntities',[Ref,C.POINTER(Ref)]);dentities=signature('SUComponentDefinitionGetEntities',[Ref,C.POINTER(Ref)]);definition=signature('SUComponentInstanceGetDefinition',[Ref,C.POINTER(Ref)]);gt=signature('SUGroupGetTransform',[Ref,C.POINTER(Transform)]);it=signature('SUComponentInstanceGetTransform',[Ref,C.POINTER(Transform)])
for kind in ['Groups','Instances']:
 signature('SUEntitiesGetNum'+kind,[Ref,C.POINTER(C.c_size_t)]);signature('SUEntitiesGet'+kind,[Ref,C.c_size_t,C.POINTER(Ref),C.POINTER(C.c_size_t)])
lib.SUInitialize();m=Ref();assert create(C.byref(m),str(D/'station-extracted/부산역(수정).skp').encode())==0;e=Ref();entities(m,C.byref(e));points=np.array([r['point'] for r in json.loads((D/'sign-raycast-points.json').read_text(encoding='utf-8')) if r['hit']]);rows=[]
def walk(e,M,path,depth=0):
 b=Box();status=bbox(e,C.byref(b))
 if status!=0:return
 corners=np.array([[x,y,z,1] for x,y,z in itertools.product([b.min.x,b.max.x],[b.min.y,b.max.y],[b.min.z,b.max.z])]);v=(corners@M.T)[:,:3]*.0254;lo=v.min(0);hi=v.max(0);hits=np.where(np.all((points>=lo-.02)&(points<=hi+.02),axis=1))[0]
 if not len(hits):return
 rows.append({'path':path,'depth':depth,'min':lo.tolist(),'max':hi.tolist(),'size':(hi-lo).tolist(),'rayHits':hits.tolist()})
 if depth>18:return
 for kind in ['Groups','Instances']:
  n=C.c_size_t();getattr(lib,'SUEntitiesGetNum'+kind)(e,C.byref(n));refs=(Ref*n.value)();got=C.c_size_t();getattr(lib,'SUEntitiesGet'+kind)(e,n,refs,C.byref(got))
  for i,r in enumerate(refs):
   child=Ref();t=Transform()
   if kind=='Groups':gentities(r,C.byref(child));gt(r,C.byref(t));key='/g'+str(i)
   else:
    d=Ref();definition(r,C.byref(d));dentities(d,C.byref(child));it(r,C.byref(t));key='/i'+str(i)
   A=np.array(t.values).reshape(4,4).T;walk(child,M@A,path+key,depth+1)
walk(e,np.eye(4),'ROOT');(D/'source-sign-group-candidates.json').write_text(json.dumps(rows,indent=2));print(json.dumps(rows[-35:],indent=2))
