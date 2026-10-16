using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    public sealed class CrowdArrivalSource
    {
        public string Id,EntryEntityId,DestinationEntityId,Role="visitor",TrainEntityId,RateUnit;
        public double StartSeconds,EndSeconds,ArrivalsPerHour,MinimumStaySeconds,MaximumStaySeconds;
        public int MaximumPartySize=1,MaximumNonPassengerCompanions;
    }
    public sealed class CrowdTrainWave
    {
        public string Id,TrainEntityId,EntryEntityId,DestinationEntityId;
        public double AtSeconds,OccupancyFraction,MinimumStaySeconds,MaximumStaySeconds;
        public int Capacity;
    }
    public sealed class CrowdIndividual
    {
        public string Id,SourceId,PartyId,LeaderId,Role,EntryEntityId,DestinationEntityId,TrainEntityId;
        public double ArrivalSeconds,IntendedStaySeconds;
    }
    // Flow counts and train waves are exogenous inputs, not emergency scripts.
    // No arrival/departure, physical pose or completed movement is fabricated here.
    public sealed class CrowdFlowPlan
    {
        public const string Version="seeded-individual-crowd-flow-v2";
        readonly CrowdIndividual[] people;
        public ulong Seed {get;private set;}
        public string InputFingerprint {get;private set;}
        public IReadOnlyList<CrowdIndividual> Individuals=>Array.AsReadOnly(people.Select(Copy).ToArray());
        CrowdFlowPlan(ulong seed,string input,CrowdIndividual[] rows){Seed=seed;InputFingerprint=input;people=rows;}
        static CrowdIndividual Copy(CrowdIndividual p)=>new CrowdIndividual{Id=p.Id,SourceId=p.SourceId,PartyId=p.PartyId,LeaderId=p.LeaderId,Role=p.Role,EntryEntityId=p.EntryEntityId,DestinationEntityId=p.DestinationEntityId,TrainEntityId=p.TrainEntityId,ArrivalSeconds=p.ArrivalSeconds,IntendedStaySeconds=p.IntendedStaySeconds};
        static void Require(bool value,string reason){if(!value)throw new ArgumentException("Crowd flow: "+reason);}
        static bool Finite(double d)=>!double.IsNaN(d)&&!double.IsInfinity(d);
        static void Stay(double low,double high)=>Require(Finite(low)&&Finite(high)&&low>=0&&high>=low&&high<=86400,"explicit bounded stay assumptions required");
        public static double EffectivePartyArrivalsPerHour(CrowdArrivalSource source)
        {
            Require(source!=null&&new[]{"PASSENGER_PERSON_EVENTS_PER_HOUR","VISITOR_PERSONS_PER_HOUR","STAFF_PERSONS_PER_HOUR","PARTY_ARRIVALS_PER_HOUR"}.Contains(source.RateUnit),"explicit persons-versus-parties rate unit required");
            Require(source.RateUnit=="PARTY_ARRIVALS_PER_HOUR"||(source.Role=="passenger"&&source.RateUnit=="PASSENGER_PERSON_EVENTS_PER_HOUR")||(source.Role=="visitor"&&source.RateUnit=="VISITOR_PERSONS_PER_HOUR")||(source.Role=="staff"&&source.RateUnit=="STAFF_PERSONS_PER_HOUR"),"rate unit and actor population must agree");
            return source.RateUnit=="PARTY_ARRIVALS_PER_HOUR"?source.ArrivalsPerHour:source.ArrivalsPerHour/((1+source.MaximumPartySize)/2.0);
        }
        public static CrowdFlowPlan Generate(ulong seed,IEnumerable<CrowdArrivalSource> arrivals,IEnumerable<CrowdTrainWave> waves,int maximumIndividuals)
        {
            Require(maximumIndividuals>0&&maximumIndividuals<=1000000,"explicit resource envelope required; overflow refuses rather than truncates");
            var sources=(arrivals??Array.Empty<CrowdArrivalSource>()).OrderBy(x=>x?.Id,StringComparer.Ordinal).ToArray();var trains=(waves??Array.Empty<CrowdTrainWave>()).OrderBy(x=>x?.Id,StringComparer.Ordinal).ToArray();
            Require(sources.All(x=>x!=null)&&trains.All(x=>x!=null),"null flow source");var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var s in sources){Require(!string.IsNullOrEmpty(s.Id)&&ids.Add(s.Id)&&!string.IsNullOrEmpty(s.EntryEntityId)&&!string.IsNullOrEmpty(s.DestinationEntityId)&&new[]{"visitor","passenger","staff"}.Contains(s.Role),"source identity/role/endpoint required");Require(Finite(s.StartSeconds)&&Finite(s.EndSeconds)&&s.StartSeconds>=-86400&&s.EndSeconds<=86400&&s.EndSeconds>=s.StartSeconds&&Finite(s.ArrivalsPerHour)&&s.ArrivalsPerHour>=0&&s.ArrivalsPerHour<=1000000&&s.MaximumPartySize>=1&&s.MaximumPartySize<=16&&s.MaximumNonPassengerCompanions>=0&&s.MaximumNonPassengerCompanions<=16,"bounded flow rate/window/party required");Stay(s.MinimumStaySeconds,s.MaximumStaySeconds);EffectivePartyArrivalsPerHour(s);}
            foreach(var w in trains){Require(!string.IsNullOrEmpty(w.Id)&&ids.Add(w.Id)&&!string.IsNullOrEmpty(w.EntryEntityId)&&!string.IsNullOrEmpty(w.DestinationEntityId)&&!string.IsNullOrEmpty(w.TrainEntityId),"unique wave and linked train/endpoints required");Require(Finite(w.AtSeconds)&&Math.Abs(w.AtSeconds)<=86400&&Finite(w.OccupancyFraction)&&w.OccupancyFraction>=0&&w.OccupancyFraction<=1&&w.Capacity>=0&&w.Capacity<=100000,"bounded train capacity/load/time required");Stay(w.MinimumStaySeconds,w.MaximumStaySeconds);}
            string input=Stable.Hash(LocalTapeCodec.Encode((JObject)LocalTapeCodec.FromObject(new {version=Version,seed=seed.ToString(),arrivals=sources,waves=trains,maximum_individuals=maximumIndividuals})).ToString(Formatting.None));
            var rows=new List<CrowdIndividual>();
            Action<string,string,string,string,string,double,double,double,int,int,string> add=(source,entry,destination,role,train,at,low,high,members,companions,eventIdentity)=>
            {
                int partySize=members+companions;Require(rows.Count<=maximumIndividuals-partySize,"requested population exceeds explicit envelope; no partial world mutation");string party="party:"+Stable.Hash(new JArray(Version,source,eventIdentity,InitialConditionMaterial.FloatBits(at)).ToString(Formatting.None)).Substring(0,32);string leader=null;
                for(int member=0;member<partySize;member++){string id="crowd:"+Stable.Hash(new JArray(Version,source,party,member).ToString(Formatting.None)).Substring(0,32);if(leader==null)leader=id;rows.Add(new CrowdIndividual{Id=id,SourceId=source,PartyId=party,LeaderId=leader,Role=member<members?role:"companion",EntryEntityId=entry,DestinationEntityId=destination,TrainEntityId=train,ArrivalSeconds=at,IntendedStaySeconds=low+Stable.Unit(seed,"crowd-stay",0,id)*(high-low)});}
            };
            foreach(var s in sources)
            {
                double partyRate=EffectivePartyArrivalsPerHour(s);if(partyRate==0)continue;double at=s.StartSeconds;int index=0;
                while(true){string draw=Stable.Hash(new JArray(s.Id,index++).ToString(Formatting.None));double u=Stable.Unit(seed,"crowd-arrival-interval",0,draw);at+=-Math.Log(Math.Max(1e-15,1-u))/(partyRate/3600);if(at>s.EndSeconds)break;int size=1+(int)(Stable.Unit(seed,"crowd-party-size",0,draw)*s.MaximumPartySize);int companions=(int)(Stable.Unit(seed,"crowd-nonpassenger-companions",0,draw)*(s.MaximumNonPassengerCompanions+1));add(s.Id,s.EntryEntityId,s.DestinationEntityId,s.Role,s.TrainEntityId,at,s.MinimumStaySeconds,s.MaximumStaySeconds,size,companions,draw);}
            }
            foreach(var w in trains)for(int seat=0;seat<w.Capacity;seat++)
                if(Stable.Unit(seed,"crowd-train-occupancy",0,Stable.Hash(new JArray(w.Id,seat).ToString(Formatting.None)))<w.OccupancyFraction)add(w.Id,w.EntryEntityId,w.DestinationEntityId,"passenger",w.TrainEntityId,w.AtSeconds,w.MinimumStaySeconds,w.MaximumStaySeconds,1,0,seat.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return new CrowdFlowPlan(seed,input,rows.OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray());
        }
        public JObject Census(double atSeconds)
        {
            Require(Finite(atSeconds),"census clock finite");return new JObject{["individuals_all"]=people.Length,["planned_rail_passenger_events"]=people.Count(p=>p.Role=="passenger"),["non_passenger_companions"]=people.Count(p=>p.Role=="companion"),["staff_individuals"]=people.Count(p=>p.Role=="staff"),["arrived_by_clock"]=people.Count(p=>p.ArrivalSeconds<=atSeconds),["planned_presence_envelope"]=people.Count(p=>p.ArrivalSeconds<=atSeconds&&p.ArrivalSeconds+p.IntendedStaySeconds>atSeconds),["unique_parties"]=people.Select(p=>p.PartyId).Distinct().Count(),["claim"]="Planned flow/stay envelope, not observed simultaneous occupancy or completed physical departures."};
        }
        public void RegisterAll(World world)
        {
            Require(world!=null&&world.Seed==Seed&&world.Tick==0,"same seeded initial world required");
            foreach(var p in people){Require(!world.Entities.ContainsKey(p.Id)&&world.Entities.ContainsKey(p.EntryEntityId)&&world.Entities.ContainsKey(p.DestinationEntityId)&&(p.TrainEntityId==null||world.Entities.ContainsKey(p.TrainEntityId)),"all immutable individual IDs and existing endpoints preflight before mutation");}
            var pending=new List<Entity>();
            foreach(var p in people){var entry=world.Entities[p.EntryEntityId];var e=new Entity{Id=p.Id,Kind="crowd-individual",X=entry.X,Y=entry.Y,Z=entry.Z};e.Tags.Add("crowd-individual");e.Tags.Add("training-model");e.Tags.Add("crowd-role:"+p.Role);e.Tags.Add("crowd-party:"+p.PartyId);e.Tags.Add("crowd-plan:"+InputFingerprint);e.Links.Add(p.EntryEntityId);e.Links.Add(p.DestinationEntityId);e.Links.Add(p.LeaderId);if(p.TrainEntityId!=null)e.Links.Add(p.TrainEntityId);e.Set("crowd.arrival.seconds",p.ArrivalSeconds);e.Set("crowd.intended-stay.seconds",p.IntendedStaySeconds);e.Set("crowd.physical-presence-observed",0);pending.Add(e);}
            foreach(var e in pending)world.Add(e);
        }
    }
    public static class CrowdDetailSelection
    {
        // Selection only; never edits identity, pose, seed, pending goals or RNG streams.
        public static IReadOnlyList<string> Select(World world,double x,double y,double z,double radius,int maximumDetailed)
        {
            if(world==null||new[]{x,y,z,radius}.Any(v=>double.IsNaN(v)||double.IsInfinity(v))||radius<0||radius>1e6||maximumDetailed<0)throw new ArgumentException("Bounded detail selection required");
            return Array.AsReadOnly(world.Entities.Values.Where(e=>e.Has("npc")||e.Has("crowd-individual")).Select(e=>new{id=e.Id,d=(e.X-x)*(e.X-x)+(e.Y-y)*(e.Y-y)+(e.Z-z)*(e.Z-z)}).Where(e=>e.d<=radius*radius).OrderBy(e=>e.d).ThenBy(e=>e.id,StringComparer.Ordinal).Take(maximumDetailed).Select(e=>e.id).ToArray());
        }
    }
}
