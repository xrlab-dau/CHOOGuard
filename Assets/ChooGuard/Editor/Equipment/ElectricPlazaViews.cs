using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Equipment;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Renders still views of the placed ElectricPlaza equipment inside the twin (batch mode with a GPU device:
    /// unity-batch.sh &lt;clone&gt; method-gfx ChooGuard.Editor.ElectricPlazaViews.Render). The shift's own spawner puts every group's
    /// equipment into FpsStation, a temporary camera looks at chosen items and each view is written to <c>.gate/w4-*.png</c>
    /// of the clone (not part of the repository). It is the evidence that placements sit on their walls facing the room.
    /// </summary>
    public static class ElectricPlazaViews
    {
        private sealed class View
        {
            public string Name, Id;
            public float Distance = 2.4f, Eye = 1.5f, Side, LookHeight = 1.0f, Fov = 60f;
            public bool DoorOpen;
        }

        private static readonly View[] Views =
        {
            new View { Name = "board-closed", Id = "board-hall2f-01", Distance = 1.8f, LookHeight = 1.4f },
            new View { Name = "board-open", Id = "board-hall2f-01", Distance = 1.8f, LookHeight = 1.4f, DoorOpen = true },
            new View { Name = "vending-bank", Id = "vending-hall2f-01", Distance = 3.4f, Side = .6f },
            new View { Name = "kiosk", Id = "kiosk-hall2f-01", Distance = 2.6f },
            new View { Name = "recycling-station", Id = "recycling-hall2f-01", Distance = 3.2f, LookHeight = .6f },
            new View { Name = "hall-wide", Id = "vending-hall2f-01", Distance = 13f, Eye = 1.7f, Fov = 70f, LookHeight = 1.2f },
            new View { Name = "platform", Id = "vending-tracks-01", Distance = 3.6f, Side = .5f },
            new View { Name = "plaza-exit", Id = "recycling-plaza-exit-west-north-01", Distance = 4.5f, LookHeight = .6f },
            new View { Name = "smoking-area", Id = "ash-plaza-exit-west-north-01", Distance = 4f, LookHeight = .6f },
        };

        public static void Render()
        {
            EquipmentBuilder.EnsureStation();
            var catalog = AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(EquipmentBuilder.CatalogPath);
            var root = new GameObject("views");
            var report = EquipmentSpawner.Spawn(catalog, root.transform);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".gate"));
            Directory.CreateDirectory(folder);
            var cameraObject = new GameObject("view-camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.Skybox;
            var texture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = texture;
            foreach (var view in Views)
            {
                var equipment = EquipmentRegistry.Find(view.Id);
                if (equipment == null) { Debug.LogWarning("CG_VIEW missing " + view.Id); continue; }
                var door = equipment.GetComponentInChildren<BoardDoor>();
                if (door != null) door.transform.localRotation = Quaternion.Euler(0, view.DoorOpen ? door.OpenDegrees : 0, 0);
                var t = equipment.transform;
                var target = t.position + Vector3.up * view.LookHeight;
                camera.transform.position = t.position + t.forward * view.Distance + t.right * view.Side + Vector3.up * view.Eye;
                camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position);
                camera.fieldOfView = view.Fov;
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = texture;
                var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                string path = Path.Combine(folder, "w4-" + view.Name + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                // 검은 그림을 걸러 낸다: 평균 밝기.
                var pixels = image.GetPixels32();
                long sum = 0;
                for (int i = 0; i < pixels.Length; i += 97) sum += pixels[i].r + pixels[i].g + pixels[i].b;
                Debug.Log("CG_VIEW " + view.Name + " " + equipment.Id + " at " + t.position.ToString("F1") + " brightness=" + (sum / (pixels.Length / 97 * 3f)).ToString("0.0") + " -> " + path);
                Object.DestroyImmediate(image);
            }
            texture.Release();
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(root);
            Debug.Log("CG_VIEWS_DONE " + report + " views=" + Views.Count(v => EquipmentRegistry.Find(v.Id) != null));
        }
    }
}
