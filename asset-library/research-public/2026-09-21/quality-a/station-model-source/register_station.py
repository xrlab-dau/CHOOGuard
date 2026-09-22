import pathlib,json,math,numpy as np
from scipy.optimize import least_squares
D=pathlib.Path.cwd()/'asset-library/research-public/2026-09-21/quality-a/station-model-source'
j=json.loads((D/'station-material-and-facade-audit.json').read_text());a=[p for p in j['glassVerticalFaces'] if p['material']=='유리#001' and p['normal'][0]<-.80 and -.10<p['normal'][1]<.60 and 20<p['center'][2]<23 and p['area']>.5]
# Outermost west glass panels in 2m north bins; reject parallel internal/return glazing.
bins={}
for p in a:
 key=math.floor(p['center'][1]/2)
 if key not in bins or p['center'][0]<bins[key]['center'][0]:bins[key]=p
source=np.array([bins[k]['center'][:2] for k in sorted(bins)])
site=json.loads(pathlib.Path('asset-library/space-references/busan-reconstruction/busan-site-selected.json').read_text());f=next(x for x in site['features'] if x['id']=='165346389');target=np.array([[p['x'],p['z']] for p in f['points'][4:20]])
def transform(params,p):
 angle,tx,ty=params;c,s=np.cos(angle),np.sin(angle);return p@np.array([[c,s],[-s,c]])+np.array([tx,ty])
def nearest(points):
 starts=target[:-1];edges=target[1:]-starts;v=points[:,None,:]-starts[None,:,:];t=np.clip(np.sum(v*edges[None,:,:],axis=2)/np.sum(edges*edges,axis=1),0,1);proj=starts[None,:,:]+t[:,:,None]*edges[None,:,:];dist=np.sum((proj-points[:,None,:])**2,axis=2);idx=dist.argmin(axis=1);return proj[np.arange(len(points)),idx],idx,t[np.arange(len(points)),idx]
def residual(params):
 q=transform(params,source);closest,_,_=nearest(q);return (q-closest).ravel()
fit=least_squares(residual,[0,-41,-18],bounds=([-math.radians(10),-70,-45],[math.radians(10),-15,10]),loss='soft_l1',f_scale=.5,xtol=1e-12,ftol=1e-12,gtol=1e-12)
q=transform(fit.x,source);closest,idx,along=nearest(q);err=np.linalg.norm(q-closest,axis=1)
# Independent rail-axis heading comparison, source component rotation vs longest OSM platform edge.
structure=json.loads((D/'station-sdk-structure.json').read_text());rail=next(i for i in structure['rootInstances'] if i['definition']=='철도_선로#001');sv=np.array(rail['transform'])[:2,0];sangle=math.atan2(sv[1],sv[0]);platform=next(x for x in site['features'] if x['kind']=='platform');poly=np.array([[p['x'],p['z']] for p in platform['points']]);edges=np.roll(poly,-1,axis=0)-poly;ei=np.argmax(np.linalg.norm(edges,axis=1));tv=edges[ei];tangle=math.atan2(tv[1],tv[0]);delta=(sangle+fit.x[0]-tangle+math.pi/2)%math.pi-math.pi/2
controls=[{'sourceGlassFaceCenterXYMetres':list(source[k]),'transformedXYMetres':list(q[k]),'targetClosestOnOsmSegmentXYMetres':list(closest[k]),'osmSegmentPointIndices':[int(idx[k]+4),int(idx[k]+5)],'segmentFraction':float(along[k]),'residualMetres':float(err[k])} for k in range(len(source))]
report={'status':'RIGID_FACADE_TO_OSM_CANDIDATE_NOT_SURVEY_REGISTRATION','source':'SKP main station layer, 유리#001 west-facing glass triangle centers at20-23m elevation; outermost west sample per2m north bin','targetSource':'asset-library/space-references/busan-reconstruction/busan-site-selected.json','osmWay':165346389,'targetPointIndices':list(range(4,20)),'targetPointsXYMetres':target.tolist(),'units':'metres; sourceBlenderXY -> stationEastNorth; sourceZ remains up','scale':1,'yawDegreesSourceXYCCW':float(np.degrees(fit.x[0])),'translationSourceXYMetres':[float(fit.x[1]),float(fit.x[2])],'verticalTranslationMetres':0,'verticalPolicy':'Original ground source approximately-0.9..0.11m; no grounded terrain datum inferred in this registration. Root must check native source ground/terrain and existing floor anchors.','fit':{'method':'robust point-to-polyline least squares, soft_l1 .5m; onlyrigid yaw/translation','samples':len(source),'rmsMetres':float(np.sqrt(np.mean(err**2))),'p95Metres':float(np.percentile(err,95)),'maximumMetres':float(err.max())},'controls':controls,'independentHeadingCheck':{'sourceComponent':'철도_선로#001','sourceDirection':sv.tolist(),'targetPlatformId':platform['id'],'targetEdgePointIndices':[int(ei),int((ei+1)%len(poly))],'targetDirection':tv.tolist(),'headingDifferenceAfterFitDegrees':float(np.degrees(delta))},'uncertainty':['OSM footprint is community outline, not survey; source is authored visual model','Glass upper panels vs footprint boundary may differ by facade setback; minimum-distance fit is not semantic corner survey','Ground datum and source geometry/gameplayfloor compatibility unresolved','NativeFBX axis signs must verified on at leastthree imported controlpoints before applying yaw']}
(D/'station-rigid-registration.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
import matplotlib;matplotlib.use('Agg');import matplotlib.pyplot as plt
fig,ax=plt.subplots(figsize=(8,8));ax.plot(target[:,0],target[:,1],'k.-',label='OSM way165346389 front4..19');ax.plot(q[:,0],q[:,1],'.',color='#1674bd',label='Registered source glass samples')
for k in [0,len(q)//2,len(q)-1]:
 ax.plot([q[k,0],closest[k,0]],[q[k,1],closest[k,1]],'r-');ax.annotate('C'+str(k),(q[k,0],q[k,1]),xytext=(10,0),textcoords='offset points')
ax.set_aspect('equal');ax.set_xlabel('Station east (m)');ax.set_ylabel('Station north (m)');ax.grid(alpha=.3);ax.legend();ax.set_title('Rigid source facade / OSM alignment candidate\nRMS %.3fm | scale1 | yaw %.3fdeg'%(report['fit']['rmsMetres'],report['yawDegreesSourceXYCCW']));fig.savefig(D/'station-registration-top.png',dpi=160,bbox_inches='tight');print(json.dumps({k:report[k] for k in ['yawDegreesSourceXYCCW','translationSourceXYMetres','fit','independentHeadingCheck']}))
