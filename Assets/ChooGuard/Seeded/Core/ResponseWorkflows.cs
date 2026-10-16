using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    public enum ResponseRole { Passenger = 1, StationStaff, TrainCrew, Control, FacilityTechnician, FireResponder, MedicalResponder }

    /// <summary>Local, recorded training input. Route verification belongs to the navigation adapter, not this input.</summary>
    public sealed class ResponseInput
    {
        public long Tick, Sequence;
        public string ActorId, Action, SubjectId, ObservationId, RecipientId, ChannelId, MessageId;
        public string RouteId, PatientId, ResourceId, RequestKind, ExecutorId, Readback;
        public double Duration;
    }
    public sealed class ResponseOutcome
    {
        public long Tick, Sequence;
        public string ActorId, Action, RecordId, Reason, Claim = ResponseWorkflows.Claim;
        public bool Accepted, Complete;
        public double Progress;
        public JObject Json() => (JObject)LocalTapeCodec.FromObject(this);
    }
    public sealed class ResponseTask
    {
        public string Id, ActorId, SubjectId, ObservationId, MessageId, Action, Reason, Status, Claim = ResponseWorkflows.Claim;
        public double Progress;
    }
    public sealed class ResponseRecord
    {
        public string Id, Kind, SubjectId, ActorId, ObservationId, RecipientId, ChannelId, RequestKind, ExecutorId, ReadbackCode;
        public string Claim = ResponseWorkflows.Claim;
        public string[] Links;
        public SortedDictionary<string, double> State;
    }
    public sealed class ResponseChannelProfile
    {
        public string Id;
        public double Range, AccessRange, DelayMinSeconds, DelayMaxSeconds, LossProbability;
    }
    public sealed class ResponseProfile
    {
        public string SchemaVersion = ResponseWorkflows.Version, Claim = ResponseWorkflows.Claim;
        public string Provenance = "User-authorized inferred training workflow; no confidential railway manual or field validation";
        public double Confidence = .35, ObservationFreshSeconds = 20, ObservationRange = 12, NearbyPeopleRange = 8;
        public double ActionReach = 3, RouteFreshSeconds = 1.5, ProtectSeconds = 2, AssessSeconds = 4;
        public double AccessControlSeconds = 4, IsolationVerifySeconds = 2, SupportSeconds = 2, EscortSeconds = 4, HandoverSeconds = 2;
        public int MaxRecords = 16384;
        public List<ResponseChannelProfile> Channels = new List<ResponseChannelProfile>
        {
            new ResponseChannelProfile { Id="face-to-face",Range=4,AccessRange=4,DelayMinSeconds=.5,DelayMaxSeconds=1.5,LossProbability=0 },
            new ResponseChannelProfile { Id="radio",Range=120,AccessRange=2.4,DelayMinSeconds=.5,DelayMaxSeconds=3,LossProbability=.03 },
            new ResponseChannelProfile { Id="intercom",Range=250,AccessRange=2.4,DelayMinSeconds=.5,DelayMaxSeconds=2,LossProbability=.01 },
            new ResponseChannelProfile { Id="pa",Range=50,AccessRange=2.4,DelayMinSeconds=.5,DelayMaxSeconds=2,LossProbability=.01 }
        };
    }

    /// <summary>
    /// Evidence-based response obligations, not emergency scenarios. Every mutable observation, message,
    /// acknowledgement, progress and escort result is in World and its digest. No model/network calls.
    /// Accepted actions never erase fire, repair equipment, heal people or teleport an evacuation.
    /// </summary>
    public sealed class ResponseWorkflows
    {
        public const string Version = "chooguard.response.v1";
        public const string Claim = "INFERRED_TRAINING";
        static readonly string[] Actions = { "observe", "report", "request", "acknowledge", "readback", "authorize", "protect", "assess-person", "control-access", "verify-isolation", "support-person", "evacuate", "guide-evacuation", "assist-evacuation", "handover" };
        static readonly string[] Requests = { "isolation", "evacuation", "medical", "specialist", "accessible-assistance" };
        static readonly string[] Hazards = { "fire", "smoke", "fault", "failed", "blocked", "wetness", "fallen", "medical.condition" };
        readonly World world;
        readonly ResponseProfile profile;
        readonly Dictionary<string, ResponseChannelProfile> channels;
        public World World => world;
        public string CatalogHash { get; private set; }
        public IReadOnlyList<string> RecordIds => world.Entities.Values.Where(e=>e.Has("response-record")).Select(e=>e.Id).ToArray();
        public event Action<ResponseOutcome> Changed;

        public ResponseWorkflows(World world, string profilesJson = null)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            if(profilesJson!=null)
            {
                var document=JObject.Parse(profilesJson);
                if((string)document["SchemaVersion"]!=Version||(string)document["Claim"]!=Claim||string.IsNullOrWhiteSpace((string)document["Provenance"])||!(document["Channels"] is JArray))throw new ArgumentException("Explicit response profile schema and inference provenance required");
            }
            profile = profilesJson == null ? new ResponseProfile() : JsonConvert.DeserializeObject<ResponseProfile>(profilesJson,new JsonSerializerSettings {ObjectCreationHandling=ObjectCreationHandling.Replace});
            ValidateProfile(profile);
            channels = profile.Channels.ToDictionary(x => x.Id, StringComparer.Ordinal);
            CatalogHash = Stable.Hash(JsonConvert.SerializeObject(profile));
            var words=Enumerable.Range(0,8).Select(i=>uint.Parse(CatalogHash.Substring(i*8,8),NumberStyles.HexNumber,CultureInfo.InvariantCulture)).ToArray();
            int bound=Enumerable.Range(0,8).Count(i=>world.Fields.ContainsKey("response.catalog.word"+i));
            if(bound>0&&(bound!=8||Enumerable.Range(0,8).Any(i=>world.Field("response.catalog.word"+i)!=words[i])))throw new ArgumentException("Response catalog differs from recorded world; explicit new run required");
            for (int i=0;i<8;i++) world.Fields["response.catalog.word"+i] = words[i];
        }
        public static string DefaultProfilesJson() => JsonConvert.SerializeObject(new ResponseProfile(), Formatting.Indented);
        static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        static void ValidateProfile(ResponseProfile p)
        {
            if (p == null || p.SchemaVersion != Version || p.Claim != Claim || string.IsNullOrWhiteSpace(p.Provenance) || !Finite(p.Confidence) || p.Confidence < 0 || p.Confidence > 1 || p.MaxRecords < 32 || p.MaxRecords > 100000) throw new ArgumentException("Response profile identity/provenance/record bound");
            if (new[] {p.ObservationFreshSeconds,p.ObservationRange,p.NearbyPeopleRange,p.ActionReach,p.RouteFreshSeconds,p.ProtectSeconds,p.AssessSeconds,p.AccessControlSeconds,p.IsolationVerifySeconds,p.SupportSeconds,p.EscortSeconds,p.HandoverSeconds}.Any(v=>!Finite(v)||v<=0)) throw new ArgumentException("Finite positive response parameters required");
            if (p.Channels == null || p.Channels.Count != 4 || p.Channels.Any(c=>c==null) || p.Channels.Select(c=>c.Id).Distinct().Count()!=4) throw new ArgumentException("Four unique response channel profiles required");
            foreach(var c in p.Channels)
                if (!new[]{"face-to-face","radio","intercom","pa"}.Contains(c.Id) || new[]{c.Range,c.AccessRange,c.DelayMinSeconds,c.DelayMaxSeconds,c.LossProbability}.Any(v=>!Finite(v)) || c.Range<=0 || c.AccessRange<=0 || c.DelayMinSeconds<=0 || c.DelayMaxSeconds<c.DelayMinSeconds || c.LossProbability<0 || c.LossProbability>1) throw new ArgumentException("Invalid response channel parameters");
        }
        public void InitializeDefaultRoles()
        {
            foreach(var e in world.Entities.Values.ToArray())
                if ((e.Has("npc") || e.Has("staff")) && e.Get("response.actor") == 0)
                    SetRole(e.Id, e.Has("staff") ? ResponseRole.StationStaff : ResponseRole.Passenger);
            foreach(var e in world.Entities.Values.ToArray())
            {
                if (e.Has("source-object") || e.Has("render-only")) continue;
                if (e.Kind == "public-address") RegisterChannel(e.Id,"pa");
                else if (e.Kind == "radio" || (e.Has("radio") && e.Get("response.actor")>0)) RegisterChannel(e.Id,"radio");
                else if (e.Kind == "intercom") RegisterChannel(e.Id,"intercom");
            }
        }
        public void SetRole(string actorId, ResponseRole role)
        {
            Entity actor;
            if (!Enum.IsDefined(typeof(ResponseRole),role) || string.IsNullOrEmpty(actorId) || !world.Entities.TryGetValue(actorId,out actor) || !(actor.Has("npc")||actor.Has("staff")||actor.Has("response-actor"))) throw new ArgumentException("Existing actor and explicit response role required");
            Write(actor,"response.actor",1,"role-registration",actor.Id);
            Write(actor,"response.role",(int)role,"role-registration",actor.Id);
            Write(actor,"response.inferred-training",1,"role-registration",actor.Id);
        }
        public ResponseRole? Role(string actorId)
        {
            Entity e; if (actorId==null || !world.Entities.TryGetValue(actorId,out e) || e.Get("response.actor")!=1) return null;
            double n=e.Get("response.role"); return Finite(n)&&n>=1&&n<=7&&n==(int)n&&Enum.IsDefined(typeof(ResponseRole),(int)n) ? (ResponseRole?)(int)n : null;
        }
        public void RegisterChannel(string entityId, string channelKind)
        {
            Entity channel;
            if (entityId==null || !world.Entities.TryGetValue(entityId,out channel) || !channels.ContainsKey(channelKind) || channelKind=="face-to-face") throw new ArgumentException("Explicit existing communication equipment and profile required");
            if (!(channel.Has("radio") || channel.Has("intercom") || channel.Has("public-address") || channel.Has("communication-equipment"))) throw new ArgumentException("Communication capability is not inferred from names");
            foreach(var c in channels.Keys.Where(c=>c!="face-to-face")) Write(channel,"response.channel."+c,c==channelKind?1:0,"channel-registration",channel.Id);
            Write(channel,"response.channel.inferred",1,"channel-registration",channel.Id);
        }

        static string Tag(Entity e,string key) => e?.Tags.FirstOrDefault(t=>t.StartsWith(key+":",StringComparison.Ordinal))?.Substring(key.Length+1);
        static string Id(string kind,string text) => "response:"+kind+":"+Stable.Hash(text);
        string RecordId(ResponseInput i) => Id(i.Action=="observe"?"observation":i.Action=="report"||i.Action=="request"?"message":"action",i.ActorId+"|"+i.Sequence+"|"+i.Action+"|"+i.SubjectId);
        string ProgressId(ResponseInput i,Entity subject,Entity observation) => Id("progress",i.ActorId+"|"+i.Action+"|"+subject.Id+"|"+observation.Id+"|"+i.PatientId+"|"+i.RouteId+"|"+i.ResourceId+"|"+i.MessageId);
        Entity Make(string id,string kind,Entity subject,string actorId,Entity observation=null)
        {
            var e=new Entity {Id=id,Kind="response-"+kind,ParentId=subject.Id,X=Center(subject,"x"),Y=Center(subject,"y"),Z=Center(subject,"z")};
            foreach(string t in new[]{"render-only","response-record",Claim,"actor:"+actorId})e.Tags.Add(t);
            e.Links.Add(subject.Id);e.Links.Add(actorId);
            if(observation!=null){e.Tags.Add("observation:"+observation.Id);e.Links.Add(observation.Id);}
            return e;
        }
        void Write(Entity e,string key,double value,string mechanism,string source) => CausalKernel.Change(world,e,key,value,"response",mechanism,source,Claim+"; "+profile.Provenance);
        static double Center(Entity e,string axis) => e.Get("mech.center."+axis,axis=="x"?e.X:axis=="y"?e.Y:e.Z);
        static double Distance(Entity a,Entity b)
        { double x=Center(a,"x")-Center(b,"x"),y=Center(a,"y")-Center(b,"y"),z=Center(a,"z")-Center(b,"z");return Math.Sqrt(x*x+y*y+z*z); }
        bool Visible(Entity a,Entity b,double radius) => Distance(a,b)<=radius && a.Get("response.occluded."+b.Id)<=0 && b.Get("removed")<=0
            && (b.Kind!="source-object" || b.Get("source.active-hierarchy")==1 && b.Get("response.bounds-known")==1);
        bool Fresh(Entity observation) => observation!=null && observation.Kind=="response-observation" && world.Tick>=observation.Get("observed.tick") && (world.Tick-observation.Get("observed.tick"))*world.StepSeconds<=profile.ObservationFreshSeconds;
        bool Knows(Entity actor,Entity observation) => Tag(observation,"actor")==actor.Id || world.Entities.Values.Any(m=>m.Kind=="response-message" && Tag(m,"recipient")==actor.Id && Tag(m,"observation")==observation.Id && m.Get("delivered")==1);
        bool Local(Entity actor,Entity subject) => Visible(actor,subject,profile.ActionReach);
        bool Privileged(ResponseRole role) => role!=ResponseRole.Passenger;
        bool CanExecute(ResponseRole role,string request) => request=="isolation" ? role==ResponseRole.FacilityTechnician : request=="medical" ? role==ResponseRole.MedicalResponder : request=="specialist" ? role==ResponseRole.FireResponder||role==ResponseRole.MedicalResponder||role==ResponseRole.FacilityTechnician : role==ResponseRole.StationStaff||role==ResponseRole.TrainCrew||role==ResponseRole.FireResponder||role==ResponseRole.MedicalResponder;
        bool Authorized(Entity message,string kind,string actorId)
        {
            if(message==null||message.Kind!="response-message")return false;
            var request=Tag(message,"authorization-for")==null?message:Find(Tag(message,"authorization-for"));
            var instruction=Find(Tag(request,"instruction"));
            return request!=null&&Tag(request,"request")==kind&&Tag(request,"executor")==actorId&&request.Get("authorized")==1
                &&instruction!=null&&Tag(instruction,"recipient")==actorId&&instruction.Get("delivered")==1&&instruction.Get("readback")==1;
        }
        sealed class Context { public Entity Actor,Subject,Observation,Message,Recipient,Channel,Patient,Route,Resource,Executor; public ResponseRole Role; public string ChannelKind; }
        Entity Find(string id) { Entity e;return id!=null&&world.Entities.TryGetValue(id,out e)?e:null; }
        string ChannelKind(Entity equipment,string channelId,bool defaultFace=false)
        {
            if(channelId=="face-to-face"||(defaultFace&&channelId==null))return "face-to-face";
            foreach(string kind in channels.Keys)if(equipment!=null&&equipment.Get("response.channel."+kind)==1)return kind;
            return null;
        }
        string ChannelReady(Entity actor,Entity recipient,Entity equipment,string kind,bool sending)
        {
            var cp=channels[kind];
            if (kind=="face-to-face") return Visible(actor,recipient,cp.Range)?null:"face_to_face_range_or_occlusion";
            if (equipment==null || equipment.Get("response.channel."+kind)!=1) return "unassigned_communication_channel";
            if (equipment.Get("power",1)<=0 || equipment.Get("failed")>0 || equipment.Get("mech.charge",equipment.Get("charge",1))<=0 || equipment.Get("mech.part.power",1)<.5) return "channel_unpowered";
            if (sending && !actor.Links.Contains(equipment.Id) && !(kind=="radio"&&actor.Get("mech.carry."+equipment.Id)==1) && Distance(actor,equipment)>cp.AccessRange) return "channel_control_out_of_reach";
            if (kind=="radio")
            {
                if (!(recipient.Has("radio") || recipient.Get("response.radio-equipped")==1)) return "recipient_has_no_radio";
                if (Distance(actor,recipient)>cp.Range || actor.Get("response.radio.coverage",1)<=0 || recipient.Get("response.radio.coverage",1)<=0 || equipment.Get("radio.contact",1)<=0) return "radio_coverage_unavailable";
                if (sending && equipment.Get("mech.profile.radio")==1 && equipment.Get("radio.transmitting")<=0) return "hold_radio_ptt_first";
            }
            if (kind=="intercom" && !equipment.Links.Contains(recipient.Id)) return "intercom_endpoint_unconnected";
            if (kind=="pa" && Distance(equipment,recipient)>cp.Range) return "recipient_outside_pa_range";
            return null;
        }
        string Validate(ResponseInput i,out Context c)
        {
            c=new Context();
            if (i==null || i.Tick!=world.Tick || i.Tick<0 || i.Tick>9007199254740991L || i.Sequence<1 || i.Sequence>9007199254740991L || !Finite(i.Duration) || i.Duration<0 || !Finite(world.StepSeconds) || world.StepSeconds<=0 || i.Duration>world.StepSeconds) return "invalid_input_envelope";
            if (!Actions.Contains(i.Action)) return "unknown_response_action";
            c.Actor=Find(i.ActorId); var role=Role(i.ActorId);
            if(c.Actor==null||!role.HasValue)return "unregistered_actor_or_role";c.Role=role.Value;
            if(i.Sequence<=c.Actor.Get("response.last-sequence"))return "duplicate_or_stale_sequence";
            if(i.Duration>0&&c.Actor.Get("response.time.tick",-1)==world.Tick&&c.Actor.Get("response.time.used")+i.Duration>world.StepSeconds+1e-12)return "actor_tick_time_budget_exceeded";
            if(c.Actor.Get("health",1)<=.05 || c.Actor.Get("removed")>0)return "actor_unavailable";
            c.Subject=Find(i.SubjectId);c.Observation=Find(i.ObservationId);c.Message=Find(i.MessageId);c.Recipient=Find(i.RecipientId);c.Channel=Find(i.ChannelId);c.Patient=Find(i.PatientId);c.Route=Find(i.RouteId);c.Resource=Find(i.ResourceId);c.Executor=Find(i.ExecutorId);
            if(new[]{i.ActorId,i.SubjectId,i.ObservationId,i.RecipientId,i.ChannelId,i.MessageId,i.RouteId,i.PatientId,i.ResourceId,i.ExecutorId}.Any(s=>s!=null&&s.Length>4096))return "identity_too_long";
            if(i.Action=="acknowledge"||i.Action=="readback"||i.Action=="authorize")
            {
                if(c.Message==null||c.Message.Kind!="response-message"||Tag(c.Message,"recipient")!=c.Actor.Id)return "message_recipient_mismatch";
                if(c.Message.Get("delivered")!=1)return "message_not_delivered";
                if(i.Action=="acknowledge")return c.Message.Get("acknowledged")>0?"already_acknowledged":null;
                if(c.Message.Get("acknowledged")!=1)return "acknowledge_first";
                if(i.Action=="readback")return i.Readback!=Tag(c.Message,"readback-code")?"readback_content_mismatch":c.Message.Get("readback")>0?"already_read_back":null;
                if(c.Message.Get("readback")!=1)return "readback_required_before_authorization";
                if(c.Role!=ResponseRole.Control)return "control_role_required";
                if(Tag(c.Message,"authorization-for")!=null)return "request_message_required";
                string request=Tag(c.Message,"request");var executorRole=Role(i.ExecutorId);
                if(!Requests.Contains(request)||c.Executor==null||!executorRole.HasValue||!CanExecute(executorRole.Value,request))return "request_executor_role_mismatch";
                if(c.Message.Get("authorized")>0)return "already_authorized";
                c.Observation=Find(Tag(c.Message,"observation"));c.Subject=Find(c.Message.ParentId);
                if(!Fresh(c.Observation))return "stale_observation_reassess";
                c.Recipient=c.Executor;c.ChannelKind=ChannelKind(c.Channel,i.ChannelId,true);
                if(c.ChannelKind==null)return "unassigned_communication_channel";
                return ChannelReady(c.Actor,c.Recipient,c.Channel,c.ChannelKind,true)??Capacity();
            }
            if(c.Subject==null||c.Subject.Has("response-record")||c.Subject.Get("removed")>0)return "missing_or_invalid_subject";
            if(i.Action=="observe")return !Visible(c.Actor,c.Subject,profile.ObservationRange)?"subject_outside_observed_space":world.Entities.ContainsKey(RecordId(i))?"duplicate_record":Capacity();
            if(!Fresh(c.Observation)||c.Observation.ParentId!=c.Subject.Id||!Knows(c.Actor,c.Observation))return "missing_stale_or_unknown_observation";
            if(i.Action=="report"||i.Action=="request")
            {
                if(c.Recipient==null||!Role(i.RecipientId).HasValue||c.Recipient.Id==c.Actor.Id)return "known_other_recipient_required";
                if(i.Action=="request"&&!Requests.Contains(i.RequestKind))return "unknown_request_kind";
                c.ChannelKind=ChannelKind(c.Channel,i.ChannelId);
                if(c.ChannelKind==null)return "unassigned_communication_channel";
                if(c.ChannelKind=="pa"&&!Privileged(c.Role))return "pa_control_role_required";
                string channelError=ChannelReady(c.Actor,c.Recipient,c.Channel,c.ChannelKind,true);return channelError??Capacity();
            }
            if(i.Duration<=0)return "positive_duration_required";
            if(!Local(c.Actor,c.Subject))return "action_out_of_reach_or_occluded";
            if(c.Subject.Get("fire")>.5||c.Subject.Get("smoke")>.7||c.Subject.Get("failed")>0)
                if(i.Action!="protect")return "unsafe_local_exposure_keep_clear";
            if(i.Action=="protect")return CapacityForProgress(i,c);
            if(i.Action=="assess-person"||i.Action=="support-person")
            {
                if(!c.Subject.Has("npc"))return "person_subject_required";
                if(i.Action=="support-person"&&!Assessment(c.Actor.Id,c.Subject.Id,c.Observation.Id))return "person_assessment_incomplete";
                if(i.Action=="support-person"&&c.Subject.Get("response.assistance-consent")!=1)return "assistance_consent_not_recorded";
            }
            if(i.Action=="control-access")
            {
                if(!Privileged(c.Role))return "staff_or_responder_role_required";
                if(c.Resource==null||!(c.Resource.Has("barrier")||c.Resource.Has("cordon"))||c.Resource.Get("response.deployed")!=1||Distance(c.Resource,c.Subject)>5)return "place_physical_barrier_first";
            }
            if(i.Action=="verify-isolation")
            {
                if(c.Role!=ResponseRole.FacilityTechnician||!Authorized(c.Message,"isolation",c.Actor.Id)||c.Message.ParentId!=c.Subject.Id)return "isolation_authorization_required";
                if(c.Subject.Get("mech.profile.breaker-cabinet")!=1||c.Subject.Get("mech.part.switch",1)>.05||c.Subject.Get("power.isolated")<.95)return "physical_isolation_incomplete";
                if(c.Subject.Get("mech.checked")!=1||c.Subject.Get("mech.tagged")!=1)return "check_and_lockout_tag_required";
            }
            if(i.Action=="evacuate"||i.Action=="guide-evacuation"||i.Action=="assist-evacuation")
            {
                if(i.Action=="evacuate")c.Patient=c.Actor;
                if(c.Patient==null||!c.Patient.Has("npc")||!Local(c.Actor,c.Patient))return "local_person_required";
                if(i.Action!="evacuate"&&(!CanExecute(c.Role,"evacuation")||!Authorized(c.Message,i.Action=="assist-evacuation"?"accessible-assistance":"evacuation",c.Actor.Id)||c.Message.ParentId!=c.Subject.Id))return "evacuation_authorization_required";
                if(c.Route==null||!(c.Route.Has("exit")||c.Route.Has("safe-zone")||c.Route.Has("shelter")))return "existing_safe_destination_required";
                if(c.Route.Get("blocked")>0||c.Route.Get("failed")>0||c.Route.Get("fire")>.1||c.Route.Get("smoke")>.3||c.Route.Get("reachable",1)<=0)return "destination_unsafe_or_unreachable";
                string prefix="response.route."+c.Route.Id+".";
                if(c.Patient.Get(prefix+"complete")!=1||!c.Patient.State.ContainsKey(prefix+"verified.tick")||world.Tick<c.Patient.Get(prefix+"verified.tick")||(world.Tick-c.Patient.Get(prefix+"verified.tick"))*world.StepSeconds>profile.RouteFreshSeconds)return "fresh_complete_navigation_path_required";
                if(c.Patient.Get(prefix+"risk")>.1)return "verified_path_exposure_too_high";
                if(c.Patient.Get("mobility",1)<.5)
                {
                    if(i.Action!="assist-evacuation"||c.Resource==null||!c.Resource.Has("accessible-transport")||c.Patient.Get("response.transport-bound."+c.Resource.Id)!=1||c.Patient.Get(prefix+"accessible")!=1)return "qualified_accessible_transport_required";
                }
                if(i.Action=="assist-evacuation"&&c.Patient.Get("response.assistance-consent")!=1)return "assistance_consent_not_recorded";
            }
            if(i.Action=="handover")
            {
                if(c.Message==null||c.Message.ParentId!=c.Subject.Id||Tag(c.Message,"observation")!=c.Observation.Id||Tag(c.Message,"actor")!=c.Actor.Id||c.Message.Get("delivered")!=1||c.Message.Get("readback")!=1)return "delivered_read_back_report_required";
                c.Recipient=Find(Tag(c.Message,"recipient"));var receiverRole=Role(c.Recipient?.Id);
                if(!receiverRole.HasValue||!(receiverRole==ResponseRole.FireResponder||receiverRole==ResponseRole.MedicalResponder||receiverRole==ResponseRole.FacilityTechnician)||!Local(c.Actor,c.Recipient))return "local_specialist_required";
                if(c.Subject.Has("npc")&&!Assessment(c.Actor.Id,c.Subject.Id,c.Observation.Id))return "person_assessment_incomplete";
            }
            return CapacityForProgress(i,c);
        }
        string Capacity() => world.Entities.Values.Count(e=>e.Has("response-record"))>=profile.MaxRecords?"response_record_capacity_reached":null;
        string CapacityForProgress(ResponseInput i,Context c)
        {
            Entity existing=Find(ProgressId(i,c.Subject,c.Observation));
            if(existing!=null&&existing.Get("complete")==1)return "already_complete";
            return existing==null?Capacity():null;
        }
        bool Assessment(string actor,string patient,string observation) => world.Entities.Values.Any(e=>e.Kind=="response-progress"&&e.ParentId==patient&&Tag(e,"actor")==actor&&Tag(e,"observation")==observation&&Tag(e,"action")=="assess-person"&&e.Get("complete")==1);

        public ResponseOutcome Apply(ResponseInput input)
        {
            Context c;string error=Validate(input,out c);
            var result=new ResponseOutcome {Tick=input?.Tick??world.Tick,Sequence=input?.Sequence??0,ActorId=input?.ActorId,Action=input?.Action,Accepted=error==null,Reason=error??"applied"};
            if(error!=null)return result; // Refusal is an outcome for the input tape; absolutely no world mutation.
            if(input.Action=="observe")Observe(input,c,result);
            else if(input.Action=="report"||input.Action=="request")Send(input,c,result);
            else if(input.Action=="acknowledge"||input.Action=="readback"||input.Action=="authorize")
            {
                result.RecordId=c.Message.Id;
                if(input.Action=="acknowledge")Write(c.Message,"acknowledged",1,"acknowledgement",c.Actor.Id);
                else if(input.Action=="readback")Write(c.Message,"readback",1,"content-readback",c.Actor.Id);
                else
                {
                    c.Message.Tags.Add("executor:"+c.Executor.Id);c.Message.Links.Add(c.Executor.Id);Write(c.Message,"authorized",1,"request-authorization",c.Actor.Id);
                    Send(input,c,result);c.Message.Tags.Add("instruction:"+result.RecordId);c.Message.Links.Add(result.RecordId);
                }
                if(input.Action!="authorize"){result.Complete=true;result.Progress=1;}
            }
            else Progress(input,c,result);
            if(input.Duration>0)
            {
                double used=c.Actor.Get("response.time.tick",-1)==world.Tick?c.Actor.Get("response.time.used"):0;
                Write(c.Actor,"response.time.tick",world.Tick,"accepted-command-time",c.Actor.Id);Write(c.Actor,"response.time.used",used+input.Duration,"accepted-command-time",c.Actor.Id);
            }
            Write(c.Actor,"response.last-sequence",input.Sequence,"accepted-command",c.Actor.Id);
            Changed?.Invoke(result);return result;
        }
        void Observe(ResponseInput i,Context c,ResponseOutcome result)
        {
            var e=Make(RecordId(i),"observation",c.Subject,c.Actor.Id);world.Add(e);
            Write(e,"observed.tick",world.Tick,"local-observation",c.Subject.Id);Write(e,"source.version",c.Subject.Version,"local-observation",c.Subject.Id);
            Write(e,"confidence",profile.Confidence,"inference-provenance",c.Subject.Id);
            foreach(string hazard in Hazards)Write(e,"hazard."+hazard,c.Subject.Get(hazard),"local-observation",c.Subject.Id);
            Write(e,"hazard.health-loss",c.Subject.Has("npc")?Math.Max(0,1-c.Subject.Get("health",1)):0,"local-observation",c.Subject.Id);
            if(c.Subject.Get("response.public-alert-known")>0)Write(e,"hazard.public-alert",world.Field("regional-alert"),"received-public-alert",c.Subject.Id);
            var people=world.Entities.Values.Where(p=>p.Has("npc")&&Visible(c.Actor,p,profile.NearbyPeopleRange)&&Distance(p,c.Subject)<=profile.NearbyPeopleRange).ToArray();
            foreach(var p in people){e.Links.Add(p.Id);e.Tags.Add("person:"+p.Id);}
            Write(e,"people.observed",people.Length,"local-count",c.Subject.Id);
            foreach(var r in world.Entities.Values.Where(r=>!r.Has("response-record")&&(r.Has("extinguisher")||r.Has("barrier")||r.Has("first-aid")||r.Has("accessible-transport"))&&Visible(c.Actor,r,profile.NearbyPeopleRange)).ToArray()) {e.Links.Add(r.Id);e.Tags.Add("resource:"+r.Id);}
            double urgency=e.Get("hazard.fire")>0||e.Get("hazard.medical.condition")>0||e.Get("hazard.health-loss")>.25?3:e.State.Any(s=>s.Key.StartsWith("hazard.",StringComparison.Ordinal)&&s.Value>.1)?2:1;
            Write(e,"urgency",urgency,"observed-priority",c.Subject.Id);result.RecordId=e.Id;result.Complete=true;result.Progress=1;
        }
        void Send(ResponseInput i,Context c,ResponseOutcome result)
        {
            string id=i.Action=="authorize"?Id("message",i.ActorId+"|"+i.Sequence+"|authorize|"+c.Message.Id):RecordId(i);
            var e=Make(id,"message",c.Subject,c.Actor.Id,c.Observation);
            e.Tags.Add("recipient:"+c.Recipient.Id);e.Tags.Add("channel:"+(i.ChannelId??"face-to-face"));e.Tags.Add("channel-kind:"+c.ChannelKind);e.Tags.Add("readback-code:"+Stable.Hash(e.Id+"|"+c.Observation.Id).Substring(0,8));e.Links.Add(c.Recipient.Id);
            if(c.Channel!=null)e.Links.Add(c.Channel.Id);if(i.Action=="request")e.Tags.Add("request:"+i.RequestKind);
            if(i.Action=="authorize"){e.Tags.Add("authorization-for:"+c.Message.Id);e.Tags.Add("request:"+Tag(c.Message,"request"));e.Links.Add(c.Message.Id);}
            world.Add(e);
            foreach(var field in c.Observation.State)Write(e,field.Key,field.Value,"report-snapshot",c.Observation.Id);
            var cp=channels[c.ChannelKind];double seconds=cp.DelayMinSeconds+(cp.DelayMaxSeconds-cp.DelayMinSeconds)*Stable.Unit(world.Seed,"response-message-delay",world.Tick,e.Id);
            Write(e,"sent.tick",world.Tick,"message-send",c.Actor.Id);Write(e,"due.tick",world.Tick+Math.Max(1,Math.Ceiling(seconds/world.StepSeconds)),"message-delay",c.Actor.Id);
            Write(e,"delivered",0,"message-send",c.Actor.Id);Write(e,"lost",0,"message-send",c.Actor.Id);Write(e,"acknowledged",0,"message-send",c.Actor.Id);Write(e,"readback",0,"message-send",c.Actor.Id);Write(e,"authorized",0,"message-send",c.Actor.Id);
            result.RecordId=e.Id;result.Reason=i.Action=="authorize"?"authorization_sent_not_delivered":i.Action=="request"?"request_sent_not_authorized":"report_sent_not_delivered";result.Complete=false;
        }
        double DurationFor(string action) => action=="protect"?profile.ProtectSeconds:action=="assess-person"?profile.AssessSeconds:action=="control-access"?profile.AccessControlSeconds:action=="verify-isolation"?profile.IsolationVerifySeconds:action=="support-person"?profile.SupportSeconds:action=="handover"?profile.HandoverSeconds:profile.EscortSeconds;
        void Progress(ResponseInput i,Context c,ResponseOutcome result)
        {
            string id=ProgressId(i,c.Subject,c.Observation);var e=Find(id);
            if(e==null)
            {
                e=Make(id,"progress",c.Subject,c.Actor.Id,c.Observation);e.Tags.Add("action:"+i.Action);
                if(c.Patient!=null){e.Tags.Add("patient:"+c.Patient.Id);e.Links.Add(c.Patient.Id);}
                if(c.Route!=null){e.Tags.Add("route:"+c.Route.Id);e.Links.Add(c.Route.Id);}
                if(c.Resource!=null){e.Tags.Add("resource:"+c.Resource.Id);e.Links.Add(c.Resource.Id);}
                if(c.Message!=null){e.Tags.Add("message:"+c.Message.Id);e.Links.Add(c.Message.Id);}
                world.Add(e);Write(e,"started.tick",world.Tick,"action-start",c.Actor.Id);
            }
            double seconds=Math.Min(DurationFor(i.Action),e.Get("seconds")+i.Duration);Write(e,"seconds",seconds,"action-progress",c.Actor.Id);
            result.RecordId=e.Id;result.Progress=seconds/DurationFor(i.Action);Write(e,"progress",result.Progress,"action-progress",c.Actor.Id);
            if(result.Progress<1){result.Reason="partial_action_continue";return;}
            bool escort=i.Action=="evacuate"||i.Action=="guide-evacuation"||i.Action=="assist-evacuation";
            if(escort)
            {
                Write(e,"in-progress",1,"escort-assigned",c.Actor.Id);Write(e,"complete",0,"escort-await-arrival",c.Actor.Id);
                Write(c.Patient,"response.escort-active",1,"escort-assigned",c.Actor.Id);
                Write(c.Patient,"response.escort."+c.Actor.Id,1,"escort-assigned",c.Actor.Id);
                if(c.Patient.Get("mobility",1)>0)
                {
                    foreach(var key in c.Patient.State.Keys.Where(k=>k.StartsWith("goal.target.",StringComparison.Ordinal)).ToArray())Write(c.Patient,key,0,"directed-evacuation",c.Actor.Id);
                    Write(c.Patient,"goal.target."+c.Route.Id,1,"directed-evacuation",c.Actor.Id);Write(c.Patient,"goal.active",1,"directed-evacuation",c.Actor.Id);Write(c.Patient,"goal.external",1,"directed-evacuation",c.Actor.Id);Write(c.Patient,"goal.kind",1,"directed-evacuation",c.Actor.Id);
                    foreach(string axis in new[]{"x","y","z"})Write(c.Patient,"goal."+axis,c.Route.Get("destination."+axis,Center(c.Route,axis)),"directed-evacuation",c.Actor.Id);
                }
                result.Reason=c.Patient.Get("mobility",1)<=0?"transport_assistance_pending_actual_motion":"escort_started_actual_arrival_required";return;
            }
            Write(e,"complete",1,"action-evidence",c.Actor.Id);Write(e,"completed.tick",world.Tick,"action-evidence",c.Actor.Id);result.Complete=true;
            if(i.Action=="protect")Write(c.Subject,"response.warning."+c.Actor.Id,1,"keep-clear-warning",c.Actor.Id);
            if(i.Action=="control-access") {Write(c.Subject,"response.access-controlled",1,"placed-barrier-check",c.Actor.Id);Write(c.Resource,"response.assigned."+c.Subject.Id,1,"barrier-assignment",c.Actor.Id);}
            if(i.Action=="verify-isolation")Write(c.Subject,"response.isolation-verified.tick",world.Tick,"physical-isolation-verification",c.Actor.Id);
            if(i.Action=="support-person") {Write(c.Subject,"response.support-active",1,"local-person-support",c.Actor.Id);Write(c.Subject,"response.supporter."+c.Actor.Id,1,"local-person-support",c.Actor.Id);Write(c.Subject,"assisted",1,"local-person-support",c.Actor.Id);}
            if(i.Action=="handover") {e.Tags.Add("recipient:"+c.Recipient.Id);e.Links.Add(c.Recipient.Id);Write(e,"hazard_resolved",0,"handover-is-not-resolution",c.Actor.Id);}
        }

        /// <summary>Call once after physical/navigation observations for this tick. Does not advance Tick.</summary>
        public void Step()
        {
            if(!Finite(world.StepSeconds)||world.StepSeconds<=0)throw new ArgumentException("Finite positive response time step required");
            foreach(var m in world.Entities.Values.Where(e=>e.Kind=="response-message"&&e.Get("delivered")==0&&e.Get("lost")==0&&e.Get("due.tick")<=world.Tick).ToArray())
            {
                var actor=Find(Tag(m,"actor"));var recipient=Find(Tag(m,"recipient"));string kind=Tag(m,"channel-kind");var equipment=Find(Tag(m,"channel"));
                bool unavailable=actor==null||recipient==null||recipient.Get("removed")>0||!channels.ContainsKey(kind)||ChannelReady(actor,recipient,equipment,kind,false)!=null;
                double loss=channels.ContainsKey(kind)?channels[kind].LossProbability:1;
                if(kind!="face-to-face")loss=1-(1-loss)*(1-CausalKernel.Clamp(world.Field("communications-disruption")));
                bool dropped=unavailable||Stable.Unit(world.Seed,"response-message-loss",(long)m.Get("sent.tick"),m.Id)<loss;
                Write(m,dropped?"lost":"delivered",1,dropped?"virtual-message-loss":"virtual-message-delivery",actor?.Id??m.Id);Write(m,"settled.tick",world.Tick,"virtual-message-result",m.Id);
                if(!dropped)
                {
                    Write(recipient,"response.known."+Tag(m,"observation"),1,"report-knowledge",m.Id);
                    double knownRisk=m.State.Where(s=>s.Key.StartsWith("hazard.",StringComparison.Ordinal)).Select(s=>s.Value).DefaultIfEmpty(0).Max();
                    Write(recipient,"awareness",Math.Max(recipient.Get("awareness"),CausalKernel.Clamp(knownRisk)),"report-knowledge",m.Id);
                }
            }
            foreach(var e in world.Entities.Values.Where(e=>e.Kind=="response-progress"&&e.Get("in-progress")==1&&e.Get("complete")==0).ToArray())
            {
                var person=Find(Tag(e,"patient"));var route=Find(Tag(e,"route"));var actor=Find(Tag(e,"actor"));
                if(person==null||route==null||actor==null)continue;
                bool unsafeRoute=route.Get("blocked")>0||route.Get("failed")>0||route.Get("fire")>.1||route.Get("smoke")>.3;
                string prefix="response.route."+route.Id+".";
                bool pathLost=person.Get(prefix+"complete")!=1||person.Get(prefix+"risk")>.1||!person.State.ContainsKey(prefix+"verified.tick")||world.Tick<person.Get(prefix+"verified.tick")||(world.Tick-person.Get(prefix+"verified.tick"))*world.StepSeconds>profile.RouteFreshSeconds;
                bool failed=person.Get("goal.failed")>0||unsafeRoute||pathLost;
                Write(e,"route-invalidated",failed?1:0,"escort-route-monitor",person.Id);
                if(failed)
                {
                    Write(e,"in-progress",0,"escort-paused-reassess",person.Id);Write(person,"response.escort-active",0,"escort-paused-reassess",person.Id);
                    if(person.Get("goal.target."+route.Id)==1){Write(person,"goal.active",0,"escort-paused-reassess",person.Id);Write(person,"goal.external",0,"escort-paused-reassess",person.Id);}
                    continue;
                }
                double dx=person.X-route.Get("destination.x",Center(route,"x")),dy=person.Y-route.Get("destination.y",Center(route,"y")),dz=person.Z-route.Get("destination.z",Center(route,"z"));
                if(Math.Sqrt(dx*dx+dy*dy+dz*dz)>person.Get("arrival.radius",.75))continue;
                Write(e,"complete",1,"actual-evacuation-arrival",person.Id);Write(e,"completed.tick",world.Tick,"actual-evacuation-arrival",person.Id);Write(e,"in-progress",0,"actual-evacuation-arrival",person.Id);Write(person,"response.escort-active",0,"actual-evacuation-arrival",person.Id);
            }
            foreach(var person in world.Entities.Values.Where(e=>e.Get("response.support-active")==1).ToArray())
            {
                bool present=world.Entities.Values.Any(a=>person.Get("response.supporter."+a.Id)==1&&a.Get("health",1)>.05&&Local(a,person));
                if(!present||person.Get("response.assistance-consent")!=1){Write(person,"response.support-active",0,"support-contact-ended",person.Id);Write(person,"assisted",0,"support-contact-ended",person.Id);}
            }
        }
        public IReadOnlyList<ResponseTask> Tasks(string actorId)
        {
            var actor=Find(actorId);var role=Role(actorId);var tasks=new List<ResponseTask>();if(actor==null||!role.HasValue)return tasks;
            foreach(var message in world.Entities.Values.Where(m=>m.Kind=="response-message"&&Tag(m,"recipient")==actorId&&m.Get("delivered")==1))
            {
                string action=message.Get("acknowledged")==0?"acknowledge":message.Get("readback")==0?"readback":Tag(message,"authorization-for")==null&&Tag(message,"request")!=null&&message.Get("authorized")==0&&role==ResponseRole.Control?"authorize":null;
                if(action!=null)tasks.Add(new ResponseTask {Id=Id("task",actorId+"|"+message.Id+"|"+action),ActorId=actorId,SubjectId=message.ParentId,ObservationId=Tag(message,"observation"),MessageId=message.Id,Action=action,Reason="Delivered virtual communication requires receipt and content confirmation",Status="pending"});
            }
            foreach(var observation in world.Entities.Values.Where(e=>e.Kind=="response-observation"&&Fresh(e)&&Knows(actor,e)))
            {
                var subject=Find(observation.ParentId);if(subject==null)continue;
                bool hazard=observation.State.Any(s=>s.Key.StartsWith("hazard.",StringComparison.Ordinal)&&s.Value>.1);
                bool person=subject.Has("npc")&&(observation.Get("hazard.medical.condition")>0||observation.Get("hazard.health-loss")>.25||observation.Get("hazard.fallen")>0);
                var actions=new List<string>();if(hazard){actions.Add("protect");actions.Add("report");actions.Add("evacuate");}
                if(person){actions.Add("assess-person");actions.Add("support-person");actions.Add("request:medical");actions.Add("handover");}
                if(hazard&&Privileged(role.Value)){actions.Add("control-access");actions.Add("request:evacuation");actions.Add("request:specialist");}
                if(observation.Get("hazard.fault")>0&&Privileged(role.Value))actions.Add("request:isolation");
                foreach(string action in actions.Distinct(StringComparer.Ordinal))
                {
                    var evidence=world.Entities.Values.Where(e=>e.Kind=="response-progress"&&e.ParentId==subject.Id&&Tag(e,"actor")==actorId&&Tag(e,"observation")==observation.Id&&Tag(e,"action")==action).OrderByDescending(e=>e.Get("progress")).FirstOrDefault();
                    bool sent=(action=="report"||action.StartsWith("request:",StringComparison.Ordinal))&&world.Entities.Values.Any(m=>m.Kind=="response-message"&&Tag(m,"actor")==actorId&&Tag(m,"observation")==observation.Id&&(action=="report"?Tag(m,"request")==null:Tag(m,"request")==action.Substring(8))&&m.Get("delivered")==1);
                    tasks.Add(new ResponseTask {Id=Id("task",actorId+"|"+observation.Id+"|"+action),ActorId=actorId,SubjectId=subject.Id,ObservationId=observation.Id,Action=action,Reason="Locally known condition; field sequence and timing are inferred training",Progress=sent?1:evidence?.Get("progress")??0,Status=sent||evidence?.Get("complete")==1?"complete":evidence?.Get("route-invalidated")==1?"reassess":evidence==null?"pending":"partial"});
                }
            }
            return tasks.OrderBy(t=>t.Id,StringComparer.Ordinal).ToArray();
        }
        public ResponseRecord Inspect(string recordId)
        {
            var e=Find(recordId);if(e==null||!e.Has("response-record"))throw new ArgumentException("Existing response record required");
            return new ResponseRecord {Id=e.Id,Kind=e.Kind,SubjectId=e.ParentId,ActorId=Tag(e,"actor"),ObservationId=Tag(e,"observation"),RecipientId=Tag(e,"recipient"),ChannelId=Tag(e,"channel"),RequestKind=Tag(e,"request"),ExecutorId=Tag(e,"executor"),ReadbackCode=Tag(e,"readback-code"),Links=e.Links.ToArray(),State=new SortedDictionary<string,double>(e.State,StringComparer.Ordinal)};
        }
    }
}
