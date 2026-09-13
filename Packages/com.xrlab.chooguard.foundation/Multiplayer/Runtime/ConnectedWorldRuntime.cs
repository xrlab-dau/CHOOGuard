using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Foundation.Multiplayer
{
    public sealed class ConnectedWorldRuntime : MonoBehaviour
    {
        public TextAsset DefinitionJson;
        public string SceneRootPath;
        private readonly HashSet<string> loaded = new HashSet<string>();
        private string[] wanted = Array.Empty<string>();
        private bool syncing, allRegions;
        private ConnectedRegionView[] regions = Array.Empty<ConnectedRegionView>();
        private ConnectedPortalBarrier[] barriers = Array.Empty<ConnectedPortalBarrier>();
        private FieldView latestView;
        private readonly Dictionary<string,bool> knownLighting=new Dictionary<string,bool>();
        public ConnectedWorldDefinition Definition { get; private set; }
        public string[] LoadedRegionIds => loaded.OrderBy(x => x).ToArray();
        public bool Loading => syncing;
        public ConnectedRegionView[] RegionViews => regions.ToArray();

        private void Awake()
        {
            Definition = JsonUtility.FromJson<ConnectedWorldDefinition>(DefinitionJson.text);
            Definition.Validate();
        }

        public IEnumerator Prepare(bool server)
        {
            allRegions = server;
            wanted = server ? Definition.Regions.Select(r => r.Id).ToArray() : Definition.RequiredRegions(Definition.StartRegionId);
            yield return Synchronize();
        }

        public void Request(string[] required)
        {
            if (allRegions) return;
            if (required == null || required.Length > 5 || required.Any(id => !Definition.Regions.Any(r => r.Id == id)))
                throw new InvalidDataException("Server requested an invalid region set.");
            wanted = required.Distinct().ToArray();
            if (!syncing && !loaded.SetEquals(wanted)) StartCoroutine(Synchronize());
        }

        private IEnumerator Synchronize()
        {
            syncing = true;
            while (!loaded.SetEquals(wanted))
            {
                var next = wanted.FirstOrDefault(id => !loaded.Contains(id));
                if (next != null)
                {
                    var path = SceneRootPath + "/" + Definition.Region(next).SceneName + ".unity";
                    var existing = SceneManager.GetSceneByPath(path);
                    if (!existing.isLoaded)
                    {
                        var operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
                        if (operation == null) throw new InvalidOperationException("Region is absent from this player: " + next);
                        yield return operation;
                    }
                    loaded.Add(next);
                    Cache();
                    // Dynamic equipment remains hidden until this participant's projection arrives.
                    if (!allRegions) regions.Single(r => r.RegionId == next).Apply(null);
                    if (!allRegions && latestView != null) ApplyView(latestView);
                    continue;
                }
                var previous = loaded.First(id => !wanted.Contains(id));
                var scene = SceneManager.GetSceneByPath(SceneRootPath + "/" + Definition.Region(previous).SceneName + ".unity");
                if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
                loaded.Remove(previous); Cache();
            }
            Physics.SyncTransforms(); syncing = false;
        }

        private void Cache()
        {
            regions = FindObjectsByType<ConnectedRegionView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            barriers = FindObjectsByType<ConnectedPortalBarrier>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        public void ValidateSession(WorldState state)
            => ValidateSessionDefinition(Definition, state);

        public static void ValidateSessionDefinition(ConnectedWorldDefinition definition, WorldState state)
        {
            if (state == null || state.SchemaVersion != 2 || state.SpatialProfileId != definition.ProfileId ||
                state.Entities == null || state.Participants == null || state.Frames == null || state.Frames.Length != definition.Frames.Length ||
                state.Frames.Any(f => f == null || !definition.Frames.Any(d => d.FrameId == f.FrameId)) ||
                state.Frames.Select(f => f.FrameId).Distinct().Count() != state.Frames.Length ||
                !state.Entities.Select(e => e.EntityId).OrderBy(x => x).SequenceEqual(
                    definition.Regions.Select(r => "equipment." + r.Id).OrderBy(x => x)) ||
                state.Participants.Any(p => p == null || !definition.Regions.Any(r => r.Id == p.RegionId)))
                throw new InvalidDataException("Session does not match this connected-world profile.");
            foreach (var expected in definition.Frames)
            {
                var actual = state.Frames.Single(f => f.FrameId == expected.FrameId);
                if (!actual.Origin.Finite || actual.Origin.DistanceSquared(expected.Origin) > 1e-10 || actual.YawDegrees != expected.YawDegrees)
                    throw new InvalidDataException("Static session frame transform differs from its authored profile.");
            }
            foreach (var region in definition.Regions)
            {
                var expected = definition.RegionEquipment(region.Id);
                var actual = state.Entities.Single(e => e.EntityId == expected.EntityId);
                if (actual.RegionId != expected.RegionId || actual.FrameId != expected.FrameId || actual.Kind != expected.Kind ||
                    !actual.Position.Finite || !actual.LocalPosition.Finite || actual.Position.DistanceSquared(expected.Position) > 1e-8 ||
                    actual.LocalPosition.DistanceSquared(expected.LocalPosition) > 1e-8 ||
                    (actual.RequiredRoleId ?? "") != expected.RequiredRoleId)
                    throw new InvalidDataException("Shared equipment is rebound outside the static profile.");
            }
            foreach (var actor in state.Participants)
            {
                var pose = new SpatialPose { Position = actor.Position, RegionId = actor.RegionId, FrameId = actor.FrameId,
                    LocalPosition = actor.LocalPosition, PortalId = actor.PortalId };
                if (!definition.TryLocate(pose, actor.Position, GroundedWorldMotor.Radius, null, out var actual) ||
                    actual.RegionId != actor.RegionId || actual.FrameId != actor.FrameId || !actor.LocalPosition.Finite ||
                    actor.LocalPosition.DistanceSquared(actual.LocalPosition) > 1e-8 || actor.PortalId != actual.PortalId)
                    throw new InvalidDataException("Participant is outside its region/frame.");
            }
        }

        public HashSet<string> ClosedPortals(WorldState state) => new HashSet<string>(Definition.Portals.Where(p =>
            p.LinkedDoorEntityIds.Any(id => !state.Entities.Single(e => e.EntityId == id).Active)).Select(p => p.Id));

        public bool CanOperate(EntityState target, IEnumerable<ParticipantState> present)
            => CanOperateDefinition(Definition, target, present);

        public static bool CanOperateDefinition(ConnectedWorldDefinition definition, EntityState target, IEnumerable<ParticipantState> present)
        {
            if (!target.Active) return true;
            foreach (var portal in definition.Portals.Where(p => p.LinkedDoorEntityIds.Contains(target.EntityId)))
            {
                var a = new Vector3(portal.FromPoint.X, portal.FromPoint.Y, portal.FromPoint.Z);
                var b = new Vector3(portal.ToPoint.X, portal.ToPoint.Y, portal.ToPoint.Z);
                var point = definition.GeometrySchemaVersion == 0 ? new Point3((portal.FromPoint.X + portal.ToPoint.X) / 2,
                    (portal.FromPoint.Y + portal.ToPoint.Y) / 2, (portal.FromPoint.Z + portal.ToPoint.Z) / 2) : portal.ClosurePoint;
                var center = new Vector3(point.X, point.Y, point.Z); var forward = b - a; forward.y = 0; forward.Normalize();
                var right = Vector3.Cross(Vector3.up, forward);
                foreach (var actor in present)
                {
                    var relative = new Vector3(actor.Position.X, actor.Position.Y, actor.Position.Z) - center;
                    if (Mathf.Abs(Vector3.Dot(relative, forward)) < GroundedWorldMotor.Radius + .12f &&
                        Mathf.Abs(Vector3.Dot(relative, right)) < portal.ClearWidth / 2 + GroundedWorldMotor.Radius &&
                        relative.y > (definition.GeometrySchemaVersion == 0 ? 0 : portal.ClosureBottom) - GroundedWorldMotor.Height &&
                        relative.y < (definition.GeometrySchemaVersion == 0 ? 2.3f : portal.ClosureBottom + portal.ClosureHeight)) return false;
                }
            }
            return true;
        }

        public static ObservedPortalState[] ProjectPortals(ConnectedWorldDefinition definition, WorldState state,
            ParticipantState actor, Func<ConnectedPortalDefinition, bool> visible)
        {
            return definition.Portals.Where(p => (p.From == actor.RegionId || p.To == actor.RegionId) &&
                actor.Position.DistanceSquared(new Point3((p.FromPoint.X + p.ToPoint.X) / 2, (p.FromPoint.Y + p.ToPoint.Y) / 2,
                    (p.FromPoint.Z + p.ToPoint.Z) / 2)) <= AuthoritativeShift.InterestRadius * AuthoritativeShift.InterestRadius && visible(p))
                .Select(p => new ObservedPortalState { PortalId = p.Id,
                    Open = p.LinkedDoorEntityIds.All(id => state.Entities.Single(e => e.EntityId == id).Active) }).ToArray();
        }

        public void ApplyServerState(WorldState state)
        {
            foreach (var region in regions) region.Apply(state.Entities.Single(e => e.EntityId == region.EquipmentEntityId));
            foreach (var barrier in barriers)
                barrier.SetOpen(barrier.LinkedDoorEntityIds.All(id => state.Entities.Single(e => e.EntityId == id).Active));
            Physics.SyncTransforms();
        }

        public void ApplyView(FieldView view)
        {
            if (view.SpatialProfileId != Definition.ProfileId || (view.ProtocolVersion != 2 && view.ProtocolVersion != 3))
                throw new InvalidDataException("Connected-world view/profile version mismatch.");
            if (view.ProtocolVersion == 3)
            {
                if (view.Physical?.Frames == null || view.Physical.Frames.Length > Definition.Frames.Length ||
                    view.Physical.Frames.Any(f => f == null || !f.Origin.Finite || !Definition.Frames.Any(a => a.FrameId == f.FrameId && a.YawDegrees == f.YawDegrees && a.Origin.Y == f.Origin.Y)) ||
                    view.Physical.Frames.Select(f => f.FrameId).Distinct().Count() != view.Physical.Frames.Length ||
                    !view.Physical.Frames.Any(f => f.FrameId == view.FrameId && f.ToWorld(view.LocalPosition).DistanceSquared(view.Position) < 1e-7) ||
                    view.Physical.Frames.Any(f => f.FrameId == "world" && f.Origin.DistanceSquared(Definition.Frame("world").Origin) != 0))
                    throw new InvalidDataException("Invalid observed physical frames.");
                foreach (var region in regions)
                {
                    var frame = view.Physical.Frames.SingleOrDefault(f => f.FrameId == region.FrameId);
                    region.gameObject.SetActive(region.FrameId == "world" || frame != null);
                    if (frame != null) region.ApplyFrame(Definition, frame);
                }
            }
            latestView = view;
            Request(view.RequiredRegions);
            foreach (var region in regions) region.Apply(view.Observed.Entities.SingleOrDefault(e => e.EntityId == region.EquipmentEntityId),view.ProtocolVersion==2);
            foreach (var barrier in barriers)
            {
                var observed = view.Portals.SingleOrDefault(p => p.PortalId == barrier.PortalId);
                if (observed != null) barrier.SetOpen(observed.Open);
            }
            if (view.ProtocolVersion == 3)
            {
                foreach (var effect in view.Physical.Regions) knownLighting[effect.RegionId]=effect.LightingOn;
                foreach (var region in regions)
                {
                    if (knownLighting.TryGetValue(region.RegionId,out var enabled)) foreach (var light in region.GetComponentsInChildren<Light>(true)) light.enabled=enabled;
                }
            }
        }
    }
}
