import ctypes as C,pathlib,json,numpy as np,itertools,math
D=pathlib.Path.cwd()/'asset-library/research-public/2026-09-21/quality-a/station-model-source'
class Ref(C.Structure):_fields_=[('ptr',C.c_void_p)]
class Point(C.Structure):_fields_=[('x',C.c_double),('y',C.c_double),('z',C.c_double)]
class Box(C.Structure):_fields_=[('min',Point),('max',Point)]
class Transform(C.Structure):_fields_=[('values',C.c_double*16)]
lib=C.CDLL(str(D/'importer-isolated/sketchup_importer/SketchUpAPI.framework/Versions/A/SketchUpAPI'))
def sig(name,args):f=getattr(lib,name);f.argtypes=args;f.restype=C.c_int;return f
create=sig('SUModelCreateFromFile',[C.POINTER(Ref),C.c_char_p]);entities=sig('SUModelGetEntities',[Ref,C.POINTER(Ref)]);bbox=sig('SUEntitiesGetBoundingBox',[Ref,C.POINTER(Box)]);ge=sig('SUGroupGetEntities',[Ref,C.POINTER(Ref)]);de=sig('SUComponentDefinitionGetEntities',[Ref,C.POINTER(Ref)]);definition=sig('SUComponentInstanceGetDefinition',[Ref,C.POINTER(Ref)]);gt=sig('SUGroupGetTransform',[Ref,C.POINTER(Transform)]);it=sig('SUComponentInstanceGetTransform',[Ref,C.POINTER(Transform)])
for kind in ['Groups','Instances']:
 sig('SUEntitiesGetNum'+kind,[Ref,C.POINTER(C.c_size_t)]);sig('SUEntitiesGet'+kind,[Ref,C.c_size_t,C.POINTER(Ref),C.POINTER(C.c_size_t)])
sc=sig('SUStringCreate',[C.POINTER(Ref)]);sn=sig('SUComponentDefinitionGetName',[Ref,C.POINTER(Ref)]);sl=sig('SUStringGetUTF8Length',[Ref,C.POINTER(C.c_size_t)]);su=sig('SUStringGetUTF8',[Ref,C.c_size_t,C.c_char_p,C.POINTER(C.c_size_t)]);sr=sig('SUStringRelease',[C.POINTER(Ref)])
def name(ref):
 s=Ref();sc(C.byref(s));sn(ref,C.byref(s));n=C.c_size_t();sl(s,C.byref(n));buf=C.create_string_buffer(n.value+1);got=C.c_size_t();su(s,n.value+1,buf,C.byref(got));result=buf.value.decode('utf-8');sr(C.byref(s));return result
reg=json.loads((D/'station-rigid-registration.json').read_text());ang=math.radians(reg['yawDegreesSourceXYCCW']);R=np.array([[math.cos(ang),-math.sin(ang)],[math.sin(ang),math.cos(ang)]]);translation=np.array(reg['translationSourceXYMetres']);lib.SUInitialize();m=Ref();assert create(C.byref(m),str(D/'station-extracted/부산역(수정).skp').encode())==0;e=Ref();entities(m,C.byref(e));rows=[];names={};visited=0

def children(e,kind):
 n=C.c_size_t();getattr(lib,'SUEntitiesGetNum'+kind)(e,C.byref(n));refs=(Ref*n.value)();got=C.c_size_t();getattr(lib,'SUEntitiesGet'+kind)(e,n,refs,C.byref(got));return refs

def classify(n):
 if '계단' in n:return 'source_named_stair'
 if '에스컬' in n:return 'source_named_escalator'
 if '엘베' in n:return 'source_named_lift'
 if n.startswith('문_') or '정문' in n or '출입' in n:return 'source_named_door_or_entrance'
 return None

def record(e,M,path,n,kind):
 b=Box();status=bbox(e,C.byref(b))
 if status:return
 corners=np.array([[x,y,z,1] for x,y,z in itertools.product([b.min.x,b.max.x],[b.min.y,b.max.y],[b.min.z,b.max.z])]);v=(corners@M.T)[:,:3]*.0254;lo=v.min(0);hi=v.max(0);center=(lo+hi)/2;xy=R@center[:2]+translation
 rows.append({'path':path,'sourceDefinitionName':n,'classification':kind,'sourceBoundsMinXYZ':lo.tolist(),'sourceBoundsMaxXYZ':hi.tolist(),'sourceCenterXYZ':center.tolist(),'registeredCenterEastUpNorth':[float(xy[0]),float(center[2]),float(xy[1])],'proofScope':'Actual source named component and transformed bounds only; not clear opening, traversability or interior adjacency proof'})
def walk(e,M,path,depth=0):
 global visited
 visited+=1
 if depth>24:raise RuntimeError('Depth guard')
 for i,g in enumerate(children(e,'Groups')):
  child=Ref();ge(g,C.byref(child));t=Transform();gt(g,C.byref(t));A=np.array(t.values).reshape(4,4).T;walk(child,M@A,path+'/g'+str(i),depth+1)
 for i,inst in enumerate(children(e,'Instances')):
  d=Ref();definition(inst,C.byref(d));child=Ref();de(d,C.byref(child));t=Transform();it(inst,C.byref(t));A=np.array(t.values).reshape(4,4).T
  if d.ptr not in names:names[d.ptr]=name(d)
  n=names[d.ptr];p=path+'/i'+str(i);kind=classify(n)
  if kind:record(child,M@A,p,n,kind)
  walk(child,M@A,p,depth+1)
# Proven mainstation root group56, not neighboring context.
g=children(e,'Groups')[56];root=Ref();ge(g,C.byref(root));t=Transform();gt(g,C.byref(t));walk(root,np.array(t.values).reshape(4,4).T,'ROOT/g56')
summary={k:sum(x['classification']==k for x in rows) for k in set(x['classification'] for x in rows)};report={'scope':'Original station SKP main root group56 only','visitedEntityContainers':visited,'counts':summary,'instances':rows,'registrationPath':str(D/'station-rigid-registration.json'),'noWalkabilityClaim':True};(D/'station-interior-source-components.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(summary)
