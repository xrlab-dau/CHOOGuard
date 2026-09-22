#if UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using System.IO;
using ChooGuard.Contracts;
using ChooGuard.Editor;
using ChooGuard.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Tests.EditMode.Stories
{
    public sealed class CSWORLD0101Tests
    {
        private Scene scene;
        [SetUp] public void SetUp() { scene = FixtureBuilder.CreatePreviewFixture(); }
        [TearDown] public void TearDown() { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
        private WorldEntityAnchor[] Anchors => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<WorldEntityAnchor>(true)).ToArray();

        [Test] public void Fixture_HasFixedMetreCoordinatesAndSyntheticLabels()
        {
            var anchors = Anchors;
            Assert.That(anchors.Length, Is.EqualTo(4));
            Assert.That(anchors.Count(a => a.Representation == WorldRepresentationKind.Space), Is.EqualTo(2));
            Assert.That(anchors.Single(a => a.EntityId.Value == "fixture.space.a").transform.position, Is.EqualTo(new Vector3(-3, .1f, 0)));
            Assert.That(anchors.Single(a => a.EntityId.Value == "fixture.space.b").transform.position, Is.EqualTo(new Vector3(3, .1f, 0)));
            Assert.That(anchors.Single(a => a.EntityId.Value == "fixture.door").transform.position, Is.EqualTo(new Vector3(0, 1, 0)));
            var metre = anchors.Single(a => a.Representation == WorldRepresentationKind.MetreReference);
            Assert.That(metre.transform.position, Is.EqualTo(new Vector3(0, .5f, -4)));
            Assert.That(metre.GetComponentInChildren<Renderer>().bounds.size, Is.EqualTo(Vector3.one));
            Assert.That(metre.GetComponent<BoxCollider>().size, Is.EqualTo(Vector3.one));
            foreach (var anchor in anchors)
            {
                Assert.That(anchor.FrameId.Value, Is.EqualTo(FixtureBuilder.FrameId));
                Assert.That(anchor.QualificationLabel, Is.EqualTo("SYNTHETIC_FIXTURE / NOT_FIELD_APPROVED"));
                Assert.That(anchor.Unit, Is.EqualTo(SiUnit.Metre));
            }
            Assert.DoesNotThrow(() => FixtureBuilder.ValidateScene(scene));
        }

        [Test] public void DuplicateAnchor_IsRejected()
        {
            var duplicate = UnityEngine.Object.Instantiate(Anchors[0].gameObject);
            SceneManager.MoveGameObjectToScene(duplicate, scene);
            Assert.Throws<InvalidOperationException>(() => FixtureBuilder.ValidateScene(scene));
        }

        [TestCase(-1f)] [TestCase(0f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidScale_IsRejected(float scale)
        {
            // Unity Transform setter가 NaN을 먼저 거부할 수 있으므로 입력 경계를 직접 검사한다.
            Assert.Throws<ArgumentOutOfRangeException>(() => WorldEntityAnchor.ValidateScale(new Vector3(scale, 1, 1)));
        }

        [Test] public void NegativeSceneScale_IsRejected()
        {
            Anchors[0].transform.localScale = new Vector3(-1, 1, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => FixtureBuilder.ValidateScene(scene));
        }

        [TestCase("Assets/ChooGuard/Scenes/Bootstrap.unity")]
        [TestCase("Assets/ChooGuard/Scenes/../Scenes/OperationsFixture.unity")]
        public void ProtectedTarget_IsRejectedWithoutSaving(string path)
        {
            Assert.Throws<ArgumentException>(() => FixtureBuilder.ValidateTargetPath(path));
        }

        [Test] public void ExistingTarget_IsRejectedByPreflightWithoutSaving()
        {
            Assert.Throws<InvalidOperationException>(() => FixtureBuilder.ValidateTargetPath(FixtureBuilder.TargetPath, true));
        }

        [Test] public void PreviewCreationAndCleanup_PreserveOpenSceneStateAndTargetBytes()
        {
            var active = SceneManager.GetActiveScene();
            var ordinary = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var paths = ordinary.Select(x => x.path).ToArray();
            var dirty = ordinary.Select(x => x.isDirty).ToArray();
            var roots = ordinary.Select(x => x.GetRootGameObjects().Select(r => r.GetInstanceID()).OrderBy(id => id).ToArray()).ToArray();
            var previews = EditorSceneManager.previewSceneCount;
            var target = Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), FixtureBuilder.TargetPath);
            var bytes = ReadOptionalBytes(target);
            var meta = ReadOptionalBytes(target + ".meta");
            Scene created = default;
            try
            {
                created = FixtureBuilder.CreatePreviewFixture();
                Assert.That(EditorSceneManager.IsPreviewScene(created), Is.True);
                Assert.That(created.path, Is.Empty);
                AssertUnchanged();
            }
            finally { if (created.IsValid()) EditorSceneManager.ClosePreviewScene(created); }
            Assert.That(created.IsValid(), Is.False);
            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previews));
            AssertUnchanged();

            void AssertUnchanged()
            {
                Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(active.handle));
                Assert.That(SceneManager.sceneCount, Is.EqualTo(ordinary.Length));
                for (var i = 0; i < ordinary.Length; i++)
                {
                    Assert.That(SceneManager.GetSceneAt(i).handle, Is.EqualTo(ordinary[i].handle));
                    Assert.That(ordinary[i].path, Is.EqualTo(paths[i]));
                    Assert.That(ordinary[i].isDirty, Is.EqualTo(dirty[i]));
                    Assert.That(ordinary[i].GetRootGameObjects().Select(r => r.GetInstanceID()).OrderBy(id => id), Is.EqualTo(roots[i]));
                }
                Assert.That(ReadOptionalBytes(target), Is.EqualTo(bytes));
                Assert.That(ReadOptionalBytes(target + ".meta"), Is.EqualTo(meta));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void DirtyUntitledScene_PreviewAndBuildPreflightPreserveUserState(bool buildSavedFixture)
        {
            // Borrow only an already dirty Untitled scene; never replace/clean the runner's scene.
            // The isolated runner must supply this precondition; an ignore is not acceptance.
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded || EditorSceneManager.IsPreviewScene(active) ||
                !string.IsNullOrEmpty(active.path) || !active.isDirty)
                Assert.Ignore("Requires an existing active dirty Untitled scene; do not replace the runner scene.");

            var originalRoots = active.GetRootGameObjects().Select(x => x.GetInstanceID()).OrderBy(x => x).ToArray();
            var sentinel = new GameObject("World fixture lifecycle sentinel");
            var sentinelId = sentinel.GetInstanceID();
            var roots = active.GetRootGameObjects().Select(x => x.GetInstanceID()).OrderBy(x => x).ToArray();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var count = SceneManager.sceneCount;
            var previews = EditorSceneManager.previewSceneCount;
            var target = Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), FixtureBuilder.TargetPath);
            var targetBytes = ReadOptionalBytes(target);
            var metaBytes = ReadOptionalBytes(target + ".meta");
            Scene created = default;
            try
            {
                if (buildSavedFixture)
                {
                    var error = Assert.Throws<InvalidOperationException>(() => FixtureBuilder.BuildOperationsFixture());
                    StringAssert.Contains("dirty or unsaved", error.Message);
                }
                else
                {
                    created = FixtureBuilder.CreatePreviewFixture();
                    Assert.That(EditorSceneManager.IsPreviewScene(created), Is.True);
                    Assert.That(created.path, Is.Empty);
                    Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previews + 1));
                    Assert.That(created.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<WorldEntityAnchor>()).Count(), Is.EqualTo(4));
                    AssertUserSceneUnchanged();
                    EditorSceneManager.ClosePreviewScene(created);
                    Assert.That(created.IsValid(), Is.False);
                }
                AssertUserSceneUnchanged();
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previews));
                Assert.That(ReadOptionalBytes(target), Is.EqualTo(targetBytes));
                Assert.That(ReadOptionalBytes(target + ".meta"), Is.EqualTo(metaBytes));
            }
            finally
            {
                if (created.IsValid()) EditorSceneManager.ClosePreviewScene(created);
                UnityEngine.Object.DestroyImmediate(sentinel); // Only this test's object is owned here.
            }
            Assert.That(active.GetRootGameObjects().Select(x => x.GetInstanceID()).OrderBy(x => x), Is.EqualTo(originalRoots));
            Assert.That(active.isDirty, Is.True);

            void AssertUserSceneUnchanged()
            {
                Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(active.handle));
                Assert.That(active.IsValid() && active.isLoaded && active.isDirty, Is.True);
                Assert.That(active.path, Is.Empty);
                Assert.That(sentinel != null && sentinel.GetInstanceID() == sentinelId, Is.True);
                Assert.That(sentinel.scene.handle, Is.EqualTo(active.handle));
                Assert.That(active.GetRootGameObjects().Select(x => x.GetInstanceID()).OrderBy(x => x), Is.EqualTo(roots));
                Assert.That(SceneManager.sceneCount, Is.EqualTo(count));
                var after = EditorSceneManager.GetSceneManagerSetup();
                Assert.That(after.Length, Is.EqualTo(setup.Length));
                for (var i = 0; i < setup.Length; i++)
                {
                    Assert.That(after[i].path, Is.EqualTo(setup[i].path));
                    Assert.That(after[i].isLoaded, Is.EqualTo(setup[i].isLoaded));
                    Assert.That(after[i].isActive, Is.EqualTo(setup[i].isActive));
                }
            }
        }

        private static byte[] ReadOptionalBytes(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

        [Test] public void InvalidIdentity_IsRejectedBeforeAnchorInitialization()
        {
            var go = new GameObject("잘못된 앵커");
            SceneManager.MoveGameObjectToScene(go, scene);
            var anchor = go.AddComponent<WorldEntityAnchor>();
            Assert.Throws<ArgumentException>(() => anchor.Initialize(default, new StableId(FixtureBuilder.FrameId), WorldRepresentationKind.Space));
            Assert.Throws<ArgumentException>(() => anchor.Initialize(new StableId("valid"), default, WorldRepresentationKind.Space));
        }
    }
}
#endif
