using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    public enum PointKind { Exit, Counter, Shop, Wait, Office, Seat, Chair, Toilet, PlatformWait, Meet }

    /// <summary>
    /// Navmesh-validated places, zones, vertical links, platforms and the KTX interior of the Busan twin, generated at
    /// edit time (station-points.json, version 2). Train positions are world positions at the set's stop pose.
    /// </summary>
    public sealed class StationPoints
    {
        public const string HistoricalModelCatalogSha256="f6dd09c80f0b874eaf6f74df92538c20fb9319388cc8f126832143e04da86f71";
        public const string ModelActiveDestination="MODEL_ACTIVE_FOOT_QUALIFIED";
        public const string PendingDestination="PENDING_CURRENT_DESTINATION_CORRESPONDENCE";
        public const string NoPhysicalDestination="NOT_GEOMETRY_BOUND";
        int sourceVersion;
        Point[] sourceMetadata;
        [Serializable] public sealed class ZoneEntry { public string id, label; public Vector3 min, max; public int priority; }
        [Serializable] public sealed class PointEntry { public string id, kind, label, zone, slot; public Vector3 position, anchor; public float yaw; }
        [Serializable] public sealed class EscalatorEntry { public string id, label, from, to; public bool up, stairs; public Vector3[] path; public Vector3 stairsTop, stairsBottom; }
        [Serializable] public sealed class ElevatorStop { public string floor, zone; public Vector3 door; public float yaw; }
        [Serializable] public sealed class ElevatorEntry { public string id, label; public ElevatorStop[] stops; }
        [Serializable] public sealed class PlatformEntry { public string id, label; public Vector3 a, b; public float halfWidth; }
        [Serializable] public sealed class DoorEntry { public string[] leaves; public Vector3 openDelta; public string lift; public Vector3 liftDelta, outside, inside; }
        [Serializable] public sealed class SeatEntry { public string id; public int row; public Vector3 anchor, front, aisle; public float yaw; }
        [Serializable] public sealed class CarEntry { public string name, label; public int number; public float floor; public bool reachable; public DoorEntry door; public Vector3[] aisle; public SeatEntry[] seats; public Vector3 min, max; }
        [Serializable] public sealed class TrainEntry { public string root, platform, track; public Vector3 axis; public CarEntry[] cars; }
        [Serializable] private sealed class File { public int version; public string note; public ZoneEntry[] zones; public PointEntry[] points; public EscalatorEntry[] escalators; public ElevatorEntry[] elevators; public PlatformEntry[] platforms; public TrainEntry train; }

        public sealed class Point
        {
            public string Id, Label, Zone;
            public PointKind Kind;
            /// <summary>Walkable navmesh position where a person stands for this place.</summary>
            public Vector3 Position;
            /// <summary>Facing on arrival (degrees). For seats and chairs: the direction the seated person faces.</summary>
            public float Yaw;
            /// <summary>Seats and chairs: floor point under the seated hips.</summary>
            public Vector3 Anchor;
            /// <summary>Seats: both sides of one bench slot share this key; one person per slot.</summary>
            public string Slot;
        }

        public sealed class CurrentBinding
        {
            public const string CurrentSourceUnavailableStatus="UNAVAILABLE_CURRENT_SOURCE_SUPPORT";
            public const string CurrentSourceEmptyEvidence="COMPLETE_CURRENT_SOURCE_SUPPORT_VOLUME_EMPTY;";
            public const string CurrentSourceFloorEvidence="COMPLETE_CURRENT_SOURCE_CENTER_CONTACT_DOMAIN_EMPTY;";
            public const string CurrentSourceFootprintEvidence="COMPLETE_CURRENT_SOURCE_REQUIRED_FOOTPRINTS_UNSUPPORTED;";
            public Point Source { get; internal set; }
            public Point Runtime { get; internal set; }
            public string Status { get; internal set; }
            public string Evidence { get; internal set; }
            public bool Bound => Runtime!=null;
            internal IReadOnlyList<Vector3> AuthorizedProposals {get;set;}
        }
        /// <summary>Operational MODEL admission is orthogonal to physical binding.
        /// Neither an active destination nor a pending one certifies its historical
        /// role, seated Anchor, public fixture correspondence or field placement.</summary>
        public sealed class CurrentDestination
        {
            public CurrentBinding Physical {get;internal set;}
            public Point Source=>Physical.Source;
            public Point Runtime=>Physical.Runtime;
            public string Status {get;internal set;}
            public string Evidence {get;internal set;}
            public bool Active=>Status==ModelActiveDestination;
            internal Point ExpectedSource,ExpectedRuntime,SourceState,RuntimeState;
            internal string PhysicalStatus,PhysicalEvidence;
            internal IReadOnlyList<Vector3> Proposals;
        }
        public sealed class CurrentRequiredEndpoint
        {
            public string Label {get;internal set;}
            public string Owner {get;internal set;}
            public Vector3 SourcePosition {get;internal set;}
            public Vector3 GraphPosition {get;internal set;}
            public float MaxAttachmentDistance {get;internal set;}
            public float MaxAttachmentHeight {get;internal set;}
            public string Evidence {get;internal set;}
        }
        public IReadOnlyList<CurrentDestination> CurrentDestinations {get;private set;}=Array.Empty<CurrentDestination>();
        public IReadOnlyList<CurrentRequiredEndpoint> RequiredCurrentEndpoints {get;private set;}=Array.Empty<CurrentRequiredEndpoint>();

        // Exact complete-catalog origin, not a failure-ID/kind list. This is a
        // explicit historical MODEL admission policy, not current role authority.
        internal void RequireCurrentModelOrigin()
        {
            if(sourceVersion!=2||SourceSha256!=HistoricalModelCatalogSha256||SourceAll==null||SourceAll.Count!=1449||
                sourceMetadata==null||sourceMetadata.Length!=SourceAll.Count)
                throw new InvalidOperationException("Current destination catalog origin/schema is unknown");
            var ids=new HashSet<string>();
            for(int i=0;i<SourceAll.Count;i++)
            {
                var row=SourceAll[i];var expected=sourceMetadata[i];
                if(row==null||string.IsNullOrWhiteSpace(row.Id)||!ids.Add(row.Id)||
                    !SameCurrentPointMetadata(row,expected)||!row.Position.Equals(expected.Position)||
                    !FiniteStandingPosition(row.Position)||!FiniteStandingPosition(row.Anchor)||float.IsNaN(row.Yaw)||float.IsInfinity(row.Yaw))
                    throw new InvalidOperationException("Current destination catalog source metadata changed: "+i);
            }
        }

        internal IReadOnlyList<CurrentDestination> CaptureCurrentDestinations(IReadOnlyDictionary<string,string> admitted,
            IReadOnlyDictionary<string,string> pending)
        {
            RequireCurrentModelOrigin();
            if(CurrentBindings.Count!=SourceAll.Count)throw new InvalidOperationException("Physical destination ledger is incomplete");
            var rows=new List<CurrentDestination>();var accounted=new HashSet<string>();
            for(int i=0;i<SourceAll.Count;i++)
            {
                var physical=CurrentBindings[i];
                if(physical==null||!ReferenceEquals(physical.Source,SourceAll[i]))throw new InvalidOperationException("Physical source order differs");
                string proof;string status;
                if(!physical.Bound){status=NoPhysicalDestination;proof=physical.Evidence;}
                else if(admitted.TryGetValue(physical.Source.Id,out proof)){status=ModelActiveDestination;accounted.Add(physical.Source.Id);}
                else if(pending.TryGetValue(physical.Source.Id,out proof)){status=PendingDestination;accounted.Add(physical.Source.Id);}
                else throw new InvalidOperationException("Geometry-bound destination lacks an explicit admission row: "+physical.Source.Id);
                rows.Add(new CurrentDestination{Physical=physical,Status=status,Evidence=proof,
                    ExpectedSource=physical.Source,ExpectedRuntime=physical.Runtime,SourceState=CaptureCurrentPointMetadata(physical.Source),RuntimeState=physical.Runtime==null?null:CaptureCurrentPointMetadata(physical.Runtime),
                    PhysicalStatus=physical.Status,PhysicalEvidence=physical.Evidence,Proposals=physical.AuthorizedProposals});
            }
            if(admitted.Count+pending.Count!=accounted.Count)
                throw new InvalidOperationException("Destination admission contains duplicate/foreign source IDs");
            return rows.AsReadOnly();
        }

        internal StationPoints WithCurrentDestinations(IReadOnlyList<CurrentDestination> rows,IReadOnlyList<CurrentRequiredEndpoint> required)
        {
            RequireCurrentModelOrigin();
            if(rows==null||rows.Count!=SourceAll.Count||CurrentBindings.Count!=rows.Count||required==null)
                throw new InvalidOperationException("Current destination/physical/mandatory partition is incomplete");
            var active=new StationPoints{Zones=Zones,Escalators=Escalators,Elevators=Elevators,Platforms=Platforms,Train=Train,
                SourceAll=SourceAll,SourceSha256=SourceSha256,sourceVersion=sourceVersion,sourceMetadata=sourceMetadata,
                CurrentBindings=CurrentBindings,CurrentDestinations=rows,RequiredCurrentEndpoints=required};
            var boundKinds=new HashSet<PointKind>();var activeKinds=new HashSet<PointKind>();
            for(int i=0;i<rows.Count;i++)
            {
                var row=rows[i];var physical=CurrentBindings[i];
                if(row==null||!ReferenceEquals(row.Physical,physical)||!ReferenceEquals(physical.Source,SourceAll[i])||
                    !ReferenceEquals(physical.Source,row.ExpectedSource)||!ReferenceEquals(physical.Runtime,row.ExpectedRuntime)||
                    physical.Status!=row.PhysicalStatus||physical.Evidence!=row.PhysicalEvidence||
                    !ReferenceEquals(physical.AuthorizedProposals,row.Proposals)||
                    !SameCurrentPointMetadata(physical.Source,row.SourceState)||!physical.Source.Position.Equals(row.SourceState.Position)||
                    string.IsNullOrWhiteSpace(row.Evidence))throw new InvalidOperationException("Current destination transaction is stale/incomplete: "+i);
                if(!physical.Bound)
                {
                    if(row.Status!=NoPhysicalDestination||row.RuntimeState!=null)throw new InvalidOperationException("Unbound physical row cannot become a destination");
                    if(physical.Status==CurrentBinding.CurrentSourceUnavailableStatus)
                    {
                        if(!physical.Evidence.StartsWith(CurrentBinding.CurrentSourceEmptyEvidence,StringComparison.Ordinal)&&
                            !physical.Evidence.StartsWith(CurrentBinding.CurrentSourceFloorEvidence,StringComparison.Ordinal)&&
                            !physical.Evidence.StartsWith(CurrentBinding.CurrentSourceFootprintEvidence,StringComparison.Ordinal))
                            throw new InvalidOperationException("Physical unbound destination lacks complete source evidence");
                    }
                    else if(physical.Status!="UNBOUND_PROVEN_NO_CURRENT_FLOOR")
                        throw new InvalidOperationException("Unknown physical outcome cannot be a destination disposition");
                    continue;
                }
                if(ReferenceEquals(physical.Source,physical.Runtime)||physical.Status!="BOUND_MODEL_COMPUTED_WALKABLE"||
                    !SameCurrentPointMetadata(physical.Runtime,physical.Source)||
                    !SameCurrentPointMetadata(physical.Runtime,row.RuntimeState)||!physical.Runtime.Position.Equals(row.RuntimeState.Position))
                    throw new InvalidOperationException("Current destination Runtime/metadata changed: "+physical.Source.Id);
                boundKinds.Add(physical.Source.Kind);
                if(row.Active)
                {
                    if(!row.Evidence.StartsWith("CERTIFIED_CURRENT_MODEL_FOOT_DESTINATION;",StringComparison.Ordinal))
                        throw new InvalidOperationException("Active destination lacks its independent foot witness");
                    active.all.Add(physical.Runtime);activeKinds.Add(physical.Source.Kind);
                    if(!active.byKind.TryGetValue(physical.Runtime.Kind,out var list))active.byKind[physical.Runtime.Kind]=list=new List<Point>();
                    list.Add(physical.Runtime);
                }
                else if(row.Status!=PendingDestination||physical.Source.Kind==PointKind.Exit||
                    !row.Evidence.StartsWith("PENDING_HISTORICAL_MODEL_CORRESPONDENCE;",StringComparison.Ordinal))
                    throw new InvalidOperationException("Mandatory/unknown destination cannot be relabeled Pending");
            }
            if(!activeKinds.Contains(PointKind.Exit))throw new InvalidOperationException("No current active Exit");
            foreach(var kind in boundKinds)if(!activeKinds.Contains(kind))
                throw new InvalidOperationException("All geometry-positive destinations of a functional kind are pending: "+kind);
            var labels=new HashSet<string>();
            foreach(var endpoint in required)if(endpoint==null||string.IsNullOrWhiteSpace(endpoint.Label)||!labels.Add(endpoint.Label)||
                string.IsNullOrWhiteSpace(endpoint.Owner)||string.IsNullOrWhiteSpace(endpoint.Evidence)||
                !FiniteStandingPosition(endpoint.SourcePosition)||!FiniteStandingPosition(endpoint.GraphPosition))
                throw new InvalidOperationException("Current mandatory endpoint inventory is incomplete");
            foreach(var exit in active.Of(PointKind.Exit))if(!labels.Contains(exit.Id))
                throw new InvalidOperationException("Mandatory current Exit endpoint is absent from inventory: "+exit.Id);
            return active;
        }

        public void RequireActiveDestination(Point point)
        {
            if(point==null||CurrentDestinations.Count==0)return; // legacy/null-goal semantics
            foreach(var active in all)if(ReferenceEquals(active,point))return;
            throw new InvalidOperationException("Point is not an active current MODEL destination: "+point.Id);
        }
        internal sealed class CurrentRequalification
        {
            internal CurrentBinding Binding;
            internal Point ExpectedSource,ExpectedRuntime,ExpectedSourceMetadata,ExpectedRuntimeMetadata;
            internal IReadOnlyList<Vector3> ExpectedProposals;
            internal string ExpectedId,ExpectedStatus,ExpectedEvidence;
            internal Vector3 ExpectedPosition,ExpectedSourcePosition;
            internal Vector3 Position,Proposal;
            internal int ProposalIndex;
            internal string Evidence;
        }
        internal sealed class CurrentRequalificationBatch
        {
            internal IReadOnlyList<CurrentRequalification> Alternatives;
            internal string Audit;
        }

        // World.Points, byKind, CurrentBindings and RoutingView share Runtime Point
        // objects. Validate the WHOLE graph's receipt set before the first write;
        // retain these exact aliases and change only standing pose + evidence.
        internal void CommitCurrentRequalifications(IReadOnlyList<CurrentRequalification> changes)
        {
            var seen=new HashSet<CurrentBinding>();
            foreach(var change in changes)
            {
                if(change==null||change.Binding==null||!seen.Add(change.Binding)||
                    !System.Linq.Enumerable.Contains(CurrentBindings,change.Binding)||
                    !ReferenceEquals(change.Binding.Source,change.ExpectedSource)||
                    !ReferenceEquals(change.Binding.Runtime,change.ExpectedRuntime)||
                    ReferenceEquals(change.ExpectedSource,change.ExpectedRuntime)||
                    !System.Linq.Enumerable.Contains(SourceAll,change.ExpectedSource)||
                    change.Binding.Source.Id!=change.ExpectedId||change.Binding.Runtime==null||
                    change.Binding.Runtime.Id!=change.ExpectedId||change.Binding.Runtime.Kind!=change.Binding.Source.Kind||
                    change.Binding.Source.Kind==PointKind.Exit||change.Binding.Status!=change.ExpectedStatus||
                    change.ExpectedStatus!="BOUND_MODEL_COMPUTED_WALKABLE"||change.Binding.Evidence!=change.ExpectedEvidence||
                    !change.Binding.Runtime.Position.Equals(change.ExpectedPosition)||
                    !change.Binding.Source.Position.Equals(change.ExpectedSourcePosition)||
                    !SameCurrentPointMetadata(change.Binding.Source,change.ExpectedSourceMetadata)||
                    !SameCurrentPointMetadata(change.Binding.Runtime,change.ExpectedRuntimeMetadata)||
                    !ReferenceEquals(change.Binding.AuthorizedProposals,change.ExpectedProposals)||change.ExpectedProposals==null||
                    change.ProposalIndex<0||change.ProposalIndex>=change.ExpectedProposals.Count||
                    !change.Proposal.Equals(change.ExpectedProposals[change.ProposalIndex])||
                    string.IsNullOrWhiteSpace(change.Evidence)||!FiniteStandingPosition(change.Position))
                    throw new InvalidOperationException("Current standing-pose transaction is stale, incomplete or changes source identity");
            }
            // No query, callback or yield occurs in this assignment-only phase.
            foreach(var change in changes)
            {
                change.Binding.Runtime.Position=change.Position;
                change.Binding.Evidence=change.Evidence;
            }
        }
        internal static Point CaptureCurrentPointMetadata(Point point)=>new Point{Id=point.Id,Label=point.Label,Zone=point.Zone,Kind=point.Kind,
            Position=point.Position,Anchor=point.Anchor,Yaw=point.Yaw,Slot=point.Slot};
        static bool SameCurrentPointMetadata(Point point,Point expected)=>expected!=null&&point.Id==expected.Id&&point.Label==expected.Label&&point.Zone==expected.Zone&&
            point.Kind==expected.Kind&&point.Anchor.Equals(expected.Anchor)&&point.Yaw.Equals(expected.Yaw)&&point.Slot==expected.Slot;
        static bool FiniteStandingPosition(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);

        public IReadOnlyList<CurrentBinding> CurrentBindings {get;private set;} = Array.Empty<CurrentBinding>();
        public IReadOnlyList<Point> SourceAll {get;private set;}
        public string SourceSha256 {get;private set;}

        internal StationPoints WithCurrentBindings(IReadOnlyList<CurrentBinding> partition)
        {
            if(partition==null||partition.Count!=all.Count)throw new InvalidOperationException("Current point source partition is incomplete");
            var ids=new HashSet<string>();
            var view=new StationPoints { Zones=Zones, Escalators=Escalators, Elevators=Elevators,
                Platforms=Platforms, Train=Train, SourceAll=all.AsReadOnly(), SourceSha256=SourceSha256,
                CurrentBindings=partition,sourceVersion=sourceVersion,sourceMetadata=sourceMetadata };
            for(int i=0;i<all.Count;i++)
            {
                var row=partition[i];
                if(row==null||row.Source!=all[i]||!ids.Add(row.Source.Id)||string.IsNullOrWhiteSpace(row.Evidence))
                    throw new InvalidOperationException("Current point source partition identity/provenance differs");
                if(row.Bound)
                {
                    if(row.Status!="BOUND_MODEL_COMPUTED_WALKABLE"||row.Runtime.Id!=row.Source.Id||row.Runtime.Kind!=row.Source.Kind)
                        throw new InvalidOperationException("Invalid current point binding");
                    view.all.Add(row.Runtime);
                    if(!view.byKind.TryGetValue(row.Runtime.Kind,out var list))view.byKind[row.Runtime.Kind]=list=new List<Point>();
                    list.Add(row.Runtime);
                }
                else if(row.Status==CurrentBinding.CurrentSourceUnavailableStatus)
                {
                    if(!row.Evidence.StartsWith(CurrentBinding.CurrentSourceEmptyEvidence,StringComparison.Ordinal)&&
                        !row.Evidence.StartsWith(CurrentBinding.CurrentSourceFloorEvidence,StringComparison.Ordinal)&&
                        !row.Evidence.StartsWith(CurrentBinding.CurrentSourceFootprintEvidence,StringComparison.Ordinal))
                        throw new InvalidOperationException("Unavailable current point lacks complete source-support evidence: "+row.Source.Id);
                }
                else if(row.Status!="UNBOUND_PROVEN_NO_CURRENT_FLOOR")
                    throw new InvalidOperationException("Uncertain physical support cannot be silently discarded: "+row.Source.Id);
            }
            // Original modeled endpoints remain in SourceAll/CurrentBindings. Only
            // physically qualified current exits may become walking/evacuation targets.
            if(view.Of(PointKind.Exit).Count==0)
                throw new InvalidOperationException("No physically qualified current exit is available");
            return view;
        }

        internal StationPoints RoutingView(IReadOnlyList<Escalator> supplied,IReadOnlyList<Elevator> elevators)
        {
            var ids=new HashSet<string>(System.Linq.Enumerable.Select(supplied,x=>x.Entry.id));
            var elevatorIds=new HashSet<string>(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(elevators,e=>e.NavigationAvailable),e=>e.Entry.id));
            var view=new StationPoints { Zones=Zones, Platforms=Platforms, Train=Train,
                Elevators=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(Elevators,e=>elevatorIds.Contains(e.id))),
                Escalators=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(Escalators,e=>ids.Contains(e.id))),
                SourceAll=SourceAll,SourceSha256=SourceSha256,CurrentBindings=CurrentBindings,
                sourceVersion=sourceVersion,sourceMetadata=sourceMetadata,CurrentDestinations=CurrentDestinations,RequiredCurrentEndpoints=RequiredCurrentEndpoints };
            view.all.AddRange(all);
            foreach(var row in byKind)view.byKind.Add(row.Key,new List<Point>(row.Value));
            return view;
        }

        private readonly List<Point> all = new List<Point>();
        private readonly Dictionary<PointKind, List<Point>> byKind = new Dictionary<PointKind, List<Point>>();

        public IReadOnlyList<Point> All => all;
        public IReadOnlyList<ZoneEntry> Zones { get; private set; } = Array.Empty<ZoneEntry>();
        public IReadOnlyList<EscalatorEntry> Escalators { get; private set; } = Array.Empty<EscalatorEntry>();
        public IReadOnlyList<ElevatorEntry> Elevators { get; private set; } = Array.Empty<ElevatorEntry>();
        public IReadOnlyList<PlatformEntry> Platforms { get; private set; } = Array.Empty<PlatformEntry>();
        public TrainEntry Train { get; private set; }

        public static StationPoints Load(TextAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            var file = JsonUtility.FromJson<File>(asset.text);
            if (file == null || file.version != 2 || file.points == null || file.points.Length == 0)
                throw new InvalidOperationException("station-points.json 구조 오류 (version 2 필요)");
            var points = new StationPoints
            {
                sourceVersion=file.version,
                Zones = file.zones ?? Array.Empty<ZoneEntry>(),
                Escalators = file.escalators ?? Array.Empty<EscalatorEntry>(),
                Elevators = file.elevators ?? Array.Empty<ElevatorEntry>(),
                Platforms = file.platforms ?? Array.Empty<PlatformEntry>(),
                Train = file.train != null && file.train.cars != null && file.train.cars.Length > 0 ? file.train : null,
            };
            foreach (var entry in file.points)
            {
                if (!Enum.TryParse(entry.kind, out PointKind kind)) throw new InvalidOperationException("알 수 없는 지점 종류: " + entry.kind);
                var point = new Point { Id = entry.id, Label = entry.label, Zone = entry.zone ?? "", Kind = kind, Position = entry.position, Yaw = entry.yaw, Anchor = entry.anchor, Slot = string.IsNullOrEmpty(entry.slot) ? entry.id : entry.slot };
                points.all.Add(point);
                if (!points.byKind.TryGetValue(kind, out var list)) points.byKind[kind] = list = new List<Point>();
                list.Add(point);
            }
            if (points.Of(PointKind.Exit).Count == 0) throw new InvalidOperationException("출입 지점이 없습니다.");
            points.SourceAll=points.all.AsReadOnly();
            points.sourceMetadata=new Point[points.all.Count];
            for(int i=0;i<points.all.Count;i++)points.sourceMetadata[i]=CaptureCurrentPointMetadata(points.all[i]);
            using(var hash=System.Security.Cryptography.SHA256.Create())
                points.SourceSha256=BitConverter.ToString(hash.ComputeHash(asset.bytes)).Replace("-","").ToLowerInvariant();
            return points;
        }

        public IReadOnlyList<Point> Of(PointKind kind) => byKind.TryGetValue(kind, out var list) ? list : (IReadOnlyList<Point>)Array.Empty<Point>();

        public Point Nearest(PointKind kind, Vector3 from, Func<Point, bool> filter = null)
        {
            Point best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (var point in Of(kind))
            {
                if (filter != null && !filter(point)) continue;
                float d = (point.Position - from).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = point; }
            }
            return best;
        }

        /// <summary>The zone containing <paramref name="position"/> (highest priority wins), or null outside all zones.</summary>
        public ZoneEntry ZoneAt(Vector3 position)
        {
            ZoneEntry best = null;
            foreach (var zone in Zones)
            {
                if (position.x < zone.min.x || position.y < zone.min.y || position.z < zone.min.z) continue;
                if (position.x > zone.max.x || position.y > zone.max.y || position.z > zone.max.z) continue;
                if (best == null || zone.priority > best.priority) best = zone;
            }
            return best;
        }

        public ZoneEntry Zone(string id)
        {
            foreach (var zone in Zones) if (zone.id == id) return zone;
            return null;
        }

        /// <summary>Platform whose strip contains <paramref name="position"/> (ground level only).</summary>
        public PlatformEntry PlatformAt(Vector3 position, float margin = .3f)
        {
            if (position.y > 2.5f || position.y < -.6f) return null;
            foreach (var platform in Platforms)
            {
                var ab = platform.b - platform.a; ab.y = 0;
                var ap = position - platform.a; ap.y = 0;
                float t = Vector3.Dot(ap, ab) / ab.sqrMagnitude;
                if (t < -.02f || t > 1.02f) continue;
                var closest = platform.a + ab * Mathf.Clamp01(t);
                closest.y = position.y;
                if (Vector3.Distance(closest, position) <= platform.halfWidth + margin) return platform;
            }
            return null;
        }
    }
}
