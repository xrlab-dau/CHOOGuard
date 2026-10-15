using System;
using System.Collections.Generic;
using System.IO;
using ChooGuard.Contracts;
using ChooGuard.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor
{
    /// <summary>공개 도면이나 현장 치수가 아닌 고정 합성 fixture. 모든 좌표와 크기는 metre다.</summary>
    public static class FixtureBuilder
    {
        public const string TargetPath = "Assets/ChooGuard/Scenes/OperationsFixture.unity";
        public const string FrameId = "fixture.frame.metres.v1";

        [MenuItem("ChooGuard/Fixture/Create synthetic operations fixture")]
        public static void BuildOperationsFixture()
        {
            // Reject before any scene/object/asset mutation; never save or replace user scenes.
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (!EditorSceneManager.IsPreviewScene(open) && (open.isDirty || string.IsNullOrEmpty(open.path)))
                    throw new InvalidOperationException("Cannot build fixture while a dirty or unsaved ordinary scene is open.");
            }
            ValidateTargetPath(TargetPath);
            var active = SceneManager.GetActiveScene();
            var scene = CreateInMemoryFixture();
            try
            {
                // 생성 중 target이 생겼더라도 기존 scene을 덮지 않는다.
                ValidateTargetPath(TargetPath);
                if (!EditorSceneManager.SaveScene(scene, TargetPath)) throw new IOException("합성 fixture scene 저장에 실패했습니다.");
            }
            finally
            {
                if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }

        /// <summary>저장하지 않는 검사 전용 preview. 활성 scene 변경이나 일반 scene 생성에 의존하지 않는다.</summary>
        public static Scene CreatePreviewFixture()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                PopulateFixture(scene);
                ValidateScene(scene);
                return scene;
            }
            catch { EditorSceneManager.ClosePreviewScene(scene); throw; }
        }

        /// <summary>저장 가능한 별도 일반 scene을 만든다. Unity 일반 scene 생성 제약을 따른다.</summary>
        public static Scene CreateInMemoryFixture()
        {
            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                PopulateFixture(scene);
                ValidateScene(scene);
                return scene;
            }
            catch { EditorSceneManager.CloseScene(scene, true); throw; }
            finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); }
        }

        /// <summary>knownExisting은 파일을 쓰지 않는 충돌 반례용이며 실제 존재 검사를 우회하지 않는다.</summary>
        public static void ValidateTargetPath(string path, bool knownExisting = false)
        {
            if (!string.Equals(path, TargetPath, StringComparison.Ordinal))
                throw new ArgumentException("정확한 합성 fixture target만 생성할 수 있습니다.", nameof(path));
            var fullPath = Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), TargetPath);
            if (knownExisting || File.Exists(fullPath) || Directory.Exists(fullPath) || File.Exists(fullPath + ".meta") ||
                !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                throw new InvalidOperationException("기존 target이나 meta를 덮어쓸 수 없습니다.");
        }

        public static void ValidateScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("로드된 scene이 필요합니다.", nameof(scene));
            var ids = new HashSet<StableId>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var anchor in root.GetComponentsInChildren<WorldEntityAnchor>(true))
                {
                    if (!ids.Add(anchor.EntityId)) throw new InvalidOperationException("중복 entity anchor입니다.");
                    if (!anchor.FrameId.Equals(new StableId(FrameId))) throw new ArgumentException("합성 fixture frame이 다릅니다.");
                    anchor.ValidateGeometry();
                }
        }

        private static void PopulateFixture(Scene scene)
        {
            var root = MoveNewObject(new GameObject("SYNTHETIC_FIXTURE / NOT_FIELD_APPROVED"), scene);
            // Unity 좌표: X 오른쪽, Y 위쪽, Z 앞쪽. 1 Unity unit = 1 metre.
            CreateAnchor(root.transform, "fixture.space.a", WorldRepresentationKind.Space, new Vector3(-3, .1f, 0), new Vector3(5, .2f, 6));
            CreateAnchor(root.transform, "fixture.space.b", WorldRepresentationKind.Space, new Vector3(3, .1f, 0), new Vector3(5, .2f, 6));
            CreateAnchor(root.transform, "fixture.door", WorldRepresentationKind.Door, new Vector3(0, 1, 0), new Vector3(1, 2, .2f));
            CreateAnchor(root.transform, "fixture.metre", WorldRepresentationKind.MetreReference, new Vector3(0, .5f, -4), Vector3.one);
        }

        // Call only with freshly created, unparented objects. Never move user scene roots.
        private static GameObject MoveNewObject(GameObject created, Scene scene)
        {
            try { SceneManager.MoveGameObjectToScene(created, scene); return created; }
            catch { UnityEngine.Object.DestroyImmediate(created); throw; }
        }

        private static void CreateAnchor(Transform parent, string id, WorldRepresentationKind kind, Vector3 position, Vector3 size)
        {
            var logical = MoveNewObject(new GameObject(id), parent.gameObject.scene);
            logical.transform.SetParent(parent, false);
            logical.transform.localPosition = position;
            // Collider는 논리 root에, Renderer는 별도 자식에 둔다. 문 회전은 collider를 움직이지 않는다.
            logical.AddComponent<BoxCollider>().size = size;
            var visual = MoveNewObject(GameObject.CreatePrimitive(PrimitiveType.Cube), parent.gameObject.scene);
            visual.name = "Visual";
            visual.transform.SetParent(logical.transform, false);
            visual.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            logical.AddComponent<WorldEntityAnchor>().Initialize(new StableId(id), new StableId(FrameId), kind);
        }
    }
}
