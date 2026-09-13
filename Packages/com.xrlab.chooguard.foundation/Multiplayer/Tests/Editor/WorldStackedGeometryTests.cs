using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Multiplayer.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class WorldStackedGeometryTests
    {
        [Test]
        public void Mixed120BodiesOnTwoActualAuthoredFloorsShareXZContactSpaceWithoutCrossFloorContacts()
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            var root = "Assets/CHOOguardGenerated/WorldMotionTest_stacked_" + Guid.NewGuid().ToString("N");
            ConnectedWorldGeometry geometry = null; ConnectedWorldMotionAdapter adapter = null;
            try
            {
                // A declared geometry stress fixture. The production 13-region profile file is never edited.
                var world = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(ConnectedWorldSceneBuilder.DefinitionPath));
                world.ProfileId += "-stacked-test";
                var upper = world.Region("forecourt_eurasia"); upper.Center.Z -= 36; upper.Hub.Z -= 36; upper.EquipmentPosition.Z -= 36;
                world.Portals.Single(p => p.From == upper.Id).FromPoint.Z -= 36;
                var paths = ConnectedWorldSceneBuilder.Build(root, world);
                foreach (var path in paths.Skip(1)) EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                geometry = ConnectedWorldGeometry.Capture(world, UnityEngine.Object.FindObjectsByType<ConnectedRegionView>(FindObjectsSortMode.None));
                var bodies = new List<WorldBodySpawn>();
                for (var level = 0; level < 2; level++) for (var i = 0; i < 60; i++)
                {
                    var p = new Point3(110 + (i % 10) * .9f, level == 0 ? -4 : 0, -2.5f + (i / 10));
                    bodies.Add(new WorldBodySpawn { BodyId = "body-" + bodies.Count.ToString("D3"), RadiusM = bodies.Count < 100 ? WorldBodyPresentation.NpcRadiusM : .3,
                        Pose = world.Pose(level == 0 ? "underground_connector" : upper.Id, p) });
                }
                adapter = new ConnectedWorldMotionAdapter(geometry, bodies.ToArray(), 37);
                var initial = adapter.Capture();
                Assert.That(initial.Crowd.Agents.Select(a => a.ContactSpaceId).Distinct(), Is.EquivalentTo(new[] { "world" }));
                for (var i = 0; i < 5; i++)
                    Assert.That(adapter.TryAdvance(.05, bodies.Select(b => new WorldMotionCommand { BodyId = b.BodyId, DesiredVelocity = new Point3(.15f, 0, .03f) }).ToArray(), out var report), Is.True, report.Failure);
                var final = adapter.Capture();
                Assert.That(final.Crowd.Agents.Count(a => Math.Abs(a.FootElevationM + 4) < .0001), Is.EqualTo(60));
                Assert.That(final.Crowd.Agents.Count(a => Math.Abs(a.FootElevationM) < .0001), Is.EqualTo(60));
                Assert.That(adapter.ValidateBodies(out var failure), Is.True, failure);
            }
            finally
            {
                adapter?.Dispose(); geometry?.Dispose();
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                if (previous.Any(s => s.isLoaded && s.isActive) && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
                AssetDatabase.DeleteAsset(root);
            }
        }
    }
}
