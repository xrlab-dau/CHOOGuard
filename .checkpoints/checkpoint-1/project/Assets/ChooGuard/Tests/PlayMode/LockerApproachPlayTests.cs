#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Shell;
using Object = UnityEngine.Object;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class LockerApproachPlayTests
    {
        private const string BasePath = "workers/twin/output/floor2-whole-layout-20261009/newphoto-scope/";
        private const string LockerPacket = BasePath + "locker-withholding/locker-withholding-packet.json";
        private const string FrontPacket = BasePath + "namcheon-source-front-packet.json";
        private const string FloorProof = BasePath + "locker-withholding/locker-approach-source-floor-proof.json";
        private const string LockerSha = "899e420e338529b4ef9e545748604bd7e89caeb4b96ad282690d3e66657dc3c7";
        private const string FrontSha = "669aca90a32f28c9eb55f86298b2c48db323fa0d1e5612bed8ee6c8889fc726f";
        private const string FloorSha = "09df5e8e48f4a0af2c1920abfed57fa3eca1adc97b35f9d2ffdf2efdce0a4748";
        private const string OutputProof = ".planning/2026-10-09-floor2-completion-pass2/locker-approach-play-proof.json";
        private const float Radius = .3f, Height = 1.72f, Skin = .02f, Step = .05f;

        [UnityTest, Timeout(180000)]
        public IEnumerator FullCapsuleCrossesFormerLockerBankAndNamcheonDoorwayAndReturnsGrounded()
        {
            var bank = ReadPinned(LockerPacket, LockerSha);
            var front = ReadPinned(FrontPacket, FrontSha);
            var floorProof = ReadPinned(FloorProof, FloorSha);
            Assert.That((string)floorProof["status"], Is.EqualTo("PASS"));
            var proof = new JObject
            {
                ["schema"] = "chooguard.locker-approach-physical-play-proof.v1",
                ["started_utc"] = DateTime.UtcNow.ToString("o"), ["passed"] = false,
                ["locker_packet_sha256"] = LockerSha, ["front_packet_sha256"] = FrontSha,
                ["source_floor_preflight_sha256"] = FloorSha,
                ["scene_file_sha256"] = Sha("Assets/ChooGuard/Scenes/FpsStation.unity"),
                ["radius_m"] = Radius, ["height_m"] = Height, ["skin_width_m"] = Skin,
                ["maximum_horizontal_move_m"] = Step, ["initial_spawn_count"] = 0,
                ["traversal_teleports"] = 0, ["controller_disabled_during_traversal"] = false,
                ["source_floor_appended"] = false, ["existing_scene_colliders_modified"] = false,
                ["field_dimensions_verified"] = false
            };
            var samples = new JArray(); proof["samples"] = samples;
            var checkpoints = new JArray(); proof["checkpoints"] = checkpoints;
            GameObject actor = null;
            StationDoor door = null;
            var oldMode = StationDoor.OperatorMode.Auto;
            bool modeChanged = false;
            try
            {
                yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
                yield return null;
                Time.captureFramerate = 30; Time.timeScale = 1;
                door = Object.FindObjectsByType<StationDoor>(FindObjectsSortMode.None)
                    .Single(d => d.Element == "2F-main-front-04-남천할매떡볶이" && d.Index == 0);
                Assert.That(door.Kind, Is.EqualTo(StationDoor.DoorKind.Sliding));
                Assert.That(door.Usable, Is.True); Assert.That(door.Leaves.Length, Is.EqualTo(2));
                var doorPose = front["door_parent_pose"];
                AssertMatrix(door.transform.localToWorldMatrix, Matrix(doorPose["expected_parent_world_matrix"]), "Namcheon source-aligned door parent");
                var originalFields = doorPose["station_fields_before"];
                AssertVector(door.Centre, Vector(originalFields["Centre"]), "preserved source Centre");
                AssertVector(door.Normal, Vector(originalFields["Normal"]), "preserved source Normal");
                AssertVector(door.Along, Vector(originalFields["Along"]), "preserved source Along");

                // Even the disabled bank renderer retains its original root frame: derive V,Y,U there.
                var bankOwner = bank["meshes"][0]["owner_receipt"]["owner"];
                var bankFrame = FindPath((string)bankOwner["path"]).transform;
                AssertMatrix(bankFrame.localToWorldMatrix, Matrix(bankOwner["world_matrix"]), "original locker source frame");
                var floorObject = FindPath((string)floorProof["current_collider"]["owner"]["path"]);
                var sourceFloor = floorObject.GetComponent<MeshCollider>();
                Assert.That(sourceFloor, Is.Not.Null); Assert.That(sourceFloor.enabled, Is.True);
                Assert.That(sourceFloor.isTrigger, Is.False); Assert.That(sourceFloor.convex, Is.False);
                AssertMatrix(sourceFloor.transform.localToWorldMatrix,
                    Matrix(floorProof["current_collider"]["owner"]["world_matrix"]), "existing native floor source frame");
                var floorMesh = sourceFloor.sharedMesh;
                Assert.That(floorMesh, Is.Not.Null);
                Assert.That(floorMesh.vertexCount, Is.EqualTo((int)floorProof["current_collider"]["binary"]["vertex_count"]));
                // Pin this correspondence; altered source parts must be qualified again, never ordinal-rebased.
                var indices = floorMesh.triangles;
                Assert.That(indices.Length / 3, Is.EqualTo((int)floorProof["current_collider"]["binary"]["triangle_count"]));
                foreach (var mapped in floorProof["current_collider"]["source_face_exact_current_triangle_correspondence"])
                {
                    int ordinal = (int)mapped["current_flattened_triangle"];
                    for (int k = 0; k < 3; k++)
                        Assert.That(indices[ordinal * 3 + k], Is.EqualTo((int)mapped["indices"][k]), "qualified existing source-floor triangle");
                }
                proof["existing_floor_owner"] = (string)floorProof["current_collider"]["owner"]["path"];
                proof["existing_floor_source_faces"] = floorProof["current_collider"]["source_face_exact_current_triangle_correspondence"].DeepClone();

                var sourceFront = Vector(bank["recipe"]["source_front_VYU"]);
                var sourceCentre = Vector(bank["recipe"]["source_native_centre_VYU"]);
                float sourceBack = bank["withheld_element"]["geometry"]["edge"].Average(e => (float)e[1]);
                float clearance = Radius + Skin + Step;
                var startNative = sourceFront - Vector3.right * clearance;
                var pastBankNative = new Vector3(sourceBack + clearance, sourceCentre.y, sourceCentre.z);
                var start = bankFrame.TransformPoint(startNative);
                var pastBank = bankFrame.TransformPoint(pastBankNative);
                // +/-1 is aperture-local as in the original physical doorway test, not a world offset.
                var outside = door.World(Vector3.forward);
                var inside = door.World(Vector3.back);
                var points = new[] { start, pastBank, outside, inside, outside, pastBank, start };
                var names = new[] { "spawn_before_former_front", "past_former_back", "door_outside",
                    "door_inside", "door_outside_return", "past_former_back_return", "start_return" };
                var nodes = new JArray();
                for (int i = 0; i < points.Length; i++)
                {
                    AssertVector(points[i], Vector(floorProof["nodes"][i]["world_XYZ"]), "qualified route " + names[i]);
                    nodes.Add(new JObject { ["name"] = names[i], ["world"] = Array(points[i]) });
                    AssertSourceSupport(sourceFloor, points[i]);
                }
                proof["route_nodes"] = nodes;
                proof["clearance_m"] = clearance;
                proof["route_derivation"] = "original bank source front/back and frame; door.World aperture-local z +/-1; continuous reverse";

                oldMode = door.Mode;
                Assert.That(door.RequestButtonOpen(out _), Is.True);
                door.SetMode(StationDoor.OperatorMode.HoldOpen); modeChanged = true;
                yield return new WaitForSeconds(StationDoor.SlideOpenSeconds + .2f);
                Physics.SyncTransforms(); Assert.That(door.Open, Is.GreaterThan(.99f));

                actor = new GameObject("Grounded former locker approach probe");
                var cc = actor.AddComponent<CharacterController>();
                cc.enabled = false;
                cc.height = Height; cc.center = Vector3.up * (Height / 2);
                cc.radius = Radius; cc.stepOffset = .3f; cc.skinWidth = Skin; cc.minMoveDistance = 0;
                actor.transform.position = start + Vector3.up * .04f; // Sole initial placement, before activation.
                proof["initial_spawn_count"] = 1;
                cc.enabled = true;
                for (int i = 0; i < 6; i++) { cc.Move(Vector3.down * .08f); yield return null; }
                Assert.That(cc.isGrounded, Is.True, "initial source floor must ground the full-size controller");
                var initialGrounded = actor.transform.position;
                proof["initial_grounded_position"] = Array(initialGrounded);
                int bankCrossings = 0, doorwayCrossings = 0;
                float maxProgressError = 0, maxFloorError = 0, maxHorizontalMove = 0;
                for (int segment = 0; segment < points.Length - 1; segment++)
                {
                    var segmentStart = points[segment]; var segmentEnd = points[segment + 1];
                    var horizontalSpan = segmentEnd - segmentStart; horizontalSpan.y = 0;
                    int count = Mathf.CeilToInt(horizontalSpan.magnitude / Step);
                    for (int step = 1; step <= count; step++)
                    {
                        var expected = Vector3.Lerp(segmentStart, segmentEnd, step / (float)count);
                        var before = actor.transform.position;
                        var wanted = expected - before; wanted.y = 0;
                        // A blocked controller never accumulates a larger jump to catch up with its route.
                        wanted = Vector3.ClampMagnitude(wanted, Step);
                        maxHorizontalMove = Mathf.Max(maxHorizontalMove, wanted.magnitude);
                        var flags = cc.Move(wanted + Vector3.down * .08f);
                        yield return null;
                        Assert.That(cc.enabled, Is.True); Assert.That(cc.radius, Is.EqualTo(Radius));
                        Assert.That(cc.height, Is.EqualTo(Height)); Assert.That(door.Open, Is.GreaterThan(.99f));
                        var actual = actor.transform.position;
                        var progressError = actual - expected; progressError.y = 0;
                        maxProgressError = Mathf.Max(maxProgressError, progressError.magnitude);
                        maxFloorError = Mathf.Max(maxFloorError, Mathf.Abs(actual.y - expected.y));
                        // At this bound the controller cannot route around the old 6.3m wide bank or a door leaf.
                        Assert.That(progressError.magnitude, Is.LessThan(.08f), "blocked source route segment=" + segment + " sample=" + step + " position=" + actual);
                        Assert.That(Mathf.Abs(actual.y - expected.y), Is.LessThan(.06f), "source floor height changed");
                        Assert.That(cc.isGrounded, Is.True, "ground lost at segment=" + segment + " sample=" + step);
                        Assert.That((flags & CollisionFlags.Below) != 0, Is.True, "every move must contact the existing floor");
                        var floorHit = AssertSourceSupport(sourceFloor, actual);
                        var actualMove = actual - before; actualMove.y = 0;
                        Assert.That(actualMove.magnitude, Is.LessThanOrEqualTo(Step + .002f), "no traversal teleport");
                        samples.Add(new JObject
                        {
                            ["segment"] = segment, ["sample"] = step,
                            ["expected_world"] = Array(expected), ["actual_world"] = Array(actual),
                            ["actual_native_VYU"] = Array(bankFrame.InverseTransformPoint(actual)),
                            ["horizontal_command_m"] = wanted.magnitude, ["horizontal_error_m"] = progressError.magnitude,
                            ["floor_error_m"] = Mathf.Abs(actual.y - floorHit.point.y),
                            ["grounded"] = cc.isGrounded, ["collision_flags"] = (int)flags,
                            ["source_floor_triangle"] = floorHit.triangleIndex, ["source_floor_world"] = Array(floorHit.point)
                        });
                    }
                    AssertVector(actor.transform.position, segmentEnd, "reached continuous checkpoint " + names[segment + 1], .08f, .06f);
                    var bankLocal = bankFrame.InverseTransformPoint(actor.transform.position);
                    var doorLocal = door.Local(actor.transform.position);
                    if (segment == 0)
                    {
                        Assert.That(bankLocal.x, Is.GreaterThan(sourceBack + Radius), "full capsule passed original bank back");
                        bankCrossings++;
                    }
                    if (segment == 5)
                    {
                        Assert.That(bankLocal.x, Is.LessThan(sourceFront.x - Radius), "full capsule returned outside original bank front");
                        bankCrossings++;
                    }
                    if (segment == 2)
                    {
                        Assert.That(doorLocal.z, Is.LessThan(-.9f), "physically entered Namcheon");
                        doorwayCrossings++;
                    }
                    if (segment == 3)
                    {
                        Assert.That(doorLocal.z, Is.GreaterThan(.9f), "physically returned through doorway");
                        doorwayCrossings++;
                    }
                    checkpoints.Add(new JObject
                    {
                        ["name"] = names[segment + 1], ["actual_world"] = Array(actor.transform.position),
                        ["actual_bank_native_VYU"] = Array(bankLocal), ["actual_door_local"] = Array(doorLocal),
                        ["grounded"] = cc.isGrounded
                    });
                }
                Assert.That(bankCrossings, Is.EqualTo(2)); Assert.That(doorwayCrossings, Is.EqualTo(2));
                Assert.That(door.Local(inside).z, Is.LessThan(-.99f));
                AssertVector(actor.transform.position, initialGrounded, "returned physically to original grounded start", .08f, .02f);
                proof["former_bank_crossings"] = bankCrossings;
                proof["doorway_crossings"] = doorwayCrossings;
                proof["sample_count"] = samples.Count;
                proof["maximum_progress_error_m"] = maxProgressError;
                proof["maximum_floor_height_error_m"] = maxFloorError;
                proof["maximum_actual_horizontal_command_m"] = maxHorizontalMove;
                proof["actual_finish"] = Array(actor.transform.position);
                proof["grounded_at_finish"] = cc.isGrounded;
                proof["passed"] = true;
            }
            finally
            {
                if (actor != null) Object.Destroy(actor);
                if (modeChanged && door != null) door.SetMode(oldMode);
                proof["finished_utc"] = DateTime.UtcNow.ToString("o");
                Directory.CreateDirectory(Path.GetDirectoryName(OutputProof));
                File.WriteAllText(OutputProof, proof.ToString());
            }
        }

        private static RaycastHit AssertSourceSupport(MeshCollider floor, Vector3 bottom)
        {
            // Query existing source collider directly. This never creates support or snaps the actor.
            Assert.That(floor.Raycast(new Ray(bottom + Vector3.up * .5f, Vector3.down), out var hit, 1f), Is.True,
                "existing native floor has no support at " + bottom);
            Assert.That(hit.normal.y, Is.GreaterThan(.99f));
            Assert.That(Mathf.Abs(hit.point.y - 7f), Is.LessThan(.001f), "qualified source floor Y7");
            Assert.That(Mathf.Abs(bottom.y - hit.point.y), Is.LessThan(.06f), "full capsule bottom on native source floor");
            return hit;
        }
        private static GameObject FindPath(string path)
        {
            var go = GameObject.Find(path); Assert.That(go, Is.Not.Null, "source owner missing: " + path); return go;
        }
        private static JObject ReadPinned(string path, string expected)
        {
            Assert.That(Sha(path), Is.EqualTo(expected), "source dependency changed: " + path);
            return JObject.Parse(File.ReadAllText(path));
        }
        private static string Sha(string path)
        {
            using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        private static Vector3 Vector(JToken value)
        {
            if (value is JArray) return new Vector3((float)value[0], (float)value[1], (float)value[2]);
            return new Vector3((float)value["x"], (float)value["y"], (float)value["z"]);
        }
        private static JArray Array(Vector3 v) => new JArray(v.x, v.y, v.z);
        private static Matrix4x4 Matrix(JToken rows)
        {
            var m = Matrix4x4.identity;
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) m[r, c] = (float)rows[r][c];
            return m;
        }
        private static void AssertMatrix(Matrix4x4 actual, Matrix4x4 expected, string label)
        {
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++)
                Assert.That(actual[r, c], Is.EqualTo(expected[r, c]).Within(.0005f), label + " matrix " + r + "," + c);
        }
        private static void AssertVector(Vector3 actual, Vector3 expected, string label, float horizontal = .001f, float vertical = .001f)
        {
            var d = actual - expected;
            Assert.That(Mathf.Abs(d.y), Is.LessThan(vertical), label + " vertical");
            d.y = 0; Assert.That(d.magnitude, Is.LessThan(horizontal), label + " horizontal");
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.captureFramerate = 0; Time.timeScale = 1;
            var s = SceneManager.GetSceneByName(SceneFlow.StationScene);
            if (s.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("LockerApproachScratch"));
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }
    }
}
#endif
