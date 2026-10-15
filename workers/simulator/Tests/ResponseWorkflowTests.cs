using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ChooGuard.Seeded.Tests
{
    public static class ResponseWorkflowTests
    {
        static void Assert(bool condition,string message) { if(!condition)throw new Exception("Response workflow test: "+message); }
        static Entity E(string id,double x,params string[] tags)
        { var e=new Entity {Id=id,Kind=id,X=x};foreach(string tag in tags)e.Tags.Add(tag);return e; }
        sealed class Rig
        {
            public World W;
            public ResponseWorkflows Engine;
            readonly Dictionary<string,long> sequence=new Dictionary<string,long>(StringComparer.Ordinal);
            public Rig(ulong seed=7,bool reverse=false,ResponseProfile profile=null)
            {
                W=new World {Seed=seed,StepSeconds=.5};W.Fields["environment.autonomous"]=0;
                var items=new[] { E("passenger",0,"npc","radio"),E("staff",.5,"npc","staff","radio"),E("control",1,"npc","radio"),E("tech",1.2,"npc","radio"),E("fire",1.3,"npc","radio"),E("medical",1.4,"npc","radio"),E("patient",2,"npc"),E("cabinet",1.5,"electrical"),E("exit",10,"exit","safe-zone"),E("barrier",1.5,"barrier"),E("radio",.5,"radio","communication-equipment"),E("pa",.5,"public-address","communication-equipment"),E("remote",100,"npc") };
                foreach(var e in reverse?items.Reverse():items)W.Add(e);
                W.Entities["cabinet"].Set("fault",.4);W.Entities["cabinet"].Set("power",1);W.Entities["cabinet"].Set("mech.profile.breaker-cabinet",1);W.Entities["cabinet"].Set("mech.part.switch",1);
                W.Entities["patient"].Set("medical.condition",.4);W.Entities["patient"].Set("health",.6);W.Entities["patient"].Set("response.assistance-consent",1);W.Entities["barrier"].Set("response.deployed",1);
                profile=profile??new ResponseProfile();foreach(var p in profile.Channels)p.LossProbability=0;
                Engine=new ResponseWorkflows(W,JsonConvert.SerializeObject(profile));
                Engine.SetRole("passenger",ResponseRole.Passenger);Engine.SetRole("staff",ResponseRole.StationStaff);Engine.SetRole("control",ResponseRole.Control);Engine.SetRole("tech",ResponseRole.FacilityTechnician);Engine.SetRole("fire",ResponseRole.FireResponder);Engine.SetRole("medical",ResponseRole.MedicalResponder);Engine.SetRole("patient",ResponseRole.Passenger);Engine.SetRole("remote",ResponseRole.Passenger);
                Engine.RegisterChannel("radio","radio");Engine.RegisterChannel("pa","pa");
            }
            public ResponseOutcome Do(string actor,string action,string subject="cabinet",string observation=null,string message=null,string recipient=null,string channel="face-to-face",string request=null,string executor=null,double duration=0,string route=null,string patient=null,string resource=null,string readback=null)
            {
                long s;sequence.TryGetValue(actor,out s);sequence[actor]=s+1;
                return Engine.Apply(new ResponseInput {Tick=W.Tick,Sequence=s+1,ActorId=actor,Action=action,SubjectId=subject,ObservationId=observation,MessageId=message,RecipientId=recipient,ChannelId=channel,RequestKind=request,ExecutorId=executor,Duration=duration,RouteId=route,PatientId=patient,ResourceId=resource,Readback=readback});
            }
            public string Observe(string actor="staff",string subject="cabinet")
            { var r=Do(actor,"observe",subject);Assert(r.Accepted&&r.Complete,"fixture observation: "+r.Reason);return r.RecordId; }
            public void Advance(int ticks=1) { for(int i=0;i<ticks;i++){W.Tick++;Engine.Step();} }
            public void Deliver(string message)
            { var m=Engine.Inspect(message);long due=(long)m.State["due.tick"];while(W.Tick<due)Advance();Engine.Step();Assert(Engine.Inspect(message).State["delivered"]==1,"fixture message delivery"); }
            public void Confirm(string actor,string message)
            { Assert(Do(actor,"acknowledge",message:message).Accepted,"fixture ACK");Assert(Do(actor,"readback",message:message,readback:Engine.Inspect(message).ReadbackCode).Accepted,"fixture readback"); }
            public string Authorization(string actor,string observation,string kind,string executor,string subject="cabinet")
            {
                var request=Do(actor,"request",subject,observation,recipient:"control",request:kind);Assert(request.Accepted,"fixture request: "+request.Reason);Deliver(request.RecordId);Confirm("control",request.RecordId);
                var decision=Do("control","authorize",message:request.RecordId,executor:executor);Assert(decision.Accepted,"fixture decision: "+decision.Reason);Deliver(decision.RecordId);Confirm(executor,decision.RecordId);return request.RecordId;
            }
            public ResponseOutcome Timed(string actor,string action,string subject,string observation,int ticks,string message=null,string route=null,string patient=null,string resource=null)
            {
                ResponseOutcome last=null;for(int i=0;i<ticks;i++)
                {
                    if(route!=null)
                    {
                        var p=W.Entities[patient??actor];p.Set("response.route."+route+".complete",1);p.Set("response.route."+route+".verified.tick",W.Tick);
                    }
                    last=Do(actor,action,subject,observation,message:message,duration:.5,route:route,patient:patient,resource:resource);Assert(last.Accepted,"fixture timed "+action+": "+last.Reason);Advance();
                }
                return last;
            }
        }
        static void RefusedAtomic(Rig rig,Func<ResponseOutcome> command,string expected)
        {
            string digest=rig.W.Digest();int journal=rig.W.Journal.Count;var result=command();
            Assert(!result.Accepted&&result.Reason==expected,"refusal "+expected+", got "+result.Reason);Assert(rig.W.Digest()==digest&&rig.W.Journal.Count==journal,"refusal preserves full world and journal: "+expected);
        }
        public static void Run()
        {
            LocalEvidenceAndRoles(); DeliveryReadbackAndRequest(); AuthorizationAndMechanicalVerification(); ChannelFailures();
            PartialDurationsAndStaleKnowledge(); ActualEvacuationAndAccessibleTransport(); PersonSupportAndHandover(); DeterministicReplayAndCatalog(); LegacyInstantActionGuard(); SourceActivityAndCarriedRadio();
        }
        static void LocalEvidenceAndRoles()
        {
            var r=new Rig();Assert(r.Engine.Tasks("staff").Count==0,"no global hazard oracle without knowledge");
            RefusedAtomic(r,()=>r.Do("remote","observe"),"subject_outside_observed_space");
            var observation=r.Observe();var info=r.Engine.Inspect(observation);
            Assert(info.Claim==ResponseWorkflows.Claim&&info.State["hazard.fault"]==.4,"actual observed hazard and inferred provenance");
            Assert(info.Links.Contains("cabinet")&&info.Links.Contains("patient")&&!info.Links.Contains("remote"),"source/object/visible-person identities retained without remote counts");
            Assert(r.Engine.Tasks("staff").Any(t=>t.Action=="request:isolation")&&r.Engine.Tasks("control").Count==0,"tasks arise from local evidence and bounded roles");
            string digest=r.W.Digest();bool bad=false;try{r.Engine.SetRole("cabinet",ResponseRole.Control);}catch(ArgumentException){bad=true;}Assert(bad&&digest==r.W.Digest(),"no arbitrary object role registration");
            var p=r.Observe("passenger");RefusedAtomic(r,()=>r.Do("passenger","control-access",observation:p,duration:.5,resource:"barrier"),"staff_or_responder_role_required");
            var record=r.Engine.Inspect(observation);record.State["hazard.fault"]=99;record.Links[0]="altered";
            Assert(r.Engine.Inspect(observation).State["hazard.fault"]==.4&&!r.Engine.Inspect(observation).Links.Contains("altered"),"inspection cannot alter authoritative evidence");
            r.W.Entities["cabinet"].Set("mech.center.x",80);RefusedAtomic(r,()=>r.Do("staff","observe"),"subject_outside_observed_space");r.W.Entities["cabinet"].Set("mech.center.x",1.5);
            r.W.Fields["regional-alert"]=1;var other=r.Observe();Assert(!r.Engine.Inspect(other).State.ContainsKey("hazard.public-alert"),"unreceived regional alert is not omniscient knowledge");
        }
        static void DeliveryReadbackAndRequest()
        {
            var r=new Rig();string obs=r.Observe();double fault=r.W.Entities["cabinet"].Get("fault");
            var report=r.Do("staff","report",observation:obs,recipient:"control");Assert(report.Accepted&&!report.Complete,"send is not delivery");
            Assert(r.Engine.Tasks("control").Count==0,"recipient has no knowledge before actual delivery");
            RefusedAtomic(r,()=>r.Do("control","acknowledge",message:report.RecordId),"message_not_delivered");r.Deliver(report.RecordId);
            Assert(r.Engine.Tasks("control").Any(t=>t.Action=="acknowledge"),"delivered report creates receipt task");
            RefusedAtomic(r,()=>r.Do("control","readback",message:report.RecordId,readback:r.Engine.Inspect(report.RecordId).ReadbackCode),"acknowledge_first");
            Assert(r.Do("control","acknowledge",message:report.RecordId).Accepted,"ACK accepted after delivery");
            RefusedAtomic(r,()=>r.Do("control","readback",message:report.RecordId,readback:"wrong"),"readback_content_mismatch");
            Assert(r.Do("control","readback",message:report.RecordId,readback:r.Engine.Inspect(report.RecordId).ReadbackCode).Accepted,"exact observed report readback");
            RefusedAtomic(r,()=>r.Do("control","authorize",message:report.RecordId,executor:"tech"),"request_executor_role_mismatch");
            Assert(r.W.Entities["cabinet"].Get("fault")==fault&&r.W.Entities["cabinet"].Get("power")==1,"report/ACK never execute equipment actions");
        }
        static void AuthorizationAndMechanicalVerification()
        {
            var r=new Rig();string obs=r.Observe();var request=r.Do("staff","request",observation:obs,recipient:"control",request:"isolation");Assert(request.Accepted,"isolation request");
            RefusedAtomic(r,()=>r.Do("tech","verify-isolation",observation:obs,message:request.RecordId,duration:.5),"missing_stale_or_unknown_observation");
            r.Deliver(request.RecordId);r.Confirm("control",request.RecordId);
            RefusedAtomic(r,()=>r.Do("control","authorize",message:request.RecordId,executor:"passenger"),"request_executor_role_mismatch");
            var instruction=r.Do("control","authorize",message:request.RecordId,executor:"tech");Assert(instruction.Accepted&&!instruction.Complete,"authorization is separate undelivered instruction");
            var own=r.Observe("tech");RefusedAtomic(r,()=>r.Do("tech","verify-isolation",observation:own,message:request.RecordId,duration:.5),"isolation_authorization_required");
            r.Deliver(instruction.RecordId);RefusedAtomic(r,()=>r.Do("tech","verify-isolation",observation:obs,message:request.RecordId,duration:.5),"isolation_authorization_required");r.Confirm("tech",instruction.RecordId);
            RefusedAtomic(r,()=>r.Do("tech","verify-isolation",observation:obs,message:request.RecordId,duration:.5),"physical_isolation_incomplete");
            var cabinet=r.W.Entities["cabinet"];cabinet.Set("mech.part.switch",0);cabinet.Set("power.isolated",1);
            RefusedAtomic(r,()=>r.Do("tech","verify-isolation",observation:obs,message:request.RecordId,duration:.5),"check_and_lockout_tag_required");
            cabinet.Set("mech.checked",1);cabinet.Set("mech.tagged",1);double fault=cabinet.Get("fault");
            var completed=r.Timed("tech","verify-isolation","cabinet",obs,4,message:instruction.RecordId);
            Assert(completed.Complete&&completed.Progress==1&&cabinet.Get("fault")==fault,"verification completes through actual mechanical evidence and never repairs fault");
        }
        static void ChannelFailures()
        {
            var r=new Rig();string obs=r.Observe();var radio=r.W.Entities["radio"];
            radio.Set("power",0);RefusedAtomic(r,()=>r.Do("staff","report",observation:obs,recipient:"control",channel:"radio"),"channel_unpowered");radio.Set("power",1);
            r.W.Entities["control"].Set("response.radio.coverage",0);RefusedAtomic(r,()=>r.Do("staff","report",observation:obs,recipient:"control",channel:"radio"),"radio_coverage_unavailable");r.W.Entities["control"].Set("response.radio.coverage",1);
            radio.Set("mech.profile.radio",1);RefusedAtomic(r,()=>r.Do("staff","report",observation:obs,recipient:"control",channel:"radio"),"hold_radio_ptt_first");radio.Set("radio.transmitting",1);
            var sent=r.Do("staff","report",observation:obs,recipient:"control",channel:"radio");Assert(sent.Accepted,"powered held PTT sends");r.W.Fields["communications-disruption"]=1;
            long due=(long)r.Engine.Inspect(sent.RecordId).State["due.tick"];while(r.W.Tick<due)r.Advance();Assert(r.Engine.Inspect(sent.RecordId).State["lost"]==1,"actual outage at delivery loses radio report");
            RefusedAtomic(r,()=>r.Do("control","acknowledge",message:sent.RecordId),"message_not_delivered");
            var face=r.Do("staff","report",observation:obs,recipient:"control");r.Deliver(face.RecordId);Assert(r.Engine.Inspect(face.RecordId).State["delivered"]==1,"local face-to-face does not require regional network");
            string po=r.Observe("passenger");RefusedAtomic(r,()=>r.Do("passenger","report",observation:po,recipient:"control",channel:"pa"),"pa_control_role_required");
            var named=E("radio_alarm_name_only",0);r.W.Add(named);string before=r.W.Digest();bool invalid=false;try{r.Engine.RegisterChannel(named.Id,"radio");}catch(ArgumentException){invalid=true;}Assert(invalid&&before==r.W.Digest(),"names do not invent equipment capabilities");
        }
        static void PartialDurationsAndStaleKnowledge()
        {
            var r=new Rig();string obs=r.Observe();var partial=r.Do("staff","protect",observation:obs,duration:.5);Assert(partial.Accepted&&!partial.Complete&&partial.Progress==.25,"partial warning effort retained");
            RefusedAtomic(r,()=>r.Do("staff","protect",observation:obs,duration:.5),"actor_tick_time_budget_exceeded");r.Advance();
            var finish=r.Timed("staff","protect","cabinet",obs,3);Assert(finish.Complete&&r.W.Entities["cabinet"].Get("response.warning.staff")==1,"warning needs sustained elapsed interaction");
            RefusedAtomic(r,()=>r.Do("staff","protect",observation:obs,duration:.5),"already_complete");
            string fresh=r.Observe();r.Advance(41);RefusedAtomic(r,()=>r.Do("staff","report",observation:fresh,recipient:"control"),"missing_stale_or_unknown_observation");
            Assert(r.Engine.Tasks("staff").Count==0,"expired observations cannot generate current task claims");
            string renewed=r.Observe();var invalid=new ResponseInput {ActorId="staff",Action="protect",SubjectId="cabinet",ObservationId=renewed,Tick=r.W.Tick,Sequence=1000,Duration=double.NaN};
            RefusedAtomic(r,()=>r.Engine.Apply(invalid),"invalid_input_envelope");
            var unknown=new ResponseInput {ActorId="staff",Action="magic-rescue",SubjectId="cabinet",ObservationId=renewed,Tick=r.W.Tick,Sequence=1000};RefusedAtomic(r,()=>r.Engine.Apply(unknown),"unknown_response_action");
        }
        static void ActualEvacuationAndAccessibleTransport()
        {
            var r=new Rig();string obs=r.Observe();string authorization=r.Authorization("staff",obs,"evacuation","staff");
            RefusedAtomic(r,()=>r.Do("staff","guide-evacuation",observation:obs,message:authorization,duration:.5,route:"exit",patient:"patient"),"fresh_complete_navigation_path_required");
            var escort=r.Timed("staff","guide-evacuation","cabinet",obs,8,message:authorization,route:"exit",patient:"patient");
            Assert(escort.Accepted&&!escort.Complete&&r.W.Entities["patient"].X==2,"assigning escort neither teleports nor completes evacuation");
            r.Engine.Step();Assert(r.Engine.Inspect(escort.RecordId).State["complete"]==0,"no arrival claim before actual physical movement");
            r.W.Entities["patient"].X=10;r.Engine.Step();Assert(r.Engine.Inspect(escort.RecordId).State["complete"]==1,"only actual destination arrival completes evacuation");
            var b=new Rig();string bo=b.Observe();string ba=b.Authorization("staff",bo,"evacuation","staff");var task=b.Timed("staff","guide-evacuation","cabinet",bo,8,message:ba,route:"exit",patient:"patient");b.W.Entities["exit"].Set("blocked",1);b.Engine.Step();
            Assert(b.Engine.Inspect(task.RecordId).State["route-invalidated"]==1&&b.Engine.Inspect(task.RecordId).State["complete"]==0,"new blockage pauses ongoing escort for reassessment");
            Assert(b.W.Entities["patient"].Get("goal.active")==0,"blocked assigned escort releases the unsafe navigation goal");
            var a=new Rig();string ao=a.Observe();string aa=a.Authorization("staff",ao,"accessible-assistance","staff");var patient=a.W.Entities["patient"];patient.Set("mobility",0);patient.Set("response.route.exit.complete",1);patient.Set("response.route.exit.verified.tick",a.W.Tick);
            RefusedAtomic(a,()=>a.Do("staff","assist-evacuation",observation:ao,message:aa,duration:.5,route:"exit",patient:"patient"),"qualified_accessible_transport_required");
            var chair=E("chair",2,"accessible-transport");a.W.Add(chair);patient.Set("response.transport-bound.chair",1);patient.Set("response.route.exit.accessible",1);
            var pending=a.Timed("staff","assist-evacuation","cabinet",ao,8,message:aa,route:"exit",patient:"patient",resource:"chair");Assert(!pending.Complete&&patient.X==2&&patient.Get("mobility")==0&&pending.Reason=="transport_assistance_pending_actual_motion","verified transport still requires actual coupled physical motion");
            var self=new Rig();string so=self.Observe("passenger");var se=self.Timed("passenger","evacuate","cabinet",so,8,route:"exit");Assert(se.Accepted&&!se.Complete,"passenger can seek local refuge without control permission or teleport");
        }
        static void PersonSupportAndHandover()
        {
            var r=new Rig();string obs=r.Observe("staff","patient");double health=r.W.Entities["patient"].Get("health"),condition=r.W.Entities["patient"].Get("medical.condition");
            RefusedAtomic(r,()=>r.Do("staff","support-person","patient",obs,duration:.5),"person_assessment_incomplete");var assess=r.Timed("staff","assess-person","patient",obs,8);Assert(assess.Complete,"basic nonclinical assessment needs time");
            r.W.Entities["patient"].Set("response.assistance-consent",0);RefusedAtomic(r,()=>r.Do("staff","support-person","patient",obs,duration:.5),"assistance_consent_not_recorded");r.W.Entities["patient"].Set("response.assistance-consent",1);
            var support=r.Timed("staff","support-person","patient",obs,4);Assert(support.Complete&&r.W.Entities["patient"].Get("assisted")==1,"completed local support establishes an actual supporter relation");
            Assert(r.W.Entities["patient"].Get("health")==health&&r.W.Entities["patient"].Get("medical.condition")==condition,"assessment/support never heal or clear a medical condition");
            var report=r.Do("staff","report","patient",obs,recipient:"medical");r.Deliver(report.RecordId);r.Confirm("medical",report.RecordId);
            var handover=r.Timed("staff","handover","patient",obs,4,message:report.RecordId);Assert(handover.Complete&&r.Engine.Inspect(handover.RecordId).State["hazard_resolved"]==0&&r.W.Entities["patient"].Get("health")==health,"specialist handover is evidence transfer, not recovery");
            r.W.Entities["staff"].X=100;r.Engine.Step();Assert(r.W.Entities["patient"].Get("assisted")==0,"support ends when physical supporter leaves");
        }
        static string ReplayTrace(ulong seed,bool reverse,bool reconstruct)
        {
            var r=new Rig(seed,reverse);string obs=r.Observe();string request=r.Authorization("staff",obs,"isolation","tech");
            if(reconstruct)r.Engine=new ResponseWorkflows(r.W,JsonConvert.SerializeObject(new ResponseProfile {Channels=new ResponseProfile().Channels.Select(c=>{c.LossProbability=0;return c;}).ToList()}));
            r.W.Entities["cabinet"].Set("mech.part.switch",0);r.W.Entities["cabinet"].Set("power.isolated",1);r.W.Entities["cabinet"].Set("mech.checked",1);r.W.Entities["cabinet"].Set("mech.tagged",1);r.Timed("tech","verify-isolation","cabinet",obs,4,message:request);
            return r.W.Digest()+"|"+string.Join("\n",r.W.Journal.Select(t=>t.ToString()+"|"+t.Reason));
        }
        static void DeterministicReplayAndCatalog()
        {
            string a=ReplayTrace(70,false,false);Assert(a==ReplayTrace(70,true,false),"world insertion order cannot change response replay");Assert(a==ReplayTrace(70,false,true),"engine reconstruction uses world state with no hidden progress/authorization memory");
            var due=new HashSet<double>();for(ulong seed=1;seed<=12;seed++){var r=new Rig(seed);var report=r.Do("staff","report",observation:r.Observe(),recipient:"control",channel:"radio");due.Add(r.Engine.Inspect(report.RecordId).State["due.tick"]);}Assert(due.Count>2,"seed diversifies actual communication delay, not scenario selection");
            var w=new World();string before=w.Digest();bool rejected=false;var bad=new ResponseProfile {AssessSeconds=double.NaN};try{new ResponseWorkflows(w,JsonConvert.SerializeObject(bad));}catch(ArgumentException){rejected=true;}catch(JsonSerializationException){rejected=true;}Assert(rejected&&w.Digest()==before,"bad profile rejected before any world mutation");
            var fresh=new World();new ResponseWorkflows(fresh);Assert(Enumerable.Range(0,8).All(i=>fresh.Fields.ContainsKey("response.catalog.word"+i)),"all 256 catalog hash bits bind replay");
            before=fresh.Digest();bad=new ResponseProfile {AssessSeconds=5};rejected=false;try{new ResponseWorkflows(fresh,JsonConvert.SerializeObject(bad));}catch(ArgumentException){rejected=true;}Assert(rejected&&before==fresh.Digest(),"different profile cannot silently change recorded world authority");
            w=new World();before=w.Digest();rejected=false;try{new ResponseWorkflows(w,"{}");}catch(ArgumentException){rejected=true;}Assert(rejected&&before==w.Digest(),"supplied catalog must contain explicit schema/inference provenance");
            var calm=new Rig();calm.W.Entities["cabinet"].Set("fault",0);var o=calm.Observe();Assert(!calm.Engine.Tasks("staff").Any(t=>t.ObservationId==o),"quiescent observation cannot force response obligations");
        }
        static void LegacyInstantActionGuard()
        {
            var r=new Rig();var staff=r.W.Entities["staff"];staff.Set("response.workflow-bound",1);
            var catalog=OperationCatalog.CreateDefault();Assert(!catalog.Available(r.W,"staff","patient").Any(o=>o.Name=="assist"||o.Name=="warn"),"workflow-bound actor cannot bypass procedures with legacy instant assist/warn");
            var plan=ActionPlan.Capture(r.W,"test","fixture","v1",OperationCatalog.Version,"fixture-nav",new[]{new ActionStep {ActorId="staff",TargetId="patient",Operation="assist",Amount=1}});string before=r.W.Digest();string reason;
            Assert(!new PlanExecutor(catalog).TryExecute(r.W,plan,0,"fixture-nav",out reason)&&reason=="affordance_precondition_failed"&&r.W.Digest()==before,"cached legacy assist cannot bypass role/progress/evidence checks or heal");
            var device=r.W.Entities["cabinet"];device.Set("mech.profile",0);
            Assert(!catalog.Available(r.W,"staff",device.Id).Any(o=>o.Name=="isolate_power"),"inferred electrical device without a mechanical isolation contract cannot use instant legacy isolation");
            var isolation=ActionPlan.Capture(r.W,"test-isolation","fixture","v1",OperationCatalog.Version,"fixture-nav",new[]{new ActionStep {ActorId="staff",TargetId=device.Id,Operation="isolate_power",Amount=1}});before=r.W.Digest();
            Assert(!new PlanExecutor(catalog).TryExecute(r.W,isolation,0,"fixture-nav",out reason)&&device.Get("power")==1&&r.W.Digest()==before,"cached isolation cannot bypass technician authorization and part/check/tag sequence");
        }
        static void SourceActivityAndCarriedRadio()
        {
            var r=new Rig();var source=E("source:inactive",.6,"source-input","render-only");source.Kind="source-object";source.Set("source.active-hierarchy",0);source.Set("response.bounds-known",1);r.W.Add(source);
            RefusedAtomic(r,()=>r.Do("staff","observe",source.Id),"subject_outside_observed_space");
            source.Set("source.active-hierarchy",1);source.Set("response.bounds-known",0);
            RefusedAtomic(r,()=>r.Do("staff","observe",source.Id),"subject_outside_observed_space");
            source.Set("response.bounds-known",1);Assert(r.Do("staff","observe",source.Id).Accepted,"actual active source with native bounds can be observed");
            var staff=r.W.Entities["staff"];var cabinet=r.W.Entities["cabinet"];staff.X=10;cabinet.X=11;var observation=r.Observe();
            RefusedAtomic(r,()=>r.Do("staff","report",observation:observation,recipient:"control",channel:"radio"),"channel_control_out_of_reach");
            staff.Set("mech.carry.radio",1);r.W.Entities["radio"].Set("radio.transmitting",1);
            Assert(r.Do("staff","report",observation:observation,recipient:"control",channel:"radio").Accepted,"mechanically carried radio remains usable after walking from pickup position");
        }
    }
}
