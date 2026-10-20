using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>Current physical NavMesh graph; immutable historical graph is retained as source inventory.</summary>
    internal sealed class CurrentStationRouteBuilder
    {
        const float Cell=16f,Band=1f,Reach=32f,Vertical=4.5f,Detour=1.6f,Slack=8f;
        readonly StationWalkingRouteEntry[] walkingRoutes;
        readonly IReadOnlyList<StationWalkingRoute> walkingOwners;
        readonly IReadOnlyList<Escalator> escalatorOwners;
        sealed class Draft
        {
            public readonly List<Vector3> Positions=new List<Vector3>();
            public readonly List<string> Labels=new List<string>();
            public readonly List<(int from,int to,float length,string link)> Edges=new List<(int,int,float,string)>();
        }
        CurrentStationRouteBuilder(IReadOnlyList<StationWalkingRoute> walking,IReadOnlyList<Escalator> escalators)
        { walkingOwners=walking;escalatorOwners=escalators;walkingRoutes=walking.Select(w=>w.Entry).ToArray(); }

        internal static RouteGraph Build(StationPoints current,IReadOnlyList<Escalator> supplied,IReadOnlyList<Elevator> elevators,
            IReadOnlyList<StationWalkingRoute> walking,TextAsset original)
        {
            var points=current.RoutingView(supplied,elevators);
            var builder=new CurrentStationRouteBuilder(walking,supplied);
            var draft=builder.Sample(points);builder.Connect(draft,points);builder.Prune(draft);
            builder.AttachEveryCurrentPoint(draft,points);
            var file=new RouteGraph.File {version=RouteGraph.Version,
                positions=draft.Positions.SelectMany(p=>new[]{Mathf.RoundToInt(p.x*100),Mathf.RoundToInt(p.y*100),Mathf.RoundToInt(p.z*100)}).ToArray(),
                labels=draft.Labels.ToArray(),from=draft.Edges.Select(e=>e.from).ToArray(),to=draft.Edges.Select(e=>e.to).ToArray(),
                length=draft.Edges.Select(e=>Mathf.RoundToInt(e.length*100)).ToArray(),link=draft.Edges.Select(e=>e.link).ToArray(),
                report="Current source MODEL walkability; "+current.All.Count+" bound source points of "+current.SourceAll.Count+
                    "; "+current.CurrentBindings.Count(r=>!r.Bound)+" explicitly witnessed UNBOUND. Historical graph immutable; edges are current actual paths."};
            var graph=RouteGraph.Load(file,supplied,walking);graph.RetainSource(original);
            foreach(var node in graph.Nodes)
                if(!NavMesh.SamplePosition(node.Position,out _, .6f,NavMesh.AllAreas))
                    throw new InvalidOperationException("Derived current graph node is off the current NavMesh: "+node.Label);
            var fields=points.Of(PointKind.Exit).Select(e=>graph.Toward(graph.ByLabel(e.Id),RouteProfile.Evacuation,
                Array.Empty<(Vector3,float)>(),Time.time)).ToArray();
            foreach(var point in points.All)
                if(!fields.Any(f=>f.Reaches(graph.ByLabel(point.Id))))
                    throw new InvalidOperationException("Current physically qualified point has no actual foot-exit route: "+point.Id);
            return graph;
        }

        sealed class AttachmentTrace
        {
            readonly SortedDictionary<string,int> reasons=new SortedDictionary<string,int>();
            readonly List<string> details=new List<string>();
            public void Add(string reason,string detail=null)
            {
                reasons[reason]=reasons.TryGetValue(reason,out int count)?count+1:1;
                if(detail!=null&&details.Count<8)details.Add(detail);
            }
            public override string ToString()=>string.Join(",",reasons.Select(p=>p.Key+"="+p.Value))+"; "+string.Join(" | ",details);
        }

        // The coarse cell survey is useful, but is not a connected-component proof.
        // A failed point alone gets a complete directed native witness to an existing
        // exit-reachable draft seed. It is only a graph representation: no source pose, floor,
        // body clearance, native area, or runtime point qualification is changed.
        void AttachEveryCurrentPoint(Draft draft,StationPoints points)
        {
            int baseline=draft.Positions.Count;
            var path=new NavMeshPath();var corners=new Vector3[512];
            var exits=points.Of(PointKind.Exit).Select(e=>Find(draft,e.Id)).ToArray();
            if(exits.Length==0||exits.Any(i=>i<0))throw new InvalidOperationException("Current graph has no complete qualified exit seeds");
            var failures=new List<string>();
            foreach(var point in points.All)
            {
                if(point.Kind==PointKind.Exit)continue;
                var trace=new AttachmentTrace();
                var candidates=Enumerable.Range(0,baseline).Where(i=>Mathf.Abs(draft.Positions[i].y-point.Position.y)<2.5f)
                    .OrderBy(i=>(draft.Positions[i]-point.Position).sqrMagnitude).ToArray();
                int edgesBefore=draft.Edges.Count;
                int index=draft.Positions.Count;draft.Positions.Add(point.Position);draft.Labels.Add(point.Id);
                int attached=0;
                foreach(int other in candidates)
                {
                    if(Vector3.Distance(point.Position,draft.Positions[other])>Reach){trace.Add("coarse-outside-reach");break;}
                    int before=draft.Edges.Count;
                    TryEdge(draft,points,path,corners,index,other,true,trace);
                    if(draft.Edges.Count>before&&++attached==3)break;
                }
                var exitReachable=FootExitReachable(draft,exits);
                bool reachesExit=exitReachable[index];
                var seeds=Enumerable.Range(0,draft.Positions.Count).Where(i=>i!=index&&exitReachable[i]).ToArray();
                if(!reachesExit)trace.Add("coarse-no-foot-exit", "coarseAttachments="+attached);
                if(reachesExit||TryAttachWitness(draft,points,path,corners,index,seeds,trace))continue;
                // A failed transaction never publishes corner nodes or edges. This
                // source may have coarse edges to a dead end. Remove ALL its new
                // directed edges before removing/reusing the node index. No partial
                // graph can escape the aggregate or contaminate later source rows.
                draft.Edges.RemoveRange(edgesBefore,draft.Edges.Count-edgesBefore);
                draft.Positions.RemoveAt(index);draft.Labels.RemoveAt(index);
                int sourceIndex=points.SourceAll.ToList().FindIndex(p=>p.Id==point.Id);
                failures.Add(point.Id+" sourceIndex="+sourceIndex+" source="+(sourceIndex>=0?Exact(points.SourceAll[sourceIndex].Position):"UNKNOWN")+" current="+Exact(point.Position)+
                    " baselineCandidates="+candidates.Length+"; "+trace);
            }
            if(failures.Count>0)throw new InvalidOperationException("Current source points cannot attach by complete actual foot paths; failures="+
                failures.Count+" (all failed points; at most eight detailed witnesses per point):\n"+string.Join("\n",failures));
        }

        // Exactly the current draft's Evacuation admission: directions are real
        // arcs; elevator/missing/closed/barred owners do not establish exit reach.
        // Stopped but unbarred escalators retain the existing walked-stairs rule.
        bool HasFootExit(Draft draft,int source,int[] exits)=>FootExitReachable(draft,exits)[source];

        // A direct long native query can exhaust its search-node budget. Roots are
        // therefore ALL existing nodes with a certified directed Evacuation chain,
        // not only distant exits. Reverse graph reach is exact, not a proximity or
        // connected-component guess; closed/unknown/elevator owners cannot seed it.
        bool[] FootExitReachable(Draft draft,int[] exits)
        {
            var reverse=new List<int>[draft.Positions.Count];
            foreach(var edge in draft.Edges)
                if(edge.link.Length==0||(edge.link!=RouteGraph.ElevatorLink&&FootOwnerAvailable(edge.link)))
                    (reverse[edge.to]??=new List<int>()).Add(edge.from);
            var seen=new bool[draft.Positions.Count];var pending=new Stack<int>();
            foreach(int exit in exits){seen[exit]=true;pending.Push(exit);}
            while(pending.Count>0)
            {
                int node=pending.Pop();if(reverse[node]==null)continue;
                foreach(int prior in reverse[node])if(!seen[prior]){seen[prior]=true;pending.Push(prior);}
            }
            return seen;
        }

        bool TryAttachWitness(Draft draft,StationPoints points,NavMeshPath path,Vector3[] corners,int source,int[] seeds,AttachmentTrace trace)
        {
            var targets=seeds.Where(i=>i!=source).Distinct().OrderBy(i=>(draft.Positions[i]-draft.Positions[source]).sqrMagnitude).ThenBy(i=>i).ToArray();
            trace.Add("witness-foot-exit-seeds","certifiedDirectedSeeds="+targets.Length);
            foreach(int exit in targets)
            {
                int mask=NavMesh.AllAreas&~(1<<StationWorld.ElevatorArea);
                bool found=NavMesh.CalculatePath(draft.Positions[source],draft.Positions[exit],mask,path);
                trace.Add("witness-query-"+(found?"true":"false"));trace.Add("witness-status-"+path.status);
                if(!found||path.status!=NavMeshPathStatus.PathComplete)
                {trace.Add("witness-incomplete","seed="+exit+":"+draft.Labels[exit]+" status="+path.status+" query="+found+" source="+Exact(draft.Positions[source])+" target="+Exact(draft.Positions[exit]));continue;}
                int count=path.GetCornersNonAlloc(corners);
                if(count<2||count>=corners.Length){trace.Add("witness-empty-or-truncated","seed="+exit+":"+draft.Labels[exit]+" corners="+count);continue;}
                // Every subsequent native call reuses the same path/buffer. Keep the
                // complete witness first; no forward result implies a reverse edge.
                var witness=new Vector3[count];Array.Copy(corners,witness,count);
                trace.Add("witness-complete","seed="+exit+":"+draft.Labels[exit]+" corners="+count+" source="+Exact(draft.Positions[source])+" target="+Exact(draft.Positions[exit]));
                if(witness.Any(p=>!Finite(p))||Vector3.Distance(witness[0],draft.Positions[source])>.6f||
                    Vector3.Distance(witness[count-1],draft.Positions[exit])>.6f)
                {trace.Add("witness-endpoint-or-finite");continue;}
                var staged=Copy(draft);int current=source;bool complete=true;
                for(int segment=1;segment<count&&complete;segment++)
                {
                    if(!TryWitnessSeam(staged,points,witness[segment-1],witness[segment],out int seamFrom,out int seamTo,out bool seam,out string reason))
                    {trace.Add(reason,"seed="+exit+":"+draft.Labels[exit]+" segment="+(segment-1)+" from="+Exact(witness[segment-1])+" to="+Exact(witness[segment]));complete=false;break;}
                    if(seam)
                    {
                        complete=AppendGround(staged,points,path,corners,ref current,staged.Positions[seamFrom],seamFrom,trace);
                        if(complete)current=seamTo; // exact existing directed owner arc, never a generic substitute
                    }
                    else complete=AppendGround(staged,points,path,corners,ref current,witness[segment],-1,trace);
                }
                if(complete)complete=AppendGround(staged,points,path,corners,ref current,staged.Positions[exit],exit,trace);
                if(!complete)continue;
                Commit(draft,staged);return true;
            }
            return false;
        }

        // Every known seam is represented by its already registered exact owner
        // endpoints/arc. Ambiguous, closed, reversed one-way or missing owners fail.
        bool TryWitnessSeam(Draft draft,StationPoints points,Vector3 from,Vector3 to,out int a,out int b,out bool seam,out string reason)
        {
            a=b=-1;seam=false;reason="";var matches=new List<(int a,int b,string link)>();
            foreach(var entry in walkingRoutes)
                if(entry.Matches(from,to,out bool reverse))matches.Add((Find(draft,StationWalkingRoutes.LinkPrefix+entry.id+(reverse?":end":":start")),
                    Find(draft,StationWalkingRoutes.LinkPrefix+entry.id+(reverse?":start":":end")),StationWalkingRoutes.LinkPrefix+entry.id));
            foreach(var entry in points.Escalators)
            {
                bool forward=Near(from,entry.path[0])&&Near(to,entry.path[entry.path.Length-1]);
                bool reverse=Near(to,entry.path[0])&&Near(from,entry.path[entry.path.Length-1]);
                if(forward||reverse)matches.Add((Find(draft,entry.id+(forward?":start":":end")),Find(draft,entry.id+(forward?":end":":start")),entry.id));
            }
            foreach(var entry in points.Elevators)
                for(int i=0;i<entry.stops.Length;i++)for(int j=0;j<entry.stops.Length;j++)
                    if(i!=j&&Near(from,entry.stops[i].door)&&Near(to,entry.stops[j].door)){reason="witness-elevator-forbidden";return false;}
            if(matches.Count==0)return true;
            if(matches.Count!=1){reason="witness-ambiguous-owner";return false;}
            var match=matches[0];a=match.a;b=match.b;seam=true;
            if(a<0||b<0||!draft.Edges.Any(e=>e.from==match.a&&e.to==match.b&&e.link==match.link)||!FootOwnerAvailable(match.link))
            {reason="witness-owner-arc-unavailable";return false;}
            return true;
        }

        bool FootOwnerAvailable(string link)
        {
            if(link.StartsWith(StationWalkingRoutes.LinkPrefix,StringComparison.Ordinal))
                return walkingOwners.Count(w=>StationWalkingRoutes.LinkPrefix+w.Entry.id==link)==1&&
                    walkingOwners.Any(w=>StationWalkingRoutes.LinkPrefix+w.Entry.id==link&&!w.Closed);
            return escalatorOwners.Count(e=>e.Entry.id==link)==1&&escalatorOwners.Any(e=>e.Entry.id==link&&!e.Closed&&!e.EntryBarred);
        }

        bool AppendGround(Draft staged,StationPoints points,NavMeshPath path,Vector3[] corners,ref int current,Vector3 target,int existing,AttachmentTrace trace)
        {
            var from=staged.Positions[current];
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(from,target)/Cell));
            for(int step=1;step<=steps;step++)
            {
                var position=Vector3.Lerp(from,target,(float)step/steps);
                int next=step==steps&&existing>=0?existing:staged.Positions.Count;
                if(next==current)continue;
                if(!Finite(position)||!NavMesh.SamplePosition(position,out _, .6f,NavMesh.AllAreas))
                {trace.Add("witness-corner-off-nav");return false;}
                if(next==staged.Positions.Count){staged.Positions.Add(position);staged.Labels.Add("");}
                if(!GroundEdge(staged.Positions[current],staged.Positions[next],points,path,corners,out float length,out string reason))
                {trace.Add(reason,"from="+Exact(staged.Positions[current])+" to="+Exact(staged.Positions[next]));return false;}
                staged.Edges.Add((current,next,length,""));
                // Reverse is independently queried/costed. Failure grants no reverse.
                if(GroundEdge(staged.Positions[next],staged.Positions[current],points,path,corners,out float back,out _))staged.Edges.Add((next,current,back,""));
                current=next;
            }
            return true;
        }

        bool GroundEdge(Vector3 from,Vector3 to,StationPoints points,NavMeshPath path,Vector3[] corners,out float length,out string reason)
        {
            length=0;reason="";
            if(Vector3.Distance(from,to)>Reach){reason="witness-edge-reach";return false;}
            if(!Walk(from,to,points,path,corners,true,out length,out string link)||link.Length>0){reason="witness-edge-walk-or-owner";return false;}
            int count=path.GetCornersNonAlloc(corners);
            if(!Finite(from)||!Finite(to)||float.IsNaN(length)||float.IsInfinity(length)||
                corners.Take(count).Any(p=>!Finite(p))||Vector3.Distance(corners[0],from)>.6f||Vector3.Distance(corners[count-1],to)>.6f)
            {reason="witness-ground-endpoint-or-finite";return false;}
            for(int i=1;i<count;i++)
            {
                if(!TryWitnessSeam(new Draft(),points,corners[i-1],corners[i],out _,out _,out bool seam,out _)||seam)
                {reason="witness-ground-crosses-owner";return false;}
                // A complete native path can traverse off-mesh links. A generic
                // ground arc additionally needs uninterrupted walk polygons; true
                // (including an outer edge/mask zero) is never waived or snapped.
                if(NavMesh.Raycast(corners[i-1],corners[i],out _,NavMesh.AllAreas&~(1<<StationWorld.ElevatorArea)))
                {reason="witness-ground-boundary-or-unknown-link";return false;}
            }
            return true;
        }

        static Draft Copy(Draft source)
        {
            var result=new Draft();result.Positions.AddRange(source.Positions);result.Labels.AddRange(source.Labels);result.Edges.AddRange(source.Edges);return result;
        }
        static void Commit(Draft target,Draft source)
        {
            target.Positions.Clear();target.Positions.AddRange(source.Positions);target.Labels.Clear();target.Labels.AddRange(source.Labels);
            target.Edges.Clear();target.Edges.AddRange(source.Edges);
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
        static string Exact(Vector3 p)=>"["+p.x.ToString("R",CultureInfo.InvariantCulture)+","+p.y.ToString("R",CultureInfo.InvariantCulture)+","+p.z.ToString("R",CultureInfo.InvariantCulture)+"]";

        private Draft Sample(StationPoints points)
        {
            var mesh = NavMesh.CalculateTriangulation();
            var best = new SortedDictionary<(int band, int x, int z), (Vector3 point, float distance)>();
            float limit = Cell * .75f;
            for (int t = 0; t + 2 < mesh.indices.Length; t += 3)
            {
                var a = mesh.vertices[mesh.indices[t]];
                var b = mesh.vertices[mesh.indices[t + 1]];
                var c = mesh.vertices[mesh.indices[t + 2]];
                if (Mathf.Abs((b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z)) < 1e-3f) continue;
                int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x) / Cell), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x, c.x) / Cell);
                int z0 = Mathf.FloorToInt(Mathf.Min(a.z, b.z, c.z) / Cell), z1 = Mathf.FloorToInt(Mathf.Max(a.z, b.z, c.z) / Cell);
                for (int cx = x0; cx <= x1; cx++)
                    for (int cz = z0; cz <= z1; cz++)
                    {
                        var centre = new Vector2((cx + .5f) * Cell, (cz + .5f) * Cell);
                        var point = ClosestOnTriangle(centre, a, b, c);
                        float distance = (new Vector2(point.x, point.z) - centre).magnitude;
                        if (distance > limit) continue;
                        var key = (Mathf.RoundToInt(point.y / Band), cx, cz);
                        if (!best.TryGetValue(key, out var old) || distance < old.distance) best[key] = (point, distance);
                    }
            }
            var floor = new List<Vector3>();
            foreach (var pair in best)
                if (NavMesh.SamplePosition(pair.Value.point, out var hit, 1f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - pair.Value.point.y) < .8f) floor.Add(hit.position);

            var special = new List<(Vector3 position, string label)>();
            foreach (var route in walkingRoutes)
            {
                special.Add((route.Start, StationWalkingRoutes.LinkPrefix + route.id + ":start"));
                special.Add((route.End, StationWalkingRoutes.LinkPrefix + route.id + ":end"));
            }
            foreach (var exit in points.Of(PointKind.Exit)) special.Add((exit.Position, exit.Id));
            foreach (var escalator in points.Escalators)
            {
                special.Add((escalator.path[0], escalator.id + ":start"));
                special.Add((escalator.path[escalator.path.Length - 1], escalator.id + ":end"));
                // 계단 양 끝에도 노드를 둔다: 에스컬레이터 끝이 계단과 이어지지 않은 navmesh 조각 위에 있으면(5·6 승강장 북쪽 등) 계단을 이을 노드가 없어 그 층 오르내림이 수백 m 돌아가는 길이 된다.
                if (escalator.stairs)
                {
                    special.Add((escalator.stairsTop, escalator.id + ":stairs-top"));
                    special.Add((escalator.stairsBottom, escalator.id + ":stairs-bottom"));
                }
            }
            foreach (var elevator in points.Elevators)
                for (int stop = 0; stop < elevator.stops.Length; stop++) special.Add((elevator.stops[stop].door, elevator.id + ":" + stop));
            var placed = new List<(Vector3 position, string label)>();
            foreach (var (position, label) in special)
            {
                bool measured = label.StartsWith(StationWalkingRoutes.LinkPrefix, StringComparison.Ordinal);
                if (!NavMesh.SamplePosition(position, out var hit, measured ? .12f : 3f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - position.y) > (measured ? .15f : 1.5f)) throw new InvalidOperationException("이 지점이 navmesh 위에 없습니다: " + label + " " + position);
                placed.Add((hit.position, label));
            }
            // 출입구·링크 끝 5 m 안의 바닥 노드는 그 노드에 자리를 내준다.
            floor.RemoveAll(f => placed.Any(p => Mathf.Abs(p.position.y - f.y) < 2.2f && new Vector2(p.position.x - f.x, p.position.z - f.z).sqrMagnitude < 25f));
            var draft = new Draft();
            foreach (var f in floor) { draft.Positions.Add(f); draft.Labels.Add(""); }
            foreach (var (position, label) in placed) { draft.Positions.Add(position); draft.Labels.Add(label); }
            return draft;
        }

        // ── 변 ──────────────────────────────────────────────────────────────

        private void Connect(Draft draft, StationPoints points)
        {
            var grid = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < draft.Positions.Count; i++)
            {
                var key = (Mathf.FloorToInt(draft.Positions[i].x / Reach), Mathf.FloorToInt(draft.Positions[i].z / Reach));
                if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                list.Add(i);
            }
            var path = new NavMeshPath();
            var corners = new Vector3[512];
            foreach (var route in walkingRoutes)
            {
                int a = Find(draft, StationWalkingRoutes.LinkPrefix + route.id + ":start"), b = Find(draft, StationWalkingRoutes.LinkPrefix + route.id + ":end");
                float length = route.Length + Vector3.Distance(draft.Positions[a], route.Start) + Vector3.Distance(route.End, draft.Positions[b]);
                if(!NavMesh.CalculatePath(draft.Positions[a],draft.Positions[b],NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete||
                   !NavMesh.CalculatePath(draft.Positions[b],draft.Positions[a],NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("Physically verified walking link does not attach to current navigation: "+route.id);
                draft.Edges.Add((a, b, length, StationWalkingRoutes.LinkPrefix + route.id));
                draft.Edges.Add((b, a, length, StationWalkingRoutes.LinkPrefix + route.id));
            }
            for (int a = 0; a < draft.Positions.Count; a++)
            {
                var from = draft.Positions[a];
                int cx = Mathf.FloorToInt(from.x / Reach), cz = Mathf.FloorToInt(from.z / Reach);
                for (int x = cx - 1; x <= cx + 1; x++)
                    for (int z = cz - 1; z <= cz + 1; z++)
                    {
                        if (!grid.TryGetValue((x, z), out var list)) continue;
                        foreach (int b in list)
                        {
                            if (b <= a) continue;
                            var to = draft.Positions[b];
                            float flat = new Vector2(to.x - from.x, to.z - from.z).magnitude;
                            if (flat > Reach || Mathf.Abs(to.y - from.y) > Vertical) continue;
                            TryEdge(draft, points, path, corners, a, b, true);
                        }
                    }
            }
            // 승강 설비의 두 끝(계단의 두 끝도)은 층이 달라 위 조건으로는 만나지 않는다: 링크와 계단 자체를 잇는다.
            foreach (var escalator in points.Escalators)
            {
                TryEdge(draft, points, path, corners, Find(draft, escalator.id + ":start"), Find(draft, escalator.id + ":end"), false);
                if (escalator.stairs) TryEdge(draft, points, path, corners, Find(draft, escalator.id + ":stairs-top"), Find(draft, escalator.id + ":stairs-bottom"), false);
            }
            foreach (var elevator in points.Elevators)
                for (int i = 0; i < elevator.stops.Length; i++)
                    for (int j = i + 1; j < elevator.stops.Length; j++)
                        TryEdge(draft, points, path, corners, Find(draft, elevator.id + ":" + i), Find(draft, elevator.id + ":" + j), false);
        }

        private int Find(Draft draft, string label) => draft.Labels.IndexOf(label);

        /// <summary>Adds a→b when the navmesh walks there without a big detour, and b→a: the same walk when it uses no one-way link, else the way back is walked separately.</summary>
        private void TryEdge(Draft draft, StationPoints points, NavMeshPath path, Vector3[] corners, int a, int b, bool ratio,AttachmentTrace trace=null)
        {
            if (!Walk(draft.Positions[a], draft.Positions[b], points, path, corners, ratio, out float length, out string link,trace)) return;
            draft.Edges.Add((a, b, length, link));
            if (Walk(draft.Positions[b], draft.Positions[a], points, path, corners, ratio, out float back, out string backLink)) draft.Edges.Add((b, a, back, backLink));
        }

        private bool Walk(Vector3 from, Vector3 to, StationPoints points, NavMeshPath path, Vector3[] corners, bool ratio, out float length, out string link,AttachmentTrace trace=null)
        {
            length = 0;
            link = "";
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) {trace?.Add("coarse-native-incomplete");return false;}
            int count = path.GetCornersNonAlloc(corners);
            if(count==0||count>=corners.Length){trace?.Add("coarse-empty-or-truncated");return false;}
            for (int i = 1; i < count; i++)
            {
                // A measured walk is its dedicated endpoint↔endpoint graph arc.
                // Excluding generic crossings prevents a composite path from losing
                // a second walking owner or an existing transport-link closure.
                if (walkingRoutes.Any(w => w.Matches(corners[i - 1], corners[i], out _))) {trace?.Add("coarse-dedicated-walking-seam");return false;}
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }
            if (ratio && length > Vector3.Distance(from, to) * Detour + Slack) {trace?.Add("coarse-detour");return false;}
            for (int i = 0; i + 1 < count; i++)
            {
                foreach (var escalator in points.Escalators)
                    if (Near(corners[i], escalator.path[0]) && Near(corners[i + 1], escalator.path[escalator.path.Length - 1]))
                    { if(link.Length>0&&link!=escalator.id){trace?.Add("coarse-composite-owner");return false;}link=escalator.id; }
                foreach (var elevator in points.Elevators)
                    for (int stop = 0; stop < elevator.stops.Length; stop++)
                        for (int other = 0; other < elevator.stops.Length; other++)
                            if (stop != other && Near(corners[i], elevator.stops[stop].door) && Near(corners[i + 1], elevator.stops[other].door))
                            { if(link.Length>0&&link!=RouteGraph.ElevatorLink){trace?.Add("coarse-composite-owner");return false;}link=RouteGraph.ElevatorLink; }
            }
            return true;
        }

        private bool Near(Vector3 a, Vector3 b) => Mathf.Abs(a.y - b.y) < .9f && (a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z) < .8f;

        /// <summary>
        /// Drops a walk that two hops through another node cover about as well (within a tenth), longest walks first and each judged
        /// against the walks still kept, so what is dropped is always still covered: routes lose nothing and the graph stays a few
        /// edges per node.
        /// </summary>
        private void Prune(Draft draft)
        {
            var kept = new Dictionary<(int, int), float>();
            var around = new Dictionary<int, List<int>>();
            foreach (var (from, to, length, _) in draft.Edges)
            {
                kept[(from, to)] = length;
                if (!around.TryGetValue(from, out var list)) around[from] = list = new List<int>();
                list.Add(to);
            }
            var dropped = new HashSet<int>();
            foreach (int i in Enumerable.Range(0, draft.Edges.Count).OrderByDescending(i => draft.Edges[i].length))
            {
                var (from, to, length, link) = draft.Edges[i];
                if (link.Length > 0) continue;
                foreach (int c in around[from])
                {
                    if (c == to || !kept.TryGetValue((c, to), out float second) || kept[(from, c)] + second > length * 1.1f + .5f) continue;
                    dropped.Add(i);
                    kept.Remove((from, to));
                    around[from].Remove(to);
                    break;
                }
            }
            var remaining = draft.Edges.Where((e, i) => !dropped.Contains(i)).ToList();
            draft.Edges.Clear();
            draft.Edges.AddRange(remaining);
        }

        /// <summary>
        /// Removes tiny pieces of floor the walk network does not join (a pocket of navmesh between a bench and a wall, a sliver at an
        /// escalator well): a node there is the nearest node of the places around it and leads nowhere. A piece of three nodes or fewer
        /// holding no exit, escalator or elevator end goes; larger pieces (track beds, the street) stay and are reported.
        /// </summary>
        // ── 검사와 기록 ──────────────────────────────────────────────────────

        /// <summary>
        /// Every place of the station must reach an exit through the graph whenever the navmesh itself reaches one (a graph that
        /// disagrees is refused); the returned account lists what cannot reach which exit at all.
        /// </summary>
        private bool[] ReachBack(Draft draft, int goal, bool lifts)
        {
            var reverse = new List<int>[draft.Positions.Count];
            foreach (var (from, to, _, link) in draft.Edges)
                if (lifts || link != RouteGraph.ElevatorLink) (reverse[to] ??= new List<int>()).Add(from);
            var seen = new bool[draft.Positions.Count];
            var stack = new Stack<int>();
            seen[goal] = true;
            stack.Push(goal);
            while (stack.Count > 0)
            {
                int node = stack.Pop();
                if (reverse[node] == null) continue;
                foreach (int from in reverse[node]) if (!seen[from]) { seen[from] = true; stack.Push(from); }
            }
            return seen;
        }

        /// <summary>The session's rule for the node a place belongs to: the nearest on the place's own storey (else the nearest of all); when that one leads nowhere it tries the next (<paramref name="count"/> in all).</summary>
        private List<int> NearestNodes(Draft draft, Vector3 position, int count) =>
            Enumerable.Range(0, draft.Positions.Count)
                .Select(i => { var d = draft.Positions[i] - position; return (index: i, squared: d.x * d.x + d.z * d.z + 4f * d.y * d.y, sameStorey: Mathf.Abs(d.y) < 2.5f); })
                .OrderBy(n => n.sameStorey ? 0 : 1).ThenBy(n => n.squared)
                .Take(count).Select(n => n.index).ToList();

        private string Format(Vector3 v) => "(" + v.x.ToString("0.#") + ", " + v.y.ToString("0.#") + ", " + v.z.ToString("0.#") + ")";

        private Vector3 ClosestOnTriangle(Vector2 p, Vector3 a3, Vector3 b3, Vector3 c3)
        {
            var a = new Vector2(a3.x, a3.z);
            var b = new Vector2(b3.x, b3.z);
            var c = new Vector2(c3.x, c3.z);
            var ab = b - a;
            var ac = c - a;
            var ap = p - a;
            float d1 = Vector2.Dot(ab, ap), d2 = Vector2.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) return a3;
            var bp = p - b;
            float d3 = Vector2.Dot(ab, bp), d4 = Vector2.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3) return b3;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0) return a3 + (b3 - a3) * (d1 / (d1 - d3));
            var cp = p - c;
            float d5 = Vector2.Dot(ab, cp), d6 = Vector2.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6) return c3;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0) return a3 + (c3 - a3) * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b3 + (c3 - b3) * ((d4 - d3) / (d4 - d3 + (d5 - d6)));
            float denominator = 1f / (va + vb + vc);
            return a3 + (b3 - a3) * (vb * denominator) + (c3 - a3) * (vc * denominator);
        }
    }
}
