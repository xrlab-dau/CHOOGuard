#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Facilities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Tests.PlayMode
{
    // Temporary driver geometry only. No station object, source pose, surveyed
    // route, named shop layout or original source asset is installed by this fixture.
    internal sealed class ArchitectureRuntimeFixture : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly Vector3 Origin = new Vector3(15000, 0, 15000);
        internal readonly BoxCollider Floor;
        internal readonly List<Collider> ApprovedFloors = new List<Collider>();
        internal readonly FirstPersonResponder Staff;
        readonly PersonBody body;
        readonly int previousCapture;
        readonly float previousScale;
        NavMeshDataInstance navigation;
        NavMeshData navigationData;
        internal PersonBody Body=>body;

        internal ArchitectureRuntimeFixture()
        {
            previousCapture = Time.captureFramerate; previousScale = Time.timeScale;
            Time.captureFramerate = 30; Time.timeScale = 1;
            Root = new GameObject("ArchitectureAlgorithmFixture"); Root.transform.position = Origin;
            Floor = Box("ApprovedFixtureFloor", Vector3.down * .1f, new Vector3(10, .2f, 10));
            ApprovedFloors.Add(Floor);
            var actor = new GameObject("ProductionPersonBody"); actor.transform.SetParent(Root.transform, false); actor.SetActive(false);
            body = actor.AddComponent<PersonBody>(); body.enabled = false;
            actor.GetComponent<NavMeshAgent>().enabled = false;
            actor.SetActive(true); Assert.That(body.Agent, Is.Not.Null);
            var staffObject = new GameObject("ProductionResponder"); staffObject.transform.SetParent(Root.transform, false);
            staffObject.transform.localPosition = new Vector3(8, 0, 8);
            Staff = staffObject.AddComponent<FirstPersonResponder>(); Staff.SetExternalInputMode(true); Staff.Pause();
            Staff.GetComponent<CharacterController>().enabled = false;
            Physics.SyncTransforms();
        }

        internal BoxCollider Box(string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name); go.transform.SetParent(Root.transform, false); go.transform.localPosition = position;
            var result = go.AddComponent<BoxCollider>(); result.size = size; return result;
        }

        internal StationWalkingRouteEntry Route(params Vector3[] points) => new StationWalkingRouteEntry
        {
            id = "isolated-installer-route", label = "Algorithm driver fixture",
            proof = "fixture:explicit-source-independent-driver", physicalVerified = true,
            path = points.Select(point => Origin + point).ToArray(),
            floorOwners = ApprovedFloors.Select(floor => NativeLandingWalker.Path(floor.transform)).ToArray()
        };

        internal StationWalkingRoute OwnedNavigationRoute()
        {
            var settings=NavMesh.GetSettingsByID(0);
            Assert.That(settings.agentTypeID,Is.GreaterThanOrEqualTo(0));
            settings.agentRadius=.3f;settings.agentHeight=1.72f;settings.agentClimb=.3f;
            settings.overrideVoxelSize=true;settings.voxelSize=.05f;
            var sources=new List<NavMeshBuildSource>();
            foreach(float side in new[]{-1f,1f})sources.Add(new NavMeshBuildSource
            {
                shape=NavMeshBuildSourceShape.Box,size=new Vector3(2,.2f,4),area=0,
                transform=Matrix4x4.TRS(Origin+new Vector3(side*1.5f,-.1f,0),Quaternion.identity,Vector3.one)
            });
            // Only endpoint pads are navigable. The physical floor remains
            // continuous, so the owned link must dispatch its actual body walker
            // across the middle gap instead of taking an ordinary nav shortcut.
            navigationData=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(Origin+Vector3.up,new Vector3(10,5,10)),Vector3.zero,Quaternion.identity);
            Assert.That(navigationData,Is.Not.Null);navigation=NavMesh.AddNavMeshData(navigationData);Assert.That(navigation.valid,Is.True);
            var route=Route(new Vector3(-1.2f,0,0),new Vector3(-.7f,0,-.6f),new Vector3(.7f,0,-.6f),new Vector3(1.2f,0,0));
            var node=new GameObject("OwnedProductionRouteFixture");node.transform.SetParent(Root.transform,false);
            var owner=node.AddComponent<StationWalkingRoute>();owner.Setup(route);return owner;
        }

        internal void PlaceNavigatingBody(Vector3 point)
        {
            body.enabled=false;body.Agent.enabled=false;body.transform.position=point;
            body.Agent.radius=.3f;body.Agent.height=1.72f;body.Agent.speed=1.3f;
            body.Agent.enabled=true;body.enabled=true;
            Assert.That(body.Agent.Warp(point),Is.True);Assert.That(body.OnNavMesh,Is.True);
        }

        internal IEnumerator Walk(StationWalkingRouteEntry route, bool reverse, bool expectedSuccess, float speed = 1.3f)
        {
            route.Validate(); var points = route.Direction(reverse); var finish = points.Last();
            body.transform.position = points[0] + Vector3.up * .04f;
            body.GoTo(finish, speed); Physics.SyncTransforms();
            bool result = false, finished = false;
            var walker = NativeLandingWalker.Walk(body, route, points, finish, ok => { result = ok; finished = true; });
            var previous = body.transform.position; float largest = 0;
            try
            {
                while (walker.MoveNext())
                {
                    var delta = body.transform.position - previous; delta.y = 0;
                    largest = Mathf.Max(largest, delta.magnitude); previous = body.transform.position;
                    yield return walker.Current;
                }
            }
            finally { (walker as IDisposable)?.Dispose(); }
            Assert.That(finished, Is.True); Assert.That(result, Is.EqualTo(expectedSuccess));
            Assert.That(largest, Is.LessThanOrEqualTo(NativeLandingWalker.MaxStep + .006f), "The real driver must not teleport across a blocker/hole");
            var apart = body.transform.position - finish; apart.y = 0;
            var handle = body.GetComponent<NativeLandingWalkHandle>(); Assert.That(handle, Is.Not.Null);
            if (expectedSuccess)
            {
                Assert.That(apart.magnitude, Is.LessThan(.12f));
                Assert.That(NativeLandingWalker.Floor(body.transform.position, route, body.gameObject, out float y), Is.True);
                Assert.That(Mathf.Abs(body.transform.position.y - y), Is.LessThan(.08f));
                Assert.That(handle.FailurePhase, Is.Empty);
            }
            else
            {
                Assert.That(apart.magnitude, Is.GreaterThan(.12f), "Failure cannot complete by endpoint snapping");
                Assert.That(handle.FailurePhase, Is.Not.Empty);
            }
            yield return null;
            Assert.That(UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(controller => controller.name == "Actual native landing walking controller"), Is.False, "Temporary driver controller leaked");
        }

        internal IEnumerator ResponderRoundtrip()
        {
            var controller = Staff.GetComponent<CharacterController>(); controller.enabled = true;
            float radius = controller.radius, height = controller.height, skin = controller.skinWidth, step = controller.stepOffset;
            Assert.That(radius, Is.EqualTo(.28f).Within(.0001f)); Assert.That(height, Is.EqualTo(1.72f).Within(.0001f));
            Assert.That(skin, Is.EqualTo(.025f).Within(.0001f)); Assert.That(step, Is.EqualTo(.28f).Within(.0001f));
            foreach (float sign in new[] { 1f, -1f })
            {
                Staff.RestorePhysicalPose(Origin + new Vector3(-sign, .04f, 0), sign > 0 ? 90 : 270, 0);
                Assert.That(Staff.Resume(false), Is.True);
                for (int index = 0; index < 12; index++) { Staff.StepInput(Vector2.zero, Vector2.zero, false, false, false, 1f / 60); yield return null; }
                var previous = Staff.transform.position;
                for (int index = 0; index < 66; index++)
                {
                    Assert.That(Staff.StepInput(Vector2.up, Vector2.zero, false, false, false, 1f / 60), Is.True);
                    var delta = Staff.transform.position - previous; delta.y = 0;
                    Assert.That(delta.magnitude, Is.LessThan(.04f)); previous = Staff.transform.position;
                    yield return null;
                }
                Assert.That(Mathf.Abs(Staff.transform.position.x - (Origin.x + sign)), Is.LessThan(.08f));
                Assert.That(controller.isGrounded, Is.True);
            }
            Assert.That(controller.radius, Is.EqualTo(radius)); Assert.That(controller.height, Is.EqualTo(height));
            Assert.That(controller.skinWidth, Is.EqualTo(skin)); Assert.That(controller.stepOffset, Is.EqualTo(step));
            Staff.Pause(); controller.enabled = false;
        }

        internal StationDoor Door(StationDoor.DoorKind kind, StationDoor.DoorUse use, bool paired)
        {
            var go = new GameObject("ProductionDoorFixture"); go.transform.SetParent(Root.transform, false); go.SetActive(false);
            var door = go.AddComponent<StationDoor>(); door.Kind = kind; door.Use = use; door.Label = "Algorithm fixture door";
            door.Element = "isolated-driver"; door.Width = paired ? 1.8f : .9f;
            var leaves = new List<StationDoor.Leaf>();
            foreach (float sign in paired ? new[] { -1f, 1f } : new[] { -1f })
            {
                var pivot = new GameObject("OriginalFixtureLeaf"); pivot.transform.SetParent(go.transform, false);
                bool sliding = kind == StationDoor.DoorKind.Sliding;
                pivot.transform.localPosition = new Vector3(sliding ? sign * .45f : sign * (paired ? .9f : .45f), 0, 0);
                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube); panel.name = "FixtureMeshAndCollider"; panel.transform.SetParent(pivot.transform, false);
                panel.transform.localPosition = new Vector3(sliding ? 0 : -sign * .45f, 1.05f, 0);
                panel.transform.localScale = new Vector3(.9f, 2.1f, .06f);
                leaves.Add(new StationDoor.Leaf { Pivot = pivot.transform, Length = .9f, Swing = -sign * 90,
                    Slide = sliding ? Vector3.right * sign : Vector3.zero });
            }
            door.Leaves = leaves.ToArray(); go.SetActive(true); return door;
        }

        internal IEnumerator DoorRoundtrip(StationDoor door, bool crossOpening)
        {
            yield return null;
            var filters = door.GetComponentsInChildren<MeshFilter>();
            var meshes = filters.Select(filter => filter.sharedMesh).ToArray();
            var vertices = meshes.Select(mesh => mesh.vertices).ToArray();
            var closed = door.Leaves.Select(leaf => leaf.Pivot.localRotation).ToArray();
            var positions = door.Leaves.Select(leaf => leaf.Pivot.localPosition).ToArray();
            if (door.Kind == StationDoor.DoorKind.Sliding) door.SetMode(StationDoor.OperatorMode.HoldOpen);
            else Assert.That(door.TryInteract(Staff, out _), Is.True);
            yield return new WaitForSeconds(1.5f); Physics.SyncTransforms();
            Assert.That(door.Open, Is.GreaterThan(.99f));
            for (int i = 0; i < door.Leaves.Length; i++)
            {
                Assert.That(door.Leaves[i].Pivot.GetComponent<Rigidbody>().isKinematic, Is.True);
                if (door.Kind == StationDoor.DoorKind.Swing)
                {
                    Assert.That(Quaternion.Angle(door.Leaves[i].Pivot.localRotation, closed[i]), Is.GreaterThan(80));
                    Assert.That(door.Leaves[i].Pivot.localPosition, Is.EqualTo(positions[i]));
                }
                else Assert.That(Vector3.Distance(door.Leaves[i].Pivot.localPosition, positions[i]), Is.GreaterThan(.9f));
            }
            if (crossOpening)
            {
                var route = Route(new Vector3(0, 0, -.7f), Vector3.zero, new Vector3(0, 0, .7f));
                bool held = door.Kind == StationDoor.DoorKind.Swing;
                if (held) { Assert.That(Staff.Resume(false), Is.True); Assert.That(door.Holdable(Staff), Is.True); door.BeginHold(Staff); }
                try { yield return Walk(route, false, true); yield return Walk(route, true, true); }
                finally { if (held) { door.EndHold(Staff); Staff.Pause(); } }
            }
            if (door.Kind == StationDoor.DoorKind.Sliding) door.SetMode(StationDoor.OperatorMode.Locked);
            else Assert.That(door.TryInteract(Staff, out _), Is.True);
            yield return new WaitForSeconds(2.8f); Physics.SyncTransforms();
            Assert.That(door.Open, Is.LessThan(.01f));
            for (int i = 0; i < door.Leaves.Length; i++)
            {
                Assert.That(Quaternion.Angle(door.Leaves[i].Pivot.localRotation, closed[i]), Is.LessThan(.001f));
                Assert.That(Vector3.Distance(door.Leaves[i].Pivot.localPosition, positions[i]), Is.LessThan(.0001f));
            }
            for (int i = 0; i < filters.Length; i++)
            {
                Assert.That(filters[i].sharedMesh, Is.SameAs(meshes[i]));
                Assert.That(meshes[i].vertices, Is.EqualTo(vertices[i]));
                Assert.That(filters[i].GetComponent<Collider>().enabled, Is.True);
            }
        }

        public void Dispose()
        {
            if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
            if(navigation.valid)navigation.Remove();
            if(navigationData!=null)UnityEngine.Object.DestroyImmediate(navigationData);
            Physics.SyncTransforms(); Time.captureFramerate = previousCapture; Time.timeScale = previousScale;
        }
    }
}
#endif
