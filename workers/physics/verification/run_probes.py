"""Bounded source-inspired numerical verification; no empirical validation claim."""
import json, math, hashlib, platform, time
from pathlib import Path
import jupedsim as j
from shapely.geometry import Polygon, Point, LineString
ROOT=Path(__file__).resolve().parent
SPEC={"source":"NIST TN 1822 (2013), Verif.2.1, Verif.2.3, flow constraints 3.1.5", "classification":"adapted component verification, not full NIST suite or real-site validation", "dt_s":0.05,"speed_m_s":1.0,"radius_m":0.2,"time_gap_s":1.0,"prespecified_checks":{"free_walk":"40m displacement in 40s at 0 and 45deg; absolute distance error <= 0.05m (one integration step), transverse error <= 1e-6m","corner":"20 agents, no center or step-segment outside geometry (1e-9m geometry arithmetic tolerance), all exit within 120s execution cap","bottleneck":"same 40 positions at widths 1m and 2m: conserve identities, contain paths, complete within 120s cap; narrow completion >= wide minus one dt; qualitative width consistency only, no universal flow threshold"},"adaptations":"Corridor extends beyond 40m measurement to avoid exit removal. Corner is a 2m-wide L; bottleneck is a flat 8x5m room plus 4m outlet (not NIST staircase)."}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def rotate(p,a):return(p[0]*math.cos(a)-p[1]*math.sin(a),p[0]*math.sin(a)+p[1]*math.cos(a))
def setup(poly,exit,pts):
 s=j.Simulation(model=j.CollisionFreeSpeedModelV3(),geometry=poly,dt=.05); e=s.add_exit_stage(exit); q=s.add_journey(j.JourneyDescription([e]))
 for p in pts:s.add_agent(j.CollisionFreeSpeedModelV3AgentParameters(position=p,journey_id=q,stage_id=e,desired_speed=1,radius=.2,time_gap=1))
 return s

def main():
 ROOT.mkdir(exist_ok=True);(ROOT/'prespecified.json').write_text(json.dumps(SPEC,indent=2)+'\n')
 start=time.time();results=[];trace=ROOT/'trajectories.jsonl'
 with trace.open('w') as out:
  for deg in [0,45]:
   a=math.radians(deg);tr=lambda ps:[rotate(p,a) for p in ps]
   s=setup(tr([(-1,-1),(43,-1),(43,1),(-1,1)]),tr([(41,-.8),(42,-.8),(42,.8),(41,.8)]),[(0,0)])
   for k in range(800):
    s.iterate();p=tuple(next(s.agents()).position);out.write(json.dumps([f'free-{deg}',k+1,p])+'\n')
   longitudinal=p[0]*math.cos(a)+p[1]*math.sin(a);lateral=-p[0]*math.sin(a)+p[1]*math.cos(a)
   results.append(dict(case=f'free-{deg}',displacement_m=longitudinal,transverse_m=lateral,passed=abs(longitudinal-40)<=.05 and abs(lateral)<=1e-6))
  cases=[('corner',[(0,0),(10,0),(10,10),(8,10),(8,2),(0,2)],[(8.1,9),(9.9,9),(9.9,9.8),(8.1,9.8)],[(.5+i*.7,.35+y*.43) for i in range(5) for y in range(4)])]
  for w in [1.,2.]:
   lo=(5-w)/2;hi=(5+w)/2
   cases.append((f'bottleneck-{w}',[(0,0),(8,0),(8,lo),(12,lo),(12,hi),(8,hi),(8,5),(0,5)],[(11,lo+.05),(11.8,lo+.05),(11.8,hi-.05),(11,hi-.05)],[(.5+x*.8,.5+y*.8) for x in range(8) for y in range(5)]))
  for name,poly,ex,pts in cases:
   s=setup(poly,ex,pts);geom=Polygon(poly).buffer(1e-9);initial={a.id for a in s.agents()};removed=set();previous={a.id:tuple(a.position) for a in s.agents()};violations=0;conserved=True;exits=[];removal_api_overlap_steps=0
   for k in range(2400):
    s.iterate();current={a.id:tuple(a.position) for a in s.agents()};api_removed=set(s.removed_agents());removal_api_overlap_steps+=bool(api_removed & set(current));new=set(previous)-set(current);removed.update(new)
    exits.extend([s.elapsed_time()]*len(new));conserved &= not(set(current)&removed) and set(current)|removed==initial
    for aid,p in current.items():
     violations+=int(not geom.covers(Point(p)) or not geom.covers(LineString([previous.get(aid,p),p])))
    out.write(json.dumps([name,k+1,current,sorted(new)])+'\n');previous=current
    if not current:break
   results.append(dict(case=name,initial=len(initial),removed=len(removed),remaining=s.agent_count(),elapsed_s=s.elapsed_time(),first_exit_s=min(exits,default=None),boundary_violations=violations,removal_api_overlap_steps=removal_api_overlap_steps,conserved=conserved,passed=conserved and violations==0 and s.agent_count()==0))
 narrow,wide=results[-2:];results.append(dict(case='width-consistency',passed=narrow['elapsed_s']>=wide['elapsed_s']-.05,narrow_s=narrow['elapsed_s'],wide_s=wide['elapsed_s']))
 report=dict(spec_sha256=sha(ROOT/'prespecified.json'),script_sha256=sha(Path(__file__)),trajectory_sha256=sha(trace),jupedsim_version=j.__version__,python=platform.python_version(),platform=platform.platform(),runtime_s=time.time()-start,results=results,not_covered=['real-site calibration/validation','contact force or pressure validation','FDS or smoke coupling','stairs and full NIST configurations','time-step convergence and stochastic ensemble'],source_digests={str(p.relative_to(ROOT)):sha(p) for p in sorted((ROOT/'sources').glob('*'))})
 (ROOT/'receipt.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
if __name__=='__main__':main()
