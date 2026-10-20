// Draft: integrate only after every promoted route passes exact physical + graph probes.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>A measured walking path on unchanged native floors; no transport geometry.</summary>
    [Serializable]
    public sealed class StationWalkingRouteEntry
    {
        public string id, label, proof;
        public bool physicalVerified;
        public Vector3[] path;
        // Exact collider paths from the proof packet, not broad name exemptions.
        public string[] floorOwners;
        IReadOnlyList<Vector3> sourcePath;
        public IReadOnlyList<Vector3> SourcePath=>sourcePath??Array.AsReadOnly((Vector3[])path.Clone());
        public string CurrentSourceProof {get;private set;}
        public Vector3 Start => path[0];
        public Vector3 End => path[path.Length - 1];
        public float Length
        {
            get { float n = 0; for (int i = 1; i < path.Length; i++) n += Vector3.Distance(path[i - 1], path[i]); return n; }
        }
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(proof) || !physicalVerified || path == null || path.Length < 3 || floorOwners == null || floorOwners.Length == 0)
                throw new InvalidOperationException("Unverified native walking route: " + id);
            foreach (var owner in floorOwners) if (string.IsNullOrWhiteSpace(owner)) throw new InvalidOperationException("Blank floor authority: " + id);
            foreach (var p in path)
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z) || Mathf.Abs(p.y - Start.y) > .4f)
                    throw new InvalidOperationException("Invalid same-floor route point: " + id);
            if (Vector3.Distance(Start, End) < .1f || Length > 40f) throw new InvalidOperationException("Invalid native walking route bounds: " + id);
        }
        internal StationWalkingRouteEntry WithCurrentFloorOwners(IEnumerable<string> exposedCurrentOwners)
        {
            Validate();
            if(exposedCurrentOwners==null)throw new ArgumentNullException(nameof(exposedCurrentOwners));
            var owners=new SortedSet<string>(floorOwners,StringComparer.Ordinal);
            foreach(var owner in exposedCurrentOwners)
            {
                if(string.IsNullOrWhiteSpace(owner))throw new InvalidOperationException("Blank current exposed floor authority: "+id);
                owners.Add(owner);
            }
            return new StationWalkingRouteEntry {id=id,label=label,proof=proof,physicalVerified=physicalVerified,
                path=(Vector3[])path.Clone(),floorOwners=new List<string>(owners).ToArray(),sourcePath=sourcePath,CurrentSourceProof=CurrentSourceProof};
        }
        internal StationWalkingRouteEntry WithCurrentPublicPath(Vector3[] publicPath,string evidence)
        {
            Validate();
            if(publicPath==null||string.IsNullOrWhiteSpace(evidence))
                throw new InvalidOperationException("Current public walking path/provenance is incomplete: "+id);
            var retained=SourcePath;var original=new Vector3[retained.Count];for(int i=0;i<original.Length;i++)original[i]=retained[i];
            var entry=new StationWalkingRouteEntry {id=id,label=label,proof=proof,physicalVerified=physicalVerified,
                path=(Vector3[])publicPath.Clone(),floorOwners=(string[])floorOwners.Clone(),
                sourcePath=Array.AsReadOnly(original),CurrentSourceProof=evidence};
            entry.Validate();return entry;
        }
        public NavMeshLinkData LinkData(int agentType = 0) => new NavMeshLinkData
        {
            startPosition = Start, endPosition = End, width = 0, bidirectional = true,
            area = 0, agentTypeID = agentType,
            // NavMesh search charges the physical route length, even though link corners
            // contain only its endpoints. The route graph must use the same length.
            costModifier = Mathf.Max(1f, Length / Vector3.Distance(Start, End))
        };
        public bool Matches(Vector3 a, Vector3 b, out bool reverse)
        {
            reverse = Vector3.Distance(a, End) < Vector3.Distance(a, Start);
            return Vector3.Distance(a, reverse ? End : Start) < .6f && Vector3.Distance(b, reverse ? Start : End) < .6f;
        }
        public Vector3[] Direction(bool reverse)
        {
            var result = (Vector3[])path.Clone();
            if (reverse) Array.Reverse(result);
            return result;
        }
    }

    public enum StationWalkingRouteBindingStatus { BOUND_CURRENT_SOURCE, UNBOUND_SOURCE_OBSTRUCTED }

    /// <summary>Complete historical-candidate audit; unbound never means the physical passage is absent.</summary>
    public sealed class StationWalkingRouteBinding
    {
        public string SourceId {get;}
        public IReadOnlyList<Vector3> SourcePath {get;}
        public StationWalkingRouteBindingStatus Status {get;}
        public StationWalkingRouteEntry CurrentEntry {get;}
        public string SourceEvidence {get;}
        public string AlternativeEvidence {get;}
        public bool Qualified=>Status==StationWalkingRouteBindingStatus.BOUND_CURRENT_SOURCE;

        internal StationWalkingRouteBinding(StationWalkingRouteEntry source,StationWalkingRouteEntry current):this(source,current,null,null) { }
        internal StationWalkingRouteBinding(StationWalkingRouteEntry source,
            CurrentStationNavigation.CurrentWalkingClearanceFailure obstruction,CurrentStationNavigation.CurrentWalkingSurfaceRejection alternative):
            this(source,null,RequireStaticSourceObstruction(obstruction),alternative?.Message??throw new ArgumentNullException(nameof(alternative))) { }
        static string RequireStaticSourceObstruction(CurrentStationNavigation.CurrentWalkingClearanceFailure obstruction)
        {
            if(obstruction==null||!obstruction.StaticCurrentSource)throw new InvalidOperationException("Unknown/foreign/dynamic walking obstruction cannot unbind a source candidate");
            return obstruction.Message;
        }
        private StationWalkingRouteBinding(StationWalkingRouteEntry source,StationWalkingRouteEntry current,
            string sourceObstruction,string rejectedAlternative)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));source.Validate();SourceId=source.id;
            var raw=source.SourcePath;var copy=new Vector3[raw.Count];for(int i=0;i<copy.Length;i++)copy[i]=raw[i];SourcePath=Array.AsReadOnly(copy);
            if(current!=null)
            {
                current.Validate();
                if(current.id!=SourceId||current.SourcePath.Count!=copy.Length||string.IsNullOrWhiteSpace(current.CurrentSourceProof))
                    throw new InvalidOperationException("Incomplete current walking source correspondence: "+SourceId);
                for(int i=0;i<copy.Length;i++)if(!copy[i].Equals(current.SourcePath[i]))
                    throw new InvalidOperationException("Current walking binding changed raw source path: "+SourceId);
                if(sourceObstruction!=null||rejectedAlternative!=null)throw new InvalidOperationException("Ambiguous current walking binding: "+SourceId);
                Status=StationWalkingRouteBindingStatus.BOUND_CURRENT_SOURCE;CurrentEntry=current;SourceEvidence=current.CurrentSourceProof;
                AlternativeEvidence="NOT_NEEDED_OR_FULLY_QUALIFIED";
            }
            else
            {
                if(string.IsNullOrWhiteSpace(sourceObstruction)||string.IsNullOrWhiteSpace(rejectedAlternative))
                    throw new InvalidOperationException("Unbound walking candidate lacks positive source obstruction and typed alternative rejection: "+SourceId);
                Status=StationWalkingRouteBindingStatus.UNBOUND_SOURCE_OBSTRUCTED;SourceEvidence=sourceObstruction;
                AlternativeEvidence="UNQUALIFIED_NAV_SURFACE_BOUNDARY; "+rejectedAlternative+
                    "; this historical candidate has no qualified current route; alternative passage existence remains unqualified, not absent";
            }
        }

        internal static void RequireCompletePartition(IReadOnlyList<StationWalkingRouteEntry> raw,IReadOnlyList<StationWalkingRouteBinding> bindings)
        {
            if(raw==null||bindings==null||raw.Count!=bindings.Count)throw new InvalidOperationException("Incomplete raw/current walking-candidate partition");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            for(int i=0;i<raw.Count;i++)
            {
                raw[i].Validate();var row=bindings[i];
                if(row==null||row.SourceId!=raw[i].id||!ids.Add(row.SourceId)||row.SourcePath.Count!=raw[i].SourcePath.Count)
                    throw new InvalidOperationException("Walking-candidate identity/order/coverage mismatch");
                for(int j=0;j<row.SourcePath.Count;j++)if(!row.SourcePath[j].Equals(raw[i].SourcePath[j]))
                    throw new InvalidOperationException("Walking-candidate raw source pose changed: "+row.SourceId);
                if(row.Status!=StationWalkingRouteBindingStatus.BOUND_CURRENT_SOURCE&&row.Status!=StationWalkingRouteBindingStatus.UNBOUND_SOURCE_OBSTRUCTED||
                    row.Qualified!=(row.CurrentEntry!=null))throw new InvalidOperationException("Walking-candidate current availability mismatch: "+row.SourceId);
            }
        }
    }

    public static class StationWalkingRoutes
    {
        public const string Resource = "StationWalkingRoutes", LinkPrefix = "walk:";
        [Serializable] public sealed class File { public int version; public StationWalkingRouteEntry[] routes; }
        public static StationWalkingRouteEntry[] Load()
        {
            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null) return Array.Empty<StationWalkingRouteEntry>();
            var f = JsonUtility.FromJson<File>(asset.text);
            if (f == null || f.version != 1 || f.routes == null) throw new InvalidOperationException("Invalid " + Resource);
            var ids = new HashSet<string>();
            foreach (var r in f.routes) { if (r == null) throw new InvalidOperationException("Null native walking route"); r.Validate(); if (!ids.Add(r.id)) throw new InvalidOperationException("Duplicate walking route: " + r.id); }
            return f.routes;
        }
        // Shared by StationRouteBuilder.AddLinks and StationNavigationBuilder.BuildPoints.
        public static void AddTemporaryLinks(List<NavMeshLinkInstance> into)
        {
            foreach (var r in Load())
            {
                var l = NavMesh.AddLink(r.LinkData());
                if (!NavMesh.IsLinkValid(l)) throw new InvalidOperationException("Native walking endpoints are not attached: " + r.id);
                into.Add(l);
            }
        }
    }

    /// <summary>Optional link outcome. PersonBody must never complete/warp on failure.</summary>
    public interface IPhysicalStationLink : IStationLink
    {
        bool Succeeded(PersonBody body);
    }

    /// <summary>Body-owned cleanup also runs on disable/destroy if an iterator is cancelled.</summary>
    public sealed class NativeLandingWalkHandle : MonoBehaviour
    {
        public bool Cancelled { get; private set; }
        public string FailurePhase { get; private set; } = "";
        public Vector3 FailurePosition { get; private set; }
        GameObject actor;
        CharacterController controller;
        public void Bind(GameObject value, CharacterController cc) { Release(); actor = value; controller = cc; Cancelled = false; FailurePhase = ""; FailurePosition = value.transform.position; }
        public void Fail(string phase, Vector3 position) { FailurePhase = phase; FailurePosition = position; }
        public void Release()
        {
            if (controller != null) controller.enabled = false;
            if (actor != null) UnityEngine.Object.Destroy(actor);
            actor = null; controller = null;
        }
        public void Cancel() { Cancelled = true; if (actor != null) Fail("cancelled", actor.transform.position); Release(); }
        void OnDisable() => Cancel();
        void OnDestroy() => Cancel();
    }

    public sealed class StationWalkingRoute : MonoBehaviour, IPhysicalStationLink
    {
        public StationWalkingRouteEntry Entry { get; private set; }
        public string Label => Entry.label;
        public bool Closed { get; private set; }
        public event Action Changed;
        NavMeshLinkInstance link;
        readonly HashSet<PersonBody> completed = new HashSet<PersonBody>();
        public void Setup(StationWalkingRouteEntry entry)
        {
            entry.Validate(); Entry = entry; link = NavMesh.AddLink(entry.LinkData());
            if (!NavMesh.IsLinkValid(link)) throw new InvalidOperationException("Walking route did not attach: " + entry.id);
            NavMesh.SetLinkOwner(link, this);
        }
        public void Close() { if (Closed) return; Closed = true; if (NavMesh.IsLinkValid(link)) NavMesh.RemoveLink(link); Changed?.Invoke(); }
        void OnDestroy() { Close(); Changed = null; }
        public bool Succeeded(PersonBody body) => completed.Remove(body);
        public IEnumerator Traverse(PersonBody body, OffMeshLinkData data)
        {
            completed.Remove(body);
            if (Closed || !Entry.Matches(data.startPos, data.endPos, out bool reverse)) yield break;
            bool pass = false;
            yield return NativeLandingWalker.Walk(body, Entry, Entry.Direction(reverse), data.endPos, ok => pass = ok);
            if (pass) completed.Add(body);
            else Close();
        }
    }

    /// <summary>Moves through the actual scene with a .3m/1.72m CharacterController.</summary>
    public static class NativeLandingWalker
    {
        public const float Radius = .3f, Height = 1.72f, MaxStep = .05f;
        public static string Path(Transform t)
        {
            string p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; } return p;
        }
        public static bool Floor(Vector3 staged, StationWalkingRouteEntry route, GameObject actor, out float height) =>
            Floor(staged,route,actor,null,out height);

        // Current-source binding may recognize only an actual exposed collider from
        // the independently installed required-floor registry. The ordinary public
        // walking policy continues to use its exact route owner paths alone.
        internal static bool Floor(Vector3 staged, StationWalkingRouteEntry route, GameObject actor,
            Func<Collider,bool> currentSourceOwner,out float height)
            =>Floor(staged,route,actor,currentSourceOwner,null,out height);

        internal static bool Floor(Vector3 staged,StationWalkingRouteEntry route,GameObject actor,
            Func<Collider,bool> currentSourceOwner,Func<Collider,bool> currentSourceEligible,out float height)
        {
            height = float.NegativeInfinity;
            float lowest = float.PositiveInfinity;
            for (int i = -1; i < 8; i++)
            {
                Vector3 offset = i < 0 ? Vector3.zero : new Vector3(Mathf.Cos(i * Mathf.PI / 4), 0, Mathf.Sin(i * Mathf.PI / 4)) * .24f;
                // First exposed collider hit is authority; a buried floor below an
                // obstruction must not certify walking support.
                var hits = Physics.RaycastAll(staged + offset + Vector3.up * .75f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore);
                RaycastHit top = default; bool found = false;
                foreach (var hit in hits)
                    if (hit.collider.gameObject != actor && hit.collider.GetComponentInParent<PersonBody>() == null && (!found || hit.distance < top.distance)) { top = hit; found = true; }
                if (!found || currentSourceEligible!=null&&!currentSourceEligible(top.collider) || top.normal.y < .9f) return false;
                if (Array.IndexOf(route.floorOwners, Path(top.collider.transform)) < 0 &&
                    (currentSourceOwner == null || !currentSourceOwner(top.collider))) return false;
                height = Mathf.Max(height, top.point.y);
                lowest = Mathf.Min(lowest, top.point.y);
            }
            // The native s1 landing edge spans the observed 7.00 -> 6.65
            // stair step. Compare that spread independently of agent/nav skin
            // height; every centre/outer ray still needs its exposed source floor.
            return !float.IsInfinity(height) && height - lowest <= .36f && Mathf.Abs(height - staged.y) < .35f;
        }
        public static IEnumerator Walk(PersonBody body, StationWalkingRouteEntry route, Vector3[] points, Vector3 attachEnd, Action<bool> finished)
        {
            GameObject actor = null; CharacterController cc = null; NativeLandingWalkHandle handle = null; bool pass = false;
            try
            {
                handle = body.GetComponent<NativeLandingWalkHandle>() ?? body.gameObject.AddComponent<NativeLandingWalkHandle>();
                actor = new GameObject("Actual native landing walking controller");
                actor.layer = body.gameObject.layer;
                cc = actor.AddComponent<CharacterController>(); cc.enabled = false;
                handle.Bind(actor, cc);
                cc.height = Height; cc.center = Vector3.up * Height * .5f; cc.radius = Radius;
                cc.stepOffset = .3f; cc.skinWidth = .02f; cc.minMoveDistance = 0;
                actor.transform.position = body.transform.position; body.Agent.enabled = false; cc.enabled = true;
                // Apply self-pair ignores after collider activation. Actual other
                // people and all original scene colliders remain collidable.
                foreach (var own in body.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(cc, own);
                // Both NavMesh attachment connectors are physically walked. No start
                // or end snap can conceal a rail/hole behind SamplePosition.
                var targets = new List<Vector3>(points); targets.Add(attachEnd);
                foreach (var target in targets)
                {
                    float until = Time.time + 4f + Vector3.Distance(actor.transform.position, target) / Mathf.Max(.1f, body.WalkSpeed) * 3f;
                    while (true)
                    {
                        if (handle.Cancelled || body == null) yield break;
                        Vector3 delta = target - actor.transform.position; delta.y = 0;
                        if (delta.magnitude <= .035f) break;
                        if (Time.time > until) { handle.Fail("timeout", actor.transform.position); yield break; }
                        var next = actor.transform.position + delta.normalized * Mathf.Min(delta.magnitude, Mathf.Min(MaxStep, body.WalkSpeed * Time.deltaTime));
                        next.y = target.y;
                        if (!Floor(next, route, actor, out float y)) { handle.Fail("staged_floor", next); yield break; }
                        Vector3 motion = next - actor.transform.position; motion.y = -.08f;
                        cc.Move(motion); body.transform.position = actor.transform.position; body.Move(delta, body.WalkSpeed);
                        Vector3 error = actor.transform.position - next; error.y = 0;
                        if (error.magnitude > .15f || Mathf.Abs(actor.transform.position.y - y) > .15f) { handle.Fail("body_collision_or_height", actor.transform.position); yield break; }
                        if (!Floor(actor.transform.position, route, actor, out float actualFloor) || Mathf.Abs(actor.transform.position.y - actualFloor) > .15f) { handle.Fail("actual_footprint_floor", actor.transform.position); yield break; }
                        yield return null;
                    }
                }
                for (int i = 0; i < 5; i++)
                {
                    if (handle.Cancelled || body == null) yield break;
                    cc.Move(Vector3.down * .06f); body.transform.position = actor.transform.position; yield return null;
                }
                if (handle.Cancelled || body == null) yield break;
                // Physics may depenetrate the controller after a yielded frame.
                // Keep the body at that actual foot before attempting a nav rebind.
                body.transform.position = actor.transform.position;
                Vector3 apart = actor.transform.position - attachEnd; apart.y = 0;
                pass = apart.magnitude < .12f && Floor(actor.transform.position, route, actor, out var floor) && Mathf.Abs(actor.transform.position.y - floor) < .08f;
                if (!pass) handle.Fail("end_attachment_or_floor", actor.transform.position);
            }
            finally
            {
                if (handle != null) handle.Release();
                else if (actor != null) { if (cc != null) cc.enabled = false; UnityEngine.Object.Destroy(actor); }
                finished(pass);
            }
        }
    }
}
